// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.CredentialManager
{
    /// <summary>
    /// Engine-facing surface of the CredentialManager addon on Android.
    /// A single <see cref="LoginAsync"/> renders every requested
    /// <c>CredentialOption</c> as one unified AndroidX Credential Manager
    /// picker.
    /// </summary>
    /// <remarks>
    /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddCredentialManager())</c>
    /// and resolve <see cref="IAndroidCredentialManagerPlugin"/> from the registry. On
    /// Android the resolved <c>AndroidCredentialManagerPlugin</c> dispatches each call
    /// through the codegen-generated <c>IAndroidCredentialManagerServiceBridge</c> to the
    /// Kotlin <c>AndroidCredentialManagerServiceNativeAdapterDefault</c>; off-Android
    /// builds (and the Editor) leave the port unregistered, so <c>HiveCore.Resolve</c>
    /// throws — guard with <c>TryResolve</c> on non-Android targets.
    /// </remarks>
    public interface IAndroidCredentialManagerPlugin
    {
        /// <summary>
        /// Invokes the Credential Manager unified picker. Each
        /// <c>CredentialOption</c> entry in <paramref name="request"/> becomes
        /// one <c>GetCredentialRequest.Builder().addCredentialOption()</c> call;
        /// the user picks from the union of every requested option variant in a
        /// single screen. User dismissal resolves to
        /// <c>AndroidCredentialManagerServiceLoginResult.UserCanceled</c>; no
        /// matching credential on the device resolves to
        /// <c>AndroidCredentialManagerServiceLoginResult.NoCredentials</c> — both
        /// typed Outcomes, not Failures.
        /// <para>
        /// On Success, sign in to the Auth capability with
        /// <c>GoogleIdTokenCredential.IdToken</c> as the <c>providerToken</c> and
        /// <c>GoogleIdTokenCredential.UniqueId</c> as the <c>providerUserId</c>.
        /// Do not use <c>Id</c>: Google deprecated it and it carries the account's
        /// email address, which the server rejects when it matches
        /// <c>providerUserId</c> against the verified <c>id_token.sub</c>.
        /// </para>
        /// <para>
        /// Every variant of the result — Success included — preserves the raw
        /// native response body on <c>RawResponse</c>, and on Success that body
        /// is the whole credential: the id_token, plus the account email as
        /// <c>Id</c> and — when the token carries an <c>email</c> claim — again
        /// as <c>Email</c>. Read it for forward-compatibility context if you
        /// need to, but never write it to a log, a crash reporter, or an
        /// analytics event.
        /// </para>
        /// </summary>
        /// <param name="request">Login request — must contain at least one
        /// <c>CredentialOption</c>, each declaring exactly one variant with a
        /// non-empty <c>WebClientId</c> / <c>Nonce</c>, and may not repeat the
        /// same variant type. Violations throw
        /// <see cref="System.ArgumentException"/> before any bridge dispatch.</param>
        /// <param name="ct">Cooperative cancellation token. Cancelling — or
        /// <see cref="CancelCurrentSession"/> — resolves the returned Task with
        /// <c>AndroidCredentialManagerServiceLoginResult.Failure</c> carrying
        /// <see cref="Hive.Axyl.Core.HiveErrorCode.Cancelled"/>, never a typed
        /// Outcome.</param>
        Task<AndroidCredentialManagerServiceLoginResult> LoginAsync(
            LoginRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Cancels the in-flight session, if any. A pending
        /// <see cref="LoginAsync"/> resolves to
        /// <c>AndroidCredentialManagerServiceLoginResult.Failure</c> with
        /// <see cref="Hive.Axyl.Core.HiveErrorCode.Cancelled"/> — never a typed
        /// Outcome. Safe no-op when no session is active.
        /// </summary>
        void CancelCurrentSession();
    }
}
