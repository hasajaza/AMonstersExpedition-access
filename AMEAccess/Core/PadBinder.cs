using System;
using System.Collections.Generic;
using UnityEngine;
using AMEAccess.Game;
using AMEAccess.Speech;

namespace AMEAccess
{
    /// <summary>
    /// Setting up a controller from inside the game, the way F2 does for the keyboard.
    ///
    /// The sticks are the point of it. A button can be found by pressing it and hearing a
    /// number, but an axis cannot be guessed at from a name or a log: the only reliable way to
    /// know which axis is the right stick's vertical is to push the stick up and see what moves.
    /// That is what this does - and because it watches which way the value went, it works out
    /// whether up reads as positive or negative too, so there is no invert setting to get wrong.
    ///
    /// It is driven from the keyboard on purpose. You are configuring the controller, so the
    /// controller cannot be trusted to navigate the thing configuring it.
    /// </summary>
    internal static class PadBinder
    {
        private enum Kind { StickHorizontal, StickVertical, PlainButton, Action }

        private struct Item
        {
            public string Name;
            public Kind Kind;
            public BepInEx.Configuration.ConfigEntry<int> Axis;          // sticks and modifiers
            public BepInEx.Configuration.ConfigEntry<string> Binding;    // everything else
        }

        private static Item[] _items;
        private static int _index;
        private static bool _active, _capturing;

        internal static bool Active => _active;

        private static void BuildItems()
        {
            var list = new List<Item>
            {
                new Item { Name = "left stick, left and right, for walking",  Kind = Kind.StickHorizontal, Axis = Cfg.PadLeftStickX },
                new Item { Name = "left stick, up and down, for walking",     Kind = Kind.StickVertical,   Axis = Cfg.PadLeftStickY },
                new Item { Name = "right stick, left and right, for the cursor", Kind = Kind.StickHorizontal, Axis = Cfg.PadRightStickX },
                new Item { Name = "right stick, up and down, for the cursor",    Kind = Kind.StickVertical,   Axis = Cfg.PadRightStickY },
                new Item { Name = "first shoulder, held for the second set",  Kind = Kind.PlainButton, Axis = Cfg.PadMod1 },
                new Item { Name = "second shoulder, held for the third set",  Kind = Kind.PlainButton, Axis = Cfg.PadMod2 },
                new Item { Name = "the stick click",                          Kind = Kind.PlainButton, Axis = Cfg.PadStickClick },
            };

            // Then everything a button can do, from the one list the config and the mod share.
            Game.PadActions.EnsureBuilt();
            foreach (var a in Game.PadActions.All)
                list.Add(new Item { Name = a.Name, Kind = Kind.Action, Binding = a.Entry });

            _items = list.ToArray();
        }

        internal static string Toggle()
        {
            _active = !_active;
            _capturing = false;

            if (!_active) { Cfg.Save(); return "Controller setup finished."; }

            BuildItems();
            _index = 0;

            string found = Pad.ControllerName();
            if (found == null) return "No controller found. Connect one and press this again.";

            return "Controller setup, using " + found
                 + ". Bracket keys to step through, enter to set the one you are on, "
                 + "backspace to unset it, and this key or escape to finish. " + Describe();
        }

        private static string Describe()
        {
            var it = _items[_index];

            string value;
            if (it.Kind == Kind.Action)
                value = Game.PadActions.Describe(it.Binding.Value);
            else if (it.Axis.Value < 0)
                value = "not set";
            else
                value = (it.Kind == Kind.PlainButton ? "button " : "axis ") + it.Axis.Value;

            string extra = "";
            if (it.Kind == Kind.StickVertical && it.Axis.Value >= 0)
                extra = Cfg.PadInvertY.Value ? ", up is negative" : ", up is positive";

            return (_index + 1) + " of " + _items.Length + ". " + it.Name + ", " + value + extra + ".";
        }

        /// <summary>Handles every key while setup is on. Returns true when it used the press.</summary>
        internal static bool Handle()
        {
            if (!_active) return false;

            if (_capturing) return Capture();

            // Both leave. The opening key is the tidy way out, because the game also reads
            // escape and will open its own menu behind us - but being unable to leave at all is
            // far worse than a menu opening, so escape works too.
            if (Cfg.Pressed(Cfg.KeyPadSetup) || Input.GetKeyDown(KeyCode.Escape))
            { Talk.Explicit(Toggle()); return true; }

            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.PageDown))
            { Step(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.PageUp))
            { Step(-1); return true; }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                var it = _items[_index];
                if (it.Kind == Kind.Action) it.Binding.Value = "";
                else it.Axis.Value = -1;

                Cfg.Save();
                Talk.Explicit("Unset. " + Describe());
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                _capturing = true;
                Pad.StartCapture();

                var it = _items[_index];
                switch (it.Kind)
                {
                    case Kind.StickHorizontal:
                        Talk.Explicit("Push that stick to the RIGHT and hold it."); break;
                    case Kind.StickVertical:
                        Talk.Explicit("Push that stick UP and hold it."); break;
                    default:
                        Talk.Explicit("Press the button you want for " + it.Name
                                    + ". Hold a shoulder as well to put it on the second or third set."); break;
                }
                return true;
            }

            return true;    // swallow everything else so nothing fires while setting up
        }

        private static void Step(int by)
        {
            _index += by;
            if (_index >= _items.Length) _index = 0;
            if (_index < 0) _index = _items.Length - 1;
            Talk.Explicit(Describe());
        }

        private static bool Capture()
        {
            if (Cfg.Pressed(Cfg.KeyPadSetup) || Input.GetKeyDown(KeyCode.Escape))
            {
                _capturing = false;
                Talk.Explicit("Cancelled. " + Describe());
                return true;
            }

            var it = _items[_index];

            if (it.Kind == Kind.Action || it.Kind == Kind.PlainButton)
            {
                int layer;
                int button = Pad.CapturedButton(out layer);
                if (button < 0) return true;

                if (it.Kind == Kind.PlainButton)
                {
                    it.Axis.Value = button;
                    Cfg.Save();
                    _capturing = false;
                    Talk.Explicit("Button " + button + " set for " + it.Name + ".");
                    return true;
                }

                // Whichever shoulder was held decides the set, so putting something on the
                // second or third set is simply holding a shoulder while you press.
                it.Binding.Value = Game.PadActions.Format(layer, button);
                Cfg.Save();
                _capturing = false;
                Talk.Explicit(Game.PadActions.Describe(it.Binding.Value) + " set for " + it.Name + ".");
                return true;
            }

            int axis; float value;
            if (!Pad.CapturedAxis(out axis, out value)) return true;

            it.Axis.Value = axis;

            if (it.Kind == Kind.StickVertical)
            {
                Cfg.PadInvertY.Value = value < 0;
                Cfg.PadInvertSet.Value = true;
            }
            else
            {
                Cfg.PadInvertX.Value = value < 0;
            }

            Cfg.Save();
            _capturing = false;

            Talk.Explicit("Axis " + axis + " set for " + it.Name
                        + (value < 0 ? ", reading backwards, which is now corrected." : ".")
                        + " " + Describe());
            return true;
        }
    }
}
