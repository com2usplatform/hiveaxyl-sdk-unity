# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Google Play Games Services v2 sign-in on Android: `IGooglePlayGamesPlugin` with `IsAuthenticatedAsync`, `SignInAsync`, `RequestServerSideAccessAsync` (returns the raw `ServerAuthCode` for backend verification), and `CancelCurrentSignIn`. Sign-in UI dismissal surfaces as the typed `UserCanceled` outcome; an unauthenticated server-side access request as `NotAuthenticated`.
- Registered with `b.AddGooglePlayGames()`; available on Android player builds only.
