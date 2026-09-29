// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Steamworks;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using Steamworks;
#endif

namespace Hive.Axyl.Payments.Addon.Steam
{
    /// <summary>
    /// Steam Microtransactions plugin for Windows and macOS. Exposes the managed
    /// surface defined by <see cref="ISteamMicrotransactionsPlugin"/>, wrapping the
    /// Steamworks.NET <c>MicroTxnAuthorizationResponse_t</c> callback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller must ensure that <c>SteamAPI.Init()</c> has been called and that
    /// <c>SteamAPI.RunCallbacks()</c> is invoked every frame so that Steamworks
    /// callbacks are dispatched. See the <c>com.com2usplatform.hiveaxyl.steamworks</c> package
    /// README for the full setup guide.
    /// </para>
    /// <para>
    /// The plugin holds no domain state — only the live callback registration handle.
    /// <see cref="MicroTxnAuthorizationResponse"/> is raised on the engine main
    /// thread via the Core <see cref="IDispatcher"/>.
    /// </para>
    /// <para>
    /// Dispose this instance when no longer needed to release the Steamworks
    /// callback registration.
    /// </para>
    /// </remarks>
    public sealed class SteamMicrotransactionsPlugin : ISteamMicrotransactionsPlugin
    {
        private readonly ISteamworksContext m_context;
        private readonly IDispatcher m_dispatcher;
        private readonly object m_lock = new object();
        private Action<SteamMicroTxnResponse>? m_microTxnAuthorizationResponse;
        private volatile bool m_disposed;

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private readonly ISteamMicroTxnCallbackAdapter m_callbackAdapter;
        private IDisposable? m_callbackRegistration;
#endif

        /// <summary>
        /// Creates a <see cref="SteamMicrotransactionsPlugin"/> wired to the given
        /// <see cref="IDispatcher"/> and the platform-appropriate Steamworks context.
        /// Register via
        /// <c>HiveBootstrap.Initialize(config, b =&gt; b.AddSteamMicrotransactions())</c> and resolve
        /// <see cref="ISteamMicrotransactionsPlugin"/>.
        /// </summary>
        /// <param name="dispatcher">The SDK's main-thread dispatcher.</param>
        // Design note: the generated AddSteamMicrotransactions registration passes the
        // dispatcher resolved from the registry (r.Resolve<IDispatcher>()), so
        // construction never touches the not-yet-initialized static HiveCore.
        public SteamMicrotransactionsPlugin(IDispatcher dispatcher)
            : this(SteamworksContext.CreateDefault(), dispatcher)
        {
        }

        internal SteamMicrotransactionsPlugin(ISteamworksContext context, IDispatcher dispatcher)
            : this(context, dispatcher,
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                new SteamMicroTxnCallbackAdapter()
#else
                null
#endif
            )
        {
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        internal SteamMicrotransactionsPlugin(
            ISteamworksContext context,
            IDispatcher dispatcher,
            ISteamMicroTxnCallbackAdapter callbackAdapter)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            m_callbackAdapter = callbackAdapter ?? throw new ArgumentNullException(nameof(callbackAdapter));
        }
#else
        private SteamMicrotransactionsPlugin(ISteamworksContext context, IDispatcher dispatcher, object? _)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }
#endif

        /// <inheritdoc />
        public event Action<SteamMicroTxnResponse> MicroTxnAuthorizationResponse
        {
            add { lock (m_lock) { m_microTxnAuthorizationResponse += value; } }
            remove { lock (m_lock) { m_microTxnAuthorizationResponse -= value; } }
        }

        /// <inheritdoc />
        public Task<SteamMicrotransactionsServiceStartCallbackListenerResult> StartCallbackListenerAsync(
            CancellationToken ct = default)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(SteamMicrotransactionsPlugin));
            }

            if (ct.IsCancellationRequested)
            {
                return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.Failure(
                    new HiveError(HiveErrorCode.Cancelled, "Operation was cancelled.")));
            }

            if (!m_context.IsSupported)
            {
                return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.Failure(
                    new HiveError(HiveErrorCode.Unavailable, "Steam is not supported on this platform.")));
            }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            if (!m_context.IsInitialized)
            {
                return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.Failure(
                    new HiveError(
                        HiveErrorCode.FailedPrecondition,
                        "SteamAPI has not been initialized. Call SteamAPI.Init() before starting the callback listener.",
                        "STEAMWORKS_NOT_INITIALIZED")));
            }

            lock (m_lock)
            {
                // Re-checked inside the critical section: a Dispose() racing past
                // the entry guard must not let Start leak a live registration on a
                // disposed instance.
                if (m_disposed)
                {
                    throw new ObjectDisposedException(nameof(SteamMicrotransactionsPlugin));
                }

                if (m_callbackRegistration != null)
                {
                    return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.AlreadyStarted());
                }

                try
                {
                    m_callbackRegistration = m_callbackAdapter.RegisterCallback(OnMicroTxnCallback);
                }
                catch (Exception ex)
                {
                    return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.Failure(
                        new HiveError(HiveErrorCode.Internal, "Native Steamworks call failed.", ex.GetType().Name)));
                }
            }

            return StartResult(new SteamMicrotransactionsServiceStartCallbackListenerResult.Success(
                new StartCallbackListenerResponse()));
#else
            // Unreachable: IsSupported returns false on non-desktop platforms,
            // so the guard above already returned before reaching this branch.
            throw new InvalidOperationException("unreachable");
#endif
        }

        /// <inheritdoc />
        public Task<SteamMicrotransactionsServiceStopCallbackListenerResult> StopCallbackListenerAsync(
            CancellationToken ct = default)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(SteamMicrotransactionsPlugin));
            }

            // Stop is a cleanup operation, so the cancellation token is deliberately
            // ignored: honoring an already-cancelled token would skip the release
            // and silently leave the listener registered.

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            lock (m_lock)
            {
                ReleaseRegistrationLocked();
            }
#endif

            // Stopping when no listener is registered — or on an unsupported
            // platform, where one can never be registered — succeeds.
            return StopResult(new SteamMicrotransactionsServiceStopCallbackListenerResult.Success(
                new StopCallbackListenerResponse()));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (m_lock)
            {
                if (m_disposed)
                {
                    return;
                }

                m_disposed = true;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                ReleaseRegistrationLocked();
#endif
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private void OnMicroTxnCallback(MicroTxnAuthorizationResponse_t cb)
        {
            // m_bAuthorized is the native uint8; != 0 is the only conversion applied.
            // appId and orderId are forwarded verbatim.
            var response = new SteamMicroTxnResponse(cb.m_unAppID, cb.m_ulOrderID, cb.m_bAuthorized != 0);
            m_dispatcher.Post(() =>
            {
                // A dispatch already queued when Stop or Dispose ran must not reach
                // the game once the listener is unregistered — the event fires only
                // while a listener is registered (interface contract; post-shutdown
                // guard pattern). Subscribers are snapshotted at
                // delivery time for the same reason.
                Action<SteamMicroTxnResponse>? handler;
                lock (m_lock)
                {
                    if (m_disposed || m_callbackRegistration == null)
                    {
                        return;
                    }

                    handler = m_microTxnAuthorizationResponse;
                }

                handler?.Invoke(response);
            });
        }

        // A native failure during unregistration must not abort the surrounding
        // flow (stop or disposal), so it is swallowed and logged. The handle is
        // cleared either way; orderId never reaches the log.
        private void ReleaseRegistrationLocked()
        {
            if (m_callbackRegistration == null)
            {
                return;
            }

            try
            {
                m_callbackRegistration.Dispose();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SteamMicrotransactionsPlugin] Callback unregistration failed: {ex}");
            }

            m_callbackRegistration = null;
        }
#endif

        private static Task<SteamMicrotransactionsServiceStartCallbackListenerResult> StartResult(
            SteamMicrotransactionsServiceStartCallbackListenerResult result)
            => Task.FromResult(result);

        private static Task<SteamMicrotransactionsServiceStopCallbackListenerResult> StopResult(
            SteamMicrotransactionsServiceStopCallbackListenerResult result)
            => Task.FromResult(result);
    }
}
