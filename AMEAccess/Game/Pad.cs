using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// Controller support: the right stick drives the review cursor, buttons run mod actions.
    ///
    /// The left stick is left alone - the game already moves your monster with it, and undo,
    /// reset and the rest are the game's own controller bindings. This adds only what the game
    /// has no equivalent for.
    ///
    /// Everything here goes through reflection rather than a reference to Rewired_Core. The game
    /// uses Rewired for input, so the API is certainly present, but its exact shape cannot be
    /// checked from here - and a wrong guess in a reference is a BUILD failure, where a wrong
    /// guess in reflection is a feature that quietly does nothing. The log says which it was.
    ///
    /// Axis and button numbers differ between controllers, and no table of them can be trusted.
    /// So there is a discovery mode instead: it says what you just moved or pressed, and you
    /// write that number into the config. That cannot be wrong about your hardware.
    /// </summary>
    internal static class Pad
    {
        private static bool _tried;
        private static object _controllers;        // Rewired.ControllerHelper
        private static PropertyInfo _pJoysticks;
        private static MethodInfo _mAxisRaw, _mButtonDown;
        private static PropertyInfo _pAxisCount, _pButtonCount, _pName, _pConnected;

        private static string _why = "not started";
        internal static string Why => _why;

        // ---------------------------------------------------------------- finding Rewired

        private static void Init()
        {
            if (_tried) return;
            _tried = true;

            try
            {
                Type reInput = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    reInput = asm.GetType("Rewired.ReInput", false);
                    if (reInput != null) break;
                }
                if (reInput == null) { _why = "Rewired not found"; return; }

                var isReady = reInput.GetProperty("isReady", BindingFlags.Public | BindingFlags.Static);
                if (isReady != null && !(bool)isReady.GetValue(null, null))
                {
                    _tried = false;                 // try again next frame
                    _why = "Rewired not ready yet";
                    return;
                }

                var pControllers = reInput.GetProperty("controllers", BindingFlags.Public | BindingFlags.Static);
                if (pControllers == null) { _why = "no controllers property"; return; }

                _controllers = pControllers.GetValue(null, null);
                if (_controllers == null) { _why = "controllers was null"; return; }

                _pJoysticks = _controllers.GetType().GetProperty("Joysticks");
                if (_pJoysticks == null) { _why = "no Joysticks list"; return; }

                _why = "ready";
            }
            catch (Exception e) { _why = e.GetType().Name + ": " + e.Message; }
        }

        private static object FirstJoystick()
        {
            Init();
            if (_pJoysticks == null) return null;

            try
            {
                var list = _pJoysticks.GetValue(_controllers, null) as IList;
                if (list == null || list.Count == 0) return null;

                foreach (var js in list)
                {
                    if (js == null) continue;
                    if (_pConnected == null) Bind(js.GetType());

                    if (_pConnected != null && !(bool)_pConnected.GetValue(js, null)) continue;
                    return js;
                }
            }
            catch { }
            return null;
        }

        internal static void ForgetController()
        {
            _templateTried = false; _rightStick = null; _pStickValue = null;
            _calibrated = false; _rest = null; _checked = false; _repaired = false;
            _assignTried = false;
        }

        private static void Bind(Type t)
        {
            _mAxisRaw = t.GetMethod("GetAxisRaw", new[] { typeof(int) });
            _mButtonDown = t.GetMethod("GetButtonDown", new[] { typeof(int) });
            _pAxisCount = t.GetProperty("axisCount");
            _pButtonCount = t.GetProperty("buttonCount");
            _pName = t.GetProperty("name");
            _pConnected = t.GetProperty("isConnected");
        }

        // ---------------------------------------------------------------- resting values
        //
        // An axis at rest is not necessarily zero. A DualSense reports its triggers as -1.00
        // when untouched, so reading one as a stick means a permanent full push in one
        // direction - which is exactly what happened: the cursor ran away saying "water, water,
        // water" with the controller sitting still.
        //
        // So every axis is sampled once while nothing is being touched, and what counts as
        // input afterwards is movement AWAY from that resting value. A trigger then reads as
        // zero until it is actually pulled, whatever its resting number happens to be.
        private static float[] _rest;
        private static bool _calibrated;
        private static object _calibratedFor;     // the controller it was taken from
        private static int _settleFrames;

        /// <summary>
        /// Record where every axis sits when nothing is being touched.
        ///
        /// Two things make this harder than it sounds, and both were caught in the logs:
        ///
        /// A controller does not report real values the instant it appears. Sampling too early
        /// gives all zeros, and then a trigger resting at -1 looks like a full push for ever
        /// after. So sampling waits a moment, and a reading of exactly zero on every axis is
        /// rejected as "not ready yet" rather than believed.
        ///
        /// And a controller that disconnects and reconnects is a new device with its own
        /// resting values, so the old ones are thrown away rather than carried over.
        /// </summary>
        private static void Calibrate(object js)
        {
            if (js != _calibratedFor) { _calibrated = false; _calibratedFor = js; _settleFrames = 0; }
            if (_calibrated) return;

            if (_settleFrames++ < 30) return;      // let Rewired settle before believing it

            int count = 0;
            try { if (_pAxisCount != null) count = (int)_pAxisCount.GetValue(js, null); }
            catch { return; }
            if (count <= 0) return;

            var sample = new float[count];
            bool anythingAtAll = false;
            for (int i = 0; i < count; i++)
            {
                sample[i] = Raw(js, i);
                if (Mathf.Abs(sample[i]) > 0.001f) anythingAtAll = true;
            }

            // Every axis at exactly zero means the device is not reporting yet. A real pad has
            // triggers that rest away from zero, or at least some noise on the sticks.
            if (!anythingAtAll && _settleFrames < 300) return;

            _rest = sample;
            _calibrated = true;

            var log = Plugin.Log;
            if (log != null)
            {
                var sb = new StringBuilder("[pad] centred: ");
                for (int i = 0; i < count; i++)
                    if (Mathf.Abs(sample[i]) > 0.1f) sb.Append(i).Append('=').Append(sample[i].ToString("0.00")).Append(' ');
                log.LogInfo(sb.ToString());
            }
        }

        internal static string Recalibrate()
        {
            _calibrated = false;
            _calibratedFor = null;
            _settleFrames = 30;
            var js = FirstJoystick();
            if (js == null) return "No controller found.";

            Calibrate(js);
            return _calibrated
                ? "Controller centred. Let go of everything before pressing this."
                : "Could not read the controller's axes.";
        }

        /// <summary>The raw value, straight from Rewired.</summary>
        private static float Raw(object js, int index)
        {
            if (js == null || _mAxisRaw == null || index < 0) return 0f;
            try { return (float)_mAxisRaw.Invoke(js, new object[] { index }); }
            catch { return 0f; }
        }

        private static float Axis(object js, int index)
        {
            if (index < 0) return 0f;

            Calibrate(js);
            float v = Raw(js, index);
            if (_rest != null && index < _rest.Length) v -= _rest[index];

            // Taking the rest off can push the range past 1; nothing here cares about more.
            return Mathf.Clamp(v, -1f, 1f);
        }

        private static bool ButtonDown(object js, int index)
        {
            if (js == null || _mButtonDown == null || index < 0) return false;

            // Each index is asked for separately and a failure on one is ignored, because a pad
            // can report far more buttons than it physically has - this one claims 144 - and a
            // throw on a high index must not stop the scan before it reaches the real ones.
            try { return (bool)_mButtonDown.Invoke(js, new object[] { index }); }
            catch { return false; }
        }

        private static MethodInfo _mAnyButtonDown;
        private static bool _anyTried;

        /// <summary>
        /// Rewired's own "did anything get pressed" check.
        ///
        /// Worth having beside the per-button loop: if this says yes while the loop finds
        /// nothing, the fault is in how buttons are being read, not in whether you pressed one.
        /// That is a distinction the speech alone could never make.
        /// </summary>
        private static bool AnyButtonDown(object js, bool probeOnly = false)
        {
            if (!_anyTried)
            {
                _anyTried = true;
                try { _mAnyButtonDown = js.GetType().GetMethod("GetAnyButtonDown", Type.EmptyTypes); }
                catch { }
            }
            if (_mAnyButtonDown == null) return false;
            if (probeOnly) return true;

            try { return (bool)_mAnyButtonDown.Invoke(js, null); }
            catch { return false; }
        }

        // ---------------------------------------------------------------- known controllers
        //
        // Rewired has no template for every pad - it has none for a DualSense - so without help
        // the stick stays unbound until someone runs the controller test and edits a file.
        //
        // These are the numbers that test produced on real hardware. They are applied ONLY
        // where the setting is still unbound, so anything you set yourself always wins, and
        // editing the file is never undone by an update.
        private struct Profile
        {
            public string Match;     // part of the controller's name, lower case
            public int StickX, StickY;
            public bool InvertY;
            public int Face0, Face1, Face2, Face3, ShoulderL, ShoulderR;
            public int StickClickL, StickClickR;
            public int LeftX, LeftY;
        }

        private static readonly Profile[] Known =
        {
            // Measured from a DualSense: axes 0 and 1 are the left stick, 3 and 4 are the
            // triggers resting at -1, and the right stick is 2 and 5.
            new Profile { Match = "dualsense", StickX = 2, StickY = 5, LeftX = 0, LeftY = 1, InvertY = true,
                          Face0 = 0, Face1 = 1, Face2 = 2, Face3 = 3, ShoulderL = 4, ShoulderR = 5,
                          StickClickL = 10, StickClickR = 11 },

            new Profile { Match = "dualshock", StickX = 2, StickY = 5, LeftX = 0, LeftY = 1, InvertY = true,
                          Face0 = 0, Face1 = 1, Face2 = 2, Face3 = 3, ShoulderL = 4, ShoulderR = 5,
                          StickClickL = 10, StickClickR = 11 },

            new Profile { Match = "xbox", StickX = 3, StickY = 4, LeftX = 0, LeftY = 1, InvertY = true,
                          Face0 = 0, Face1 = 1, Face2 = 2, Face3 = 3, ShoulderL = 4, ShoulderR = 5,
                          StickClickL = 8, StickClickR = 9 },
        };

        /// <summary>
        /// Fill in again after a bad axis has been cleared, so a recognised controller repairs
        /// itself rather than leaving the stick dead until someone runs the setup.
        /// </summary>
        private static void ApplyProfileAgain(object js)
        {
            if (!_checked || _repaired) return;
            _repaired = true;

            // The left stick counts too. It was left out, so if its axis was ever cleared -
            // by the trigger check, or by hand - nothing put it back and walking simply stopped.
            bool allSet = Cfg.PadRightStickX.Value >= 0 && Cfg.PadRightStickY.Value >= 0
                       && Cfg.PadLeftStickX.Value >= 0 && Cfg.PadLeftStickY.Value >= 0;
            if (allSet) return;

            _profileTried = false;
            ApplyProfile(js);

            // A pad with no profile still needs a left stick, and axes 0 and 1 are the left
            // stick on every controller seen so far. Better a sensible guess than no walking.
            if (Cfg.PadLeftStickX.Value < 0 && LooksLikeStick(0)) Cfg.PadLeftStickX.Value = 0;
            if (Cfg.PadLeftStickY.Value < 0 && LooksLikeStick(1)) Cfg.PadLeftStickY.Value = 1;
            Cfg.Save();
        }

        /// <summary>
        /// Could this axis be a stick? A resting value near the end of its travel says no: that
        /// is a trigger. Unknown resting values are allowed through, since refusing everything
        /// would leave the stick unusable.
        /// </summary>
        private static bool LooksLikeStick(int axis)
        {
            if (_rest == null || axis < 0 || axis >= _rest.Length) return true;
            return Mathf.Abs(_rest[axis]) < 0.8f;
        }

        private static bool _repaired;
        private static bool _profileTried;
        private static string _profileUsed = "none";
        internal static string ProfileUsed => _profileUsed;

        private static void ApplyProfile(object js)
        {
            if (_profileTried) return;
            _profileTried = true;

            string name;
            try { name = ((_pName.GetValue(js, null) as string) ?? "").ToLowerInvariant(); }
            catch { return; }

            foreach (var p in Known)
            {
                if (!name.Contains(p.Match)) continue;

                _profileUsed = p.Match;

                // Unbound only. A number you chose is never overwritten.
                // Only if the axis really looks like a stick. The numbers a controller uses
                // are not fixed between runs on every system, so a profile value can land on a
                // trigger - and a trigger never passes through the middle, so that direction
                // just dies silently.
                if (Cfg.PadLeftStickX.Value < 0 && LooksLikeStick(p.LeftX)) Cfg.PadLeftStickX.Value = p.LeftX;
                if (Cfg.PadLeftStickY.Value < 0 && LooksLikeStick(p.LeftY)) Cfg.PadLeftStickY.Value = p.LeftY;
                if (Cfg.PadRightStickX.Value < 0 && LooksLikeStick(p.StickX)) Cfg.PadRightStickX.Value = p.StickX;
                if (Cfg.PadRightStickY.Value < 0 && LooksLikeStick(p.StickY)) Cfg.PadRightStickY.Value = p.StickY;
                if (Cfg.PadLookAround.Value < 0) Cfg.PadLookAround.Value = p.Face0;
                if (Cfg.PadSurvey.Value < 0) Cfg.PadSurvey.Value = p.Face1;
                if (Cfg.PadPosition.Value < 0) Cfg.PadPosition.Value = p.Face2;
                if (Cfg.PadInspect.Value < 0) Cfg.PadInspect.Value = p.Face3;
                if (Cfg.PadPrevItem.Value < 0) Cfg.PadPrevItem.Value = p.ShoulderL;
                if (Cfg.PadNextItem.Value < 0) Cfg.PadNextItem.Value = p.ShoulderR;
                if (Cfg.PadStickClick.Value < 0) Cfg.PadStickClick.Value = p.StickClickL;
                if (Cfg.PadMod1.Value < 0) Cfg.PadMod1.Value = p.ShoulderL;
                if (Cfg.PadMod2.Value < 0) Cfg.PadMod2.Value = p.ShoulderR;

                // Up reads as negative on these pads, so the cursor would go the wrong way.
                if (p.InvertY && !Cfg.PadInvertSet.Value)
                {
                    Cfg.PadInvertY.Value = true;
                    Cfg.PadInvertSet.Value = true;
                }

                Cfg.Save();

                var log = Plugin.Log;
                if (log != null)
                    log.LogInfo("[pad] recognised a " + p.Match + " and filled in the unset numbers.");
                return;
            }

            _profileUsed = "unrecognised: " + name;
        }

        // ---------------------------------------------------------------- the game's own input
        //
        // The game does not respond to this pad at all - not just the mod, the game. Rewired has
        // no template for it, and a controller Rewired does not recognise may never be assigned
        // to the player the game reads from, so the game sees nothing from it.
        //
        // Rewired lets that be put right: a player's controller list is public, and so is adding
        // to it. If the game's player does not have the pad, it is handed over. That fixes the
        // game's own movement and buttons rather than the mod reimplementing them, which is far
        // better - the game knows what its buttons do and the mod does not.
        //
        // It may not be enough on its own: without a template there may also be no map saying
        // which stick means what. The log reports what happened either way, so the next step is
        // based on fact.
        private static bool _assignTried;
        private static string _assignResult = "not tried";
        internal static string AssignResult => _assignResult;

        private static void AssignToGame(object js)
        {
            if (_assignTried || !Cfg.PadAssignToGame.Value) return;
            _assignTried = true;

            try
            {
                var globals = Type.GetType("Globals, Assembly-CSharp");
                if (globals == null)
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        globals = asm.GetType("Globals", false);
                        if (globals != null) break;
                    }
                if (globals == null) { _assignResult = "no Globals"; return; }

                var pRewired = globals.GetProperty("rewired",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (pRewired == null) { _assignResult = "no Globals.rewired"; return; }

                var player = pRewired.GetValue(null, null);
                if (player == null) { _assignResult = "the game has no Rewired player yet"; _assignTried = false; return; }

                var fControllers = player.GetType().GetField("controllers");
                if (fControllers == null) { _assignResult = "no player.controllers"; return; }

                var helper = fControllers.GetValue(player);
                if (helper == null) { _assignResult = "player.controllers was null"; return; }

                var ht = helper.GetType();

                int before = 0;
                var pCount = ht.GetProperty("joystickCount");
                if (pCount != null) before = (int)pCount.GetValue(helper, null);

                var mContains = ht.GetMethod("ContainsController", new[] { js.GetType().BaseType ?? js.GetType() })
                             ?? ht.GetMethod("ContainsController", new[] { js.GetType() });

                bool has = false;
                if (mContains != null)
                {
                    try { has = (bool)mContains.Invoke(helper, new[] { js }); } catch { }
                }

                if (has) { _assignResult = "already assigned, " + before + " joysticks"; return; }

                var mAdd = ht.GetMethod("AddController", new[] { js.GetType().BaseType ?? js.GetType(), typeof(bool) })
                        ?? ht.GetMethod("AddController", new[] { js.GetType(), typeof(bool) });

                if (mAdd == null) { _assignResult = "AddController not found"; return; }

                mAdd.Invoke(helper, new object[] { js, true });

                int after = before;
                if (pCount != null) after = (int)pCount.GetValue(helper, null);

                _assignResult = "assigned to the game, joysticks " + before + " to " + after;

                var log = Plugin.Log;
                if (log != null) log.LogInfo("[pad] " + _assignResult);
            }
            catch (Exception e) { _assignResult = e.GetType().Name + ": " + e.Message; }
        }

        // ---------------------------------------------------------------- sanity
        //
        // A number already in the config is never overwritten by a profile, which is right
        // almost always and wrong in one case: when the number is left over from an older
        // default that happens to be a trigger. A trigger rests at -1 and never passes through
        // the middle, so the stick simply does nothing in that direction and nothing in the
        // log says why.
        //
        // A resting value near the end of its travel is the giveaway. No stick rests there.
        private static bool _checked;

        private static void CheckStickAxes(object js)
        {
            if (_checked || !_calibrated || _rest == null) return;
            _checked = true;

            bool fixedX = Reject(Cfg.PadRightStickX, "left and right");
            bool fixedY = Reject(Cfg.PadRightStickY, "up and down");
            Reject(Cfg.PadLeftStickX, "walking, left and right");
            Reject(Cfg.PadLeftStickY, "walking, up and down");

            if (fixedX || fixedY)
            {
                Cfg.Save();
                Talk.Explicit("The right stick was set to a trigger, which cannot work, so it "
                            + "has been cleared. Press the controller setup key, F7, and push "
                            + "the stick to set it.");
            }
        }

        private static bool Reject(BepInEx.Configuration.ConfigEntry<int> entry, string which)
        {
            int axis = entry.Value;
            if (axis < 0 || axis >= _rest.Length) return false;
            if (Mathf.Abs(_rest[axis]) < 0.8f) return false;

            var log = Plugin.Log;
            if (log != null)
                log.LogWarning("[pad] axis " + axis + " is set for the right stick " + which
                             + ", but it rests at " + _rest[axis].ToString("0.00")
                             + ", so it is a trigger, not a stick. Clearing it.");

            entry.Value = -1;
            return true;
        }

        // ---------------------------------------------------------------- the right stick

        private static PropertyInfo _pTemplates;
        private static bool _templateTried;
        private static object _rightStick;          // IControllerTemplateThumbStick
        private static PropertyInfo _pStickValue;   // its value, a Vector2
        private static string _stickSource = "not looked for";

        internal static string StickSource => _stickSource;

        /// <summary>
        /// Find the right stick through Rewired's controller templates.
        ///
        /// Templates are how Rewired describes a pad in terms a person would use: every element
        /// has a descriptive name, and a thumb stick hands back its position as a Vector2. So
        /// the stick can be found by name and read directly, and the axis numbers in the config
        /// are only needed when a controller has no template - which is the unusual case, not
        /// the normal one.
        /// </summary>
        private static void FindRightStick(object js)
        {
            if (_templateTried) return;
            _templateTried = true;

            try
            {
                if (_pTemplates == null) _pTemplates = js.GetType().GetProperty("Templates");
                if (_pTemplates == null) { _stickSource = "controller has no templates"; return; }

                var templates = _pTemplates.GetValue(js, null) as IList;
                if (templates == null || templates.Count == 0)
                {
                    _stickSource = "no template for this controller, using axis numbers";
                    return;
                }

                foreach (var template in templates)
                {
                    if (template == null) continue;

                    var pElements = template.GetType().GetProperty("elements");
                    if (pElements == null) continue;

                    var elements = pElements.GetValue(template, null) as IList;
                    if (elements == null) continue;

                    foreach (var el in elements)
                    {
                        if (el == null) continue;

                        var pName = el.GetType().GetProperty("descriptiveName");
                        if (pName == null) continue;

                        string name = (pName.GetValue(el, null) as string ?? "").ToLowerInvariant();
                        if (!name.Contains("right") || !name.Contains("stick")) continue;

                        // A thumb stick reports its position as a Vector2; anything else here
                        // (a press, a single axis) is not what we want.
                        var pValue = el.GetType().GetProperty("value");
                        if (pValue == null || pValue.PropertyType != typeof(Vector2)) continue;

                        _rightStick = el;
                        _pStickValue = pValue;
                        _stickSource = "template: " + (pName.GetValue(el, null) as string);
                        return;
                    }
                }

                _stickSource = "no right stick in the template, using axis numbers";
            }
            catch (Exception e) { _stickSource = "template lookup failed: " + e.Message; }
        }

        /// <summary>The right stick, from the template when there is one, else by axis number.</summary>
        private static Vector2 RightStick(object js)
        {
            FindRightStick(js);

            if (_rightStick != null && _pStickValue != null)
            {
                try { return (Vector2)_pStickValue.GetValue(_rightStick, null); }
                catch { }
            }

            return new Vector2(Axis(js, Cfg.PadRightStickX.Value), Axis(js, Cfg.PadRightStickY.Value));
        }

        // ---------------------------------------------------------------- the stick as a cursor

        private static float _nextStep;
        private static Vector3i _lastDir;

        /// <summary>
        /// Turn the right stick into steps rather than a glide.
        ///
        /// The cursor moves a tile at a time and each tile is spoken, so a stick held over has
        /// to behave like a held key: one step, a pause, then a steady repeat. Without that it
        /// would cross the island in a few frames and queue thirty utterances.
        /// </summary>
        private static void Stick(object js)
        {
            // Unbound means unbound: with no axis numbers set and no template to fall back on,
            // the stick is left alone rather than guessed at.
            if (_rightStick == null
                && (Cfg.PadRightStickX.Value < 0 || Cfg.PadRightStickY.Value < 0)) return;

            var stick = RightStick(js);
            float x = stick.x;
            float y = stick.y;
            if (Cfg.PadInvertX.Value) x = -x;
            if (Cfg.PadInvertY.Value) y = -y;

            float dead = Cfg.PadDeadzone.Value;
            Vector3i dir = default(Vector3i);

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                if (x > dead) dir = Vector3i.east;
                else if (x < -dead) dir = Vector3i.west;
            }
            else
            {
                if (y > dead) dir = Vector3i.north;
                else if (y < -dead) dir = Vector3i.south;
            }

            if (dir == default(Vector3i)) { _lastDir = dir; return; }

            float now = Time.unscaledTime;

            // A new direction moves at once; the same direction waits, then repeats.
            if (!(dir == _lastDir)) { _lastDir = dir; _nextStep = now + Cfg.PadRepeatDelay.Value; }
            else if (now < _nextStep) return;
            else _nextStep = now + Cfg.PadRepeatRate.Value;

            Talk.Explicit(ReviewCursor.Move(dir, 1));
        }

        // ---------------------------------------------------------------- the warp map
        //
        // The bracket keys mean two different things depending on where you are: objects on an
        // island, destinations on the warp map. The pad buttons have to follow, or a button
        // called "next object" would step through an island's trees while you were looking at
        // a map of the world.
        //
        // The warp map is handled before menus because it has its own list and its own idea of
        // what is selected; letting the menu navigation loose on it would move Unity's
        // selection out from under it.

        private static float _nextWarpStep;
        private static Vector3i _lastWarpDir;

        private static bool WarpMapInput(object js)
        {
            if (!WarpReader.Active) return false;

            if (ButtonDown(js, Cfg.PadNextItem.Value)) { Talk.Explicit(WarpReader.Step(1)); return true; }
            if (ButtonDown(js, Cfg.PadPrevItem.Value)) { Talk.Explicit(WarpReader.Step(-1)); return true; }
            if (ButtonDown(js, Cfg.PadNextCategory.Value)) { Talk.Explicit(WarpReader.CycleCategory(1)); return true; }
            if (ButtonDown(js, Cfg.PadPrevCategory.Value)) { Talk.Explicit(WarpReader.CycleCategory(-1)); return true; }

            if (ButtonDown(js, Cfg.PadStickClick.Value) || ButtonDown(js, Cfg.PadInspect.Value))
            { Talk.Explicit(WarpReader.Activate()); return true; }

            if (ButtonDown(js, Cfg.PadSurvey.Value)) { Talk.Explicit(WarpReader.List()); return true; }
            if (ButtonDown(js, Cfg.PadPosition.Value)) { Talk.Explicit(WarpReader.Current()); return true; }
            if (ButtonDown(js, Cfg.PadRoute.Value)) { Talk.Explicit(WarpReader.RouteStatus()); return true; }

            // Right stick steps destinations, left stick changes the group - the same two axes
            // the brackets and the page keys give you.
            Vector3i right = StickDirection(js, Cfg.PadRightStickX.Value, Cfg.PadRightStickY.Value);
            Vector3i left = StickDirection(js, Cfg.PadLeftStickX.Value, Cfg.PadLeftStickY.Value);

            Vector3i dir = default(Vector3i);
            bool byGroup = false;

            if (!(right == default(Vector3i))) dir = right;
            else if (!(left == default(Vector3i))) { dir = left; byGroup = true; }

            if (dir == default(Vector3i)) { _lastWarpDir = dir; return true; }

            float now = Time.unscaledTime;
            if (!(dir == _lastWarpDir)) { _lastWarpDir = dir; _nextWarpStep = now + Cfg.PadRepeatDelay.Value; }
            else if (now < _nextWarpStep) return true;
            else _nextWarpStep = now + Cfg.PadRepeatRate.Value;

            int step = (dir == Vector3i.north || dir == Vector3i.east) ? 1 : -1;
            Talk.Explicit(byGroup ? WarpReader.CycleCategory(step) : WarpReader.Step(step));
            return true;
        }

        /// <summary>A stick reduced to one of four directions, or nothing.</summary>
        private static Vector3i StickDirection(object js, int ax, int ay)
        {
            if (ax < 0 || ay < 0) return default(Vector3i);

            float x = Axis(js, ax);
            float y = Axis(js, ay);
            if (Cfg.PadInvertY.Value) y = -y;

            float dead = Cfg.PadDeadzone.Value;

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                if (x > dead) return Vector3i.east;
                if (x < -dead) return Vector3i.west;
            }
            else
            {
                if (y > dead) return Vector3i.north;
                if (y < -dead) return Vector3i.south;
            }
            return default(Vector3i);
        }

        // ---------------------------------------------------------------- menus
        //
        // The same reason the left stick does not walk: with no map, the game's menus get
        // nothing from this pad either. So the mod moves the selection itself, through Unity's
        // own navigation - FindSelectableOnUp and its siblings are what the keyboard arrows use,
        // so the stick follows exactly the order the menu was designed with.
        //
        // The menu reader then speaks whatever became selected, because it watches the
        // selection rather than the keys that changed it. Nothing extra is needed for that.

        private static float _nextMenuStep;
        private static Vector3i _lastMenuDir;

        private static bool Menus(object js)
        {
            if (!Cfg.PadMenus.Value) return false;

            // A menu is open when you are not on the board.
            //
            // This was working and I replaced it with a GameState check to cover the pause menu
            // opening over gameplay - a case I had never actually seen. It broke walking: the
            // check read as "in a menu" during normal play, and this method returning true
            // swallows every press. Reverted.
            //
            // If the pause menu ever does turn out to leave the board live underneath, the fix
            // is to prove that first with a log, not to guess at it again.
            if (Refs.InPlay) return false;

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;

            var selected = es.currentSelectedGameObject;
            if (selected == null) return false;

            // Anything that means "choose" works here, not only the stick click. That starts
            // unbound, so insisting on it left a player able to move around a menu and unable
            // to pick anything, which is worse than no menu support at all.
            if (ChoosePressed(js)) { Activate(selected); return true; }

            int ax = Cfg.PadLeftStickX.Value, ay = Cfg.PadLeftStickY.Value;
            if (ax < 0 || ay < 0) return false;

            float x = Axis(js, ax);
            float y = Axis(js, ay);
            if (Cfg.PadInvertY.Value) y = -y;

            float dead = Cfg.PadDeadzone.Value;
            Vector3i dir = default(Vector3i);

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                if (x > dead) dir = Vector3i.east;
                else if (x < -dead) dir = Vector3i.west;
            }
            else
            {
                if (y > dead) dir = Vector3i.north;
                else if (y < -dead) dir = Vector3i.south;
            }

            if (dir == default(Vector3i)) { _lastMenuDir = dir; return true; }

            float now = Time.unscaledTime;
            if (!(dir == _lastMenuDir)) { _lastMenuDir = dir; _nextMenuStep = now + Cfg.PadRepeatDelay.Value; }
            else if (now < _nextMenuStep) return true;
            else _nextMenuStep = now + Cfg.PadRepeatRate.Value;

            Navigate(es, selected, dir);
            return true;
        }

        /// <summary>
        /// Nudge a slider. True when there was one, so the caller knows not to move the
        /// selection as well.
        ///
        /// A slider in whole steps moves by one; a continuous one by a twentieth of its range,
        /// which is about the same feel. The menu reader notices the change and says the new
        /// value, so nothing needs announcing here.
        /// </summary>
        private static bool AdjustSlider(GameObject selected, bool up)
        {
            var c = MenuReader.FindSlider(selected);
            if (c == null) return false;

            try
            {
                var pValue = c.GetType().GetProperty("value");
                var pMin = c.GetType().GetProperty("minValue");
                var pMax = c.GetType().GetProperty("maxValue");
                if (pValue == null || pMin == null || pMax == null) return false;

                float v = Convert.ToSingle(pValue.GetValue(c, null));
                float min = Convert.ToSingle(pMin.GetValue(c, null));
                float max = Convert.ToSingle(pMax.GetValue(c, null));
                if (max <= min) return false;

                float step = (max - min) / 20f;

                var pWhole = c.GetType().GetProperty("wholeNumbers");
                if (pWhole != null)
                {
                    object w = pWhole.GetValue(c, null);
                    if (w is bool && (bool)w) step = 1f;    // one notch per push
                }

                float next = Mathf.Clamp(v + (up ? step : -step), min, max);
                pValue.SetValue(c, next, null);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Did anything that means "choose" get pressed?</summary>
        private static bool ChoosePressed(object js)
        {
            if (Cfg.PadStickClick.Value >= 0 && ButtonDown(js, Cfg.PadStickClick.Value)) return true;

            BuildTable();
            int layer = Layer(js);

            foreach (var action in PadActions.All)
            {
                if (action.Name != "choose" && action.Name != "full detail") continue;

                var b = PadActions.Parse(action.Entry.Value);
                if (!b.Set || b.Layer != layer) continue;
                if (ButtonDown(js, b.Button)) return true;
            }
            return false;
        }

        private static void Navigate(UnityEngine.EventSystems.EventSystem es,
                                     GameObject selected, Vector3i dir)
        {
            // On a slider, left and right change the value rather than move to another row -
            // which is what the arrow keys do there, and the only way to set the volume.
            if ((dir == Vector3i.east || dir == Vector3i.west) && AdjustSlider(selected, dir == Vector3i.east))
                return;

            var sel = selected.GetComponent<UnityEngine.UI.Selectable>();
            if (sel == null) return;

            UnityEngine.UI.Selectable next = null;
            try
            {
                if (dir == Vector3i.north) next = sel.FindSelectableOnUp();
                else if (dir == Vector3i.south) next = sel.FindSelectableOnDown();
                else if (dir == Vector3i.west) next = sel.FindSelectableOnLeft();
                else if (dir == Vector3i.east) next = sel.FindSelectableOnRight();
            }
            catch { }

            if (next == null) return;
            try { es.SetSelectedGameObject(next.gameObject); } catch { }
        }

        /// <summary>Press whatever is selected: a button, or a tick box.</summary>
        private static void Activate(GameObject selected)
        {
            try
            {
                var button = selected.GetComponent<UnityEngine.UI.Button>();
                if (button != null) { button.onClick.Invoke(); return; }

                var toggle = selected.GetComponent<UnityEngine.UI.Toggle>();
                if (toggle != null) { toggle.isOn = !toggle.isOn; return; }
            }
            catch { }

            // Not a plain Button: send Unity's submit event, which is what the enter key
            // raises, so a menu item built from something else still answers.
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var data = new UnityEngine.EventSystems.PointerEventData(es);
                UnityEngine.EventSystems.ExecuteEvents.Execute(
                    selected, data, UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            }
            catch { }
        }

        // ---------------------------------------------------------------- the left stick
        //
        // The game cannot use this controller even with the pad handed to it: Rewired has no
        // template for it, so there is no map telling the game which stick means "move". The
        // mod therefore issues the move itself.
        //
        // It does so exactly as the game does. GridKeyboardInput.StartMoving ends in
        //     SetQueuedMove(direction, true, repeat, not riding a raft, false, sitting check)
        // and those are the values used here, rather than guessed at - which matters, because
        // this is the one place the mod drives the game instead of reading it.

        private static float _nextWalk;
        private static Vector3i _lastWalkDir;

        private static void LeftStick(object js)
        {
            if (!Cfg.PadMovesMonster.Value) return;

            int ax = Cfg.PadLeftStickX.Value, ay = Cfg.PadLeftStickY.Value;
            if (ax < 0 || ay < 0) return;

            float x = Axis(js, ax);
            float y = Axis(js, ay);
            if (Cfg.PadInvertY.Value) y = -y;

            float dead = Cfg.PadDeadzone.Value;
            Vector3i dir = default(Vector3i);

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                if (x > dead) dir = Vector3i.east;
                else if (x < -dead) dir = Vector3i.west;
            }
            else
            {
                if (y > dead) dir = Vector3i.north;
                else if (y < -dead) dir = Vector3i.south;
            }

            if (dir == default(Vector3i)) { _lastWalkDir = dir; return; }

            float now = Time.unscaledTime;
            if (!(dir == _lastWalkDir)) { _lastWalkDir = dir; _nextWalk = now + Cfg.PadRepeatDelay.Value; }
            else if (now < _nextWalk) return;
            else _nextWalk = now + Cfg.PadWalkRate.Value;

            Walk(dir);
        }

        private static void Walk(Vector3i dir)
        {
            var move = Refs.PlayerMove;
            if (move == null) return;

            try
            {
                bool onRaft = false;
                try { onRaft = GameState.Is(GameState.RidingRaft); } catch { }

                move.SetQueuedMove(dir, true, false, !onRaft, false, false);
            }
            catch (Exception e)
            {
                var log = Plugin.Log;
                if (log != null) log.LogWarning("[pad] could not move: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- buttons

        private static bool _tableBuilt;

        /// <summary>
        /// Hook every binding up to what it does. Built once, from the same list the config and
        /// the setup screen use, so an action cannot exist in one and be missing from another.
        /// </summary>
        internal static void EnsureTable() { BuildTable(); }

        private static void BuildTable()
        {
            if (_tableBuilt) return;
            _tableBuilt = true;

            var B = Cfg.PadBindings;
            Action a = PadActions.Add;

            a("choose", B["Choose"], Choose);
            a("undo", B["Undo"], Undo);
            a("look around", B["LookAround"], () => Describe.LookAround());
            a("the menu", B["Menu"], ToggleMenu);

            a("island survey", B["IslandSurvey"], () => Survey.IslandSurvey());
            a("your position", B["Position"], () => Describe.WhereAmI());
            a("facing", B["Facing"], () => Describe.Facing());
            a("distance scan", B["DistanceScan"], () => Survey.Scan());
            a("island info", B["IslandInfo"], () => Plugin.IslandInfo());
            a("status", B["Status"], () => Plugin.Status());
            a("stats", B["Stats"], () => Stats.Summary());
            a("reset the island", B["ResetIsland"], ResetIsland);

            a("next object", B["NextObject"], () => Survey.CycleNext(1));
            a("previous object", B["PreviousObject"], () => Survey.CycleNext(-1));
            a("next group", B["NextGroup"], () => Survey.CycleCategory(1));
            a("previous group", B["PreviousGroup"], () => Survey.CycleCategory(-1));
            a("full detail", B["FullDetail"], () => Plugin.InspectTarget());
            a("this island only", B["ThisIslandOnly"], () => Survey.ToggleScope());

            a("cursor back to you", B["CursorHome"], () => ReviewCursor.Home());
            a("read the column", B["ReadColumn"], () => ReviewCursor.ReadColumn());
            a("route to the cursor", B["RouteToCursor"], () => ReviewCursor.RouteHere());
            a("cursor to the object", B["JumpCursorToObject"], () => Plugin.JumpToSelected());

            a("nearby islands", B["NearbyIslands"], () => Survey.NearbyIslands());
            a("next island", B["NextIsland"], () => Survey.NearestUnvisited());
            a("main route", B["MainRoute"], () => WarpReader.RouteStatus());

            a("read hints", B["ReadHints"], () => Hints.Read());
            a("repeat", B["RepeatLast"], () => { Talk.RepeatLast(); return ""; });
            a("repeat the plaque", B["RepeatPlaque"], () => PlaqueLog.ReadLatest());
            a("previous plaque", B["PreviousPlaque"], () => PlaqueLog.Step(-1));

            a("north", B["North"], () => Compass.Read(Vector3i.north, CursorLive));
            a("south", B["South"], () => Compass.Read(Vector3i.south, CursorLive));
            a("east", B["East"], () => Compass.Read(Vector3i.east, CursorLive));
            a("west", B["West"], () => Compass.Read(Vector3i.west, CursorLive));

            a("review mode", B["ReviewMode"], () => Plugin.ToggleReviewMode());
            a("speech or sound", B["SpeechOrSound"], () => Sfx.Cycle());
            a("list bookmarks", B["ListBookmarks"], () => Bookmarks.List());
        }

        private delegate void Action(string name, BepInEx.Configuration.ConfigEntry<string> e, Func<string> run, bool crucial = false);

        private static bool CursorLive => Plugin.CursorIsLiveNow;

        /// <summary>Which set of buttons is active: none, or one of the two shoulders.</summary>
        private static int Layer(object js)
        {
            if (Cfg.PadMod1.Value >= 0 && Held(js, Cfg.PadMod1.Value)) return 1;
            if (Cfg.PadMod2.Value >= 0 && Held(js, Cfg.PadMod2.Value)) return 2;
            return 0;
        }

        private static MethodInfo _mButton;
        private static bool _mButtonTried;

        private static bool Held(object js, int index)
        {
            if (!_mButtonTried)
            {
                _mButtonTried = true;
                try { _mButton = js.GetType().GetMethod("GetButton", new[] { typeof(int) }); }
                catch { }
            }
            if (_mButton == null || index < 0) return false;
            try { return (bool)_mButton.Invoke(js, new object[] { index }); }
            catch { return false; }
        }

        private static void Buttons(object js)
        {
            BuildTable();

            int layer = Layer(js);

            foreach (var action in PadActions.All)
            {
                var b = PadActions.Parse(action.Entry.Value);
                if (!b.Set || b.Layer != layer) continue;
                if (!ButtonDown(js, b.Button)) continue;

                string said = action.Run();
                if (!string.IsNullOrEmpty(said)) Talk.Explicit(said);
                return;
            }
        }

        /// <summary>Walk into what is in front, or choose a menu item.</summary>
        private static string Choose()
        {
            var move = Refs.PlayerMove;
            if (move == null) return "";

            try
            {
                bool onRaft = false;
                try { onRaft = GameState.Is(GameState.RidingRaft); } catch { }
                move.SetQueuedMove(Refs.PlayerFacing, true, false, !onRaft, false, false);
            }
            catch { }
            return "";
        }

        /// <summary>
        /// The escape key's job. HUD.ShowMainMenu and HUD.CloseMenu are public, so the game's
        /// own menu is opened and closed rather than a key being imitated.
        /// </summary>
        private static string ToggleMenu()
        {
            var sim = Refs.Sim;
            if (sim == null || sim.hud == null) return "The menu is not available just now.";

            try
            {
                bool open = false;
                try { open = GameState.Is(GameState.Menu); } catch { }

                if (open) sim.hud.CloseMenu();
                else sim.hud.ShowMainMenu();
                return "";
            }
            catch { return "The menu would not open."; }
        }

        private static bool Act(object js, BepInEx.Configuration.ConfigEntry<int> button, Func<string> what)
        {
            if (!ButtonDown(js, button.Value)) return false;
            Talk.Explicit(what());
            return true;
        }

        // ---------------------------------------------------------------- capture, for setup
        //
        // The setup screen asks you to push a stick or press a button and then needs to know
        // WHICH. Both are answered the same way: look at everything, and report whatever moved
        // furthest from where it was resting.

        private static bool _capturing;

        internal static void StartCapture() { _capturing = true; }

        internal static bool Capturing => _capturing;

        internal static string ControllerName()
        {
            var js = FirstJoystick();
            if (js == null) return null;
            try { return _pName.GetValue(js, null) as string; }
            catch { return "controller"; }
        }

        /// <summary>The button being pressed right now, or -1.</summary>
        internal static int CapturedButton() { int ignored; return CapturedButton(out ignored); }

        internal static int CapturedButton(out int layer)
        {
            layer = 0;
            var js = FirstJoystick();
            if (js == null) return -1;

            int buttons = 0;
            try { if (_pButtonCount != null) buttons = (int)_pButtonCount.GetValue(js, null); } catch { }

            // Low numbers first and nothing above 100: a pad can report the same physical button
            // twice under different layouts, and the high copy is the one that works
            // inconsistently.
            int mod1 = Cfg.PadMod1.Value, mod2 = Cfg.PadMod2.Value;
            if (mod1 >= 0 && Held(js, mod1)) layer = 1;
            else if (mod2 >= 0 && Held(js, mod2)) layer = 2;

            int limit = buttons > 100 ? 100 : buttons;
            for (int i = 0; i < limit; i++)
            {
                if (i == mod1 || i == mod2) continue;     // a shoulder alone is not a binding
                if (ButtonDown(js, i)) { _capturing = false; return i; }
            }

            return -1;
        }

        /// <summary>The axis being pushed furthest right now, with which way it went.</summary>
        internal static bool CapturedAxis(out int axis, out float value)
        {
            axis = -1; value = 0f;

            var js = FirstJoystick();
            if (js == null) return false;

            int count = 0;
            try { if (_pAxisCount != null) count = (int)_pAxisCount.GetValue(js, null); } catch { }

            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                float v = Axis(js, i);            // measured from rest
                if (Mathf.Abs(v) > Mathf.Abs(best)) { best = v; axis = i; }
            }

            if (axis < 0 || Mathf.Abs(best) < 0.6f) { axis = -1; return false; }

            value = best;
            _capturing = false;
            return true;
        }

        /// <summary>
        /// Undo and reset, through the game's own History and Resetter rather than by pretending
        /// to press its keys. They are public, so there is nothing to imitate.
        /// </summary>
        private static string Undo()
        {
            var h = Refs.History;
            if (h == null) return "Cannot undo just now.";

            try
            {
                if (!h.CanUndo()) return "Nothing to undo.";
                h.Undo(true);
                return "";          // the game's own undo event announces it
            }
            catch { return "Cannot undo just now."; }
        }

        private static string ResetIsland()
        {
            var r = Refs.Resetter;
            if (r == null) return "Cannot reset just now.";

            try { r.ResetIsland(); return ""; }
            catch { return "Cannot reset just now."; }
        }

        // ---------------------------------------------------------------- discovery

        private static bool _discovering;

        internal static string ToggleDiscovery()
        {
            _discovering = !_discovering;
            if (!_discovering) return "Controller test off.";

            var js = FirstJoystick();
            if (js == null)
                return "No controller found. " + Why + ". Nothing will be reported.";

            _calibrated = false;
            Calibrate(js);

            string name = "controller";
            try { if (_pName != null) name = (string)_pName.GetValue(js, null); } catch { }

            return "Controller test on, using " + name
                 + ". Move a stick or press a button and I will say its number. "
                 + "Write those numbers into the config file, then press this key again.";
        }

        private static float _nextReport;

        private static void Discover(object js)
        {
            if (Time.unscaledTime < _nextReport) return;

            int axes = 0, buttons = 0;
            try
            {
                if (_pAxisCount != null) axes = (int)_pAxisCount.GetValue(js, null);
                if (_pButtonCount != null) buttons = (int)_pButtonCount.GetValue(js, null);
            }
            catch { }

            bool any = AnyButtonDown(js);

            for (int i = 0; i < buttons; i++)
                if (ButtonDown(js, i))
                {
                    _nextReport = Time.unscaledTime + 0.4f;
                    Report("Button " + i + ".");
                    return;
                }

            // Rewired saw a press that the numbered loop did not. Say so rather than stay
            // silent, because silence here is indistinguishable from not having pressed
            // anything, and that is what made the last attempt impossible to diagnose.
            if (any)
            {
                _nextReport = Time.unscaledTime + 0.5f;
                Report("A button was pressed but its number could not be read.");
            }

            for (int i = 0; i < axes; i++)
            {
                float v = Axis(js, i);      // already measured from rest
                if (Mathf.Abs(v) > 0.7f)
                {
                    _nextReport = Time.unscaledTime + 0.6f;
                    Report("Axis " + i + ", " + (v > 0 ? "positive." : "negative."));
                    return;
                }
            }
        }

        /// <summary>Say it and write it down, so a silent test can still be diagnosed.</summary>
        private static void Report(string what)
        {
            Talk.Explicit(what);
            var log = Plugin.Log;
            if (log != null) log.LogInfo("[pad] " + what);
        }

        // ---------------------------------------------------------------- per frame

        internal static void Tick()
        {
            if (!Cfg.PadEnabled.Value && !_discovering) return;

            var js = FirstJoystick();
            if (js == null) return;

            ApplyProfile(js);
            AssignToGame(js);
            CheckStickAxes(js);
            ApplyProfileAgain(js);

            if (_discovering) { Discover(js); return; }

            // While setup is asking you to push something, that push must not also move the
            // cursor or fire an action.
            if (_capturing || PadBinder.Active) return;

            // The warp map has its own list and its own selection, so it comes before menus.
            if (WarpMapInput(js)) return;

            // Menus next: when one is open the sticks belong to it, not to the board.
            if (Menus(js)) return;

            if (!Refs.InPlay) return;

            LeftStick(js);
            Stick(js);
            Buttons(js);
        }

        internal static void LogState(BepInEx.Logging.ManualLogSource log)
        {
            if (log == null) return;
            Init();

            var js = FirstJoystick();
            if (js != null) ApplyProfile(js);

            log.LogInfo("--- controller ---");
            log.LogInfo("rewired     : " + Why);
            log.LogInfo("right stick : " + StickSource);
            log.LogInfo("profile     : " + ProfileUsed);
            log.LogInfo("given to game: " + AssignResult);

            if (js == null) { log.LogInfo("no connected controller"); log.LogInfo("--- end ---"); return; }

            log.LogInfo("found GetAxisRaw   : " + (_mAxisRaw != null));
            log.LogInfo("found GetButtonDown: " + (_mButtonDown != null));
            log.LogInfo("found axisCount    : " + (_pAxisCount != null));
            log.LogInfo("found buttonCount  : " + (_pButtonCount != null));
            log.LogInfo("found GetAnyButtonDown: " + (AnyButtonDown(js, true) ? "yes" : "no or false"));

            try
            {
                log.LogInfo("name   : " + _pName.GetValue(js, null));
                log.LogInfo("axes   : " + _pAxisCount.GetValue(js, null));
                log.LogInfo("buttons: " + _pButtonCount.GetValue(js, null));

                Calibrate(js);

                int axes = (int)_pAxisCount.GetValue(js, null);

                var raw = new StringBuilder("raw values : ");
                for (int i = 0; i < axes; i++) raw.Append(i).Append('=').Append(Raw(js, i).ToString("0.00")).Append(' ');
                log.LogInfo(raw.ToString());

                var rest = new StringBuilder("resting    : ");
                for (int i = 0; i < axes && _rest != null && i < _rest.Length; i++)
                    rest.Append(i).Append('=').Append(_rest[i].ToString("0.00")).Append(' ');
                log.LogInfo(rest.ToString());

                log.LogInfo("enabled    : " + Cfg.PadEnabled.Value);
                log.LogInfo("calibrated : " + _calibrated);

                log.LogInfo("RIGHT stick (cursor) axes : X=" + Cfg.PadRightStickX.Value
                            + " Y=" + Cfg.PadRightStickY.Value
                            + "  now X=" + Axis(js, Cfg.PadRightStickX.Value).ToString("0.00")
                            + " Y=" + Axis(js, Cfg.PadRightStickY.Value).ToString("0.00"));

                // The left stick was missing from this report, which is why the last fault
                // could not be read off a log and had to be guessed at.
                log.LogInfo("LEFT stick (walking) axes : X=" + Cfg.PadLeftStickX.Value
                            + " Y=" + Cfg.PadLeftStickY.Value
                            + "  now X=" + Axis(js, Cfg.PadLeftStickX.Value).ToString("0.00")
                            + " Y=" + Axis(js, Cfg.PadLeftStickY.Value).ToString("0.00"));

                log.LogInfo("deadzone=" + Cfg.PadDeadzone.Value
                            + "  invertX=" + Cfg.PadInvertX.Value
                            + "  invertY=" + Cfg.PadInvertY.Value);

                log.LogInfo("left stick walks  : " + Cfg.PadMovesMonster.Value);
                log.LogInfo("left stick menus  : " + Cfg.PadMenus.Value);

                bool inMenuNow = false, inPlayNow = false;
                try { inMenuNow = GameState.Is(GameState.Menu) || GameState.Is(GameState.TitleScreen); } catch { }
                try { inPlayNow = Refs.InPlay; } catch { }
                log.LogInfo("in a menu now     : " + inMenuNow + "   on the board now: " + inPlayNow);

                log.LogInfo("modifiers  : first=" + Cfg.PadMod1.Value + " second=" + Cfg.PadMod2.Value
                            + "  stick click=" + Cfg.PadStickClick.Value);

                BuildTable();
                var bound = new StringBuilder("bound actions: ");
                foreach (var act in PadActions.All)
                    if (!string.IsNullOrEmpty(act.Entry.Value))
                        bound.Append(act.Name).Append('=').Append(act.Entry.Value).Append("  ");
                log.LogInfo(bound.ToString());

                var now = new StringBuilder("from rest  : ");
                for (int i = 0; i < axes; i++) now.Append(i).Append('=').Append(Axis(js, i).ToString("0.00")).Append(' ');
                log.LogInfo(now.ToString());
            }
            catch (Exception e) { log.LogInfo("could not read: " + e.Message); }

            log.LogInfo("--- end controller ---");
        }
    }
}
