// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.GPG
{
    /// <summary>
    /// Engine-side orchestration for the GPG addon. Each
    /// <see cref="IGooglePlayGamesPlugin"/> call delegates to the codegen-generated
    /// <c>IGooglePlayGamesServiceBridge</c> — which marshals the request, dispatches
    /// through Core's <c>INativeBridge</c>, and parses the response envelope into the
    /// typed <c>GooglePlayGamesService*Result</c> hierarchy. This type adds only the
    /// engine-side policy the bridge does not own: the single-SignIn-session
    /// guard and programmatic cancellation.
    /// </summary>
    /// <remarks>
    /// Constructing this plugin registers the native Play Games adapter with the
    /// dispatcher via the generated <see cref="GooglePlayGamesService.Register"/>,
    /// so bridge calls reach it on Android; off-Android targets leave the dispatcher
    /// without a registered plugin, so calls surface as a Failure rather than throwing.
    /// <para>
    /// Cancellation: both the caller's <see cref="CancellationToken"/> and
    /// <see cref="CancelCurrentSignIn"/> resolve a pending <see cref="SignInAsync"/>
    /// as <c>Failure(HiveErrorCode.Cancelled)</c> — never a typed Outcome. The
    /// generated bridge owns the cancellation-to-Failure mapping; this plugin
    /// only links the tokens. <c>ServerAuthCode</c> is never written to logs.
    /// </para>
    /// Thread-safety: all members are safe for concurrent use. Only SignIn presents
    /// UI and is single-session; IsAuthenticated / RequestServerSideAccess are UI-less
    /// and re-entrant.
    /// </remarks>
    public sealed class GooglePlayGamesPlugin : IGooglePlayGamesPlugin
    {
        private readonly IGooglePlayGamesServiceBridge m_bridge;

        // Guards m_inflightSignIn so the single-session check and the slot
        // assignment are atomic against concurrent SignInAsync callers. Only
        // SignIn shows UI, so only SignIn is single-session;
        // IsAuthenticated / RequestServerSideAccess are UI-less and re-entrant.
        private readonly object m_sessionLock = new object();
        private CancellationTokenSource? m_inflightSignIn;

        /// <summary>
        /// Production constructor. Registers the native plugin with the dispatcher
        /// (generated <see cref="GooglePlayGamesService.Register"/>) and dispatches
        /// through a fresh generated bridge. Idempotent — safe to construct repeatedly.
        /// </summary>
        public GooglePlayGamesPlugin()
            : this(new GooglePlayGamesServiceBridge())
        {
            GooglePlayGamesService.Register();
        }

        internal GooglePlayGamesPlugin(IGooglePlayGamesServiceBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        public async Task<GooglePlayGamesServiceIsAuthenticatedResult> IsAuthenticatedAsync(
            IsAuthenticatedRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return await m_bridge.IsAuthenticatedAsync(
                request,
                new BridgeCallContext { Token = ct }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        public async Task<GooglePlayGamesServiceSignInResult> SignInAsync(
            SignInRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Single-session: a second concurrent SignIn returns
            // FailedPrecondition immediately — no bridge dispatch occurs.
            CancellationTokenSource? linked = null;
            try
            {
                lock (m_sessionLock)
                {
                    if (m_inflightSignIn != null)
                    {
                        return new GooglePlayGamesServiceSignInResult.Failure(
                            new HiveError(
                                HiveErrorCode.FailedPrecondition,
                                "Another SignInAsync is in flight (single-session)."));
                    }

                    // Link the caller's token so either the token or
                    // CancelCurrentSignIn aborts the same in-flight session; the
                    // generated bridge maps the cancellation to Failure(Cancelled).
                    linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    m_inflightSignIn = linked;
                }

                return await m_bridge.SignInAsync(
                    request,
                    new BridgeCallContext { Token = linked.Token }).ConfigureAwait(false);
            }
            finally
            {
                lock (m_sessionLock)
                {
                    // Guard against clearing a slot a later session already took
                    // (defensive — the single-session policy precludes overlap).
                    if (ReferenceEquals(m_inflightSignIn, linked))
                    {
                        m_inflightSignIn = null;
                    }
                }

                linked?.Dispose();
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <c>WebClientId</c> is null/empty
        /// (fail-fast — an App-side construction error, not a runtime failure).</exception>
        public async Task<GooglePlayGamesServiceRequestServerSideAccessResult> RequestServerSideAccessAsync(
            RequestServerSideAccessRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Fail fast at the boundary so no bridge dispatch occurs for a
            // build-time misconfiguration, independent of how the DTO was constructed.
            if (string.IsNullOrEmpty(request.WebClientId))
            {
                throw new ArgumentException(
                    "WebClientId must not be null or empty.", nameof(request));
            }

            return await m_bridge.RequestServerSideAccessAsync(
                request,
                new BridgeCallContext { Token = ct }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void CancelCurrentSignIn()
        {
            CancellationTokenSource? cts;
            lock (m_sessionLock)
            {
                cts = m_inflightSignIn;
            }

            // Cancel outside the monitor — Cancel() can run synchronous continuations
            // that take other locks. The awaiting SignInAsync observes the cancellation
            // through the linked token and the generated bridge resolves it to
            // Failure(Cancelled). No-op when no session is active.
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // SignInAsync finished and disposed the source between our read above
                // and this Cancel() — the cancel is a benign no-op (the operation
                // already completed), not an error.
            }
        }
    }
}
