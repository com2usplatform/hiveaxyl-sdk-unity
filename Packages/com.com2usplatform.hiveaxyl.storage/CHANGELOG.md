# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- `ISecureStorage` — encrypted string persistence over each platform's secure store, with `SaveAsync`, `LoadAsync`, `DeleteAsync`, and `ClearAsync`. `LoadAsync` reports key absence as a successful load with a null value.
- Platform backends: Android (encrypted DataStore), iOS / macOS (Keychain, scoped per app), and Windows (DPAPI, current-user scope).
- Typed recovery outcomes: `Save` / `Load` / `Delete` expose `DataCorrupted` and `AccessDenied`; `Clear` exposes only `AccessDenied`, so an app cannot be led to wipe intact data. `DataCorrupted` is reserved for locally confirmed, permanent corruption — recoverable faults (disk full, transient I/O) surface as a plain `Failure` so the store is preserved. The `IDataCorruptedOutcome` / `IAccessDeniedOutcome` markers let an app branch on the recovery path uniformly across all four calls.
- Registered with `b.AddSecureStorage()`; available on iOS / macOS / Android / Windows player builds only. Windows additionally requires the Microsoft Visual C++ 2015–2022 Redistributable (x64) on every machine that runs the game, including your own while developing.
