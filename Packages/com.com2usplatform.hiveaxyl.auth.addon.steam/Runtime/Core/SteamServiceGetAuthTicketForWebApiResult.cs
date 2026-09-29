// Copyright (c) Com2uS Platform Corp. All rights reserved.

using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.Steam
{
    public abstract partial class SteamServiceGetAuthTicketForWebApiResult
    {
        /// <summary>Returns a Success result carrying the hex-encoded ticket.</summary>
        internal static SteamServiceGetAuthTicketForWebApiResult ForSuccess(string ticketHex)
            => new Success(new TicketResponse { TicketHex = ticketHex });

        /// <summary>Returns a NotAuthenticated outcome result.</summary>
        internal static SteamServiceGetAuthTicketForWebApiResult ForNotAuthenticated()
            => new NotAuthenticated();

        /// <summary>Returns an Untyped Problem result.</summary>
        internal static SteamServiceGetAuthTicketForWebApiResult ForUntypedProblem(HiveError error)
            => new Failure(error);
    }
}
