// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Storage
{
    /// <summary>
    /// Persists string values under string keys in an encrypted, app-scoped
    /// store that survives application restarts. Used to keep sensitive data
    /// such as authentication tokens across sessions.
    /// </summary>
    /// <remarks>
    /// AccessDenied does not imply that stored data is gone; recovering by
    /// clearing the store would destroy still-valid data. DataCorrupted means
    /// the data is unreadable, and clearing followed by re-acquisition is
    /// the only recovery path.
    /// </remarks>
    public interface ISecureStorage
    {
        /// <summary>Persists the value under the given key. An existing entry for the same key is replaced.</summary>
        Task<SecureStorageSaveResult> SaveAsync(SecureStorageSaveRequest request, CancellationToken cancellationToken = default);

        /// <summary>Reads the value stored under the given key. A successful response with no value indicates the key has no entry.</summary>
        Task<SecureStorageLoadResult> LoadAsync(SecureStorageLoadRequest request, CancellationToken cancellationToken = default);

        /// <summary>Removes the entry for the given key. Reports success even when no such entry exists.</summary>
        Task<SecureStorageDeleteResult> DeleteAsync(SecureStorageDeleteRequest request, CancellationToken cancellationToken = default);

        /// <summary>Returns the store to an empty state. Succeeds even when stored data is unrecoverable.</summary>
        Task<SecureStorageClearResult> ClearAsync(CancellationToken cancellationToken = default);
    }
}
