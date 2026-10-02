using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AMEAccess
{
    /// <summary>
    /// All settings live in BepInEx\config\hassan.ameaccess.cfg and can be edited
    /// in a text editor, or with a config manager plugin, without rebuilding.
    /// </summary>
    internal static class Cfg
    {
        /// <summary>
        /// Bump this whenever a default key changes.
        ///
        /// BepInEx writes the config file once and then NEVER overwrites a setting that is
        /// already in it. So changing a default in code has no effect on anyone who has run the
        /// mod before - they keep the old key silently, and the documentation stops matching
        /// their install. Raising this version resets the key bindings to the current defaults.
        /// </summary>
        private const int KeyLayoutVersion = 14;

        private static ConfigEntry<int> _layoutVersion;
        private static ConfigFile _file;

        /// <summary>
        /// Write the config to disk.
        ///
        /// BepInEx saves automatically when a config value is set, so this is belt and braces.
        /// It is called by reflection on purpose: an explicit Save method is not something this
        /// code can verify exists, and a missing method would be a BUILD failure rather than a
        /// harmless no-op. Reflection turns that into "nothing happened", and the automatic
        /// save still covers it.
        /// </summary>
        internal static void Save()
        {
            if (_file == null) return;
            try
            {
                var m = _file.GetType().GetMethod("Save", new System.Type[0]);
                if (m != null) m.Invoke(_file, null);
            }
            catch (System.Exception e)
            {
                var log = Plugin.Log;
                if (log != null) log.LogWarning("Could not save the config: " + e.Message);
            }
        }

        private static readonly List<ConfigEntry<KeyboardShortcut>> Bound =
            new List<ConfigEntry<KeyboardShortcut>>();
        private static readonly List<KeyboardShortcut> Defaults = new List<KeyboardShortcut>();
        private static readonly List<string> Sections = new List<string>();
        private static readonly List<string> Names = new List<string>();

        /// <summary>One binding, for the help reader and the rebinder to share.</summary>
        internal struct Binding
        {
            public string Section;
            public string Name;
            public ConfigEntry<KeyboardShortcut> Entry;
            public KeyboardShortcut Default;
        }

        /// <summary>
        /// Every key the mod binds, in the order it was bound.
        ///
        /// The help and the rebinder both read this rather than a hand-written list, so a key
        /// cannot be added to the mod and left out of the help, and cannot be listed under a
        /// name the rebinder does not recognise.
        /// </summary>
        internal static List<Binding> AllBindings
        {
            get
            {
                var all = new List<Binding>();
                for (int i = 0; i < Bound.Count; i++)
                    all.Add(new Binding
                    {
                        Section = Sections[i],
                        Name = Names[i],
                        Entry = Bound[i],
                        Default = Defaults[i]
                    });
                return all;
            }
        }

        /// <summary>Bind a shortcut and remember its default, so it can be reset later.</summary>
        private static ConfigEntry<KeyboardShortcut> BindKey(
            ConfigFile c, string section, string key, KeyboardShortcut def, string desc)
        {
            var e = c.Bind(section, key, def, desc);
            Bound.Add(e);
            Defaults.Add(def);
            Sections.Add(section);
            Names.Add(key);
            return e;
        }

        // --- keys -------------------------------------------------------------
        internal static ConfigEntry<KeyboardShortcut> KeyWhereAmI;
        internal static ConfigEntry<KeyboardShortcut> KeyLookAround;
        internal static ConfigEntry<KeyboardShortcut> KeyIslandInfo;
        internal static ConfigEntry<KeyboardShortcut> KeyFacing;
        internal static ConfigEntry<KeyboardShortcut> KeyRepeat;
        internal static ConfigEntry<KeyboardShortcut> KeySilence;
        internal static ConfigEntry<KeyboardShortcut> KeyStatus;
        internal static ConfigEntry<KeyboardShortcut> KeyScan;
        internal static ConfigEntry<KeyboardShortcut> KeySurvey;
        internal static ConfigEntry<KeyboardShortcut> KeyNextObject;
        internal static ConfigEntry<KeyboardShortcut> KeyPrevObject;
        internal static ConfigEntry<KeyboardShortcut> KeyNextCategory;
        internal static ConfigEntry<KeyboardShortcut> KeyPrevCategory;
        internal static ConfigEntry<KeyboardShortcut> KeyNextItem;
        internal static ConfigEntry<KeyboardShortcut> KeyPrevItem;
        internal static ConfigEntry<KeyboardShortcut> KeyIslandOnly;
        internal static ConfigEntry<KeyboardShortcut> KeyInspect;
        internal static ConfigEntry<KeyboardShortcut> KeyIslandsNearby;
        internal static ConfigEntry<KeyboardShortcut> KeyNextIsland;
        internal static ConfigEntry<KeyboardShortcut> KeyMainRoute;
        internal static ConfigEntry<KeyboardShortcut> KeyPadTest;
        internal static ConfigEntry<KeyboardShortcut> KeyFeedbackMode;
        internal static ConfigEntry<KeyboardShortcut> KeyPadSetup;

        internal static ConfigEntry<string> FeedbackMode;
        internal static ConfigEntry<float> SoundDistance;
        internal static ConfigEntry<string> WaterSound;
        internal static ConfigEntry<bool> PadEnabled;
        internal static ConfigEntry<bool> PadAssignToGame;
        internal static ConfigEntry<bool> PadMovesMonster;
        internal static ConfigEntry<int> PadLeftStickX;
        internal static ConfigEntry<int> PadLeftStickY;
        internal static ConfigEntry<float> PadWalkRate;
        internal static ConfigEntry<bool> PadMenus;
        internal static ConfigEntry<int> PadStickClick, PadNearbyIslands, PadScope, PadUndo, PadReset;
        internal static ConfigEntry<int> PadNextCategory, PadPrevCategory;

        internal static ConfigEntry<int> PadMod1, PadMod2;
        internal static readonly Dictionary<string, ConfigEntry<string>> PadBindings =
            new Dictionary<string, ConfigEntry<string>>();

        /// <summary>
        /// One text binding per action. Written as "3" or "mod1+3", so holding a shoulder gives
        /// a second and third set of the same buttons - the only way forty actions fit on a pad.
        /// </summary>
        private static ConfigEntry<string> PadBind(ConfigFile c, string name, string def, string desc)
        {
            var e = c.Bind("Controller buttons", name, def, desc);
            PadBindings[name] = e;
            return e;
        }
        internal static ConfigEntry<bool> PadInvertY;
        internal static ConfigEntry<bool> PadInvertSet;
        internal static ConfigEntry<bool> PadInvertX;
        internal static ConfigEntry<int> PadRightStickX;
        internal static ConfigEntry<int> PadRightStickY;
        internal static ConfigEntry<float> PadDeadzone;
        internal static ConfigEntry<float> PadRepeatDelay;
        internal static ConfigEntry<float> PadRepeatRate;
        internal static ConfigEntry<int> PadSurvey, PadLookAround, PadPosition, PadNextItem,
                                         PadPrevItem, PadInspect, PadCursorHome, PadRoute;
        internal static ConfigEntry<KeyboardShortcut> KeyListBookmarks;
        internal static ConfigEntry<KeyboardShortcut> KeyReadHints;
        internal static ConfigEntry<KeyboardShortcut> KeySpeechTest;
        internal static ConfigEntry<KeyboardShortcut> KeyDumpPieces;
        internal static ConfigEntry<KeyboardShortcut> KeyRebind;
        internal static ConfigEntry<KeyboardShortcut> KeyArrowMode;
        internal static ConfigEntry<KeyboardShortcut> KeyStats;
        internal static ConfigEntry<KeyboardShortcut> KeyHelp;
        internal static ConfigEntry<KeyboardShortcut> KeyHelpPrev;
        internal static ConfigEntry<KeyboardShortcut> KeyPlaqueRepeat;
        internal static ConfigEntry<KeyboardShortcut> KeyPlaquePrev;

        // The eight-direction cluster: U I O / J K L / M , .  held with Ctrl.
        internal static ConfigEntry<KeyboardShortcut> DirHere;
        internal static ConfigEntry<KeyboardShortcut> DirN;
        internal static ConfigEntry<KeyboardShortcut> DirNE;
        internal static ConfigEntry<KeyboardShortcut> DirE;
        internal static ConfigEntry<KeyboardShortcut> DirSE;
        internal static ConfigEntry<KeyboardShortcut> DirS;
        internal static ConfigEntry<KeyboardShortcut> DirSW;
        internal static ConfigEntry<KeyboardShortcut> DirW;
        internal static ConfigEntry<KeyboardShortcut> DirNW;

        // Review cursor. Laptop friendly: no numpad anywhere. Toggling review mode freezes
        // the monster, which frees up the arrow keys you already use.
        internal static ConfigEntry<KeyboardShortcut> KeyReviewToggle;
        internal static ConfigEntry<KeyboardShortcut> CurNorth;
        internal static ConfigEntry<KeyboardShortcut> CurSouth;
        internal static ConfigEntry<KeyboardShortcut> CurEast;
        internal static ConfigEntry<KeyboardShortcut> CurWest;
        internal static ConfigEntry<KeyboardShortcut> CurHome;
        internal static ConfigEntry<KeyboardShortcut> CurHomeAlt;
        internal static ConfigEntry<KeyboardShortcut> CurJumpToItem;
        internal static ConfigEntry<KeyboardShortcut> CurPeekModifier;
        internal static ConfigEntry<KeyboardShortcut> CurColumn;
        internal static ConfigEntry<KeyboardShortcut> CurRoute;
        internal static ConfigEntry<KeyboardShortcut> CurBigStepModifier;

        // --- behaviour --------------------------------------------------------
        internal static ConfigEntry<bool> AnnounceMoves;
        internal static ConfigEntry<int> MoveDetail;
        internal static ConfigEntry<bool> AnnounceActions;
        internal static ConfigEntry<bool> AnnounceBlocked;
        internal static ConfigEntry<bool> AnnounceIslandChange;
        internal static ConfigEntry<bool> AnnounceUndoReset;
        internal static ConfigEntry<bool> AnnounceGameKeys;
        internal static ConfigEntry<bool> ReadHintsOnShow;
        internal static ConfigEntry<bool> ReadPlaques;
        internal static ConfigEntry<bool> SpeakCoordinates;
        internal static ConfigEntry<bool> CoordinatesFollowGrid;
        internal static ConfigEntry<bool> UseGameCoordinateOrigin;
        internal static ConfigEntry<bool> UsePreviewInteraction;
        internal static ConfigEntry<bool> RespectFog;
        internal static ConfigEntry<bool> NumberObjects;
        internal static ConfigEntry<bool> SpeakBiome;
        internal static ConfigEntry<int> RayDistance;
        internal static ConfigEntry<int> RayRuns;
        internal static ConfigEntry<int> SurveyPadding;
        internal static ConfigEntry<int> SurveyRadius;
        internal static ConfigEntry<int> IslandSearchRadius;
        internal static ConfigEntry<int> IslandListCount;
        internal static ConfigEntry<int> WarpListCount;
        internal static ConfigEntry<bool> AnnounceUnvisitedCount;
        internal static ConfigEntry<int> CursorBigStep;

        // --- output -----------------------------------------------------------
        internal static ConfigEntry<bool> IncidentalInterrupts;
        internal static ConfigEntry<float> IncidentalHoldOff;
        internal static ConfigEntry<float> DuplicateWindow;
        internal static ConfigEntry<bool> Braille;
        internal static ConfigEntry<bool> LogSpeech;
        internal static ConfigEntry<string> SpeechEngine;
        internal static ConfigEntry<bool> EnableSapiFallback;
        internal static ConfigEntry<bool> ReadMenus;
        internal static ConfigEntry<bool> ArrowsAlwaysReview;
        internal static ConfigEntry<bool> CursorFollowsPlayer;
        internal static ConfigEntry<bool> SliderAsPercent;
        internal static ConfigEntry<bool> AnnounceSaveSlots;
        internal static ConfigEntry<float> MenuPollInterval;
        internal static ConfigEntry<KeyboardShortcut> KeyReadFocus;
        internal static ConfigEntry<KeyboardShortcut> KeyReadPanel;

        internal static void Bind(ConfigFile c)
        {
            _file = c;

            Bound.Clear();
            Defaults.Clear();
            Sections.Clear();
            Names.Clear();

            const string K = "Keys";
            KeyWhereAmI = BindKey(c, K, "WhereAmI", new KeyboardShortcut(KeyCode.K),
                "Speak your position and what you are standing on.");
            KeyLookAround = BindKey(c, K, "LookAround", new KeyboardShortcut(KeyCode.L),
                "Speak the four neighbouring tiles and what walking into each would do.");
            KeyIslandInfo = BindKey(c, K, "IslandInfo", new KeyboardShortcut(KeyCode.I),
                "Speak the current island's name and whether you have visited it before.");
            KeyFacing = BindKey(c, K, "Facing", new KeyboardShortcut(KeyCode.E),
                "Describe the thing you are facing, and read its plaque if it has one.");
            KeyRepeat = BindKey(c, K, "Repeat", new KeyboardShortcut(KeyCode.Q),
                "Repeat the last thing spoken.");
            KeySilence = BindKey(c, K, "Silence", new KeyboardShortcut(KeyCode.LeftControl),
                "Stop speech immediately. Left Control on its own, which also means every " +
                "Ctrl+letter in the direction cluster cuts off whatever is speaking before it " +
                "reads the new tile - the same way a screen reader behaves.");
            KeyStatus = BindKey(c, K, "Status", new KeyboardShortcut(KeyCode.U),
                "Speak move count and whether undo or reset is available.");
            KeyScan = BindKey(c, K, "Scan", new KeyboardShortcut(KeyCode.F),
                "Look into the distance in all four directions and report what is there.");
            KeySurvey = BindKey(c, K, "Survey", new KeyboardShortcut(KeyCode.T),
                "Survey the whole island: counts of everything, then the nearest items.");
            KeyNextObject = BindKey(c, K, "NextObject", new KeyboardShortcut(KeyCode.RightBracket),
                "Step to the next nearby object and say where it is.");
            KeyPrevObject = BindKey(c, K, "PrevObject", new KeyboardShortcut(KeyCode.LeftBracket),
                "Step to the previous nearby object.");
            KeyNextCategory = BindKey(c, K, "NextCategory", new KeyboardShortcut(KeyCode.PageDown),
                "Move to the next category of things on the island - trees, logs, rocks and so on.");
            KeyPrevCategory = BindKey(c, K, "PrevCategory", new KeyboardShortcut(KeyCode.PageUp),
                "Move to the previous category.");
            KeyNextItem = BindKey(c, K, "NextItem", new KeyboardShortcut(KeyCode.PageDown, KeyCode.LeftControl),
                "Next item inside the current category. Same as the right bracket key.");
            KeyPrevItem = BindKey(c, K, "PrevItem", new KeyboardShortcut(KeyCode.PageUp, KeyCode.LeftControl),
                "Previous item inside the current category. Same as the left bracket key.");
            KeyIslandOnly = BindKey(c, K, "IslandOnlyToggle", new KeyboardShortcut(KeyCode.X),
                "Switch between listing only what is on the island you are standing on, and " +
                "everything in sight including logs and rafts in the water around it.");
            KeyInspect = BindKey(c, K, "Inspect", new KeyboardShortcut(KeyCode.Return),
                "Full detail on the object you last stepped to, or the one you are facing.");
            KeyPadSetup = BindKey(c, K, "ControllerSetup", new KeyboardShortcut(KeyCode.F7),
                "Set up the controller from inside the game: step through the actions, push a " +
                "stick or press a button to set each one. Like the key changer, but for a pad.");
            KeyFeedbackMode = BindKey(c, K, "SpeechOrSound", new KeyboardShortcut(KeyCode.F6),
                "Switch between speech, sound and both for describing tiles.");
            KeyPadTest = BindKey(c, K, "ControllerTest", new KeyboardShortcut(KeyCode.F5),
                "Say the number of whatever stick or button you move, so you can write it into " +
                "the controller settings below. Press again to stop.");
            KeyMainRoute = BindKey(c, K, "MainRoute", new KeyboardShortcut(KeyCode.F4),
                "Are you on the game's main route? If not, where it is and which postbox lands " +
                "you nearest to it.");
            KeyNextIsland = BindKey(c, K, "NextIsland", new KeyboardShortcut(KeyCode.Backslash),
                "Where to go next: the nearest island you have not visited, how far it is, and " +
                "whether you can already walk there.");
            KeyListBookmarks = BindKey(c, K, "ListBookmarks", new KeyboardShortcut(KeyCode.Quote),
                "List the bookmarks you have set.");
            KeyIslandsNearby = BindKey(c, K, "IslandsNearby", new KeyboardShortcut(KeyCode.N),
                "List other islands in range, with bearing and whether you can walk there now.");
            KeyReadHints = BindKey(c, K, "ReadHints", new KeyboardShortcut(KeyCode.B),
                "Read where the island hints point. Only works when 'Enable Island Hints' is on " +
                "in the game's settings, because that is when a sighted player sees them too.");
            KeyHelpPrev = BindKey(c, K, "HelpPrevious", new KeyboardShortcut(KeyCode.F1, KeyCode.LeftShift),
                "Go back one key in the key list. Either shift key works.");
            KeyHelp = BindKey(c, K, "Help", new KeyboardShortcut(KeyCode.F1),
                "Read the next key in the mod's key list, one key per press. Also writes the whole " +
                "list to the BepInEx log.");
            KeyPlaqueRepeat = BindKey(c, K, "PlaqueRepeat", new KeyboardShortcut(KeyCode.F9),
                "Read the last exhibit plaque again, in full.");
            KeyPlaquePrev = BindKey(c, K, "PlaquePrevious", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Step back through earlier plaques read this session.");
            KeyStats = BindKey(c, K, "Stats", new KeyboardShortcut(KeyCode.Y),
                "Speak overall progress: islands visited, exhibits discovered, time played.");
            KeyReadPanel = BindKey(c, K, "ReadPanel", new KeyboardShortcut(KeyCode.F10),
                "Read everything in the panel around the focused control - on a save slot that " +
                "is the play time, date, islands visited and exhibits found.");
            KeyReadFocus = BindKey(c, K, "ReadFocus", new KeyboardShortcut(KeyCode.F11),
                "Re-read the menu item that currently has focus.");
            KeyArrowMode = BindKey(c, K, "ArrowModeToggle", new KeyboardShortcut(KeyCode.F3),
                "Switch between the arrow keys moving your monster and the arrow keys moving " +
                "the review cursor. Saved straight away, so it survives quitting. Same setting " +
                "as ArrowsAlwaysReview, without opening this file.");
            KeyRebind = BindKey(c, K, "ChangeKeys", new KeyboardShortcut(KeyCode.F2),
                "Change the mod's keys from inside the game, without editing this file.");
            KeyDumpPieces = BindKey(c, K, "DumpPieces", new KeyboardShortcut(KeyCode.F8),
                "Write every piece near you to the BepInEx log, with the reason each was kept " +
                "or dropped from the survey. Use this when something on screen is not listed.");
            KeySpeechTest = BindKey(c, K, "SpeechTest", new KeyboardShortcut(KeyCode.F12),
                "Re-run the speech self test and write the result to BepInEx\\LogOutput.log. " +
                "Works anywhere, including menus. Use this when the mod is silent.");

            const string D = "Direction cluster";
            // U I O / J K L / M , .  - the keys sit where the tiles sit.
            DirNW = BindKey(c, D, "NorthWest", new KeyboardShortcut(KeyCode.U, KeyCode.LeftControl), "Read the tile to the north west.");
            DirN  = BindKey(c, D, "North",     new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl), "Read the tile to the north.");
            DirNE = BindKey(c, D, "NorthEast", new KeyboardShortcut(KeyCode.O, KeyCode.LeftControl), "Read the tile to the north east.");
            DirW  = BindKey(c, D, "West",      new KeyboardShortcut(KeyCode.J, KeyCode.LeftControl), "Read the tile to the west.");
            DirHere = BindKey(c, D, "Here",    new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl), "Read where the cluster is centred: your monster, or the review cursor whenever the cursor is the thing you are moving.");
            DirE  = BindKey(c, D, "East",      new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl), "Read the tile to the east.");
            DirSW = BindKey(c, D, "SouthWest", new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl), "Read the tile to the south west.");
            DirS  = BindKey(c, D, "South",     new KeyboardShortcut(KeyCode.Comma, KeyCode.LeftControl), "Read the tile to the south.");
            DirSE = BindKey(c, D, "SouthEast", new KeyboardShortcut(KeyCode.Period, KeyCode.LeftControl), "Read the tile to the south east.");

            const string C = "Review cursor (review mode only)";
            KeyReviewToggle = BindKey(c, C, "ReviewToggle", new KeyboardShortcut(KeyCode.V),
                "Turn review mode on or off. While it is on your monster cannot move, so the " +
                "arrow keys drive the review cursor instead. Press again to go back to playing.");
            CurNorth = BindKey(c, C, "North", new KeyboardShortcut(KeyCode.UpArrow), "Cursor north (review mode).");
            CurSouth = BindKey(c, C, "South", new KeyboardShortcut(KeyCode.DownArrow), "Cursor south (review mode).");
            CurEast = BindKey(c, C, "East", new KeyboardShortcut(KeyCode.RightArrow), "Cursor east (review mode).");
            CurWest = BindKey(c, C, "West", new KeyboardShortcut(KeyCode.LeftArrow), "Cursor west (review mode).");
            CurHome = BindKey(c, C, "Home", new KeyboardShortcut(KeyCode.Backspace),
                "Snap the cursor back to your monster.");
            CurHomeAlt = BindKey(c, C, "HomeAlt", new KeyboardShortcut(KeyCode.Home),
                "Second key for snapping the cursor back to your monster.");
            CurPeekModifier = BindKey(c, C, "PeekHold", new KeyboardShortcut(KeyCode.LeftAlt),
                "HOLD this and use the arrow keys to move the review cursor WITHOUT entering " +
                "review mode. Movement is frozen only while it is held, so let go and the arrow " +
                "keys walk again. Either alt key works. This is the quick way to look around; " +
                "the review toggle is for a longer look.");
            CurJumpToItem = BindKey(c, C, "JumpToItem", new KeyboardShortcut(KeyCode.End),
                "Jump the review cursor to the object you have selected with the category and " +
                "item keys. Turns review mode on if it is not already.");
            CurColumn = BindKey(c, C, "Column", new KeyboardShortcut(KeyCode.C),
                "Read everything stacked in the cursor's column, with heights.");
            CurRoute = BindKey(c, C, "Route", new KeyboardShortcut(KeyCode.M),
                "Say whether you could walk to the cursor from here, and in how many steps. " +
                "This mirrors the path preview the game already draws for mouse players.");
            CurBigStepModifier = BindKey(c, C, "JumpFiveTiles", new KeyboardShortcut(KeyCode.RightShift),
                "REVIEW MODE ONLY. Hold this with a cursor arrow key to move the review cursor " +
                "five tiles at a time instead of one. It does nothing on its own. How far it " +
                "jumps is set by CursorBigStep.");

            const string B = "Behaviour";
            const string P = "Controller";
            AnnounceMoves = c.Bind(B, "AnnounceMoves", true,
                "Say something after each step.");
            MoveDetail = c.Bind(B, "MoveDetail", 1,
                "How much to say after each step. " +
                "0 = just what you stepped onto. " +
                "1 = that plus your coordinates. " +
                "2 = that plus all four neighbouring tiles, so you always have the full picture " +
                "without pressing anything. Level 2 is verbose but nothing is hidden from you.");
            AnnounceActions = c.Bind(B, "AnnounceActions", true,
                "Announce chopping, pushing, rolling, rafts and similar.");
            AnnounceBlocked = c.Bind(B, "AnnounceBlocked", true,
                "Say \"blocked\" when you try to walk somewhere you cannot go.");
            AnnounceIslandChange = c.Bind(B, "AnnounceIslandChange", true,
                "Announce arriving on a different island.");
            AnnounceUndoReset = c.Bind(B, "AnnounceUndoReset", true,
                "Announce undo, redo and island reset.");
            ReadHintsOnShow = c.Bind(B, "ReadHintsOnShow", true,
                "When hints appear, read where they point straight away instead of only saying " +
                "that they are showing.");
            AnnounceGameKeys = c.Bind(B, "AnnounceGameKeys", true,
                "Announce the effect of the game's own keys - Show Hint and Toggle Grid - which " +
                "change something on screen but would otherwise be silent.");
            ReadPlaques = c.Bind(B, "ReadPlaques", true,
                "Read museum plaques aloud when the game shows one. This is parity with a " +
                "sighted player, who simply reads the plaque on screen.");
            CoordinatesFollowGrid = c.Bind(B, "CoordinatesFollowGrid", true,
                "Let the game's Toggle Grid key decide whether coordinates are spoken. The grid " +
                "is what shows a sighted player the tile lines, so this keeps the two in step: " +
                "grid on, coordinates spoken; grid off, quiet. Set false to use SpeakCoordinates " +
                "on its own instead.");
            // ---- Controller ------------------------------------------------------------
            //
            // The left stick is the game's: it moves your monster, and the game's own buttons
            // handle undo, reset and the rest. These settings are only for what the game has no
            // equivalent of.
            WaterSound = c.Bind(B, "WaterSound", "split",
                "Which of the game's splashes water uses. Try these if the current one sounds " +
                "doubled or is hard to hear: split, roll, knock, raft, standup, fall. Each is a " +
                "single event from the game, and they differ in length and sharpness.");

            SoundDistance = c.Bind(B, "SoundDistance", 2.5f,
                "How close the tile sounds are played to you, which is what makes them audible " +
                "over the music. They are still played in the direction of the tile, so east " +
                "still sounds east - only the distance is shortened. Lower is louder. Zero " +
                "plays them flat, as loud as possible, with no direction at all.");

            FeedbackMode = c.Bind(B, "FeedbackMode", "speech",
                "How tiles are reported as you move the cursor or read around you: speech, " +
                "sound, or both. Sound uses the game's own audio, played from the tile itself, " +
                "so it comes from the right direction. Names, plaques and menus are always " +
                "spoken whatever this is set to.");

            PadMenus = c.Bind(P, "LeftStickMovesMenus", true,
                "Move through menus with the left stick, and choose with the stick click. The " +
                "game does this itself for a controller it recognises; for one it does not, " +
                "the menus cannot be used at all without this.");
            PadStickClick = c.Bind(P, "ButtonStickClick", 10,
                "Button number for pressing the left stick in, which chooses a menu item. 10 is " +
                "the usual number; the controller setup key finds yours.");

            PadMovesMonster = c.Bind(P, "LeftStickMovesMonster", true,
                "Move your monster with the left stick. The game does this itself for a " +
                "controller it recognises; for one it does not, nothing happens without this.");
            PadLeftStickX = c.Bind(P, "LeftStickX", 0,
                "Axis number for the left stick left and right.");
            PadLeftStickY = c.Bind(P, "LeftStickY", 1,
                "Axis number for the left stick up and down.");
            PadWalkRate = c.Bind(P, "WalkRate", 0.22f,
                "Seconds between steps when the left stick is held.");

            PadAssignToGame = c.Bind(P, "GiveControllerToGame", true,
                "Hand the controller to the game if Rewired has not assigned it. Some pads are " +
                "not recognised and the game then gets nothing from them, so the left stick " +
                "does not move your monster. Turn off if it causes trouble.");

            PadEnabled = c.Bind(P, "Enabled", true,
                "Use a controller for the review cursor and the actions set below.");
            // No defaults on purpose. Axis numbers differ between controllers, and a wrong
            // guess is not harmless: a DualSense rests its triggers at -1, so an axis picked
            // blindly can look like a stick held hard over, and the cursor runs away. Unbound
            // until you set it from the controller test.
            PadRightStickX = c.Bind(P, "RightStickX", -1,
                "Axis number for the right stick left and right. -1 is unbound. Press the " +
                "controller test key, push the stick right, and use the number it says.");
            PadRightStickY = c.Bind(P, "RightStickY", -1,
                "Axis number for the right stick up and down. -1 is unbound. Press the " +
                "controller test key, push the stick up, and use the number it says.");
            PadInvertX = c.Bind(P, "InvertLeftRight", false,
                "Set true if pushing the stick right moves the cursor west. The controller " +
                "setup key works this out for you.");
            PadInvertSet = c.Bind(P, "InvertUpDownWasSet", false,
                "Do not edit. Remembers that the up and down setting has been decided, so a " +
                "recognised controller does not keep overwriting your choice.");
            PadInvertY = c.Bind(P, "InvertUpDown", false,
                "Set true if pushing the stick up moves the cursor south.");
            PadDeadzone = c.Bind(P, "Deadzone", 0.5f,
                "How far the stick must go before it counts. Raise it if the cursor drifts.");
            PadRepeatDelay = c.Bind(P, "RepeatDelay", 0.35f,
                "Seconds before a held stick starts repeating.");
            PadRepeatRate = c.Bind(P, "RepeatRate", 0.15f,
                "Seconds between steps once it is repeating.");

            PadSurvey = c.Bind(P, "ButtonSurvey", -1, "Button number for the island survey. -1 is unbound.");
            PadLookAround = c.Bind(P, "ButtonLookAround", -1, "Button number for look around. -1 is unbound.");
            PadPosition = c.Bind(P, "ButtonPosition", -1, "Button number for your position. -1 is unbound.");
            PadNextItem = c.Bind(P, "ButtonNextItem", -1, "Button number for the next object. -1 is unbound.");
            PadPrevItem = c.Bind(P, "ButtonPreviousItem", -1, "Button number for the previous object. -1 is unbound.");
            PadInspect = c.Bind(P, "ButtonInspect", -1, "Button number for full detail. -1 is unbound.");
            PadCursorHome = c.Bind(P, "ButtonCursorHome", -1, "Button number to put the cursor back on you. -1 is unbound.");
            PadRoute = c.Bind(P, "ButtonMainRoute", -1, "Button number for the main route. -1 is unbound.");
            PadNearbyIslands = c.Bind(P, "ButtonNearbyIslands", -1, "Button number for nearby islands. -1 is unbound.");
            PadScope = c.Bind(P, "ButtonThisIslandOnly", -1, "Button number for switching between this island only and everything in sight. -1 is unbound.");
            PadUndo = c.Bind(P, "ButtonUndo", -1, "Button number for undo. -1 is unbound.");
            PadReset = c.Bind(P, "ButtonResetIsland", -1, "Button number for resetting the island. -1 is unbound.");
            // ---- what each button does ------------------------------------------------
            //
            // Everything the keyboard can do is here, so a player can pick whichever handful
            // they actually want. Only the ones you cannot play without are given a default:
            // moving, looking, choosing, undo, reset and the menu. The rest start unbound and
            // are set with the controller setup key, which also records whether a shoulder was
            // held, so putting something on the second or third set is just holding a shoulder
            // while you press.
            //
            // The defaults assume the four face buttons are 0 to 3, which is what a DualSense
            // reported. If yours differs, the setup key fixes it without touching this file.
            // The two stick clicks carry the two actions you reach for constantly, so they are
            // under the thumbs already holding the sticks: press the left stick to choose, the
            // right stick to open or close the menu.
            //
            // 10 and 11 are the stick clicks in the standard layout, which is the one this
            // controller reported. If yours numbers them differently, the setup key fixes it
            // without touching this file.
            PadBind(c, "Choose",            "10", "Press the thing in front of you, or choose a menu item. The left stick click.");
            PadBind(c, "Menu",              "11", "Open or close the game's menu, the escape key's job. The right stick click.");
            PadBind(c, "Undo",              "1", "Undo the last move.");
            PadBind(c, "LookAround",        "2", "What is on all four sides of you.");

            PadBind(c, "IslandSurvey",      "", "Counts of everything on the island.");
            PadBind(c, "Position",          "", "Where you are and what you are standing on.");
            PadBind(c, "Facing",            "", "Which way you are facing, and the plaque in front of you.");
            PadBind(c, "DistanceScan",      "", "How far it is to water in each direction.");
            PadBind(c, "IslandInfo",        "", "The island's name and size.");
            PadBind(c, "Status",            "", "Moves made, and whether undo and reset are available.");
            PadBind(c, "Stats",             "", "Islands visited, exhibits found, time played.");
            PadBind(c, "ResetIsland",       "", "Put the island back as it started.");

            PadBind(c, "NextObject",        "", "Next object on the island, or next destination on the warp map.");
            PadBind(c, "PreviousObject",    "", "Previous object, or previous destination.");
            PadBind(c, "NextGroup",         "", "Next category, or next group of destinations.");
            PadBind(c, "PreviousGroup",     "", "Previous category or group.");
            PadBind(c, "FullDetail",        "", "Everything known about the selected object.");
            PadBind(c, "ThisIslandOnly",    "", "Switch between this island only and everything in sight.");

            PadBind(c, "CursorHome",        "", "Put the review cursor back on your monster.");
            PadBind(c, "ReadColumn",        "", "Everything stacked on the cursor's tile.");
            PadBind(c, "RouteToCursor",     "", "Walking directions to the review cursor.");
            PadBind(c, "JumpCursorToObject","", "Put the cursor on the selected object.");

            PadBind(c, "NearbyIslands",     "", "Other islands in range.");
            PadBind(c, "NextIsland",        "", "The nearest island you have not visited.");
            PadBind(c, "MainRoute",         "", "Whether you are on the game's main route, and where it is.");

            PadBind(c, "ReadHints",         "", "Where the island hints point, while they are showing.");
            PadBind(c, "RepeatLast",        "", "Say the last thing again.");
            PadBind(c, "RepeatPlaque",      "", "Read the last exhibit plaque again.");
            PadBind(c, "PreviousPlaque",    "", "Step back through the plaques you have read.");

            PadBind(c, "North",             "", "Read the tile to the north.");
            PadBind(c, "South",             "", "Read the tile to the south.");
            PadBind(c, "East",              "", "Read the tile to the east.");
            PadBind(c, "West",              "", "Read the tile to the west.");

            PadBind(c, "ReviewMode",        "", "Freeze your monster and move a cursor instead.");
            PadBind(c, "SpeechOrSound",     "", "Switch between speech, sound and both.");
            PadBind(c, "ListBookmarks",     "", "List the bookmarks you have set.");

            // The two shoulder buttons that reach the second and third set of actions. Set
            // them from the controller setup key; without them only the plain buttons work.
            PadMod1 = c.Bind(P, "FirstShoulder", -1,
                "Button number of the shoulder held for the second set of actions.");
            PadMod2 = c.Bind(P, "SecondShoulder", -1,
                "Button number of the shoulder held for the third set of actions.");

            PadNextCategory = c.Bind(P, "ButtonNextGroup", -1, "Button number for the next group: categories on an island, groups of destinations on the warp map. -1 is unbound.");
            PadPrevCategory = c.Bind(P, "ButtonPreviousGroup", -1, "Button number for the previous group. -1 is unbound.");

            SpeakCoordinates = c.Bind(B, "SpeakCoordinates", true,
                "Include your grid coordinates when speaking your position. A sighted player " +
                "does not see these; they are an orientation aid. Turn off for a purer experience.");
            UseGameCoordinateOrigin = c.Bind(B, "UseGameCoordinateOrigin", true,
                "Offset spoken coordinates by the game's own island-coordinate origin (200, 200) " +
                "so they line up with the island names the game displays.");
            UsePreviewInteraction = c.Bind(B, "UsePreviewInteraction", true,
                "Use the game's own interaction preview to say whether a direction would chop or " +
                "push. Accurate, but it is not a completely side-effect-free call while you are " +
                "standing on a raft. Turn this off if you ever see odd raft behaviour.");
            SpeakBiome = c.Bind(B, "SpeakBiome", true,
                "Mention the biome in island names. A whole region shares one biome, so it is " +
                "only spoken when it changes; asking with the island key always reports it.");
            NumberObjects = c.Bind(B, "NumberObjects", true,
                "Number things there is more than one of, so two trees become tree 1 and " +
                "tree 2. Numbers are fixed per island and do not change as you move.");
            RespectFog = c.Bind(B, "RespectFog", true,
                "Skip pieces hidden behind the fog of war, so you are told what a sighted player " +
                "could actually see. Turn off to have the mod describe fogged areas too.");
            RayDistance = c.Bind(B, "RayDistance", 20,
                "How many tiles the distance scan looks along each direction.");
            RayRuns = c.Bind(B, "RayRuns", 5,
                "How many distinct stretches of terrain the distance scan reports per direction.");
            SurveyRadius = c.Bind(B, "SurveyRadius", 20,
                "Always include anything within this many tiles of you, whatever the island's " +
                "land bounds say. Catches things standing on structures rather than on ground.");
            SurveyPadding = c.Bind(B, "SurveyPadding", 6,
                "How far past the island's shoreline the survey looks, so floating logs and rafts " +
                "are included.");
            IslandSearchRadius = c.Bind(B, "IslandSearchRadius", 40,
                "How far to look when listing nearby islands.");
            IslandListCount = c.Bind(B, "IslandListCount", 5,
                "How many nearby islands to name.");
            WarpListCount = c.Bind(B, "WarpListCount", 6,
                "How many warp destinations to name before saying how many more there are.");
            AnnounceUnvisitedCount = c.Bind(B, "AnnounceUnvisitedCount", false,
                "Add how many other unvisited islands are in range. Off by default: the number " +
                "runs into the dozens and does not tell you anything you can act on.");
            CursorBigStep = c.Bind(B, "CursorBigStep", 5,
                "How many tiles the review cursor jumps when the big-step modifier is held.");

            const string O = "Output";
            IncidentalInterrupts = c.Bind(O, "IncidentalInterrupts", true,
                "Let automatic feedback cut off whatever is currently being spoken. " +
                "Turn off if speech feels clipped during fast movement.");
            IncidentalHoldOff = c.Bind(O, "IncidentalHoldOff", 0.35f,
                "Seconds after you press a key during which automatic feedback stays quiet.");
            DuplicateWindow = c.Bind(O, "DuplicateWindow", 0.6f,
                "Seconds within which an identical repeated line is suppressed, to stop stutter.");
            Braille = c.Bind(O, "Braille", false,
                "Also send text to a braille display via UniversalSpeech.");
            _layoutVersion = c.Bind("Internal", "KeyLayoutVersion", 0,
                "Do not edit. Used to reset key bindings when the mod's defaults change.");

            LogSpeech = c.Bind(O, "LogSpeech", false,
                "Write everything spoken to the BepInEx log. Useful when reporting a problem.");
            SpeechEngine = c.Bind(O, "SpeechEngine", "auto",
                "Which speech route to use: auto, sapi, nvda, or jaws. " +
                "'auto' asks UniversalSpeech to pick. Use 'sapi' if a screen reader is running " +
                "but silent - JAWS sleep mode being the usual reason - because SAPI bypasses the " +
                "screen reader completely and speaks through Windows directly.");
            EnableSapiFallback = c.Bind(O, "EnableSapiFallback", true,
                "Turn SAPI on at startup so there is always something able to speak, even when " +
                "no screen reader is running.");
            CursorFollowsPlayer = c.Bind(B, "CursorFollowsPlayer", true,
                "With the arrow keys driving the cursor, bring the cursor back to your monster " +
                "each time you walk. Turn off to leave it where you put it, which is useful for " +
                "keeping a spot marked while you walk towards it.");
            ArrowsAlwaysReview = c.Bind(B, "ArrowsAlwaysReview", false,
                "Give the arrow keys to the review cursor permanently, and walk with W A S D. " +
                "Off by default. With it on you never switch modes, but the arrow keys no " +
                "longer move your monster, which is what they do for everyone else. The review " +
                "toggle still works either way, so there is always a way back.");
            SliderAsPercent = c.Bind(O, "SliderAsPercent", false,
                "Read sliders as a percentage instead of as steps. Off by default: a volume " +
                "slider moves in whole steps, so \"10 of 15\" tells you what one arrow press " +
                "will do, where \"67 percent\" does not.");
            ReadMenus = c.Bind(O, "ReadMenus", true,
                "Read menus, buttons and settings aloud as you move through them.");
            AnnounceSaveSlots = c.Bind(O, "AnnounceSaveSlots", true,
                "On the load screen, read the save's play time, date, islands and exhibits as " +
                "soon as you move onto it, rather than waiting for the read-panel key.");
            MenuPollInterval = c.Bind(O, "MenuPollInterval", 0.1f,
                "How often to check whether menu focus changed, in seconds.");
        }

        /// <summary>
        /// Should coordinates be spoken right now?
        ///
        /// By default this follows the game's own grid toggle, so one key controls both the
        /// tile lines a sighted player sees and the numbers you hear. Turning the grid off
        /// quietens the per-step announcements without touching anything else.
        /// </summary>
        internal static bool CoordsOn
        {
            get
            {
                if (!SpeakCoordinates.Value) return false;
                if (!CoordinatesFollowGrid.Value) return true;
                try { return global::Config.gridView; }
                catch { return SpeakCoordinates.Value; }
            }
        }

        /// <summary>
        /// Put every key back to the current default if the layout changed since last run.
        /// Returns the number of keys reset, so the mod can say so out loud.
        /// </summary>
        internal static int ResetKeysIfOutdated()
        {
            if (_layoutVersion == null) return 0;
            if (_layoutVersion.Value >= KeyLayoutVersion) return 0;

            int changed = 0;
            for (int i = 0; i < Bound.Count && i < Defaults.Count; i++)
            {
                if (Bound[i].Value.MainKey == Defaults[i].MainKey) continue;
                Bound[i].Value = Defaults[i];
                changed++;
            }
            _layoutVersion.Value = KeyLayoutVersion;
            return changed;
        }

        /// <summary>
        /// Is a modifier held, counting the left and right key as the same one?
        /// </summary>
        internal static bool HeldEither(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftControl: case KeyCode.RightControl:
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case KeyCode.LeftShift: case KeyCode.RightShift:
                    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case KeyCode.LeftAlt: case KeyCode.RightAlt:
                    return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                default:
                    return k != KeyCode.None && Input.GetKey(k);
            }
        }

        /// <summary>
        /// Is this binding being pressed right now?
        ///
        /// Not KeyboardShortcut.IsDown(), because that matches the exact modifier keys stored in
        /// the binding. A binding of Shift plus F1 stores LEFT shift, so pressing right shift
        /// with F1 did nothing at all - and the same went for right control with the direction
        /// cluster. Left and right of the same modifier are the same key to a person.
        ///
        /// The modifiers are read out of KeyboardShortcut.ToString, which renders as
        /// "F1 + LeftShift". If that text does not start with the main key, the format is not
        /// what we expect and we fall back to IsDown rather than guess.
        ///
        /// The match is exact in the other direction too: a binding with no modifiers does NOT
        /// fire while control is held, so Ctrl+K cannot also trigger plain K.
        /// </summary>
        internal static bool Pressed(ConfigEntry<KeyboardShortcut> e)
        {
            return Pressed(e, KeyCode.None);
        }

        /// <summary>
        /// As above, but treat one modifier as "do not care".
        ///
        /// Needed for the review cursor's arrow keys: they are bound with no modifiers, and the
        /// exact match above meant that holding the jump-five-tiles shift stopped the arrow
        /// binding matching at all - so the cursor sat still and only the modifier was noticed.
        /// </summary>
        internal static bool Pressed(ConfigEntry<KeyboardShortcut> e, KeyCode ignore)
        {
            var s = e.Value;
            var main = s.MainKey;
            if (main == KeyCode.None) return false;
            if (!Input.GetKeyDown(main)) return false;

            bool wantCtrl = false, wantShift = false, wantAlt = false;

            string raw = s.ToString();
            var parts = (raw ?? "").Split('+');

            if (parts.Length == 0 || parts[0].Trim() != main.ToString())
                return s.IsDown();          // unfamiliar format; let BepInEx decide

            for (int i = 1; i < parts.Length; i++)
            {
                string m = parts[i].Trim();
                if (m.EndsWith("Control")) wantCtrl = true;
                else if (m.EndsWith("Shift")) wantShift = true;
                else if (m.EndsWith("Alt")) wantAlt = true;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // Whatever we were told to ignore stops mattering in either direction.
            if (ignore == KeyCode.LeftControl || ignore == KeyCode.RightControl) ctrl = wantCtrl;
            if (ignore == KeyCode.LeftShift || ignore == KeyCode.RightShift) shift = wantShift;
            if (ignore == KeyCode.LeftAlt || ignore == KeyCode.RightAlt) alt = wantAlt;

            return ctrl == wantCtrl && shift == wantShift && alt == wantAlt;
        }
    }
}
