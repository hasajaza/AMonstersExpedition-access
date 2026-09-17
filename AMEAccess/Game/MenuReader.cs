using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Rich = AMEAccess.Util.Rich;

using UnityEngine.EventSystems;
using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// Reads the game's menus.
    ///
    /// This was missing entirely from the first version, which meant the mod was useless until
    /// you had already started a game - and you could not start a game, because the title screen
    /// and menus were silent. Gameplay keys are gated on GameState allowing movement, which is
    /// false in every menu.
    ///
    /// The game uses stock Unity UI (UnityEngine.UI.Toggle, EventSystems interfaces), so the
    /// focused control is whatever EventSystem.current reports as selected. We read its label by
    /// looking for any component exposing a string "text" property, which covers both
    /// UnityEngine.UI.Text and TextMeshPro without needing a hard reference to either.
    /// </summary>
    internal static class MenuReader
    {
        private static GameObject _lastSelected;
        private static string _lastSlider;
        private static string _lastSpoken;
        private static float _nextPoll;
        private static object _lastSlot;

        /// <summary>Watch for focus changes and announce them. Called every frame.</summary>
        internal static void Tick()
        {
            if (!Cfg.ReadMenus.Value) return;

            // Polling a few times a second is plenty and keeps the cost invisible.
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + Cfg.MenuPollInterval.Value;

            var es = EventSystem.current;
            if (es == null) return;

            var sel = es.currentSelectedGameObject;
            if (sel == null) { _lastSelected = null; return; }

            // Moving a slider does not move focus, so without this a volume row is read once
            // and then stays silent while you are actually adjusting it.
            if (sel == _lastSelected)
            {
                string now = SliderText(sel);
                if (now != null && now != _lastSlider)
                {
                    _lastSlider = now;
                    Talk.Explicit(now);
                }
                return;
            }
            _lastSlider = SliderText(sel);

            _lastSelected = sel;

            string text = Describe(sel);
            if (string.IsNullOrEmpty(text) || text == _lastSpoken) return;

            // Arriving at a save slot should read the save, not just the button. Which slot you
            // are on is the whole question on that screen, and pressing a key to find out is a
            // step a sighted player never has to take.
            //
            // Only when the slot CHANGES: moving between Delete, Reset and Continue inside one
            // slot would otherwise repeat the whole summary three times.
            if (Cfg.AnnounceSaveSlots.Value)
            {
                var slot = SlotOf(sel);
                if (slot != null && !ReferenceEquals(slot, _lastSlot))
                {
                    _lastSlot = slot;
                    string details = ReadSaveSlot(sel);
                    if (!string.IsNullOrEmpty(details)) text = details + " " + text;
                }
                else if (slot == null)
                {
                    _lastSlot = null;
                }
            }

            _lastSpoken = text;
            Talk.Explicit(text);
        }

        /// <summary>
        /// Write the focused control's surrounding hierarchy to the BepInEx log.
        ///
        /// Menu layouts differ from screen to screen and cannot be inferred from the binaries,
        /// so when something reads wrongly this dump shows the actual structure - which object
        /// has focus, what its siblings are, and which components carry the text and the state.
        /// </summary>
        internal static void DumpFocused(BepInEx.Logging.ManualLogSource log)
        {
            var es = EventSystem.current;
            if (es == null) { log.LogInfo("[menu dump] no EventSystem"); return; }

            var sel = es.currentSelectedGameObject;
            if (sel == null) { log.LogInfo("[menu dump] nothing focused"); return; }

            log.LogInfo("--- menu dump ---");
            log.LogInfo("spoken as  : " + Describe(sel));
            log.LogInfo("focused    : " + Path(sel));
            log.LogInfo("components : " + Components(sel));
            log.LogInfo("own text   : " + (FindText(sel) ?? "(none)"));
            var cands = LabelCandidates(sel, FindText(sel));
            log.LogInfo("label cands: " + (cands.Count == 0 ? "(none)" : string.Join(" | ", cands.ToArray()))
                        + "   -> " + (cands.Count == 1 ? "used" : "ambiguous, ignored"));

            var parent = sel.transform.parent;
            if (parent == null) { log.LogInfo("no parent"); log.LogInfo("--- end menu dump ---"); return; }

            log.LogInfo("parent     : " + Path(parent.gameObject) + "  [" + Components(parent.gameObject) + "]");
            foreach (Transform sib in parent)
            {
                string mark = sib == sel.transform ? " <== focused" : "";
                log.LogInfo("  sibling  : " + sib.gameObject.name +
                            "  active=" + sib.gameObject.activeInHierarchy +
                            "  text=" + (DeepText(sib.gameObject, 3) ?? "-") +
                            "  interactive=" + IsInteractive(sib.gameObject) +
                            "  isOn=" + (IsOnIn(sib.gameObject, 2) ?? "-") + mark);
            }
            log.LogInfo("--- end menu dump ---");
        }

        private static string Path(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            Transform t = go.transform.parent;
            for (int i = 0; i < 4 && t != null; i++) { sb.Insert(0, t.name + "/"); t = t.parent; }
            return sb.ToString();
        }

        private static string Components(GameObject go)
        {
            var sb = new StringBuilder();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c.GetType().Name);
            }
            return sb.Length == 0 ? "(none)" : sb.ToString();
        }

        /// <summary>The save slot this control belongs to, or null.</summary>
        private static object SlotOf(GameObject go)
        {
            var d = go.GetComponentInParent<SaveSlotDetails>();
            return d == null ? null : (object)d;
        }

        /// <summary>
        /// Read a save slot from the game's own SaveSlotDetails component.
        ///
        /// Scraping the panel gives "0" and "2" with no idea what they count - on screen those
        /// numbers sit beside icons, and an icon says nothing out loud. SaveSlotDetails holds
        /// each value in a named field, so we can pair every number with what it actually means.
        ///
        /// The TMP fields are read by reflection so the mod needs no reference to
        /// Unity.TextMeshPro; only the field names, which come from the game assembly.
        /// </summary>
        private static string ReadSaveSlot(GameObject go)
        {
            var details = go.GetComponentInParent<SaveSlotDetails>();
            if (details == null) return null;

            var parts = new List<string>();
            Add(parts, details, "TMPTimePlayed", "time played");
            Add(parts, details, "TMPLastPlayed", "last played");
            Add(parts, details, "TMPIslandsVisited", "islands visited");
            Add(parts, details, "TMPExhibitsDiscovered", "exhibits discovered");
            Add(parts, details, "TMPFriendsMet", "friends met");
            Add(parts, details, "TMPLastPlayer", "last player");

            if (parts.Count == 0) return null;

            var sb = new StringBuilder("Save slot. ");
            foreach (var p in parts)
            {
                sb.Append(p);
                if (!p.EndsWith(".")) sb.Append('.');
                sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        private static void Add(List<string> into, object details, string fieldName, string label)
        {
            try
            {
                var f = details.GetType().GetField(fieldName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (f == null) return;

                var tmp = f.GetValue(details) as UnityEngine.Object;
                if (tmp == null) return;                       // Unity-aware null check
                if (!((Component)tmp).gameObject.activeInHierarchy) return;

                object v = GetProp(tmp, "text");
                if (!(v is string)) return;

                string text = Rich.Strip((string)v).Trim();
                if (text.Length == 0) return;

                // useLabels makes the game print its own label, so do not add a second one.
                into.Add(text.Contains(":") ? text : label + " " + text);
            }
            catch { }
        }

        /// <summary>
        /// Read every visible piece of text in the panel around the focused control.
        ///
        /// Some screens put information beside the buttons rather than inside them. A save slot
        /// shows a play time, a date, an islands-visited count and an exhibits count next to its
        /// Delete, Reset and Continue buttons. None of that belongs to any one button, so it is
        /// never spoken as a label - but it is exactly what you need in order to know which save
        /// you are about to load.
        /// </summary>
        internal static string ReadPanel()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return "Nothing focused.";

            var sel = es.currentSelectedGameObject;

            // A save slot has a component that knows what each number means; prefer it.
            string slot = ReadSaveSlot(sel);
            if (!string.IsNullOrEmpty(slot)) return slot;

            Transform panel = sel.transform.parent;

            for (int i = 0; i < 4 && panel != null; i++)
            {
                var texts = new List<string>();
                var fromControls = new List<string>();
                Collect(panel.gameObject, texts, 5);
                CollectControlLabels(panel.gameObject, fromControls, 5);

                // A container holding nothing but button captions tells you no more than moving
                // between the buttons would. Keep climbing until there is something else in it.
                bool onlyButtons = true;
                foreach (var t in texts)
                    if (!fromControls.Contains(t)) { onlyButtons = false; break; }

                if (texts.Count > 0 && !onlyButtons)
                {
                    var sb = new StringBuilder();
                    foreach (var line in texts)
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(line);
                    }
                    return sb.Append('.').ToString();
                }

                panel = panel.parent;
            }
            return "Nothing else in this panel.";
        }

        /// <summary>Texts that belong to a control, i.e. button captions.</summary>
        private static void CollectControlLabels(GameObject go, List<string> into, int depth)
        {
            if (go == null || !go.activeInHierarchy || depth < 0) return;

            if (IsInteractive(go))
            {
                string t = DeepText(go, 3);
                if (!string.IsNullOrEmpty(t) && !into.Contains(t)) into.Add(t);
                return;
            }

            foreach (Transform child in go.transform)
                CollectControlLabels(child.gameObject, into, depth - 1);
        }

        private static void Collect(GameObject go, List<string> into, int depth)
        {
            if (go == null || !go.activeInHierarchy || depth < 0) return;

            string t = TextOn(go);
            if (!string.IsNullOrEmpty(t) && !into.Contains(t)) into.Add(t);

            foreach (Transform child in go.transform)
                Collect(child.gameObject, into, depth - 1);
        }

        /// <summary>Re-read whatever currently has focus.</summary>
        internal static string ReadFocused()
        {
            var es = EventSystem.current;
            if (es == null) return "No menu focus.";

            var sel = es.currentSelectedGameObject;
            if (sel == null) return "Nothing focused. Use the arrow keys or Tab to move around the menu.";

            string t = Describe(sel);
            return string.IsNullOrEmpty(t) ? "Unlabelled control." : t;
        }

        /// <summary>
        /// Build a spoken label: the control's text, its state if it has one, and its role.
        /// </summary>
        private static string Describe(GameObject go)
        {
            var sb = new StringBuilder();

            // The focused control usually carries only its VALUE - "Auto" on a dropdown, "A" on
            // a key-binding button. The thing it belongs to ("Language", "Move left") is a
            // sibling label on the surrounding row. Reading the value alone is useless, so we
            // look outward for the label and say label first, value second.
            string value = FindText(go);
            string label = FindRowLabel(go, value);

            if (!string.IsNullOrEmpty(label)) sb.Append(label);

            if (!string.IsNullOrEmpty(value) && value != label)
            {
                if (sb.Length > 0) sb.Append(": ");
                sb.Append(value);
            }

            if (sb.Length == 0) sb.Append(Prettify(go.name));

            bool foundToggle = false;

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string type = c.GetType().Name;

                if (type == "Toggle")
                {
                    object on = GetProp(c, "isOn");
                    if (on is bool) { sb.Append((bool)on ? ", checked" : ", unchecked"); foundToggle = true; }
                }
                // Sliders are handled after this loop, by capability rather than type name:
                // the game's are SliderWithLabelHighlight, a SUBCLASS of Slider, so matching
                // the name exactly never fired and volume rows read without their value.
                else if (type == "TMP_Dropdown" || type == "Dropdown")
                {
                    sb.Append(", dropdown");
                }
                else if (type == "Button")
                {
                    sb.Append(", button");
                }
                else if (type == "InputField" || type == "TMP_InputField")
                {
                    object v = GetProp(c, "text");
                    sb.Append(", edit box");
                    if (v is string && ((string)v).Length > 0) sb.Append(", ").Append(v);
                }

                object interactable = GetProp(c, "interactable");
                if (interactable is bool && !(bool)interactable) sb.Append(", unavailable");
            }

            // Some rows put the Toggle on a parent or a child rather than on the focused
            // object, which is why checkboxes were reading without their state.
            if (!foundToggle)
            {
                object on = FindToggleState(go);
                if (on is bool) sb.Append((bool)on ? ", checked" : ", unchecked");
            }

            string slider = SliderText(go);
            if (slider != null) sb.Append(", ").Append(slider);

            return Rich.Strip(sb.ToString());
        }

        /// <summary>
        /// Find the toggle that belongs to this row.
        ///
        /// The settings screen uses ordinary UnityEngine.UI.Toggle components, but focus often
        /// lands on a neighbouring object in the row rather than on the toggle itself, which is
        /// why checkboxes were reading without their state. So we check the object, then below
        /// it, then its siblings, then its parent.
        /// </summary>
        private static object FindToggleState(GameObject go)
        {
            object v = IsOnIn(go, 2);
            if (v != null) return v;

            Transform parent = go.transform.parent;
            if (parent == null) return null;

            foreach (Transform sibling in parent)
            {
                if (sibling == go.transform) continue;
                v = IsOnIn(sibling.gameObject, 2);
                if (v != null) return v;
            }

            foreach (var c in parent.GetComponents<Component>())
            {
                if (c == null) continue;
                object p = GetProp(c, "isOn");
                if (p is bool) return p;
            }

            return null;
        }

        private static object IsOnIn(GameObject go, int depth)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                object v = GetProp(c, "isOn");
                if (v is bool) return v;
            }
            if (depth <= 0) return null;

            foreach (Transform child in go.transform)
            {
                object v = IsOnIn(child.gameObject, depth - 1);
                if (v != null) return v;
            }
            return null;
        }

        /// <summary>
        /// Walk outward from the focused control looking for the label of the row it sits in.
        ///
        /// Settings rows and key-binding rows are laid out as a container holding a label and a
        /// control side by side, so the label is a sibling, not a child. We check the parent's
        /// other children first, then the grandparent, and stop as soon as we find text that
        /// is not just a repeat of the control's own value.
        /// </summary>
        private static string FindRowLabel(GameObject go, string ownValue)
        {
            var candidates = LabelCandidates(go, ownValue);

            // A label is only trustworthy when there is exactly one thing it could be.
            //
            // A settings row is [label][control], so there is a single non-interactive text
            // beside the control and it is certainly the label. A save slot panel is nothing
            // like that: it holds a date, a play time, a device name, an islands-visited count
            // and several buttons all under one parent. Picking the first text there produced
            // readings like "Device Name: LOAD GAME". With more than one candidate we cannot
            // tell which is the label, so we say none and read the control on its own.
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Non-interactive sibling texts that could be this control's label.</summary>
        private static List<string> LabelCandidates(GameObject go, string ownValue)
        {
            var found = new List<string>();

            Transform t = go.transform;
            Transform parent = t.parent;
            if (parent == null) return found;

            foreach (Transform sibling in parent)
            {
                if (sibling == t) continue;
                if (!sibling.gameObject.activeInHierarchy) continue;

                // A sibling that is itself a control is a NEIGHBOURING MENU ITEM, not a label.
                if (IsInteractive(sibling.gameObject)) continue;

                string text = DeepText(sibling.gameObject, 3);
                if (string.IsNullOrEmpty(text)) continue;
                if (text == ownValue) continue;
                if (!LooksLikeLabel(text)) continue;
                if (found.Contains(text)) continue;

                found.Add(text);
            }
            return found;
        }

        /// <summary>
        /// Does this object carry a control? Anything with an "interactable" property is a Unity
        /// Selectable - button, toggle, slider, dropdown, scrollbar, input field.
        /// </summary>
        private static bool IsInteractive(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (GetProp(c, "interactable") != null) return true;
                string n = c.GetType().Name;
                if (n == "Button" || n == "Toggle" || n == "Slider" || n == "Scrollbar" ||
                    n == "Dropdown" || n == "TMP_Dropdown" || n == "InputField" ||
                    n == "TMP_InputField" || n == "Selectable") return true;
            }

            foreach (Transform child in go.transform)
                if (IsInteractive(child.gameObject)) return true;

            return false;
        }

        /// <summary>
        /// Reject stray one and two character strings picked up from decorative text, which
        /// produced readings like "M: GENERAL". A real row label is a word.
        /// </summary>
        private static bool LooksLikeLabel(string text)
        {
            string t = text.Trim();
            if (t.Length < 3) return false;

            bool hasLetter = false;
            foreach (char ch in t) if (char.IsLetter(ch)) { hasLetter = true; break; }
            return hasLetter;
        }

        /// <summary>
        /// Read a slider as a percentage.
        ///
        /// Found by capability, not by type name. The game's sliders are
        /// SliderWithLabelHighlight, which derives from UnityEngine.UI.Slider, so testing for
        /// the exact name "Slider" never matched and every volume row read without its value.
        /// Anything carrying value, minValue and maxValue is a slider as far as we care.
        /// </summary>
        internal static string SliderText(GameObject go)
        {
            var c = FindSlider(go);
            if (c == null) return null;

            try
            {
                double v = Convert.ToDouble(GetProp(c, "value"));
                double min = Convert.ToDouble(GetProp(c, "minValue"));
                double max = Convert.ToDouble(GetProp(c, "maxValue"));
                if (max <= min) return null;

                int pct = (int)Math.Round((v - min) / (max - min) * 100.0);

                // A slider in whole steps is reported as "10 of 15" rather than a percentage.
                //
                // That is deliberate. You change these with the arrow keys, one step per press,
                // so knowing there are fifteen steps and you are on the tenth tells you what a
                // press will do; "67 percent" does not. Percentages are used for continuous
                // sliders, where steps have no meaning. SliderAsPercent forces percentages.
                object whole = GetProp(c, "wholeNumbers");
                bool stepped = whole is bool && (bool)whole && max - min <= 30;

                if (stepped && !Cfg.SliderAsPercent.Value)
                    return Math.Round(v) + " of " + Math.Round(max);

                return pct + " percent";
            }
            catch { return null; }
        }

        /// <summary>The slider belonging to this row: on it, below it, beside it, or above it.</summary>
        private static Component FindSlider(GameObject go)
        {
            var c = SliderOn(go, 2);
            if (c != null) return c;

            Transform parent = go.transform.parent;
            if (parent == null) return null;

            foreach (Transform sib in parent)
            {
                if (sib == go.transform) continue;
                c = SliderOn(sib.gameObject, 2);
                if (c != null) return c;
            }
            return SliderOn(parent.gameObject, 0);
        }

        private static Component SliderOn(GameObject go, int depth)
        {
            if (go == null || !go.activeInHierarchy) return null;

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (GetProp(c, "value") == null) continue;
                if (GetProp(c, "minValue") == null) continue;
                if (GetProp(c, "maxValue") == null) continue;
                return c;
            }
            if (depth <= 0) return null;

            foreach (Transform child in go.transform)
            {
                var r = SliderOn(child.gameObject, depth - 1);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Search an object and its descendants for the first non-empty label.</summary>
        private static string DeepText(GameObject go, int depth)
        {
            if (!go.activeInHierarchy) return null;

            string t = TextOn(go);
            if (!string.IsNullOrEmpty(t)) return t;
            if (depth <= 0) return null;

            foreach (Transform child in go.transform)
            {
                string c = DeepText(child.gameObject, depth - 1);
                if (!string.IsNullOrEmpty(c)) return c;
            }
            return null;
        }

        /// <summary>
        /// Find a label on this object or below it. Any component with a public string "text"
        /// property counts, which catches UnityEngine.UI.Text, TextMeshProUGUI and TMP_Text.
        /// </summary>
        private static string FindText(GameObject go)
        {
            string best = TextOn(go);
            if (!string.IsNullOrEmpty(best)) return best;

            foreach (Transform child in go.transform)
            {
                string t = TextOn(child.gameObject);
                if (!string.IsNullOrEmpty(t)) return t;

                // One more level down covers the usual Button > Label > Text nesting.
                foreach (Transform grand in child)
                {
                    string g = TextOn(grand.gameObject);
                    if (!string.IsNullOrEmpty(g)) return g;
                }
            }
            return null;
        }

        private static string TextOn(GameObject go)
        {
            // Only read what is actually on screen.
            //
            // Unity menus are full of inactive template objects - a "Device Name" field that is
            // never shown, a "LOAD GAME" label sitting behind the visible "CONTINUE" one. They
            // are still present in the hierarchy and still carry text, so reading them produced
            // labels for things a sighted player cannot see at all.
            if (!go.activeInHierarchy) return null;

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string type = c.GetType().Name;
                if (type.Contains("InputField")) continue;   // handled separately as an edit box
                if (!type.Contains("Text") && type != "TextMeshProUGUI") continue;

                object v = GetProp(c, "text");
                if (!(v is string)) continue;

                // Strip here rather than at the call sites. TextMeshPro text carries markup such
                // as <font="LondrinaSolid-Regular SDF">, which a screen reader reads out
                // character by character. Doing it at this one choke point means every path -
                // labels, values, whole-panel reads - is covered.
                string text = Rich.Strip((string)v);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            return null;
        }

        private static object GetProp(object o, string name)
        {
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance);
                return p == null ? null : p.GetValue(o, null);
            }
            catch { return null; }
        }

        /// <summary>"MainMenuStartButton" -> "Main Menu Start Button", as a last resort.</summary>
        private static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (ch == '_' || ch == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(ch) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
