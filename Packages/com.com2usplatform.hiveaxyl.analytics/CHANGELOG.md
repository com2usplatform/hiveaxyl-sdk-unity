# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Analytics capability client (`IAnalyticsService`): posts a batch of client log events to the Axyl log intake. Registered with `b.AddAnalytics()`, which binds it to the data host (`data-api.hiveaxyl.com`). Analytics has no sandbox host, so there is no `sandbox:` overload; `AddAnalytics(baseUrl)` takes an explicit host for any other environment. Server API: `/analytics/v1/`.
- One intake call, `CollectClientLogAsync` (`POST /analytics/v1/recv`), which keeps the ambient
  credential: with an active session the token rides the request and the gateway derives the
  player from it; with no active session the SDK omits the `Authorization` header, which is what
  makes a log from before sign-in an ordinary call. Its contract declares Bearer auth, so whether
  an unauthenticated intake is accepted there is the server's policy, not a guarantee of this
  client.
- `ClientLogCollectRequest` / `ClientLogEvent`: an event requires `EventTime` and `EventName`;
  `DeviceId`, `UserId` and `IdentifierProvider` are optional. The server overwrites
  `IdentifierProvider`, so sending it is never necessary.
- `ClientLogEvent.AdditionalProperties` carries the attributes the game names itself, which is most of what a log event is for. Each value is a **raw JSON literal**, not display text -- `"42"` for a number, `"\"gold\""` for a string, `"[1,2]"` for an array, `"null"` for a JSON null -- and each entry is written flattened into the same JSON object as `EventName`, not nested under a key of its own, so an attribute such as `level` sits at the top level of the event, where the server reads it. An empty map writes nothing, so an event with no attributes sends exactly what a closed object would.
