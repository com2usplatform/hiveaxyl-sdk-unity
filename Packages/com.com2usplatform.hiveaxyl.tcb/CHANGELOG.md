# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- TCB (Tencent CloudBase) capability client (`ITcbService`): execute a Cloud Function stored on TCB. Registered with `b.AddTcb()`, which binds it to the app host, `https://app-api.hiveaxyl.com`. TCB has no sandbox host, so there is no `sandbox:` overload; `b.AddTcb(baseUrl)` takes an explicit host for any other environment.
