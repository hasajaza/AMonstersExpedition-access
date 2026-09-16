using System.Text;

namespace AMEAccess.Util
{
    /// <summary>
    /// Strips TextMeshPro rich-text markup.
    ///
    /// The shipped exhibit prose contains tags such as &lt;mark=#000000ff&gt;…&lt;/mark&gt;,
    /// which a screen reader would otherwise read out character by character. We keep the
    /// inner text and drop the tags. Also normalises whitespace and the non-breaking space
    /// that TextMeshPro sometimes emits.
    /// </summary>
    internal static class Rich
    {
        internal static string Strip(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            bool inTag = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '<')
                {
                    // Only treat it as a tag if there is a closing '>' reasonably close by,
                    // so that a stray '<' in prose survives.
                    int close = s.IndexOf('>', i + 1);
                    if (close > i && close - i <= 64) { inTag = true; continue; }
                }

                if (inTag)
                {
                    if (c == '>') inTag = false;
                    continue;
                }

                if (c == '\u00A0' || c == '\u200B') { sb.Append(' '); continue; }
                if (c == '\r' || c == '\n' || c == '\t') { sb.Append(' '); continue; }

                sb.Append(c);
            }

            // Collapse runs of whitespace.
            var outSb = new StringBuilder(sb.Length);
            bool lastSpace = false;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                bool isSpace = c == ' ';
                if (isSpace && lastSpace) continue;
                outSb.Append(c);
                lastSpace = isSpace;
            }

            return outSb.ToString().Trim();
        }
    }
}
