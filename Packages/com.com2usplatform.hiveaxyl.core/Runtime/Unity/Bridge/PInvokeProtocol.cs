// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Apple native-bridge wire-contract constants shared between
    /// <see cref="ApplePlatformInvoker"/> and the <c>@_cdecl</c> receivers in
    /// the HiveAxylCore framework's <c>AxylNativeBridge.swift</c>.
    /// These names must match their Swift counterparts byte-for-byte —
    /// the Unity runtime resolves them via <c>DllImport("__Internal")</c>
    /// on iOS device builds and against <c>HiveAxylCore.bundle</c> on
    /// macOS standalone builds.
    /// </summary>
    internal static class PInvokeProtocol
    {
#if UNITY_IOS && !UNITY_EDITOR
        internal const string k_DllName = "__Internal";
#else
        internal const string k_DllName = "HiveAxylCore";
#endif

        internal const string k_SetCallbacks = "Hive_Axyl_Core_SetCallbacks";
        internal const string k_CallSync = "Hive_Axyl_Core_CallSync";
        internal const string k_CallAsync = "Hive_Axyl_Core_CallAsync";
        internal const string k_FreeString = "Hive_Axyl_Core_FreeString";
        internal const string k_Shutdown = "Hive_Axyl_Core_Shutdown";
    }
}

#endif
