// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_WEBGL && !UNITY_EDITOR

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// WebGL jslib bridge wire-contract constants shared between
    /// <see cref="WebGLPlatformInvoker"/> and <c>HiveAxylCore.jslib</c>.
    /// These names must match their JavaScript counterparts byte-for-byte —
    /// the Unity WebGL IL2CPP runtime resolves them via
    /// <c>DllImport("__Internal")</c> against the merged jslib library.
    /// </summary>
    internal static class JslibProtocol
    {
        internal const string k_DllName = "__Internal";

        internal const string k_SetCallbacks = "HiveAxyl_Jslib_SetCallbacks";
        internal const string k_CallSync     = "HiveAxyl_Jslib_CallSync";
        internal const string k_CallAsync    = "HiveAxyl_Jslib_CallAsync";
        internal const string k_Cancel       = "HiveAxyl_Jslib_Cancel";
        internal const string k_FreeString   = "HiveAxyl_Jslib_FreeString";
        internal const string k_Shutdown     = "HiveAxyl_Jslib_Shutdown";
    }
}

#endif
