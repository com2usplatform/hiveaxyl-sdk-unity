# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- APNs on iOS / macOS: `IAPNSPlugin` covers `GetTokenAsync`, `GetProviderEnvironmentAsync`, `RequestAuthorizationAsync`, `GetNotificationSettingsAsync`, `SetBadgeCountAsync`, and `GetColdStartNotificationAsync`, and raises the `NotificationPresented` / `NotificationOpened` / `TokenRefreshed` events. `GetTokenAsync` fails on a simulator or when the `aps-environment` entitlement is missing.
- The `Notify*` methods forward the game's OS notification callbacks, including the launch notification that `GetColdStartNotificationAsync` returns, into the add-on.
- Xcode build post-processor that enables the Push Notifications capability, writes the `aps-environment` entitlement (`development` / `production`), and guarantees `UserNotifications.framework` is linked on iOS.
- Registered with `b.AddAPNS()`; available on iOS / macOS player builds only.
