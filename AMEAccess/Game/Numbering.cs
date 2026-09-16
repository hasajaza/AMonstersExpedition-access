using System.Collections.Generic;

namespace AMEAccess.Game
{
    /// <summary>
    /// Stable numbers for things there is more than one of.
    ///
    /// "tree" and "tree" on the same island are impossible to tell apart. Numbering them fixes
    /// that, but only if the numbers hold still: if they were assigned by distance from you,
    /// tree 1 would become tree 2 as you walked past, which is worse than no numbers at all.
    ///
    /// So numbers are assigned once per island, in a fixed spatial order (south to north, then
    /// west to east), and cached against the piece itself. A log keeps its number even after you
    /// roll it somewhere else, and a tree keeps its number until it is chopped.
    ///
    /// Only categories with more than one member are numbered - "tree 1" when there is a single
    /// tree is noise.
    /// </summary>
    internal static class Numbering
    {
        private static readonly Dictionary<Piece, int> Numbers = new Dictionary<Piece, int>();
        private static readonly Dictionary<string, int> NextFree = new Dictionary<string, int>();
        private static readonly HashSet<string> Numbered = new HashSet<string>();

        private static Island _island;
        private static bool _islandOnly;
        private static bool _building;

        internal static void Forget()
        {
            Numbers.Clear();
            NextFree.Clear();
            Numbered.Clear();
            _island = null;
        }

        /// <summary>Renumber from scratch the next time a number is asked for.</summary>
        internal static void Invalidate() { _island = null; }

        /// <summary>The number to speak after a piece's name, or 0 for none.</summary>
        internal static int Of(Piece p)
        {
            if (p == null || !Cfg.NumberObjects.Value) return 0;

            // Only number things the survey would actually list. Without this, ordinary ground
            // fell into the catch-all "object" category and came back as "land 1", "land 2",
            // "land 3" as you walked across an island.
            if (!Survey.IsNotable(p)) return 0;

            var island = Refs.CurrentIsland;
            if (island == null) return 0;

            // Rebuild when the island changes, and when the scope changes, because the scope
            // decides which pieces are in the set being numbered.
            if (island != _island || Survey.LastScopeIslandOnly != _islandOnly) Build(island);

            int n;
            if (Numbers.TryGetValue(p, out n)) return n;

            // Something new since the island was scanned - a log that drifted in, or a fresh
            // log from a chopped tree.
            //
            // Only number it if it is actually on this island. Without that check every piece
            // the mod ever describes takes the next number, and the distance scan reaches
            // twenty tiles onto neighbouring islands - which is how two boulders on one island
            // came to be called "boulder 25" and "boulder 298".
            if (!Survey.OnCurrentIslandFootprint(p.position)) return 0;

            string cat = Survey.CategoryOf(p);
            if (!Numbered.Contains(cat)) return 0;

            int next;
            NextFree.TryGetValue(cat, out next);
            next++;
            NextFree[cat] = next;
            Numbers[p] = next;
            return next;
        }

        private static void Build(Island island)
        {
            if (_building) return;          // Survey calls back into naming; do not recurse
            _building = true;

            try
            {
                Numbers.Clear();
                NextFree.Clear();
                Numbered.Clear();
                _island = island;
                _islandOnly = Survey.LastScopeIslandOnly;

                var pieces = Survey.NotablePieces();

                // Fixed spatial order so the numbers do not depend on where you are standing.
                pieces.Sort((a, b) =>
                {
                    int dz = a.position.z.CompareTo(b.position.z);
                    return dz != 0 ? dz : a.position.x.CompareTo(b.position.x);
                });

                var counts = new Dictionary<string, int>();
                foreach (var p in pieces)
                {
                    string c = Survey.CategoryOf(p);
                    int n; counts.TryGetValue(c, out n);
                    counts[c] = n + 1;
                }

                foreach (var kv in counts)
                    if (kv.Value > 1) Numbered.Add(kv.Key);

                foreach (var p in pieces)
                {
                    string c = Survey.CategoryOf(p);
                    if (!Numbered.Contains(c)) continue;

                    int next;
                    NextFree.TryGetValue(c, out next);
                    next++;
                    NextFree[c] = next;
                    Numbers[p] = next;
                }
            }
            finally { _building = false; }
        }
    }
}
