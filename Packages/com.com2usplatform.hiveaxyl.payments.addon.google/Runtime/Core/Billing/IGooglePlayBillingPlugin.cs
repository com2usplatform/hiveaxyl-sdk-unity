// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Payments.Addon.Google
{
    /// <summary>
    /// Façade for the Google Play Billing addon on Android.
    /// Wraps the Billing Library 9 <c>BillingClient</c>
    /// API as a raw surface: the game drives the connection lifecycle, queries
    /// products, launches the purchase flow, and acknowledges / consumes
    /// purchases explicitly.
    /// <para>
    /// Each request/response call delegates to the codegen-generated
    /// <see cref="IGooglePlayBillingServiceBridge"/>, which marshals the request,
    /// dispatches through Core's native bridge, and parses the response envelope into
    /// the typed <c>GooglePlayBillingService*Result</c> hierarchy. The two one-way
    /// <c>OnPurchasesUpdated</c> / <c>OnBillingServiceDisconnected</c> native
    /// events are not request/response RPCs and are surfaced here as the
    /// <see cref="PurchasesUpdated"/> / <see cref="BillingServiceDisconnected"/>
    /// C# events.
    /// </para>
    /// <para>
    /// The plugin is non-orchestrating and stateless: it never persists
    /// purchases and never auto-routes acknowledge vs consume — the game
    /// decides by product type.
    /// </para>
    /// <para>
    /// PII policy: purchase tokens, signatures, original JSON, and
    /// obfuscated account identifiers are never written to logs.
    /// </para>
    /// </summary>
    public interface IGooglePlayBillingPlugin : IDisposable
    {
        /// <summary>
        /// Builds the single <c>BillingClient</c> (with the purchases-updated
        /// listener and pending-purchase support) and connects it
        /// (<c>startConnection</c>). The game calls this explicitly; the addon
        /// never auto-connects. Re-calling while connected yields
        /// <see cref="GooglePlayBillingServiceStartConnectionResult.AlreadyConnected"/>.
        /// </summary>
        Task<GooglePlayBillingServiceStartConnectionResult> StartConnectionAsync(CancellationToken ct = default);

        /// <summary>
        /// Ends the <c>BillingClient</c> connection (<c>endConnection</c>).
        /// Idempotent — calling while disconnected is not an error.
        /// </summary>
        Task<GooglePlayBillingServiceEndConnectionResult> EndConnectionAsync(CancellationToken ct = default);

        /// <summary>
        /// Queries product details for the given IDs and product type
        /// (<c>queryProductDetailsAsync</c>). Fetched products and per-product
        /// failures are both returned raw (v9 partial-failure model).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="productIds"/> is null or empty.</exception>
        Task<GooglePlayBillingServiceQueryProductDetailsResult> QueryProductDetailsAsync(
            IReadOnlyList<string> productIds,
            ProductType productType,
            CancellationToken ct = default);

        /// <summary>
        /// Launches the purchase UI (<c>launchBillingFlow</c>) for the given
        /// request. The foreground Activity is acquired automatically.
        /// Success only means the UI was launched; the purchase result arrives via
        /// <see cref="PurchasesUpdated"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="request"/>.Products is empty.</exception>
        Task<GooglePlayBillingServiceLaunchBillingFlowResult> LaunchBillingFlowAsync(
            LaunchBillingFlowRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Queries the active and unfinished purchases for the product type
        /// (<c>queryPurchasesAsync</c>). Also the Restore surface.
        /// </summary>
        Task<GooglePlayBillingServiceQueryPurchasesResult> QueryPurchasesAsync(
            ProductType productType,
            CancellationToken ct = default);

        /// <summary>
        /// Acknowledges a non-consumable / subscription purchase
        /// (<c>acknowledgePurchase</c>). The addon does not auto-route — the game
        /// chooses acknowledge vs <see cref="ConsumeAsync"/> by product type.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="purchaseToken"/> is null or empty.</exception>
        Task<GooglePlayBillingServiceAcknowledgePurchaseResult> AcknowledgePurchaseAsync(
            string purchaseToken,
            CancellationToken ct = default);

        /// <summary>
        /// Consumes a consumable purchase (<c>consumeAsync</c>); consume includes
        /// the acknowledge effect.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="purchaseToken"/> is null or empty.</exception>
        Task<GooglePlayBillingServiceConsumeResult> ConsumeAsync(
            string purchaseToken,
            CancellationToken ct = default);

        /// <summary>
        /// Raised for every <c>PurchasesUpdatedListener.onPurchasesUpdated</c>
        /// callback — the result of <see cref="LaunchBillingFlowAsync"/> and any
        /// out-of-band purchase (other device, promo redemption, pending →
        /// approved transition). The raw <see cref="PurchasesUpdatedEventArgs"/>
        /// is passed verbatim; the game inspects the response code and processes
        /// the purchases. Marshalled to the engine main thread by
        /// the Core dispatcher.
        /// </summary>
        event Action<PurchasesUpdatedEventArgs> PurchasesUpdated;

        /// <summary>
        /// Raised when the Billing service disconnects
        /// (<c>onBillingServiceDisconnected</c>). The addon does NOT auto-reconnect
        /// — the game decides whether and when to call
        /// <see cref="StartConnectionAsync"/> again. Marshalled to the
        /// engine main thread.
        /// </summary>
        event Action BillingServiceDisconnected;
    }
}
