# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Mailbox capability client (`IMailboxService`): send, list / retrieve / delete sent and received mail, mark-read, and recall. An attachment's `AttachmentType` is optional and starts at `"DEFAULT"`, so an attachment sent without a type is accepted. Registered with `b.AddMailbox()`, which binds it to the app host, `https://app-api.hiveaxyl.com`. Mailbox has no sandbox host, so there is no `sandbox:` overload; `b.AddMailbox(baseUrl)` takes an explicit host for any other environment.
