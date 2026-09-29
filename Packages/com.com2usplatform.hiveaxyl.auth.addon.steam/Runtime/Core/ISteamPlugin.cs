// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Steam authentication surface exposed to game code.
    /// Register via <c>b.AddSteamAuth()</c> during bootstrap, then obtain an instance
    /// with <c>HiveCore.Resolve&lt;ISteamPlugin&gt;()</c>.
    /// Supported platforms: Windows and macOS only.
    /// </summary>
    /// <remarks>
    /// Prerequisites: the SDK never initializes the Steam API. The application must
    /// call <c>SteamAPI.Init()</c> once at startup (with <c>steam_appid.txt</c> in
    /// place when running outside the Steam client during development) and pump
    /// <c>SteamAPI.RunCallbacks()</c> every frame; a call made before initialization
    /// fails with a <c>FailedPrecondition</c> error rather than throwing.
    /// </remarks>
    public interface ISteamPlugin : IDisposable
    {
        /// <summary>
        /// Acquires a Steam Web API session ticket and returns it hex-encoded.
        /// </summary>
        Task<SteamServiceGetAuthTicketForWebApiResult> GetAuthTicketForWebApiAsync(
            GetAuthTicketForWebApiRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Releases the Steam <c>HAuthTicket</c> behind a ticket previously returned by
        /// <see cref="GetAuthTicketForWebApiAsync"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A WebApi ticket must stay alive until the backend validates it via
        /// <c>ISteamUserAuth/AuthenticateUserTicket</c>; cancelling it earlier makes Steam
        /// reject it as invalid. Call this once validation has completed (success or failure)
        /// so the handle is returned to Steam — Steam caps the number of concurrently
        /// outstanding auth tickets, so leaking them can make later requests fail. Tickets not
        /// released this way are cancelled only when the plugin is disposed, which is a
        /// shutdown backstop, not the per-session release path.
        /// </para>
        /// <para>
        /// Idempotent: releasing an unknown or already-released ticket (including any call after
        /// <see cref="System.IDisposable.Dispose"/>) is a no-op. Call on the Unity main thread.
        /// </para>
        /// </remarks>
        /// <param name="ticketHex">
        /// The hex string from the <c>Success</c> result of <see cref="GetAuthTicketForWebApiAsync"/>.
        /// </param>
        void ReleaseTicket(string ticketHex);
    }
}
