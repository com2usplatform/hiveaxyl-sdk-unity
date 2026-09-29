// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Steamworks;

namespace Hive.Axyl.Auth.Addon.Steam
{
    /// <summary>
    /// Steam authentication plugin for Windows and macOS.
    /// Wraps <c>ISteamUser::GetAuthTicketForWebApi</c> for use with the Hive Web API backend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller must ensure that <c>SteamAPI.Init()</c> has been called and that
    /// <c>SteamAPI.RunCallbacks()</c> is invoked every frame so that Steamworks callbacks
    /// are dispatched. See the <c>com.com2usplatform.hiveaxyl.steamworks</c> package README for the full
    /// setup guide.
    /// </para>
    /// <para>
    /// Construct and dispose on the Unity main thread. Concurrent calls to
    /// <see cref="GetAuthTicketForWebApiAsync"/> are each independent; Steamworks allows
    /// multiple <c>HAuthTicket</c> handles simultaneously.
    /// </para>
    /// <para>
    /// Dispose this instance when no longer needed to release the Steamworks callback
    /// registration and cancel any pending requests.
    /// </para>
    /// </remarks>
    public sealed class SteamPlugin : ISteamPlugin
    {
        private readonly ISteamServiceBridge m_bridge;

        /// <summary>
        /// Creates a <see cref="SteamPlugin"/> wired to the given <see cref="IDispatcher"/>
        /// and the platform-appropriate Steamworks context. Register via
        /// <c>HiveBootstrap.Initialize(config, b =&gt; b.AddSteamAuth())</c> and resolve
        /// <see cref="ISteamPlugin"/>.
        /// </summary>
        /// <param name="dispatcher">The SDK's main-thread dispatcher.</param>
        // Design note: the generated AddSteamAuth registration passes the dispatcher
        // resolved from the registry (r.Resolve<IDispatcher>()), so construction never
        // touches the not-yet-initialized static HiveCore.
        public SteamPlugin(IDispatcher dispatcher)
            : this(new SteamServiceBridgeImpl(SteamworksContext.CreateDefault(), dispatcher))
        {
        }

        /// <summary>
        /// Test seam: inject a custom <see cref="ISteamServiceBridge"/>.
        /// </summary>
        internal SteamPlugin(ISteamServiceBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc/>
        public Task<SteamServiceGetAuthTicketForWebApiResult> GetAuthTicketForWebApiAsync(
            GetAuthTicketForWebApiRequest request,
            CancellationToken ct = default)
            => m_bridge.GetAuthTicketForWebApiAsync(request, new BridgeCallContext { Token = ct });

        /// <inheritdoc/>
        public void ReleaseTicket(string ticketHex)
            => (m_bridge as ISteamTicketReleaser)?.ReleaseTicket(ticketHex);

        /// <inheritdoc/>
        public void Dispose() => (m_bridge as IDisposable)?.Dispose();
    }
}
