// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_ANDROID

using System.IO;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Hive.Axyl.Push.Addon.FCM.Editor
{
    /// <summary>
    /// Applies Google's <c>com.google.gms:google-services</c> Gradle plugin to the generated
    /// Android project, so Firebase finds its project configuration.
    /// <para>
    /// The addon ships firebase-messaging, but Firebase reads its project identity from string
    /// resources (<c>google_app_id</c>, <c>gcm_defaultSenderId</c>, <c>google_api_key</c>,
    /// <c>project_id</c>) that this first-party plugin generates from <c>google-services.json</c>.
    /// Without it <c>FirebaseInitProvider</c> logs "Default FirebaseApp failed to initialize
    /// because no default options were found" and every call fails with
    /// <c>FailedPrecondition</c> — the addon never initializes Firebase itself. Generating those
    /// resources by hand is rejected: mimicking AGP's transform breaks whenever Google
    /// adds a key, and the plugin additionally verifies the config's <c>package_name</c> against
    /// the application id, turning a mismatched config into a build error instead of a runtime one.
    /// </para>
    /// <para>
    /// The <b>generated</b> project is patched rather than the app's <c>*Template.gradle</c> files,
    /// even though rewriting the templates is the other available approach: those templates are app-owned
    /// and routinely customized — a game may keep its own <c>launcherTemplate.gradle</c> in version
    /// control (for a WebAuth redirect scheme, say) — so rewriting them would collide with the
    /// game's edits. Patching the generated copy leaves the game's sources untouched.
    /// </para>
    /// <para>
    /// The plugin goes on the <b>launcher</b> (application) module: it emits resources into the
    /// module it is applied to and matches the config against that module's application id, both of
    /// which belong to the app rather than to <c>unityLibrary</c>. No repository declaration is
    /// needed — Unity's generated <c>settings.gradle</c> already resolves plugins through
    /// <c>google()</c>.
    /// </para>
    /// <para>
    /// The app supplies the config at <c>Assets/Plugins/Android/google-services.json</c>
    /// (downloaded from the Firebase console for an Android app whose package name equals the
    /// build's application id). With no such file this pass is a no-op, because applying the plugin
    /// without a config fails the Gradle build — which would break every project that does not use
    /// FCM.
    /// </para>
    /// <para>
    /// Once that file <i>is</i> present the game has asked for FCM, and every remaining failure
    /// stops the build with a <see cref="BuildFailedException"/> rather than a warning. Carrying on
    /// would ship an APK whose Firebase resources are missing, which surfaces only as a
    /// <c>FailedPrecondition</c> on device — the runtime-error-for-build-error trade this pass
    /// exists to avoid.
    /// </para>
    /// </summary>
    internal sealed class HiveAxylFcmGoogleServicesPostprocessor : IPostGenerateGradleAndroidProject
    {
        private const string k_ConfigFileName = "google-services.json";
        private const string k_AssetsFolder = "Assets/";
        private const string k_ConfigAssetPath = k_AssetsFolder + "Plugins/Android/" + k_ConfigFileName;
        private const string k_PluginId = "com.google.gms.google-services";

        // Pinned rather than tracking latest: the plugin's output is the resource set Firebase
        // reads at startup, so an unattended upgrade could change it under a shipped build.
        private const string k_PluginVersion = "4.4.2";

        private const string k_LauncherModule = "launcher";
        private const string k_SettingsFile = "settings.gradle";
        private const string k_BuildFile = "build.gradle";
        private const string k_Tag = "[HiveAxyl.Push.Addon.FCM]";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Application.dataPath IS the Assets folder, so the file to read is the asset path
            // above with its "Assets/" prefix dropped. Derived rather than re-spelled: a const
            // the log quotes and a separate Path.Combine of the same segments drift apart the
            // moment either one is edited, and then the log names a path nothing looks at.
            var config = Path.Combine(
                Application.dataPath,
                k_ConfigAssetPath.Substring(k_AssetsFolder.Length)
                    .Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(config))
            {
                // Warned rather than silent, and warned rather than logged: this assembly only
                // exists in a project that installed the FCM add-on, so a missing config is an
                // incomplete setup rather than a normal path. Left at Info it would be lost in
                // the build log and resurface as a FailedPrecondition on the device.
                Debug.LogWarning($"{k_Tag} No {k_ConfigAssetPath} — skipping the Google Services Gradle "
                    + "plugin. FCM calls fail with FailedPrecondition until that file is added.");
                return;
            }

            var root = GradleTextPatch.ResolveGradleRoot(
                path,
                candidate => File.Exists(Path.Combine(candidate, k_SettingsFile)),
                candidate => Directory.GetParent(candidate)?.FullName);

            var launcher = root == null ? null : Path.Combine(root, k_LauncherModule);
            if (launcher == null || !File.Exists(Path.Combine(launcher, k_BuildFile)))
            {
                throw new BuildFailedException(
                    $"{k_Tag} No {k_LauncherModule}/{k_BuildFile} under '{path}', so the Google "
                    + $"Services plugin cannot be applied. {k_ConfigAssetPath} is present, so this "
                    + "build wants FCM and would ship without Firebase configuration.");
            }

            var rootBuildFile = Path.Combine(root, k_BuildFile);
            if (!File.Exists(rootBuildFile))
            {
                // Checked rather than left to ReadAllText, so the failure carries this pass's
                // tag and its reason instead of a bare FileNotFoundException.
                throw new BuildFailedException(
                    $"{k_Tag} No {k_BuildFile} at '{root}', so the Google Services plugin cannot "
                    + $"be declared. {k_ConfigAssetPath} is present, so this build wants FCM and "
                    + "would ship without Firebase configuration.");
            }

            File.Copy(config, Path.Combine(launcher, k_ConfigFileName), overwrite: true);

            // Both edits are required: applying a plugin that was never declared fails the
            // consumer's Gradle build with an error that names Gradle rather than this pass. The
            // declaration therefore runs first and throws on failure, so the application below
            // never runs alone and the success line never prints over a failure.
            PatchOrThrow(rootBuildFile, DeclareIn);
            PatchOrThrow(Path.Combine(launcher, k_BuildFile), ApplyIn);

            Debug.Log($"{k_Tag} Applied {k_PluginId}:{k_PluginVersion} to the {k_LauncherModule} "
                + $"module with {k_ConfigAssetPath}.");
        }

        private delegate GradlePatchResult Patch(string text, out string patched);

        private static GradlePatchResult DeclareIn(string text, out string patched)
            => GradleTextPatch.DeclarePlugin(text, k_PluginId, k_PluginVersion, out patched);

        private static GradlePatchResult ApplyIn(string text, out string patched)
            => GradleTextPatch.ApplyPlugin(text, k_PluginId, out patched);

        /// <summary>
        /// Runs one patch against <paramref name="buildFile"/>, writing only when the text
        /// changed. A missing anchor means the generated Gradle file is not the shape this pass
        /// knows — most likely a Unity version whose templates moved — so it stops the build
        /// instead of leaving a half-configured project behind.
        /// </summary>
        private static void PatchOrThrow(string buildFile, Patch patch)
        {
            switch (patch(File.ReadAllText(buildFile), out var patched))
            {
                case GradlePatchResult.AlreadyPresent:
                    return;

                case GradlePatchResult.Inserted:
                    File.WriteAllText(buildFile, patched);
                    return;

                default:
                    throw new BuildFailedException(
                        $"{k_Tag} No anchor to patch in {buildFile}, so the Google Services "
                        + "plugin was not applied. The generated Gradle project is not the shape "
                        + "this pass expects; the Unity version may have changed its templates.");
            }
        }
    }
}

#endif
