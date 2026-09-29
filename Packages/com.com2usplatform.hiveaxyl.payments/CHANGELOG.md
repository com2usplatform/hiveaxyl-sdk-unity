# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29

### Added

- Payments capability client (`IPaymentsService`) covering the PG / Apple / Google / Steam purchase flow: payment page URL creation, pre-purchase, post / finalize / restore purchase, and subscriptions. Receipt verification is a server-to-server call the spec marks server-only, so it is not part of this client. Payments runs on its own commerce hosts. Registered with `b.AddPayments()`, which binds it to `https://commerce-api.hiveaxyl.com`; `b.AddPayments(sandbox: true)` sends it to `https://sandbox-commerce-api.hiveaxyl.com` without moving any other capability — payments is the only capability whose spec declares a sandbox server, so it is the only one handed the `sandbox:` overload, and `b.AddPayments(baseUrl)` takes an explicit host.
- Which request fields you must fill: `Price` and `Currency` are optional on `PurchaseRequest`
  (`decimal?` / `string?`) — neither is ever what the server records, so they only serve its
  amount comparison, and sending one without the other compares the amount alone. `AccountUuid` is
  optional and nullable on every request that still carries it, so an unset one is left off the
  payload rather than sent as an empty string. Set it — derived from `playerId` as the field
  documents — whenever the server should compare the account.
- `ProductOffer` covers one-time and subscription offers alike and carries `BasePlanId` (null for
  a one-time offer). `offerToken` identifies an offer — `offerId` is null on a base offer, so it
  cannot tell the two apart.
- Response properties the server declares always present come through non-nullable: three on
  `PurchaseInitResponseData`, two each on `ProductResponseData` and
  `SubscriptionPrePurchaseResponseData`, and one each on `StoreResponseData`, `StoreProduct`,
  `RestorePurchase`, `SubscriptionPurchaseResponseData` and
  `SubscriptionPurchasePostResponseData`. Reading one needs no null check.
