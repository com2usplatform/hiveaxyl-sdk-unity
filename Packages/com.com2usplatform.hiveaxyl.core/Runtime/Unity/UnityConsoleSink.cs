// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System.Text;
using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// An <see cref="ILogSink"/> implementation that outputs structured log entries
    /// to the Unity console via <see cref="Debug.Log"/>, <see cref="Debug.LogWarning"/>,
    /// and <see cref="Debug.LogError"/>.
    /// <para>
    /// Output format: <c>[{Level}][{Category}] {Message} {ContextJson}</c>
    /// </para>
    /// <para>
    /// Thread safety: Unity's <c>Debug.Log</c> methods are thread-safe, so this sink
    /// can be called from any thread.
    /// </para>
    /// </summary>
    internal sealed class UnityConsoleSink : ILogSink
    {
        // Reuse StringBuilder per thread to reduce allocations.
        [System.ThreadStatic]
        private static StringBuilder s_builder;

        /// <inheritdoc />
        public void Emit(LogEntry entry)
        {
            var formatted = FormatEntry(entry);

            switch (entry.Level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(formatted);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning(formatted);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Debug.LogError(formatted);
                    break;
                default:
                    Debug.Log(formatted);
                    break;
            }
        }

        private static string FormatEntry(LogEntry entry)
        {
            if (s_builder == null)
            {
                s_builder = new StringBuilder(256);
            }

            s_builder.Clear();
            s_builder.Append('[');
            s_builder.Append(entry.Level);
            s_builder.Append(']');

            if (!string.IsNullOrEmpty(entry.Category))
            {
                s_builder.Append('[');
                s_builder.Append(entry.Category);
                s_builder.Append(']');
            }

            s_builder.Append(' ');
            s_builder.Append(entry.Message);

            if (entry.Context != null && entry.Context.Count > 0)
            {
                s_builder.Append(" {");
                var first = true;
                foreach (var kvp in entry.Context)
                {
                    if (!first)
                    {
                        s_builder.Append(", ");
                    }

                    s_builder.Append(kvp.Key);
                    s_builder.Append(": ");
                    s_builder.Append(kvp.Value ?? "null");
                    first = false;
                }

                s_builder.Append('}');
            }

            return s_builder.ToString();
        }
    }
}
