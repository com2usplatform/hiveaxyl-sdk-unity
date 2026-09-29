# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Sign in with Apple on iOS / macOS: `IAppleSignInPlugin.LoginAsync` runs the native `ASAuthorizationController` flow and returns the raw Apple credential — user identifier, identity token, authorization code, email, name, and real-user status. User cancellation surfaces as the typed `UserCanceled` outcome, which implements the cross-add-on `IUserCanceledOutcome` marker so one handler can match the OS-UI cancel across add-ons.
- Registered with `b.AddAppleSignIn()`; available on iOS / macOS player builds only.
- Xcode build post-processor that enables the "Sign in with Apple" capability and writes the `com.apple.developer.applesignin` entitlement into the generated project. App ID and provisioning-profile setup in the Apple Developer Portal remains a manual step.
