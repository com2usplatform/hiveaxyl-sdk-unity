# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29


### Added

- Auth and Token capability clients: `IAuthService` / `ITokenService` cover guest creation and login, provider list, provider token exchange, provider / username / custom-provider login, username creation, token issuance, and logout.
- `GetBlockStatusAsync` tells a restriction with no message for the default language (`AuthGetBlockStatusResult.BlockTypeContentNotFound`) apart from a block type that does not exist (`BlockTypeNotFound`).
- On token issue, `TokenIssueTokenResult.TemporarilyUnavailable` (the store could not be reached) is the one outcome worth retrying; every other code answers the same however often it is sent. Back off rather than retrying immediately: the code is retryable and the interval is left to the caller.
- Registered with `b.AddAuth().AddToken()`, which puts each on the core host, `https://core-api.hiveaxyl.com`. Neither has a sandbox host, so there is no `sandbox:` overload; `b.AddAuth(baseUrl)` / `b.AddToken(baseUrl)` take an explicit host for any other environment.
- `AuthTokenRefresh.Enable(baseUrl, clientId)` — enables automatic session refresh. On a 401 the SDK refreshes once via the `refresh_token` grant; the refresh token rotates on every use, so the attempt is never replayed. Only the server's verdict that the refresh token is invalid logs the session out; any other failure — including a caller-cancelled refresh — keeps the stored credentials (after three consecutive no-verdict failures the in-memory session ends while the persisted credentials survive; see the core package changelog).
- Login and token endpoints are called without the session `Authorization` header, so they work with no session and an expired session token can never turn a login into a spurious 401.
