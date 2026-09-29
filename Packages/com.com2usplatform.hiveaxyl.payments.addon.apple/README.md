# com.com2usplatform.hiveaxyl.payments.addon.apple

Apple StoreKit2 in-app purchase addon for Hive Axyl SDK.

Wraps StoreKit2 `Product` / `Transaction` / `AppStore` to expose raw product,
purchase, and transaction fields — including the signed `jwsRepresentation` and
the `VerificationResult` branch — directly at the SDK surface. Supported
platforms: **iOS 17.0+**, **macOS 15.0+**.

Purchase orchestration, receipt normalization / persistence, and client-side
JWS verification are out of scope: the addon passes raw values through and the
App + game server own the domain logic.

## Requirements

- In-App Purchase products registered in App Store Connect, and the In-App
  Purchase capability enabled for the App ID. Nothing in the SDK adds the
  In-App Purchase capability to the Xcode project; add it yourself.

## Usage

```csharp
// Register at bootstrap. Registration takes effect only in iOS and macOS
// player builds; in the Editor and on other targets nothing is registered.
HiveBootstrap.Initialize(config, b => b.AddStoreKit());

// Then resolve from the registry on iOS or macOS:
IAppleStoreKitPlugin plugin = HiveCore.Resolve<IAppleStoreKitPlugin>();
var products = await plugin.GetProductsAsync(
    new GetProductsRequest { ProductIds = new[] { "com.mygame.gems100" } },
    cancellationToken);
```

`HiveCore.Resolve` throws `RegistrationNotFoundException` where nothing was
registered, so resolve the plugin only in iOS or macOS player builds, or use
`HiveCore.TryResolve`. The iOS and macOS native adapters are bundled in this
package (`Runtime/Plugins/iOS`, `Runtime/Plugins/macOS`).
