using System.Collections.Generic;

namespace AMEAccess.Game
{
    /// <summary>
    /// Which island each warp destination belongs to, for the whole world.
    ///
    /// The warp map shows postboxes on islands that are nowhere near you and are not loaded, so
    /// asking the live board which island a destination sits on returns nothing for almost all
    /// of them. That is why the map could hardly ever say whether a destination was on the main
    /// route: the answer needs an island, and there was no island to be had.
    ///
    /// The game has the same problem and solves it from data rather than from loaded objects.
    /// WarpMode.CreateWarpButtons walks LoadLevels.serializedWorld.serializedIslands - every
    /// island in the game, loaded or not - finds each one's warp point pieces, and builds the
    /// buttons from those positions. This does the same walk once and keeps the answers, so
    /// every destination can be matched to its island by position.
    /// </summary>
    internal static class WorldIndex
    {
        private static readonly Dictionary<long, SerializedIsland> ByWarpPoint =
            new Dictionary<long, SerializedIsland>();

        // Where every island in the game sits, whether loaded or not. Needed because the route
        // the game wants you to take can point at an island with no postbox, which can never
        // appear on the warp map - and saying only "no main route" there is useless when the
        // game knows perfectly well where you should go next.
        private static readonly Dictionary<int, Vector3i> IslandCentre = new Dictionary<int, Vector3i>();
        private static readonly Dictionary<int, SerializedIsland> IslandByUid =
            new Dictionary<int, SerializedIsland>();

        private static readonly List<Vector3i> WarpPositions = new List<Vector3i>();

        private static bool _built;

        internal static int Count => ByWarpPoint.Count;

        private static long Key(Vector3i p) => ((long)p.x << 32) ^ (uint)p.z;

        internal static void Forget() { ByWarpPoint.Clear(); _built = false; }

        /// <summary>Walk the world data once. Safe to call repeatedly.</summary>
        internal static void Build()
        {
            if (_built) return;
            _built = true;
            ByWarpPoint.Clear();
            IslandCentre.Clear();
            IslandByUid.Clear();
            WarpPositions.Clear();

            var sim = Refs.Sim;
            if (sim == null || sim.levelLoader == null) return;

            var world = sim.levelLoader.serializedWorld;
            if (world == null) return;

            foreach (var isl in world.serializedIslands)
            {
                if (isl == null || isl.serializedPieces == null) continue;

                IslandByUid[isl.id.uid] = isl;

                bool any = false;
                int minX = 0, maxX = 0, minZ = 0, maxZ = 0;

                foreach (var sp in isl.serializedPieces)
                {
                    if (sp == null || sp.prefab == null) continue;

                    // The stored piece state is in island-local space; the island turns it into
                    // world space, exactly as the game does when it places the buttons.
                    var state = isl.TransformPieceState(sp.pieceState);
                    var pos = state.position;

                    if (!any) { minX = maxX = pos.x; minZ = maxZ = pos.z; any = true; }
                    else
                    {
                        if (pos.x < minX) minX = pos.x;
                        if (pos.x > maxX) maxX = pos.x;
                        if (pos.z < minZ) minZ = pos.z;
                        if (pos.z > maxZ) maxZ = pos.z;
                    }

                    var piece = sp.prefab.GetComponent<Piece>();
                    if (piece != null && piece.type == PieceType.WarpPoint)
                    {
                        ByWarpPoint[Key(pos)] = isl;
                        WarpPositions.Add(pos);
                    }
                }

                if (any)
                    IslandCentre[isl.id.uid] = new Vector3i((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
            }
        }

        /// <summary>Every postbox in the game, loaded or not.</summary>
        internal static List<Vector3i> AllWarpPoints() { Build(); return WarpPositions; }

        /// <summary>Where an island sits in the world, by its uid.</summary>
        internal static bool CentreOf(int uid, out Vector3i centre)
        {
            Build();
            return IslandCentre.TryGetValue(uid, out centre);
        }

        /// <summary>An island by its uid, loaded or not.</summary>
        internal static SerializedIsland ById(int uid)
        {
            Build();
            SerializedIsland isl;
            return IslandByUid.TryGetValue(uid, out isl) ? isl : null;
        }

        /// <summary>The island a warp destination stands on, or null.</summary>
        internal static SerializedIsland IslandAt(Vector3i pos)
        {
            Build();
            SerializedIsland isl;
            return ByWarpPoint.TryGetValue(Key(pos), out isl) ? isl : null;
        }
    }
}
