// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_WEBGL && !UNITY_EDITOR

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// WebGL <see cref="IPlatformInvoker"/> that bridges C# to the JavaScript
    /// receiver via Unity's jslib mechanism (<c>DllImport("__Internal")</c>).
    /// Owns the engine-bound surface only — logging, callback correlation, and
    /// the event bus are Core-side concerns assembled by <see cref="NativeBridge"/>.
    /// Mirrors <see cref="ApplePlatformInvoker"/>: sync calls return inline;
    /// async calls are fire-and-forget and the JavaScript side delivers responses
    /// through the registered reverse callbacks via <c>dynCall_vii</c> into the
    /// <see cref="JslibInterop"/> trampolines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Threading: Unity WebGL runs on a single thread (the browser main thread).
    /// There are no background threads delivering async responses — the JavaScript
    /// <c>onComplete</c> callback fires within the JS event loop and is forwarded
    /// synchronously through the trampoline. <see cref="NativeBridge"/> still
    /// dispatches completions through <see cref="IDispatcher"/> for interface
    /// consistency.
    /// </para>
    /// <para>
    /// Lifecycle: the constructor registers the reverse-jslib trampolines via
    /// <see cref="JslibInterop.SetCallbacks"/> before storing the composer-side sinks,
    /// so a native-registration failure leaves no partial state on the instance.
    /// <see cref="Dispose"/> is idempotent; late async responses after shutdown are
    /// silently dropped by <see cref="NativeBridge"/>'s correlation manager.
    /// </para>
    /// <para>
    /// Exception semantics: any exception thrown by <see cref="CallSync"/> or
    /// <see cref="CallAsync"/> is caught by <see cref="NativeBridge"/> and returned as
    /// an <c>INTERNAL</c> envelope. Callers of <see cref="INativeBridge"/> never observe
    /// a raw jslib exception.
    /// </para>
    /// <para>
    /// A null pointer from <c>HiveAxyl_Jslib_CallSync</c> — the jslib ENOMEM branch of
    /// <c>_malloc</c> — is converted here into an <c>INTERNAL</c> envelope rather than
    /// propagated as a <see cref="NullReferenceException"/>.
    /// </para>
    /// </remarks>
    internal sealed class WebGLPlatformInvoker : IPlatformInvoker
    {
        private Action<int, string>? m_onAsyncResponse;
        private Action<string, string>? m_onEvent;
        private int m_disposed; // 0 = alive, 1 = disposed

        /// <summary>
        /// Registers the reverse-jslib trampolines via <see cref="JslibInterop.SetCallbacks"/>
        /// then stores the composer-side sinks. The static-slot semantics of
        /// <see cref="JslibInterop"/> imply a single <see cref="WebGLPlatformInvoker"/>
        /// per process — same constraint as <see cref="ApplePlatformInvoker"/>, lifted
        /// into the constructor.
        /// </summary>
        /// <param name="onAsyncResponse"><c>(callbackId, responseJson)</c> sink for async response.</param>
        /// <param name="onEvent"><c>(eventName, jsonPayload)</c> sink for one-way native events.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="onAsyncResponse"/> or <paramref name="onEvent"/> is null.
        /// </exception>
        public WebGLPlatformInvoker(
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

            JslibInterop.SetCallbacks(OnAsyncResponseFromNative, OnEventFromNative);

            m_onAsyncResponse = onAsyncResponse;
            m_onEvent = onEvent;
        }

        /// <inheritdoc/>
        public string CallSync(string pluginName, string methodName, string jsonPayload)
        {
            IntPtr ptr = JslibInterop.NativeCallSync(pluginName, methodName, jsonPayload);
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
                JslibInterop.NativeFreeString(ptr);
            }
        }

        /// <inheritdoc/>
        public void CallAsync(string pluginName, string methodName, string enrichedPayload)
        {
            JslibInterop.NativeCallAsync(pluginName, methodName, enrichedPayload);
        }

        /// <inheritdoc/>
        public void CancelAsync(string pluginName, int callbackId)
        {
            // Best-effort native interrupt; the JavaScript side routes it to the
            // plugin's cancel hook so an in-flight operation (e.g. a popup awaiting
            // an OAuth redirect) is released. The managed task is completed as
            // cancelled by NativeBridge regardless.
            JslibInterop.NativeCancel(pluginName, callbackId);
        }

        /// <summary>
        /// Calls the jslib shutdown hook and clears the reverse-jslib delegate slots.
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
                JslibInterop.NativeShutdown();
            }
            catch (Exception)
            {
                // JavaScript context may be torn down during page unload;
                // swallow so cleanup continues.
            }

            JslibInterop.ClearCallbacks();

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
