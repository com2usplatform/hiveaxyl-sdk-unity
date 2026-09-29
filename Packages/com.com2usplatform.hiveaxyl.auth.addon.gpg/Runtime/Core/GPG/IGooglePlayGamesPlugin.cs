// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Auth.Addon.GPG
{
    /// <summary>
    /// Engine-facing surface of the GPG addon on Android. Wraps Play Games
    /// Services v2 <c>GamesSignInClient</c> as three independent RPCs; the addon
    /// never chains them, so the game sequences the calls itself.
    /// </summary>
    /// <remarks>
    /// Register via <c>HiveBootstrap.Initialize(config, b =&gt; b.AddGooglePlayGames())</c>
    /// and resolve <see cref="IGooglePlayGamesPlugin"/> from the registry. On
    /// Android the resolved <c>GooglePlayGamesPlugin</c> dispatches each call
    /// through the codegen-generated <c>IGooglePlayGamesServiceBridge</c> to the
    /// Kotlin <c>GooglePlayGamesServiceNativeAdapterDefault</c>; off-Android
    /// builds leave the port unregistered, so <c>HiveCore.Resolve</c> throws —
    /// guard with <c>TryResolve</c> on non-Android targets.
    /// <para>
    /// Prerequisites: the SDK performs no Play Games project setup. The application
    /// must declare its Play Games App ID (the
    /// <c>com.google.android.gms.games.APP_ID</c> manifest meta-data entry).
    /// <c>PlayGamesSdk.initialize()</c> needs no call site — the Play Games SDK
    /// initializes itself from the <c>PlayGamesInitProvider</c> its own manifest
    /// contributes, so nothing is required of a Unity project; an app that removes
    /// that provider and does not call it gets a <c>FailedPrecondition</c> error
    /// rather than a throw. Server-side access additionally needs the OAuth web
    /// client ID supplied per request.
    /// </para>
    /// </remarks>
    public interface IGooglePlayGamesPlugin
    {
        /// <summary>
        /// UI-less query of the current Play Games authentication state.
        /// <c>NotAuthenticated</c> is a typed Outcome, not a Failure, so the App
        /// can fall back to <see cref="SignInAsync"/> without confusing it with
        /// an error condition.
        /// </summary>
        /// <remarks>Thread-safe; UI-less and re-entrant.</remarks>
        Task<GooglePlayGamesServiceIsAuthenticatedResult> IsAuthenticatedAsync(
            IsAuthenticatedRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Triggers the Play Games account picker / silent sign-in flow. User
        /// dismissal of the OS UI resolves to
        /// <c>GooglePlayGamesServiceSignInResult.UserCanceled</c>. Only one
        /// in-flight SignIn UI session is allowed — a second concurrent call
        /// resolves to <c>GooglePlayGamesServiceSignInResult.Failure</c> with
        /// <see cref="Hive.Axyl.Core.HiveErrorCode.FailedPrecondition"/>.
        /// </summary>
        /// <remarks>Thread-safe; single-session for the SignIn UI.</remarks>
        Task<GooglePlayGamesServiceSignInResult> SignInAsync(
            SignInRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Acquires a server-side authorization code. Returns
        /// <c>GooglePlayGamesServiceRequestServerSideAccessResult.NotAuthenticated</c>
        /// when the pre-auth condition is unmet — App should
        /// <see cref="SignInAsync"/> first and retry. The success
        /// <c>RequestServerSideAccessResponse.ServerAuthCode</c> is returned raw,
        /// without transformation.
        /// </summary>
        /// <remarks>Thread-safe; UI-less and re-entrant.</remarks>
        Task<GooglePlayGamesServiceRequestServerSideAccessResult> RequestServerSideAccessAsync(
            RequestServerSideAccessRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// Programmatically cancels the in-flight SignIn UI session.
        /// A pending <see cref="SignInAsync"/> resolves to an Untyped Problem with
        /// <see cref="Hive.Axyl.Core.HiveErrorCode.Cancelled"/> — never a typed
        /// Outcome. <see cref="IsAuthenticatedAsync"/> and
        /// <see cref="RequestServerSideAccessAsync"/> are UI-less short-Task calls
        /// and are not affected. Safe no-op when no SignIn is in flight.
        /// </summary>
        /// <remarks>Thread-safe.</remarks>
        void CancelCurrentSignIn();
    }
}
