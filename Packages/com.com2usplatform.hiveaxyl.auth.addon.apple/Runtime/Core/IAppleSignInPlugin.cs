// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.Apple
{
    /// <summary>
    /// Sign in with Apple entry point. Runs the native authorization flow and
    /// returns the raw Apple credential fields for the application to exchange
    /// with its authentication backend.
    /// </summary>
    /// <remarks>
    /// Prerequisites: installing this addon automatically adds the Sign in with
    /// Apple entitlement to the generated Xcode project at build time, but enabling
    /// the capability on the App ID and issuing the matching provisioning profile
    /// in the Apple Developer Portal remain the application team's manual steps — a
    /// build signed without them is rejected by the system at runtime.
    /// </remarks>
    public interface IAppleSignInPlugin
    {
        /// <summary>
        /// Drives the native Apple Sign-In flow via ASAuthorizationController.
        /// OS-level dialog dismissal surfaces as the UserCanceled outcome;
        /// programmatic cancellation (token or CancelCurrentSession) resolves
        /// with an UntypedProblem carrying HiveErrorCode.Cancelled.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="request"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the App constructed the request incorrectly:
        /// <c>NonceHash</c> is null/empty, or
        /// <c>RequestedScopes</c> contains <see cref="RequestedScope.Unspecified"/>.
        /// </exception>
        Task<AppleSignInServiceLoginResult> LoginAsync(
            AppleSignInServiceLoginRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Cancels the in-flight session via ASAuthorizationController.cancel().
        /// The pending LoginAsync resolves with an UntypedProblem
        /// (HiveErrorCode.Cancelled). No-op when no session is active.
        /// </summary>
        void CancelCurrentSession();
    }
}
