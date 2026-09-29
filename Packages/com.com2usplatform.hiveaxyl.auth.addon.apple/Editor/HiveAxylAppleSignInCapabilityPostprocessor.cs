// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_IOS || UNITY_STANDALONE_OSX

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Hive.Axyl.Auth.Addon.Apple.Editor
{
    /// <summary>
    /// Enables the "Sign in with Apple" capability on the generated Xcode
    /// project so the addon's <c>ASAuthorizationController</c> flow is entitled
    /// on a code-signed build. Without the
    /// <c>com.apple.developer.applesignin</c> entitlement the system rejects
    /// the request at runtime (<c>ASAuthorizationError</c> 1000 / unknown).
    /// <para>
    /// Games that install <c>com.com2usplatform.hiveaxyl.auth.addon.apple</c> get the
    /// entitlement automatically on iOS builds and on macOS builds that create an
    /// Xcode project (a direct macOS .app build has no project to edit and is
    /// skipped), matching the build-time automation the Storage package's
    /// keychain pass provides. App ID configuration and
    /// provisioning-profile (re)issue in the Apple Developer Portal remain the
    /// game's manual step.
    /// </para>
    /// <para>
    /// Writes the shared <c>HiveAxyl.entitlements</c> file.
    /// <c>ProjectCapabilityManager</c> loads any existing entitlements on
    /// construction and appends, so this composes with the Storage keychain
    /// and APNs passes regardless of order; on macOS the file sits at the
    /// project root and is attached to the single main target (resolved by GUID).
    /// </para>
    /// </summary>
    internal static class HiveAxylAppleSignInCapabilityPostprocessor
    {
        // After HiveAxylXCFrameworkPostprocessor (100), the Storage keychain
        // entitlement pass (200) and the APNs push entitlement pass (210).
        // Ordering is not functionally critical (the manager
        // re-reads from disk and appends), but keeps the SDK's entitlement
        // writers deterministic.
        private const int k_CallbackOrder = 220;
        private const string k_EntitlementsFileName = "HiveAxyl.entitlements";
        // The applesignin entitlement key, used as the idempotent guard so an
        // "Append to existing project" rebuild does not write a duplicate entry.
        private const string k_EntitlementKey = "com.apple.developer.applesignin";

        [PostProcessBuild(k_CallbackOrder)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            string pbxprojPath;
            string mainTargetName;
            string mainTargetGuid = null;
            string entitlementsRelative;

            if (target == BuildTarget.iOS)
            {
                pbxprojPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
                mainTargetName = "Unity-iPhone";
                // Entitlement path is interpreted relative to the Xcode project root;
                // park it inside the target's source folder next to Info.plist,
                // matching the layout the keychain pass uses.
                entitlementsRelative = Path.Combine(mainTargetName, k_EntitlementsFileName);
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                mainTargetGuid = TryResolveMacOsMainTargetGuid(pathToBuiltProject, out pbxprojPath);
                if (string.IsNullOrEmpty(mainTargetGuid))
                {
                    return;
                }

                // Resolve by GUID only — ProjectCapabilityManager rejects being handed
                // both a name and a GUID ("specify only one"). The target's source-
                // folder name tracks the same unstable target name, so park the
                // entitlements file at the project root instead.
                mainTargetName = null;
                entitlementsRelative = k_EntitlementsFileName;
            }
            else
            {
                return;
            }

            if (!File.Exists(pbxprojPath))
            {
                Debug.LogWarning("[HiveAxyl.Auth.Apple.Postprocess] project.pbxproj missing — skipped");
                return;
            }

            string entitlementsAbsolute = Path.Combine(pathToBuiltProject, entitlementsRelative);

            // Guard against iOS "Append to existing project" rebuilds: they reuse the
            // pbxproj, and AddSignInWithApple is not idempotent, so a second run would
            // duplicate the entitlement entry and provisioning validators may reject the
            // signature. This is keyed off the persisted entitlements file only for iOS.
            // macOS regenerates the pbxproj on every build (a fresh CODE_SIGN_ENTITLEMENTS
            // wiring each time) while the entitlements file at the project root persists,
            // so gating macOS on that file would skip the wiring and leave the fresh
            // pbxproj without the capability — the file present but not attached.
            if (target == BuildTarget.iOS
                && File.Exists(entitlementsAbsolute)
                && File.ReadAllText(entitlementsAbsolute).Contains(k_EntitlementKey))
            {
                Debug.Log("[HiveAxyl.Auth.Apple.Postprocess] Sign in with Apple entitlement already present — skipped");
                return;
            }

            // Pass the resolved GUID (non-null on macOS) so the capability is added to
            // the exact main target; iOS keeps resolving by the stable name.
            var capabilityManager = new ProjectCapabilityManager(
                pbxprojPath,
                entitlementsRelative,
                mainTargetName,
                mainTargetGuid);

            capabilityManager.AddSignInWithApple();
            capabilityManager.WriteToFile();

            Debug.Log("[HiveAxyl.Auth.Apple.Postprocess] added Sign in with Apple entitlement at "
                + entitlementsRelative);
        }

        // Resolves the macOS main target GUID, or null (logging why) if no Xcode project
        // or main target is found so the caller skips. macOS emits "<output>.xcodeproj"
        // (not the iOS "Unity-iPhone" layout) and names its native target after
        // PlayerSettings.productName; the GUID from GetUnityMainTargetGuid() is the
        // stable handle — a hardcoded name fails ProjectCapabilityManager with "Could
        // not find target". An IL2CPP build additionally emits Unity's own
        // "GameAssembly.xcodeproj" (the IL2CPP static library) alongside the app
        // project in an unspecified directory order, so this resolves against each
        // project and takes the first with a main target — the GameAssembly project has
        // none and is skipped. pbxprojPath is set to the resolved path on a non-null return.
        private static string TryResolveMacOsMainTargetGuid(
            string pathToBuiltProject, out string pbxprojPath)
        {
            pbxprojPath = null;

            string[] projects = Directory.GetDirectories(pathToBuiltProject, "*.xcodeproj");
            if (projects.Length == 0)
            {
                Debug.LogWarning("[HiveAxyl.Auth.Apple.Postprocess] .xcodeproj missing — skipped");
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
                    // GetUnityMainTargetGuid() is documented to return "" on some macOS
                    // Unity revisions (a known PBXProject API inconsistency Unity only
                    // backported to 21.3 LTS), which would otherwise skip the entitlement
                    // silently — and altool --validate-app does not check entitlements, so
                    // it would pass. Fall back to the target named after PlayerSettings.productName,
                    // the name macOS gives its native target on the affected revisions.
                    guid = project.TargetGuidByName(PlayerSettings.productName);
                }

                if (!string.IsNullOrEmpty(guid))
                {
                    pbxprojPath = candidatePath;
                    return guid;
                }
            }

            Debug.LogWarning("[HiveAxyl.Auth.Apple.Postprocess] macOS main target not found — skipped");
            return null;
        }
    }
}

#endif
