// Copyright (c) Com2uS Platform Corp. All rights reserved.

namespace Hive.Axyl.Steamworks
{
    /// <summary>
    /// Provides access to the Steamworks SDK availability state through the
    /// <c>com.com2usplatform.hiveaxyl.steamworks</c> infrastructure module.
    /// </summary>
    /// <remarks>
    /// Consume via <see cref="SteamworksContext.CreateDefault"/>. The App must call
    /// <c>SteamAPI.Init()</c> before using Steamworks, and must call
    /// <c>SteamAPI.RunCallbacks()</c> every frame after it succeeds so that Steamworks
    /// callbacks are dispatched. <see cref="IsInitialized"/> does not report whether
    /// <c>SteamAPI.Init()</c> succeeded; the App keeps the value <c>SteamAPI.Init()</c>
    /// returned. See the package README for the full setup guide.
    /// </remarks>
    public interface ISteamworksContext
    {
        /// <summary>
        /// Returns <c>true</c> when the build target is Windows or macOS, including
        /// in the Editor with one of those build targets.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Returns <c>true</c> when the Steam client is running. It does not report
        /// whether the App's <c>SteamAPI.Init()</c> call succeeded.
        /// </summary>
        /// <remarks>
        /// Backed by <c>SteamAPI.IsSteamRunning()</c> on Windows and macOS.
        /// Always returns <c>false</c> on Linux, and in the Editor when the build target
        /// is neither Windows nor macOS.
        /// </remarks>
        bool IsInitialized { get; }
    }
}
