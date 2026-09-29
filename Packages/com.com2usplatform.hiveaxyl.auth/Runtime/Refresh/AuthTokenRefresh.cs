// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth
{
    /// <summary>
    /// Enables automatic token refresh for the Auth capability. Call once after
    /// <c>HiveBootstrap.Initialize</c>. Builds the refresh handler on the refresh-safe
    /// transport and registers it with Core.
    /// </summary>
    public static class AuthTokenRefresh
    {
        /// <summary>Wires the auth token-refresh handler into Core. Call once.</summary>
        /// <param name="baseUrl">Token endpoint host root (same as the Token capability).</param>
        /// <param name="clientId">OAuth client id sent with the refresh request.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="baseUrl"/> or <paramref name="clientId"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Core has not been initialized, or when a refresh handler has
        /// already been registered.
        /// </exception>
        public static void Enable(string baseUrl, string clientId)
        {
            if (baseUrl == null) { throw new ArgumentNullException(nameof(baseUrl)); }
            if (clientId == null) { throw new ArgumentNullException(nameof(clientId)); }

            var transport = HiveCore.Resolve<IRefreshTransport>();
            var tokenService = new TokenService(transport, baseUrl);
            var handler = new AuthTokenRefreshHandler(tokenService, clientId);
            HiveCore.SetTokenRefreshHandler(handler);
        }
    }
}
