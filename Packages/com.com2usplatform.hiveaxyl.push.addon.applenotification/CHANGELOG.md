# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Local notifications on macOS: `ILocalNotificationApplePlugin` covers `RequestPermissionAsync`, `RegisterCategoryAsync`, the trigger-specific `ScheduleTimeIntervalAsync` / `ScheduleCalendarAsync`, `CancelPendingAsync` / `CancelDeliveredAsync`, `ListPendingAsync` / `ListDeliveredAsync`, and `SetBadgeCountAsync`, and raises the `NotificationPresented` / `NotificationOpened` events — `NotificationOpened` pairs the notification with the action identifier the user tapped.
- The `Notify*` methods let a game that owns its notification delegate forward the OS callbacks into the add-on's events.
- Registered with `b.AddAppleNotification()`; available on macOS player builds only. iOS local notifications are the Unity Mobile Notifications package's.
