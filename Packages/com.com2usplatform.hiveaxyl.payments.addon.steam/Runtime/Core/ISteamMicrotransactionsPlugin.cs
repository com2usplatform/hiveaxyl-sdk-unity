// Copyright (c) Com2uS Platform Corp. All rights reserved.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Axyl.Payments.Addon.Steam
{
    /// <summary>
    /// Steam Microtransactions surface exposed to game code. Wraps the Steamworks
    /// <c>MicroTxnAuthorizationResponse_t</c> callback so the App receives the raw
    /// payment-authorization response. Supported platforms: Windows and macOS only.
    /// </summary>
    /// <remarks>
    /// Prerequisites: the SDK never initializes the Steam API. The application must
    /// call <c>SteamAPI.Init()</c> once at startup and pump
    /// <c>SteamAPI.RunCallbacks()</c> every frame so authorization callbacks are
    /// delivered; starting the callback listener before initialization fails with a
    /// <c>FailedPrecondition</c> error rather than throwing.
    /// </remarks>
    public interface ISteamMicrotransactionsPlugin : IDisposable
    {
        /// <summary>
        /// Raised when Steamworks delivers a <c>MicroTxnAuthorizationResponse_t</c>
        /// callback after the user approves or cancels a purchase in the Steam Overlay.
        /// Fires only while a listener is registered via
        /// <see cref="StartCallbackListenerAsync"/>. Delivered on the engine main thread.
        /// </summary>
        event Action<SteamMicroTxnResponse> MicroTxnAuthorizationResponse;

        /// <summary>
        /// Registers the <c>MicroTxnAuthorizationResponse_t</c> callback listener.
        /// Returns <c>AlreadyStarted</c> when a listener is already registered, or a
        /// Failure when the Steamworks infrastructure is not initialized.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the plugin has been disposed.
        /// </exception>
        Task<SteamMicrotransactionsServiceStartCallbackListenerResult> StartCallbackListenerAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Unregisters the callback listener. Idempotent: calling when no listener is
        /// registered is not an error. As a cleanup operation it ignores cancellation —
        /// the listener is released even when <paramref name="ct"/> is already cancelled.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the plugin has been disposed.
        /// </exception>
        Task<SteamMicrotransactionsServiceStopCallbackListenerResult> StopCallbackListenerAsync(
            CancellationToken ct = default);
    }
}
