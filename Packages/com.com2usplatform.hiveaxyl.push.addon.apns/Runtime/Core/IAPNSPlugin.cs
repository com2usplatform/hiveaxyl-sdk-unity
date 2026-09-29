// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Push.Addon.APNS
{
    /// <summary>
    /// Façade for the APNs addon on iOS / macOS. Wraps the
    /// generated <see cref="IApnsServiceBridge"/> for the request/response RPCs
    /// and surfaces the three one-way native events
    /// (<see cref="NotificationPresented"/> / <see cref="NotificationOpened"/> /
    /// <see cref="TokenRefreshed"/>) that travel over Core's event bus rather
    /// than as RPCs.
    /// <para>
    /// The plugin is stateless and non-orchestrating: it never persists the
    /// token or installs an app delegate. The game forwards the OS delegate
    /// callbacks by calling the <c>Notify*</c> methods explicitly. Notification
    /// authorization is exposed here (the addon owns its own permission
    /// methods); the iOS authorization set is OS-global, shared with the
    /// LocalNotification Apple addon.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Prerequisites: installing this addon automatically enables the Push
    /// Notifications capability and writes the APNs environment entitlement into
    /// the generated Xcode project at build time (a Unity Development Build targets
    /// the APNs sandbox, a release build targets production). Enabling Push
    /// Notifications on the App ID and issuing the matching provisioning profile in
    /// the Apple Developer Portal remain the application team's manual steps.
    /// </remarks>
    public interface IAPNSPlugin : IDisposable
    {
        /// <summary>
        /// Triggers <c>UIApplication.registerForRemoteNotifications</c> and
        /// resolves once the app forwards the OS callback via
        /// <see cref="NotifyDidRegisterForRemoteNotificationsAsync"/>, returning
        /// the device token as a lowercase hex string. Fails <c>UNAVAILABLE</c>
        /// on the simulator or a <c>didFailToRegister</c> callback.
        /// </summary>
        Task<ApnsServiceGetTokenResult> GetTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// Reads the <c>aps-environment</c> entitlement and maps it to the
        /// provider environment (APNS / APNS_SANDBOX). Fails
        /// <c>FAILED_PRECONDITION</c> when the Push Notifications capability is
        /// not enabled. The game passes the result to
        /// <c>IPushService.UpsertTokenAsync</c>.
        /// </summary>
        Task<ApnsServiceGetProviderEnvironmentResult> GetProviderEnvironmentAsync(CancellationToken ct = default);

        /// <summary>
        /// Drains the single-use cold-start buffer. Returns the notification the
        /// user tapped to launch the app, or an unset notification when there is
        /// nothing to drain: a normal launch, a second call, a build without the
        /// capture source (macOS bare .app), or a query that ran before the app's
        /// relay finished (await the template's <c>ColdStartRelayed</c> first).
        /// Unset is a non-null notification whose fields are all defaults — test
        /// <c>UserInfoJson</c> for presence; a relayed payload always carries it
        /// non-empty.
        /// </summary>
        Task<ApnsServiceGetColdStartNotificationResult> GetColdStartNotificationAsync(CancellationToken ct = default);

        /// <summary>
        /// Requests notification authorization via
        /// <c>UNUserNotificationCenter.requestAuthorization(options:)</c>,
        /// returning the raw <c>granted</c> flag. The OS shows its system prompt
        /// only on the first call. A framework error surfaces as a Failure, not
        /// <c>granted = false</c> — except the OS refusal itself. iOS answers a
        /// refusal with <c>granted = false</c>, while macOS fails the call with
        /// <c>UNErrorCodeNotificationsNotAllowed</c> once notifications are off
        /// for the app; both mean "not permitted", so the addon absorbs the
        /// difference and this returns <c>granted = false</c> on either platform.
        /// Handle a refusal on the <c>granted = false</c> path, not the Failure
        /// path, and call
        /// <see cref="GetNotificationSettingsAsync"/> when the raw
        /// <see cref="UNAuthorizationStatus"/> is needed. The authorization set is
        /// OS-global, shared with the LocalNotification Apple addon — granting
        /// through either applies to both.
        /// </summary>
        Task<ApnsServiceRequestAuthorizationResult> RequestAuthorizationAsync(
            IReadOnlyList<UNAuthorizationOption> options,
            CancellationToken ct = default);

        /// <summary>
        /// Sets the app-icon badge to <paramref name="count"/> via
        /// <c>UNUserNotificationCenter.setBadgeCount(_:)</c>; <c>0</c> clears it.
        /// The app-icon badge is OS-global — shared with the LocalNotification
        /// Apple addon and independent of a notification's own <c>badge</c> field —
        /// so a call here affects the whole app. Tapping a notification does not
        /// clear the badge (Apple behavior); the game clears it by passing <c>0</c>.
        /// </summary>
        Task<ApnsServiceSetBadgeCountResult> SetBadgeCountAsync(
            int count,
            CancellationToken ct = default);

        /// <summary>
        /// Reads the current notification settings, exposing the raw
        /// <see cref="UNAuthorizationStatus"/>. The addon performs no
        /// "permanently blocked" derivation — interpreting the status is the
        /// App's responsibility.
        /// </summary>
        Task<ApnsServiceGetNotificationSettingsResult> GetNotificationSettingsAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>application(_:didRegisterForRemoteNotificationsWithDeviceToken:)</c>
        /// with the raw token bytes. Resolves a pending
        /// <see cref="GetTokenAsync"/> and the native side raises
        /// <see cref="TokenRefreshed"/> over the event bus.
        /// </summary>
        Task<ApnsServiceNotifyDidRegisterForRemoteNotificationsResult> NotifyDidRegisterForRemoteNotificationsAsync(
            byte[] deviceToken,
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>application(_:didFailToRegisterForRemoteNotificationsWithError:)</c>.
        /// Fails a pending <see cref="GetTokenAsync"/> with <c>UNAVAILABLE</c>.
        /// </summary>
        Task<ApnsServiceNotifyDidFailToRegisterResult> NotifyDidFailToRegisterAsync(
            string errorDescription,
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>userNotificationCenter(_:willPresent:withCompletionHandler:)</c>.
        /// The native side raises <see cref="NotificationPresented"/> over the
        /// event bus. The returned result's presentation options are always
        /// empty — the addon does not relay them; the app returns its chosen
        /// options directly to the OS completion handler inside its own
        /// willPresent delegate.
        /// </summary>
        Task<ApnsServiceNotifyWillPresentNotificationResult> NotifyWillPresentNotificationAsync(
            ApnsNotification notification,
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>userNotificationCenter(_:didReceive:withCompletionHandler:)</c>.
        /// The native side raises <see cref="NotificationOpened"/> over the
        /// event bus for a notification the user acted on (tap or custom
        /// action), carrying the action identifier alongside it.
        /// </summary>
        Task<ApnsServiceNotifyDidReceiveResponseResult> NotifyDidReceiveResponseAsync(
            ApnsNotification notification,
            string actionIdentifier,
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call once at startup with the raw payload of the
        /// notification that launched the app from a terminated state, serialised
        /// as a JSON object string. Seeds the single-use cold-start buffer so the
        /// first <see cref="GetColdStartNotificationAsync"/> returns it. The
        /// standard capture template reads the launch tap on iOS from
        /// <c>launchOptions[.remoteNotification]</c> (an early launch observer); on
        /// macOS the launch key only marks that a notification launched the app
        /// (its response carries no payload), so an early notification-center
        /// delegate takes the payload from the launch tap's <c>didReceive</c>. A
        /// normal launch relays an empty string, which is a no-op.
        /// </summary>
        Task<ApnsServiceNotifyColdStartNotificationResult> NotifyColdStartNotificationAsync(
            string userInfoJson,
            CancellationToken ct = default);

        /// <summary>
        /// Raised when a remote notification is shown while the app is in the
        /// foreground — the native <c>willPresent</c> path, forwarded via
        /// <see cref="NotifyWillPresentNotificationAsync"/>, publishing on the
        /// <c>OnNotificationPresented</c> channel. The raw
        /// <see cref="ApnsNotification"/> is passed verbatim.
        /// Marshalled to the engine main thread by the Core dispatcher.
        /// </summary>
        event Action<ApnsNotification> NotificationPresented;

        /// <summary>
        /// Raised when the user taps a remote notification or one of its actions
        /// — the native <c>didReceive</c> path, forwarded via
        /// <see cref="NotifyDidReceiveResponseAsync"/>, publishing on the
        /// <c>OnNotificationOpened</c> channel. Carries the raw
        /// <see cref="ApnsNotification"/> together with the action identifier, as
        /// an <see cref="ApnsNotificationOpened"/>.
        /// <para>
        /// Held apart from <see cref="NotificationPresented"/> rather than sharing
        /// one event with a nullable action id: a subscriber could not otherwise
        /// tell a foreground presentation from a tap.
        /// </para>
        /// Marshalled to the engine main thread by the Core dispatcher.
        /// </summary>
        event Action<ApnsNotificationOpened> NotificationOpened;

        /// <summary>
        /// Raised when the device token is (re)issued, i.e. when the app
        /// forwards <see cref="NotifyDidRegisterForRemoteNotificationsAsync"/>
        /// and the native side publishes on the <c>OnTokenRefreshed</c> channel.
        /// Carries the token as a lowercase hex string. The game re-registers
        /// the token explicitly — the plugin does not.
        /// Marshalled to the engine main thread.
        /// </summary>
        event Action<string> TokenRefreshed;
    }
}
