// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// iOS / macOS P/Invoke-based <see cref="IPlatformInvoker"/>. Delegates to the
    /// Swift-side <c>AxylBridgeDispatcher</c> exposed through the <c>@_cdecl</c> entry
    /// points in the HiveAxylCore framework's <c>AxylNativeBridge.swift</c>.
    /// Owns the engine-bound surface only — Core-side concerns (logging, callback
    /// correlation, event dispatch) live in <see cref="NativeBridge"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Threading: <see cref="CallSync"/> runs on the caller thread; <see cref="CallAsync"/>
    /// returns immediately after issuing the native call. Reverse-P/Invoke trampolines
    /// arrive on whichever thread the native side invokes on; this invoker forwards them
    /// directly to the constructor-supplied composer delegates, and the composer is
    /// responsible for routing them to the main thread.
    /// </para>
    /// <para>
    /// Lifecycle: the constructor stores the composer-side sinks <i>before</i> calling
    /// <see cref="PInvokeInterop.SetCallbacks"/>, so a fast reverse-P/Invoke cannot land
    /// on a null sink. <see cref="Dispose"/> is idempotent; late native responses after
    /// shutdown are silently dropped by <see cref="NativeBridge"/>'s correlation manager.
    /// </para>
    /// <para>
    /// Exception semantics: any exception thrown by <see cref="CallSync"/> or
    /// <see cref="CallAsync"/> — including <see cref="DllNotFoundException"/> when the
    /// native framework is missing, <see cref="EntryPointNotFoundException"/> when a
    /// symbol name has drifted, or marshal failures — is caught by
    /// <see cref="NativeBridge"/> and returned as an <c>INTERNAL</c> envelope. Callers
    /// of <see cref="INativeBridge"/> never observe a raw P/Invoke exception.
    /// </para>
    /// <para>
    /// A null pointer from the native <c>CallSync</c> entry — documented on the Swift
    /// side as the ENOMEM branch of <c>strdup</c> — is converted here into an
    /// <c>INTERNAL</c> envelope rather than propagated as a
    /// <see cref="NullReferenceException"/>.
    /// </para>
    /// </remarks>
    internal sealed class ApplePlatformInvoker : IPlatformInvoker
    {
        private Action<int, string>? m_onAsyncResponse;
        private Action<string, string>? m_onEvent;
        private int m_disposed; // 0 = alive, 1 = disposed

        /// <summary>
        /// Stores the composer-side sinks then registers the reverse-P/Invoke trampolines
        /// via <see cref="PInvokeInterop.SetCallbacks"/>. The static-slot semantics of
        /// <see cref="PInvokeInterop"/> imply a single <see cref="ApplePlatformInvoker"/>
        /// per process.
        /// </summary>
        /// <param name="onAsyncResponse"><c>(callbackId, responseJson)</c> sink for async response.</param>
        /// <param name="onEvent"><c>(eventName, jsonPayload)</c> sink for one-way native events.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="onAsyncResponse"/> or <paramref name="onEvent"/> is null.
        /// </exception>
        public ApplePlatformInvoker(
            Action<int, string> onAsyncResponse,
            Action<string, string> onEvent)
        {
            if (onAsyncResponse == null)
            {
                throw new ArgumentNullException(nameof(onAsyncResponse));
            }
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            m_onAsyncResponse = onAsyncResponse;
            m_onEvent = onEvent;

            // Sinks stored above; native register happens last so a fast reverse-P/Invoke
            // cannot land before there's a place to route it.
            PInvokeInterop.SetCallbacks(OnAsyncResponseFromNative, OnEventFromNative);
        }

        /// <inheritdoc/>
        public string CallSync(string pluginName, string methodName, string jsonPayload)
        {
            IntPtr ptr = PInvokeInterop.NativeCallSync(pluginName, methodName, jsonPayload);
            if (ptr == IntPtr.Zero)
            {
                return HiveBridgeErrorEnvelope.Wrap(
                    HiveErrorCode.Internal,
                    $"Native CallSync returned null (plugin={pluginName}, method={methodName})");
            }

            try
            {
                return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
            }
            finally
            {
                PInvokeInterop.NativeFreeString(ptr);
            }
        }

        /// <inheritdoc/>
        public void CallAsync(string pluginName, string methodName, string enrichedPayload)
        {
            PInvokeInterop.NativeCallAsync(pluginName, methodName, enrichedPayload);
        }

        /// <inheritdoc/>
        public void CancelAsync(string pluginName, int callbackId)
        {
            // No-op: the Swift AxylBridgeDispatcher exposes no cancel entry that routes to
            // the plugin's cancel hook, so cancellation does not reach the native side. The
            // managed task is still completed as cancelled by NativeBridge.
            _ = pluginName;
            _ = callbackId;
        }

        /// <summary>
        /// Calls the native shutdown hook and clears the reverse-P/Invoke delegate slots.
        /// Idempotent. Invoked by <see cref="NativeBridge.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) == 1)
            {
                return;
            }

            try
            {
                PInvokeInterop.NativeShutdown();
            }
            catch (Exception)
            {
                // Native surface may already be torn down during application
                // exit; swallow so cleanup continues.
            }

            PInvokeInterop.ClearCallbacks();

            // Release composer references so the cycle (composer ↔ invoker delegates)
            // can be collected.
            m_onAsyncResponse = null;
            m_onEvent = null;
        }

        private void OnAsyncResponseFromNative(int callbackId, string responseJson)
        {
            m_onAsyncResponse?.Invoke(callbackId, responseJson);
        }

        private void OnEventFromNative(string eventName, string jsonPayload)
        {
            m_onEvent?.Invoke(eventName, jsonPayload);
        }
    }
}

#endif
