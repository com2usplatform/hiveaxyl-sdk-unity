# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Shared Steamworks infrastructure for the Steam auth and payments add-ons. Integrates Steamworks.NET 20.2.0 (`com.rlabrecque.steamworks.net`) via OpenUPM as the managed binding; the required native binaries are bundled by the upstream package.
- `ISteamworksContext` with `IsInitialized` for checking Steamworks SDK availability: Steamworks.NET-backed on Windows and macOS, and a no-op stub that reports `false` on every other platform.
