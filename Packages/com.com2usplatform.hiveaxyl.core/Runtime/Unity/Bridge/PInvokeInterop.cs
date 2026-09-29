// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR

#nullable enable

using System;
using System.Runtime.InteropServices;
using AOT;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// P/Invoke declarations and IL2CPP-safe callback trampolines for the
    /// Apple native bridge. The <see cref="MonoPInvokeCallbackAttribute"/>
    /// static trampolines forward reverse-P/Invoke invocations to delegate
    /// slots held here — a single <see cref="ApplePlatformInvoker"/> instance per
    /// process is assumed, mirroring the Android <c>AndroidPlatformInvoker</c> design.
    /// </summary>
    internal static class PInvokeInterop
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AsyncResponseDelegate(int callbackId, IntPtr utf8Json);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void NativeEventDelegate(IntPtr utf8EventName, IntPtr utf8Payload);

        // `volatile` for cross-thread visibility against Dispose: the
        // trampolines read these fields from whichever thread the native
        // side invokes on. GC anchoring: the delegate instances passed to
        // NativeSetCallbacks must also be kept alive; see s_asyncDelegate.
        private static volatile Action<int, string>? s_onAsyncResponse;
        private static volatile Action<string, string>? s_onEvent;

        // Strong references to the trampoline delegates so the GC does not
        // collect them while the native side holds raw function pointers.
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
                // IL2CPP reverse-P/Invoke trampolines must never throw into
                // native — a managed exception here would terminate the
                // process. Swallow and drop the response; the correlation
                // manager's shutdown path will eventually cancel the TCS.
            }
        }

        [MonoPInvokeCallback(typeof(NativeEventDelegate))]
        private static void NativeEventTrampoline(IntPtr utf8EventName, IntPtr utf8Payload)
        {
            try
            {
                string eventName = Marshal.PtrToStringUTF8(utf8EventName) ?? string.Empty;
                string payload = Marshal.PtrToStringUTF8(utf8Payload) ?? string.Empty;
                s_onEvent?.Invoke(eventName, payload);
            }
            catch
            {
                // See AsyncResponseTrampoline.
            }
        }

        // ----------------------------------------------------------------
        //  Native P/Invoke declarations (symbol names in PInvokeProtocol)
        // ----------------------------------------------------------------

        [DllImport(PInvokeProtocol.k_DllName, EntryPoint = PInvokeProtocol.k_SetCallbacks)]
        private static extern void NativeSetCallbacks(
            AsyncResponseDelegate? asyncResponse,
            NativeEventDelegate? onEvent);

        [DllImport(PInvokeProtocol.k_DllName, EntryPoint = PInvokeProtocol.k_CallSync)]
        internal static extern IntPtr NativeCallSync(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pluginName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string methodName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string payloadJson);

        [DllImport(PInvokeProtocol.k_DllName, EntryPoint = PInvokeProtocol.k_CallAsync)]
        internal static extern void NativeCallAsync(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string pluginName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string methodName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string enrichedPayloadJson);

        [DllImport(PInvokeProtocol.k_DllName, EntryPoint = PInvokeProtocol.k_FreeString)]
        internal static extern void NativeFreeString(IntPtr ptr);

        [DllImport(PInvokeProtocol.k_DllName, EntryPoint = PInvokeProtocol.k_Shutdown)]
        internal static extern void NativeShutdown();
    }
}

#endif
