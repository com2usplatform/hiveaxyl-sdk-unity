// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using System;
using Steamworks;

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Isolates <see cref="SteamPlugin"/> from the static <c>Steamworks.SteamUser</c> API surface.
    /// </summary>
    internal interface ISteamUserAdapter
    {
        /// <summary>Whether the Steam client is currently logged in.</summary>
        bool IsLoggedOn { get; }

        /// <summary>Requests a Web API session ticket for the given identity string.</summary>
        HAuthTicket GetAuthTicketForWebApi(string identity);

        /// <summary>Releases a previously issued ticket handle.</summary>
        void CancelAuthTicket(HAuthTicket handle);

        /// <summary>
        /// Subscribes <paramref name="handler"/> to <c>GetTicketForWebApiResponse_t</c> callbacks.
        /// The returned disposable unregisters the subscription when disposed.
        /// </summary>
        IDisposable RegisterTicketCallback(Action<GetTicketForWebApiResponse_t> handler);
    }
}
#endif
