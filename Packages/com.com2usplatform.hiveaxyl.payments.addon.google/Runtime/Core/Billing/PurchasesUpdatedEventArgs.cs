// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Collections.Generic;

namespace Hive.Axyl.Payments.Addon.Google
{
    /// <summary>
    /// Payload of the <see cref="IGooglePlayBillingPlugin.PurchasesUpdated"/>
    /// event — the raw <c>PurchasesUpdatedListener.onPurchasesUpdated</c>
    /// callback. The game inspects
    /// <see cref="BillingResult"/>.<see cref="Hive.Axyl.Payments.Addon.Google.BillingResult.ResponseCode"/>
    /// (e.g. <c>USER_CANCELED</c>, <c>ITEM_ALREADY_OWNED</c>) and processes
    /// <see cref="Purchases"/> — the SDK does not branch.
    /// </summary>
    public sealed class PurchasesUpdatedEventArgs
    {
        /// <summary>The raw <see cref="Hive.Axyl.Payments.Addon.Google.BillingResult"/> of the update.</summary>
        public BillingResult BillingResult { get; }

        /// <summary>
        /// The updated purchases. Empty when <see cref="BillingResult"/> carries a
        /// non-OK response code (e.g. user cancellation).
        /// </summary>
        public IReadOnlyList<GooglePurchase> Purchases { get; }

        /// <summary>
        /// Creates the event payload for one <c>onPurchasesUpdated</c> callback.
        /// </summary>
        public PurchasesUpdatedEventArgs(BillingResult billingResult, IReadOnlyList<GooglePurchase>? purchases)
        {
            BillingResult = billingResult ?? throw new ArgumentNullException(nameof(billingResult));
            Purchases = purchases ?? Array.Empty<GooglePurchase>();
        }
    }
}
