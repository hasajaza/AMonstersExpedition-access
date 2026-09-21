using HarmonyLib;
using UnityEngine;
using AMEAccess.Game;

namespace AMEAccess
{
    /// <summary>
    /// Lets the arrow keys drive the review cursor permanently, when the player asks for it.
    ///
    /// The arrows cannot simply be reassigned. The game reads movement from Rewired's axes, and
    /// both the arrows and W A S D feed those axes, so by the time the game sees a value there
    /// is no way to tell which key produced it.
    ///
    /// So this vetoes the game's own input method on frames where an arrow key is down and no
    /// W A S D key is. On every other frame the original runs untouched, which means W A S D
    /// movement is still entirely the game's code - input buffering, sitting, the undo hold,
    /// the first-move case and everything else. Nothing is reimplemented.
    ///
    /// Off by default. The review toggle stays available either way, so there is always a way
    /// back if this ever misbehaves.
    /// </summary>
    [HarmonyPatch(typeof(Player.GridKeyboardInput), "DirectionalInput")]
    internal static class ArrowVeto
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!Cfg.ArrowsAlwaysReview.Value) return true;      // option off: game as normal
            if (!Refs.InPlay) return true;                       // menus and cutscenes untouched

            bool arrows = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
                       || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
            if (!arrows) return true;

            // Holding both means you meant to walk; W A S D always wins.
            bool wasd = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A)
                     || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
            if (wasd) return true;

            return false;   // arrow-only frame: the game does not move the monster
        }
    }
}
