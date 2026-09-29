// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hive.Axyl.Core;
using Hive.Axyl.Core.Serialization;

namespace Hive.Axyl.Push.Addon.AppleNotification
{
    /// <summary>
    /// macOS implementation of <see cref="ILocalNotificationApplePlugin"/>.
    /// Validates arguments fail-fast at the engine boundary, then
    /// delegates each RPC to the generated
    /// <see cref="ILocalNotificationAppleServiceBridge"/> (which marshals onto
    /// Core's <c>INativeBridge</c> and parses the response envelope). Stateless
    /// and non-orchestrating: it holds no notification state and
    /// installs no delegate. The two game-driven <c>Notify*</c> RPCs additionally
    /// forward the one-way <c>OnNotificationPresented</c> /
    /// <c>OnNotificationOpened</c> native events to the
    /// <see cref="NotificationPresented"/> / <see cref="NotificationOpened"/> C#
    /// events, matching the APNs sibling.
    /// <para>
    /// Construction registers the native <c>LocalNotificationAppleService</c>
    /// plugin with Core's dispatcher, so the game must run
    /// <c>HiveBootstrap.Initialize</c> beforehand. On non-Apple build targets
    /// the dispatcher has no registered plugin, so RPCs surface as a Failure.
    /// </para>
    /// <para>
    /// The plugin takes its <see cref="INativeBridge"/> by injection — the
    /// generated <c>AddAppleNotification</c> registration resolves it from the
    /// registry and passes it in — and uses it to wire the event subscriptions.
    /// Dispose releases them.
    /// </para>
    /// </summary>
    public sealed class LocalNotificationApplePlugin : ILocalNotificationApplePlugin
    {
        // One-way Native Event Bus channels (Plugin -> game). Must match the
        // channel names LocalNotificationAppleEventName publishes; the prefix is
        // the proto service name (LocalNotificationAppleService).
        private const string k_EventNotificationPresented =
            "LocalNotificationAppleService.OnNotificationPresented";

        private const string k_EventNotificationOpened =
            "LocalNotificationAppleService.OnNotificationOpened";

        private const string k_NotificationEventKey = "notification";
        private const string k_ActionIdentifierEventKey = "actionIdentifier";

        // Apple's UNTimeIntervalNotificationTrigger requires a repeating
        // interval of at least 60 seconds.
        private static readonly TimeSpan k_MinRepeatInterval = TimeSpan.FromSeconds(60);

        private readonly ILocalNotificationAppleServiceBridge m_bridge;
        private readonly INativeBridge m_nativeBridge;

        // Cache the delegate instances so Subscribe/Unsubscribe pair up.
        private readonly Action<string> m_onNotificationPresented;
        private readonly Action<string> m_onNotificationOpened;

        private int m_disposed;

        /// <inheritdoc />
        public event Action<NotificationPayload> NotificationPresented;

        /// <inheritdoc />
        public event Action<LocalNotificationOpened> NotificationOpened;

        /// <summary>
        /// Creates a plugin wired to the given native bridge.
        /// </summary>
        /// <param name="nativeBridge">The SDK's native bridge.</param>
        public LocalNotificationApplePlugin(INativeBridge nativeBridge)
            : this(new LocalNotificationAppleServiceBridge(), nativeBridge)
        {
            try
            {
                // Register the native plugin with the dispatcher so bridge calls
                // reach it. Idempotent — safe to construct repeatedly.
                LocalNotificationAppleService.Register();
            }
            catch (Exception)
            {
                // The base constructor already wired the event subscriptions; if
                // registration throws the caller never receives the instance and
                // can never Dispose(), so release them here before rethrowing
                // (Dispose is idempotent).
                Dispose();
                throw;
            }
        }

        internal LocalNotificationApplePlugin(
            ILocalNotificationAppleServiceBridge bridge, INativeBridge nativeBridge)
        {
            m_bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            m_nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));

            m_onNotificationPresented = OnNativeNotificationPresented;
            m_onNotificationOpened = OnNativeNotificationOpened;

            try
            {
                m_nativeBridge.SubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
                m_nativeBridge.SubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);
            }
            catch (Exception)
            {
                // A subscription threw (e.g. a disposed bridge). The caller never
                // receives the instance, so it can never Dispose() — roll the
                // subscriptions back so a failed construction leaves no dangling
                // subscriber. Unsubscribe is a no-op for an unsubscribed channel.
                m_nativeBridge.UnsubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
                m_nativeBridge.UnsubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceRequestPermissionResult> RequestPermissionAsync(
            IReadOnlyList<UNLocalAuthorizationOption> options,
            CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return m_bridge.RequestPermissionAsync(
                new RequestPermissionRequest { Options = options },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceSetBadgeCountResult> SetBadgeCountAsync(
            int count,
            CancellationToken ct = default)
        {
            return m_bridge.SetBadgeCountAsync(
                new SetBadgeCountRequest { Count = count },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceRegisterCategoryResult> RegisterCategoryAsync(
            IReadOnlyList<CategorySpec> categories,
            CancellationToken ct = default)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            foreach (CategorySpec category in categories)
            {
                if (category == null)
                {
                    throw new ArgumentException("categories must not contain null entries.", nameof(categories));
                }

                if (string.IsNullOrEmpty(category.Identifier))
                {
                    throw new ArgumentException("Each category identifier must not be empty.", nameof(categories));
                }
            }

            return m_bridge.RegisterCategoryAsync(
                new RegisterCategoryRequest { Categories = categories },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceScheduleTimeIntervalResult> ScheduleTimeIntervalAsync(
            string notificationId,
            NotificationContent content,
            TimeSpan interval,
            bool repeats,
            CancellationToken ct = default)
        {
            RequireNotificationId(notificationId);

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentException("interval must be greater than zero.", nameof(interval));
            }

            if (repeats && interval < k_MinRepeatInterval)
            {
                throw new ArgumentException(
                    "A repeating time-interval trigger requires interval >= 60 seconds (Apple constraint).",
                    nameof(interval));
            }

            return m_bridge.ScheduleTimeIntervalAsync(
                new ScheduleTimeIntervalRequest
                {
                    NotificationId = notificationId,
                    Content = content,
                    IntervalMillis = (long)interval.TotalMilliseconds,
                    Repeats = repeats,
                },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceScheduleCalendarResult> ScheduleCalendarAsync(
            string notificationId,
            NotificationContent content,
            DateComponents dateComponents,
            bool repeats,
            CancellationToken ct = default)
        {
            RequireNotificationId(notificationId);

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (dateComponents == null)
            {
                throw new ArgumentNullException(nameof(dateComponents));
            }

            // The dateComponents are forwarded raw — the plugin does not
            // pre-screen or reinterpret them. A repeating trigger with an absolute
            // year cannot recur and macOS then refires it endlessly —
            // an OS implementation defect, not an SDK constraint — so the game omits
            // the year for a periodic schedule (weekday+hour+minute for weekly, etc.);
            // the SDK does not force that choice.
            return m_bridge.ScheduleCalendarAsync(
                new ScheduleCalendarRequest
                {
                    NotificationId = notificationId,
                    Content = content,
                    DateComponents = dateComponents,
                    Repeats = repeats,
                },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceCancelPendingResult> CancelPendingAsync(
            string notificationId,
            CancellationToken ct = default)
        {
            if (notificationId == null)
            {
                throw new ArgumentNullException(nameof(notificationId));
            }

            return m_bridge.CancelPendingAsync(
                new CancelPendingRequest { NotificationId = notificationId },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceCancelDeliveredResult> CancelDeliveredAsync(
            string notificationId,
            CancellationToken ct = default)
        {
            if (notificationId == null)
            {
                throw new ArgumentNullException(nameof(notificationId));
            }

            return m_bridge.CancelDeliveredAsync(
                new CancelDeliveredRequest { NotificationId = notificationId },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceListPendingResult> ListPendingAsync(CancellationToken ct = default)
            => m_bridge.ListPendingAsync(new ListPendingRequest(), Context(ct));

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceListDeliveredResult> ListDeliveredAsync(CancellationToken ct = default)
            => m_bridge.ListDeliveredAsync(new ListDeliveredRequest(), Context(ct));

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceNotifyWillPresentNotificationResult> NotifyWillPresentNotificationAsync(
            NotificationPayload notification,
            CancellationToken ct = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            return m_bridge.NotifyWillPresentNotificationAsync(
                new NotifyWillPresentNotificationRequest { Notification = notification },
                Context(ct));
        }

        /// <inheritdoc />
        public Task<LocalNotificationAppleServiceNotifyDidReceiveResponseResult> NotifyDidReceiveResponseAsync(
            NotificationPayload notification,
            string actionIdentifier,
            CancellationToken ct = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (actionIdentifier == null)
            {
                throw new ArgumentNullException(nameof(actionIdentifier));
            }

            return m_bridge.NotifyDidReceiveResponseAsync(
                new NotifyDidReceiveResponseRequest
                {
                    Notification = notification,
                    ActionIdentifier = actionIdentifier,
                },
                Context(ct));
        }

        // Empty notificationId would violate the Apple request contract; reject
        // it at the boundary. A null id is a programming
        // error surfaced as ArgumentNullException.
        private static void RequireNotificationId(string notificationId)
        {
            if (notificationId == null)
            {
                throw new ArgumentNullException(nameof(notificationId));
            }

            if (notificationId.Length == 0)
            {
                throw new ArgumentException("notificationId must not be empty.", nameof(notificationId));
            }
        }

        private static BridgeCallContext Context(CancellationToken ct)
            => new BridgeCallContext { Token = ct };

        private void OnNativeNotificationPresented(string json)
        {
            var handler = NotificationPresented;
            if (handler == null)
            {
                return;
            }

            NotificationPayload? payload = ParseNotificationEvent(json);
            if (payload != null)
            {
                handler(payload);
            }
        }

        private void OnNativeNotificationOpened(string json)
        {
            var handler = NotificationOpened;
            if (handler == null)
            {
                return;
            }

            LocalNotificationOpened? opened = ParseNotificationOpenedEvent(json);
            if (opened != null)
            {
                handler(opened);
            }
        }

        // Parses the raw NotificationPayload from a notification event payload.
        // Returns null on a malformed payload so a parse failure drops the event
        // rather than throwing into the bridge's event-dispatch loop (the payload
        // is produced by our own native serializer, so this is defensive).
        private static NotificationPayload? ParseNotificationEvent(string json)
        {
            try
            {
                return NotificationPayload.FromJsonString(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Splits the {"notification": {…}, "actionIdentifier": "…"} payload of
        // OnNotificationOpened, then decodes the notification with its own generated
        // reader. Returns null when the notification is absent or the payload is
        // malformed — without a notification there is nothing to report.
        //
        // A missing action identifier is NOT a failure: it maps to an empty id and
        // the event still fires, since a tap the SDK cannot label is more useful to
        // a subscriber than no tap at all.
        private static LocalNotificationOpened? ParseNotificationOpenedEvent(string json)
        {
            if (!JsonReadHelpers.TryReadWrappedPayload(
                    json,
                    k_NotificationEventKey,
                    k_ActionIdentifierEventKey,
                    out string notificationJson,
                    out string actionIdentifier))
            {
                return null;
            }

            try
            {
                return new LocalNotificationOpened(NotificationPayload.FromJsonString(notificationJson), actionIdentifier);
            }
            catch (Exception)
            {
                // Defensive: the payload is produced by our own native serializer,
                // so a DTO-level parse failure drops the event rather than throwing
                // into the bridge's event-dispatch loop.
                return null;
            }
        }

        /// <summary>
        /// Releases the native event subscriptions. Idempotent — a second call is
        /// a no-op, so the failed-construction rollback and a normal dispose can
        /// both run.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) != 0)
            {
                return;
            }

            m_nativeBridge.UnsubscribeEvent(k_EventNotificationPresented, m_onNotificationPresented);
            m_nativeBridge.UnsubscribeEvent(k_EventNotificationOpened, m_onNotificationOpened);
        }
    }
}
