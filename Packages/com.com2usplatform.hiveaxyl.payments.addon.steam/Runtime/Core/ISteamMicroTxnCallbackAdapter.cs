// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System;
using Steamworks;

namespace Hive.Axyl.Payments.Addon.Steam
{
    /// <summary>
    /// Isolates <see cref="SteamMicrotransactionsPlugin"/> from the static
    /// Steamworks.NET callback registration surface.
    /// </summary>
    internal interface ISteamMicroTxnCallbackAdapter
    {
        /// <summary>
        /// Subscribes <paramref name="handler"/> to <c>MicroTxnAuthorizationResponse_t</c>
        /// callbacks. The returned disposable unregisters the subscription when disposed.
        /// </summary>
        IDisposable RegisterCallback(Action<MicroTxnAuthorizationResponse_t> handler);
    }
}
#endif
