using System.Text;
using BepInEx;
using UnityEngine;
using AMEAccess.Speech;

// Explicit aliases for every one of the mod's own types used here.
//
// "using AMEAccess.Game;" alone is not safe: UnityEngine is a huge namespace and any name we
// pick may already exist in it - Cursor, Compass and others have all collided already. An alias
// binds the name to OUR type unconditionally, so this whole class of build error cannot recur
// no matter what we add later.
using Compass = AMEAccess.Game.Compass;
using Describe = AMEAccess.Game.Describe;
using Details = AMEAccess.Game.Details;
using Hints = AMEAccess.Game.Hints;
using GameKeys = AMEAccess.Game.GameKeys;
using Hooks = AMEAccess.Game.Hooks;
using MenuReader = AMEAccess.Game.MenuReader;
using Naming = AMEAccess.Game.Naming;
using Refs = AMEAccess.Game.Refs;
using ReviewCursor = AMEAccess.Game.ReviewCursor;
using Bookmarks = AMEAccess.Game.Bookmarks;
using PlaqueLog = AMEAccess.Game.PlaqueLog;
using Stats = AMEAccess.Game.Stats;
using Survey = AMEAccess.Game.Survey;
using WarpReader = AMEAccess.Game.WarpReader;

namespace AMEAccess
{
    /// <summary>
    /// A Monster's Expedition accessibility mod.
    ///
    /// Design principle: play the game the way a normal player plays it.
    ///
    /// The mod's job is to convey what is on the screen, not what is in the solver. So it
    /// describes terrain, says what walking in a direction would do, reads plaques, and reports
    /// undo and reset. It does not route you anywhere, does not run the developer solver, and
    /// does not tell you whether a puzzle is solved. Hints exist only where the game already
    /// offers them, behind the game's own setting.
    ///
    /// Environment (all verified against the shipped binaries):
    ///   Unity 2020.3.8f1, Mono backend, 32-bit  =>  BepInEx 5 (win_x86).
    ///   No game files are modified; BepInEx injects via a proxy DLL.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "hassan.ameaccess";
        public const string Name = "A Monster's Expedition Accessibility";
        public const string Version = "0.1.0";

        internal static Plugin Instance;

        /// <summary>Shared logger so the rest of the mod can write to the BepInEx log.</summary>
        internal static BepInEx.Logging.ManualLogSource Log;

        private bool _announcedReady;
        private static bool _reviewMode;
        private bool _loggedNotReady;
        private static int _keysReset;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Cfg.Bind(Config);
            _keysReset = Cfg.ResetKeysIfOutdated();
            if (_keysReset > 0)
                Logger.LogWarning(_keysReset + " key bindings were reset to the mod's current defaults.");
            Talk.Init(Logger);

            Logger.LogInfo(Name + " " + Version + " loaded.");
            Logger.LogInfo("Keys: " +
                Cfg.KeyWhereAmI.Value + " position, " +
                Cfg.KeyLookAround.Value + " look around, " +
                Cfg.KeyIslandInfo.Value + " island, " +
                Cfg.KeyFacing.Value + " facing, " +
                Cfg.KeyStatus.Value + " status, " +
                Cfg.KeyHint.Value + " hint, " +
                Cfg.KeyRepeat.Value + " repeat.");
        }

        private void OnDestroy()
        {
            EnsureMovementRestored();
            Hooks.Unsubscribe();
            Refs.Forget();
        }

        private void LateUpdate()
        {
            if (Refs.Ready) Hooks.EndOfFrame();
        }

        private void Update()
        {
            // Everything lives in one scene, so once Simulator.current is up we are up.
            if (!Refs.Ready)
            {
                if (!_loggedNotReady && Time.unscaledTime > 15f)
                {
                    _loggedNotReady = true;
                    Logger.LogWarning("Simulator.current is still null after 15 seconds. The mod " +
                                      "cannot read game state yet. If you are past the title screen " +
                                      "this is a bug worth reporting.");
                }
                if (_announcedReady) { _announcedReady = false; _reviewMode = false; Hooks.Unsubscribe(); Refs.Forget(); GameKeys.Forget(); WarpReader.Forget(); }
                return;
            }

            if (!_announcedReady)
            {
                _announcedReady = true;
                Hooks.Subscribe();
                Talk.Explicit("Accessibility mod ready. Press " + Cfg.KeyHelp.Value.MainKey +
                    " for the key list." + (_keysReset > 0
                        ? " " + _keysReset + " keys were updated to new defaults."
                        : ""));
            }

            // Rebinding owns the keyboard while it is on, so it goes before everything.
            if (Rebinder.Active) { Rebinder.Handle(); return; }
            if (Cfg.Pressed(Cfg.KeyRebind)) { Talk.Explicit(Rebinder.Toggle()); return; }

            // Silence works everywhere, including menus.
            // The warp map is its own screen with its own selection to follow.
            WarpReader.Tick();

            // The game's own keys change things on screen silently; report what changed.
            GameKeys.Tick();

            // Menus first: these must work before a game has even been started.
            MenuReader.Tick();
            if (Cfg.Pressed(Cfg.KeyReadPanel)) { Talk.Explicit(MenuReader.ReadPanel()); return; }
            if (Cfg.Pressed(Cfg.KeyReadFocus))
            {
                Talk.Explicit(MenuReader.ReadFocused());
                MenuReader.DumpFocused(Logger);   // so a mis-read item can be diagnosed from the log
                return;
            }
            if (Cfg.Pressed(Cfg.KeyHelp)) { Help.Dump(Logger); Talk.Explicit(Help.Next()); return; }
            if (Cfg.Pressed(Cfg.KeyPlaquePrev)) { Talk.Explicit(PlaqueLog.Step(-1)); return; }
            if (Cfg.Pressed(Cfg.KeyPlaqueRepeat)) { Talk.Explicit(PlaqueLog.ReadLatest()); return; }
            if (Cfg.Pressed(Cfg.KeyDumpPieces))
            {
                Survey.Dump(Logger, 25);
                Talk.Explicit("Nearby pieces written to the log.");
                return;
            }
            if (Cfg.Pressed(Cfg.KeySpeechTest)) { Talk.SelfTest(true); LogState(); return; }
            if (Cfg.Pressed(Cfg.KeySilence)) { Talk.Silence(); return; }
            if (Cfg.Pressed(Cfg.KeyRepeat)) { Talk.RepeatLast(); return; }

            // On the warp map the island keys have no meaning, so point them at the map.
            if (WarpReader.Active)
            {
                if (Cfg.Pressed(Cfg.KeyNextObject) || Cfg.Pressed(Cfg.KeyNextItem))
                { Talk.Explicit(WarpReader.Step(1)); return; }
                if (Cfg.Pressed(Cfg.KeyPrevObject) || Cfg.Pressed(Cfg.KeyPrevItem))
                { Talk.Explicit(WarpReader.Step(-1)); return; }
                if (Cfg.Pressed(Cfg.KeyInspect)) { Talk.Explicit(WarpReader.Activate()); return; }
                if (Cfg.Pressed(Cfg.KeySurvey)) { Talk.Explicit(WarpReader.List()); return; }
                if (Cfg.Pressed(Cfg.KeyWhereAmI) || Cfg.Pressed(Cfg.KeyIslandInfo))
                { Talk.Explicit(WarpReader.Current()); return; }
                return;
            }

            // The rest only makes sense while actually walking around.
            if (!Refs.InPlay) { EnsureMovementRestored(); return; }

            // The Ctrl cluster is checked before everything else so that Ctrl+K can never
            // be mistaken for plain K.
            if (HandleBookmarks()) return;
            if (HandleCompass()) return;

            // Review mode owns the arrow keys while it is on, so it goes first.
            if (HandleReview()) return;

            if (Cfg.Pressed(Cfg.KeyWhereAmI)) { Talk.Explicit(Describe.WhereAmI()); return; }
            if (Cfg.Pressed(Cfg.KeyLookAround)) { Talk.Explicit(Describe.LookAround()); return; }
            if (Cfg.Pressed(Cfg.KeyIslandInfo)) { Talk.Explicit(IslandInfo()); return; }
            if (Cfg.Pressed(Cfg.KeyFacing)) { Talk.Explicit(Describe.Facing()); return; }
            if (Cfg.Pressed(Cfg.KeyStatus)) { Talk.Explicit(Status()); return; }
            if (Cfg.Pressed(Cfg.KeyStats)) { Talk.Explicit(Stats.Summary()); return; }

            if (Cfg.Pressed(Cfg.KeyScan)) { Talk.Explicit(Survey.Scan()); return; }
            if (Cfg.Pressed(Cfg.KeySurvey)) { Survey.RebuildCycle(); Talk.Explicit(Survey.IslandSurvey()); return; }
            if (Cfg.Pressed(Cfg.KeyNextItem)) { Talk.Explicit(Survey.CycleNext(1)); return; }
            if (Cfg.Pressed(Cfg.KeyPrevItem)) { Talk.Explicit(Survey.CycleNext(-1)); return; }
            if (Cfg.Pressed(Cfg.KeyIslandOnly)) { Talk.Explicit(Survey.ToggleScope()); return; }
            if (Cfg.Pressed(Cfg.KeyNextCategory)) { Talk.Explicit(Survey.CycleCategory(1)); return; }
            if (Cfg.Pressed(Cfg.KeyPrevCategory)) { Talk.Explicit(Survey.CycleCategory(-1)); return; }
            if (Cfg.Pressed(Cfg.KeyNextObject)) { Talk.Explicit(Survey.CycleNext(1)); return; }
            if (Cfg.Pressed(Cfg.KeyPrevObject)) { Talk.Explicit(Survey.CycleNext(-1)); return; }
            if (Cfg.Pressed(Cfg.KeyInspect)) { Talk.Explicit(InspectTarget()); return; }
            if (Cfg.Pressed(Cfg.KeyIslandsNearby)) { Talk.Explicit(Survey.NearbyIslands()); return; }
            if (Cfg.Pressed(Cfg.KeyNextIsland)) { Talk.Explicit(Survey.NearestUnvisited()); return; }
            if (Cfg.Pressed(Cfg.KeyListBookmarks)) { Talk.Explicit(Bookmarks.List()); return; }

            if (Cfg.Pressed(Cfg.KeyHint)) { Talk.Explicit(Hints.Toggle()); return; }
            if (Cfg.Pressed(Cfg.KeyReadHints)) { Talk.Explicit(Hints.Read()); return; }

        }

        // -------------------------------------------------------------------------

        private static string IslandInfo()
        {
            var island = Refs.CurrentIsland;
            if (island == null) return "Not on an island.";

            var sb = new StringBuilder();
            Naming.AlwaysBiome = true;               // asked directly, so report everything
            sb.Append(Naming.IslandLabel(island)).Append('.');
            Naming.AlwaysBiome = false;

            sb.Append(island.GetVisited() ? " Visited before." : " Not visited before.");

            var biome = island.biome;
            if (biome != null)
            {
                if (biome.raining) sb.Append(" Raining.");
                else if (biome.snowing) sb.Append(" Snowing.");
                else if (biome.foggy) sb.Append(" Foggy.");
            }

            return sb.ToString();
        }

        private static string Status()
        {
            var move = Refs.PlayerMove;
            var history = Refs.History;
            var sim = Refs.Sim;
            if (move == null) return "Not in the game world.";

            var sb = new StringBuilder();
            sb.Append(move.moveCount).Append(move.moveCount == 1 ? " move." : " moves.");

            if (history != null)
            {
                sb.Append(history.CanUndo() ? " Undo available." : " Nothing to undo.");
                if (history.CanRedo()) sb.Append(" Redo available.");
            }

            if (sim != null && sim.CanReset()) sb.Append(" Island can be reset.");

            return sb.ToString();
        }

        /// <summary>Dump the mod's view of the game, for diagnosing a silent or inert mod.</summary>
        private void LogState()
        {
            Logger.LogInfo("--- mod state ---");
            Logger.LogInfo("Simulator.current  : " + (Refs.Sim != null));
            Logger.LogInfo("Refs.Ready         : " + Refs.Ready);
            Logger.LogInfo("Refs.InPlay        : " + Refs.InPlay);
            Logger.LogInfo("Review mode        : " + _reviewMode);
            var island = Refs.CurrentIsland;
            Logger.LogInfo("Current island     : " + (island != null ? Naming.IslandName(island) : "none"));
            if (Refs.Ready) Logger.LogInfo("Player position    : " + Naming.Coords(Refs.PlayerPos));
            Logger.LogInfo("--- end mod state ---");
        }

        /// <summary>
        /// Number keys 1 to 9.
        ///
        /// In review mode a number SETS a bookmark at the cursor. While playing, the same number
        /// gives walking directions back to it. One key, two meanings, but they can never be
        /// confused because the modes are exclusive - and it means marking a spot and returning
        /// to it use the same finger.
        ///
        /// Ctrl plus a number clears that bookmark.
        /// </summary>
        private static bool HandleBookmarks()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            for (int i = 1; i <= 9; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
                if (!Input.GetKeyDown(key)) continue;

                if (ctrl) { Talk.Explicit(Bookmarks.Clear(i)); return true; }

                if (_reviewMode) Talk.Explicit(Bookmarks.Set(i, ReviewCursor.Position));
                else Talk.Explicit(Bookmarks.Go(i));
                return true;
            }
            return false;
        }

        /// <summary>
        /// The eight-direction letter cluster, U I O / J K L / M , . held with Ctrl.
        /// Returns true if one was pressed.
        /// </summary>
        private static bool HandleCompass()
        {
            if (Cfg.Pressed(Cfg.DirHere)) { Talk.Explicit(Compass.Here(_reviewMode)); return true; }

            if (Cfg.Pressed(Cfg.DirN))  { Talk.Explicit(Compass.Read(Vector3i.north, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirS))  { Talk.Explicit(Compass.Read(Vector3i.south, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirE))  { Talk.Explicit(Compass.Read(Vector3i.east, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirW))  { Talk.Explicit(Compass.Read(Vector3i.west, _reviewMode)); return true; }

            if (Cfg.Pressed(Cfg.DirNE)) { Talk.Explicit(Compass.Read(Compass.NorthEast, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirSE)) { Talk.Explicit(Compass.Read(Compass.SouthEast, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirSW)) { Talk.Explicit(Compass.Read(Compass.SouthWest, _reviewMode)); return true; }
            if (Cfg.Pressed(Cfg.DirNW)) { Talk.Explicit(Compass.Read(Compass.NorthWest, _reviewMode)); return true; }

            return false;
        }

        /// <summary>
        /// Review mode: look anywhere on the map without moving your monster.
        ///
        /// Turning it on disables Player.GridKeyboardInput, which is the only component that
        /// turns keyboard input into monster movement. Its OnEnable/OnDisable are clean and
        /// symmetric (they add and remove two event subscriptions and clear lastX/lastY), so
        /// toggling it is safe and fully reversible. With movement frozen, the arrow keys are
        /// free to drive the cursor, which is far kinder on a laptop than a numpad.
        ///
        /// Returns true if a review key was handled this frame.
        /// </summary>
        private static bool HandleReview()
        {
            if (Cfg.Pressed(Cfg.CurJumpToItem)) { Talk.Explicit(JumpToSelected()); return true; }

            if (Cfg.Pressed(Cfg.KeyReviewToggle))
            {
                SetReviewMode(!_reviewMode);
                return true;
            }

            if (!_reviewMode) return false;

            int step = 1;
            var mod = Cfg.CurBigStepModifier.Value.MainKey;
            if (mod != KeyCode.None && Input.GetKey(mod)) step = Cfg.CursorBigStep.Value;

            if (Cfg.Pressed(Cfg.CurNorth)) { Talk.Explicit(ReviewCursor.Move(Vector3i.north, step)); return true; }
            if (Cfg.Pressed(Cfg.CurSouth)) { Talk.Explicit(ReviewCursor.Move(Vector3i.south, step)); return true; }
            if (Cfg.Pressed(Cfg.CurEast)) { Talk.Explicit(ReviewCursor.Move(Vector3i.east, step)); return true; }
            if (Cfg.Pressed(Cfg.CurWest)) { Talk.Explicit(ReviewCursor.Move(Vector3i.west, step)); return true; }

            if (Cfg.Pressed(Cfg.CurHome)) { Talk.Explicit(ReviewCursor.Home()); return true; }
            if (Cfg.Pressed(Cfg.CurColumn)) { Talk.Explicit(ReviewCursor.ReadColumn()); return true; }
            if (Cfg.Pressed(Cfg.CurRoute)) { Talk.Explicit(ReviewCursor.RouteHere()); return true; }

            // In review mode these act on the cursor rather than just reading out a list.
            if (Cfg.Pressed(Cfg.KeyInspect)) { Talk.Explicit(ReviewCursor.Inspect()); return true; }
            if (Cfg.Pressed(Cfg.KeyNextItem))
            { Survey.CycleNext(1); Talk.Explicit(ReviewCursor.JumpTo(Survey.Selected)); return true; }
            if (Cfg.Pressed(Cfg.KeyPrevItem))
            { Survey.CycleNext(-1); Talk.Explicit(ReviewCursor.JumpTo(Survey.Selected)); return true; }
            if (Cfg.Pressed(Cfg.KeyNextCategory)) { Talk.Explicit(Survey.CycleCategory(1)); return true; }
            if (Cfg.Pressed(Cfg.KeyPrevCategory)) { Talk.Explicit(Survey.CycleCategory(-1)); return true; }

            if (Cfg.Pressed(Cfg.KeyNextObject))
            {
                Survey.CycleNext(1);
                Talk.Explicit(ReviewCursor.JumpTo(Survey.Selected));
                return true;
            }
            if (Cfg.Pressed(Cfg.KeyPrevObject))
            {
                Survey.CycleNext(-1);
                Talk.Explicit(ReviewCursor.JumpTo(Survey.Selected));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Put the review cursor on whatever the category and item keys have selected.
        ///
        /// Paging through categories tells you what exists but leaves the cursor where it was.
        /// This is the "take me there" step: it turns review mode on if needed, picks the first
        /// item in the category when nothing is selected yet, and moves the cursor onto it.
        /// </summary>
        private static string JumpToSelected()
        {
            var piece = Survey.Selected;
            if (piece == null)
            {
                Survey.CycleNext(1);          // nothing chosen yet, take the nearest
                piece = Survey.Selected;
            }
            if (piece == null) return "Nothing selected.";

            string prefix = "";
            if (!_reviewMode)
            {
                var input = Refs.KeyboardInput;
                if (input != null)
                {
                    _reviewMode = true;
                    input.enabled = false;
                    prefix = "Review mode. ";
                }
            }

            return prefix + ReviewCursor.JumpTo(piece);
        }

        private static void SetReviewMode(bool on)
        {
            var input = Refs.KeyboardInput;
            if (input == null)
            {
                Talk.Explicit("Cannot switch to review mode right now.");
                return;
            }

            _reviewMode = on;
            input.enabled = !on;

            if (on)
            {
                Survey.RebuildCycle();
                Talk.Explicit("Review mode. Arrow keys move the cursor. " +
                    Cfg.CurHome.Value.MainKey + " brings it back to you. " + ReviewCursor.Home());
            }
            else
            {
                Talk.Explicit("Playing again.");
            }
        }

        /// <summary>Never leave the player stuck with movement disabled.</summary>
        private static void EnsureMovementRestored()
        {
            if (!_reviewMode) return;
            var input = Refs.KeyboardInput;
            if (input != null) input.enabled = true;
            _reviewMode = false;
        }

        /// <summary>
        /// Inspect whatever the player last stepped to with the object cycler, falling back to
        /// whatever they are facing.
        /// </summary>
        private static string InspectTarget()
        {
            var picked = Survey.Selected;
            if (picked != null) return Details.Detail(picked);

            var sim = Refs.Sim;
            if (sim != null)
            {
                var facing = sim.GetFacingInteractable();
                if (facing != null) return Details.Detail(facing);
            }

            var pos = Refs.PlayerPos + Refs.PlayerFacing;
            var top = Survey.TopOfColumn(pos);
            if (top != null) return Details.Detail(top);

            return "Water ahead.";
        }

    }
}
