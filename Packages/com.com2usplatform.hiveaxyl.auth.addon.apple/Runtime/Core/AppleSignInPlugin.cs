// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.Apple
{
    /// <summary>
    /// Apple Sign-In plugin. Wraps ASAuthorizationAppleIDProvider /
    /// ASAuthorizationController and returns the
    /// raw ASAuthorizationAppleIDCredential fields (userIdentifier,
    /// identityToken, authorizationCode, email, fullName, realUserStatus).
    /// Nonce hashing, id_token validation, and email / fullName persistence
    /// are the App's responsibility.
    /// </summary>
    /// <remarks>
    /// The iOS/macOS native adapter (<c>AppleSignInServiceNativeAdapterDefault</c>)
    /// lives in the HiveAxylAuthAddonApple Swift package and is bundled with this
    /// addon. Constructing this plugin calls <see cref="AppleSignInService.Register"/>,
    /// which registers that adapter with the native dispatcher; the generated bridge
    /// then dispatches each call through Core's <c>INativeBridge</c> and parses the
    /// native response envelope into the typed result. On non-Apple build targets the dispatcher
    /// has no registered plugin, so calls surface as a Failure rather than throwing.
    /// <para>
    /// This plugin owns the single-session policy and programmatic
    /// cancellation, mirroring <c>WebAuthSessionPlugin</c>: only one
    /// <see cref="LoginAsync"/> may be in flight at a time, and both the
    /// caller's <see cref="CancellationToken"/> and <see cref="CancelCurrentSession"/>
    /// resolve the pending call as <see cref="AppleSignInServiceLoginResult.Failure"/>
    /// with <see cref="HiveErrorCode.Cancelled"/>. OS-level dialog dismissal is a
    /// distinct outcome (<see cref="AppleSignInServiceLoginResult.UserCanceled"/>),
    /// classified by the native adapter.
    /// </para>
    /// </remarks>
    public sealed class AppleSignInPlugin : IAppleSignInPlugin
    {
        private readonly IAppleSignInServiceBridge m_bridge;

        // Guards m_activeSession so the single-session check and the slot
        // assignment are atomic against concurrent LoginAsync callers.
        private readonly object m_sessionLock = new object();

        // Non-null while a login is in flight; cancelling it drives both
        // CancellationToken propagation and CancelCurrentSession.
        private CancellationTokenSource? m_activeSession;

        public AppleSignInPlugin()
            : this(new AppleSignInServiceBridge())
        {
            // Register the native Apple plugin with the dispatcher so bridge calls
            // reach it. Idempotent — safe to construct this plugin repeatedly.
            AppleSignInService.Register();
        }

        internal AppleSignInPlugin(IAppleSignInServiceBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <c>NonceHash</c> is null/empty
        /// or when <c>RequestedScopes</c> contains <see cref="RequestedScope.Unspecified"/>
        /// (fail-fast — App-side construction error, not a runtime failure).</exception>
        public async Task<AppleSignInServiceLoginResult> LoginAsync(
            AppleSignInServiceLoginRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrEmpty(request.NonceHash))
            {
                throw new ArgumentException(
                    "AppleSignInServiceLoginRequest.NonceHash must be a non-empty SHA256 hex string. "
                    + "App is responsible for generating the raw nonce and hashing it.",
                    nameof(request));
            }

            if (request.RequestedScopes != null)
            {
                foreach (var scope in request.RequestedScopes)
                {
                    if (scope == RequestedScope.Unspecified)
                    {
                        throw new ArgumentException(
                            "AppleSignInServiceLoginRequest.RequestedScopes must not contain RequestedScope.Unspecified. "
                            + "Use Email and/or FullName, or pass an empty list to omit scopes.",
                            nameof(request));
                    }
                }
            }

            CancellationTokenSource session;
            lock (m_sessionLock)
            {
                if (m_activeSession != null)
                {
                    // Single-session policy: reject the concurrent login without
                    // disturbing the in-flight one. No bridge dispatch
                    // occurs on this path — the rejection is immediate.
                    return new AppleSignInServiceLoginResult.Failure(
                        new HiveError(
                            HiveErrorCode.FailedPrecondition,
                            "An Apple Sign-In session is already in progress. Only one login "
                            + "may run at a time."));
                }

                // Link the caller's token so either the token or
                // CancelCurrentSession aborts the same in-flight session.
                session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                m_activeSession = session;
            }

            try
            {
                return await m_bridge.LoginAsync(
                    request,
                    new BridgeCallContext { Token = session.Token }).ConfigureAwait(false);
            }
            finally
            {
                lock (m_sessionLock)
                {
                    // Guard against clearing a slot a later session already took
                    // (defensive — the single-session policy precludes overlap).
                    if (ReferenceEquals(m_activeSession, session))
                    {
                        m_activeSession = null;
                    }
                }

                session.Dispose();
            }
        }

        /// <inheritdoc />
        public void CancelCurrentSession()
        {
            CancellationTokenSource? session;
            lock (m_sessionLock)
            {
                session = m_activeSession;
            }

            // Cancels the linked token the in-flight LoginAsync handed to the
            // bridge; the pending call resolves with HiveErrorCode.Cancelled.
            // No-op when no session is active.
            try
            {
                session?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The session completed and LoginAsync's finally disposed the
                // CTS in the window between the snapshot under the lock above
                // and this call (the bridge continuation can resume on another
                // thread). The pending call has already resolved, so honoring
                // the documented "safe no-op" contract means swallowing it.
            }
        }
    }
}
