using System.Collections.Generic;
using System.Text;
using AMEAccess.Speech;

namespace AMEAccess
{
    /// <summary>
    /// The key list, spoken a section at a time.
    ///
    /// Generated from the bindings themselves rather than written by hand, so every key the mod
    /// binds is in here and none can be described under a key it no longer uses. Each entry is
    /// the action followed by its key, modifiers first: "east, control plus L".
    /// </summary>
    internal static class Help
    {
        private static int _section = -1;

        private static List<string> Titles()
        {
            var titles = new List<string>();
            foreach (var b in Cfg.AllBindings)
                if (!titles.Contains(b.Section)) titles.Add(b.Section);
            return titles;
        }

        private static string Section(string title)
        {
            var sb = new StringBuilder(title).Append(". ");
            foreach (var b in Cfg.AllBindings)
            {
                if (b.Section != title) continue;
                sb.Append(KeyNames.Action(b.Name)).Append(", ")
                  .Append(KeyNames.Shortcut(b.Entry.Value)).Append(". ");
            }
            return sb.ToString();
        }

        /// <summary>Each press advances one section.</summary>
        internal static string Next()
        {
            var titles = Titles();
            if (titles.Count == 0) return "No keys bound.";

            _section++;
            if (_section >= titles.Count + 1) _section = 0;

            // A short orientation first, then the sections.
            if (_section == 0)
            {
                return "Key list, " + (titles.Count + 1) + " parts. Press again for each part. "
                     + "Movement is the game's own arrow keys. Undo and reset are the game's keys "
                     + "too, in its controls settings. Everything below is the mod's.";
            }

            string title = titles[_section - 1];
            return "Part " + (_section + 1) + " of " + (titles.Count + 1) + ". " + Section(title)
                 + (_section == titles.Count ? "That is the end. Press again to start over." : "");
        }

        internal static void Reset() { _section = -1; }

        /// <summary>Write the whole list to the log, so there is a copy to read.</summary>
        internal static void Dump(BepInEx.Logging.ManualLogSource log)
        {
            log.LogInfo("--- AMEAccess key list ---");
            string current = null;
            foreach (var b in Cfg.AllBindings)
            {
                if (b.Section != current)
                {
                    current = b.Section;
                    log.LogInfo("[" + current + "]");
                }
                log.LogInfo(string.Format("   {0,-22} {1}",
                    KeyNames.Action(b.Name), KeyNames.Shortcut(b.Entry.Value)));
            }
            log.LogInfo("--- end key list ---");
        }
    }
}
