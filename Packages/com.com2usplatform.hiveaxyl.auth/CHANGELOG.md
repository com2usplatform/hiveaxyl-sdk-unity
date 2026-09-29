# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29


### Added

- `AuthGetBlockStatusResult.BlockTypeContentNotFound` — the block type exists but has no
  content for the default language, so there is no message to show the player. It is
  distinct from `BlockTypeNotFound`, which says the block type itself is unknown: the
  first is missing copy on a real restriction, the second a restriction that does not
  exist.
- `TokenIssueTokenResult.TemporarilyUnavailable` — the store could not be reached. It is the one
  outcome on token issue worth retrying; every other code answers the same however often it is
  sent. Back off rather than retrying immediately: the contract states the code is retryable and
  leaves the interval to the caller.
- Auth and Token capability clients: `IAuthService` / `ITokenService` cover guest creation and login, provider list, provider token exchange, provider / username / custom-provider login, username creation, token issuance, and logout.
- Registered with `b.AddAuth().AddToken()`, which puts each on the core host, `https://core-api.hiveaxyl.com`. The spec declares no sandbox for either, so no sandbox overload is generated and asking for one does not compile; `b.AddAuth(baseUrl)` / `b.AddToken(baseUrl)` take an explicit host for an environment the spec does not declare.
- `AuthTokenRefresh.Enable(baseUrl, clientId)` — enables automatic session refresh. On a 401 the SDK refreshes once via the `refresh_token` grant; the refresh token rotates on every use, so the attempt is never replayed. Only the server's verdict that the refresh token is invalid logs the session out; any other failure — including a caller-cancelled refresh — keeps the stored credentials (after three consecutive no-verdict failures the in-memory session ends while the persisted credentials survive; see the core package changelog).
- Login and token endpoints are called without the session `Authorization` header, so they work with no session and an expired session token can never turn a login into a spurious 401.
