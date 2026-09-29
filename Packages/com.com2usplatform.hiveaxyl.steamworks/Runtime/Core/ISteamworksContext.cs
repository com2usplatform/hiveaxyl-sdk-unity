// Copyright (c) Com2uS Platform Corp. All rights reserved.

namespace Hive.Axyl.Steamworks
{
    /// <summary>
    /// Provides access to the Steamworks SDK availability state through the
    /// <c>com.com2usplatform.hiveaxyl.steamworks</c> infrastructure module.
    /// </summary>
    /// <remarks>
    /// Consume via <see cref="SteamworksContext.CreateDefault"/>. The App must call
    /// <c>SteamAPI.Init()</c> before <see cref="IsInitialized"/> returns <c>true</c>, and
    /// must call <c>SteamAPI.RunCallbacks()</c> every frame so that Steamworks callbacks
    /// are dispatched. See the package README for the full setup guide.
    /// </remarks>
    public interface ISteamworksContext
    {
        /// <summary>
        /// Returns <c>true</c> when this platform can run the Steamworks SDK
        /// (Windows StandaloneWindows64 and macOS StandaloneOSX only).
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Returns <c>true</c> when the Steamworks SDK has been initialized by the App
        /// and the Steam client is running and reachable.
        /// </summary>
        /// <remarks>
        /// Backed by <c>SteamAPI.IsSteamRunning()</c> on Windows and macOS.
        /// Always returns <c>false</c> on unsupported platforms (Android, iOS, WebGL, Linux).
        /// A <c>false</c> result before the App calls <c>SteamAPI.Init()</c> is expected —
        /// do not treat it as an error during initialization sequencing.
        /// </remarks>
        bool IsInitialized { get; }
    }
}
