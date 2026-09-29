# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Firebase Cloud Messaging on Android: `IFCMPlugin` gets / deletes the FCM token, reads the notification that launched the app (cold-start tap), and raises the incoming-message and token-refresh events. Message `SentTime` / `Ttl` surface as `DateTimeOffset` / `TimeSpan`.
- Zero-Gradle-work Firebase setup: the add-on applies Google's `com.google.gms:google-services` plugin to the generated Android project at build time and copies the game's `Assets/Plugins/Android/google-services.json` into the launcher module. A missing file warns and skips the pass (token calls — `GetTokenAsync` / `DeleteTokenAsync` — then return `FailedPrecondition`, while the cold-start read still works; projects without FCM are unaffected), and a `google-services.json` whose `package_name` differs from the build's application id fails the Gradle build.
- Registered with `b.AddFCM()`; available on Android player builds only.
