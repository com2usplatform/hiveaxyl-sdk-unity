# Hive Axyl Steamworks

Steamworks SDK infrastructure module for the Hive Axyl SDK.
Wraps the [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) (rlabrecque, MIT, v20.2.0+) managed binding and
native plugins as the **single entry point** for all Steamworks-dependent packages
(`com.com2usplatform.hiveaxyl.auth.addon.steam`, `com.com2usplatform.hiveaxyl.payments.addon.steam`, etc.).
Steamworks.NET was chosen as the managed binding over Facepunch.Steamworks.

---

## Installation

### 1. Add the Steamworks.NET registry

Add the following entry to the `"scopedRegistries"` array in your project's
`Packages/manifest.json`. If the array does not exist yet, create it at the top level.

```json
"scopedRegistries": [
  {
    "name": "rlabrecque",
    "url": "https://package.openupm.com",
    "scopes": ["com.rlabrecque"]
  }
]
```

Alternatively, open **Edit → Project Settings → Package Manager**, scroll to
**Scoped Registries**, click **+**, and fill in Name, URL, and Scope as above.

### 2. Add the Hive Axyl registry and install

Follow the Hive Axyl SDK installation guide to add the Hive registry, then install
`com.com2usplatform.hiveaxyl.steamworks` via **Window → Package Manager**.

---

## App Responsibilities: SteamAPI Lifecycle

> **This module does not call `SteamAPI_Init()` or `SteamAPI_RunCallbacks()`.**
> These are the App's (or engine plugin's) responsibility. Addon packages that depend
> on this module (`auth.addon.steam`, etc.) assume that the App has completed this setup.

### SteamAPI.Init()

Call `SteamAPI.Init()` once at application startup **before** using any Steamworks
functionality. Place `steam_appid.txt` (containing your Steam App ID) in the project root
during development if running outside Steam.

```csharp
using Steamworks;

void Awake()
{
    if (!SteamAPI.Init())
    {
        // Steam client is not running, or steam_appid.txt is missing / App ID mismatch.
        // Show a "Please start Steam" UI and exit, or disable Steam features.
        Debug.LogError("SteamAPI.Init() failed.");
        return;
    }
}
```

### SteamAPI.RunCallbacks()

Call `SteamAPI.RunCallbacks()` **every frame** (e.g., in a `MonoBehaviour.Update()`)
to dispatch pending Steamworks callbacks. Without this, any Steamworks operation that
uses callbacks — including `GetAuthTicketForWebApi` — will never receive its response
and will time out via `CancellationToken`.

```csharp
void Update()
{
    if (SteamAPI.IsSteamRunning())
        SteamAPI.RunCallbacks();
}
```

### SteamAPI.Shutdown()

Call `SteamAPI.Shutdown()` once when the application exits.

```csharp
void OnApplicationQuit()
{
    SteamAPI.Shutdown();
}
```

---

## Usage

```csharp
using Hive.Axyl.Steamworks;

// After SteamAPI.Init() succeeds:
ISteamworksContext steamworks = SteamworksContext.CreateDefault();

if (!steamworks.IsInitialized)
{
    // Steamworks is unavailable on this platform, or SteamAPI.Init() has not been called.
    return;
}

// steamworks.IsInitialized == true → safe to proceed with Steam operations.
```

---

## Supported Platforms

| Platform | Supported | Notes |
|----------|:---------:|-------|
| Windows (x86_64) | ✓ | Steam client must be running |
| macOS | ✓ | Steam client must be running |
| Android | ✗ | Use `auth.addon.webauth` for Steam OpenID |
| iOS | ✗ | Use `auth.addon.webauth` for Steam OpenID |
| WebGL | ✗ | Use `auth.addon.webauth` for Steam OpenID |
| Linux | ✗ | Not supported |

---

## Related Packages

- `com.com2usplatform.hiveaxyl.auth.addon.steam` — Steam auth ticket provider (depends on this package)
- `com.com2usplatform.hiveaxyl.core` — Core Hive Axyl SDK infrastructure
