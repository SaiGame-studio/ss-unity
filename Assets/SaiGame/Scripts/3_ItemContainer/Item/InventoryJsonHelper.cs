namespace SaiGame.Services
{
    /// <summary>
    /// Pre-processes raw JSON from the inventory endpoint so that Unity's
    /// JsonUtility can deserialize it correctly.
    ///
    /// Problem: Unity JsonUtility cannot map a JSON *object* value into a C# string field.
    /// Fields like "base_stats", "public_properties", and "private_properties" are returned
    /// as JSON objects ({...}) by the server, but they are declared as string in the C# models.
    /// When JsonUtility encounters a type mismatch it silently stops parsing the current item,
    /// which causes only some items to appear in the resulting array.
    ///
    /// Fix: before handing the raw JSON to JsonUtility, this helper scans the string and
    /// replaces every occurrence of the affected fields with a stringified (escaped) version
    /// of the object value.  e.g.  "base_stats":{"a":1}  →  "base_stats":"{\"a\":1}"
    /// </summary>
    public static class InventoryJsonHelper
    {
        // Fields whose object/array values should be converted to escaped JSON strings
        private static readonly string[] ObjectStringFields =
        {
            "base_stats",
            "public_properties",
            "private_properties",
            "custom_properties",
        };

        /// <summary>
        /// Returns a version of <paramref name="json"/> where the listed object/array
        /// fields have their values replaced by escaped JSON string literals.
        /// </summary>
        public static string StringifyObjectFields(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            foreach (string field in ObjectStringFields)
                json = StringifyField(json, field);

            return json;
        }

        // ── Core scanner ──────────────────────────────────────────────────────

        private static string StringifyField(string json, string fieldName)
        {
            string key = $"\"{fieldName}\"";
            var sb = new System.Text.StringBuilder(json.Length + 64);
            int pos = 0;

            while (pos < json.Length)
            {
                // Find next occurrence of the key
                int keyIdx = json.IndexOf(key, pos, System.StringComparison.Ordinal);
                if (keyIdx < 0)
                {
                    sb.Append(json, pos, json.Length - pos);
                    break;
                }

                // Make sure we are NOT inside a string (simple heuristic: count unescaped quotes before keyIdx)
                if (IsInsideString(json, keyIdx))
                {
                    sb.Append(json, pos, keyIdx - pos + 1);
                    pos = keyIdx + 1;
                    continue;
                }

                // Append everything up to and including the key
                int afterKey = keyIdx + key.Length;
                sb.Append(json, pos, afterKey - pos);
                pos = afterKey;

                // Skip whitespace
                while (pos < json.Length && IsWhitespace(json[pos])) pos++;

                // Expect ':'
                if (pos >= json.Length || json[pos] != ':')
                    continue;

                sb.Append(':');
                pos++; // skip ':'

                // Skip whitespace
                while (pos < json.Length && IsWhitespace(json[pos])) pos++;

                if (pos >= json.Length) break;

                char first = json[pos];

                // If the value is already a string — leave it alone
                if (first == '"')
                    continue;

                // If the value is an object or array — capture it and stringify
                if (first == '{' || first == '[')
                {
                    int end = FindMatchingClose(json, pos);
                    if (end < 0) continue; // malformed — leave as is

                    string raw = json.Substring(pos, end - pos + 1);
                    sb.Append('"');
                    sb.Append(raw.Replace("\\", "\\\\").Replace("\"", "\\\""));
                    sb.Append('"');
                    pos = end + 1;
                    continue;
                }

                // Any other value — leave untouched
            }

            return sb.ToString();
        }

        /// <summary>
        /// Find the index of the closing brace/bracket that matches the opening at <paramref name="start"/>.
        /// Returns -1 if not found or malformed.
        /// </summary>
        private static int FindMatchingClose(string json, int start)
        {
            char open  = json[start];
            char close = open == '{' ? '}' : ']';
            int depth  = 0;
            bool inStr = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];

                if (inStr)
                {
                    if (c == '\\') { i++; continue; } // skip escaped char
                    if (c == '"') inStr = false;
                    continue;
                }

                if (c == '"') { inStr = true; continue; }
                if (c == open)  { depth++; continue; }
                if (c == close) { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        /// <summary>
        /// Naïve check: count unescaped quotes before <paramref name="idx"/>;
        /// if odd, we are inside a string literal.
        /// </summary>
        private static bool IsInsideString(string json, int idx)
        {
            int quotes = 0;
            for (int i = 0; i < idx; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    quotes++;
            }
            return (quotes % 2) == 1;
        }

        private static bool IsWhitespace(char c) =>
            c == ' ' || c == '\t' || c == '\n' || c == '\r';
    }
}
