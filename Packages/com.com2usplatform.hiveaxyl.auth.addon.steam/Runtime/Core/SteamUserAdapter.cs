// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System;
using Steamworks;

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Live <see cref="ISteamUserAdapter"/> that calls into the Steamworks.NET runtime.
    /// </summary>
    internal sealed class SteamUserAdapter : ISteamUserAdapter
    {
        /// <inheritdoc />
        public bool IsLoggedOn => SteamUser.BLoggedOn();

        /// <inheritdoc />
        public HAuthTicket GetAuthTicketForWebApi(string identity)
            => SteamUser.GetAuthTicketForWebApi(identity);

        /// <inheritdoc />
        public void CancelAuthTicket(HAuthTicket handle)
            => SteamUser.CancelAuthTicket(handle);

        /// <inheritdoc />
        public IDisposable RegisterTicketCallback(Action<GetTicketForWebApiResponse_t> handler)
            => Callback<GetTicketForWebApiResponse_t>.Create(t => handler(t));
    }
}
#endif
