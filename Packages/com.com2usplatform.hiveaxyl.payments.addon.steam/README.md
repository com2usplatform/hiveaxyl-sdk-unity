# com.com2usplatform.hiveaxyl.payments.addon.steam

Steam Microtransactions addon for Hive Axyl SDK.

Wraps the Steamworks SDK `MicroTxnAuthorizationResponse_t` callback so the App
receives the raw Steam payment-authorization response (`appId` / `orderId` /
`authorized`). The server-side flow (`InitTxn` / `FinalizeTxn` / IPN webhook) is
the game server ↔ Steam Web API boundary and is out of SDK scope.
Supported platforms: **Windows** and **macOS** only.

## Requirements

- `com.com2usplatform.hiveaxyl.steamworks` — Steamworks SDK infrastructure, shared with the Steam Auth addon, installed automatically as a dependency
- Steam client running and logged in at call time
- App must call `SteamAPI_Init()` and `SteamAPI_RunCallbacks()` per frame

## Usage

```csharp
// Register at bootstrap (a no-op in the Editor and off Windows/macOS):
HiveBootstrap.Initialize(config, b => b.AddSteamMicrotransactions());

// Then resolve from the registry once the Steamworks infrastructure is running:
ISteamMicrotransactionsPlugin plugin = HiveCore.Resolve<ISteamMicrotransactionsPlugin>();

plugin.MicroTxnAuthorizationResponse += response =>
{
    if (response.Authorized)
    {
        // User approved in the Steam Overlay — proceed with server-side finalize.
        BeginServerFinalize(response.OrderId);
    }
    else
    {
        // User canceled in the Steam Overlay.
        CancelPurchaseFlow(response.OrderId);
    }
};

// Register the listener when entering the IAP flow. Always inspect the Result:
// discarding it leaves the listener unregistered while the event stays subscribed,
// so the Steam callback never arrives (a silent failure).
var startResult = await plugin.StartCallbackListenerAsync();
switch (startResult)
{
    case SteamMicrotransactionsServiceStartCallbackListenerResult.Success:
        // Listener registered; the handler above now receives Steam callbacks.
        break;
    case SteamMicrotransactionsServiceStartCallbackListenerResult.AlreadyStarted:
        // A listener is already active for this flow — nothing more to do.
        break;
    default:
        // Failure (e.g. Steamworks not initialized) — surface it instead of
        // entering the purchase flow with a listener that will never fire.
        if (startResult.IsUntypedProblem) ShowErrorDialog(startResult.UntypedProblem);
        break;
}

// ... purchase flow runs; Steam delivers the callback to the handler above ...

// Unregister when leaving the IAP flow. Stop is a cleanup operation: on a live
// plugin it always succeeds, ignores cancellation, and is idempotent when
// nothing is registered.
await plugin.StopCallbackListenerAsync();
```
