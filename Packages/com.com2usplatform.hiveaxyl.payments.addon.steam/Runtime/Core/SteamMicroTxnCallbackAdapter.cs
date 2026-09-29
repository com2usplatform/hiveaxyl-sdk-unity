// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System;
using Steamworks;

namespace Hive.Axyl.Payments.Addon.Steam
{
    /// <summary>
    /// Live <see cref="ISteamMicroTxnCallbackAdapter"/> that registers with the
    /// Steamworks.NET callback dispatcher, shared with every other Steamworks
    /// consumer in the process (e.g. the Steam Auth addon).
    /// </summary>
    internal sealed class SteamMicroTxnCallbackAdapter : ISteamMicroTxnCallbackAdapter
    {
        /// <inheritdoc />
        public IDisposable RegisterCallback(Action<MicroTxnAuthorizationResponse_t> handler)
            => Callback<MicroTxnAuthorizationResponse_t>.Create(t => handler(t));
    }
}
#endif
