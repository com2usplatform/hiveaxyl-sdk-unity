// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Core.Serialization;

namespace Hive.Axyl.Push.Addon.FCM
{
    /// <summary>
    /// Android implementation of <see cref="IFCMPlugin"/>. Delegates the
    /// request/response RPCs to the generated <see cref="IFcmServiceBridge"/>
    /// (which marshals them onto Core's <c>INativeBridge</c> and parses the
    /// response envelope), and forwards the one-way <c>OnTokenRefreshed</c> /
    /// <c>OnNotificationReceived</c> native events to the
    /// <see cref="TokenRefreshed"/> / <see cref="NotificationReceived"/> C#
    /// events. Stateless and non-orchestrating:
    /// it never persists tokens or registers them with the server.
    /// <para>
    /// The plugin takes its <see cref="INativeBridge"/> by injection — the
    /// generated <c>AddFCM</c> registration resolves it from the registry and
    /// passes it in — and uses it to wire the native event subscriptions.
    /// Register the port via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddFCM())</c>
    /// and resolve <see cref="IFCMPlugin"/>. On non-Android build targets the
    /// port stays unregistered, so <c>HiveCore.Resolve</c> throws
    /// <see cref="RegistrationNotFoundException"/>.
    /// </para>
    /// <para>
    /// PII policy: the device token and message payloads
    /// are never written to logs.
    /// </para>
    /// </summary>
    public sealed class FCMPlugin : IFCMPlugin
    {
        // One-way Native Event Bus channels (Plugin -> game). Must match the
        // channel names the native HiveFirebaseMessagingService publishes;
        // the prefix is the generated plugin name
        // (FcmService).
        private const string k_EventTokenRefreshed = "FcmService.OnTokenRefreshed";
        private const string k_EventNotificationReceived = "FcmService.OnNotificationReceived";

        // Key of the token string in the {"newToken": "<token>"} OnTokenRefreshed
        // event payload (matches the native emitter and the APNs addon).
        private const string k_TokenEventKey = "newToken";

        private readonly IFcmServiceBridge m_bridge;
        private readonly INativeBridge m_nativeBridge;

        // Cache the delegate instances so Subscribe/Unsubscribe pair up.
        private readonly Action<string> m_onTokenRefreshed;
        private readonly Action<string> m_onNotificationReceived;

        private int m_disposed;

        public event Action<FcmRemoteMessage> NotificationReceived;
        public event Action<string> TokenRefreshed;

        /// <summary>
        /// Creates an <see cref="FCMPlugin"/> wired to the given
        /// <see cref="INativeBridge"/> for its native event subscriptions.
        /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddFCM())</c>
        /// rather than constructing directly.
        /// </summary>
        /// <param name="nativeBridge">The SDK's native bridge.</param>
        // Design note: the generated AddFCM registration passes the bridge that the
        // factory resolves (r.Resolve<INativeBridge>()) eagerly at Register time from
        // the in-progress registry, so construction never touches the not-yet-committed
        // static HiveCore.
        public FCMPlugin(INativeBridge nativeBridge)
            : this(new FcmServiceBridge(), nativeBridge, FcmService.Register)
        {
        }

        // Test seam: the native-registration handshake is injected so an EditMode
        // test can drive the Register()-failure rollback without a JNI environment.
        // Production passes FcmService.Register (a no-op off Android).
        internal FCMPlugin(IFcmServiceBridge bridge, INativeBridge nativeBridge, Action registerNative)
            : this(bridge, nativeBridge)
        {
            try
            {
                // Register the native FCM plugin with the dispatcher so bridge
                // calls reach it. Idempotent — safe to construct repeatedly.
                registerNative();
            }
            catch (Exception)
            {
                // The base constructor already wired the event subscriptions; if
                // registration throws the caller never receives the instance and
                // can never Dispose(), so release the subscriptions here before
                // rethrowing (Dispose is idempotent).
                Dispose();
                throw;
            }
        }

        internal FCMPlugin(IFcmServiceBridge bridge, INativeBridge nativeBridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            m_nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));

            m_onTokenRefreshed = OnNativeTokenRefreshed;
            m_onNotificationReceived = OnNativeNotificationReceived;

            try
            {
                m_nativeBridge.SubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
                m_nativeBridge.SubscribeEvent(k_EventNotificationReceived, m_onNotificationReceived);
            }
            catch (Exception)
            {
                // A subscription threw (e.g. a disposed bridge). The caller never
                // receives the instance, so it can never Dispose() — roll the
                // subscriptions back so a failed construction leaves no dangling
                // subscriber on the long-lived bridge. Unsubscribe is a no-op for
                // a channel that was never subscribed.
                m_nativeBridge.UnsubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
                m_nativeBridge.UnsubscribeEvent(k_EventNotificationReceived, m_onNotificationReceived);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<FcmServiceGetTokenResult> GetTokenAsync(CancellationToken ct = default)
            => m_bridge.GetTokenAsync(new GetTokenRequest(), Context(ct));

        /// <inheritdoc />
        public Task<FcmServiceDeleteTokenResult> DeleteTokenAsync(CancellationToken ct = default)
            => m_bridge.DeleteTokenAsync(new DeleteTokenRequest(), Context(ct));

        /// <inheritdoc />
        public Task<FcmServiceGetColdStartMessageResult> GetColdStartMessageAsync(CancellationToken ct = default)
            => m_bridge.GetColdStartMessageAsync(new GetColdStartMessageRequest(), Context(ct));

        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) != 0)
            {
                return;
            }

            m_nativeBridge.UnsubscribeEvent(k_EventTokenRefreshed, m_onTokenRefreshed);
            m_nativeBridge.UnsubscribeEvent(k_EventNotificationReceived, m_onNotificationReceived);
        }

        private static BridgeCallContext Context(CancellationToken ct)
            => new BridgeCallContext { Token = ct };

        private void OnNativeTokenRefreshed(string json)
        {
            var handler = TokenRefreshed;
            if (handler == null)
            {
                return;
            }

            string? token = ParseTokenEvent(json);
            if (token != null)
            {
                handler(token);
            }
        }

        private void OnNativeNotificationReceived(string json)
        {
            var handler = NotificationReceived;
            if (handler == null)
            {
                return;
            }

            FcmRemoteMessage? message = ParseNotificationEvent(json);
            if (message != null)
            {
                handler(message);
            }
        }

        // Extracts the token from the {"newToken": "<token>"} event payload.
        // Returns null on a malformed payload so a parse failure drops the event
        // rather than throwing into the bridge's event-dispatch loop (the payload
        // is produced by our own native serializer, so this is defensive).
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

        // Parses the raw FcmRemoteMessage from the OnNotificationReceived event
        // payload using the generated DTO codec. Returns null on a malformed
        // payload (defensive, as above).
        private static FcmRemoteMessage? ParseNotificationEvent(string json)
        {
            try
            {
                return FcmRemoteMessage.FromJsonString(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
