using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
        private static readonly List<WarpModeButton> Ordered = new List<WarpModeButton>();
        private static int _cursor = -1;

        private static FieldInfo _fButtons, _fCritical, _fProgress, _fEntrance, _fPriority;
        private static bool _reflected;

        private static readonly List<int> CriticalBuffer = new List<int>();

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

        private static List<WarpModeButton> Buttons()
        {
            Reflect();
            var w = WarpMode.currentWarpMode;
            if (w == null || _fButtons == null) return null;
            try { return _fButtons.GetValue(w) as List<WarpModeButton>; }
            catch { return null; }
        }

        private static WarpModeButton Recommended()
        {
            Reflect();
            var w = WarpMode.currentWarpMode;
            if (w == null || _fPriority == null) return null;
            try { return _fPriority.GetValue(w) as WarpModeButton; }
            catch { return null; }
        }

        /// <summary>Island uids on the game's own critical path — the green route.</summary>
        private static List<int> CriticalIslands()
        {
            Reflect();
            var w = WarpMode.currentWarpMode;
            if (w == null || _fCritical == null || _fProgress == null) return null;

            try
            {
                var cp = _fCritical.GetValue(w) as CriticalPath;
                var progress = _fProgress.GetValue(w) as Progress;
                if (cp == null || progress == null) return null;

                var profile = progress.profile;
                if (profile == null) return null;

                CriticalBuffer.Clear();
                return cp.GetIslands(profile, CriticalBuffer);
            }
            catch { return null; }
        }

        private static Vector3i Origin()
        {
            Reflect();
            var w = WarpMode.currentWarpMode;
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

        /// <summary>Describe one destination.</summary>
        private static string Describe(WarpModeButton b, List<int> critical, Vector3i origin)
        {
            if (b == null) return "unknown destination";

            var sb = new StringBuilder();
            var pos = b.position;

            var sim = Refs.Sim;
            Island island = null;
            if (sim != null)
            {
                try { island = sim.GetIslandBeneathPosition(pos); }
                catch { }
            }

            if (island != null) sb.Append(Naming.IslandLabel(island, false));
            else sb.Append("postbox at ").Append(Naming.Coords(pos));

            sb.Append(". ").Append(Survey.Relative(origin, pos));

            // Visited, from the progress record rather than any display flag.
            var progress = Refs.Progress;
            if (island != null && progress != null)
            {
                try
                {
                    sb.Append(progress.HasSteppedOn(island.id.uid) ? ", visited" : ", not visited");
                }
                catch { }
            }

            // The green route. Critical path membership is by island, so it can only be stated
            // for a destination whose island is loaded.
            if (island != null && critical != null)
            {
                try { if (critical.Contains(island.id.uid)) sb.Append(", main route"); }
                catch { }
            }

            if (b.isPriority) sb.Append(", suggested");

            return sb.ToString();
        }

        /// <summary>Build the ordered destination list, nearest first.</summary>
        private static void BuildOrder()
        {
            Ordered.Clear();
            var buttons = Buttons();
            if (buttons == null) return;

            var origin = Origin();
            foreach (var b in buttons) if (b != null) Ordered.Add(b);
            Ordered.Sort((a, b) => Survey.Manhattan(origin, a.position)
                                         .CompareTo(Survey.Manhattan(origin, b.position)));
        }

        /// <summary>Step the cursor through the destinations.</summary>
        internal static string Step(int direction)
        {
            if (Ordered.Count == 0) BuildOrder();
            if (Ordered.Count == 0) return "No destinations on the map.";

            _cursor += direction;
            if (_cursor >= Ordered.Count) _cursor = 0;
            if (_cursor < 0) _cursor = Ordered.Count - 1;

            var b = Ordered[_cursor];
            return (_cursor + 1) + " of " + Ordered.Count + ". "
                 + Describe(b, CriticalIslands(), Origin()) + ".";
        }

        /// <summary>
        /// Travel to the destination under the cursor.
        ///
        /// The warp methods are private, but WarpModeButton.button is public, so invoking its
        /// onClick runs the same path a mouse click would - no reflection into game logic.
        /// </summary>
        internal static string Activate()
        {
            if (_cursor < 0 || _cursor >= Ordered.Count) return "No destination selected. Step to one first.";

            var b = Ordered[_cursor];
            if (b == null || b.button == null) return "That destination cannot be used.";

            string where = Describe(b, CriticalIslands(), Origin());
            try { b.button.onClick.Invoke(); }
            catch (Exception e) { return "Could not travel there: " + e.GetType().Name + "."; }

            return "Travelling to " + where + ".";
        }

        /// <summary>Every destination, nearest first.</summary>
        internal static string List()
        {
            var buttons = Buttons();
            if (buttons == null || buttons.Count == 0) return "No destinations on the map.";

            var origin = Origin();
            var critical = CriticalIslands();

            var sorted = new List<WarpModeButton>(buttons);
            sorted.RemoveAll(b => b == null);
            sorted.Sort((a, b) => Survey.Manhattan(origin, a.position)
                                        .CompareTo(Survey.Manhattan(origin, b.position)));

            var sb = new StringBuilder();
            sb.Append(sorted.Count).Append(sorted.Count == 1 ? " destination. " : " destinations. ");

            int listed = 0;
            foreach (var b in sorted)
            {
                if (listed >= Cfg.WarpListCount.Value) break;
                sb.Append(listed + 1).Append(". ").Append(Describe(b, critical, origin)).Append(". ");
                listed++;
            }
            if (sorted.Count > listed) sb.Append(sorted.Count - listed).Append(" more.");
            return sb.ToString();
        }

        /// <summary>Whatever the game currently has selected.</summary>
        internal static string Current()
        {
            var buttons = Buttons();
            if (buttons == null || buttons.Count == 0) return "No destinations on the map.";

            if (Ordered.Count == 0) BuildOrder();
            if (_cursor < 0 || _cursor >= Ordered.Count)
                return Ordered.Count + " destinations. Use the bracket keys to step through them.";

            return (_cursor + 1) + " of " + Ordered.Count + ". "
                 + Describe(Ordered[_cursor], CriticalIslands(), Origin()) + ".";
        }

        /// <summary>What the game itself recommends, if anything.</summary>
        private static string Suggestion()
        {
            var rec = Recommended();
            if (rec == null) return null;
            return "Suggested: " + Describe(rec, CriticalIslands(), Origin()) + ".";
        }

        // ---------------------------------------------------------------- polling

        /// <summary>Called each frame: announce entering, leaving and selection changes.</summary>
        internal static void Tick()
        {
            bool active = Active;

            if (active && !_inWarp)
            {
                _inWarp = true;

                var buttons = Buttons();
                int n = buttons == null ? 0 : buttons.Count;

                BuildOrder();
                _cursor = -1;

                var sb = new StringBuilder("Warp map. ");
                sb.Append(n).Append(n == 1 ? " destination. " : " destinations. ");

                string suggested = Suggestion();
                if (suggested != null) sb.Append(suggested).Append(' ');

                sb.Append("Use the bracket keys to step through them, enter to travel.");

                var log = Plugin.Log;
                if (log != null)
                    log.LogInfo("[warp] entered map, " + n + " destinations, ordered "
                                + Ordered.Count + ".");

                Talk.Explicit(sb.ToString());
                return;
            }

            if (!active && _inWarp)
            {
                _inWarp = false;
                Ordered.Clear();
                _cursor = -1;
                Talk.Explicit("Left the warp map.");
                return;
            }

            if (!active) return;

            // Deliberately does not chase the game's own selection. That selection follows the
            // camera, so it would fight the cursor and announce destinations you did not choose.
        }

        internal static void Forget() { _inWarp = false; Ordered.Clear(); _cursor = -1; }
    }
}
