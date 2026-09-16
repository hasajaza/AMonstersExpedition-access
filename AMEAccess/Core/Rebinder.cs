using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AMEAccess.Speech;

namespace AMEAccess
{
    /// <summary>
    /// Changing the mod's keys from inside the game.
    ///
    /// Until now the only way to move a key was to quit, find the config file and edit it, which
    /// is a poor answer when a key turns out to clash with something or simply sits awkwardly
    /// under the hand. This walks the same binding list the help reads, so every key the mod has
    /// can be changed and none can be missing from the list.
    ///
    /// The flow is deliberately plain: step through the actions, press enter, press the key you
    /// want. Anything the game itself uses is refused with the reason, because silently taking a
    /// key the game needs would break movement with no clue why.
    /// </summary>
    internal static class Rebinder
    {
        private static bool _active;
        private static bool _capturing;
        private static int _index;

        internal static bool Active => _active;

        // The game's own bindings, from its Controls screen. Taking one of these would leave the
        // mod and the game fighting over the same key.
        private static readonly KeyCode[] GameKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.Z, KeyCode.R, KeyCode.G, KeyCode.H,
            KeyCode.O, KeyCode.P, KeyCode.Space, KeyCode.Escape,
            KeyCode.LeftShift,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow
        };

        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt
        };

        internal static string Toggle()
        {
            _active = !_active;
            _capturing = false;

            if (!_active) return "Key changes finished.";

            _index = -1;
            return "Change keys. Use the bracket keys to step through the actions, enter to "
                 + "change the one you are on, escape to finish.";
        }

        private static List<Cfg.Binding> All => Cfg.AllBindings;

        private static string Announce()
        {
            var all = All;
            if (_index < 0 || _index >= all.Count) return "No actions.";

            var b = all[_index];
            return (_index + 1) + " of " + all.Count + ". " + b.Section + ", "
                 + KeyNames.Action(b.Name) + ", currently " + KeyNames.Shortcut(b.Entry.Value) + ".";
        }

        private static string Step(int dir)
        {
            var all = All;
            if (all.Count == 0) return "No actions.";

            _index += dir;
            if (_index >= all.Count) _index = 0;
            if (_index < 0) _index = all.Count - 1;
            return Announce();
        }

        /// <summary>
        /// Handle every key while the mode is on. Returns true when the press was consumed, so
        /// nothing reaches the rest of the mod and you cannot trigger an action while rebinding.
        /// </summary>
        internal static bool Handle()
        {
            if (!_active) return false;

            if (_capturing) return Capture();

            if (Input.GetKeyDown(KeyCode.Escape)) { Talk.Explicit(Toggle()); return true; }
            if (Input.GetKeyDown(KeyCode.RightBracket)) { Talk.Explicit(Step(1)); return true; }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) { Talk.Explicit(Step(-1)); return true; }
            if (Input.GetKeyDown(KeyCode.PageDown)) { Talk.Explicit(Step(1)); return true; }
            if (Input.GetKeyDown(KeyCode.PageUp)) { Talk.Explicit(Step(-1)); return true; }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                var all = All;
                if (_index >= 0 && _index < all.Count)
                {
                    all[_index].Entry.Value = all[_index].Default;
                    Save();
                    Talk.Explicit("Put back to " + KeyNames.Shortcut(all[_index].Default) + ".");
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var all = All;
                if (_index < 0 || _index >= all.Count)
                {
                    Talk.Explicit("Step to an action first with the bracket keys.");
                    return true;
                }
                _capturing = true;
                Talk.Explicit("Press the new key for " + KeyNames.Action(all[_index].Name)
                            + ". Hold control or shift with it if you want them. Escape to cancel.");
                return true;
            }

            return true;   // swallow everything else so no action fires while rebinding
        }

        private static bool Capture()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _capturing = false;
                Talk.Explicit("Cancelled. " + Announce());
                return true;
            }

            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(k)) continue;
                if (IsModifier(k)) continue;          // a modifier alone is not a binding

                if (Array.IndexOf(GameKeys, k) >= 0)
                {
                    Talk.Explicit(KeyNames.Key(k.ToString())
                        + " is used by the game itself. Pick another key.");
                    return true;
                }

                var mods = new List<KeyCode>();
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    mods.Add(KeyCode.LeftControl);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    mods.Add(KeyCode.LeftShift);
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                    mods.Add(KeyCode.LeftAlt);

                var shortcut = mods.Count == 0
                    ? new KeyboardShortcutOf(k)
                    : new KeyboardShortcutOf(k, mods.ToArray());

                var all = All;
                var target = all[_index];

                string clash = ClashingAction(shortcut.Value, target.Section, target.Name);
                if (clash != null)
                {
                    Talk.Explicit(KeyNames.Shortcut(shortcut.Value) + " is already used by "
                                + clash + ". Pick another key.");
                    return true;
                }

                target.Entry.Value = shortcut.Value;
                Save();

                _capturing = false;
                Talk.Explicit(KeyNames.Action(target.Name) + " is now "
                            + KeyNames.Shortcut(shortcut.Value) + ".");
                return true;
            }

            return true;
        }

        private static bool IsModifier(KeyCode k) => Array.IndexOf(Modifiers, k) >= 0;

        /// <summary>
        /// Which other action already uses this combination, if any.
        ///
        /// Matched on section AND name: "North" exists in both the direction cluster and the
        /// review cursor, so skipping by name alone would skip the wrong entry as well as the
        /// right one, and a real clash between the two would go unreported.
        /// </summary>
        private static string ClashingAction(BepInEx.Configuration.KeyboardShortcut s,
                                             string skipSection, string skipName)
        {
            string wanted = KeyNames.Shortcut(s);
            foreach (var b in All)
            {
                if (b.Section == skipSection && b.Name == skipName) continue;
                if (KeyNames.Shortcut(b.Entry.Value) == wanted)
                    return b.Section + ", " + KeyNames.Action(b.Name);
            }
            return null;
        }

        private static void Save() { Cfg.Save(); }

        /// <summary>
        /// Small helper so a shortcut can be built with or without modifiers without repeating
        /// the constructor call, which differs in shape between the two cases.
        /// </summary>
        private struct KeyboardShortcutOf
        {
            public readonly BepInEx.Configuration.KeyboardShortcut Value;
            public KeyboardShortcutOf(KeyCode main)
            { Value = new BepInEx.Configuration.KeyboardShortcut(main); }
            public KeyboardShortcutOf(KeyCode main, KeyCode[] mods)
            { Value = new BepInEx.Configuration.KeyboardShortcut(main, mods); }
        }
    }
}
