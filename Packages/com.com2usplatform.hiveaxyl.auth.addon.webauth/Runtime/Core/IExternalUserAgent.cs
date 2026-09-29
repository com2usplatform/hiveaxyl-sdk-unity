// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.WebAuth
{
    /// <summary>
    /// Opens an OAuth 2.0 (RFC 8252) external user-agent session for an
    /// authorization URL and captures the redirect callback. The contract is
    /// protocol-neutral: the result carries the raw callback URL and its parsed
    /// query parameters, leaving OAuth interpretation (PKCE, state, nonce) and
    /// redirect-URI matching to the App / Auth Capability.
    /// </summary>
    public interface IExternalUserAgent
    {
        /// <summary>
        /// Opens the session for <paramref name="request"/> and resolves once
        /// the platform delivers the redirect callback. Only one session may
        /// run at a time: a concurrent call while a session is in flight
        /// resolves with an UntypedProblem (HiveErrorCode.FailedPrecondition)
        /// without disturbing the active session. OS-UI dismissal
        /// surfaces as the UserCanceled outcome; programmatic cancellation
        /// (the token or <see cref="CancelCurrentSession"/>) resolves with an
        /// UntypedProblem (HiveErrorCode.Cancelled) — the two are distinct.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="request"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown fail-fast when <c>Url</c> or <c>RedirectUri</c>
        /// is null/empty — an absent URL is App-side misuse, not a runtime
        /// state.
        /// </exception>
        Task<ExternalUserAgentServiceOpenResult> OpenAsync(
            OpenRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Cancels the in-flight session via the platform's cancel mechanism.
        /// The pending <see cref="OpenAsync"/> resolves with an UntypedProblem
        /// (HiveErrorCode.Cancelled) — programmatic cancellation, not the
        /// UserCanceled outcome. No-op when no session is
        /// active.
        /// </summary>
        void CancelCurrentSession();
    }
}
