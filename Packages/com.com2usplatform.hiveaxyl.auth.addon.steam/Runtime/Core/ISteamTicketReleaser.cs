// Copyright (c) Com2uS Platform Corp. All rights reserved.

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Internal seam that lets <see cref="SteamPlugin"/> release a previously issued
    /// Steam Web API ticket through the managed bridge without widening the generated
    /// <see cref="ISteamServiceBridge"/> contract (whose generated twin must keep
    /// implementing exactly the proto-derived surface).
    /// </summary>
    internal interface ISteamTicketReleaser
    {
        /// <inheritdoc cref="ISteamPlugin.ReleaseTicket"/>
        void ReleaseTicket(string ticketHex);
    }
}
