// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Payments.Addon.Apple
{
    /// <summary>
    /// Apple StoreKit2 in-app purchase plugin. Wraps
    /// StoreKit2 <c>Product</c> / <c>Transaction</c> / <c>AppStore</c> and exposes
    /// raw transaction and product fields without normalization.
    /// <see cref="AppleTransaction.VerificationStatus"/> /
    /// <see cref="AppleTransaction.VerificationError"/> keep both verified and
    /// unverified branches rather than flattening to a bool.
    /// </summary>
    /// <remarks>
    /// The iOS/macOS native adapter (<c>AppleStoreKitServiceNativeAdapterDefault</c>)
    /// lives in the HiveAxylPaymentsAddonApple Swift package and is bundled with this
    /// addon. Constructing the plugin calls <c>AppleStoreKitService.Register</c>, which
    /// registers that adapter with the native dispatcher; the generated bridge then
    /// dispatches each call through Core's <c>INativeBridge</c> and parses the envelope
    /// into the typed result. On non-Apple build targets the dispatcher has no
    /// registered plugin, so calls surface as a Failure rather than throwing.
    /// </remarks>
    public interface IAppleStoreKitPlugin : IDisposable
    {
        /// <summary>
        /// Raised for each transaction delivered on StoreKit's
        /// <c>Transaction.updates</c> stream while the observer is running (start it
        /// via <see cref="StartTransactionObserverAsync"/>, stop via
        /// <see cref="StopTransactionObserverAsync"/>). Dispatched on the engine main
        /// thread. The app inspects each transaction's verification status
        /// and finishes or server-verifies it.
        /// </summary>
        event Action<AppleTransaction> TransactionUpdated;

        /// <summary>Fetches products for the given identifiers.</summary>
        Task<AppleStoreKitServiceGetProductsResult> GetProductsAsync(
            GetProductsRequest request, CancellationToken ct = default);

        /// <summary>Initiates a purchase; OS cancellation surfaces as the
        /// UserCanceled outcome, Ask-to-Buy as Pending.</summary>
        Task<AppleStoreKitServicePurchaseResult> PurchaseAsync(
            PurchaseRequest request, CancellationToken ct = default);

        /// <summary>Subscribes to <c>Transaction.updates</c>; re-invoking while
        /// running returns AlreadyStarted.</summary>
        Task<AppleStoreKitServiceStartTransactionObserverResult> StartTransactionObserverAsync(
            StartTransactionObserverRequest request, CancellationToken ct = default);

        /// <summary>Cancels the <c>Transaction.updates</c> subscription; idempotent.</summary>
        Task<AppleStoreKitServiceStopTransactionObserverResult> StopTransactionObserverAsync(
            StopTransactionObserverRequest request, CancellationToken ct = default);

        /// <summary>Drains <c>Transaction.currentEntitlements</c>.</summary>
        Task<AppleStoreKitServiceGetCurrentEntitlementsResult> GetCurrentEntitlementsAsync(
            GetCurrentEntitlementsRequest request, CancellationToken ct = default);

        /// <summary>Drains <c>Transaction.all</c>.</summary>
        Task<AppleStoreKitServiceGetAllTransactionsResult> GetAllTransactionsAsync(
            GetAllTransactionsRequest request, CancellationToken ct = default);

        /// <summary>Drains <c>Transaction.unfinished</c>.</summary>
        Task<AppleStoreKitServiceGetUnfinishedTransactionsResult> GetUnfinishedTransactionsAsync(
            GetUnfinishedTransactionsRequest request, CancellationToken ct = default);

        /// <summary>Finishes a transaction; unknown id returns TransactionNotFound,
        /// an already-finished one is idempotent.</summary>
        Task<AppleStoreKitServiceFinishTransactionResult> FinishTransactionAsync(
            FinishTransactionRequest request, CancellationToken ct = default);

        /// <summary>Syncs with the App Store to restore purchases.</summary>
        Task<AppleStoreKitServiceSyncResult> SyncAsync(
            SyncRequest request, CancellationToken ct = default);

        /// <summary>Reads the current storefront.</summary>
        Task<AppleStoreKitServiceGetStorefrontResult> GetStorefrontAsync(
            GetStorefrontRequest request, CancellationToken ct = default);
    }
}
