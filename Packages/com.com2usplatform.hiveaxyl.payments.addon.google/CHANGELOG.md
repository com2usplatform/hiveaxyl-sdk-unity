# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Google Play Billing on Android over Billing Library 9 (`com.android.billingclient:billing:9.0.0`): `IGooglePlayBillingPlugin` covers the raw connection lifecycle, product query (with v9 partial-failure results), the purchase flow (the current Activity is injected automatically), query purchases (restore), separate acknowledge / consume, and the `PurchasesUpdated` / `BillingServiceDisconnected` events. Raw `GoogleProductDetails` / `GooglePurchase` / `BillingResult` are exposed verbatim, without normalization.
- Ergonomic field types: `GooglePurchase.PurchaseTime` is a `DateTimeOffset`, and the offer / pricing-phase `PriceAmount` fields are `decimal` with Billing's micros precision preserved.
- Registered with `b.AddPlayBilling()`; available on Android player builds only.
