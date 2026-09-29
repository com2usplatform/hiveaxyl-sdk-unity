// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;

namespace Hive.Axyl.Storage
{
    /// <summary>
    /// Default <see cref="ISecureStorage"/> implementation. Delegates each
    /// request/response RPC to the generated <see cref="ISecureStorageBridge"/>,
    /// which marshals the call onto Core's <c>INativeBridge</c> and parses the
    /// native response envelope into the matching <c>SecureStorageXxxResult</c>
    /// variant. The native <c>SecureStorage</c> plugin is the same on every
    /// declared platform (iOS / macOS / Android / Windows), so a single façade
    /// serves all targets — platform routing lives in the native dispatcher,
    /// not here.
    /// <para>
    /// Construction binds the native plugin to Core's dispatcher through the
    /// generated <c>SecureStorage.Register</c>. The generated <c>AddSecureStorage</c>
    /// front door constructs the façade with the app id it resolved from the registry
    /// being assembled, so registration works inside the <c>HiveBootstrap.Initialize</c>
    /// closure. The public parameterless constructor resolves the app id from the
    /// initialized <c>HiveCore</c> instead, so it is only for construction after
    /// <c>HiveBootstrap.Initialize</c> has returned. On build targets outside the
    /// service's declared platforms the dispatcher has no registered plugin, so RPCs
    /// surface as a Failure.
    /// </para>
    /// <para>
    /// PII policy: the stored value is carried by <c>SecureStorageSaveRequest</c>
    /// / <c>SecureStorageLoadResponse</c> and is never written to logs.
    /// </para>
    /// </summary>
    public sealed class SecureStoragePlugin : ISecureStorage
    {
        private readonly ISecureStorageBridge m_bridge;

        /// <summary>
        /// Creates the plugin backed by the generated bridge and registers the
        /// native plugin with Core's dispatcher, resolving the app id from the
        /// initialized <c>HiveCore</c>. Call only after <c>HiveBootstrap.Initialize</c>
        /// has returned; inside its closure the static core is not yet published and
        /// this throws. Idempotent on the native side; safe to construct more than once.
        /// </summary>
        public SecureStoragePlugin()
            : this(new SecureStorageBridge())
        {
            SecureStorage.Register();
        }

        /// <summary>
        /// Creates the plugin backed by the generated bridge and registers the
        /// native plugin scoped to <paramref name="appId"/>. The generated
        /// <c>AddSecureStorage</c> front door uses this constructor, passing the id it
        /// resolved from the registry being assembled, so construction is safe inside
        /// the bootstrap closure. Idempotent on the native side.
        /// </summary>
        /// <param name="appId">The application identifier from <c>CoreConfig.App.AppId</c>.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="appId"/> is null or empty.
        /// </exception>
        internal SecureStoragePlugin(string appId)
            : this(new SecureStorageBridge())
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentException("appId must not be null or empty.", nameof(appId));
            }

            SecureStorage.Register(appId);
        }

        /// <summary>
        /// Test seam: injects a bridge double so the façade can be exercised
        /// without a live native dispatcher (Classical-school isolation).
        /// </summary>
        internal SecureStoragePlugin(ISecureStorageBridge bridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <inheritdoc />
        public Task<SecureStorageSaveResult> SaveAsync(
            SecureStorageSaveRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return m_bridge.SaveAsync(request, Context(cancellationToken));
        }

        /// <inheritdoc />
        public Task<SecureStorageLoadResult> LoadAsync(
            SecureStorageLoadRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return m_bridge.LoadAsync(request, Context(cancellationToken));
        }

        /// <inheritdoc />
        public Task<SecureStorageDeleteResult> DeleteAsync(
            SecureStorageDeleteRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return m_bridge.DeleteAsync(request, Context(cancellationToken));
        }

        /// <inheritdoc />
        public Task<SecureStorageClearResult> ClearAsync(CancellationToken cancellationToken = default)
            => m_bridge.ClearAsync(new SecureStorageClearRequest(), Context(cancellationToken));

        private static BridgeCallContext Context(CancellationToken cancellationToken)
            => new BridgeCallContext { Token = cancellationToken };
    }
}
