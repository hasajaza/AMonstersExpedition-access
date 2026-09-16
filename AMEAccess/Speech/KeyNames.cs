using System.Text;
using BepInEx.Configuration;

namespace AMEAccess.Speech
{
    /// <summary>
    /// Turning key codes and action names into something worth hearing.
    ///
    /// Shared by the help reader and the rebinder so a key is never described one way in the
    /// list and another way when you change it. Modifiers are spoken first, because "control
    /// plus K" is how a person says it; KeyboardShortcut.ToString renders it the other way round.
    /// </summary>
    internal static class KeyNames
    {
        internal static string Shortcut(KeyboardShortcut k)
        {
            if (k.MainKey == UnityEngine.KeyCode.None) return "unbound";

            string raw = k.ToString();
            if (string.IsNullOrEmpty(raw)) return Key(k.MainKey.ToString());

            var parts = raw.Split('+');
            if (parts.Length == 1) return Key(parts[0].Trim());

            var sb = new StringBuilder();
            for (int i = parts.Length - 1; i >= 0; i--)     // modifiers first
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(" plus ");
                sb.Append(Key(p));
            }
            return sb.ToString();
        }

        internal static string Key(string name)
        {
            switch (name)
            {
                case "LeftControl": case "RightControl": return "control";
                case "LeftShift": return "left shift";
                case "RightShift": return "right shift";
                case "LeftAlt": case "RightAlt": return "alt";
                case "LeftBracket": return "left bracket";
                case "RightBracket": return "right bracket";
                case "Return": case "KeypadEnter": return "enter";
                case "Comma": return "comma";
                case "Period": return "full stop";
                case "Slash": return "slash";
                case "Backslash": return "backslash";
                case "Quote": return "apostrophe";
                case "Semicolon": return "semicolon";
                case "Backspace": return "backspace";
                case "Escape": return "escape";
                case "Space": return "space";
                case "Tab": return "tab";
                case "UpArrow": return "up arrow";
                case "DownArrow": return "down arrow";
                case "LeftArrow": return "left arrow";
                case "RightArrow": return "right arrow";
                case "PageUp": return "page up";
                case "PageDown": return "page down";
                case "None": return "unbound";
            }

            if (name.StartsWith("Alpha") && name.Length == 6) return name.Substring(5);

            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().ToLowerInvariant();
        }

        /// <summary>"NextCategory" -> "next category", "WhereAmI" -> "where am I".</summary>
        internal static string Action(string configKey)
        {
            if (configKey == "WhereAmI") return "where am I";

            var sb = new StringBuilder();
            for (int i = 0; i < configKey.Length; i++)
            {
                char c = configKey[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(configKey[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().ToLowerInvariant();
        }
    }
}
