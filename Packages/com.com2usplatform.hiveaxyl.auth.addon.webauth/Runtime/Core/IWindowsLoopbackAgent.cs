// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.WebAuth
{
    /// <summary>
    /// Windows-only agent that reserves an ephemeral loopback port + HTTP
    /// listener for the RFC 8252 §7.3 Loopback Interface Redirection flow. The
    /// App calls <see cref="AllocateLoopbackRedirectUriAsync"/> to obtain the
    /// redirect URI, embeds it in the OAuth authorization URL, then passes the
    /// SAME URI to <see cref="IExternalUserAgent.OpenAsync"/> so the reserved
    /// listener is reused for the redirect capture.
    /// </summary>
    /// <remarks>
    /// Available only on Windows; <see cref="WebAuthSessionPlugin.WindowsLoopback"/>
    /// is null on other platforms, where the redirect scheme/origin is fixed and
    /// no pre-allocation is needed.
    /// </remarks>
    public interface IWindowsLoopbackAgent
    {
        /// <summary>
        /// Reserves an ephemeral loopback port and pre-binds an HTTP listener,
        /// returning the redirect URI (<c>http://127.0.0.1:&lt;port&gt;&lt;path&gt;</c>)
        /// to embed in the authorization URL. Calling this again before
        /// <see cref="IExternalUserAgent.OpenAsync"/> releases the previous
        /// listener and re-allocates a new port, invalidating the earlier URI.
        /// Failure (HiveError): RESOURCE_EXHAUSTED when no port can be bound,
        /// UNAVAILABLE when binding is blocked, INVALID_ARGUMENT when
        /// <c>PathPrefix</c> is malformed.
        /// </summary>
        Task<WindowsLoopbackServiceAllocateLoopbackRedirectUriResult> AllocateLoopbackRedirectUriAsync(
            AllocateLoopbackRedirectUriRequest request,
            CancellationToken ct = default);
    }
}
