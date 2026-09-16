using UnityEngine;
using Rich = AMEAccess.Util.Rich;

using GridMovement = Player.GridMovement;

namespace AMEAccess.Game
{
    /// <summary>
    /// Cached handles into the game.
    ///
    /// Everything gameplay-related lives on a handful of objects in the single boot scene
    /// (verified by parsing level0): "Gameplay" carries Simulator, Board, World, History,
    /// Progress; "Player" carries GridMovement; "Resetter", "Island Hinter" and "Plaque"
    /// carry the rest. So there are no scene transitions to handle.
    ///
    /// Simulator.current is a plain public static field assigned in Simulator.Awake(), which
    /// makes it the cheapest entry point. Globals.simulator would also work but falls back to
    /// FindObjectOfType whenever its backing field is null, so it is not safe to poll.
    ///
    /// Note: we deliberately avoid ?. on UnityEngine.Object references. A destroyed object is
    /// not null to the C# operator but is "null" to Unity's overloaded ==, so ?. would happily
    /// dereference a dead object during shutdown.
    /// </summary>
    internal static class Refs
    {
        private static Resetter _resetter;

        internal static Simulator Sim
        {
            get { var s = Simulator.current; return s != null ? s : null; }
        }

        internal static bool Ready
        {
            get
            {
                var s = Sim;
                if (s == null) return false;
                return s.player != null && s.board != null;
            }
        }

        internal static GridMovement PlayerMove { get { var s = Sim; return s == null ? null : s.player; } }

        private static Player.GridKeyboardInput _keyboardInput;
        /// <summary>
        /// The component that turns keyboard input into monster movement. Review mode disables
        /// it so the arrow keys can drive the cursor instead.
        /// </summary>
        internal static Player.GridKeyboardInput KeyboardInput
        {
            get
            {
                if (_keyboardInput == null) _keyboardInput = Object.FindObjectOfType<Player.GridKeyboardInput>();
                return _keyboardInput;
            }
        }
        internal static Board Board { get { var s = Sim; return s == null ? null : s.board; } }
        internal static History History { get { var s = Sim; return s == null ? null : s.history; } }
        internal static Progress Progress { get { var s = Sim; return s == null ? null : s.progress; } }
        internal static World World { get { var s = Sim; return s == null ? null : s.world; } }
        internal static Island CurrentIsland { get { var s = Sim; return s == null ? null : s.island; } }
        internal static CaptionViewer Captions { get { var s = Sim; return s == null ? null : s.captionViewer; } }

        internal static Resetter Resetter
        {
            get
            {
                // Simulator has no resetter field, so find it once and keep it.
                if (_resetter == null) _resetter = Object.FindObjectOfType<Resetter>();
                return _resetter;
            }
        }

        internal static void Forget() { _resetter = null; _keyboardInput = null; }

        /// <summary>
        /// True when the game is in a state where the player could actually be moving around.
        /// Keeps the mod silent in menus, cutscenes, the title screen and warp mode.
        /// </summary>
        internal static bool InPlay
        {
            get
            {
                if (!Ready) return false;
                return GameState.Has(GameState.Capabilities.AllowsKeyboardGamepadMovement);
            }
        }

        internal static Vector3i PlayerPos
        {
            get { var p = PlayerMove; return p == null ? Vector3i.zero : p.position; }
        }

        internal static Vector3i PlayerFacing
        {
            get { var p = PlayerMove; return p == null ? Vector3i.north : p.direction; }
        }
    }
}
