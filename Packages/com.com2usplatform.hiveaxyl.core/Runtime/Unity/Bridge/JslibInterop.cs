// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_WEBGL && !UNITY_EDITOR

#nullable enable

using System;
using System.Runtime.InteropServices;
using AOT;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// DllImport declarations and IL2CPP-safe callback trampolines for the
    /// WebGL jslib bridge. The <see cref="MonoPInvokeCallbackAttribute"/>
    /// static trampolines forward reverse-jslib invocations (via
    /// <c>dynCall_vii</c>) to delegate slots held here — a single
    /// <see cref="WebGLPlatformInvoker"/> instance per page is assumed,
    /// mirroring the <c>PInvokeInterop</c> design.
    /// </summary>
    internal static class JslibInterop
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AsyncResponseDelegate(int callbackId, IntPtr utf8Json);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void NativeEventDelegate(IntPtr utf8EventName, IntPtr utf8Payload);

        // Accessible from static trampolines: [MonoPInvokeCallback] requires static
        // methods, so the composer sinks cannot be stored on an instance.
        private static Action<int, string>? s_onAsyncResponse;
        private static Action<string, string>? s_onEvent;

        private static AsyncResponseDelegate? s_asyncDelegate;
        private static NativeEventDelegate? s_eventDelegate;

        internal static void SetCallbacks(
            Action<int, string> onAsyncResponse,
            Action<string, string> onEvent)
        {
            s_onAsyncResponse = onAsyncResponse;
            s_onEvent = onEvent;
            s_asyncDelegate = AsyncResponseTrampoline;
            s_eventDelegate = NativeEventTrampoline;
            NativeSetCallbacks(s_asyncDelegate, s_eventDelegate);
        }

        internal static void ClearCallbacks()
        {
            NativeSetCallbacks(null, null);
            s_onAsyncResponse = null;
            s_onEvent = null;
            s_asyncDelegate = null;
            s_eventDelegate = null;
        }

        [MonoPInvokeCallback(typeof(AsyncResponseDelegate))]
        private static void AsyncResponseTrampoline(int callbackId, IntPtr utf8Json)
        {
            try
            {
                string responseJson = Marshal.PtrToStringUTF8(utf8Json) ?? string.Empty;
                s_onAsyncResponse?.Invoke(callbackId, responseJson);
            }
            catch
            {
                // IL2CPP reverse-jslib trampolines must never throw back into
                // JavaScript — a managed exception escaping here terminates the
                // page. Swallow; the correlation manager's shutdown path will
                // eventually cancel the pending TCS.
            }
        }

        [MonoPInvokeCallback(typeof(NativeEventDelegate))]
        private static void NativeEventTrampoline(IntPtr utf8EventName, IntPtr utf8Payload)
        {
            try
            {
                string eventName = Marshal.PtrToStringUTF8(utf8EventName) ?? string.Empty;
                string payload   = Marshal.PtrToStringUTF8(utf8Payload)   ?? string.Empty;
                s_onEvent?.Invoke(eventName, payload);
            }
            catch
            {
                // See AsyncResponseTrampoline.
            }
        }

        // ----------------------------------------------------------------
        //  Native DllImport declarations (entry point names in JslibProtocol)
        // ----------------------------------------------------------------

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_SetCallbacks)]
        private static extern void NativeSetCallbacks(
            AsyncResponseDelegate? asyncResponse,
            NativeEventDelegate?   onEvent);

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_CallSync)]
        internal static extern IntPtr NativeCallSync(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pluginName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string methodName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string payloadJson);

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_CallAsync)]
        internal static extern void NativeCallAsync(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pluginName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string methodName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string enrichedPayloadJson);

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_Cancel)]
        internal static extern void NativeCancel(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pluginName,
            int callbackId);

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_FreeString)]
        internal static extern void NativeFreeString(IntPtr ptr);

        [DllImport(JslibProtocol.k_DllName, EntryPoint = JslibProtocol.k_Shutdown)]
        internal static extern void NativeShutdown();
    }
}

#endif
