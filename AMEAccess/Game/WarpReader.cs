using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// Makes the warp map readable.
    ///
    /// The warp map is the screen you get from a postbox, and it is how the world is actually
    /// navigated once you are past the first few islands. It is drawn as unlabelled icons at
    /// world positions, with colour carrying the meaning: markers for exhibits and friends, and
    /// the footprints a sighted player reads as "you can probably solve this" and "this is the
    /// main route". None of that has any text, so without this the map is blank to a screen
    /// reader and postboxes are unusable.
    ///
    /// WarpMode keeps nearly everything private, so most of this is reflection. Only
    /// WarpMode.currentWarpMode and the button's position and isPriority are public. Every
    /// lookup is cached and guarded, and a failure degrades to "less detail" rather than
    /// breaking the screen.
    /// </summary>
    internal static class WarpReader
    {
        private static bool _inWarp;

        // Our own cursor over the destinations.
        //
        // The map has no button-to-button navigation: UpdateWarpButtonSelection picks whichever
        // button is nearest the camera, so the arrow keys pan and the game's selection only
        // changes when a different button happens to become closest. Following that selection
        // means long silences and no way to reach a destination deliberately. So the mod keeps
        // its own ordered cursor and activates a destination through the button's own onClick,
        // which is exactly what a mouse click does.

        private static FieldInfo _fButtons, _fCritical, _fFriends, _fProgress, _fEntrance, _fPriority;
        private static bool _reflected;


        // ---------------------------------------------------------------- setup

        private static void Reflect()
        {
            if (_reflected) return;
            _reflected = true;

            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var t = typeof(WarpMode);
                _fButtons = t.GetField("warpExitButtons", F);
                _fCritical = t.GetField("criticalPath", F);
                _fFriends = t.GetField("friendHints", F);
                _fProgress = t.GetField("progress", F);
                _fEntrance = t.GetField("entrance", F);
                _fPriority = t.GetField("highestPriorityButton", F);
            }
            catch { }
        }

        internal static bool Active
        {
            get
            {
                var w = WarpMode.currentWarpMode;
                return w != null;
            }
        }

        private static WarpMode _sceneWarp;

        /// <summary>
        /// The WarpMode component, whether or not the map is open.
        ///
        /// currentWarpMode is only set while the map is showing, but the component sits in the
        /// scene the whole time and holds the critical path. Finding it lets the route be read
        /// while you are walking about, which is when you actually want to know where to go.
        /// </summary>
        private static WarpMode Instance
        {
            get
            {
                var w = WarpMode.currentWarpMode;
                if (w != null) return w;
                if (_sceneWarp == null) _sceneWarp = UnityEngine.Object.FindObjectOfType<WarpMode>();
                return _sceneWarp;
            }
        }

        private static List<WarpModeButton> Buttons()
        {
            Reflect();
            var w = Instance;
            if (w == null || _fButtons == null) return null;
            try { return _fButtons.GetValue(w) as List<WarpModeButton>; }
            catch { return null; }
        }

        private static WarpModeButton Recommended()
        {
            Reflect();
            var w = Instance;
            if (w == null || _fPriority == null) return null;
            try { return _fPriority.GetValue(w) as WarpModeButton; }
            catch { return null; }
        }

        /// <summary>Island uids on the game's own critical path - the green route.</summary>
        private static List<int> CriticalIslands() { return UidsFrom(_fCritical, "CriticalPath"); }

        /// <summary>Island uids the game is pointing you at for friends.</summary>
        private static List<int> FriendIslands() { return UidsFrom(_fFriends, "FriendHints"); }

        private static readonly List<int> CritBuffer = new List<int>();
        private static readonly List<int> FriendBuffer = new List<int>();

        /// <summary>
        /// Both marker sets answer the same way: give them the profile and a buffer and they
        /// return island uids. They are different classes with no shared base, so the call is
        /// made by reflection rather than duplicating the method twice.
        /// </summary>
        private static List<int> UidsFrom(FieldInfo field, string what)
        {
            Reflect();
            var w = Instance;
            if (w == null || field == null || _fProgress == null) return null;

            try
            {
                var source = field.GetValue(w);
                var progress = _fProgress.GetValue(w) as Progress;
                if (source == null || progress == null) return null;

                var profile = progress.profile;
                if (profile == null) return null;

                var buffer = what == "CriticalPath" ? CritBuffer : FriendBuffer;
                buffer.Clear();

                var m = source.GetType().GetMethod("GetIslands",
                    new[] { typeof(Profiles.Profile), typeof(List<int>) });
                if (m == null) return null;

                return m.Invoke(source, new object[] { profile, buffer }) as List<int>;
            }
            catch { return null; }
        }

        /// <summary>
        /// The friend markers the game actually draws, rather than inferred from island
        /// membership.
        ///
        /// FriendMarkerData carries the marker's position and, usefully, singleIsland: true
        /// when the friend is on that island, false when the marker covers a group and only
        /// points the way. Saying "friend" for both would claim a monster is somewhere it is
        /// not.
        ///
        /// Only read when the game's own "Enable Friend Hints" setting is on, because that is
        /// when a sighted player sees the markers at all.
        /// </summary>
        private static List<FriendHints.FriendMarkerData> FriendMarkers()
        {
            Reflect();

            try { if (!global::Config.enableFriendHints) return null; }
            catch { return null; }

            var w = Instance;
            if (w == null || _fFriends == null || _fProgress == null) return null;

            try
            {
                var fh = _fFriends.GetValue(w) as FriendHints;
                var progress = _fProgress.GetValue(w) as Progress;
                if (fh == null || progress == null) return null;

                var profile = progress.profile;
                if (profile == null) return null;

                return fh.GetMarkerPositions(profile);
            }
            catch { return null; }
        }

        /// <summary>Is there a friend on this island, or only a marker pointing past it?</summary>
        private static bool FriendIsHere(List<FriendHints.FriendMarkerData> markers, Vector3i centre)
        {
            if (markers == null) return false;

            foreach (var m in markers)
            {
                if (!m.singleIsland) continue;   // FriendMarkerData is a struct, never null
                if (Near(m.position, centre)) return true;

                if (m.islandPositions != null)
                    foreach (var p in m.islandPositions)
                        if (Near(p, centre)) return true;
            }
            return false;
        }

        private static bool Near(UnityEngine.Vector3 a, Vector3i b)
        {
            int dx = Mathf.RoundToInt(a.x) - b.x;
            int dz = Mathf.RoundToInt(a.z) - b.z;
            if (dx < 0) dx = -dx;
            if (dz < 0) dz = -dz;
            return dx + dz <= 4;
        }

        private static Vector3i Origin()
        {
            Reflect();
            var w = Instance;
            if (w != null && _fEntrance != null)
            {
                try
                {
                    var wp = _fEntrance.GetValue(w) as WarpPoint;
                    if (wp != null) return wp.position;
                }
                catch { }
            }
            return Refs.PlayerPos;
        }

        // ---------------------------------------------------------------- reading

        /// <summary>What we know about one destination, worked out once per map.</summary>
        private struct Dest
        {
            public WarpModeButton Button;
            public Vector3i Position;
            public string Name;
            public bool Visited;
            public bool Critical;
            public bool Friend;        // flagged by the friend hints
            public bool FriendHere;    // and the friend is on this island, not further on
            public bool Suggested;
            public int Distance;       // from you
            public int RouteDist;      // from the nearest island the main route points at
        }

        private static readonly List<Dest> All = new List<Dest>();

        /// <summary>
        /// Work out every destination from the world data.
        ///
        /// This is the part that used to fail. Asking the live board which island a destination
        /// belonged to only worked for islands that happened to be loaded, which on a map of
        /// dozens of destinations is almost none of them - so "main route" could hardly ever be
        /// said, and the friend markers were not read at all. The world index answers for every
        /// destination whether its island is loaded or not.
        /// </summary>
        private static void BuildAll()
        {
            All.Clear();

            var buttons = Buttons();
            if (buttons == null) return;

            var origin = Origin();
            var critical = CriticalIslands();
            var friends = FriendIslands();
            var markers = FriendMarkers();
            var rec = Recommended();

            // With the game's friend hints switched off, a sighted player sees no markers, so
            // neither should you.
            bool friendHintsOn = true;
            try { friendHintsOn = global::Config.enableFriendHints; } catch { }

            Progress progress = Refs.Progress;
            Profiles.Profile profile = progress == null ? null : progress.profile;

            foreach (var b in buttons)
            {
                if (b == null) continue;

                var isl = WorldIndex.IslandAt(b.position);
                int uid = isl == null ? -1 : isl.id.uid;

                var d = new Dest
                {
                    Button = b,
                    Position = b.position,
                    Distance = Survey.Manhattan(origin, b.position),
                    Suggested = b.isPriority || (rec != null && rec == b),
                    Critical = uid >= 0 && critical != null && critical.Contains(uid),
                    Friend = friendHintsOn && uid >= 0 && friends != null && friends.Contains(uid),
                    Visited = isl != null && profile != null && profile.HasVisitedIsland(isl),
                    Name = NameFor(isl, b.position)
                };

                if (d.Friend)
                {
                    Vector3i centre;
                    d.FriendHere = WorldIndex.CentreOf(uid, out centre)
                                && FriendIsHere(markers, centre);
                }

                All.Add(d);
            }

            // How near each destination gets you to the route. The route usually points at
            // islands with no postbox, so "which postbox lands me closest" is the question that
            // actually has an answer.
            var routeCentres = new List<Vector3i>();
            if (critical != null)
                foreach (int uid in critical)
                {
                    Vector3i c;
                    if (WorldIndex.CentreOf(uid, out c)) routeCentres.Add(c);
                }

            for (int i = 0; i < All.Count; i++)
            {
                var d = All[i];
                d.RouteDist = int.MaxValue;
                foreach (var c in routeCentres)
                {
                    int dist = Survey.Manhattan(d.Position, c);
                    if (dist < d.RouteDist) d.RouteDist = dist;
                }
                All[i] = d;
            }

            All.Sort((x, y) => x.Distance.CompareTo(y.Distance));
        }

        /// <summary>
        /// A destination's name. An island that is not loaded has no board to measure, but its
        /// stored exhibit id still gives a real name, so most destinations can be called
        /// something better than a coordinate.
        /// </summary>
        private static string NameFor(SerializedIsland isl, Vector3i pos)
        {
            if (isl != null && !string.IsNullOrEmpty(isl.exhibitDescriptionId))
            {
                string t = GameText.ExhibitTitleById(isl.exhibitDescriptionId);
                if (!string.IsNullOrEmpty(t)) return t;
            }

            var sim = Refs.Sim;
            if (sim != null)
            {
                try
                {
                    var live = sim.GetIslandBeneathPosition(pos);
                    if (live != null) return Naming.IslandLabel(live, false);
                }
                catch { }
            }

            return "postbox at " + Naming.Coords(pos);
        }

        private static string Describe(Dest d) { return Describe(d, false); }

        private static string Describe(Dest d, bool withRouteDistance)
        {
            var sb = new StringBuilder(d.Name);
            sb.Append(". ").Append(Survey.Relative(Origin(), d.Position));
            sb.Append(d.Visited ? ", visited" : ", not visited");
            if (d.Critical) sb.Append(", main route");
            // A marker can sit on a group of islands and only point the way, so claiming a
            // friend is here when the game only said "somewhere over there" would be wrong.
            if (d.Friend) sb.Append(d.FriendHere ? ", friend here" : ", towards a friend");
            if (d.Suggested) sb.Append(", suggested");

            if (withRouteDistance && d.RouteDist != int.MaxValue)
                sb.Append(", ").Append(d.RouteDist)
                  .Append(d.RouteDist == 1 ? " tile from the main route" : " tiles from the main route");

            return sb.ToString();
        }

        /// <summary>Build the ordered destination list, nearest first.</summary>
        // ---------------------------------------------------------------- categories
        //
        // A map with dozens of destinations is unusable as one long list, so it is split the
        // same way the island survey is: one pair of keys chooses a category, another steps
        // through what is in it. Categories with nothing in them are left out, so paging never
        // lands on silence.
        private static readonly List<string> Categories = new List<string>();
        private static readonly List<Dest> Shown = new List<Dest>();
        private static int _catIndex;
        private static int _cursor = -1;

        private const string AllCats = "all";
        private const string RouteCat = "towards the main route";

        /// <summary>How many destinations the route group offers. More is just noise.</summary>
        private const int RouteGroupSize = 5;

        private static bool InCategory(Dest d, string cat)
        {
            switch (cat)
            {
                case "main route": return d.Critical;
                case "friends": return d.Friend;
                case "not visited": return !d.Visited;
                case "visited": return d.Visited;
                default: return true;
            }
        }

        private static void BuildCategories()
        {
            Categories.Clear();
            Categories.Add(AllCats);

            // The route group comes first after "all": it is the one that answers where to go.
            foreach (var d in All)
                if (d.RouteDist != int.MaxValue) { Categories.Add(RouteCat); break; }

            foreach (var name in new[] { "main route", "friends", "not visited", "visited" })
                foreach (var d in All)
                    if (InCategory(d, name)) { Categories.Add(name); break; }

            // Start in the route group when there is one, because "where do I go next" is the
            // reason the map was opened. Paging back reaches everything else.
            _catIndex = Categories.IndexOf(RouteCat);
            if (_catIndex < 0) _catIndex = 0;
            ApplyCategory();
        }

        private static void ApplyCategory()
        {
            string cat = (_catIndex >= 0 && _catIndex < Categories.Count) ? Categories[_catIndex] : AllCats;
            Shown.Clear();

            if (cat == RouteCat)
            {
                // Ordered by how close they get you to the route, not by how close they are to
                // you, and cut short: the sixth-best stepping stone is not worth stepping to.
                var sorted = new List<Dest>();
                foreach (var d in All) if (d.RouteDist != int.MaxValue) sorted.Add(d);
                sorted.Sort((x, y) => x.RouteDist.CompareTo(y.RouteDist));

                for (int i = 0; i < sorted.Count && i < RouteGroupSize; i++) Shown.Add(sorted[i]);
            }
            else
            {
                foreach (var d in All) if (InCategory(d, cat)) Shown.Add(d);
            }

            _cursor = -1;
        }

        internal static string CycleCategory(int step)
        {
            if (All.Count == 0) BuildAll();
            if (Categories.Count == 0) BuildCategories();
            if (Categories.Count <= 1) return "Only one group of destinations.";

            _catIndex += step;
            if (_catIndex >= Categories.Count) _catIndex = 0;
            if (_catIndex < 0) _catIndex = Categories.Count - 1;
            ApplyCategory();

            return (_catIndex + 1) + " of " + Categories.Count + ". "
                 + Cap(Categories[_catIndex]) + ", " + Shown.Count + ".";
        }

        /// <summary>Step through the destinations in the chosen category.</summary>
        internal static string Step(int direction)
        {
            if (All.Count == 0) { BuildAll(); BuildCategories(); }
            if (Shown.Count == 0) return "Nothing in this group.";

            _cursor += direction;
            if (_cursor >= Shown.Count) _cursor = 0;
            if (_cursor < 0) _cursor = Shown.Count - 1;

            return (_cursor + 1) + " of " + Shown.Count + ". "
                 + Describe(Shown[_cursor], InRouteGroup) + ".";
        }

        private static bool InRouteGroup =>
            _catIndex >= 0 && _catIndex < Categories.Count && Categories[_catIndex] == RouteCat;

        /// <summary>
        /// Travel to the destination under the cursor.
        ///
        /// The warp methods are private, but WarpModeButton.button is public, so invoking its
        /// click handler runs the same path a mouse click would.
        /// </summary>
        internal static string Activate()
        {
            if (_cursor < 0 || _cursor >= Shown.Count)
                return "No destination chosen. Step to one first.";

            var d = Shown[_cursor];
            if (d.Button == null || d.Button.button == null) return "That destination cannot be used.";

            try { d.Button.button.onClick.Invoke(); }
            catch (Exception e) { return "Could not travel there: " + e.GetType().Name + "."; }

            return "Travelling to " + d.Name + ".";
        }

        /// <summary>The whole map in one line, by group.</summary>
        internal static string List()
        {
            if (All.Count == 0) { BuildAll(); BuildCategories(); }
            if (All.Count == 0) return "No destinations on the map.";

            int crit = 0, friend = 0, unseen = 0;
            foreach (var d in All)
            {
                if (d.Critical) crit++;
                if (d.Friend) friend++;
                if (!d.Visited) unseen++;
            }

            var sb = new StringBuilder();
            sb.Append(All.Count).Append(" destinations. ");
            if (crit > 0) sb.Append(crit).Append(" on the main route. ");
            if (friend > 0)
                sb.Append(friend).Append(friend == 1 ? " marked for a friend. " : " marked for friends. ");
            sb.Append(unseen).Append(" not visited. ");

            string nearest = null;
            foreach (var d in All) if (d.Critical) { nearest = Describe(d); break; }
            if (nearest == null) foreach (var d in All) if (!d.Visited) { nearest = Describe(d); break; }
            if (nearest != null) sb.Append("Nearest worth going to: ").Append(nearest).Append(". ");

            // The route matters more than the count, and it is often not on this map at all.
            if (crit == 0) sb.Append(MainRoute()).Append(' ');

            sb.Append(Categories.Count > 1
                ? "Page down for groups, brackets for destinations."
                : "Brackets to step through them.");
            return sb.ToString();
        }

        /// <summary>
        /// Are you on the main route, and if not, where is it?
        ///
        /// Asked as a question about where you are standing, because that is the thing worth
        /// knowing and because the alternative was worse than useless: the route can name the
        /// island you are already on, and being told to travel to it reads as nonsense.
        /// </summary>
        internal static string RouteStatus()
        {
            var critical = CriticalIslands();
            if (critical == null) return "The main route could not be read.";
            if (critical.Count == 0)
                return "No main route right now. You have reached everything it points at so far.";

            var here = Refs.CurrentIsland;
            if (here != null)
            {
                int hereUid = -1;
                try { hereUid = here.id.uid; } catch { }

                if (hereUid >= 0 && critical.Contains(hereUid))
                    return critical.Count == 1
                        ? "You are on the main route, and this is the only island it points at."
                        : "You are on the main route. " + (critical.Count - 1)
                          + (critical.Count == 2 ? " other island is on it too." : " other islands are on it too.");
            }

            return "Not on the main route. " + MainRoute();
        }

        /// <summary>
        /// Where the game wants you to go next, even when you cannot warp there.
        ///
        /// The critical path is a route through islands, and most islands have no postbox, so a
        /// path several islands long can produce no warp markers at all. Saying "no main route"
        /// in that case is worse than useless: the game knows exactly where you should head. So
        /// name the island, say where it is, and name the destination that gets you nearest to
        /// it, which is the question you were really asking.
        /// </summary>
        internal static string MainRoute()
        {
            if (Active && All.Count == 0) { BuildAll(); BuildCategories(); }

            var critical = CriticalIslands();
            if (critical == null) return "The main route could not be read.";
            if (critical.Count == 0)
                return "No main route right now. You have reached everything it points at so far.";

            // Anything on it you can warp straight to is the simple case.
            var direct = new List<Dest>();
            foreach (var d in All) if (d.Critical) direct.Add(d);
            if (direct.Count > 0)
                return direct.Count + " on the main route. Nearest: " + Describe(direct[0]) + ".";

            var sb = new StringBuilder();
            sb.Append(critical.Count).Append(critical.Count == 1 ? " island" : " islands")
              .Append(" on the main route, none with a postbox, so you cannot warp straight there. ");

            var origin = Origin();

            // Skip any route island you are already standing on. Naming it and telling you how
            // to travel there is the thing that made this confusing in the first place.
            int hereUid = -1;
            var here = Refs.CurrentIsland;
            if (here != null) { try { hereUid = here.id.uid; } catch { } }

            int uid = -1;
            foreach (int candidate in critical)
                if (candidate != hereUid) { uid = candidate; break; }

            if (uid < 0) return "You are standing on the only island the main route points at.";

            Vector3i centre;
            if (!WorldIndex.CentreOf(uid, out centre))
                return sb.Append("Where it is could not be worked out.").ToString();

            var isl = WorldIndex.ById(uid);
            string name = NameFor(isl, centre);

            sb.Append("It is ").Append(name).Append(", ")
              .Append(Survey.Relative(origin, centre)).Append(" from here. ");

            // Which postbox lands you closest to it. Taken from the world index rather than the
            // map's buttons, so this answers while you are walking about as well.
            Vector3i bestPost = origin; int bestDist = int.MaxValue; bool found = false;
            foreach (var p in WorldIndex.AllWarpPoints())
            {
                int dist = Survey.Manhattan(p, centre);
                if (dist < bestDist) { bestDist = dist; bestPost = p; found = true; }
            }

            if (found)
            {
                var postIsland = WorldIndex.IslandAt(bestPost);
                sb.Append("Closest postbox to it: ").Append(NameFor(postIsland, bestPost))
                  .Append(", ").Append(bestDist).Append(bestDist == 1 ? " tile from it. " : " tiles from it. ");

                // Naming it is not much use if you then have to hunt for it among dozens. If it
                // is on this map, put the cursor on it so travelling there is one keypress.
                if (Active && SelectDestinationAt(bestPost))
                    sb.Append("Selected on the map. Press enter to travel there.");
            }

            return sb.ToString();
        }

        /// <summary>Put the cursor on the destination at this position, if the map has one.</summary>
        private static bool SelectDestinationAt(Vector3i pos)
        {
            // Show everything, so the destination cannot be hidden by the current group.
            _catIndex = 0;
            ApplyCategory();

            for (int i = 0; i < Shown.Count; i++)
                if (Shown[i].Position == pos) { _cursor = i; return true; }

            return false;
        }

        internal static string Current()
        {
            if (All.Count == 0) { BuildAll(); BuildCategories(); }
            if (_cursor < 0 || _cursor >= Shown.Count)
                return Cap(Categories.Count > 0 ? Categories[_catIndex] : AllCats)
                     + ", " + Shown.Count + ". Use the bracket keys to step through them.";

            return (_cursor + 1) + " of " + Shown.Count + ". "
                 + Describe(Shown[_cursor], InRouteGroup) + ".";
        }

        private static string Cap(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ---------------------------------------------------------------- polling

        /// <summary>Called each frame: announce entering and leaving the map.</summary>
        internal static void Tick()
        {
            bool active = Active;

            if (active && !_inWarp)
            {
                _inWarp = true;
                BuildAll();
                BuildCategories();

                LogDiagnostics();

                Talk.Explicit("Warp map. " + List());
                return;
            }

            if (!active && _inWarp)
            {
                _inWarp = false;
                All.Clear(); Shown.Clear(); Categories.Clear(); _cursor = -1;
                Talk.Explicit("Left the warp map.");
            }
        }

        /// <summary>
        /// Write what the map is made of to the log.
        ///
        /// "No main route" has two very different causes and they sound identical: the critical
        /// path may be empty because you have already reached all of it, or it may be full of
        /// islands that simply have no postbox on them, so none of them can ever appear as a
        /// warp destination. This prints both numbers so the difference is visible.
        /// </summary>
        private static void LogDiagnostics()
        {
            var log = Plugin.Log;
            if (log == null) return;

            var critical = CriticalIslands();
            var friends = FriendIslands();

            int critMatched = 0, friendMatched = 0, unknownIsland = 0;
            foreach (var d in All)
            {
                if (d.Critical) critMatched++;
                if (d.Friend) friendMatched++;
                if (WorldIndex.IslandAt(d.Position) == null) unknownIsland++;
            }

            log.LogInfo("--- warp map ---");
            log.LogInfo("destinations            : " + All.Count);
            log.LogInfo("world index warp points : " + WorldIndex.Count);
            log.LogInfo("destinations with no island matched: " + unknownIsland);
            log.LogInfo("critical path islands    : "
                        + (critical == null ? "could not read" : critical.Count.ToString()));
            log.LogInfo("  of those, reachable by warp: " + critMatched);
            log.LogInfo("friend hint islands      : "
                        + (friends == null ? "could not read" : friends.Count.ToString()));
            log.LogInfo("  of those, reachable by warp: " + friendMatched);

            if (critical != null && critical.Count > 0 && critMatched == 0)
                log.LogInfo("  NOTE: the critical path leads to islands with no postbox on them, "
                          + "so none of them can appear on this map.");
            if (critical != null && critical.Count == 0)
                log.LogInfo("  NOTE: the critical path is empty - you have reached all of it so far.");
            if (critical == null)
                log.LogInfo("  NOTE: the critical path could not be read at all; this is a mod fault.");

            if (critical != null)
            {
                var sb = new StringBuilder("critical path uids: ");
                for (int i = 0; i < critical.Count && i < 30; i++) sb.Append(critical[i]).Append(' ');
                log.LogInfo(sb.ToString());
            }
            log.LogInfo("--- end warp map ---");
        }

        internal static void Forget()
        {
            _inWarp = false;
            All.Clear(); Shown.Clear(); Categories.Clear(); _cursor = -1;
            WorldIndex.Forget();
        }
    }
}
