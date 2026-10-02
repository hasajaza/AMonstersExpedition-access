using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AMEAccess.Game
{
    /// <summary>
    /// Everything a controller button can be made to do, and how a binding is written down.
    ///
    /// A pad has about sixteen inputs and the mod has far more actions than that, so buttons
    /// come in layers: a button on its own, the same button with the first shoulder held, and
    /// again with the second. Three layers over a dozen buttons is more than enough room, and
    /// there is only one idea to learn - hold a shoulder for the other set - which is the same
    /// idea the keyboard already uses with control.
    ///
    /// A binding is stored as plain text so it can be read and edited: "3" is button three on
    /// its own, "mod1+3" is button three with the first shoulder held. Unbound is empty.
    /// </summary>
    internal static class PadActions
    {
        internal struct Binding
        {
            public int Layer;      // 0 none, 1 first shoulder, 2 second
            public int Button;
            public bool Set;
        }

        internal sealed class Action
        {
            public string Name;
            public ConfigEntry<string> Entry;
            public Func<string> Run;
            public bool Crucial;   // given a default by the controller profile
        }

        internal static readonly List<Action> All = new List<Action>();

        /// <summary>Make sure the table exists; the mod builds it on first use.</summary>
        internal static void EnsureBuilt() { Game.Pad.EnsureTable(); }

        internal static void Add(string name, ConfigEntry<string> entry, Func<string> run, bool crucial = false)
        {
            All.Add(new Action { Name = name, Entry = entry, Run = run, Crucial = crucial });
        }

        // ---------------------------------------------------------------- reading and writing

        internal static Binding Parse(string text)
        {
            var b = new Binding { Layer = 0, Button = -1, Set = false };
            if (string.IsNullOrEmpty(text)) return b;

            string t = text.Trim().ToLowerInvariant();

            if (t.StartsWith("mod1+")) { b.Layer = 1; t = t.Substring(5); }
            else if (t.StartsWith("mod2+")) { b.Layer = 2; t = t.Substring(5); }

            int n;
            if (!int.TryParse(t.Trim(), out n) || n < 0) return b;

            b.Button = n;
            b.Set = true;
            return b;
        }

        internal static string Format(int layer, int button)
        {
            if (button < 0) return "";
            if (layer == 1) return "mod1+" + button;
            if (layer == 2) return "mod2+" + button;
            return button.ToString();
        }

        /// <summary>How a binding should be spoken.</summary>
        internal static string Describe(string text)
        {
            var b = Parse(text);
            if (!b.Set) return "not set";

            if (b.Layer == 1) return "first shoulder and button " + b.Button;
            if (b.Layer == 2) return "second shoulder and button " + b.Button;
            return "button " + b.Button;
        }
    }
}
