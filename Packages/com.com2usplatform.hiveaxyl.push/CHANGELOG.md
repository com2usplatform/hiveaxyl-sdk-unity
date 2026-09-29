# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Push capability client (`IPushService`): register / sync a push token, change its language and receive-consent, and unbind the identifier. Registered with `b.AddPush()`, which binds it to the app host, `https://app-api.hiveaxyl.com`. The spec declares no sandbox for push, so no sandbox overload is generated and asking for one does not compile; `b.AddPush(baseUrl)` takes an explicit host for any environment the spec does not declare.
