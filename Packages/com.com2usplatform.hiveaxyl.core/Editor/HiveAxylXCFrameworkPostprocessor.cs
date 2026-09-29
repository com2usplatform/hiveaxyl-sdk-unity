// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_IOS

#nullable enable

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace Hive.Axyl.Core.Editor
{
    /// <summary>
    /// Embeds every <c>HiveAxyl*.xcframework</c> placed under each package's
    /// <c>Runtime/Plugins/iOS</c> directory into the generated Xcode project. Unity's
    /// built-in PluginImporter does not pick up <c>.xcframework</c> bundles
    /// automatically the way it does for <c>.framework</c>, so the iOS build
    /// post-processor wires them in manually.
    /// <para>
    /// Each xcframework is copied into the project's
    /// <c>Frameworks/&lt;Module&gt;.xcframework</c> directory, linked into the
    /// UnityFramework target (where the <c>DllImport("__Internal")</c>
    /// trampolines live) and embedded on the host app target, which is what
    /// makes Xcode sign it — the bundle is marked <c>CODE_SIGN_ON_COPY</c> so
    /// signing happens as part of the host app's archive.
    /// </para>
    /// <para>
    /// Discovery enumerates every registered <c>com.com2usplatform.hiveaxyl.*</c> package via
    /// <see cref="UnityEditor.PackageManager.PackageInfo"/> and reads its
    /// <c>resolvedPath</c>, so it works the same for a local path reference, a
    /// registry install and a git install. Every Hive Axyl module gets its
    /// native bridge embedded without registering anything per-module.
    /// </para>
    /// </summary>
    internal static class HiveAxylXCFrameworkPostprocessor
    {
        private const int k_CallbackOrder = 100;
        private const string k_PackagePrefix = "com.com2usplatform.hiveaxyl.";
        private const string k_PluginsRelative = "Runtime/Plugins/iOS";

        [PostProcessBuild(k_CallbackOrder)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) { return; }

            string pbxprojPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!TryLoadProject(pbxprojPath, out PBXProject project)) { return; }

            int embedded = EmbedAllXCFrameworks(project, pathToBuiltProject);
            if (embedded > 0)
            {
                ConfigureSwiftStdlibEmbedding(project);
            }

            project.WriteToFile(pbxprojPath);
            Debug.Log("[HiveAxyl.iOS.Postprocess] done — embedded " + embedded + " xcframework(s)");
        }

        private static bool TryLoadProject(string pbxprojPath, out PBXProject project)
        {
            project = new PBXProject();
            if (!File.Exists(pbxprojPath))
            {
                Debug.LogWarning(
                    "[HiveAxyl.iOS.Postprocess] project.pbxproj missing — skipped");
                return false;
            }
            project.ReadFromFile(pbxprojPath);
            return true;
        }

        private static int EmbedAllXCFrameworks(PBXProject project, string pathToBuiltProject)
        {
            // Unity's iOS build splits the host app into two targets: the
            // outer "Unity-iPhone" app and an inner "UnityFramework" static
            // framework that owns every Unity-side native dep. PInvoke
            // (`__Internal`) resolution happens in UnityFramework, so the
            // xcframework has to be linked there. The host target embeds it.
            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            int embedded = 0;
            foreach (string xcframeworkSource in DiscoverXCFrameworks())
            {
                string filename = Path.GetFileName(xcframeworkSource);
                string destInProject = Path.Combine(pathToBuiltProject, "Frameworks", filename);
                CopyDirectory(xcframeworkSource, destInProject);

                // Unity's iOS exporter auto-registers any `.xcframework`
                // sitting under a package's `Runtime/Plugins/iOS/` regardless of
                // PluginImporter settings, copying it into the generated
                // project at `Frameworks/<package>/Runtime/Plugins/iOS/<X>.xcframework`
                // and wiring it into UnityFramework's link phase. We want
                // a single canonical location (`Frameworks/<X>.xcframework`)
                // owned by this post-processor so library evolution / Swift
                // embedding / signing flags are applied consistently. Strip
                // the auto-generated entries before re-adding so we don't
                // hit "Multiple commands produce <X>.xcframework" when both
                // copies live in the same Xcode project.
                RemoveExistingFileEntries(project, filename);

                string projectRelative = "Frameworks/" + filename;
                string fileGuid = project.AddFile(projectRelative, projectRelative, PBXSourceTree.Source);
                project.AddFileToBuild(frameworkTargetGuid, fileGuid);
                project.AddFileToEmbedFrameworks(mainTargetGuid, fileGuid);

                Debug.Log("[HiveAxyl.iOS.Postprocess] embedded " + filename);
                embedded++;
            }
            return embedded;
        }

        private static void ConfigureSwiftStdlibEmbedding(PBXProject project)
        {
            // Our xcframeworks ship Swift binaries built with
            // `BUILD_LIBRARY_FOR_DISTRIBUTION=YES`, which depends on
            // the Swift standard library being present in the host
            // app. Unity's iOS template is ObjC-only and ships with
            // `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES=NO`, so dyld
            // aborts during framework load with a SIGABRT in
            // `RuntimeState::notifyDebuggerLoad` on macOS / Mac
            // Designed-for-iPad if we don't flip it on the host app.
            //
            // The setting MUST only apply to the host app
            // (`Unity-iPhone`). Flipping it on the embedded
            // `UnityFramework` target causes the framework to copy
            // its own libswift*.dylib next to the host's set, and
            // dyld then has to resolve duplicate Swift runtime
            // dylibs at startup — slow on the iOS simulator and
            // Mac, hangs entirely on a real device.
            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.SetBuildProperty(mainTargetGuid, "EMBEDDED_CONTENT_CONTAINS_SWIFT", "YES");
            project.SetBuildProperty(frameworkTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            Debug.Log("[HiveAxyl.iOS.Postprocess] enabled ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES on host only");
        }

        private static void RemoveExistingFileEntries(PBXProject project, string filename)
        {
            string canonical = "Frameworks/" + filename;
            string canonicalGuid = project.FindFileGuidByProjectPath(canonical);
            if (!string.IsNullOrEmpty(canonicalGuid))
            {
                project.RemoveFile(canonicalGuid);
                Debug.Log("[HiveAxyl.iOS.Postprocess] cleared stale ref " + canonical);
            }

            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (!package.name.StartsWith(k_PackagePrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }
                string autoPath = "Frameworks/" + package.name + "/" + k_PluginsRelative + "/" + filename;
                string autoGuid = project.FindFileGuidByProjectPath(autoPath);
                if (!string.IsNullOrEmpty(autoGuid))
                {
                    project.RemoveFile(autoGuid);
                    Debug.Log("[HiveAxyl.iOS.Postprocess] cleared auto-registered ref " + autoPath);
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<string> DiscoverXCFrameworks()
        {
            // Resolve every Hive Axyl Unity package via PackageInfo so the
            // discovery survives `file:` references, registry refs and any
            // path the consumer chooses.
            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (!package.name.StartsWith(k_PackagePrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }
                string pluginsDir = Path.Combine(package.resolvedPath, k_PluginsRelative);
                if (!Directory.Exists(pluginsDir)) { continue; }
                foreach (string candidate in Directory.GetDirectories(pluginsDir, "*.xcframework"))
                {
                    yield return candidate;
                }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
            Directory.CreateDirectory(destination);
            // Rebuild each child path via `GetRelativePath` + `Combine`
            // rather than `string.Replace`: the latter rewrites every
            // occurrence of `source` in the child path, so a build
            // server layout that nests the source name inside a child
            // segment would silently corrupt the destination.
            foreach (string entry in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(source, entry);
                Directory.CreateDirectory(Path.Combine(destination, rel));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(source, file);
                File.Copy(file, Path.Combine(destination, rel), overwrite: true);
            }
        }
    }
}

#endif
