# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29


### Added

- Service Access-Control capability client (`IServiceAccessService`): maintenance info, client-update check, and country-block check. Registered with `b.AddServiceAccess()`, which binds it to the core host, `https://core-api.hiveaxyl.com`. It has no sandbox host, so there is no `sandbox:` overload; `b.AddServiceAccess(baseUrl)` takes an explicit host.
- All three checks are called without the session `Authorization` header, so they work before login and an expired session token cannot cause a spurious 401 on them.
- `X-Hive-Country-Code` is injected by the gateway, which derives it from the caller's IP, so the SDK never sends it.
