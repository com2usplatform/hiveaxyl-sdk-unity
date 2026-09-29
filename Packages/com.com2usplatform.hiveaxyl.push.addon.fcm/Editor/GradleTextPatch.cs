// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Hive.Axyl.Push.Addon.FCM.Editor
{
    /// <summary>
    /// Outcome of a patch attempt against one Gradle build file.
    /// </summary>
    internal enum GradlePatchResult
    {
        /// <summary>The edit is already present; the file needs no write.</summary>
        AlreadyPresent,

        /// <summary>The edit was inserted; the caller must write the returned text.</summary>
        Inserted,

        /// <summary>No anchor to insert after; the text is returned unchanged.</summary>
        AnchorMissing,
    }

    /// <summary>
    /// Text edits on a generated Gradle project, kept free of <c>UnityEditor.Android</c> so the
    /// logic compiles and is unit-testable whatever the active build target is — the callback
    /// that drives it cannot be, and that gap is what let a partial patch ship (see the
    /// caller's guard in <c>HiveAxylFcmGoogleServicesPostprocessor</c>).
    /// <para>
    /// Matching is line-based and comment-aware on purpose. A plain <c>IndexOf</c> over the whole
    /// file matches inside comments: a stray <c>plugins {</c> in a comment above the real block
    /// took the insertion with it and left the file syntactically broken, and a commented-out
    /// mention of the plugin id read as "already declared" and skipped the edit silently.
    /// </para>
    /// </summary>
    internal static class GradleTextPatch
    {
        // `plugins{`, `plugins {` and `plugins  {` are all valid Gradle.
        private static readonly Regex k_PluginsBlock = new Regex(@"^\s*plugins\s*\{", RegexOptions.Compiled);

        private const string k_AndroidApplicationId = "com.android.application";

        /// <summary>
        /// Declares <paramref name="pluginId"/> in the root project's <c>plugins</c> block without
        /// applying it. A legacy <c>buildscript</c> classpath entry for the same artefact counts as
        /// already declared — the coordinate is spelled with a colon there, so a plain substring
        /// test against the dotted plugin id misses it and would insert a duplicate.
        /// </summary>
        internal static GradlePatchResult DeclarePlugin(
            string text, string pluginId, string version, out string patched)
        {
            patched = text;

            var declared = new Regex(
                @"(^\s*id\s+['""]" + Regex.Escape(pluginId) + @"['""])" +
                @"|(classpath\s+['""]" + Regex.Escape(ClasspathCoordinate(pluginId)) + @":)");
            if (MatchesOutsideComments(text, declared))
            {
                return GradlePatchResult.AlreadyPresent;
            }

            var anchor = LineIndexOutsideComments(text, k_PluginsBlock);
            if (anchor < 0)
            {
                return GradlePatchResult.AnchorMissing;
            }

            patched = InsertAfterLine(
                text, anchor, $"    id '{pluginId}' version '{version}' apply false");
            return GradlePatchResult.Inserted;
        }

        /// <summary>
        /// Applies <paramref name="pluginId"/> in a module build file, immediately after the Android
        /// application plugin it extends — the Google plugin hooks that plugin's variants, so order
        /// matters. Both the legacy <c>apply plugin:</c> form and the declarative <c>plugins</c>
        /// block are accepted, because Unity's launcher template has used the former so far and
        /// nothing pins it there.
        /// </summary>
        internal static GradlePatchResult ApplyPlugin(string text, string pluginId, out string patched)
        {
            patched = text;

            var applied = new Regex(
                @"(^\s*apply\s+plugin:\s*['""]" + Regex.Escape(pluginId) + @"['""])" +
                @"|(^\s*id\s+['""]" + Regex.Escape(pluginId) + @"['""])");
            if (MatchesOutsideComments(text, applied))
            {
                return GradlePatchResult.AlreadyPresent;
            }

            var legacy = new Regex(
                @"^\s*apply\s+plugin:\s*['""]" + Regex.Escape(k_AndroidApplicationId) + @"['""]");
            var anchor = LineIndexOutsideComments(text, legacy);
            if (anchor >= 0)
            {
                patched = InsertAfterLine(text, anchor, $"apply plugin: '{pluginId}'");
                return GradlePatchResult.Inserted;
            }

            var declarative = new Regex(
                @"^(\s*)id\s+['""]" + Regex.Escape(k_AndroidApplicationId) + @"['""]");
            anchor = LineIndexOutsideComments(text, declarative, out var indent);
            if (anchor >= 0)
            {
                patched = InsertAfterLine(text, anchor, $"{indent}id '{pluginId}'");
                return GradlePatchResult.Inserted;
            }

            return GradlePatchResult.AnchorMissing;
        }

        /// <summary>
        /// Resolves the Gradle project root from the directory Unity passes to
        /// <c>OnPostGenerateGradleAndroidProject</c>. Unity documents that argument as the Gradle
        /// project path, and this does not rely on it being the root: <c>settings.gradle</c> exists
        /// only at the root, so <paramref name="hasSettingsFile"/> accepts the argument itself or
        /// its parent — which covers the argument pointing at a module such as
        /// <c>unityLibrary</c> without asserting when Unity does that. Returns null when neither is
        /// the root, and the caller stops the build rather than guessing.
        /// </summary>
        internal static string ResolveGradleRoot(
            string path, Func<string, bool> hasSettingsFile, Func<string, string> parentOf)
        {
            if (hasSettingsFile(path))
            {
                return path;
            }

            var parent = parentOf(path);
            return parent != null && hasSettingsFile(parent) ? parent : null;
        }

        // `com.google.gms.google-services` is the plugin id; the artefact a legacy buildscript
        // block puts on the classpath is the same thing spelled `com.google.gms:google-services`.
        private static string ClasspathCoordinate(string pluginId)
        {
            var cut = pluginId.LastIndexOf('.');
            return cut < 0 ? pluginId : pluginId.Substring(0, cut) + ":" + pluginId.Substring(cut + 1);
        }

        private static bool MatchesOutsideComments(string text, Regex pattern)
            => LineIndexOutsideComments(text, pattern) >= 0;

        private static int LineIndexOutsideComments(string text, Regex pattern)
            => LineIndexOutsideComments(text, pattern, out _);

        /// <summary>
        /// Index of the first line whose code — comment text removed — matches
        /// <paramref name="pattern"/>, or -1. <paramref name="firstGroup"/> carries capture group 1
        /// of that match (used to copy a line's indentation).
        /// </summary>
        private static int LineIndexOutsideComments(string text, Regex pattern, out string firstGroup)
        {
            firstGroup = string.Empty;
            var lines = text.Split('\n');
            var inBlockComment = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var code = CodeOf(lines[i], ref inBlockComment);
                var match = pattern.Match(code);
                if (!match.Success)
                {
                    continue;
                }

                firstGroup = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
                return i;
            }

            return -1;
        }

        /// <summary>
        /// The code portion of one line, with <c>//</c> and <c>/* */</c> comment text blanked out.
        /// <paramref name="inBlockComment"/> carries block state across lines. A <c>//</c> inside a
        /// string literal is treated as a comment; that only ever shortens a line, so it cannot
        /// produce a false match for the patterns here.
        /// </summary>
        private static string CodeOf(string line, ref bool inBlockComment)
        {
            var code = new StringBuilder(line.Length);

            for (var i = 0; i < line.Length; i++)
            {
                if (inBlockComment)
                {
                    if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    break;
                }

                code.Append(line[i]);
            }

            return code.ToString();
        }

        private static string InsertAfterLine(string text, int lineIndex, string insertion)
        {
            var lines = new List<string>(text.Split('\n'));
            lines.Insert(lineIndex + 1, insertion);
            return string.Join("\n", lines);
        }
    }
}
