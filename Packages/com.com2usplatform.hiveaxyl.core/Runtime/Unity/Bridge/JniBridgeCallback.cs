// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_ANDROID && !UNITY_EDITOR

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// <see cref="AndroidJavaProxy"/> for the Kotlin-side
    /// <c>com.com2usplatform.hiveaxyl.core.AxylNativeCallback</c> interface. Forwards Kotlin
    /// invocations on a JNI worker thread to delegates supplied by <see cref="AndroidPlatformInvoker"/>.
    /// </summary>
    /// <remarks>
    /// Lowercase method names (<c>onAsyncResponse</c>, <c>onEvent</c>) must match the Java
    /// interface byte-for-byte because <see cref="AndroidJavaProxy"/> routes reverse-JNI
    /// invocations by Java method name via reflection.
    /// </remarks>
    [SuppressMessage("Style", "IDE1006:Naming Styles",
        Justification = "Method names must match Java interface AxylNativeCallback (JNI interop).")]
    internal sealed class JniBridgeCallback : AndroidJavaProxy, IDisposable
    {
        private volatile Action<int, string>? m_onAsyncResponse;
        private volatile Action<string, string>? m_onEvent;

        /// <summary>
        /// Binds the proxy to the Java <c>AxylNativeCallback</c> interface and stores the
        /// delegate sinks that <see cref="onAsyncResponse"/> and <see cref="onEvent"/> forward
        /// to. Sinks are held as clearable references so <see cref="Dispose"/> can detach them.
        /// </summary>
        public JniBridgeCallback(
            Action<int, string> onAsyncResponse,
            Action<string, string> onEvent)
            : base(JniProtocol.k_CallbackInterfaceName)
        {
            m_onAsyncResponse = onAsyncResponse;
            m_onEvent = onEvent;
        }

        /// <summary>Reverse-JNI entry for async call completion. No-op after <see cref="Dispose"/>.</summary>
        public void onAsyncResponse(int callbackId, string responseJson)
        {
            m_onAsyncResponse?.Invoke(callbackId, responseJson);
        }

        /// <summary>Reverse-JNI entry for one-way native events. No-op after <see cref="Dispose"/>.</summary>
        public void onEvent(string eventName, string jsonPayload)
        {
            m_onEvent?.Invoke(eventName, jsonPayload);
        }

        /// <summary>
        /// Clears delegate references. An in-flight invocation on the worker thread
        /// completes against a null delegate and becomes a no-op.
        /// </summary>
        public void Dispose()
        {
            m_onAsyncResponse = null;
            m_onEvent = null;
        }
    }
}

#endif
