# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Steam Microtransactions callback surface on Windows and macOS: `ISteamMicrotransactionsPlugin.StartCallbackListenerAsync` registers the Steamworks `MicroTxnAuthorizationResponse_t` listener and the `MicroTxnAuthorizationResponse` event delivers the raw response (`AppId` / `OrderId` / `Authorized`) on the engine main thread; `StopCallbackListenerAsync` unregisters idempotently. Re-registration surfaces as the typed `AlreadyStarted` outcome, and starting before `SteamAPI.Init()` fails with a `FailedPrecondition` naming the cause.
- Registered with `b.AddSteamMicrotransactions()`; available on Windows / macOS player builds only.
- Depends on `com.com2usplatform.hiveaxyl.steamworks` for Steamworks SDK access, shared with the Steam auth add-on.
