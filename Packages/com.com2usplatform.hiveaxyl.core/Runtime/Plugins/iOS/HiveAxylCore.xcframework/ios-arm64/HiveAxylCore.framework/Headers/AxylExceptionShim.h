// Copyright (c) Com2uS Platform Corp. All rights reserved.

#ifndef AXYL_EXCEPTION_SHIM_H
#define AXYL_EXCEPTION_SHIM_H

#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

// The bridge layer must wrap native calls in
// `try-catch` (Android JNI) / `@try-@catch` (Apple, Objective-C).
// Caught exceptions surface as `HiveErrorCode.Internal` envelopes.
//
// Swift's `do { try ... } catch` only catches `Error` conformers; `NSException`
// raised by AppKit/UIKit/Foundation traverses Swift frames as a non-Swift
// unwind and ends in `std::terminate` if not caught here. This module is the
// Bridge layer's mandated `@try`/`@catch` boundary for the Apple platform.

/// Block returning a non-null `NSString`. Used for sync plugin entry points
/// that produce a JSON response.
typedef NSString * _Nonnull (^AxylGuardedReturning)(void);

/// Block with no return value. Used for async/fire-and-forget plugin entry
/// points whose responses surface through a separate callback channel.
typedef void (^AxylGuardedVoid)(void);

/// Runs `block` under `@try`/`@catch(NSException *)`.
///
/// - Parameters:
///   - block: the call to guard.
///   - outReason: on `NSException`, populated with `name: reason`;
///     left untouched on normal completion.
/// - Returns: block's return value, or `nil` if an `NSException` was caught.
NSString * _Nullable AxylRunGuarded(
    AxylGuardedReturning block,
    NSString * _Nullable * _Nonnull outReason
) NS_SWIFT_NAME(AxylRunGuarded(_:outReason:));

/// Void variant of `AxylRunGuarded` for fire-and-forget plugin calls.
///
/// - Parameters:
///   - block: the call to guard.
///   - outReason: on `NSException`, populated with `name: reason`;
///     left untouched on normal completion.
/// - Returns: `YES` on normal completion, `NO` if an `NSException` was caught.
BOOL AxylRunGuardedVoid(
    AxylGuardedVoid block,
    NSString * _Nullable * _Nonnull outReason
) NS_SWIFT_NAME(AxylRunGuardedVoid(_:outReason:));

NS_ASSUME_NONNULL_END

#endif // AXYL_EXCEPTION_SHIM_H
