using System.Collections.Generic;
using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Wide-area information.
    ///
    /// A sighted player takes in the whole island in one glance: how big it is, where the trees
    /// are, which way the logs are lying, where the shoreline is, what other islands are within
    /// sight. None of that is a solution — it is just what is on the screen. Withholding it
    /// would not make the game "fairer", it would make it unplayable. So this class gives as
    /// much of it as possible.
    ///
    /// The one thing it respects is fog. A sighted player cannot see through the fog of war
    /// either, so pieces reporting FogOcclusionState.OccludedByFog are skipped unless the
    /// player turns that off.
    /// </summary>
    internal static class Survey
    {
        private static readonly List<Piece> Buffer = new List<Piece>();
        private static int _cycleIndex = -1;
        private static readonly List<Piece> Cycle = new List<Piece>();

        /// <summary>
        /// Write every piece near you to the log, with the reason it was kept or dropped.
        ///
        /// When something on screen does not appear in the survey there is no way to reason it
        /// out from the binaries - it depends on that piece's live state. This prints the lot:
        /// type, position, which island the game thinks it belongs to, whether it is hidden or
        /// fogged, and which filter rejected it.
        /// </summary>
        internal static void Dump(BepInEx.Logging.ManualLogSource log, int radius)
        {
            var island = Refs.CurrentIsland;
            var me = Refs.PlayerPos;
            var all = Piece.all;

            NotablePieces();   // establishes the scan area used by the verdicts below

            log.LogInfo("--- pieces within " + radius + " tiles ---");
            log.LogInfo("island: " + (island == null ? "none" : Naming.IslandName(island))
                        + "   player: " + me.x + "," + me.y + "," + me.z
                        + "   IslandOnly=" + IslandOnly + "   Piece.all=" + (all == null ? 0 : all.Count));

            if (all == null) { log.LogInfo("--- end ---"); return; }

            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;

                var pos = p.position;
                if (Manhattan(me, pos) > radius) continue;

                string verdict;
                if (p.hidden) verdict = "DROPPED: hidden";
                else if (Cfg.RespectFog.Value && p.island != island
                         && p.fogOcclusionState == Piece.FogOcclusionState.OccludedByFog) verdict = "DROPPED: fogged";
                else if (Interest(p) < 50) verdict = "DROPPED: interest " + Interest(p) + " below 50";
                else if (IslandOnly && p.island != null && p.island != island)
                    verdict = "DROPPED: other island";
                else if (IslandOnly && p.island == null && !OnIslandFootprint(pos))
                    verdict = "DROPPED: unattributed and off this island";
                else verdict = "kept";

                string extra = "";
                var lm = p as Landmark;
                if (lm != null)
                    extra = "  kind=" + Landmarks.KindOf(lm)
                          + " behaviour=" + lm.interactionBehaviour
                          + " prefab=" + (Landmarks.PrefabName(lm) ?? "-");

                log.LogInfo(string.Format(
                    "  {0,-14} at {1},{2},{3}  island={4}  hidden={5} fog={6} physical={7} interest={8}  {9}{10}",
                    p.type, pos.x, pos.y, pos.z,
                    p.island == null ? "null" : Naming.IslandName(p.island),
                    p.hidden, p.fogOcclusionState, p.IsPhysical(), Interest(p), verdict, extra));
                shown++;
            }

            log.LogInfo("listed " + shown + " pieces. Survey returns " + NotablePieces().Count
                        + ", item cycle holds " + Cycle.Count + ".");
            log.LogInfo("--- end pieces ---");
        }

        // -------------------------------------------------------------------------
        // relative position
        // -------------------------------------------------------------------------

        /// <summary>
        /// "4 north, 2 east". Grid-relative wording beats clock bearings here, because the
        /// player moves in exactly these units.
        /// </summary>
        internal static string Relative(Vector3i from, Vector3i to)
        {
            int dx = to.x - from.x;
            int dz = to.z - from.z;
            if (dx == 0 && dz == 0) return "here";

            var sb = new StringBuilder();
            if (dz > 0) sb.Append(dz).Append(" north");
            else if (dz < 0) sb.Append(-dz).Append(" south");

            if (dx != 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                if (dx > 0) sb.Append(dx).Append(" east");
                else sb.Append(-dx).Append(" west");
            }
            return sb.ToString();
        }

        internal static int Manhattan(Vector3i a, Vector3i b)
        {
            int dx = a.x - b.x; if (dx < 0) dx = -dx;
            int dz = a.z - b.z; if (dz < 0) dz = -dz;
            return dx + dz;
        }

        // -------------------------------------------------------------------------
        // column contents
        // -------------------------------------------------------------------------

        /// <summary>
        /// Everything standing in one grid column, ignoring height. This is more reliable than
        /// probing a single Y, because trees and standing logs occupy several cells upward.
        /// </summary>
        internal static void Column(Vector3i cell, List<Piece> into)
        {
            into.Clear();
            var board = Refs.Board;
            if (board == null) return;

            var at = new Vector3i(cell.x, 0, cell.z);
            var bounds = new Boundsi(at, at);

            var found = board.GetPhysicalPiecesIntersectingFlat(bounds);
            if (found == null) return;

            for (int i = 0; i < found.Count; i++)
            {
                var p = found[i];
                if (p == null) continue;
                if (!Visible(p)) continue;
                into.Add(p);
            }
        }

        /// <summary>Would a sighted player be able to see this piece right now?</summary>
        internal static bool Visible(Piece p)
        {
            if (p == null) return false;
            if (p.hidden) return false;
            if (!Cfg.RespectFog.Value) return true;
            return p.fogOcclusionState != Piece.FogOcclusionState.OccludedByFog;
        }

        /// <summary>
        /// Rank pieces so a column reads the way it looks: you notice the tree, not the dirt
        /// under it.
        /// </summary>
        private static bool InScanArea(Vector3i pos)
        {
            return pos.x >= _scanMinX && pos.x <= _scanMaxX
                && pos.z >= _scanMinZ && pos.z <= _scanMaxZ;
        }

        private static bool OnIslandFootprint(Vector3i pos)
        {
            return pos.x >= _islMinX && pos.x <= _islMaxX
                && pos.z >= _islMinZ && pos.z <= _islMaxZ;
        }

        /// <summary>Is this piece worth listing at all? Used to keep numbering off scenery.</summary>
        internal static bool IsNotable(Piece p) => p != null && Interest(p) >= 50;

        /// <summary>Is this position on the island you are standing on?</summary>
        internal static bool OnCurrentIslandFootprint(Vector3i pos) => OnIslandFootprint(pos);

        /// <summary>The scope the last scan ran under, so callers can tell when it changed.</summary>
        internal static bool LastScopeIslandOnly => IslandOnly;

        private static int Interest(Piece p)
        {
            switch (p.type)
            {
                case PieceType.Tree: return 100;
                case PieceType.Log: return 95;
                case PieceType.Raft: return 94;
                case PieceType.Villager: return 90;
                case PieceType.Landmark: return Landmarks.Interest(Landmarks.KindOf(p as Landmark));
                case PieceType.Monument: return 86;
                case PieceType.Campfire: return 84;
                case PieceType.WarpPoint: return 82;
                case PieceType.Obstacle: return 70;
                case PieceType.Rock: return 65;
                case PieceType.TreeStump: return 60;
                case PieceType.Ramp: return 55;
                // Spawn points are invisible markers for where you arrive by raft or warp.
                // Nothing is drawn for them, so listing them as "landing spot" put an object in
                // the survey that a sighted player cannot see and cannot interact with.
                case PieceType.ActiveSpawnPoint:
                case PieceType.PassiveSpawnPoint: return 5;
                case PieceType.Caption: return 87;
                case PieceType.ShallowWater: return 20;
                case PieceType.Land: return 10;
                default: return 1;
            }
        }

        /// <summary>The single most notable thing in a column, or null for open water.</summary>
        internal static Piece TopOfColumn(Vector3i cell)
        {
            Column(cell, Buffer);
            Piece best = null;
            int bestScore = -1;
            for (int i = 0; i < Buffer.Count; i++)
            {
                int s = Interest(Buffer[i]);
                if (s > bestScore) { bestScore = s; best = Buffer[i]; }
            }
            return best;
        }

        // -------------------------------------------------------------------------
        // ray scan
        // -------------------------------------------------------------------------

        /// <summary>
        /// Look along one direction and report what you see, collapsing runs of the same thing.
        /// "land 3, tree, land 2, water" is roughly what the eye reports.
        /// </summary>
        internal static string Ray(Vector3i dir, int maxDistance)
        {
            var origin = Refs.PlayerPos;
            var sb = new StringBuilder();

            string runName = null;
            int runLength = 0;
            int emitted = 0;

            for (int d = 1; d <= maxDistance; d++)
            {
                var cell = origin + dir * d;
                var top = TopOfColumn(cell);
                string name = top == null ? "water" : Describe.PieceName(top);

                if (name == runName) { runLength++; continue; }

                if (runName != null)
                {
                    Append(sb, runName, runLength);
                    emitted++;
                    // Once we hit open water, everything past it is the same story.
                    if (runName == "water") return sb.ToString();
                    if (emitted >= Cfg.RayRuns.Value) return sb.Append(", more").ToString();
                }

                runName = name;
                runLength = 1;
            }

            if (runName != null) Append(sb, runName, runLength);
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }

        private static void Append(StringBuilder sb, string name, int count)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(name);
            if (count > 1) sb.Append(' ').Append(count);
        }

        /// <summary>Ray scan in all four directions.</summary>
        internal static string Scan()
        {
            var sb = new StringBuilder();
            foreach (var d in Describe.Cardinals)
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(Naming.Compass(d)).Append(": ").Append(Ray(d, Cfg.RayDistance.Value));
            }
            return sb.Append('.').ToString();
        }

        // -------------------------------------------------------------------------
        // island survey
        // -------------------------------------------------------------------------

        /// <summary>
        /// Every notable piece on and around the current island. This is the audible equivalent
        /// of looking at the island.
        /// </summary>
        /// <summary>
        /// When true, only pieces belonging to the island you are standing on are listed.
        /// Piece.island is set by the game, so this is its own notion of belonging, not a
        /// guess from coordinates - a log floating just offshore belongs to no island and
        /// drops out.
        /// </summary>
        internal static bool IslandOnly;

        // The area the current scan covers, so pieces the game does not attribute to any island
        // can still be judged as "here" or "not here".
        private static int _scanMinX, _scanMaxX, _scanMinZ, _scanMaxZ;

        // The island's own footprint, kept apart from the wider player radius. Pieces the game
        // does not attribute to an island are judged against THIS, not the radius, so a
        // neighbouring island's exhibits do not pile into your survey.
        private static int _islMinX, _islMaxX, _islMinZ, _islMaxZ;

        internal static List<Piece> NotablePieces()
        {
            var result = new List<Piece>();
            var island = Refs.CurrentIsland;
            if (island == null || island.board == null) return result;

            // Walk Piece.all rather than the board tree.
            //
            // Board.GetPieces<T>() only reaches this board and its DIRECT children, and
            // GetPhysicalPiecesIntersectingFlat only sees pieces flagged physical. Between them
            // they missed exhibits sitting on a nested board or with the physical flag off -
            // which is how a plinth on the island you were standing on never appeared in the
            // survey at all. Piece.all is the game's own list of every live piece, so nothing
            // can hide from it.
            var all = Piece.all;
            if (all == null) return result;

            var land = island.board.CalculateLandBounds();
            int pad = Cfg.SurveyPadding.Value;
            int minX = land.min.x - pad, maxX = land.max.x + pad;
            int minZ = land.min.z - pad, maxZ = land.max.z + pad;

            // Also take everything within a plain radius of you, not just the island's land
            // bounds. Some things stand on structures whose footprint is not part of the
            // island's land - museum plinths being the obvious case - so a bounds-only scan
            // walks straight past an exhibit you are standing next to.
            var me = Refs.PlayerPos;
            int rad = Cfg.SurveyRadius.Value;
            if (me.x - rad < minX) minX = me.x - rad;
            if (me.x + rad > maxX) maxX = me.x + rad;
            if (me.z - rad < minZ) minZ = me.z - rad;
            if (me.z + rad > maxZ) maxZ = me.z + rad;

            _scanMinX = minX; _scanMaxX = maxX;
            _scanMinZ = minZ; _scanMaxZ = maxZ;

            _islMinX = land.min.x - 2; _islMaxX = land.max.x + 2;
            _islMinZ = land.min.z - 2; _islMaxZ = land.max.z + 2;

            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;

                var pos = p.position;
                if (pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ) continue;

                Consider(p, island, result);
            }

            var origin = Refs.PlayerPos;
            result.Sort((a, b) => Manhattan(origin, a.position).CompareTo(Manhattan(origin, b.position)));
            return result;
        }

        private static void Consider(Piece p, Island island, List<Piece> into)
        {
            if (p == null) return;

            // Fog hides distant islands, but you can see what is on the one you are standing on.
            // Applying the fog test there could hide an exhibit at your feet.
            // Exhibits are not attached to an island.
            //
            // Landmark pieces come back with Piece.island == null - the game does not attribute
            // them to any island at all. The island-only filter was therefore throwing away
            // every exhibit in the game, which is why they never appeared in the survey and had
            // to be found by walking into them. For an unattributed piece, judge it by position
            // instead: if it is inside the area being scanned, it is here.
            bool unattributed = p.island == null;
            bool onThisIsland = unattributed ? OnIslandFootprint(p.position) : p.island == island;

            if (!onThisIsland && !Visible(p)) return;
            if (onThisIsland && p.hidden) return;
            if (Interest(p) < 50) return;              // skip plain land / shallow water
            if (IslandOnly && !onThisIsland) return;
            if (into.Contains(p)) return;              // the two passes can overlap
            into.Add(p);
        }

        /// <summary>
        /// A counted summary. Short on purpose.
        ///
        /// One line, counts only. It gives the shape of the island; the category and item keys
        /// step through the objects themselves at your own pace.
        /// </summary>
        internal static string IslandSurvey()
        {
            var pieces = NotablePieces();
            if (pieces.Count == 0)
                return IslandOnly ? "Nothing on this island." : "Nothing notable in sight.";

            var counts = new Dictionary<string, int>();
            foreach (var p in pieces)
            {
                string k = CategoryName(p);
                int n; counts.TryGetValue(k, out n);
                counts[k] = n + 1;
            }

            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in counts)
            {
                if (!first) sb.Append(", ");
                sb.Append(kv.Value).Append(' ').Append(kv.Value == 1 ? kv.Key : Plural(kv.Key));
                first = false;
            }
            sb.Append('.');

            sb.Append(IslandOnly ? " This island only." : " Including the water around.");

            // Counts only, deliberately. Naming each object with its bearing made this the
            // longest thing the mod says, and it duplicated the item keys, which step through
            // exactly the same list at your own pace.
            if (_hintsGiven < 3)
            {
                _hintsGiven++;
                sb.Append(" Page down for categories, brackets for items.");
            }

            return sb.ToString();
        }

        private static int _hintsGiven;

        internal static string CategoryOf(Piece p) => CategoryName(p);

        private static string CategoryName(Piece p)
        {
            switch (p.type)
            {
                case PieceType.Tree: return "tree";
                case PieceType.Log: return "log";
                case PieceType.Raft: return "raft";
                case PieceType.Rock: return "rock";
                case PieceType.Obstacle: return "boulder";
                case PieceType.TreeStump: return "stump";
                case PieceType.Ramp: return "ramp";
                case PieceType.Villager: return "villager";
                case PieceType.Landmark: return Landmarks.Category(Landmarks.KindOf(p as Landmark));
                case PieceType.Caption: return "plaque";
                case PieceType.Monument: return "monument";
                case PieceType.Campfire: return "campfire";
                case PieceType.WarpPoint: return "postbox";
                default: return "object";
            }
        }

        private static string Plural(string s)
        {
            if (s.EndsWith("s") || s.EndsWith("x")) return s + "es";
            return s + "s";
        }

        // -------------------------------------------------------------------------
        // cycling through objects
        // -------------------------------------------------------------------------

        // Two axes: Page Up and Page Down choose a category, the bracket keys step through the
        // objects inside it. Nothing is ever read out as a long list you have to remember.
        private static readonly List<string> Categories = new List<string>();
        private static int _catIndex;

        private static Island _cycleIsland;
        private static bool _cycleScope;

        /// <summary>
        /// Make sure the list the item keys step through matches what T just described.
        ///
        /// The cycle was only rebuilt when you pressed the survey key or toggled scope, so
        /// stepping with the bracket keys after walking to another island, or straight after
        /// toggling, could walk an old and much longer list. Checking the island and the scope
        /// before every step removes the possibility.
        /// </summary>
        private static void EnsureCycleFresh()
        {
            var isl = Refs.CurrentIsland;
            if (Cycle.Count == 0 || isl != _cycleIsland || IslandOnly != _cycleScope)
            {
                RebuildCycle();
                ApplyCategory();
            }
        }

        internal static void RebuildCycle()
        {
            _cycleIsland = Refs.CurrentIsland;
            _cycleScope = IslandOnly;

            Cycle.Clear();
            Cycle.AddRange(NotablePieces());
            _cycleIndex = -1;

            // "everything" always sits at the front so you can get back to the full list.
            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var p in Cycle)
            {
                string c = CategoryName(p);
                if (!Categories.Contains(c)) Categories.Add(c);
            }
            _catIndex = 0;
        }

        private const string AllCategories = "everything";

        /// <summary>Switch between "this island only" and "everything in sight".</summary>
        internal static string ToggleScope()
        {
            IslandOnly = !IslandOnly;
            Numbering.Forget();     // numbers are per scope as well as per island
            RebuildCycle();
            // IslandSurvey already ends by naming the scope, so it is not repeated here.
            return IslandSurvey();
        }

        private static string CurrentCategory
            => (_catIndex >= 0 && _catIndex < Categories.Count) ? Categories[_catIndex] : AllCategories;

        /// <summary>Rebuild the working list from whichever category is selected.</summary>
        private static void ApplyCategory()
        {
            var all = NotablePieces();
            Cycle.Clear();

            string cat = CurrentCategory;
            foreach (var p in all)
                if (cat == AllCategories || CategoryName(p) == cat) Cycle.Add(p);

            _cycleIndex = -1;
        }

        /// <summary>Move to the next or previous category and say what is in it.</summary>
        internal static string CycleCategory(int step)
        {
            EnsureCycleFresh();
            if (Categories.Count == 0) RebuildCycle();
            if (Categories.Count <= 1) return "Nothing notable in sight.";

            _catIndex += step;
            if (_catIndex >= Categories.Count) _catIndex = 0;
            if (_catIndex < 0) _catIndex = Categories.Count - 1;

            ApplyCategory();

            string cat = CurrentCategory;
            string name = Cycle.Count == 1 ? cat : Plural(cat);
            if (cat == AllCategories) name = cat;

            return (_catIndex + 1) + " of " + Categories.Count + ". " +
                   Cap(name) + ", " + Cycle.Count + ".";
        }

        internal static string CategorySummary()
        {
            if (Categories.Count == 0) RebuildCycle();
            return Cap(CurrentCategory) + ", " + Cycle.Count + ".";
        }

        internal static string CycleNext(int step)
        {
            EnsureCycleFresh();
            if (Cycle.Count == 0) return "Nothing in this category.";

            _cycleIndex += step;
            if (_cycleIndex >= Cycle.Count) _cycleIndex = 0;
            if (_cycleIndex < 0) _cycleIndex = Cycle.Count - 1;

            var p = Cycle[_cycleIndex];
            if (p == null) { Cycle.RemoveAt(_cycleIndex); return CycleNext(0); }

            return (_cycleIndex + 1) + " of " + Cycle.Count + ". " +
                   Describe.PieceName(p) + ", " + Relative(Refs.PlayerPos, p.position) + ".";
        }

        internal static Piece Selected
        {
            get
            {
                if (_cycleIndex < 0 || _cycleIndex >= Cycle.Count) return null;
                return Cycle[_cycleIndex];
            }
        }

        private static string Cap(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // -------------------------------------------------------------------------
        // other islands
        // -------------------------------------------------------------------------

        /// <summary>
        /// Islands within sight, with bearing and distance, and whether you can currently walk
        /// there. Both are things a sighted player can see: the island is on screen, and so is
        /// the log bridging to it.
        /// </summary>
        internal static string NearbyIslands()
        {
            var world = Refs.World;
            var here = Refs.CurrentIsland;
            var origin = Refs.PlayerPos;
            if (world == null) return "No world loaded.";

            int r = Cfg.IslandSearchRadius.Value;
            var min = new Vector3i(origin.x - r, 0, origin.z - r);
            var max = new Vector3i(origin.x + r, 0, origin.z + r);

            var found = new List<Island>();
            foreach (var isl in world.GetIslandsIntersectingFlat(new Boundsi(min, max)))
            {
                if (isl == null || isl == here) continue;
                if (isl.isHiddenIsland) continue;
                found.Add(isl);
            }

            if (found.Count == 0) return "No other islands in range.";

            // Which of them can you actually reach on foot right now?
            var reachable = new HashSet<Island>();
            try
            {
                var connected = PathFinding.PathFinding.ConnectedIslands(origin);
                if (connected != null)
                    foreach (var isl in connected) if (isl != null) reachable.Add(isl);
            }
            catch { /* pathfinding is best effort; never let it break the readout */ }
            finally
            {
                // The A* scratch state is shared with the game's own mouse pathing, so put it back.
                try { PathFinding.PathFinding.Reset(); } catch { }
            }

            found.Sort((a, b) => Centre(a, origin).CompareTo(Centre(b, origin)));

            var sb = new StringBuilder();
            int listed = 0;
            foreach (var isl in found)
            {
                if (listed >= Cfg.IslandListCount.Value) break;
                var c = CentrePoint(isl);
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Naming.IslandLabel(isl, false))
                  .Append(", ").Append(Relative(origin, c));
                if (reachable.Contains(isl)) sb.Append(", reachable");
                sb.Append('.');
                listed++;
            }
            if (found.Count > listed) sb.Append(' ').Append(found.Count - listed).Append(" more.");
            return sb.ToString();
        }

        /// <summary>
        /// Where to go next: the nearest island you have not set foot on.
        ///
        /// Hunting for the next island by sweeping water tiles is slow and a sighted player
        /// never has to do it - unexplored islands are simply visible across the water. This
        /// gives the nearest one, how far it is, and whether you can already walk there.
        /// "Reachable" means a bridge already exists; if it says not reachable, that gap is the
        /// puzzle.
        /// </summary>
        internal static string NearestUnvisited()
        {
            var world = Refs.World;
            var here = Refs.CurrentIsland;
            var progress = Refs.Progress;
            var origin = Refs.PlayerPos;
            if (world == null) return "No world loaded.";

            int r = Cfg.IslandSearchRadius.Value * 2;
            var min = new Vector3i(origin.x - r, 0, origin.z - r);
            var max = new Vector3i(origin.x + r, 0, origin.z + r);

            Boundsi hereBounds = default(Boundsi);
            bool haveHere = here != null && here.board != null;
            if (haveHere) hereBounds = here.board.CalculateLandBounds();

            Island best = null;
            int bestDist = int.MaxValue, bestGap = int.MaxValue;
            Vector3i bestPoint = origin;
            string bestGapDir = null;
            bool bestStraight = false;
            int unvisited = 0;

            foreach (var isl in world.GetIslandsIntersectingFlat(new Boundsi(min, max)))
            {
                if (isl == null || isl == here) continue;
                if (isl.isHiddenIsland) continue;
                if (isl.board == null) continue;

                // HasSteppedOn is the authoritative record of where you have actually been.
                // Island.GetVisited() is a per-island flag the game also uses for its own
                // display state, so it is not the same question.
                //
                // Note it is HasSteppedOn, not SteppedOn: SteppedOn returns void and RECORDS a
                // visit. Calling that here would have quietly marked every island it looked at
                // as already visited.
                if (progress != null && progress.HasSteppedOn(isl.id.uid)) continue;

                var b = isl.board.CalculateLandBounds();

                // Measure to the island's NEAREST SHORE, not its centre.
                //
                // Distance to the centre is what made this pick the wrong island: a large island
                // whose edge is three tiles away can have its middle twenty tiles away, so a
                // small distant island won instead. What you care about is how far the land is,
                // and then how wide the water is.
                var near = ClosestPointOn(b, origin);
                int dist = Manhattan(origin, near);
                int gap = int.MaxValue;
                string gapDir = null;
                bool straight = false;
                if (haveHere) straight = StraightGap(hereBounds, b, out gap, out gapDir);
                if (!straight) { gap = int.MaxValue; gapDir = null; }

                unvisited++;

                // Prefer the island that needs the shortest bridge; break ties by how far you
                // have to walk to reach the near shore.
                if (gap < bestGap || (gap == bestGap && dist < bestDist))
                {
                    bestGap = gap; bestDist = dist; best = isl; bestPoint = near;
                    bestGapDir = gapDir; bestStraight = straight;
                }
            }

            if (best == null)
                return "No unvisited islands within " + r + " tiles. Try a postbox to warp further out.";

            bool reachable = false;
            try
            {
                var connected = PathFinding.PathFinding.ConnectedIslands(origin);
                if (connected != null)
                    foreach (var isl in connected) if (isl == best) { reachable = true; break; }
            }
            catch { }
            finally { try { PathFinding.PathFinding.Reset(); } catch { } }

            var sb = new StringBuilder("Nearest unvisited island: ");
            sb.Append(Naming.IslandLabel(best, false)).Append(". ");
            sb.Append("Its shore is ").Append(Relative(origin, bestPoint)).Append(". ");

            if (reachable)
                sb.Append("You can walk there now.");
            else if (!bestStraight)
                sb.Append("It sits off the corner of this island, so there is no straight crossing to it from here.");
            else if (bestGap <= 0)
                sb.Append("Its land touches yours, but you cannot walk across yet.");
            else
            {
                sb.Append("Water gap ").Append(bestGap).Append(bestGap == 1 ? " tile" : " tiles");
                if (bestGapDir != null) sb.Append(" to the ").Append(bestGapDir);
                sb.Append(", so you need a log ").Append(bestGap).Append(" long or more.");
            }

            if (Cfg.AnnounceUnvisitedCount.Value && unvisited > 1)
                sb.Append(' ').Append(unvisited - 1).Append(" other unvisited nearby.");
            return sb.ToString();
        }

        /// <summary>The point on a bounds closest to a position, ignoring height.</summary>
        private static Vector3i ClosestPointOn(Boundsi b, Vector3i p)
        {
            int x = p.x < b.min.x ? b.min.x : (p.x > b.max.x ? b.max.x : p.x);
            int z = p.z < b.min.z ? b.min.z : (p.z > b.max.z ? b.max.z : p.z);
            return new Vector3i(x, b.min.y, z);
        }

        /// <summary>
        /// The straight water crossing between two islands, if there is one.
        ///
        /// A log bridges along ONE axis. Adding the north-south gap to the east-west gap, which
        /// is what this used to do, gives a number no log can span: an island sitting diagonally
        /// off your corner has no straight crossing at all, however small both gaps are. So the
        /// islands must overlap on one axis for a crossing to exist, and the gap is measured
        /// along the other.
        /// </summary>
        private static bool StraightGap(Boundsi a, Boundsi b, out int gap, out string dir)
        {
            gap = int.MaxValue; dir = null;

            bool overlapZ = a.min.z <= b.max.z && b.min.z <= a.max.z;
            bool overlapX = a.min.x <= b.max.x && b.min.x <= a.max.x;

            if (overlapZ)
            {
                if (b.min.x > a.max.x) { gap = b.min.x - a.max.x - 1; dir = "east"; }
                else if (a.min.x > b.max.x) { gap = a.min.x - b.max.x - 1; dir = "west"; }
                else gap = 0;
                if (gap < 0) gap = 0;
                return true;
            }

            if (overlapX)
            {
                if (b.min.z > a.max.z) { gap = b.min.z - a.max.z - 1; dir = "north"; }
                else if (a.min.z > b.max.z) { gap = a.min.z - b.max.z - 1; dir = "south"; }
                else gap = 0;
                if (gap < 0) gap = 0;
                return true;
            }

            return false;   // diagonal: nothing to bridge straight across
        }

        private static Vector3i CentrePoint(Island isl)
        {
            if (isl.board == null) return Vector3i.zero;
            var b = isl.board.CalculateLandBounds();
            return new Vector3i((b.min.x + b.max.x) / 2, 0, (b.min.z + b.max.z) / 2);
        }

        private static int Centre(Island isl, Vector3i from) => Manhattan(from, CentrePoint(isl));
    }
}
