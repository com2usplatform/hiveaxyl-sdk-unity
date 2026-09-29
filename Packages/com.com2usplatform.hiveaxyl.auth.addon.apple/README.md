# com.com2usplatform.hiveaxyl.auth.addon.apple

Apple Sign-In authentication addon for Hive Axyl SDK.

Wraps `ASAuthorizationAppleIDProvider` / `ASAuthorizationController` to
return the raw `ASAuthorizationAppleIDCredential` fields. Supported
platforms: **iOS 17.0+**, **macOS 15.0+**.

OAuth interpretation, identity token validation, nonce generation, and
`email` / `fullName` persistence are App / Auth Capability responsibilities.

## Requirements

- "Sign In with Apple" enabled for the App ID in the Apple Developer Portal
  (the addon writes the Xcode entitlement automatically at build time)
- App-generated raw nonce + SHA256 hex hash passed to the Plugin

## Usage

```csharp
var plugin = new AppleSignInPlugin();
var result = await plugin.LoginAsync(
    new AppleSignInServiceLoginRequest
    {
        NonceHash = sha256HexOfRawNonce,
        RequestedScopes = new[]
        {
            RequestedScope.Email,
            RequestedScope.FullName,
        },
    },
    cancellationToken);

// AppleSignInServiceLoginResult is one of
// (Success / UserCanceled / Failure / UnknownOutcome). The iOS/macOS
// native adapter ships in the HiveAxylAuthAddonApple Swift package.
```
