using System.Collections.Generic;
using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Turns board state into the sort of thing a sighted player takes in at a glance.
    ///
    /// Two facts drive most of this:
    ///
    ///  1. PieceType is a BIT FLAG enum, not a sequence. Land=1, Log=2, Player=4, Raft=8,
    ///     Rock=16, PassiveSpawnPoint=32, Tree=64, TreeStump=128, ShallowWater=256, Ramp=512,
    ///     ... Campfire=65536, Obstacle=131072, Monument=262144, Villager=524288,
    ///     Trigger=4194304, Landmark=8388608, Caption=16777216. Comparing with == works only
    ///     because a piece reports exactly one flag; never assume ordinal values.
    ///
    ///  2. There is no Water piece. Open water is the ABSENCE of a physical piece, which is
    ///     why Board.GetPhysicalPieceBelow returning null means "water" and not "error".
    ///     ShallowWater is a real piece, and is not physical.
    /// </summary>
    internal static class Describe
    {
        /// <summary>Short human name for whatever a piece is.</summary>
        internal static string PieceName(Piece p)
        {
            string name = BaseName(p);

            // Number it only where there is more than one of its kind on the island, so that
            // two trees are "tree 1" and "tree 2" instead of both just "tree".
            int n = Numbering.Of(p);
            return n > 0 ? name + " " + n : name;
        }

        private static string BaseName(Piece p)
        {
            if (p == null) return "water";

            switch (p.type)
            {
                case PieceType.Land: return "land";
                case PieceType.ShallowWater: return "shallow water";
                case PieceType.Rock: return "rock";
                case PieceType.Ramp: return "ramp";
                case PieceType.TreeStump: return "stump";
                case PieceType.Tree: return TreeName(p);
                case PieceType.Obstacle: return "boulder";
                case PieceType.Monument: return "monument";
                case PieceType.Campfire: return "campfire";
                case PieceType.WarpPoint: return "postbox";
                case PieceType.Villager: return VillagerName(p);
                case PieceType.Landmark: return LandmarkName(p);
                case PieceType.Caption: return CaptionName();
                case PieceType.ActiveSpawnPoint:
                case PieceType.PassiveSpawnPoint: return "arrival point";
                case PieceType.Player: return "you";
                case PieceType.Raft: return RaftName(p);
                case PieceType.Log: return LogName(p);
                case PieceType.Trigger: return "trigger";
                default: return "something";
            }
        }

        /// <summary>
        /// A tree, and crucially the length of the log it will become.
        ///
        /// TreeLog.height is stump height + 4 * logPrefab.length, so logPrefab.length is
        /// literally the log this tree yields. A sighted player judges that from how tall the
        /// tree looks, and it decides whether the tree is worth chopping at all - a 2 long log
        /// will not span a 3 tile gap.
        /// </summary>
        private static string TreeName(Piece p)
        {
            var tree = p as TreeLog;
            if (tree == null || tree.logPrefab == null) return "tree";
            return "tree, log " + tree.logPrefab.length;
        }

        internal static int TreeLogLength(Piece p)
        {
            var tree = p as TreeLog;
            return (tree == null || tree.logPrefab == null) ? 0 : tree.logPrefab.length;
        }

        private static string LogName(Piece p)
        {
            var log = p as Log;
            if (log == null) return "log";
            if (log.standing) return "standing log";
            // A lying log runs along one axis. Which axis matters, because you can only roll it
            // sideways and only push it end-on.
            var d = log.direction;
            string axis = (d.x != 0) ? "east to west" : "north to south";
            return "log lying " + axis + (log.length > 1 ? ", " + log.length + " long" : "");
        }

        private static string RaftName(Piece p)
        {
            var r = p as Raft;
            if (r == null) return "raft";
            return r.length > 1 ? "raft, " + r.length + " long" : "raft";
        }

        private static string VillagerName(Piece p)
        {
            var v = p as Villager;
            if (v == null) return "someone";
            string name = GameText.VillagerDisplayName(v);
            return string.IsNullOrEmpty(name) ? "someone" : name;
        }

        /// <summary>
        /// A Caption piece carries no data of its own - the exhibit it belongs to is recorded on
        /// the island, in Island.exhibitDescriptionId. Use that title where there is one.
        /// </summary>
        private static string CaptionName()
        {
            var island = Refs.CurrentIsland;
            if (island != null && !string.IsNullOrEmpty(island.exhibitDescriptionId))
            {
                string t = GameText.ExhibitTitleById(island.exhibitDescriptionId);
                if (!string.IsNullOrEmpty(t)) return t;
            }
            return "plaque";
        }

        private static string LandmarkName(Piece p)
        {
            var lm = p as Landmark;
            if (lm == null) return "exhibit";
            string name = GameText.ExhibitTitle(lm);
            return string.IsNullOrEmpty(name) ? "exhibit" : name;
        }

        // -------------------------------------------------------------------------

        /// <summary>
        /// What is sitting on top of a piece.
        ///
        /// Later puzzles stack things: a log on a stump, a log on another log, a log carried on
        /// a raft. Naming only the topmost object loses the arrangement, which is usually the
        /// whole point of the position. Board.GetPhysicalPiecesStackedOn answers this directly.
        /// </summary>
        internal static string StackedOn(Piece p)
        {
            var board = Refs.Board;
            if (board == null || p == null) return null;

            var on = board.GetPhysicalPiecesStackedOn(p.bounds);
            if (on == null || on.Count == 0) return null;

            var sb = new StringBuilder();
            for (int i = 0; i < on.Count; i++)
            {
                var q = on[i];
                if (q == null || q == p) continue;
                if (q.type == PieceType.Player) continue;
                if (sb.Length > 0) sb.Append(" and ");
                sb.Append(PieceName(q));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>What a piece is resting on, when that is not plain land.</summary>
        internal static string RestingOn(Piece p)
        {
            var board = Refs.Board;
            if (board == null || p == null) return null;

            var under = board.GetPhysicalPieceBelow(p.position);
            if (under == null) return "water";
            if (under.type == PieceType.Land) return null;      // ordinary ground, not worth saying
            return PieceName(under);
        }

        /// <summary>
        /// What you are standing on right now.
        /// </summary>
        internal static string Underfoot()
        {
            var board = Refs.Board;
            var pos = Refs.PlayerPos;
            if (board == null) return "nothing";
            // Vector3i.up is (0,1,0); GetPhysicalPieceBelow finds the highest surface strictly
            // below the given point, so offsetting up by one gives the tile you stand on.
            Piece under = board.GetPhysicalPieceBelow(pos + Vector3i.up);
            return PieceName(under);
        }

        /// <summary>
        /// Describe one neighbouring tile, the way a sighted player would read it: what is there,
        /// and what stepping that way would actually do.
        /// </summary>
        internal static string Neighbour(Vector3i dir)
        {
            var sim = Refs.Sim;
            var board = Refs.Board;
            var move = Refs.PlayerMove;
            if (sim == null || board == null || move == null) return "unknown";

            var pos = move.position;
            var target = pos + dir;

            Piece platform = board.GetPhysicalPieceBelow(target + Vector3i.up);

            // MOVING WINS OVER INTERACTING. GridMovement.SetQueuedMove calls
            // GetPositionAfterMove first and only falls through to an interaction when the
            // position would not change. An earlier version asked PreviewInteraction first and
            // so could announce "push" for a log you would actually just step onto.
            if (move.CanMove(dir))
            {
                if (platform == null) return "water";

                string name = PieceName(platform);

                string under = RestingOn(platform);
                if (under != null) name += " on " + under;

                int rise = platform.top.y - pos.y;
                if (rise > 0) return name + ", step up";
                if (rise < 0) return name + ", step down";
                return name;
            }

            // Cannot walk there, so whatever happens is an interaction.
            if (Cfg.UsePreviewInteraction.Value)
            {
                var preview = sim.PreviewInteraction(pos, dir);

                if (preview.type == Simulator.InteractionType.Chop)
                    return PieceName(preview.piece) + ", chop";

                if (preview.type == Simulator.InteractionType.Balance)
                    return PieceName(preview.piece) + ", balance across";

                if (preview.type == Simulator.InteractionType.Push)
                    return PieceName(preview.piece) + ", " + PushVerb(preview.piece, dir);
            }

            // Interactable pieces are not obstacles. Walking into an exhibit views its plaque,
            // walking into a villager talks to them, walking into a postbox opens the warp map.
            // PreviewInteraction only covers trees, logs and rafts, so without this the mod
            // called every exhibit in the game "blocked".
            Piece facing = board.GetPhysicalPieceAtFlat(target);
            if (facing != null && facing.IsInteractable())
                return PieceName(facing) + ", " + InteractVerb(facing);

            string reason = BlockedReason(dir);
            return reason == null ? "blocked" : reason + ", blocked";
        }

        internal static string InteractVerbPublic(Piece p) => InteractVerb(p);

        /// <summary>
        /// Why a move failed, in the terms that let you fix it.
        ///
        /// "Blocked" on its own tells you nothing you did not already know. Water, too high and
        /// wrong side are three completely different problems: one needs a bridge, one needs a
        /// different route, and one just needs you to walk round to the other end.
        ///
        /// The "wrong side" case comes from Piece.CanWalkOntoFrom, which for a lying log returns
        /// true only along its length. That is the single most common blocked move in the game
        /// and the least obvious without being told.
        /// </summary>
        internal static string BlockedReason(Vector3i dir)
        {
            var board = Refs.Board;
            var move = Refs.PlayerMove;
            if (board == null || move == null) return null;

            var pos = move.position;
            var target = pos + dir;

            Piece platform = board.GetPhysicalPieceBelow(target + Vector3i.up);
            if (platform == null) return "water";

            // Name what is actually in the way, not the ground under it.
            //
            // GetPhysicalPieceBelow finds the surface you would LAND on, which for a boulder is
            // the land beneath it - so a boulder in your path was reported as "blocked by land".
            // The tallest notable piece in that column is the boulder itself.
            Piece blocker = Survey.TopOfColumn(target);
            if (blocker == null || blocker.top.y <= pos.y) blocker = platform;

            int rise = blocker.top.y - pos.y;
            if (rise > 1)
                return PieceName(blocker) + ", " + rise + " steps up, too high to climb";

            if (!platform.CanWalkOntoFrom(dir))
            {
                var log = platform as Log;
                if (log != null && !log.standing)
                {
                    string axis = log.direction.x != 0 ? "east to west" : "north to south";
                    return PieceName(platform) + ", you can only step onto it along its length, " + axis;
                }
                return PieceName(platform) + ", not from this side";
            }

            return PieceName(blocker);
        }

        /// <summary>What walking into an interactable piece actually does.</summary>
        private static string InteractVerb(Piece p)
        {
            switch (p.type)
            {
                case PieceType.Landmark:
                    var lm = p as Landmark;
                    if (lm != null)
                    {
                        switch (lm.interactionBehaviour)
                        {
                            case Landmark.InteractionBehaviour.ViewCaption: return "read the plaque";
                            case Landmark.InteractionBehaviour.TrophyCollectible: return "collect it";
                            case Landmark.InteractionBehaviour.Hug: return "hug";
                            case Landmark.InteractionBehaviour.CoffeeHut: return "coffee hut";
                            case Landmark.InteractionBehaviour.PopcornHut: return "popcorn hut";
                            case Landmark.InteractionBehaviour.Bench: return "sit down";
                            case Landmark.InteractionBehaviour.FerryOutro: return "board the ferry";
                        }
                    }
                    return "read the plaque";

                case PieceType.Villager: return "talk";
                case PieceType.WarpPoint: return "open the map";
                case PieceType.Campfire: return "rest";
                case PieceType.Caption: return "read the plaque";
                case PieceType.Obstacle: return "blocked";
                default: return "interact";
            }
        }

        /// <summary>
        /// Which kind of push this actually is.
        ///
        /// "Push" covers three different moves and knowing which one matters: a standing log is
        /// knocked flat, a log lying across your path rolls sideways, and a log lying along your
        /// path slides end-on. Saying just "push" leaves you guessing where it will end up.
        /// </summary>
        private static string PushVerb(Piece p, Vector3i dir)
        {
            var log = p as Log;
            if (log == null) return "push";

            if (log.standing) return "knock it over";
            if (log.IsPerpendicularTo(dir)) return "roll it";
            if (log.IsParallelTo(dir)) return "slide it end on";
            return "push";
        }

        /// <summary>
        /// The full look-around: four cardinal directions, always spoken in the same order so
        /// it becomes muscle memory.
        /// </summary>
        internal static string LookAround()
        {
            var sb = new StringBuilder();
            foreach (var d in Cardinals)
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(Naming.Compass(d)).Append(": ").Append(Neighbour(d));
            }
            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// Fixed north, south, east, west order. We do not use Vector3i.cardinals because its
        /// ordering is an implementation detail we would rather not depend on for speech.
        /// </summary>
        internal static readonly Vector3i[] Cardinals =
        {
            Vector3i.north, Vector3i.south, Vector3i.east, Vector3i.west
        };

        /// <summary>Where you are, and what you are on.</summary>
        internal static string WhereAmI()
        {
            var move = Refs.PlayerMove;
            if (move == null) return "Not in the game world.";

            var sb = new StringBuilder();

            // Asked directly, so always answer with the numbers even when the grid is off.
            sb.Append(Naming.Coords(move.position)).Append(". ");

            sb.Append("Facing ").Append(Naming.Compass(move.direction)).Append(". ");
            sb.Append("On ").Append(Underfoot()).Append('.');

            if (move.sitting) sb.Append(" Sitting.");
            if (move.ridingRaftGroup) sb.Append(" Riding a raft.");
            return sb.ToString();
        }

        /// <summary>Describe the thing directly in front of you.</summary>
        internal static string Facing()
        {
            var sim = Refs.Sim;
            var move = Refs.PlayerMove;
            if (sim == null || move == null) return "Nothing.";

            Piece p = sim.GetFacingInteractable();
            if (p == null)
            {
                // Nothing interactable, so fall back to plain terrain.
                return Naming.Compass(move.direction) + ": " + Neighbour(move.direction) + ".";
            }

            string name = PieceName(p);
            string plaque = GameText.PlaqueFor(p);
            if (!string.IsNullOrEmpty(plaque)) return name + ". " + plaque;
            return name + ".";
        }
    }
}
