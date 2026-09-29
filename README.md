# Hive Axyl SDK for Unity

> **Official repository** of Com2uS Platform.
> Canonical URL: https://github.com/com2usplatform/hiveaxyl-sdk-unity

| | |
| --- | --- |
| Product | Hive Axyl |
| Domain | sdk |
| Version | see [`CHANGELOG.md`](CHANGELOG.md) and each package's `package.json` |
| Lifecycle | Active |
| Related | [hiveaxyl-sdk-example-unity](https://github.com/com2usplatform/hiveaxyl-sdk-example-unity): Recipes and usage examples for this SDK |

The Hive Axyl SDK for Unity is a set of UPM packages that connect a Unity game to the Hive Axyl platform: sign-in, payments, push, mailbox, coupons, analytics and more. Every package ships at the same version and is released together.

Requirements: Unity 6000.0 or later. Supported platforms are Android (API 29+), iOS (17+), macOS (15+), Windows (11 64-bit+) and WebGL (WebGL 2.0); support varies per package. For the packages that support WebGL, it uses the same 1.0.0 API and Semantic Versioning as the other platforms. WebGL verification has not been completed for this release; issues it finds will be fixed in later releases. Windows builds also need the Microsoft Visual C++ 2015–2022 Redistributable (x64).

## Installation

The packages are published on [OpenUPM](https://openupm.com). Add the scoped registry to `Packages/manifest.json`, then add the packages you need. `com.com2usplatform.hiveaxyl.core` is required; the others are optional and pull in their own dependencies.

```json
{
  "scopedRegistries": [
    {
      "name": "Hive Axyl",
      "url": "https://package.openupm.com",
      "scopes": ["com.com2usplatform.hiveaxyl"]
    },
    {
      "name": "rlabrecque",
      "url": "https://package.openupm.com",
      "scopes": ["com.rlabrecque"]
    }
  ],
  "dependencies": {
    "com.com2usplatform.hiveaxyl.core": "<version>",
    "com.com2usplatform.hiveaxyl.auth": "<version>"
  }
}
```

The `rlabrecque` entry is only needed for the Steam packages, which depend on Steamworks.NET.

## Packages

Each package directory under [`Packages/`](Packages) has its own CHANGELOG, and some also have a README with setup and usage notes.

| Package | Purpose |
| --- | --- |
| `com.com2usplatform.hiveaxyl.analytics` | Analytics Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.auth.addon.apple` | Apple Sign-In authentication addon for Hive Axyl SDK. Wraps ASAuthorizationAppleIDProvider / ASAuthorizationController to return raw ASAuthorizationAppleIDCredential fields on iOS 17.0+ and macOS 15.0+. |
| `com.com2usplatform.hiveaxyl.auth.addon.credentialmanager` | Android Credential Manager addon (AndroidX Credential Manager + Google Identity SDK) for Hive Axyl Auth. |
| `com.com2usplatform.hiveaxyl.auth.addon.gpg` | Android Google Play Games addon (play-services-games-v2 GamesSignInClient) for Hive Axyl Auth. |
| `com.com2usplatform.hiveaxyl.auth.addon.steam` | Steam authentication addon for Hive Axyl SDK. Wraps Steamworks SDK ISteamUser::GetAuthTicketForWebApi for Web API backend verification on Windows and macOS. |
| `com.com2usplatform.hiveaxyl.auth.addon.webauth` | OAuth 2.0 (RFC 8252) external user-agent authentication addon for Hive Axyl SDK. Opens authorization URLs in the platform web auth session (iOS/macOS ASWebAuthenticationSession, Android Chrome Custom Tabs, Windows loopback HTTP, WebGL postMessage) and returns the raw redirect callback. OAuth interpretation (PKCE, state, nonce) and redirect-URI matching are the app's responsibility. |
| `com.com2usplatform.hiveaxyl.auth` | Auth and Token clients (IAuthService / ITokenService) for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.core` | Core infrastructure module for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.coupon` | Coupon Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.mailbox` | Mailbox Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.payments.addon.apple` | Apple StoreKit2 in-app purchase addon for Hive Axyl SDK. Wraps Product / Transaction / AppStore to expose raw StoreKit2 product, purchase, and transaction fields (including the signed JWS) on iOS 17.0+ and macOS 15.0+. |
| `com.com2usplatform.hiveaxyl.payments.addon.google` | Android Google Play Billing addon (raw Billing Library 9 BillingClient surface: product query, purchase flow, query purchases, acknowledge/consume) for Hive Axyl Payments. |
| `com.com2usplatform.hiveaxyl.payments.addon.steam` | Steam Microtransactions addon for Hive Axyl SDK. Wraps the Steamworks SDK MicroTxnAuthorizationResponse_t callback so the App receives raw Steam payment-authorization responses on Windows and macOS. |
| `com.com2usplatform.hiveaxyl.payments` | Payments Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.push.addon.apns` | iOS/macOS Apple Push Notification service addon (raw device-token + UNNotification surface) for Hive Axyl Push. |
| `com.com2usplatform.hiveaxyl.push.addon.applenotification` | macOS local-notification addon (UNUserNotificationCenter scheduling via UNTimeIntervalNotificationTrigger / UNCalendarNotificationTrigger + cancel / query) for Hive Axyl. Raw, non-orchestrating, stateless. |
| `com.com2usplatform.hiveaxyl.push.addon.fcm` | Android Firebase Cloud Messaging addon (raw device-token + remote-message surface) for Hive Axyl Push. |
| `com.com2usplatform.hiveaxyl.push` | Push Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.serviceaccess` | ServiceAccess Capability for Hive Axyl SDK. |
| `com.com2usplatform.hiveaxyl.steamworks` | Steamworks SDK infrastructure module for Hive Axyl SDK. Wraps the Steamworks.NET managed binding, whose package also carries the Steamworks native libraries, as the single entry point for all Steamworks-dependent packages. |
| `com.com2usplatform.hiveaxyl.storage` | Secure storage module (ISecureStorage) for Hive Axyl SDK. Encrypted string persistence over each platform's secure store on iOS, macOS, Android, and Windows. |
| `com.com2usplatform.hiveaxyl.tcb` | TCB Connector Capability for Hive Axyl SDK. |

## Support

GitHub Issues are not used as a support channel for this repository. Please use the channels below for questions, bug reports, feature requests, and customer inquiries.

| Purpose | Channel |
| --- | --- |
| Questions, bug reports, feature requests | <cs-platform@com2us.com> |
| Security vulnerabilities | Private report only, see [`SECURITY.md`](SECURITY.md) |

GitHub is not an official customer support channel, and we do not commit to response or resolution timelines here. External pull requests are not accepted; see [`CONTRIBUTING.md`](CONTRIBUTING.md).

## License

Licensed under the Apache License, Version 2.0. See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).
