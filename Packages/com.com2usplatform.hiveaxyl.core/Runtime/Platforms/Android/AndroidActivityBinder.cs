// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_ANDROID && !UNITY_EDITOR

using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Unity-side adapter that hands the current <c>UnityPlayer</c> Activity
    /// to the engine-neutral <c>AxylAndroidActivity</c> holder in the Core
    /// Android AAR. Concentrating the <c>UnityPlayer</c> reflection in this
    /// one file keeps the native AAR free of Unity symbols — Unreal and pure
    /// native Android hosts plug in their own equivalent binders.
    /// </summary>
    /// <remarks>
    /// Invoked exactly once from <see cref="HiveBootstrap.Initialize"/> on
    /// Android. Subsequent Activity transitions (rotation, task-root swap,
    /// new Activity pushed on the stack) are tracked by the Java-side
    /// <see href="https://developer.android.com/reference/android/app/Application.ActivityLifecycleCallbacks"/>
    /// the holder registers internally, so this binder does not need to
    /// re-bind on every call.
    /// </remarks>
    internal static class AndroidActivityBinder
    {
        private const string k_UnityPlayerClass = "com.unity3d.player.UnityPlayer";
        private const string k_CurrentActivityField = "currentActivity";

        private const string k_HolderClass = "com.com2usplatform.hiveaxyl.core.AxylAndroidActivity";
        private const string k_BindMethod = "bind";

        /// <summary>
        /// Reads <c>UnityPlayer.currentActivity</c> and forwards it to the
        /// Core Android holder. No-op when Unity's runtime is unavailable
        /// (e.g., Editor on a non-Android target) — the bootstrap path is
        /// already gated on <c>UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c>.
        /// </summary>
        internal static void Bind()
        {
            using var unityPlayer = new AndroidJavaClass(k_UnityPlayerClass);
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>(k_CurrentActivityField);
            if (activity == null || activity.GetRawObject() == System.IntPtr.Zero)
            {
                // Activity not attached yet — caller is expected to invoke
                // Bind() from the main-thread Initialize path, so a null
                // here indicates a host configuration error rather than a
                // race. Surface as a no-op; the first CredentialManager
                // call will fail with FAILED_PRECONDITION and the diagnostic
                // message will point the integrator back here.
                return;
            }

            using var holder = new AndroidJavaClass(k_HolderClass);
            holder.CallStatic(k_BindMethod, activity);
        }
    }
}

#endif
