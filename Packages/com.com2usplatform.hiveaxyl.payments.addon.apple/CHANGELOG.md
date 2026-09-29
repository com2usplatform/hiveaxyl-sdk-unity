# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- StoreKit 2 on iOS / macOS: `IAppleStoreKitPlugin` covers `GetProductsAsync`, `PurchaseAsync`, `StartTransactionObserverAsync` / `StopTransactionObserverAsync` with the `TransactionUpdated` event, `GetCurrentEntitlementsAsync`, `GetAllTransactionsAsync`, `GetUnfinishedTransactionsAsync`, `FinishTransactionAsync`, `SyncAsync`, and `GetStorefrontAsync`. User cancellation surfaces as the typed `UserCanceled` outcome (implementing the cross-add-on `IUserCanceledOutcome` marker), alongside the `Pending`, `AlreadyStarted`, and `TransactionNotFound` outcomes.
- Raw StoreKit 2 data preserved without flattening: `AppleProduct` / `SubscriptionInfo` / `Offer` and the full `AppleTransaction` — including `JwsRepresentation` and the verification status — so the backend can verify transactions from the original signed payloads. Sensitive transaction fields are masked in SDK logs.
- Registered with `b.AddStoreKit()`; available on iOS / macOS player builds only.
