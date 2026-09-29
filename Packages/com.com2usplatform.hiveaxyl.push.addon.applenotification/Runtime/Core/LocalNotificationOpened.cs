// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;

namespace Hive.Axyl.Push.Addon.AppleNotification
{
    /// <summary>
    /// Payload of <see cref="ILocalNotificationApplePlugin.NotificationOpened"/>:
    /// the local notification the user acted on, plus which action they chose.
    /// <para>
    /// A dedicated type rather than two event arguments, because the action
    /// identifier is not a field of the notification —
    /// <see cref="NotificationPayload"/> mirrors what the OS delivered. Pairing
    /// them in one type names the value at the subscriber
    /// (<c>e.ActionIdentifier</c>) instead of leaving it an unnamed second
    /// argument.
    /// </para>
    /// <para>
    /// The wire shape it decodes is
    /// <c>{"notification": {…}, "actionIdentifier": "…"}</c>.
    /// </para>
    /// </summary>
    // Design note: hand-written rather than generated, and shaped to match the APNs
    // sibling's ApnsNotificationOpened — the event channels are not request/response RPCs,
    // so neither they nor their payloads are modeled in the proto.
    public sealed class LocalNotificationOpened
    {
        /// <summary>
        /// Creates a payload pairing a notification with the action taken on it.
        /// </summary>
        /// <param name="notification">The notification the user acted on.</param>
        /// <param name="actionIdentifier">
        /// The action taken. Empty (never null) when the native side reported
        /// none — see <see cref="ActionIdentifier"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="notification"/> or <paramref name="actionIdentifier"/>
        /// is null.
        /// </exception>
        public LocalNotificationOpened(NotificationPayload notification, string actionIdentifier)
        {
            Notification = notification ?? throw new ArgumentNullException(nameof(notification));
            ActionIdentifier = actionIdentifier ?? throw new ArgumentNullException(nameof(actionIdentifier));
        }

        /// <summary>
        /// The notification the user acted on, passed through verbatim.
        /// </summary>
        public NotificationPayload Notification { get; }

        /// <summary>
        /// <c>UNNotificationResponse.actionIdentifier</c>: Apple's default-action
        /// or dismiss-action constant, or a custom action id registered with the
        /// notification's category.
        /// <para>
        /// Empty when the native side reported none. The event still fires in
        /// that case — a tap the SDK cannot label is more useful to a subscriber
        /// than no tap at all.
        /// </para>
        /// </summary>
        public string ActionIdentifier { get; }
    }
}
