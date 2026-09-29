// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_IOS || UNITY_STANDALONE_OSX

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Hive.Axyl.Push.Addon.APNS.Editor
{
    /// <summary>
    /// Enables push notifications on the generated Apple Xcode project and writes the APNs
    /// environment entitlement so APNs token registration succeeds on a code-signed build.
    /// Without the entitlement registration fails and the plugin surfaces
    /// <c>FAILED_PRECONDITION</c> / <c>ENTITLEMENT_MISSING</c>. On iOS it also guarantees
    /// <c>UserNotifications.framework</c> is linked on the UnityFramework target, which the
    /// statically compiled delegate template requires (see <see cref="LinkUserNotifications"/>).
    /// <para>
    /// The entitlement <b>key differs by platform</b> (confirmed against Apple's entitlement
    /// docs): iOS uses <c>aps-environment</c>, macOS uses <c>com.apple.developer.aps-environment</c>.
    /// The value follows the build configuration: a Development Build maps to
    /// <c>development</c> (APNs sandbox), a release build to <c>production</c>.
    /// <c>GetProviderEnvironmentAsync</c> reads this back at runtime to pick
    /// <c>APNS_SANDBOX</c> vs <c>APNS</c>.
    /// </para>
    /// <para>
    /// iOS uses Unity's <c>ProjectCapabilityManager.AddPushNotifications</c> (which writes the
    /// iOS key and toggles the capability). macOS writes the macOS key directly into the shared
    /// <c>HiveAxyl.entitlements</c> file and wires <c>CODE_SIGN_ENTITLEMENTS</c> on the generated
    /// main target, because that manager writes only the iOS key. Both compose with the Storage
    /// keychain and Apple Sign In passes that share the same entitlements file.
    /// </para>
    /// <para>
    /// Background Modes are intentionally NOT added: silent push is unsupported, so
    /// <c>UIBackgroundModes</c>'s <c>remote-notification</c> is unnecessary.
    /// </para>
    /// </summary>
    internal static class HiveAxylApnsCapabilityPostprocessor
    {
        // Runs after HiveAxylXCFrameworkPostprocessor (order 100) and the
        // Storage keychain pass (order 200). ProjectCapabilityManager and the
        // macOS PlistDocument path both re-read from disk, so trailing the
        // other writers lets their changes land first and compose cleanly.
        private const int k_CallbackOrder = 210;
        private const string k_MainTargetName = "Unity-iPhone";
        // Shared with the Storage keychain and Apple Sign In postprocessors so a build that
        // pulls in several addons keeps a single entitlements file that each pass appends to.
        private const string k_EntitlementsFileName = "HiveAxyl.entitlements";
        private const string k_IosApsEnvironmentKey = "aps-environment";
        private const string k_MacApsEnvironmentKey = "com.apple.developer.aps-environment";

        [PostProcessBuild(k_CallbackOrder)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            // Development Build => sandbox (development); release => production.
            string desired = EditorUserBuildSettings.development ? "development" : "production";

            if (target == BuildTarget.iOS)
            {
                ApplyIos(pathToBuiltProject, desired);
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                ApplyMacOs(pathToBuiltProject, desired);
            }
        }

        private static void ApplyIos(string pathToBuiltProject, string desired)
        {
            string pbxprojPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!File.Exists(pbxprojPath))
            {
                Debug.LogWarning("[HiveAxyl.Push.APNS.iOS.Postprocess] project.pbxproj missing — skipped");
                return;
            }

            LinkUserNotifications(pbxprojPath);

            string entitlementsRelative = Path.Combine(k_MainTargetName, k_EntitlementsFileName);
            string entitlementsAbsolute = Path.Combine(pathToBuiltProject, entitlementsRelative);

            // The aps-environment entitlement is single-valued, so a write is an overwrite and
            // re-running is safe. Skip only when the existing value already matches this build's
            // configuration — an "Append to existing project" rebuild, or a release build
            // re-exported over an earlier development one, must overwrite the stale value.
            bool development = desired == "development";
            if (File.Exists(entitlementsAbsolute))
            {
                var existingPlist = new PlistDocument();
                existingPlist.ReadFromFile(entitlementsAbsolute);
                if (existingPlist.root.values.TryGetValue(k_IosApsEnvironmentKey, out PlistElement existing)
                    && existing is PlistElementString existingValue
                    && existingValue.value == desired)
                {
                    Debug.Log("[HiveAxyl.Push.APNS.iOS.Postprocess] aps-environment already '"
                        + desired + "' — skipped");
                    return;
                }
            }

            var capabilityManager = new ProjectCapabilityManager(
                pbxprojPath,
                entitlementsRelative,
                k_MainTargetName);

            capabilityManager.AddPushNotifications(development);
            capabilityManager.WriteToFile();

            Debug.Log("[HiveAxyl.Push.APNS.iOS.Postprocess] added Push Notifications capability + "
                + "aps-environment=" + desired + " at " + entitlementsRelative);
        }

        // The integration guide's standard delegate template
        // (ApnsRemoteNotificationForwarder.mm) compiles statically into UnityFramework and
        // references UNUserNotificationCenter. Unity only links UserNotifications when the
        // copied file's importer happens to declare it, so the addon guarantees the link
        // here — an integration that misses the importer setting otherwise fails at the
        // archive's link step with an unresolved class symbol. Runs before the entitlement
        // pass's early return so an append rebuild keeps the link too; harmlessly idempotent
        // when the framework is already present.
        //
        // iOS-only on purpose: on macOS the forwarder ships as a prebuilt bundle that links
        // AppKit and UserNotifications itself, so ApplyMacOs needs no link pass — its job is
        // the entitlement alone.
        private static void LinkUserNotifications(string pbxprojPath)
        {
            var project = new PBXProject();
            project.ReadFromFile(pbxprojPath);
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();
            if (string.IsNullOrEmpty(frameworkTarget))
            {
                Debug.LogWarning("[HiveAxyl.Push.APNS.iOS.Postprocess] UnityFramework target missing — "
                    + "UserNotifications link skipped");
                return;
            }

            if (!project.ContainsFramework(frameworkTarget, "UserNotifications.framework"))
            {
                project.AddFrameworkToProject(frameworkTarget, "UserNotifications.framework", false);
                project.WriteToFile(pbxprojPath);
                Debug.Log("[HiveAxyl.Push.APNS.iOS.Postprocess] linked UserNotifications.framework "
                    + "to UnityFramework");
            }
        }

        private static void ApplyMacOs(string pathToBuiltProject, string desired)
        {
            string mainTargetGuid = TryResolveMacOsMainTargetGuid(pathToBuiltProject, out string pbxprojPath);
            if (string.IsNullOrEmpty(mainTargetGuid))
            {
                return;
            }

            // Entitlements live at the project root on macOS (the target's source-folder name is
            // unstable). Write the macOS key into the shared file, preserving whatever the
            // keychain / Apple Sign In passes already wrote, then wire it onto the main target.
            // macOS regenerates the pbxproj on every build, so always re-wire (no file-based skip).
            string entitlementsAbsolute = Path.Combine(pathToBuiltProject, k_EntitlementsFileName);

            var plist = new PlistDocument();
            if (File.Exists(entitlementsAbsolute))
            {
                plist.ReadFromFile(entitlementsAbsolute);
            }

            plist.root.SetString(k_MacApsEnvironmentKey, desired);
            plist.WriteToFile(entitlementsAbsolute);

            var project = new PBXProject();
            project.ReadFromFile(pbxprojPath);
            project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", k_EntitlementsFileName);
            project.WriteToFile(pbxprojPath);

            Debug.Log("[HiveAxyl.Push.APNS.macOS.Postprocess] wrote " + k_MacApsEnvironmentKey + "="
                + desired + " at " + k_EntitlementsFileName);
        }

        // Resolves the macOS main target GUID, or null (logging why) if no Xcode project or
        // main target is found so the caller skips. An IL2CPP macOS build emits two projects
        // (the app project and Unity's "GameAssembly.xcodeproj", the IL2CPP static library) in
        // an unspecified order, so resolve against each and take the first with a main target —
        // the GameAssembly project has none. GetUnityMainTargetGuid() returns "" on some macOS
        // Unity revisions, so fall back to the target named after PlayerSettings.productName.
        private static string TryResolveMacOsMainTargetGuid(
            string pathToBuiltProject, out string pbxprojPath)
        {
            pbxprojPath = null;

            string[] projects = Directory.GetDirectories(pathToBuiltProject, "*.xcodeproj");
            if (projects.Length == 0)
            {
                Debug.LogWarning("[HiveAxyl.Push.APNS.macOS.Postprocess] .xcodeproj missing — skipped");
                return null;
            }

            foreach (var projectDir in projects)
            {
                var candidatePath = Path.Combine(projectDir, "project.pbxproj");
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                var project = new PBXProject();
                project.ReadFromFile(candidatePath);
                string guid = project.GetUnityMainTargetGuid();
                if (string.IsNullOrEmpty(guid))
                {
                    guid = project.TargetGuidByName(PlayerSettings.productName);
                }

                if (!string.IsNullOrEmpty(guid))
                {
                    pbxprojPath = candidatePath;
                    return guid;
                }
            }

            Debug.LogWarning("[HiveAxyl.Push.APNS.macOS.Postprocess] macOS main target not found — skipped");
            return null;
        }
    }
}

#endif
