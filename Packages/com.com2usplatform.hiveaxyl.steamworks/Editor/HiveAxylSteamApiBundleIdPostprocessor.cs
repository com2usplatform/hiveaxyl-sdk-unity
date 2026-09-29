// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_STANDALONE_OSX

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Hive.Axyl.Steamworks.Editor
{
    /// <summary>
    /// The bundled Steamworks.NET plugin (<c>steam_api.bundle</c>) ships with the bundle
    /// identifier <c>com.rileylabrecque.steam_api</c>, whose underscore is not a valid
    /// <c>CFBundleIdentifier</c> and fails App Store / TestFlight validation
    /// ("Invalid Bundle Identifier"). The plugin comes from the third-party
    /// <c>com.rlabrecque.steamworks.net</c> package under the gitignored
    /// <c>Library/PackageCache</c>, so it cannot be patched at the source — rewrite the id
    /// in the built player instead. This ships in the Steamworks entry-point package so
    /// every Steam-dependent game inherits the fix. macOS standalone only; the pass runs
    /// before external code-signing.
    /// </summary>
    internal sealed class HiveAxylSteamApiBundleIdPostprocessor : IPostprocessBuildWithReport
    {
        private const string k_InvalidId = "com.rileylabrecque.steam_api";
        private const string k_ValidId = "com.rileylabrecque.steamapi";
        private const string k_BundleMarker = "/steam_api.bundle/";
        private const string k_BundleIdKey = "CFBundleIdentifier";

        int IOrderedCallback.callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX)
            {
                return;
            }

            var outputPath = report.summary.outputPath;
            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath))
            {
                return;
            }

            // Locate the plugin's Info.plist under the built output and rewrite only the
            // CFBundleIdentifier key (leaving any other Info.plist untouched).
            foreach (var infoPlist in Directory.GetFiles(outputPath, "Info.plist", SearchOption.AllDirectories))
            {
                if (!infoPlist.Replace('\\', '/').Contains(k_BundleMarker))
                {
                    continue;
                }

                var plist = new PlistDocument();
                plist.ReadFromFile(infoPlist);

                // Target the CFBundleIdentifier key specifically rather than a whole-file
                // string replace, matching the plutil key edits in install-to-unity.sh and
                // the PlistDocument usage in the APNs postprocessor.
                if (!plist.root.values.TryGetValue(k_BundleIdKey, out PlistElement idElement)
                    || idElement is not PlistElementString idString
                    || idString.value != k_InvalidId)
                {
                    // Bundle is present but its id is not the known-invalid one: either it
                    // was already rewritten, or upstream Steamworks.NET changed the bundle
                    // name/id. Log it so this fix silently no-op'ing becomes visible drift
                    // instead of an unnoticed regression.
                    Debug.Log($"[HiveAxyl.Steamworks] {k_BundleMarker} found but {k_BundleIdKey} is not "
                        + $"{k_InvalidId} — skipped (already fixed or upstream changed) in {infoPlist}");
                    continue;
                }

                idString.value = k_ValidId;
                plist.WriteToFile(infoPlist);
                Debug.Log($"[HiveAxyl.Steamworks] Rewrote {k_InvalidId} -> {k_ValidId} in {infoPlist}");
            }
        }
    }
}

#endif
