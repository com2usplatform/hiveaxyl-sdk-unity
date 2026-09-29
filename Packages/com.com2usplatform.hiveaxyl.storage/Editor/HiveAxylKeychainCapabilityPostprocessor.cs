// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_IOS || UNITY_STANDALONE_OSX

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Hive.Axyl.Storage.Editor
{
    /// <summary>
    /// Attaches the Keychain Sharing entitlement to the host app target so
    /// SecureStorage's <c>SecItem*</c> calls succeed on a code-signed build.
    /// Without it a signed (and, on macOS, sandboxed) app gets
    /// <c>errSecMissingEntitlement</c> from the Keychain, even though an
    /// unsigned/simulator build works without it. The
    /// <c>keychain-access-groups</c> key is the same on iOS and macOS
    /// (Apple's Keychain Access Groups entitlement is documented for both),
    /// so the same pass serves both — iOS resolves the fixed
    /// <c>Unity-iPhone</c> target, macOS resolves the generated main target
    /// by GUID (its name tracks PlayerSettings.productName).
    /// <para>
    /// The native plugin (<c>SecureStoragePlugin</c>) queries
    /// <c>kSecClassGenericPassword</c> without setting a custom access group,
    /// so registering the default group
    /// <c>$(AppIdentifierPrefix)$(CFBundleIdentifier)</c> — Apple's stock
    /// per-app keychain partition — is sufficient. Unity's
    /// <c>ProjectCapabilityManager.AddKeychainSharing()</c> writes that group
    /// into the shared <c>HiveAxyl.entitlements</c> file; it re-reads any
    /// existing entitlements on construction, so it composes with the Apple
    /// Sign In and APNs passes that write the same file.
    /// </para>
    /// </summary>
    internal static class HiveAxylKeychainCapabilityPostprocessor
    {
        // Runs after HiveAxylXCFrameworkPostprocessor (order 100). The
        // capability manager re-reads pbxproj from disk, so we let the
        // framework-embedding pass finish writing first to avoid clobbering
        // its changes.
        private const int k_CallbackOrder = 200;
        private const string k_EntitlementsFileName = "HiveAxyl.entitlements";
        private const string k_EntitlementKey = "keychain-access-groups";
        // The default per-app keychain partition. SecItem queries that omit
        // kSecAttrAccessGroup hit this group, which matches what the native
        // plugin does. Xcode resolves the $(...) tokens at sign time.
        private const string k_DefaultAccessGroup = "$(AppIdentifierPrefix)$(CFBundleIdentifier)";

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
                // Park the entitlements inside the target's source folder next
                // to Info.plist, matching the layout Unity uses for user
                // entitlements and the sibling capability passes.
                entitlementsRelative = Path.Combine(mainTargetName, k_EntitlementsFileName);
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                mainTargetGuid = TryResolveMacOsMainTargetGuid(pathToBuiltProject, out pbxprojPath);
                if (string.IsNullOrEmpty(mainTargetGuid))
                {
                    return;
                }

                // Resolve by GUID only (ProjectCapabilityManager rejects a name
                // and a GUID together); park the entitlements at the project root.
                mainTargetName = null;
                entitlementsRelative = k_EntitlementsFileName;
            }
            else
            {
                return;
            }

            if (!File.Exists(pbxprojPath))
            {
                Debug.LogWarning("[HiveAxyl.Storage.Postprocess] project.pbxproj missing — skipped");
                return;
            }

            string entitlementsAbsolute = Path.Combine(pathToBuiltProject, entitlementsRelative);

            // Guard against iOS "Append to existing project" rebuilds: they reuse the
            // pbxproj, and PlistArray.AddString does not dedupe, so a second run would
            // write the access group twice and validators may reject the signature. Keyed
            // off the persisted file only for iOS — macOS regenerates the pbxproj on every
            // build while the root entitlements file persists, so gating macOS on that file
            // would skip the wiring and leave the fresh pbxproj without the capability.
            if (target == BuildTarget.iOS
                && File.Exists(entitlementsAbsolute)
                && File.ReadAllText(entitlementsAbsolute).Contains(k_EntitlementKey))
            {
                Debug.Log("[HiveAxyl.Storage.Postprocess] keychain entitlement already present — skipped");
                return;
            }

            var capabilityManager = new ProjectCapabilityManager(
                pbxprojPath,
                entitlementsRelative,
                mainTargetName,
                mainTargetGuid);

            // Register just the default access group; Unity 6's overload
            // requires the array, so we pass the single entry the native
            // plugin actually uses.
            capabilityManager.AddKeychainSharing(new[] { k_DefaultAccessGroup });
            capabilityManager.WriteToFile();

            Debug.Log("[HiveAxyl.Storage.Postprocess] added Keychain Sharing entitlement at "
                + entitlementsRelative);
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
                Debug.LogWarning("[HiveAxyl.Storage.Postprocess] .xcodeproj missing — skipped");
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

            Debug.LogWarning("[HiveAxyl.Storage.Postprocess] macOS main target not found — skipped");
            return null;
        }
    }
}

#endif
