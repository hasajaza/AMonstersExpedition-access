using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Nine places you can name and come back to.
    ///
    /// Set one with a number key while the review cursor is on the spot; press the same number
    /// while playing and you get walking directions to it. The point is to stop you having to
    /// re-find something: park a marker on the shoreline you want to bridge, or on the tree
    /// with the right log, and the way back is one keypress.
    ///
    /// Each slot remembers what was there when you set it, so recalling tells you what you
    /// marked and not just a coordinate.
    /// </summary>
    internal static class Bookmarks
    {
        private struct Mark
        {
            public bool Set;
            public Vector3i Position;
            public string What;
            public string Island;
        }

        private static readonly Mark[] Slots = new Mark[9];

        internal static string Set(int slot, Vector3i pos)
        {
            if (slot < 1 || slot > 9) return null;

            var top = Survey.TopOfColumn(pos);
            var island = Refs.CurrentIsland;

            Slots[slot - 1] = new Mark
            {
                Set = true,
                Position = pos,
                What = top == null ? "water" : Describe.PieceName(top),
                Island = island == null ? null : Naming.IslandLabel(island, false)
            };

            return "Bookmark " + slot + " set on " + Slots[slot - 1].What +
                   ", " + Survey.Relative(Refs.PlayerPos, pos) + " from you.";
        }

        /// <summary>Walking directions to a bookmark.</summary>
        internal static string Go(int slot)
        {
            if (slot < 1 || slot > 9) return null;

            var m = Slots[slot - 1];
            // Say how to set it, not where. Setting moved to shift and a number so that it
            // works however you are looking around; this message still said "in review mode".
            if (!m.Set)
                return "Bookmark " + slot + " is empty. Put the cursor where you want it and "
                     + "press shift and " + slot + ".";

            var sb = new StringBuilder("Bookmark ");
            sb.Append(slot).Append(", ").Append(m.What).Append(". ");
            sb.Append(Routing.Describe(Refs.PlayerPos, m.Position));

            // A bookmark on another island is worth flagging, since the route may not exist yet.
            var here = Refs.CurrentIsland;
            string hereName = here == null ? null : Naming.IslandLabel(here, false);
            if (m.Island != null && hereName != null && m.Island != hereName)
                sb.Append(" On ").Append(m.Island).Append('.');

            return sb.ToString();
        }

        internal static string Clear(int slot)
        {
            if (slot < 1 || slot > 9) return null;
            if (!Slots[slot - 1].Set) return "Bookmark " + slot + " is already empty.";
            Slots[slot - 1] = new Mark();
            return "Bookmark " + slot + " cleared.";
        }

        internal static string List()
        {
            var sb = new StringBuilder();
            int n = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].Set) continue;
                n++;
                sb.Append(' ').Append(i + 1).Append(", ").Append(Slots[i].What)
                  .Append(", ").Append(Survey.Relative(Refs.PlayerPos, Slots[i].Position)).Append('.');
            }
            return n == 0 ? "No bookmarks set." : n + (n == 1 ? " bookmark." : " bookmarks.") + sb;
        }
    }
}
