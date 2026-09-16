using AMEAccess.Speech;

namespace AMEAccess.Game
{
    /// <summary>
    /// Notification plumbing.
    ///
    /// The game exposes a global static class, Events, holding roughly a hundred PUBLIC STATIC
    /// MUTABLE DELEGATE FIELDS (Events.VoidEvent, Events.IslandEvent, Events.LogEvent, ...).
    /// That means the mod can subscribe with plain C# += and needs NO Harmony patches at all
    /// for notifications. This is far stabler than patching methods, so it is what we use.
    ///
    /// We deliberately subscribe only to things a sighted player would also perceive:
    /// stepping somewhere, chopping, pushing, arriving on an island, undoing, resetting,
    /// a plaque appearing. No solver output, no route hints, nothing the game would not
    /// otherwise show.
    /// </summary>
    internal static class Hooks
    {
        private static bool _subscribed;
        private static Island _lastIsland;

        internal static void Subscribe()
        {
            if (_subscribed) return;

            Events.playerMoved += OnPlayerMoved;
            Events.playerChopped += OnChopped;
            Events.playerPushed += OnPushed;
            Events.playerRolled += OnRolled;
            // NOT playerSat/playerStood: those fire every time the monster's idle pose
            // changes, which is on every single step. The bench events are the meaningful ones.
            Events.playerSatOnBench += OnSatOnBench;
            Events.playerStoodFromBench += OnStoodFromBench;
            Events.playerActed += OnActed;
            Events.playerBalanced += OnBalanced;
            Events.playerPeeked += OnPeeked;
            Events.treeChopped += OnTreeChopped;
            Events.logSplashed += OnLogSplashed;
            Events.logStartedRolling += OnLogRolling;
            Events.raftPushoff += OnRaftPushoff;
            Events.playerStartedRidingRaft += OnRaftStart;
            Events.playerStoppedRidingRaft += OnRaftStop;
            Events.playerDriftedOffOnRaft += OnRaftDrift;

            Events.playerSteppedOntoIsland += OnSteppedOntoIsland;
            Events.islandDiscovered += OnIslandDiscovered;
            Events.islandReset += OnIslandReset;

            Events.undo += OnUndo;
            Events.redo += OnRedo;
            Events.reset += OnReset;

            Events.captionShown += OnCaptionShown;
            Events.logFellIntoWater += OnLogInWater;
            Events.raftFormed += OnRaftFormed;

            _subscribed = true;
        }

        internal static void Unsubscribe()
        {
            if (!_subscribed) return;

            Events.playerMoved -= OnPlayerMoved;
            Events.playerChopped -= OnChopped;
            Events.playerPushed -= OnPushed;
            Events.playerRolled -= OnRolled;
            Events.playerSatOnBench -= OnSatOnBench;
            Events.playerStoodFromBench -= OnStoodFromBench;
            Events.playerActed -= OnActed;
            Events.playerBalanced -= OnBalanced;
            Events.playerPeeked -= OnPeeked;
            Events.treeChopped -= OnTreeChopped;
            Events.logSplashed -= OnLogSplashed;
            Events.logStartedRolling -= OnLogRolling;
            Events.raftPushoff -= OnRaftPushoff;
            Events.playerStartedRidingRaft -= OnRaftStart;
            Events.playerStoppedRidingRaft -= OnRaftStop;
            Events.playerDriftedOffOnRaft -= OnRaftDrift;

            Events.playerSteppedOntoIsland -= OnSteppedOntoIsland;
            Events.islandDiscovered -= OnIslandDiscovered;
            Events.islandReset -= OnIslandReset;

            Events.undo -= OnUndo;
            Events.redo -= OnRedo;
            Events.reset -= OnReset;

            Events.captionShown -= OnCaptionShown;
            Events.logFellIntoWater -= OnLogInWater;
            Events.raftFormed -= OnRaftFormed;

            _subscribed = false;
        }

        // --- movement ---------------------------------------------------------

        private static void OnPlayerMoved()
        {
            _movedThisFrame = true;
            if (!Cfg.AnnounceMoves.Value || !Refs.InPlay) return;

            // How much to say is the player's call. A sighted player re-reads the whole screen
            // for free after every step, so level 2 is the closest thing to parity; it is just
            // slower to listen to than it is to look at.
            int level = Cfg.MoveDetail.Value;
            string text = Describe.Underfoot();

            if (level >= 1 && Cfg.CoordsOn)
                text = Naming.Coords(Refs.PlayerPos) + ", " + text;

            if (level >= 2)
                text = text + ". " + Describe.LookAround();

            Talk.Incidental(text);
        }

        private static void OnChopped()
        {
            _interactedThisFrame = true;
            if (!Cfg.AnnounceActions.Value) return;
            Talk.Incidental("Chopped.");
        }

        private static void OnPushed()
        {
            _interactedThisFrame = true;
            if (!Cfg.AnnounceActions.Value) return;
            Talk.Incidental("Pushed.");
        }

        private static void OnRolled()
        {
            _interactedThisFrame = true;
            if (!Cfg.AnnounceActions.Value) return;
            Talk.Incidental("Rolled.");
        }

        private static void OnSatOnBench() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Sat on the bench."); }
        private static void OnStoodFromBench() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Off the bench."); }
        private static void OnBalanced() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Balancing."); }
        private static void OnPeeked() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Peeking over the edge."); }
        private static void OnTreeChopped(TreeLog t) { if (Cfg.AnnounceActions.Value) Talk.Incidental("Tree chopped."); }
        private static void OnLogSplashed(Log l) { if (Cfg.AnnounceActions.Value) Talk.Incidental("Splash."); }
        private static void OnLogRolling(Log l) { if (Cfg.AnnounceActions.Value) Talk.Incidental("Log rolling."); }
        private static void OnRaftPushoff() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Raft pushing off."); }

        // --- blocked-move detection ------------------------------------------
        // playerActed fires for every committed action; playerMoved only fires when the
        // position actually changed. If we act and do not move, and nothing interacted, the
        // way was blocked - which is exactly the thing a sighted player sees and we would
        // otherwise leave in silence.
        private static bool _actedThisFrame;
        private static bool _movedThisFrame;
        private static bool _interactedThisFrame;

        private static void OnActed() { _actedThisFrame = true; }

        /// <summary>Called once per frame by the plugin, after all events have fired.</summary>
        internal static void EndOfFrame()
        {
            if (_actedThisFrame && !_movedThisFrame && !_interactedThisFrame
                && Cfg.AnnounceBlocked.Value && Refs.InPlay)
            {
                // The monster turns to face a move it could not make, so its facing is the
                // direction that was refused.
                string why = Describe.BlockedReason(Refs.PlayerFacing);
                Talk.Incidental(string.IsNullOrEmpty(why) ? "Blocked." : "Blocked by " + why + ".");
            }
            _actedThisFrame = false;
            _movedThisFrame = false;
            _interactedThisFrame = false;
        }

        private static void OnRaftStart() { if (Cfg.AnnounceActions.Value) Talk.Incidental("On the raft."); }
        private static void OnRaftStop() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Off the raft."); }
        private static void OnRaftDrift() { if (Cfg.AnnounceActions.Value) Talk.Explicit("Drifting away on the raft."); }
        private static void OnRaftFormed() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Raft formed."); }
        private static void OnLogInWater() { if (Cfg.AnnounceActions.Value) Talk.Incidental("Log in the water."); }

        // --- islands ----------------------------------------------------------

        private static void OnSteppedOntoIsland(Island island)
        {
            if (!Cfg.AnnounceIslandChange.Value || island == null) return;
            if (island == _lastIsland) return;
            _lastIsland = island;
            Numbering.Forget();      // renumber for the new island
            Survey.RebuildCycle();   // and step through this island's objects, not the last one's
            Talk.Incidental(Naming.IslandLabel(island, Cfg.CoordsOn) + ".");
        }

        private static void OnIslandDiscovered(Island island)
        {
            if (!Cfg.AnnounceIslandChange.Value || island == null) return;
            // The game marks a first visit visually; this is the audible equivalent.
            Talk.Explicit("New island. " + Naming.IslandLabel(island) + ".");
        }

        private static void OnIslandReset(Island island)
        {
            if (!Cfg.AnnounceUndoReset.Value) return;
            Talk.Explicit("Island reset.");
        }

        // --- history ----------------------------------------------------------

        private static void OnUndo() { if (Cfg.AnnounceUndoReset.Value) Talk.Incidental("Undo."); }
        private static void OnRedo() { if (Cfg.AnnounceUndoReset.Value) Talk.Incidental("Redo."); }
        private static void OnReset() { if (Cfg.AnnounceUndoReset.Value) Talk.Explicit("Reset."); }

        // --- plaques ----------------------------------------------------------

        private static void OnCaptionShown()
        {
            if (!Cfg.ReadPlaques.Value) return;

            // The caption viewer has just been handed a landmark group. Read what it is showing.
            var viewer = Refs.Captions;
            if (viewer == null) return;

            var group = viewer.landmarkGroup;
            if (group == null) return;

            var table = viewer.exhibitDescriptions;
            if (table == null) return;

            var desc = table.GetExhibitDescriptionForLandmarkGroup(group);
            if (desc == null) return;

            var strings = Globals.strings;
            if (strings == null) return;

            string title = Util.Rich.Strip(strings.Get(desc.itemId, "auto"));
            string body = Util.Rich.Strip(strings.Get(desc.descriptionId, "auto"));

            // Keep it before speaking, so a plaque talked over by something else is still
            // recoverable with the plaque-repeat key.
            PlaqueLog.Record(title, body, Refs.CurrentIsland);

            string text = ((title ?? "") + ". " + (body ?? "")).Trim();
            if (text != ".") Talk.Explicit(text);
        }
    }
}
