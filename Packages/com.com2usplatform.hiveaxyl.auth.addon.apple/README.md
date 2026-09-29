# com.com2usplatform.hiveaxyl.auth.addon.apple

Apple Sign-In authentication addon for Hive Axyl SDK.

Wraps `ASAuthorizationAppleIDProvider` / `ASAuthorizationController` to
return the raw `ASAuthorizationAppleIDCredential` fields. Supported
platforms: **iOS 17.0+**, **macOS 15.0+**.

OAuth interpretation, identity token validation, nonce generation, and
`email` / `fullName` persistence are the app's responsibility.

## Requirements

- "Sign In with Apple" enabled for the App ID in the Apple Developer Portal,
  and the provisioning profile reissued afterwards. The addon writes the Xcode
  entitlement at build time for iOS builds and for macOS builds with
  **Create Xcode Project** enabled; a macOS build that produces the .app
  directly is not edited, so add the entitlement when you sign that build
- App-generated raw nonce + SHA256 hex hash passed to the Plugin

## Usage

```csharp
// Register at bootstrap. Registration takes effect only in iOS and macOS
// player builds; in the Editor and on other targets nothing is registered.
HiveBootstrap.Initialize(config, b => b.AddAppleSignIn());

// Then resolve from the registry on iOS or macOS:
IAppleSignInPlugin plugin = HiveCore.Resolve<IAppleSignInPlugin>();
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
// (Success / UserCanceled / Failure / UnknownOutcome).
```

`HiveCore.Resolve` throws `RegistrationNotFoundException` where nothing was
registered, so resolve the plugin only in iOS or macOS player builds, or use
`HiveCore.TryResolve`. The iOS and macOS native adapters are bundled in this
package (`Runtime/Plugins/iOS`, `Runtime/Plugins/macOS`).
