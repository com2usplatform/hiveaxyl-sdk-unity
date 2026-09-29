// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Push.Addon.AppleNotification
{
    /// <summary>
    /// Façade for the LocalNotification Apple addon on macOS.
    /// Wraps the generated
    /// <see cref="ILocalNotificationAppleServiceBridge"/> for the
    /// request/response RPCs — category registration, the two trigger-specific
    /// schedule calls, pending / delivered cancellation, the pending /
    /// delivered queries, and the two game-driven <c>Notify*</c> delegate
    /// methods.
    /// <para>
    /// The plugin is stateless and non-orchestrating: it
    /// never persists notification state or installs a
    /// <c>UNUserNotificationCenterDelegate</c>. Notification authorization is
    /// exposed here (<see cref="RequestPermissionAsync"/>) — each addon owns
    /// its own permission method. The game
    /// forwards the
    /// OS delegate callbacks by calling the <c>Notify*</c> methods explicitly
    /// and owns source routing — the plugin does not judge whether an
    /// identifier is one it scheduled.
    /// </para>
    /// <para>
    /// Required arguments are validated fail-fast at this engine boundary
    /// so the native <c>UNTimeIntervalNotificationTrigger</c>
    /// contract is never violated; runtime failures surface as a Result. The
    /// authorization set is OS-global and shared with the APNs addon.
    /// PII policy: content title / body / userInfo are
    /// never written to logs.
    /// </para>
    /// <para>
    /// Disposable because the implementation holds two native event
    /// subscriptions for <see cref="NotificationPresented"/> /
    /// <see cref="NotificationOpened"/>. A caller holding only the interface can
    /// release them without a cast, matching the APNs sibling.
    /// </para>
    /// </summary>
    public interface ILocalNotificationApplePlugin : IDisposable
    {
        /// <summary>
        /// Requests notification authorization via
        /// <c>UNUserNotificationCenter.requestAuthorization(options:)</c>,
        /// returning the raw <c>granted</c> flag. The OS shows its system prompt
        /// only on the first call. A framework error surfaces as a Failure, not
        /// <c>granted = false</c> — except the OS refusal itself. macOS fails the
        /// call with <c>UNErrorCodeNotificationsNotAllowed</c> once notifications
        /// are off for the app; that means "not permitted" rather than a fault, so
        /// the addon absorbs it and this returns <c>granted = false</c>. Handle a
        /// refusal on the <c>granted = false</c> path, not the Failure path. The authorization set is OS-global,
        /// shared with the APNs addon — granting through either applies to both,
        /// so request it once. Throws
        /// <see cref="ArgumentNullException"/> when <paramref name="options"/> is
        /// null.
        /// </summary>
        Task<LocalNotificationAppleServiceRequestPermissionResult> RequestPermissionAsync(
            IReadOnlyList<UNLocalAuthorizationOption> options,
            CancellationToken ct = default);

        /// <summary>
        /// Sets the app-icon badge to <paramref name="count"/> via
        /// <c>UNUserNotificationCenter.setBadgeCount(_:)</c>; <c>0</c> clears it.
        /// The app-icon badge is OS-global — shared with the APNs addon and
        /// independent of a notification's own <c>badge</c> field — so a call here
        /// affects the whole app. Tapping a notification does not clear the badge
        /// (Apple behavior); the game clears it explicitly by passing <c>0</c>.
        /// </summary>
        Task<LocalNotificationAppleServiceSetBadgeCountResult> SetBadgeCountAsync(
            int count,
            CancellationToken ct = default);

        /// <summary>
        /// Registers the notification category set via
        /// <c>UNUserNotificationCenter.setNotificationCategories(_:)</c>. The set
        /// is REPLACED on each call (Apple behavior), so the game passes its full
        /// category set in one call, typically at init.
        /// Throws <see cref="ArgumentNullException"/> when <paramref name="categories"/>
        /// is null and <see cref="ArgumentException"/> when any entry is null or
        /// has an empty identifier.
        /// </summary>
        Task<LocalNotificationAppleServiceRegisterCategoryResult> RegisterCategoryAsync(
            IReadOnlyList<CategorySpec> categories,
            CancellationToken ct = default);

        /// <summary>
        /// Schedules a notification with a
        /// <c>UNTimeIntervalNotificationTrigger</c>. Throws
        /// <see cref="ArgumentException"/> when <paramref name="notificationId"/>
        /// is empty, <paramref name="interval"/> is not positive, or
        /// <paramref name="repeats"/> is true with an interval below 60 seconds
        /// (the Apple contract); throws <see cref="ArgumentNullException"/> when
        /// <paramref name="content"/> is null. An ungranted notification
        /// permission still succeeds — the OS simply does not show it.
        /// </summary>
        Task<LocalNotificationAppleServiceScheduleTimeIntervalResult> ScheduleTimeIntervalAsync(
            string notificationId,
            NotificationContent content,
            TimeSpan interval,
            bool repeats,
            CancellationToken ct = default);

        /// <summary>
        /// Schedules a notification with a
        /// <c>UNCalendarNotificationTrigger</c>. The
        /// <paramref name="dateComponents"/> are forwarded raw; the plugin does
        /// not pre-screen or reinterpret them. A past date with
        /// <paramref name="repeats"/> false is accepted by the OS (it never
        /// fires), and a repeating trigger with an absolute year is forwarded
        /// as-is — an absolute instant cannot recur, and macOS then refires the
        /// notification endlessly; this is an OS implementation
        /// defect, not an SDK constraint, so the game omits the year for a
        /// periodic schedule (e.g. weekday + hour + minute). Throws
        /// <see cref="ArgumentException"/> when
        /// <paramref name="notificationId"/> is empty; throws
        /// <see cref="ArgumentNullException"/> when <paramref name="content"/> or
        /// <paramref name="dateComponents"/> is null.
        /// </summary>
        Task<LocalNotificationAppleServiceScheduleCalendarResult> ScheduleCalendarAsync(
            string notificationId,
            NotificationContent content,
            DateComponents dateComponents,
            bool repeats,
            CancellationToken ct = default);

        /// <summary>
        /// Removes the matching pending notification request
        /// (<c>removePendingNotificationRequests</c>). An unknown id is a success
        /// (idempotent). Throws <see cref="ArgumentNullException"/>
        /// when <paramref name="notificationId"/> is null.
        /// </summary>
        Task<LocalNotificationAppleServiceCancelPendingResult> CancelPendingAsync(
            string notificationId,
            CancellationToken ct = default);

        /// <summary>
        /// Removes the matching delivered notification from Notification Center
        /// (<c>removeDeliveredNotifications</c>). An unknown id is a success
        /// (idempotent). Throws <see cref="ArgumentNullException"/>
        /// when <paramref name="notificationId"/> is null.
        /// </summary>
        Task<LocalNotificationAppleServiceCancelDeliveredResult> CancelDeliveredAsync(
            string notificationId,
            CancellationToken ct = default);

        /// <summary>
        /// Lists the pending notification requests
        /// (<c>getPendingNotificationRequests</c>), flattened to id / trigger
        /// kind / next fire time.
        /// </summary>
        Task<LocalNotificationAppleServiceListPendingResult> ListPendingAsync(CancellationToken ct = default);

        /// <summary>
        /// Lists the delivered notifications still in Notification Center
        /// (<c>getDeliveredNotifications</c>), flattened to id / delivery time.
        /// </summary>
        Task<LocalNotificationAppleServiceListDeliveredResult> ListDeliveredAsync(CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>userNotificationCenter(_:willPresent:withCompletionHandler:)</c>.
        /// The returned presentation options are the App's foreground-display
        /// decision; the plugin holds no display policy and does not judge
        /// whether it scheduled the notification.
        /// Throws <see cref="ArgumentNullException"/> when
        /// <paramref name="notification"/> is null.
        /// </summary>
        Task<LocalNotificationAppleServiceNotifyWillPresentNotificationResult> NotifyWillPresentNotificationAsync(
            NotificationPayload notification,
            CancellationToken ct = default);

        /// <summary>
        /// Game-driven: call from
        /// <c>userNotificationCenter(_:didReceive:withCompletionHandler:)</c> for
        /// a notification the user acted on. <paramref name="actionIdentifier"/>
        /// is the raw <c>UNNotificationResponse.actionIdentifier</c>.
        /// Throws <see cref="ArgumentNullException"/> when
        /// <paramref name="notification"/> or <paramref name="actionIdentifier"/>
        /// is null.
        /// </summary>
        Task<LocalNotificationAppleServiceNotifyDidReceiveResponseResult> NotifyDidReceiveResponseAsync(
            NotificationPayload notification,
            string actionIdentifier,
            CancellationToken ct = default);

        /// <summary>
        /// Raised when a local notification is shown while the app is in the
        /// foreground — the <c>willPresent</c> path, forwarded via
        /// <see cref="NotifyWillPresentNotificationAsync"/>, publishing on the
        /// <c>OnNotificationPresented</c> channel. The raw
        /// <see cref="NotificationPayload"/> is passed verbatim.
        /// Marshalled to the engine main thread by the Core dispatcher.
        /// </summary>
        event Action<NotificationPayload> NotificationPresented;

        /// <summary>
        /// Raised when the user taps a local notification or one of its actions
        /// — the <c>didReceive</c> path, forwarded via
        /// <see cref="NotifyDidReceiveResponseAsync"/>, publishing on the
        /// <c>OnNotificationOpened</c> channel. Carries the raw
        /// <see cref="NotificationPayload"/> together with the action identifier,
        /// as a <see cref="LocalNotificationOpened"/>.
        /// <para>
        /// The game already holds the payload — it is the caller of the
        /// <c>Notify*</c> method that raises this. The event exists for the code
        /// that does not own the delegate. Same pair of events as the APNs addon.
        /// </para>
        /// Marshalled to the engine main thread by the Core dispatcher.
        /// </summary>
        event Action<LocalNotificationOpened> NotificationOpened;
    }
}
