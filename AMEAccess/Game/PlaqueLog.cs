using System.Collections.Generic;
using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// A record of every exhibit plaque read this session.
    ///
    /// Plaques are the longest text in the game and the easiest to lose: they fire when you walk
    /// up to an exhibit, and any incidental announcement afterwards can talk over them. They are
    /// also the actual reward for solving an island, so losing one matters more than losing a
    /// tile description.
    ///
    /// Everything is kept in order and written to the BepInEx log as well, so there is a
    /// permanent transcript in BepInEx\LogOutput.log after you quit.
    /// </summary>
    internal static class PlaqueLog
    {
        private struct Entry
        {
            public string Title;
            public string Body;
            public string Island;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static int _cursor = -1;

        internal static int Count => Entries.Count;

        internal static void Record(string title, string body, Island island)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return;

            var e = new Entry
            {
                Title = title,
                Body = body,
                Island = island == null ? null : Naming.IslandLabel(island, false)
            };

            // Walking back onto the same exhibit re-fires the caption; do not stack duplicates.
            if (Entries.Count > 0)
            {
                var last = Entries[Entries.Count - 1];
                if (last.Title == e.Title && last.Body == e.Body) { _cursor = Entries.Count - 1; return; }
            }

            Entries.Add(e);
            _cursor = Entries.Count - 1;

            var log = Plugin.Log;
            if (log != null)
            {
                log.LogInfo("[plaque " + Entries.Count + "] " +
                            (e.Island == null ? "" : e.Island + " - ") + e.Title);
                log.LogInfo("           " + e.Body);
            }
        }

        /// <summary>Read the most recent plaque again, in full.</summary>
        internal static string ReadLatest()
        {
            if (Entries.Count == 0) return "No plaques read yet.";
            _cursor = Entries.Count - 1;
            return Format(_cursor);
        }

        /// <summary>Step back through earlier plaques, or forward again.</summary>
        internal static string Step(int direction)
        {
            if (Entries.Count == 0) return "No plaques read yet.";

            _cursor += direction;
            if (_cursor < 0) _cursor = Entries.Count - 1;
            if (_cursor >= Entries.Count) _cursor = 0;

            return Format(_cursor);
        }

        private static string Format(int i)
        {
            var e = Entries[i];
            string head = (i + 1) + " of " + Entries.Count + ". ";
            if (!string.IsNullOrEmpty(e.Island)) head += e.Island + ". ";
            return head + e.Title + ". " + e.Body;
        }
    }
}
