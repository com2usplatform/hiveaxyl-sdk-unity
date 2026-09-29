# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

This package supports WebGL with the same 1.0.0 API and Semantic Versioning as on the other platforms. WebGL verification has not been completed for this release; issues it finds will be fixed in later releases.

### Added

- OAuth 2.0 (RFC 8252) external user-agent flow: `IExternalUserAgent.OpenAsync` opens an authorization URL in the platform's web auth session and returns the raw redirect callback URL plus its parsed query parameters; `CancelCurrentSession` aborts the in-flight session. One session at a time — a concurrent `OpenAsync` fails with `FailedPrecondition`.
- Platform backends: Android (Chrome Custom Tab — register the OAuth redirect scheme through the `hiveAxylWebAuthRedirectScheme` Gradle manifest placeholder), iOS / macOS (`ASWebAuthenticationSession`), Windows (RFC 8252 loopback redirect capture), and WebGL (browser popup). User dismissal surfaces as the typed `UserCanceled` outcome, which implements the cross-add-on `IUserCanceledOutcome` marker. The WebGL backend ships inside the package as a bundled `.jspre`, so a WebGL build needs no host-page wiring; the redirect page the flow returns to remains app-hosted.
- Registered with `b.AddWebAuth()`; available on Android / iOS / macOS / Windows / WebGL player builds only. Windows additionally requires the Microsoft Visual C++ 2015–2022 Redistributable (x64) on every machine that runs the game, including your own while developing.
