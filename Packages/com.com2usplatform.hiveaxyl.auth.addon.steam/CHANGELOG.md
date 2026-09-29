# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Steam authentication on Windows and macOS: `ISteamPlugin.GetAuthTicketForWebApiAsync` returns a hex-encoded Steam Web API session ticket via `ISteamUser::GetAuthTicketForWebApi`. A delivered ticket stays alive until the backend validates it — call `ReleaseTicket(ticketHex)` once validation completes, since Steam caps concurrently outstanding tickets; `Dispose` cancels any unreleased ticket as a shutdown backstop.
- Registered with `b.AddSteamAuth()`; available on Windows / macOS player builds only.
- Depends on `com.com2usplatform.hiveaxyl.steamworks` for Steamworks SDK access, shared with the Steam payments add-on.
