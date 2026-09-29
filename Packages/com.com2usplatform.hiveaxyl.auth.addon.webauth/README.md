# com.com2usplatform.hiveaxyl.auth.addon.webauth

OAuth 2.0 (RFC 8252) external user-agent authentication addon for Hive Axyl SDK.

Opens an authorization URL in the platform-provided web auth session and
captures the redirect callback. The addon is **protocol-neutral**: it returns
the raw callback URL and its parsed query parameters. OAuth interpretation
(PKCE, state, nonce generation/validation) and redirect-URI matching are the
App / Auth Capability responsibility.

Supported backends:

| Platform | Native API |
|----------|------------|
| iOS / macOS | `ASWebAuthenticationSession` |
| Android | Chrome Custom Tabs |
| Windows | Loopback HTTP listener |
| WebGL | `window.open` + `postMessage` |

## Usage

```csharp
var plugin = new WebAuthSessionPlugin();
var result = await plugin.OpenAsync(
    new OpenRequest
    {
        Url = authorizationUrl,   // built by the App / Auth Capability
        RedirectUri = redirectUri,
    },
    cancellationToken);

// ExternalUserAgentServiceOpenResult is one of
// (Success / UserCanceled / Failure / UnknownOutcome). On Success,
// result.Data.Parameters carries the parsed callback query string.
//
// Only one session may run at a time: a concurrent OpenAsync resolves with a
// Failure (HiveErrorCode.FailedPrecondition). CancelCurrentSession() aborts the
// in-flight session, resolving its OpenAsync with HiveErrorCode.Cancelled.
```

iOS/macOS, Android (Chrome Custom Tabs), Windows, and WebGL dispatch natively.

On Android the App must register the OAuth redirect scheme via the
`hiveAxylWebAuthRedirectScheme` Gradle manifest placeholder so the redirect
deep-link reaches the addon's callback Activity. Declare it in
`launcherTemplate.gradle`, after enabling **Custom Launcher Gradle Template**
under Player Settings → Publishing Settings:

```groovy
// Assets/Plugins/Android/launcherTemplate.gradle
android { defaultConfig { manifestPlaceholders = [hiveAxylWebAuthRedirectScheme: "com.myapp.oauth"] } }
```

Not `mainTemplate.gradle`: AGP defers a library's placeholder to the application
module, so `unityLibrary` cannot substitute it.

## WebGL integration

The WebGL backend is the `@com2usplatform/hiveaxyl-auth-addon-webauth` npm package,
shipped inside this package as
`Runtime/Plugins/WebGL/HiveAxylAuthAddonWebAuth.jspre`, alongside core's
`HiveAxylCore.jspre` in `com.com2usplatform.hiveaxyl.core`. Unity includes both as
emscripten pre-js: core's installs `Module['HiveAxylBridge']` and this one
installs the registration hook that `ExternalUserAgentService.Register()`
invokes, so the host page needs no changes. The `.jspre` ships prebuilt in this package.

### The redirect (callback) page

The App hosts the redirect page — the SDK ships none. Pass its URL as
`OpenRequest.RedirectUri`, register the identical URL with the provider, and have
the page hand the callback back to the opener:

```html
<body><script>
  // Pin the target origin — never "*". The callback URL carries the
  // authorization code, so "*" would post it to whatever page is the opener.
  // Served from the game's own origin (StreamingAssets hosting, below),
  // window.location.origin IS the parent origin.
  if (window.opener) {
    window.opener.postMessage(window.location.href, window.location.origin);
    window.close();  // the page closes itself
  } else {
    // A COOP header on the provider's pages can sever the opener (the game
    // side then sees the popup as closed and reports user-canceled); leave
    // a hint instead of throwing with the code still in the address bar.
    document.body.textContent = "You can close this window and return to the game.";
  }
</script></body>
```

The addon accepts the message only from the `RedirectUri`'s origin;
it also closes the popup as a fallback if the page did not. Host the page in the
player's own `StreamingAssets` so `window.location.origin` is the game's origin:
nothing has to be substituted at build time, one build serves every host whose
redirect URL you have registered with the provider, and deriving `RedirectUri`
from `Application.streamingAssetsPath` keeps the registered URL and the served
page in lock-step. A page hosted anywhere else must name the game page's origin
as a literal string instead — there `window.location.origin` is the page's own
origin, not the opener's.

Call `OpenAsync` synchronously inside the click that starts sign-in — the popup is a
`window.open`, allowed only within a user activation, and an `await` before it risks
outliving that short-lived activation and having the popup blocked (`UNAVAILABLE`).
