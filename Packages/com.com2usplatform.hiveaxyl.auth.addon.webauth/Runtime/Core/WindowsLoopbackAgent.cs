// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Auth.Addon.WebAuth
{
    /// <summary>
    /// Default <see cref="IWindowsLoopbackAgent"/> that forwards to the generated
    /// <see cref="IWindowsLoopbackServiceBridge"/>, which dispatches to the native
    /// Windows plugin (the C++ loopback backend). Registered only on Windows
    /// builds; on other platforms <see cref="WebAuthSessionPlugin.WindowsLoopback"/>
    /// is null.
    /// </summary>
    internal sealed class WindowsLoopbackAgent : IWindowsLoopbackAgent
    {
        private readonly IWindowsLoopbackServiceBridge m_bridge;

        public WindowsLoopbackAgent()
            : this(new WindowsLoopbackServiceBridge())
        {
        }

        internal WindowsLoopbackAgent(IWindowsLoopbackServiceBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <c>PathPrefix</c> is <c>null</c>
        /// (fail-fast — App-side construction error; leave it empty to take the
        /// default callback path).</exception>
        public Task<WindowsLoopbackServiceAllocateLoopbackRedirectUriResult> AllocateLoopbackRedirectUriAsync(
            AllocateLoopbackRedirectUriRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.PathPrefix == null)
            {
                throw new ArgumentException(
                    "AllocateLoopbackRedirectUriRequest.PathPrefix must not be null; leave it "
                    + "empty to take the default callback path.",
                    nameof(request));
            }

            return m_bridge.AllocateLoopbackRedirectUriAsync(
                request,
                new BridgeCallContext { Token = ct });
        }
    }
}
