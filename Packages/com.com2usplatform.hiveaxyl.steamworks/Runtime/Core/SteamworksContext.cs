// Copyright (c) Com2uS Platform Corp. All rights reserved.

namespace Hive.Axyl.Steamworks
{
    /// <summary>
    /// Entry point for the <c>com.com2usplatform.hiveaxyl.steamworks</c> infrastructure module.
    /// Creates the platform-appropriate <see cref="ISteamworksContext"/> implementation.
    /// </summary>
    /// <remarks>
    /// Supported platforms: Windows (StandaloneWindows64) and macOS (StandaloneOSX).
    /// All other targets receive a stub implementation that always reports
    /// <see cref="ISteamworksContext.IsInitialized"/> as <c>false</c>.
    /// </remarks>
    public static class SteamworksContext
    {
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX)
        private static readonly ISteamworksContext s_unsupported = new UnsupportedSteamworksContext();
#endif

        /// <summary>
        /// Creates and returns the <see cref="ISteamworksContext"/> implementation for
        /// the current build target. Safe to call before <c>SteamAPI.Init()</c> — the
        /// returned context remains valid for the application lifetime.
        /// </summary>
        /// <returns>
        /// A desktop implementation backed by Steamworks.NET on Windows and macOS,
        /// or a no-op stub that always returns <c>false</c> on all other targets.
        /// </returns>
        public static ISteamworksContext CreateDefault()
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            return new DesktopSteamworksContext();
#else
            return s_unsupported;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        private sealed class DesktopSteamworksContext : ISteamworksContext
        {
            /// <inheritdoc/>
            public bool IsSupported => true;

            /// <inheritdoc/>
            public bool IsInitialized => global::Steamworks.SteamAPI.IsSteamRunning();
        }
#endif
    }
}
