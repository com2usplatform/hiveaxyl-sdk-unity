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
  Purchase capability enabled for the App ID (the build post-processor that
  writes the Xcode capability ships with the native adapter)

## Status

This package currently provides the `AppleStoreKitService` Proto IDL contract
and the generated Unity bridge (`IAppleStoreKitServiceBridge`) with the raw
`AppleProduct` / `AppleTransaction` DTOs. The iOS/macOS native adapter ships in
the `HiveAxylPaymentsAddonApple` Swift package, and the managed plugin facade
that drives it lands in a later release.
