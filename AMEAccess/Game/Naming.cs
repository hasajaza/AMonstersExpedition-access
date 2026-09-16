using System.Globalization;
using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Island names and coordinates.
    ///
    /// IslandNameDisplay.CalculateIslandName and IslandNameDisplay.coordinateOrigin are both
    /// private, so rather than reflect into them we reproduce the exact logic. The original,
    /// read from IL, is:
    ///
    ///     Boundsi b = island.board.CalculateLandBounds().OffsetBy(coordinateOrigin);
    ///     int cx = (b.min.x + b.max.x) / 2;
    ///     int cz = (b.min.z + b.max.z) / 2;
    ///     int levelId  = island.serializedIsland.serializedLevel.id;
    ///     int islandUid = island.serializedIsland.id.uid;
    ///     return string.Format("{0}.{1}, {2}.{3}", cx, levelId.ToString("0000"),
    ///                                              cz, islandUid.ToString("0000"));
    ///
    /// with coordinateOrigin = new Vector3i(200, 0, 200), set in the static constructor.
    ///
    /// We do the offset with plain integer arithmetic so we do not depend on Boundsi.OffsetBy.
    /// </summary>
    internal static class Naming
    {
        private static string _lastBiome;

        /// <summary>Set while answering a direct question, where the biome is always wanted.</summary>
        internal static bool AlwaysBiome;

        internal const int OriginX = 200;
        internal const int OriginZ = 200;

        /// <summary>The same label the game shows in its island-name overlay.</summary>
        internal static string IslandName(Island island)
        {
            if (island == null || island.board == null) return "unknown island";

            Boundsi b = island.board.CalculateLandBounds();
            int cx = ((b.min.x + OriginX) + (b.max.x + OriginX)) / 2;
            int cz = ((b.min.z + OriginZ) + (b.max.z + OriginZ)) / 2;

            int levelId = 0, uid = 0;
            var si = island.serializedIsland;
            if (si != null)
            {
                uid = si.id.uid;
                if (si.serializedLevel != null) levelId = si.serializedLevel.id;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}, {2}.{3}",
                cx, levelId.ToString("0000"), cz, uid.ToString("0000"));
        }

        /// <summary>
        /// Spoken form of the island label. Read digit-groups apart so a screen reader does not
        /// run "214.0031" together as one long number.
        /// </summary>
        internal static string IslandNameSpoken(Island island)
        {
            string raw = IslandName(island);
            return raw.Replace(".", " dot ");
        }

        /// <summary>
        /// A descriptive island name, built from what is actually on it.
        ///
        /// The game itself has no island names - the HUD label is purely coordinates, and the
        /// warp map uses unlabelled icons. But coordinates are miserable to remember, so we
        /// name an island after its most distinctive feature and keep the coordinate as the
        /// unambiguous part.
        ///
        /// Sources, in order of preference:
        ///   1. Island.exhibitDescriptionId - set on only 15 of the 763 islands, but exact.
        ///   2. A Landmark piece standing on the island, whose exhibit title we can read.
        ///      This covers all 167 exhibits, which is where the real landmarks are.
        ///   3. A villager, a postbox or a campfire.
        ///   4. Nothing, in which case the coordinate stands alone.
        ///
        /// The biome comes from the Biome asset's own name, tidied up: "BiomeSwampMushrooms2"
        /// reads as "swamp mushrooms".
        /// </summary>
        internal static string IslandLabel(Island island, bool withCoordinates = true)
        {
            if (island == null) return "unknown island";

            var sb = new StringBuilder();

            string feature = Feature(island);
            if (!string.IsNullOrEmpty(feature)) sb.Append(feature);

            // Biome is the region, not the island, and a whole region shares one. Saying
            // "summer" on every island in the summer region is noise, so only mention it when
            // it differs from the last island announced. Asking directly always reports it.
            string biome = BiomeName(island);
            if (!string.IsNullOrEmpty(biome) && Cfg.SpeakBiome.Value
                && (AlwaysBiome || biome != _lastBiome))
            {
                _lastBiome = biome;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(biome);
            }
            else if (!string.IsNullOrEmpty(biome)) _lastBiome = biome;

            if (withCoordinates || sb.Length == 0)
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(IslandNameSpoken(island));
            }

            return sb.ToString();
        }

        /// <summary>The most distinctive thing standing on the island, if anything.</summary>
        private static string Feature(Island island)
        {
            // The exact field first, where the designers set one.
            if (!string.IsNullOrEmpty(island.exhibitDescriptionId))
            {
                string t = GameText.ExhibitTitleById(island.exhibitDescriptionId);
                if (!string.IsNullOrEmpty(t)) return Tidy(t);
            }

            if (island.board == null) return null;

            // Scan Piece.all over the island's footprint rather than the island's own board.
            //
            // Exhibits come back with Piece.island == null and are not necessarily parented to
            // the island board, so asking the board for them finds nothing. Position is the
            // only reliable way to say which island an exhibit stands on.
            var bounds = island.board.CalculateLandBounds();
            var pieces = Piece.all;
            if (pieces == null) return null;

            int minX = bounds.min.x - 2, maxX = bounds.max.x + 2;
            int minZ = bounds.min.z - 2, maxZ = bounds.max.z + 2;

            string villager = null, postbox = null, campfire = null, monument = null;

            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null) continue;

                var pp = p.position;
                if (pp.x < minX || pp.x > maxX || pp.z < minZ || pp.z > maxZ) continue;

                if (p.type == PieceType.Landmark)
                {
                    string t = GameText.ExhibitTitle(p as Landmark);
                    if (!string.IsNullOrEmpty(t)) return Tidy(t);   // best possible name
                }
                else if (p.type == PieceType.Villager && villager == null)
                {
                    string v = GameText.VillagerDisplayName(p as Villager);
                    if (!string.IsNullOrEmpty(v)) villager = v;
                }
                else if (p.type == PieceType.WarpPoint) postbox = "postbox";
                else if (p.type == PieceType.Campfire) campfire = "campfire";
                else if (p.type == PieceType.Monument) monument = "monument";
            }

            return villager ?? monument ?? postbox ?? campfire;
        }

        /// <summary>"BiomeSwampMushrooms2" -> "swamp mushrooms".</summary>
        internal static string BiomeName(Island island)
        {
            var biome = island.biome;
            if (biome == null) return null;

            string n = biome.name;
            if (string.IsNullOrEmpty(n)) return null;
            if (n.StartsWith("Biome")) n = n.Substring(5);

            var sb = new StringBuilder();
            for (int i = 0; i < n.Length; i++)
            {
                char ch = n[i];
                if (char.IsDigit(ch)) continue;
                if (char.IsUpper(ch) && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString().Trim();
        }

        /// <summary>Exhibit titles ship in capitals; sentence case reads better aloud.</summary>
        private static string Tidy(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.Trim();
            bool allCaps = true;
            foreach (char ch in t) if (char.IsLower(ch)) { allCaps = false; break; }
            if (!allCaps) return t;
            return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
        }

        /// <summary>A grid position, in the same coordinate space the island labels use.</summary>
        internal static string Coords(Vector3i p)
        {
            int x = p.x, z = p.z;
            if (Cfg.UseGameCoordinateOrigin.Value) { x += OriginX; z += OriginZ; }
            return string.Format(CultureInfo.InvariantCulture, "{0}, {1}", x, z);
        }

        internal static string Compass(Vector3i dir)
        {
            if (dir.z > 0) return "north";
            if (dir.z < 0) return "south";
            if (dir.x > 0) return "east";
            if (dir.x < 0) return "west";
            return "nowhere";
        }
    }
}
