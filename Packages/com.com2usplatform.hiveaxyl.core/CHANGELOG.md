# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

This package supports WebGL with the same 1.0.0 API and Semantic Versioning as on the other platforms. WebGL verification has not been completed for this release; issues it finds will be fixed in later releases.

### Added

- SDK bootstrap and service registry. `HiveBootstrap.Initialize(config, b => ...)` is the single entry point: it starts the SDK, runs the registration closure where capabilities and add-ons are wired (`b.AddAuth()`, `b.AddFCM()`, ...), and forwards application pause / resume / quit automatically. Resolve any registered service with `HiveCore.Resolve<T>()` or `HiveCore.TryResolve<T>(out var service)`; the registry freezes when the closure returns. `HiveCore.Shutdown` disposes registered `IDisposable` services at shutdown (disposal order across services is unspecified), so the SDK can be re-initialized.
- Immutable configuration: `CoreConfig` with a fluent, validating `CoreConfigBuilder` covering app, auth, log, and network options.
- Typed result model shared by every capability and add-on: each call returns a `{Method}Result` with `Success`, typed outcome variants, `UnknownOutcome` (unrecognized server outcome codes are preserved, not errors), and `Failure` for infrastructure problems, marked with the cross-cutting `IUntypedProblem` interface. Every variant exposes the original response body via `RawResponse`, so a server-added field can be read without an SDK update.
- HTTP transport pipeline: interceptor-based request processing with automatic common headers, W3C `traceparent` request tracing, `Idempotency-Key`, structured request / response logging with PII masking, automatic retry with exponential backoff, jitter, and `Retry-After` support, and per-call timeout / retry / logging overrides via `ApiCallContext` and `RequestOptions`.
- Session management with automatic token refresh: on a 401 the SDK performs a single refresh through the Auth-supplied handler and replays waiting requests. Only the server's verdict that the refresh token itself is invalid destroys the session and raises `OnSessionExpired`; a transient failure or caller cancellation keeps the stored credentials (three consecutive no-verdict failures end the in-memory session, but the persisted credentials survive for the next restore).
- Per-call credential: `ApiCallContext.Credential` selects `Ambient` (the SDK session), `Anonymous` (no `Authorization` header), or `Bearer(token)`, with `WithAccessToken(token)` as the session-restore shorthand. A non-ambient call never triggers, nor waits behind, the automatic refresh — its 401 judges the supplied token, so a stored token can be validated before being committed as the session. A `Bearer` with an empty token fails with `InvalidArgument` before reaching the network.
- `HiveCore.SetLanguage(string?)` — override the `Accept-Language` advertised to the server at runtime (BCP 47 tag), or pass `null` to restore the device OS locale. Takes effect from the next request.
- Native bridge to the platform integration layer on Android (JNI), iOS / macOS (Swift dispatcher), Windows (P/Invoke DLL), and WebGL (jslib), with async call marshalling, native event subscription, cancellation propagation, and deterministic disposal. Build post-processing embeds the Apple xcframeworks into the generated Xcode project automatically, and on WebGL the browser runtime ships as a bundled `.jspre` that installs the bridge before startup — a WebGL build needs no host-page wiring. Windows additionally requires the Microsoft Visual C++ 2015–2022 Redistributable (x64) on every machine that runs the game, including your own while developing.
- Logging: `ILogger` with pluggable sinks (`HiveCore.AddLogSink`), levels, and PII masking; `IDispatcher` main-thread dispatching; dependency-free streaming JSON serialization used by the generated clients.
