// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.CredentialManager
{
    /// <summary>
    /// Engine-side orchestration for the CredentialManager addon. Each
    /// <see cref="IAndroidCredentialManagerPlugin"/> call delegates to the
    /// codegen-generated <c>IAndroidCredentialManagerServiceBridge</c> — which
    /// marshals the request, dispatches through Core's <c>INativeBridge</c>, and
    /// parses the response envelope into the typed
    /// <c>AndroidCredentialManagerServiceLoginResult</c> hierarchy. This type
    /// adds only the engine-side policy the bridge does not own: fail-fast
    /// request validation, the single-session guard, and programmatic
    /// cancellation.
    /// </summary>
    /// <remarks>
    /// Constructing this plugin registers the native Credential Manager adapter
    /// with the dispatcher via the generated
    /// <see cref="AndroidCredentialManagerService.Register"/>, so bridge calls
    /// reach it on Android. Off-Android targets (and the Editor) leave the port
    /// unregistered, so <c>HiveCore.Resolve</c> throws
    /// <c>RegistrationNotFoundException</c> before any call — guard with
    /// <c>TryResolve</c> on non-Android targets.
    /// <para>
    /// Cancellation: both the caller's <see cref="CancellationToken"/> and
    /// <see cref="CancelCurrentSession"/> resolve a pending
    /// <see cref="LoginAsync"/> as <c>Failure(HiveErrorCode.Cancelled)</c> —
    /// never a typed Outcome. The generated bridge owns the
    /// cancellation-to-Failure mapping; this plugin only links the tokens. Token
    /// payload fields (id_token, phone, profile picture) are never written to
    /// logs.
    /// </para>
    /// Thread-safety: all members are safe for concurrent use. Login presents UI
    /// and is single-session.
    /// </remarks>
    public sealed class AndroidCredentialManagerPlugin : IAndroidCredentialManagerPlugin
    {
        private readonly IAndroidCredentialManagerServiceBridge m_bridge;

        // Guards m_inflight so the single-session check and the slot assignment
        // are atomic against concurrent LoginAsync callers.
        private readonly object m_sessionLock = new object();
        private CancellationTokenSource? m_inflight;

        /// <summary>
        /// Production constructor. Registers the native plugin with the dispatcher
        /// (generated <see cref="AndroidCredentialManagerService.Register"/>) and
        /// dispatches through a fresh generated bridge. Idempotent — safe to
        /// construct repeatedly.
        /// </summary>
        public AndroidCredentialManagerPlugin()
            : this(new AndroidCredentialManagerServiceBridge())
        {
            AndroidCredentialManagerService.Register();
        }

        internal AndroidCredentialManagerPlugin(IAndroidCredentialManagerServiceBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when the request is empty, an option
        /// declares no/both oneof variants, repeats a variant, or carries an empty
        /// <c>WebClientId</c> / <c>Nonce</c> (fail-fast — App-side construction
        /// errors, not runtime failures).</exception>
        public async Task<AndroidCredentialManagerServiceLoginResult> LoginAsync(
            LoginRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Fail fast at the boundary so no bridge
            // dispatch occurs for a build-time misconfiguration.
            ValidateRequest(request);

            // Single-session: a second concurrent Login returns
            // FailedPrecondition immediately — no bridge dispatch occurs.
            CancellationTokenSource? linked = null;
            try
            {
                lock (m_sessionLock)
                {
                    if (m_inflight != null)
                    {
                        return new AndroidCredentialManagerServiceLoginResult.Failure(
                            new HiveError(
                                HiveErrorCode.FailedPrecondition,
                                "Another LoginAsync is in flight (Login is single-session)."));
                    }

                    // Link the caller's token so either the token or
                    // CancelCurrentSession aborts the same in-flight session; the
                    // generated bridge maps the cancellation to Failure(Cancelled).
                    linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    m_inflight = linked;
                }

                return await m_bridge.LoginAsync(
                    request,
                    new BridgeCallContext { Token = linked.Token }).ConfigureAwait(false);
            }
            finally
            {
                lock (m_sessionLock)
                {
                    if (ReferenceEquals(m_inflight, linked))
                    {
                        m_inflight = null;
                    }
                }

                linked?.Dispose();
            }
        }

        /// <inheritdoc />
        public void CancelCurrentSession()
        {
            CancellationTokenSource? cts;
            lock (m_sessionLock)
            {
                cts = m_inflight;
            }

            // Cancel outside the monitor — Cancel() can run synchronous continuations
            // that take other locks. The awaiting LoginAsync observes the cancellation
            // through the linked token and the generated bridge resolves it to
            // Failure(Cancelled). No-op when no session is active.
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // LoginAsync finished and disposed the source between our read above
                // and this Cancel() — a benign no-op, not an error.
            }
        }

        // ── Request validation ───────────────────────────────────────────────

        private static void ValidateRequest(LoginRequest request)
        {
            IReadOnlyList<CredentialOption> options = request.Options;

            // At least one Option.
            if (options == null || options.Count == 0)
            {
                throw new ArgumentException(
                    "LoginRequest.Options must contain at least one entry.",
                    nameof(request));
            }

            bool sawGoogleId = false;
            bool sawSignInWithGoogle = false;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null)
                {
                    throw new ArgumentException(
                        $"LoginRequest.Options[{i}] is null.", nameof(request));
                }

                bool hasGoogleId = option.GoogleId != null;
                bool hasSignInWithGoogle = option.SignInWithGoogle != null;

                // Exactly one oneof variant per Option.
                if (hasGoogleId == hasSignInWithGoogle)
                {
                    throw new ArgumentException(
                        $"LoginRequest.Options[{i}] must declare exactly one variant " +
                        "('GoogleId' or 'SignInWithGoogle').",
                        nameof(request));
                }

                // Forbid same-variant duplicates so the unified picker UX
                // stays unambiguous.
                if (hasGoogleId)
                {
                    if (sawGoogleId)
                    {
                        throw new ArgumentException(
                            "LoginRequest.Options contains duplicate GoogleId " +
                            "(same-variant duplicates are forbidden).",
                            nameof(request));
                    }
                    sawGoogleId = true;
                    RequireNonEmpty(option.GoogleId!.WebClientId, i, "GoogleId", nameof(GoogleIdOption.WebClientId));
                    RequireNonEmpty(option.GoogleId!.Nonce, i, "GoogleId", nameof(GoogleIdOption.Nonce));
                }
                else
                {
                    if (sawSignInWithGoogle)
                    {
                        throw new ArgumentException(
                            "LoginRequest.Options contains duplicate SignInWithGoogle " +
                            "(same-variant duplicates are forbidden).",
                            nameof(request));
                    }
                    sawSignInWithGoogle = true;
                    RequireNonEmpty(option.SignInWithGoogle!.WebClientId, i, "SignInWithGoogle", nameof(SignInWithGoogleOption.WebClientId));
                    RequireNonEmpty(option.SignInWithGoogle!.Nonce, i, "SignInWithGoogle", nameof(SignInWithGoogleOption.Nonce));
                }
            }
        }

        /// <summary>
        /// Every option variant must declare non-empty
        /// <c>WebClientId</c> and <c>Nonce</c> — build-time configuration errors
        /// that surface as a synchronous <see cref="ArgumentException"/> rather
        /// than a runtime Failure, keeping the nonce-driven replay-attack guard a
        /// hard precondition.
        /// </summary>
        private static void RequireNonEmpty(string value, int index, string variantName, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    $"LoginRequest.Options[{index}] ({variantName}).{fieldName} " +
                    "must be non-empty.",
                    "request");
            }
        }
    }
}
