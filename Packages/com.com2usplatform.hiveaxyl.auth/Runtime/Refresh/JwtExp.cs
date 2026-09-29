// Copyright (c) Com2uS Platform Corp. All rights reserved.

using System;
using System.Text;

namespace Hive.Axyl.Auth
{
    /// <summary>Reads the <c>exp</c> claim (absolute Unix seconds) from a JWT access
    /// token. No signature verification — the token came from our own TLS refresh call
    /// and the value is only used for local expiry tracking.</summary>
    internal static class JwtExp
    {
        public static bool TryReadExp(string jwt, out long exp)
        {
            exp = 0;
            if (string.IsNullOrEmpty(jwt))
            {
                return false;
            }

            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return false;
            }

            byte[] payload;
            try
            {
                payload = Base64UrlDecode(parts[1]);
            }
            catch
            {
                return false;
            }

            return TryExtractExp(Encoding.UTF8.GetString(payload), out exp);
        }

        private static byte[] Base64UrlDecode(string s)
        {
            var t = s.Replace('-', '+').Replace('_', '/');
            switch (t.Length % 4)
            {
                case 2: t += "=="; break;
                case 3: t += "="; break;
                case 1: throw new FormatException("Invalid base64url length.");
            }
            return Convert.FromBase64String(t);
        }

        // Scans the payload for a top-level "exp" object key and returns its integer value.
        // Tracks brace/bracket depth and string spans, so a literal "exp" inside a string value
        // or as a key in a nested object is ignored — only a depth-1 key matches.
        private static bool TryExtractExp(string json, out long exp)
        {
            exp = 0;
            int depth = 0;
            int i = 0;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '{' || c == '[')
                {
                    depth++;
                    i++;
                }
                else if (c == '}' || c == ']')
                {
                    depth--;
                    i++;
                }
                else if (c == '"')
                {
                    int contentStart = i + 1;
                    int contentEnd = StringEnd(json, contentStart);
                    int afterColon = ColonAfter(json, contentEnd + 1);
                    if (depth == 1
                        && afterColon >= 0
                        && contentEnd - contentStart == 3
                        && string.CompareOrdinal(json, contentStart, "exp", 0, 3) == 0
                        && TryParseDigits(json, afterColon, out exp))
                    {
                        return true;
                    }
                    i = contentEnd + 1;
                }
                else
                {
                    i++;
                }
            }
            return false;
        }

        // Returns the index of the closing quote of a JSON string whose content starts at
        // <paramref name="start"/>, honoring backslash escapes.
        private static int StringEnd(string json, int start)
        {
            int i = start;
            while (i < json.Length)
            {
                if (json[i] == '\\') { i += 2; continue; }
                if (json[i] == '"') { return i; }
                i++;
            }
            return json.Length;
        }

        // If the next non-whitespace char at/after <paramref name="from"/> is ':', returns the
        // index just past it; otherwise -1 (the string was a value, not a key).
        private static int ColonAfter(string json, int from)
        {
            int i = from;
            while (i < json.Length && IsWhitespace(json[i]))
            {
                i++;
            }
            return (i < json.Length && json[i] == ':') ? i + 1 : -1;
        }

        private static bool TryParseDigits(string json, int from, out long value)
        {
            value = 0;
            int i = from;
            while (i < json.Length && IsWhitespace(json[i]))
            {
                i++;
            }
            int start = i;
            while (i < json.Length && char.IsDigit(json[i]))
            {
                i++;
            }
            return i > start && long.TryParse(json.Substring(start, i - start), out value);
        }

        private static bool IsWhitespace(char c)
            => c == ' ' || c == '\t' || c == '\n' || c == '\r';
    }
}
