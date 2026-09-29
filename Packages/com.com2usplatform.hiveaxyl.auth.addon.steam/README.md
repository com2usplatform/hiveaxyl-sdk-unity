# com.com2usplatform.hiveaxyl.auth.addon.steam

Steam authentication addon for Hive Axyl SDK.

Wraps `ISteamUser::GetAuthTicketForWebApi` to provide a hex-encoded session
ticket for server-side verification via `ISteamUserAuth/AuthenticateUserTicket`.
Supported platforms: **Windows** and **macOS** only.

## Requirements

- `com.com2usplatform.hiveaxyl.steamworks` — Steamworks SDK infrastructure, installed automatically as a dependency
- Steam client running and logged in at call time
- App must call `SteamAPI_Init()` and `SteamAPI_RunCallbacks()` per frame

## Usage

```csharp
// Register at bootstrap:
HiveBootstrap.Initialize(config, b => b.AddSteamAuth());

// Then resolve from the registry:
ISteamPlugin plugin = HiveCore.Resolve<ISteamPlugin>();
var result = await plugin.GetAuthTicketForWebApiAsync(
    new GetAuthTicketForWebApiRequest { Identity = "my-server-id" },
    cancellationToken);

switch (result)
{
    case SteamServiceGetAuthTicketForWebApiResult.Success s:
        SendTicketToServer(s.Data.TicketHex);
        break;
    case SteamServiceGetAuthTicketForWebApiResult.NotAuthenticated:
        ShowSteamLoginPrompt();
        break;
    default:
        if (result.IsUntypedProblem) ShowErrorDialog(result.UntypedProblem);
        break;
}
```
