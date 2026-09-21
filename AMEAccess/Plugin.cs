using System.Text;
using BepInEx;
using HarmonyLib;
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
        private static bool _peeking;
        private Harmony _harmony;
        private bool _loggedNotReady;
        private static int _keysReset;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // One patch, for the optional arrows-always-review mode. It does nothing at all
            // unless that setting is on; see ArrowVeto.
            try
            {
                _harmony = new Harmony(Guid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (System.Exception e)
            {
                Logger.LogWarning("Could not apply the input patch: " + e.Message
                                + ". Arrows-always-review will not work; everything else will.");
            }
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
                Cfg.KeyReadHints.Value + " read hints, " +
                Cfg.KeyRepeat.Value + " repeat.");
        }

        private void OnDestroy()
        {
            EnsureMovementRestored();
            if (_harmony != null) { try { _harmony.UnpatchSelf(); } catch { } }
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
            if (Cfg.Pressed(Cfg.KeyArrowMode)) { Talk.Explicit(ToggleArrowMode()); return; }

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
            if (Cfg.Pressed(Cfg.KeyHelpPrev)) { Talk.Explicit(Help.Next(-1)); return; }
            if (Cfg.Pressed(Cfg.KeyHelp)) { Help.Dump(Logger); Talk.Explicit(Help.Next(1)); return; }
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

            // With arrows-always-review on, the arrows are the cursor's and never the
            // monster's. The veto patch stops the game acting on them; this acts on them here.
            if (Cfg.ArrowsAlwaysReview.Value && !_reviewMode && !_peeking && HandleArrowCursor())
                return;

            // Holding the peek key is a momentary review mode, handled before the toggled one.
            if (HandlePeek()) return;

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
        /// Number keys 1 to 9: bookmarks.
        ///
        ///   1 to 9              walking directions to that bookmark
        ///   shift and 1 to 9    set that bookmark at the review cursor
        ///   control and 1 to 9  clear it
        ///
        /// Which action you get used to depend on whether review mode was on: a number set a
        /// bookmark in review mode and recalled one otherwise. That broke as soon as there was
        /// more than one way to look around - holding the peek key and arrows-always-review
        /// both move the cursor without review mode being on, so a number could only ever
        /// recall and there was no way left to set one.
        ///
        /// A modifier says which action you mean, so it no longer matters how you are looking.
        /// </summary>
        private static bool HandleBookmarks()
        {
            bool ctrl = Cfg.HeldEither(KeyCode.LeftControl);
            bool shift = Cfg.HeldEither(KeyCode.LeftShift);

            for (int i = 1; i <= 9; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
                if (!Input.GetKeyDown(key)) continue;

                if (ctrl) Talk.Explicit(Bookmarks.Clear(i));
                else if (shift) Talk.Explicit(Bookmarks.Set(i, ReviewCursor.Position));
                else Talk.Explicit(Bookmarks.Go(i));
                return true;
            }
            return false;
        }

        /// <summary>
        /// The eight-direction letter cluster, U I O / J K L / M , . held with Ctrl.
        /// Returns true if one was pressed.
        /// </summary>
        /// <summary>
        /// True whenever the review cursor is the thing you are moving, by any of the three
        /// ways of looking around. Anything that used to ask "is review mode on?" should ask
        /// this instead, or it goes wrong in the other two.
        /// </summary>
        private static bool CursorIsLive =>
            _reviewMode || _peeking || Cfg.ArrowsAlwaysReview.Value;

        private static bool HandleCompass()
        {
            bool atCursor = CursorIsLive;

            if (Cfg.Pressed(Cfg.DirHere)) { Talk.Explicit(Compass.Here(atCursor)); return true; }

            if (Cfg.Pressed(Cfg.DirN))  { Talk.Explicit(Compass.Read(Vector3i.north, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirS))  { Talk.Explicit(Compass.Read(Vector3i.south, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirE))  { Talk.Explicit(Compass.Read(Vector3i.east, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirW))  { Talk.Explicit(Compass.Read(Vector3i.west, atCursor)); return true; }

            if (Cfg.Pressed(Cfg.DirNE)) { Talk.Explicit(Compass.Read(Compass.NorthEast, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirSE)) { Talk.Explicit(Compass.Read(Compass.SouthEast, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirSW)) { Talk.Explicit(Compass.Read(Compass.SouthWest, atCursor)); return true; }
            if (Cfg.Pressed(Cfg.DirNW)) { Talk.Explicit(Compass.Read(Compass.NorthWest, atCursor)); return true; }

            return false;
        }

        /// <summary>
        /// Switch what the arrow keys do, without opening the config file.
        ///
        /// Any look in progress is ended first, so the two ways of moving the cursor can never
        /// be half-on at once, and movement is always handed back before the change takes
        /// effect. The setting is saved immediately, so it survives quitting.
        /// </summary>
        private static string ToggleArrowMode()
        {
            if (_reviewMode) SetReviewMode(false);
            if (_peeking) EndPeek();

            bool on = !Cfg.ArrowsAlwaysReview.Value;
            Cfg.ArrowsAlwaysReview.Value = on;
            Cfg.Save();

            if (on)
            {
                ReviewCursor.Home();
                return "Arrow keys now move the review cursor. Walk with W, A, S and D. "
                     + "Shift and a number sets a bookmark.";
            }

            return "Arrow keys move your monster again. Hold "
                 + Cfg.CurPeekModifier.Value.MainKey + " with them for a quick look, or press "
                 + Cfg.KeyReviewToggle.Value.MainKey + " for review mode.";
        }

        /// <summary>
        /// The arrow keys as a permanent review cursor, for players who chose that.
        ///
        /// Walking is W A S D, which the game handles itself. Nothing here freezes movement:
        /// the veto patch simply stops the game acting on arrow-only frames, so there is no
        /// mode to get stuck in.
        /// </summary>
        private static bool HandleArrowCursor()
        {
            int step = 1;
            if (Cfg.HeldEither(Cfg.CurBigStepModifier.Value.MainKey)) step = Cfg.CursorBigStep.Value;

            var jump = Cfg.CurBigStepModifier.Value.MainKey;

            if (Cfg.Pressed(Cfg.CurNorth, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.north, step)); return true; }
            if (Cfg.Pressed(Cfg.CurSouth, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.south, step)); return true; }
            if (Cfg.Pressed(Cfg.CurEast, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.east, step)); return true; }
            if (Cfg.Pressed(Cfg.CurWest, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.west, step)); return true; }

            if (Cfg.Pressed(Cfg.CurHome) || Cfg.Pressed(Cfg.CurHomeAlt))
            { Talk.Explicit(ReviewCursor.Home()); return true; }

            // Everything review mode offers for the cursor, since the cursor is live here too.
            // Without these, the column and route keys were dead in this mode - they were only
            // ever reached through the review-mode handler.
            if (Cfg.Pressed(Cfg.CurColumn)) { Talk.Explicit(ReviewCursor.ReadColumn()); return true; }
            if (Cfg.Pressed(Cfg.CurRoute)) { Talk.Explicit(ReviewCursor.RouteHere()); return true; }
            if (Cfg.Pressed(Cfg.KeyInspect)) { Talk.Explicit(ReviewCursor.Inspect()); return true; }

            return false;
        }

        /// <summary>
        /// Hold-to-look: the arrow keys move the review cursor while the peek key is held.
        ///
        /// This exists because toggling review mode costs two presses around every glance, and
        /// glancing is the commonest thing you do. Holding a key instead is one press, with
        /// nothing to enter, leave or forget.
        ///
        /// The arrow keys cannot simply be reassigned, because the game reads MOVEMENT from
        /// Rewired's axes, which both the arrows and W A S D feed - holding alt does not stop
        /// the game seeing an arrow press. So movement is switched off for exactly as long as
        /// the key is held, and switched back on the moment it is released.
        ///
        /// Returns true while the peek key is held, so nothing else acts on those presses.
        /// </summary>
        private static bool HandlePeek()
        {
            // The toggled review mode already owns the arrows; do not fight it.
            if (_reviewMode) return false;

            bool held = Cfg.HeldEither(Cfg.CurPeekModifier.Value.MainKey);

            if (!held)
            {
                if (_peeking) EndPeek();
                return false;
            }

            if (!_peeking)
            {
                var input = Refs.KeyboardInput;
                if (input == null) return false;

                _peeking = true;
                input.enabled = false;

                // Start each look from your monster, so a glance is always about where you are
                // rather than wherever the cursor happened to be left.
                ReviewCursor.Home();
            }

            int step = 1;
            if (Cfg.HeldEither(Cfg.CurBigStepModifier.Value.MainKey)) step = Cfg.CursorBigStep.Value;

            var peek = Cfg.CurPeekModifier.Value.MainKey;

            if (Cfg.Pressed(Cfg.CurNorth, peek) || Input.GetKeyDown(KeyCode.UpArrow))
            { Talk.Explicit(ReviewCursor.Move(Vector3i.north, step)); return true; }
            if (Cfg.Pressed(Cfg.CurSouth, peek) || Input.GetKeyDown(KeyCode.DownArrow))
            { Talk.Explicit(ReviewCursor.Move(Vector3i.south, step)); return true; }
            if (Cfg.Pressed(Cfg.CurEast, peek) || Input.GetKeyDown(KeyCode.RightArrow))
            { Talk.Explicit(ReviewCursor.Move(Vector3i.east, step)); return true; }
            if (Cfg.Pressed(Cfg.CurWest, peek) || Input.GetKeyDown(KeyCode.LeftArrow))
            { Talk.Explicit(ReviewCursor.Move(Vector3i.west, step)); return true; }

            if (Input.GetKeyDown(Cfg.CurColumn.Value.MainKey))
            { Talk.Explicit(ReviewCursor.ReadColumn()); return true; }
            if (Input.GetKeyDown(Cfg.CurRoute.Value.MainKey))
            { Talk.Explicit(ReviewCursor.RouteHere()); return true; }
            if (Input.GetKeyDown(KeyCode.Return))
            { Talk.Explicit(ReviewCursor.Inspect()); return true; }

            return true;   // swallow everything else while the key is held
        }

        /// <summary>Give the arrow keys back to the game.</summary>
        private static void EndPeek()
        {
            _peeking = false;
            var input = Refs.KeyboardInput;
            if (input != null) input.enabled = true;
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
            // Either shift counts, the same as everywhere else.
            if (Cfg.HeldEither(Cfg.CurBigStepModifier.Value.MainKey)) step = Cfg.CursorBigStep.Value;

            // The jump modifier must not stop the arrow keys matching, so it is ignored here.
            var jump = Cfg.CurBigStepModifier.Value.MainKey;

            if (Cfg.Pressed(Cfg.CurNorth, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.north, step)); return true; }
            if (Cfg.Pressed(Cfg.CurSouth, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.south, step)); return true; }
            if (Cfg.Pressed(Cfg.CurEast, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.east, step)); return true; }
            if (Cfg.Pressed(Cfg.CurWest, jump)) { Talk.Explicit(ReviewCursor.Move(Vector3i.west, step)); return true; }

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

            // Only switch review mode on when that is the only way to have a live cursor.
            //
            // With the arrows already driving the cursor, or while the peek key is held, the
            // cursor is live already - turning review mode on as well froze movement for no
            // reason and left you in a mode you had not asked for.
            string prefix = "";
            if (!_reviewMode && !_peeking && !Cfg.ArrowsAlwaysReview.Value)
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
            if (_peeking) EndPeek();
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
