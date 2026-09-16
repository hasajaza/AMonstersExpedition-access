using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// The eight-direction letter cluster.
    ///
    ///     U  I  O          north-west  north  north-east
    ///     J  K  L    =     west        here   east
    ///     M  ,  .          south-west  south  south-east
    ///
    /// The keys sit in the same physical arrangement as the tiles they describe, which is the
    /// numpad review layout every screen reader user already knows, moved onto letters for
    /// laptops. Held with Ctrl so nothing collides with the game or the rest of the mod.
    ///
    /// Diagonals are included even though the monster cannot walk diagonally. A sighted player
    /// sees those tiles, so you should hear them - but we do not claim a push or a chop for
    /// them, because those only happen along the four cardinals.
    /// </summary>
    internal static class Compass
    {
        internal static readonly Vector3i NorthEast = Vector3i.north + Vector3i.east;
        internal static readonly Vector3i SouthEast = Vector3i.south + Vector3i.east;
        internal static readonly Vector3i SouthWest = Vector3i.south + Vector3i.west;
        internal static readonly Vector3i NorthWest = Vector3i.north + Vector3i.west;

        internal static bool IsCardinal(Vector3i d) => d.x == 0 || d.z == 0;

        internal static string Name(Vector3i d)
        {
            if (d.z > 0 && d.x > 0) return "north east";
            if (d.z > 0 && d.x < 0) return "north west";
            if (d.z < 0 && d.x > 0) return "south east";
            if (d.z < 0 && d.x < 0) return "south west";
            if (d.z > 0) return "north";
            if (d.z < 0) return "south";
            if (d.x > 0) return "east";
            if (d.x < 0) return "west";
            return "here";
        }

        /// <summary>
        /// Where the reading is taken from. In review mode the cluster follows the cursor, so
        /// you can park it somewhere across the island and feel around it; otherwise it reads
        /// around your monster.
        /// </summary>
        private static Vector3i Origin(bool reviewMode)
            => reviewMode ? ReviewCursor.Position : Refs.PlayerPos;

        /// <summary>Read one of the eight surrounding tiles.</summary>
        internal static string Read(Vector3i dir, bool reviewMode)
        {
            var origin = Origin(reviewMode);
            var sb = new StringBuilder();
            sb.Append(Name(dir)).Append(": ");

            // Cardinals get the full treatment, including whether walking there would chop or
            // push, because the game can only interact along those four.
            if (IsCardinal(dir) && !reviewMode)
            {
                sb.Append(Describe.Neighbour(dir));
                return sb.Append('.').ToString();
            }

            var cell = origin + dir;
            var top = Survey.TopOfColumn(cell);
            sb.Append(top == null ? "water" : Describe.PieceName(top));

            if (top != null)
            {
                int rise = top.top.y - origin.y;
                if (rise > 0) sb.Append(", higher");
                else if (rise < 0) sb.Append(", lower");
            }

            return sb.Append('.').ToString();
        }

        /// <summary>The centre key: where the reading is being taken from.</summary>
        internal static string Here(bool reviewMode)
        {
            if (!reviewMode) return Describe.WhereAmI();
            return "Cursor. " + ReviewCursor.Read();
        }
    }
}
