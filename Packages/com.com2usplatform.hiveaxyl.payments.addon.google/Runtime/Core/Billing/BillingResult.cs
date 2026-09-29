// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using Hive.Axyl.Core.Serialization;

namespace Hive.Axyl.Payments.Addon.Google
{
    /// <summary>
    /// Raw mirror of <c>com.android.billingclient.api.BillingResult</c>. Carried
    /// by the <see cref="IGooglePlayBillingPlugin.PurchasesUpdated"/> event
    /// (RPC failures map to <c>HiveError</c> instead). The game
    /// interprets <see cref="ResponseCode"/> raw — e.g. <c>USER_CANCELED</c>
    /// inside the purchases-updated event.
    /// </summary>
    // Design note: hand-written rather than codegen-generated. BillingResult appears only
    // in the one-way OnPurchasesUpdated event payload, never in an RPC request/response, so
    // it is unreachable from the service surface and is not generated (the
    // event itself is hand-wired). FromJson mirrors the generated DTO reader convention so
    // the façade's event parser treats it uniformly with the generated GooglePurchase.
    public sealed class BillingResult
    {
        /// <summary><c>getResponseCode()</c> — <c>BillingClient.BillingResponseCode</c> (raw int).</summary>
        public int ResponseCode { get; }

        /// <summary><c>getDebugMessage()</c>.</summary>
        public string DebugMessage { get; }

        /// <summary>
        /// <c>getOnPurchasesUpdatedSubResponseCode()</c> (Billing Library v9) — the
        /// <c>onPurchasesUpdated</c> sub-response code
        /// (<c>PAYMENT_DECLINED_DUE_TO_INSUFFICIENT_FUNDS</c> /
        /// <c>USER_INELIGIBLE</c> / <c>NO_APPLICABLE_SUB_RESPONSE_CODE</c>).
        /// Forwarded raw; the SDK does not branch on it.
        /// </summary>
        public int SubResponseCode { get; }

        /// <summary>
        /// Creates a result mirroring one native <c>BillingResult</c> callback payload.
        /// </summary>
        public BillingResult(int responseCode, string debugMessage, int subResponseCode)
        {
            ResponseCode = responseCode;
            DebugMessage = debugMessage ?? string.Empty;
            SubResponseCode = subResponseCode;
        }

        /// <summary>
        /// Reads a <see cref="BillingResult"/> from the reader positioned at the
        /// value's <c>StartObject</c>, matching the generated DTO
        /// <c>FromJson(IJsonReader)</c> contract (camelCase wire keys; the native
        /// <c>GooglePlayBillingEventEmitter</c> produces the matching payload).
        /// </summary>
        public static BillingResult FromJson(IJsonReader r)
        {
            int responseCode = 0;
            string debugMessage = string.Empty;
            int subResponseCode = 0;

            while (r.Read() && r.TokenType != JsonTokenType.EndObject)
            {
                if (r.TokenEquals("responseCode")) { r.Read(); if (r.TokenType == JsonTokenType.Null) { r.Skip(); } else { responseCode = (int)r.ReadInt64(); } }
                else if (r.TokenEquals("debugMessage")) { r.Read(); if (r.TokenType == JsonTokenType.Null) { r.Skip(); } else { debugMessage = r.ReadString(); } }
                else if (r.TokenEquals("subResponseCode")) { r.Read(); if (r.TokenType == JsonTokenType.Null) { r.Skip(); } else { subResponseCode = (int)r.ReadInt64(); } }
                else { r.Read(); r.Skip(); }
            }

            return new BillingResult(responseCode, debugMessage, subResponseCode);
        }
    }
}
