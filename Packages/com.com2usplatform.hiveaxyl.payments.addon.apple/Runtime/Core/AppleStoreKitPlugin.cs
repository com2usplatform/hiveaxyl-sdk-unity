// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Payments.Addon.Apple
{
    /// <summary>
    /// Default <see cref="IAppleStoreKitPlugin"/> implementation. Forwards each call
    /// to the generated <see cref="IAppleStoreKitServiceBridge"/> (which dispatches
    /// through Core's <c>INativeBridge</c> to the Swift adapter) and re-raises the
    /// hand-wired <c>Transaction.updates</c> event as <see cref="TransactionUpdated"/>.
    /// </summary>
    public sealed class AppleStoreKitPlugin : IAppleStoreKitPlugin, IDisposable
    {
        // Must match TransactionObserver.eventName on the Swift side.
        private const string TransactionUpdateEventName = "AppleStoreKitService.OnTransactionUpdate";

        private readonly IAppleStoreKitServiceBridge m_bridge;
        private readonly INativeBridge? m_nativeBridge;
        private readonly Action<string> m_onTransactionUpdate;
        private bool m_disposed;

        /// <inheritdoc />
        public event Action<AppleTransaction>? TransactionUpdated;

        /// <summary>
        /// Creates an <see cref="AppleStoreKitPlugin"/> wired to the given
        /// <see cref="INativeBridge"/> for the <c>Transaction.updates</c> subscription.
        /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddStoreKit())</c>
        /// rather than constructing directly.
        /// </summary>
        /// <param name="nativeBridge">The SDK's native bridge.</param>
        // Design note: the generated AddStoreKit registration passes the bridge that
        // the factory resolves (r.Resolve<INativeBridge>()) eagerly at Register time
        // from the in-progress registry, so construction never touches the
        // not-yet-committed static HiveCore. The earlier self-resolving form left the
        // bridge null inside the bootstrap closure and silently never subscribed.
        public AppleStoreKitPlugin(INativeBridge nativeBridge)
            : this(new AppleStoreKitServiceBridge(), nativeBridge)
        {
            try
            {
                // Register the native plugin so bridge calls reach it. Idempotent.
                // Kept in the public constructor (not the injectable one below) so the
                // behavior tests can construct the façade without binding the native
                // Register symbol — this addon ships no native bundle on the managed
                // test target, so calling it there would DllNotFound.
                AppleStoreKitService.Register();
            }
            catch (Exception)
            {
                // The base constructor already wired the event subscription; a thrown
                // registration means the caller never receives the instance and so can
                // never Dispose(), so release the subscription here before rethrowing.
                Dispose();
                throw;
            }
        }

        internal AppleStoreKitPlugin(IAppleStoreKitServiceBridge bridge, INativeBridge? nativeBridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            // Subscribe to the hand-wired Transaction.updates event bus. On non-Apple
            // targets (or before Core init) the native bridge is unavailable, so the
            // event simply never fires; RPC calls still surface a Failure envelope.
            m_onTransactionUpdate = HandleTransactionUpdate;
            m_nativeBridge = nativeBridge;
            m_nativeBridge?.SubscribeEvent(TransactionUpdateEventName, m_onTransactionUpdate);
        }

        private void HandleTransactionUpdate(string jsonPayload)
        {
            // Raw AppleTransaction JSON emitted by the Swift observer.
            var transaction = AppleTransaction.FromJsonString(jsonPayload);
            TransactionUpdated?.Invoke(transaction);
        }

        /// <summary>Unsubscribes from the Transaction.updates event bus. Idempotent —
        /// a second call (or one after the native bridge was already disposed) is a
        /// no-op rather than re-invoking the bridge.</summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_nativeBridge?.UnsubscribeEvent(TransactionUpdateEventName, m_onTransactionUpdate);
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceGetProductsResult> GetProductsAsync(
            GetProductsRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ProductIds == null || request.ProductIds.Count == 0)
                throw new ArgumentException("productIds must not be empty.", nameof(request));
            return m_bridge.GetProductsAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServicePurchaseResult> PurchaseAsync(
            PurchaseRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.PurchaseAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceStartTransactionObserverResult> StartTransactionObserverAsync(
            StartTransactionObserverRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.StartTransactionObserverAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceStopTransactionObserverResult> StopTransactionObserverAsync(
            StopTransactionObserverRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.StopTransactionObserverAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceGetCurrentEntitlementsResult> GetCurrentEntitlementsAsync(
            GetCurrentEntitlementsRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.GetCurrentEntitlementsAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceGetAllTransactionsResult> GetAllTransactionsAsync(
            GetAllTransactionsRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.GetAllTransactionsAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceGetUnfinishedTransactionsResult> GetUnfinishedTransactionsAsync(
            GetUnfinishedTransactionsRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.GetUnfinishedTransactionsAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceFinishTransactionResult> FinishTransactionAsync(
            FinishTransactionRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.FinishTransactionAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceSyncResult> SyncAsync(
            SyncRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.SyncAsync(request, new BridgeCallContext { Token = ct });
        }

        /// <inheritdoc />
        public Task<AppleStoreKitServiceGetStorefrontResult> GetStorefrontAsync(
            GetStorefrontRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return m_bridge.GetStorefrontAsync(request, new BridgeCallContext { Token = ct });
        }
    }
}
