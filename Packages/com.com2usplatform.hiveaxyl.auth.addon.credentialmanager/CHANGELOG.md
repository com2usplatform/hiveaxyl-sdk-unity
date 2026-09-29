# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Google sign-in on Android via the Credential Manager unified picker: `IAndroidCredentialManagerPlugin.LoginAsync` returns the Google ID token credential. `GoogleIdTokenCredential.UniqueId` is the stable Google account id — the value to send as `providerUserId` — and `Email` is the account's email address; the deprecated Google `Id` field is forwarded verbatim but holds an email, not an identifier. User cancellation and the no-saved-credentials case surface as the typed `UserCanceled` / `NoCredentials` outcomes.
- Registered with `b.AddCredentialManager()`; available on Android player builds only.
- Requires `com.google.android.libraries.identity.googleid:googleid` 1.2.0 or newer at runtime; an older resolved version fails with a `FailedPrecondition` naming the requirement.
- The raw native response is preserved on every result variant via `RawResponse`, including the id token and account email on `Success`.
