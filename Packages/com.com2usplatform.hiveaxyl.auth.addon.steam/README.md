# com.com2usplatform.hiveaxyl.auth.addon.steam

Steam authentication addon for Hive Axyl SDK.

Wraps `ISteamUser::GetAuthTicketForWebApi` to provide a hex-encoded session
ticket for server-side verification via `ISteamUserAuth/AuthenticateUserTicket`.
Supported platforms: **Windows** and **macOS** only.

## Requirements

- `com.com2usplatform.hiveaxyl.steamworks` — Steamworks SDK infrastructure, installed automatically as a dependency. It needs the `com.rlabrecque` scoped registry; see its README.
- Steam client running and logged in at call time
- App must call `SteamAPI.Init()` once at startup and `SteamAPI.RunCallbacks()` every frame

## Usage

The package builds only for Windows, macOS, Linux, and the Editor, so in a
project that also targets Android, iOS, or WebGL, wrap every call into it in
`#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX`. Registration takes effect
only in Windows and macOS player builds; in the Editor nothing is registered,
even with a Windows or macOS build target, so resolve with
`HiveCore.TryResolve` there.

```csharp
// Register at bootstrap:
HiveBootstrap.Initialize(config, b => b.AddSteamAuth());

// Then resolve from the registry. Not registered in the Editor:
if (!HiveCore.TryResolve<ISteamPlugin>(out var plugin))
    return;
var result = await plugin.GetAuthTicketForWebApiAsync(
    new GetAuthTicketForWebApiRequest { Identity = "my-server-id" },
    cancellationToken);

switch (result)
{
    case SteamServiceGetAuthTicketForWebApiResult.Success s:
        await SendTicketToServer(s.Data.TicketHex);
        // Release the ticket once the server has validated it, whatever the
        // outcome. Steam limits outstanding tickets, and releasing one before
        // validation makes Steam reject it.
        plugin.ReleaseTicket(s.Data.TicketHex);
        break;
    case SteamServiceGetAuthTicketForWebApiResult.NotAuthenticated:
        ShowSteamLoginPrompt();
        break;
    default:
        if (result.IsUntypedProblem) ShowErrorDialog(result.UntypedProblem);
        break;
}
```
