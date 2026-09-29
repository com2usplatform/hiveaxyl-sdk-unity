// Copyright (c) Com2uS Platform Corp. All rights reserved.

namespace Hive.Axyl.Steamworks
{
    /// <summary>
    /// <see cref="ISteamworksContext"/> stub for build targets where Steamworks SDK is
    /// not supported (Linux, and the Editor when the build target is neither Windows nor
    /// macOS). Always reports <see cref="IsInitialized"/> as <c>false</c>.
    /// </summary>
    internal sealed class UnsupportedSteamworksContext : ISteamworksContext
    {
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public bool IsInitialized => false;
    }
}
