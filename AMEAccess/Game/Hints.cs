using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// The game's own hint system, spoken.
    ///
    /// This is parity, not assistance. A Monster's Expedition ships an island hint feature
    /// behind a settings toggle ("Enable Island Hints"). When it is on, a sighted player sees
    /// ghost logs showing where logs are meant to end up. So when — and only when — the player
    /// has that setting on, we say the same thing out loud.
    ///
    /// With the setting off, the hint key says so and nothing else. We never read hint targets
    /// that a sighted player would not currently be seeing.
    /// </summary>
    internal static class Hints
    {
        internal static bool Enabled => global::Config.enableIslandHints;

        private static Island _shownFor;
        private static bool _showing;

        /// <summary>Name a key the way a person says it, from the live binding.</summary>
        private static string KeyName(BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut> e)
        {
            var k = e.Value;
            if (k.MainKey == UnityEngine.KeyCode.None) return "the read hints key";

            string raw = k.ToString();
            if (string.IsNullOrEmpty(raw)) return k.MainKey.ToString();

            var parts = raw.Split('+');
            var sb = new StringBuilder();
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(" plus ");
                sb.Append(p);
            }
            return sb.ToString();
        }

        internal static int Count(Island island)
        {
            if (island == null) return 0;
            int n = 0;
            if (island.logHints != null) n += island.logHints.Count;
            if (island.raftHints != null) n += island.raftHints.Count;
            return n;
        }

        /// <summary>Toggle the visual hints, exactly as the in-game hint button does.</summary>
        internal static string Toggle()
        {
            if (!Enabled) return "Island hints are turned off in the game's settings.";

            var island = Refs.CurrentIsland;
            if (island == null) return "No island to hint about.";

            int n = Count(island);
            if (n == 0) return "This island has no hints.";

            IslandHinter.ToggleHintsFor(island);

            // IslandHinter keeps its shown/hidden state in private fields, so we track our own.
            // It is reset whenever the island changes, which is when the game hides them anyway.
            if (!ReferenceEquals(island, _shownFor)) { _shownFor = island; _showing = false; }
            _showing = !_showing;

            if (!_showing) return "Hints hidden.";

            // Show and read in one action; asking for a second keypress to hear what appeared
            // just adds a step a sighted player never takes.
            return "Hints shown. " + Read();
        }

        /// <summary>
        /// Speak where the hints point, the way the ghost logs show it on screen.
        /// A LogHint carries the target placement; IsHintSatisfied says whether a log or raft
        /// of matching bounds is already sitting there.
        /// </summary>
        internal static string Read()
        {
            if (!Enabled) return "Island hints are turned off in the game's settings.";

            var island = Refs.CurrentIsland;
            if (island == null) return "No island to hint about.";
            if (Count(island) == 0) return "This island has no hints.";

            var origin = Refs.PlayerPos;
            var sb = new StringBuilder();
            int done = 0, total = 0;

            if (island.logHints != null)
            {
                foreach (var h in island.logHints)
                {
                    if (h == null) continue;
                    total++;
                    if (IslandHinter.IsHintSatisfied(island, h)) { done++; continue; }
                    sb.Append(' ').Append(DescribeLogHint(island, h, origin));
                }
            }

            if (island.raftHints != null)
            {
                foreach (var h in island.raftHints)
                {
                    if (h == null) continue;
                    total++;
                    if (IslandHinter.IsHintSatisfied(island, h)) { done++; continue; }
                    sb.Append(' ').Append(DescribeRaftHint(island, h, origin));
                }
            }

            string head = done + " of " + total + " logs in place.";
            return sb.Length == 0 ? head : head + sb.ToString();
        }

        /// <summary>
        /// Spell out a log hint properly.
        ///
        /// "Log wanted at 3 north, 4 east, lying" is not enough to act on: a log occupies
        /// several tiles, so a single point does not say which ones, and "lying" alone does not
        /// say along which axis. The hint carries a length, an orientation in
        /// localPieceState.direction, and a bounds covering every tile it must fill, so we give
        /// all three and name both ends.
        /// </summary>
        private static string DescribeLogHint(Island island, LogHint h, Vector3i origin)
        {
            var b = h.GetBounds(island);
            var sb = new StringBuilder("Log ");

            if (h.length > 0) sb.Append(h.length).Append(" long, ");

            if (h.logState.standing)
            {
                sb.Append("standing, at ").Append(Survey.Relative(origin, b.min)).Append('.');
                return sb.ToString();
            }

            var dir = h.localPieceState.direction;
            sb.Append("lying ").Append(dir.x != 0 ? "east to west" : "north to south").Append(", ");

            // Name both ends so you know exactly which tiles have to be covered.
            var a = new Vector3i(b.min.x, b.min.y, b.min.z);
            var z = new Vector3i(b.max.x, b.min.y, b.max.z);

            if (a == z) sb.Append("at ").Append(Survey.Relative(origin, a)).Append('.');
            else sb.Append("from ").Append(Survey.Relative(origin, a))
                   .Append(" to ").Append(Survey.Relative(origin, z)).Append('.');

            return sb.ToString();
        }

        private static string DescribeRaftHint(Island island, RaftHint h, Vector3i origin)
        {
            var b = h.GetBounds(island);
            var sb = new StringBuilder("Raft ");
            if (h.length > 0) sb.Append(h.length).Append(" long, ");

            var a = new Vector3i(b.min.x, b.min.y, b.min.z);
            var z = new Vector3i(b.max.x, b.min.y, b.max.z);
            if (a == z) sb.Append("at ").Append(Survey.Relative(origin, a)).Append('.');
            else sb.Append("from ").Append(Survey.Relative(origin, a))
                   .Append(" to ").Append(Survey.Relative(origin, z)).Append('.');
            return sb.ToString();
        }

        private static string Describe_Count(int n) =>
            n + (n == 1 ? " hint on this island" : " hints on this island");
    }
}
