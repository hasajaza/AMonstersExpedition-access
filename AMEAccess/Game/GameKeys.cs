using System;
using System.Reflection;
using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// Announces the effect of the GAME's own keys.
    ///
    /// The mod deliberately avoids binding anything the game already uses, which left a gap:
    /// pressing the game's Show Hint or Toggle Grid changed something on screen and said
    /// nothing at all. A sighted player sees hints appear and a grid switch on; without this
    /// those keys are simply dead to a blind player.
    ///
    /// Nothing is intercepted. The game handles its own keys as always; we watch the state
    /// afterwards and report what changed, so rebinding the game's controls cannot break this.
    /// </summary>
    internal static class GameKeys
    {
        private static bool _known;
        private static bool _hintsShowing;
        private static bool _grid;

        private static FieldInfo _hinterInstance;
        private static FieldInfo _hinterIsland;
        private static bool _reflectionTried;

        /// <summary>
        /// Are the game's hint markers on screen?
        ///
        /// IslandHinter keeps this privately: HideHints() sets currentIsland to null and
        /// ShowHintsFor sets it, so a non-null currentIsland means hints are visible. Read by
        /// reflection because both the static instance and the field are private.
        /// </summary>
        internal static bool HintsShowing()
        {
            if (!_reflectionTried)
            {
                _reflectionTried = true;
                try
                {
                    var t = typeof(IslandHinter);
                    _hinterInstance = t.GetField("islandHinter",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _hinterIsland = t.GetField("currentIsland",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                catch { }
            }

            if (_hinterInstance == null || _hinterIsland == null) return false;

            try
            {
                var inst = _hinterInstance.GetValue(null);
                if (inst == null) return false;
                var island = _hinterIsland.GetValue(inst) as UnityEngine.Object;
                return island != null;
            }
            catch { return false; }
        }

        /// <summary>Called each frame. Speaks only when something actually changed.</summary>
        internal static void Tick()
        {
            if (!Cfg.AnnounceGameKeys.Value || !Refs.Ready) return;

            bool hints = HintsShowing();
            bool grid = SafeGrid();

            if (!_known)
            {
                _known = true;
                _hintsShowing = hints;
                _grid = grid;
                return;                      // first pass just records the baseline
            }

            if (hints != _hintsShowing)
            {
                _hintsShowing = hints;

                // "Hints shown" on its own is useless: a sighted player sees the ghost logs the
                // instant they appear, so being told they exist without being told where they
                // point is strictly worse than nothing. Read them straight away.
                if (hints && Cfg.ReadHintsOnShow.Value)
                {
                    string where = Hints.Read();
                    Talk.Explicit("Hints shown. " + where);
                }
                else
                {
                    Talk.Explicit(hints ? "Hints shown." : "Hints hidden.");
                }
                return;
            }

            if (grid != _grid)
            {
                _grid = grid;

                // The grid is tile lines drawn on the ground - a purely visual aid with no
                // sound to it. The equivalent is coordinates, which the mod gives you whether
                // the grid is on or off, so point at that rather than leave a dead toggle.
                if (Cfg.CoordinatesFollowGrid.Value && Cfg.SpeakCoordinates.Value)
                    Talk.Explicit(grid
                        ? "Grid on. Coordinates spoken as you move."
                        : "Grid off. Coordinates quiet.");
                else
                    Talk.Explicit(grid ? "Grid on." : "Grid off.");
            }
        }

        private static bool SafeGrid()
        {
            try { return global::Config.gridView; }
            catch { return _grid; }
        }

        internal static void Forget() { _known = false; }
    }
}
