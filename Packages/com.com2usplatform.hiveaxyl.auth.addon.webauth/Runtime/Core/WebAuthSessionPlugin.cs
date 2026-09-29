// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.WebAuth
{
    /// <summary>
    /// Mechanism Plugin that wraps the platform-provided web auth session API
    /// (iOS/macOS ASWebAuthenticationSession, Android Chrome Custom Tabs,
    /// Windows loopback HTTP, WebGL postMessage) behind the protocol-neutral
    /// <see cref="IExternalUserAgent"/> contract. It enforces the single-session
    /// policy and propagates cancellation; OAuth interpretation and
    /// redirect-URI matching remain the App's responsibility.
    /// </summary>
    /// <remarks>
    /// All backends dispatch natively: iOS/macOS (ASWebAuthenticationSession),
    /// Android (Chrome Custom Tabs), Windows (loopback HTTP), and WebGL
    /// (popup + postMessage).
    /// </remarks>
    public sealed class WebAuthSessionPlugin : IExternalUserAgent
    {
        private readonly IExternalUserAgentServiceBridge m_bridge;

        // Windows-only loopback agent for the two-phase redirect flow; null on
        // every other platform.
        private readonly IWindowsLoopbackAgent? m_windowsLoopback;

        // Guards m_activeSession so the single-session check and the slot
        // assignment are atomic against concurrent OpenAsync callers.
        private readonly object m_sessionLock = new object();

        // Non-null while a session is in flight; cancelling it drives both
        // CancellationToken propagation and CancelCurrentSession.
        private CancellationTokenSource? m_activeSession;

        /// <summary>
        /// Creates the plugin with the platform-default external user agent and,
        /// on Windows standalone, the default loopback redirect agent.
        /// </summary>
        public WebAuthSessionPlugin()
            : this(new ExternalUserAgentServiceBridge(), CreateWindowsLoopbackAgent())
        {
            // Register the native plugins with Core's bridge dispatcher so bridge calls
            // reach them. Idempotent — safe to construct this plugin repeatedly, and a
            // no-op on a build target the service does not ship to.
            //
            // This is the only path that wires an adapter. AxylPluginLoader's
            // ObjC-runtime scan calls the protocol-required register() hook, which is a
            // deliberate no-op in every generated plugin so the scan cannot register a
            // half-wired one. Discovery therefore never substitutes for this call on any
            // platform, which is why every sibling addon asks from its ctor as well.
            ExternalUserAgentService.Register();
            WindowsLoopbackService.Register();
        }

        internal WebAuthSessionPlugin(
            IExternalUserAgentServiceBridge bridge,
            IWindowsLoopbackAgent? windowsLoopback = null)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            m_windowsLoopback = windowsLoopback;
        }

        // The loopback agent is a Windows-only backend: on other build targets
        // the OAuth redirect scheme/origin is fixed and no pre-allocation is
        // needed, so the agent is absent.
        private static IWindowsLoopbackAgent? CreateWindowsLoopbackAgent()
        {
#if UNITY_STANDALONE_WIN
            return new WindowsLoopbackAgent();
#else
            return null;
#endif
        }

        /// <summary>
        /// Windows-only loopback agent for the two-phase OAuth redirect flow:
        /// call <see cref="IWindowsLoopbackAgent.AllocateLoopbackRedirectUriAsync"/>
        /// to reserve the redirect URI before <see cref="OpenAsync"/>.
        /// Null on non-Windows platforms.
        /// </summary>
        public IWindowsLoopbackAgent? WindowsLoopback => m_windowsLoopback;

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <c>Url</c> or <c>RedirectUri</c>
        /// is null/empty (fail-fast — App-side construction error, not a runtime failure).</exception>
        public async Task<ExternalUserAgentServiceOpenResult> OpenAsync(
            OpenRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrEmpty(request.Url))
            {
                throw new ArgumentException(
                    "OpenRequest.Url must be a non-empty authorization URL built by the App / "
                    + "Auth Capability.",
                    nameof(request));
            }

            if (string.IsNullOrEmpty(request.RedirectUri))
            {
                throw new ArgumentException(
                    "OpenRequest.RedirectUri must be a non-empty redirect URI used for platform "
                    + "session configuration.",
                    nameof(request));
            }

            CancellationTokenSource session;
            lock (m_sessionLock)
            {
                if (m_activeSession != null)
                {
                    // Single-session policy: reject the concurrent Open without
                    // disturbing the in-flight one.
                    return new ExternalUserAgentServiceOpenResult.Failure(
                        new HiveError(
                            HiveErrorCode.FailedPrecondition,
                            "An ExternalUserAgent session is already in progress. Only one web auth "
                            + "session may run at a time."));
                }

                // Link the caller's token so either the token or
                // CancelCurrentSession aborts the same in-flight session.
                session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                m_activeSession = session;
            }

            try
            {
                return await m_bridge.OpenAsync(
                    request,
                    new BridgeCallContext { Token = session.Token });
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

            // Cancels the linked token the in-flight OpenAsync handed to the
            // bridge; the pending call resolves with HiveErrorCode.Cancelled.
            // No-op when no session is active.
            try
            {
                session?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The session completed and OpenAsync's finally disposed the
                // CTS in the window between the snapshot under the lock above
                // and this call (the bridge continuation can resume on another
                // thread). The pending call has already resolved, so honoring
                // the documented "safe no-op" contract means swallowing it.
            }
        }
    }
}
