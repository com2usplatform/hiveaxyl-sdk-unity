// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Core.Serialization;

namespace Hive.Axyl.Push.Addon.APNS
{
    /// <summary>
    /// iOS / macOS implementation of <see cref="IAPNSPlugin"/>. Delegates the
    /// request/response RPCs to the generated <see cref="IApnsServiceBridge"/>
    /// (which marshals them onto Core's <c>INativeBridge</c> and parses the
    /// response envelope), and forwards the one-way <c>OnTokenRefreshed</c> /
    /// <c>OnNotificationPresented</c> / <c>OnNotificationOpened</c> native events
    /// to the <see cref="TokenRefreshed"/> / <see cref="NotificationPresented"/> /
    /// <see cref="NotificationOpened"/> C# events. Stateless and
    /// non-orchestrating.
    /// <para>
    /// The plugin takes its <see cref="INativeBridge"/> by injection — the generated
    /// <c>AddAPNS</c> registration resolves it from the registry and passes it in —
    /// and uses it to wire the native event subscriptions. Register the port via
    /// <c>HiveBootstrap.Initialize(config, b =&gt; b.AddAPNS())</c> and resolve
    /// <see cref="IAPNSPlugin"/>. In the Editor and on build targets other than iOS
    /// and macOS the port stays unregistered, so <c>HiveCore.Resolve</c> throws
    /// <see cref="RegistrationNotFoundException"/>.
    /// </para>
    /// <para>
    /// PII policy: the device token and notification payloads are
    /// never written to logs.
    /// </para>
    /// </summary>
    public sealed class APNSPlugin : IAPNSPlugin
    {
        // One-way Native Event Bus channels (Plugin -> game). Must match the
        // channel names the native ApnsEventEmitter publishes; the
        // prefix is the proto service name (APNSService).
        private const string k_EventTokenRefreshed = "APNSService.OnTokenRefreshed";
        private const string k_EventNotificationPresented = "APNSService.OnNotificationPresented";
        private const string k_EventNotificationOpened = "APNSService.OnNotificationOpened";

        private const string k_TokenEventKey = "newToken";
        private const string k_NotificationEventKey = "notification";
        private const string k_ActionIdentifierEventKey = "actionIdentifier";

        private readonly IApnsServiceBridge m_bridge;
        private readonly INativeBridge m_nativeBridge;

        // Cache the delegate instances so Subscribe/Unsubscribe pair up.
        private readonly Action<string> m_onTokenRefreshed;
        private readonly Action<string> m_onNotificationPresented;
        private readonly Action<string> m_onNotificationOpened;

        // GetTokenAsync stays pending until the app forwards the
        // registration result. NotifyDidRegisterForRemoteNotifications (surfaced
        // by the native side as the OnTokenRefreshed event) resolves it with the
        // token; NotifyDidFailToRegister fails it with UNAVAILABLE. The native
        // getToken only *triggers* registration and returns an empty token
        // (it caches nothing), so this pending-call correlation lives here.
        private readonly object m_pendingTokenGate = new object();
        private TaskCompletionSource<ApnsServiceGetTokenResult>? m_pendingToken;

        private int m_disposed;

        public event Action<ApnsNotification> NotificationPresented;
        public event Action<ApnsNotificationOpened> NotificationOpened;
        public event Action<string> TokenRefreshed;

        /// <summary>
        /// Creates an <see cref="APNSPlugin"/> wired to the given
        /// <see cref="INativeBridge"/> for its native event subscriptions.
        /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddAPNS())</c>
        /// rather than constructing directly.
        /// </summary>
        /// <param name="nativeBridge">The SDK's native bridge.</param>
        // Design note: the generated AddAPNS registration passes the bridge that the
        // factory resolves (r.Resolve<INativeBridge>()) eagerly at Register time from
        // the in-progress registry, so construction never touches the not-yet-committed
        // static HiveCore.
        public APNSPlugin(INativeBridge nativeBridge)
            : this(new ApnsServiceBridge(), nativeBridge)
        {
            try
            {
                // Register the native APNs plugin with the dispatcher so bridge
                // calls reach it. Idempotent — safe to construct repeatedly.
                ApnsService.Register();
            }
            catch (Exception)
            {
                // The base (internal) constructor already wired the event
                // subscriptions; if registration throws the caller never
                // receives the instance and can never Dispose(), so release the
                // subscriptions here before rethrowing (Dispose is idempotent).
                Dispose();
                throw;
            }
        }

        internal APNSPlugin(IApnsServiceBridge bridge, INativeBridge nativeBridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            m_nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));

            m_onTokenRefreshed = OnNativeTokenRefreshed;
            m_onNotificationPresented = OnNativeNotificationPresented;
            m_onNotificationOpened = OnNativeNotificationOpened;

            try
            {
                m_nativeBridge.SubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
                m_nativeBridge.SubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
                m_nativeBridge.SubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);
            }
            catch (Exception)
            {
                // A subscription threw (e.g. a disposed bridge). The caller never
                // receives the instance, so it can never Dispose() — roll the
                // subscriptions back so a failed construction leaves no dangling
                // subscriber on the long-lived bridge. Unsubscribe is a no-op for
                // a channel that was never subscribed.
                m_nativeBridge.UnsubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
                m_nativeBridge.UnsubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
                m_nativeBridge.UnsubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ApnsServiceGetTokenResult> GetTokenAsync(CancellationToken ct = default)
        {
            // Arm the pending correlation before triggering registration so a fast
            // OnTokenRefreshed cannot arrive before we are listening.
            var pending = new TaskCompletionSource<ApnsServiceGetTokenResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (m_pendingTokenGate)
            {
                // A prior in-flight call is superseded; resolving it as cancelled
                // frees its awaiter rather than leaving it hung.
                m_pendingToken?.TrySetResult(CancelledTokenResult());
                m_pendingToken = pending;
            }

            ApnsServiceGetTokenResult trigger;
            try
            {
                trigger = await m_bridge
                    .GetTokenAsync(new APNSServiceGetTokenRequest(), Context(ct))
                    .ConfigureAwait(false);
            }
            catch
            {
                ClearPendingToken(pending);
                throw;
            }

            // A failure (simulator / missing entitlement / cancellation) is final —
            // no token is forwarded. A non-empty token is already complete
            // (defensive; the native trigger normally returns empty).
            if (trigger is not ApnsServiceGetTokenResult.Success success
                || !string.IsNullOrEmpty(success.Data?.Token))
            {
                ClearPendingToken(pending);
                return trigger;
            }

            // Empty-token success means "registration initiated"; wait for the app
            // to forward the OS result via NotifyDidRegister/NotifyDidFailToRegister.
            using (ct.Register(() => pending.TrySetResult(CancelledTokenResult())))
            {
                // pending is a RunContinuationsAsynchronously TCS: on WebGL,
                // .ConfigureAwait(false) would queue the continuation to a
                // ThreadPool that never runs and this await would hang forever
                // (same hazard as the generated bridge dispatch). Resume on the
                // caller's context there; elsewhere keep .ConfigureAwait(false)
                // and stay off the main thread.
#if UNITY_WEBGL
                return await pending.Task;
#else
                return await pending.Task.ConfigureAwait(false);
#endif
            }
        }

        /// <inheritdoc />
        public Task<ApnsServiceGetProviderEnvironmentResult> GetProviderEnvironmentAsync(CancellationToken ct = default)
            => m_bridge.GetProviderEnvironmentAsync(new APNSServiceGetProviderEnvironmentRequest(), Context(ct));

        /// <inheritdoc />
        public Task<ApnsServiceGetColdStartNotificationResult> GetColdStartNotificationAsync(CancellationToken ct = default)
            => m_bridge.GetColdStartNotificationAsync(new APNSServiceGetColdStartNotificationRequest(), Context(ct));

        /// <inheritdoc />
        public Task<ApnsServiceRequestAuthorizationResult> RequestAuthorizationAsync(
            IReadOnlyList<UNAuthorizationOption> options,
            CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return m_bridge.RequestAuthorizationAsync(
                new APNSServiceRequestAuthorizationRequest { Options = options },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<ApnsServiceSetBadgeCountResult> SetBadgeCountAsync(
            int count, CancellationToken ct = default)
            => m_bridge.SetBadgeCountAsync(
                new APNSServiceSetBadgeCountRequest { Count = count }, Context(ct));

        /// <inheritdoc />
        public Task<ApnsServiceGetNotificationSettingsResult> GetNotificationSettingsAsync(CancellationToken ct = default)
            => m_bridge.GetNotificationSettingsAsync(new APNSServiceGetNotificationSettingsRequest(), Context(ct));

        /// <inheritdoc />
        public Task<ApnsServiceNotifyDidRegisterForRemoteNotificationsResult> NotifyDidRegisterForRemoteNotificationsAsync(
            byte[] deviceToken,
            CancellationToken ct = default)
        {
            if (deviceToken == null)
            {
                throw new ArgumentNullException(nameof(deviceToken));
            }

            return m_bridge.NotifyDidRegisterForRemoteNotificationsAsync(
                new APNSServiceNotifyDidRegisterForRemoteNotificationsRequest { DeviceToken = deviceToken },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<ApnsServiceNotifyDidFailToRegisterResult> NotifyDidFailToRegisterAsync(
            string errorDescription,
            CancellationToken ct = default)
        {
            if (errorDescription == null)
            {
                throw new ArgumentNullException(nameof(errorDescription));
            }

            // A registration failure fails any pending GetTokenAsync.
            FailPendingToken(errorDescription);

            return m_bridge.NotifyDidFailToRegisterAsync(
                new APNSServiceNotifyDidFailToRegisterRequest { ErrorDescription = errorDescription },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<ApnsServiceNotifyWillPresentNotificationResult> NotifyWillPresentNotificationAsync(
            ApnsNotification notification,
            CancellationToken ct = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            return m_bridge.NotifyWillPresentNotificationAsync(
                new APNSServiceNotifyWillPresentNotificationRequest { Notification = notification },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<ApnsServiceNotifyDidReceiveResponseResult> NotifyDidReceiveResponseAsync(
            ApnsNotification notification,
            string actionIdentifier,
            CancellationToken ct = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (actionIdentifier == null)
            {
                throw new ArgumentNullException(nameof(actionIdentifier));
            }

            return m_bridge.NotifyDidReceiveResponseAsync(
                new APNSServiceNotifyDidReceiveResponseRequest
                {
                    Notification = notification,
                    ActionIdentifier = actionIdentifier,
                },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<ApnsServiceNotifyColdStartNotificationResult> NotifyColdStartNotificationAsync(
            string userInfoJson,
            CancellationToken ct = default)
        {
            if (userInfoJson == null)
            {
                throw new ArgumentNullException(nameof(userInfoJson));
            }

            return m_bridge.NotifyColdStartNotificationAsync(
                new APNSServiceNotifyColdStartNotificationRequest { UserInfoJson = userInfoJson },
                Context(ct));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) != 0)
            {
                return;
            }

            m_nativeBridge.UnsubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
            m_nativeBridge.UnsubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
            m_nativeBridge.UnsubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);

            // Release a pending GetTokenAsync so its awaiter does not hang past Dispose.
            FailPendingToken("APNSPlugin was disposed before registration completed.");
        }

        private static BridgeCallContext Context(CancellationToken ct)
            => new BridgeCallContext { Token = ct };

        private void OnNativeTokenRefreshed(string json)
        {
            // Parse unconditionally: even with no TokenRefreshed subscriber the
            // token must resolve a pending GetTokenAsync.
            string? token = ParseTokenEvent(json);
            if (token == null)
            {
                return;
            }

            ResolvePendingToken(token);
            TokenRefreshed?.Invoke(token);
        }

        // Resolves a pending GetTokenAsync with the device token. No-op when no
        // call is pending (e.g. an unsolicited token refresh).
        private void ResolvePendingToken(string token)
        {
            TaskCompletionSource<ApnsServiceGetTokenResult>? pending;
            lock (m_pendingTokenGate)
            {
                pending = m_pendingToken;
                m_pendingToken = null;
            }

            pending?.TrySetResult(new ApnsServiceGetTokenResult.Success(
                new APNSServiceGetTokenResponse { Token = token }));
        }

        // Fails a pending GetTokenAsync with UNAVAILABLE. No-op
        // when no call is pending.
        private void FailPendingToken(string errorDescription)
        {
            TaskCompletionSource<ApnsServiceGetTokenResult>? pending;
            lock (m_pendingTokenGate)
            {
                pending = m_pendingToken;
                m_pendingToken = null;
            }

            pending?.TrySetResult(new ApnsServiceGetTokenResult.Failure(
                new HiveError(HiveErrorCode.Unavailable, errorDescription)));
        }

        private void ClearPendingToken(TaskCompletionSource<ApnsServiceGetTokenResult> pending)
        {
            lock (m_pendingTokenGate)
            {
                if (ReferenceEquals(m_pendingToken, pending))
                {
                    m_pendingToken = null;
                }
            }
        }

        private static ApnsServiceGetTokenResult CancelledTokenResult()
            => new ApnsServiceGetTokenResult.Failure(
                new HiveError(HiveErrorCode.Cancelled, "GetToken was cancelled."));

        private void OnNativeNotificationPresented(string json)
        {
            var handler = NotificationPresented;
            if (handler == null)
            {
                return;
            }

            ApnsNotification? notification = ParseNotificationEvent(json);
            if (notification != null)
            {
                handler(notification);
            }
        }

        private void OnNativeNotificationOpened(string json)
        {
            var handler = NotificationOpened;
            if (handler == null)
            {
                return;
            }

            ApnsNotificationOpened? opened = ParseNotificationOpenedEvent(json);
            if (opened != null)
            {
                handler(opened);
            }
        }

        // Extracts the hex token from the {"newToken": "<hex>"} event payload.
        // Returns null on a malformed payload so a parse failure drops the event
        // rather than throwing into the bridge's event-dispatch loop (the
        // payload is produced by our own native serializer, so this is defensive).
        private static string? ParseTokenEvent(string json)
        {
            try
            {
                var r = new JsonReader(json);
                if (!r.Read() || r.TokenType != JsonTokenType.StartObject)
                {
                    return null;
                }

                while (r.Read() && r.TokenType != JsonTokenType.EndObject)
                {
                    if (r.TokenEquals(k_TokenEventKey))
                    {
                        r.Read();
                        return r.TokenType == JsonTokenType.Null ? null : r.ReadString();
                    }

                    r.Read();
                    r.Skip();
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Splits the {"notification": {…}, "actionIdentifier": "…"} payload of
        // OnNotificationOpened, then decodes the notification with its own generated
        // reader. Returns null when the notification is absent or the payload is
        // malformed — without a notification there is nothing to report.
        //
        // A missing action identifier is NOT a failure: it maps to an empty id and
        // the event still fires, since a tap the SDK cannot label is more useful to
        // a subscriber than no tap at all.
        private static ApnsNotificationOpened? ParseNotificationOpenedEvent(string json)
        {
            if (!JsonReadHelpers.TryReadWrappedPayload(
                    json,
                    k_NotificationEventKey,
                    k_ActionIdentifierEventKey,
                    out string notificationJson,
                    out string actionIdentifier))
            {
                return null;
            }

            try
            {
                return new ApnsNotificationOpened(ApnsNotification.FromJsonString(notificationJson), actionIdentifier);
            }
            catch (Exception)
            {
                // Defensive: the payload is produced by our own native serializer,
                // so a DTO-level parse failure drops the event rather than throwing
                // into the bridge's event-dispatch loop.
                return null;
            }
        }

        // Parses the raw ApnsNotification from a notification event payload.
        // Returns null on a malformed payload (defensive, as above).
        private static ApnsNotification? ParseNotificationEvent(string json)
        {
            try
            {
                return ApnsNotification.FromJsonString(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
