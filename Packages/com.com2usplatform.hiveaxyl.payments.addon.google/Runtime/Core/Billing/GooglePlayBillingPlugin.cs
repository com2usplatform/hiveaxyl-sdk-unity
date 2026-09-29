// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Core.Serialization;

namespace Hive.Axyl.Payments.Addon.Google
{
    /// <summary>
    /// Engine-side orchestration for the Google Play Billing addon. Each
    /// <see cref="IGooglePlayBillingPlugin"/> request/response call delegates to the
    /// codegen-generated <see cref="IGooglePlayBillingServiceBridge"/> — which marshals
    /// the request, dispatches through Core's <see cref="INativeBridge"/>, and parses
    /// the response envelope into the typed <c>GooglePlayBillingService*Result</c> hierarchy.
    /// This type adds only the engine-side concerns the bridge does not own:
    /// argument fail-fast and forwarding the one-way
    /// <c>OnPurchasesUpdated</c> / <c>OnBillingServiceDisconnected</c> native events to
    /// the <see cref="PurchasesUpdated"/> / <see cref="BillingServiceDisconnected"/>
    /// C# events.
    /// <para>
    /// Construction registers the native plugin with the dispatcher via the generated
    /// <see cref="GooglePlayBillingService.Register"/> and takes
    /// <see cref="INativeBridge"/> by injection — the generated <c>AddPlayBilling</c>
    /// registration resolves it from the registry and passes it in — to wire the
    /// native event subscriptions. Register the port via
    /// <c>HiveBootstrap.Initialize(config, b =&gt; b.AddPlayBilling())</c> and resolve
    /// <see cref="IGooglePlayBillingPlugin"/>. In the Editor and on non-Android build
    /// targets the port stays unregistered, so <c>HiveCore.Resolve</c> throws
    /// <see cref="RegistrationNotFoundException"/>.
    /// </para>
    /// <para>
    /// PII: purchase tokens / signatures / original JSON / obfuscated
    /// identifiers are never logged.
    /// </para>
    /// </summary>
    public sealed class GooglePlayBillingPlugin : IGooglePlayBillingPlugin
    {
        // One-way Native Event Bus channels (Plugin -> game). Must match the
        // channel names the native GooglePlayBillingEventEmitter publishes;
        // the prefix is the proto service name (GooglePlayBillingService).
        private const string k_EventPurchasesUpdated = "GooglePlayBillingService.OnPurchasesUpdated";
        private const string k_EventBillingServiceDisconnected = "GooglePlayBillingService.OnBillingServiceDisconnected";

        private readonly IGooglePlayBillingServiceBridge m_bridge;
        private readonly INativeBridge m_nativeBridge;

        // Cache the delegate instances so Subscribe/Unsubscribe pair up.
        private readonly Action<string> m_onPurchasesUpdated;
        private readonly Action<string> m_onBillingServiceDisconnected;

        private int m_disposed;

        public event Action<PurchasesUpdatedEventArgs> PurchasesUpdated;
        public event Action BillingServiceDisconnected;

        /// <summary>
        /// Production constructor. Registers the native plugin with the dispatcher
        /// (generated <see cref="GooglePlayBillingService.Register"/>), dispatches
        /// through a fresh generated bridge, and subscribes to the native events via
        /// the given <see cref="INativeBridge"/>.
        /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddPlayBilling())</c>
        /// rather than constructing directly.
        /// Idempotent on the native side — safe to construct repeatedly.
        /// </summary>
        /// <param name="nativeBridge">The SDK's native bridge.</param>
        // Design note: the generated AddPlayBilling registration passes the bridge that
        // the factory resolves (r.Resolve<INativeBridge>()) eagerly at Register time
        // from the in-progress registry, so construction never touches the
        // not-yet-committed static HiveCore.
        public GooglePlayBillingPlugin(INativeBridge nativeBridge)
            : this(new GooglePlayBillingServiceBridge(), nativeBridge)
        {
            try
            {
                GooglePlayBillingService.Register();
            }
            catch (Exception)
            {
                // The base (internal) constructor already wired the event
                // subscriptions; if registration throws the caller never receives
                // the instance and can never Dispose(), so release the
                // subscriptions here before rethrowing (Dispose is idempotent).
                Dispose();
                throw;
            }
        }

        internal GooglePlayBillingPlugin(IGooglePlayBillingServiceBridge bridge, INativeBridge nativeBridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            m_nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));

            m_onPurchasesUpdated = OnNativePurchasesUpdated;
            m_onBillingServiceDisconnected = OnNativeBillingServiceDisconnected;

            try
            {
                m_nativeBridge.SubscribeEvent(k_EventPurchasesUpdated, m_onPurchasesUpdated);
                m_nativeBridge.SubscribeEvent(k_EventBillingServiceDisconnected, m_onBillingServiceDisconnected);
            }
            catch (Exception)
            {
                // A subscription failed half-wired; the caller never receives the
                // instance and so can never Dispose() — roll back so a failed
                // (possibly retried) construction never leaks a dangling subscriber
                // on the long-lived bridge. Unsubscribe is a no-op for a channel
                // that was never subscribed.
                m_nativeBridge.UnsubscribeEvent(k_EventPurchasesUpdated, m_onPurchasesUpdated);
                m_nativeBridge.UnsubscribeEvent(k_EventBillingServiceDisconnected, m_onBillingServiceDisconnected);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceStartConnectionResult> StartConnectionAsync(CancellationToken ct = default)
            => m_bridge.StartConnectionAsync(new StartConnectionRequest(), Context(ct));

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceEndConnectionResult> EndConnectionAsync(CancellationToken ct = default)
            => m_bridge.EndConnectionAsync(new EndConnectionRequest(), Context(ct));

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceQueryProductDetailsResult> QueryProductDetailsAsync(
            IReadOnlyList<string> productIds,
            ProductType productType,
            CancellationToken ct = default)
        {
            // Fail fast: argument validation throws rather than returning a
            // Result, so a build-time misuse surfaces immediately and no bridge
            // dispatch occurs.
            if (productIds == null || productIds.Count == 0)
            {
                throw new ArgumentException("productIds must not be null or empty.", nameof(productIds));
            }

            return m_bridge.QueryProductDetailsAsync(
                new QueryProductDetailsRequest { ProductIds = productIds, ProductType = productType },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceLaunchBillingFlowResult> LaunchBillingFlowAsync(
            LaunchBillingFlowRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Fail fast: BillingFlowParams requires at least one product (proto
            // "At least one entry required"). Guard at the engine boundary so an empty
            // list surfaces immediately instead of reaching native as a DEVELOPER_ERROR.
            if (request.Products == null || request.Products.Count == 0)
            {
                throw new ArgumentException("request.Products must contain at least one product.", nameof(request));
            }

            return m_bridge.LaunchBillingFlowAsync(request, Context(ct));
        }

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceQueryPurchasesResult> QueryPurchasesAsync(
            ProductType productType,
            CancellationToken ct = default)
            => m_bridge.QueryPurchasesAsync(new QueryPurchasesRequest { ProductType = productType }, Context(ct));

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceAcknowledgePurchaseResult> AcknowledgePurchaseAsync(
            string purchaseToken,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(purchaseToken))
            {
                throw new ArgumentException("purchaseToken must not be null or empty.", nameof(purchaseToken));
            }

            return m_bridge.AcknowledgePurchaseAsync(
                new AcknowledgePurchaseRequest { PurchaseToken = purchaseToken },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<GooglePlayBillingServiceConsumeResult> ConsumeAsync(
            string purchaseToken,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(purchaseToken))
            {
                throw new ArgumentException("purchaseToken must not be null or empty.", nameof(purchaseToken));
            }

            return m_bridge.ConsumeAsync(new ConsumeRequest { PurchaseToken = purchaseToken }, Context(ct));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) != 0)
            {
                return;
            }

            m_nativeBridge.UnsubscribeEvent(k_EventPurchasesUpdated, m_onPurchasesUpdated);
            m_nativeBridge.UnsubscribeEvent(k_EventBillingServiceDisconnected, m_onBillingServiceDisconnected);
        }

        private static BridgeCallContext Context(CancellationToken ct)
            => new BridgeCallContext { Token = ct };

        private void OnNativePurchasesUpdated(string json)
        {
            var handler = PurchasesUpdated;
            if (handler == null)
            {
                return;
            }

            var args = ParsePurchasesUpdatedEvent(json);
            if (args != null)
            {
                handler(args);
            }
        }

        private void OnNativeBillingServiceDisconnected(string json)
        {
            // The disconnect event carries no payload; the addon does not
            // auto-reconnect; it just notifies the game.
            BillingServiceDisconnected?.Invoke();
        }

        // Parses the {"billingResult": {...}, "purchases": [...]} OnPurchasesUpdated
        // payload using the generated DTO readers. Returns null on a malformed
        // payload so a parse failure drops the event rather than throwing into the
        // bridge's event-dispatch loop (the payload is produced by our own native
        // emitter, so this is defensive).
        private static PurchasesUpdatedEventArgs? ParsePurchasesUpdatedEvent(string json)
        {
            try
            {
                var r = new JsonReader(json);
                if (!r.Read() || r.TokenType != JsonTokenType.StartObject)
                {
                    return null;
                }

                BillingResult? billingResult = null;
                var purchases = new List<GooglePurchase>();

                while (r.Read() && r.TokenType != JsonTokenType.EndObject)
                {
                    if (r.TokenEquals("billingResult"))
                    {
                        r.Read();
                        if (r.TokenType == JsonTokenType.Null) { r.Skip(); }
                        else { billingResult = BillingResult.FromJson(r); }
                    }
                    else if (r.TokenEquals("purchases"))
                    {
                        r.Read();
                        if (r.TokenType == JsonTokenType.Null)
                        {
                            r.Skip();
                        }
                        else if (r.TokenType == JsonTokenType.StartArray)
                        {
                            while (r.Read() && r.TokenType != JsonTokenType.EndArray)
                            {
                                purchases.Add(GooglePurchase.FromJson(r));
                            }
                        }
                    }
                    else
                    {
                        r.Read();
                        r.Skip();
                    }
                }

                // A well-formed event always carries the BillingResult; without it
                // there is nothing meaningful to hand the game.
                return billingResult == null ? null : new PurchasesUpdatedEventArgs(billingResult, purchases);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
