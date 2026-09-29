// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

namespace Hive.Axyl.Payments.Addon.Steam
{
    /// <summary>
    /// Raw mirror of the Steamworks <c>MicroTxnAuthorizationResponse_t</c> callback
    /// payload. Carried by the
    /// <see cref="ISteamMicrotransactionsPlugin.MicroTxnAuthorizationResponse"/> event.
    /// Fields are forwarded verbatim; domain interpretation is the consumer's
    /// responsibility.
    /// </summary>
    /// <remarks>
    /// The unsigned widths match the native struct exactly —
    /// <c>m_unAppID</c> is a <c>uint32</c> and <c>m_ulOrderID</c> is a <c>uint64</c>.
    /// </remarks>
    // Design note: hand-written rather than generated — no rpc references this type, so
    // codegen does not emit it.
    public sealed class SteamMicroTxnResponse
    {
        /// <summary>
        /// <c>MicroTxnAuthorizationResponse_t.m_unAppID</c> — the Steam AppID.
        /// Game-public information.
        /// </summary>
        public uint AppId { get; }

        /// <summary>
        /// <c>MicroTxnAuthorizationResponse_t.m_ulOrderID</c> — the app-supplied order
        /// id echoed back in the authorization callback. The developer generates it and
        /// passes it to <c>InitTxn</c>; Steam's own transaction id is <c>transid</c>,
        /// which is distinct. Sensitive: never written to SDK logs.
        /// </summary>
        public ulong OrderId { get; }

        /// <summary>
        /// <c>MicroTxnAuthorizationResponse_t.m_bAuthorized</c> — <c>true</c> when the
        /// user approved the purchase in the Steam Overlay, <c>false</c> when canceled.
        /// </summary>
        public bool Authorized { get; }

        /// <summary>
        /// Creates a response mirroring one <c>MicroTxnAuthorizationResponse_t</c> callback.
        /// </summary>
        public SteamMicroTxnResponse(uint appId, ulong orderId, bool authorized)
        {
            AppId = appId;
            OrderId = orderId;
            Authorized = authorized;
        }
    }
}
