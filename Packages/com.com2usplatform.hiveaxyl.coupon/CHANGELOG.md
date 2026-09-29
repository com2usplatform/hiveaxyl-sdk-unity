# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-29


### Added

- Coupon capability client (`ICouponService`): `RedeemCouponAsync` redeems a coupon code, which grants its reward through the mailbox and returns the mail receipt id (`MailId`). Registered with `b.AddCoupon()`, which binds it to the commerce host, `https://commerce-api.hiveaxyl.com`. The spec declares no sandbox for coupon, so no sandbox overload is generated and asking for one does not compile; `b.AddCoupon(baseUrl)` takes an explicit host for any environment the spec does not declare. Redemption from inside the game is this call. Server API: `/coupon/v1/`.
- Typed redemption outcomes: `CouponCodeNotFound`, `CouponCodeInvalidFormat`, `CouponExpired`, `CouponBeforeStart`, `CouponAlreadyUsed`, `CouponDisabled`, and the `CouponAccountLimitExceeded` / `CouponGroupLimitExceeded` / `CouponTotalLimitExceeded` usage limits. A platform-level problem (an expired session, a rate limit) stays a `Failure`, so the two never have to be told apart by reading a code.
