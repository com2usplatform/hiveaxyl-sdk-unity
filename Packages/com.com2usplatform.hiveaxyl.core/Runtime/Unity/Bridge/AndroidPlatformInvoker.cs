// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_ANDROID && !UNITY_EDITOR

#nullable enable

using System;
using System.Threading;
using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Android JNI-based <see cref="IPlatformInvoker"/>. Delegates to the Kotlin-side
    /// <c>com.com2usplatform.hiveaxyl.core.AxylNativeBridge</c> singleton via
    /// <see cref="AndroidJavaClass"/> and receives async responses and native events
    /// through a <see cref="JniBridgeCallback"/> <see cref="AndroidJavaProxy"/>.
    /// Owns the engine-bound surface only — Core-side concerns (logging, callback
    /// correlation, event dispatch) live in <see cref="NativeBridge"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Threading: <see cref="CallSync"/> runs on the caller thread; <see cref="CallAsync"/>
    /// returns immediately after issuing the JNI call. Reverse-JNI invocations arrive on a
    /// JNI worker thread; this invoker forwards them directly to the constructor-supplied
    /// composer delegates, and the composer is responsible for routing them to the
    /// main thread.
    /// </para>
    /// <para>
    /// Lifecycle: must be constructed on the main thread so that JNI attaches on the correct
    /// thread for subsequent <see cref="AndroidJavaObject"/> operations. The constructor stores
    /// the composer-side sinks <i>before</i> registering the Kotlin-side callback, so a fast
    /// reverse-JNI invocation cannot land on a null sink.
    /// <see cref="Dispose"/> is idempotent; late native responses after shutdown are
    /// silently dropped by <see cref="NativeBridge"/>'s correlation manager.
    /// </para>
    /// <para>
    /// Exception semantics: any exception thrown by <see cref="CallSync"/> or
    /// <see cref="CallAsync"/> (including <c>AndroidJavaException</c> from malformed JNI
    /// calls) is caught by <see cref="NativeBridge"/> and returned as an <c>INTERNAL</c>
    /// error envelope — callers of <see cref="INativeBridge"/> never observe a raw JNI
    /// exception. The constructor is the sole exception: AAR misconfiguration surfaces as
    /// <c>AndroidJavaException</c> during bootstrap by design.
    /// </para>
    /// </remarks>
    internal sealed class AndroidPlatformInvoker : IPlatformInvoker
    {
        private AndroidJavaClass? m_bridgeClass;
        private JniBridgeCallback? m_callback;
        private Action<int, string>? m_onAsyncResponse;
        private Action<string, string>? m_onEvent;
        private int m_disposed; // 0 = alive, 1 = disposed

        /// <summary>
        /// Must be invoked on the main thread so that JNI attaches on the correct thread
        /// for subsequent <see cref="AndroidJavaObject"/> operations. Stores the composer
        /// sinks <i>before</i> registering the Kotlin-side callback so a fast reverse-JNI
        /// invocation cannot land on a null sink.
        /// </summary>
        /// <param name="onAsyncResponse"><c>(callbackId, responseJson)</c> sink for async response.</param>
        /// <param name="onEvent"><c>(eventName, jsonPayload)</c> sink for one-way native events.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="onAsyncResponse"/> or <paramref name="onEvent"/> is null.
        /// </exception>
        /// <exception cref="AndroidJavaException">
        /// Thrown when <see cref="JniProtocol.k_BridgeClassName"/> cannot be resolved —
        /// typically a misconfigured AAR in the Android build.
        /// </exception>
        public AndroidPlatformInvoker(
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

            // Sinks stored above; only now do we touch the JNI surface so a fast
            // reverse-JNI invocation cannot land before there's a place to route it.
            m_bridgeClass = new AndroidJavaClass(JniProtocol.k_BridgeClassName);
            m_callback = new JniBridgeCallback(OnAsyncResponseFromNative, OnEventFromNative);
            m_bridgeClass.CallStatic("setCallback", m_callback);
        }

        /// <inheritdoc/>
        public string CallSync(string pluginName, string methodName, string jsonPayload)
        {
            var cls = m_bridgeClass
                ?? throw new ObjectDisposedException(nameof(AndroidPlatformInvoker));
            return cls.CallStatic<string>("callSync", pluginName, methodName, jsonPayload);
        }

        /// <inheritdoc/>
        public void CallAsync(string pluginName, string methodName, string enrichedPayload)
        {
            var cls = m_bridgeClass
                ?? throw new ObjectDisposedException(nameof(AndroidPlatformInvoker));
            cls.CallStatic("callAsync", pluginName, methodName, enrichedPayload);
        }

        /// <inheritdoc/>
        public void CancelAsync(string pluginName, int callbackId)
        {
            // No-op until the Kotlin AxylNativeBridge exposes a cancel entry that routes to
            // the plugin's cancel hook (lands with the Android backend that needs it). The
            // managed task is still completed as cancelled by NativeBridge.
            _ = pluginName;
            _ = callbackId;
        }

        /// <summary>
        /// Calls the Kotlin-side <c>shutdown()</c> and releases the Java class handle and
        /// reverse-JNI proxy. Idempotent. Invoked by <see cref="NativeBridge.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref m_disposed, 1) == 1)
            {
                return;
            }

            try
            {
                m_bridgeClass?.CallStatic("shutdown");
            }
            catch (Exception)
            {
                // JNI surface may already be torn down during application exit; swallow
                // to let cleanup continue.
            }

            m_bridgeClass?.Dispose();
            m_bridgeClass = null;
            m_callback?.Dispose();
            m_callback = null;

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
