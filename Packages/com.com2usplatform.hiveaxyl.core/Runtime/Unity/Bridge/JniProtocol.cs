// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_ANDROID && !UNITY_EDITOR

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Android JNI wire-contract constants shared between <see cref="AndroidPlatformInvoker"/> and
    /// <see cref="JniBridgeCallback"/>. These fully-qualified names must match the
    /// corresponding Kotlin declarations in the <c>com.com2usplatform.hiveaxyl.core</c>
    /// package of <c>hive-axyl-core.aar</c> byte-for-byte — <see cref="UnityEngine.AndroidJavaClass"/> and
    /// <see cref="UnityEngine.AndroidJavaProxy"/> resolve them via reflection.
    /// </summary>
    internal static class JniProtocol
    {
        internal const string k_BridgeClassName =
            "com.com2usplatform.hiveaxyl.core.AxylNativeBridge";

        internal const string k_CallbackInterfaceName =
            "com.com2usplatform.hiveaxyl.core.AxylNativeCallback";
    }
}

#endif
