using System.Collections.Generic;
using AMEAccess.Speech;

namespace AMEAccess
{
    /// <summary>
    /// The key list, one key per press.
    ///
    /// Reading a whole section in one utterance was unusable: forty words go past before you
    /// can act on the first of them, and there is no way back to one you missed. So each press
    /// reads exactly one binding and you move through them at your own pace, the same way the
    /// island survey works.
    ///
    /// The list is generated from the bindings themselves, so every key the mod has is in it
    /// and none can be described under a key it no longer uses.
    /// </summary>
    internal static class Help
    {
        private static int _index = -1;
        private static string _lastSection;

        /// <summary>Next key. Wraps round at the end.</summary>
        internal static string Next(int direction)
        {
            var all = Cfg.AllBindings;
            if (all.Count == 0) return "No keys bound.";

            if (_index < 0 && direction > 0)
            {
                _index = 0;
                _lastSection = null;
                return "Key list, " + all.Count + " keys. Press again for the next, "
                     + "hold shift for the previous. " + Entry(all, 0);
            }

            _index += direction;
            if (_index >= all.Count) _index = 0;
            if (_index < 0) _index = all.Count - 1;

            return Entry(all, _index);
        }

        /// <summary>One binding: where you are, the section when it changes, the action, the key.</summary>
        private static string Entry(List<Cfg.Binding> all, int i)
        {
            var b = all[i];
            string head = (i + 1) + " of " + all.Count + ". ";

            // Name the section only when it changes, so it is not repeated fifty times.
            if (b.Section != _lastSection)
            {
                _lastSection = b.Section;
                head += b.Section + ". ";
            }

            return head + KeyNames.Action(b.Name) + ", " + KeyNames.Shortcut(b.Entry.Value) + ".";
        }

        internal static void Reset() { _index = -1; _lastSection = null; }

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
