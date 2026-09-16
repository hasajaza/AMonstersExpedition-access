using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Full detail on a single piece — the equivalent of leaning in and looking closely.
    ///
    /// Everything here is observable state: what the thing is, where it is, how big it is,
    /// which way it lies. None of it is a hint about what to do with it.
    /// </summary>
    internal static class Details
    {
        internal static string Detail(Piece p)
        {
            if (p == null) return "Nothing there.";

            var origin = Refs.PlayerPos;
            var sb = new StringBuilder();

            sb.Append(Describe.PieceName(p)).Append('.');
            sb.Append(' ').Append(Survey.Relative(origin, p.position)).Append('.');

            if (Cfg.CoordsOn)
                sb.Append(" At ").Append(Naming.Coords(p.position)).Append('.');

            // Height relative to where you stand matters for climbing.
            int rise = p.top.y - origin.y;
            if (rise > 0) sb.Append(' ').Append(rise).Append(rise == 1 ? " step up." : " steps up.");
            else if (rise < 0) sb.Append(' ').Append(-rise).Append(rise == -1 ? " step down." : " steps down.");
            else sb.Append(" Level with you.");

            var log = p as Log;
            if (log != null)
            {
                sb.Append(" Length ").Append(log.length).Append('.');
                if (log.standing)
                {
                    sb.Append(" Standing upright.");
                }
                else
                {
                    var d = log.direction;
                    sb.Append(d.x != 0 ? " Lying east to west." : " Lying north to south.");
                    if (log.CanBeStoodUp()) sb.Append(" Can be stood up.");
                }
            }

            var tree = p as TreeLog;
            if (tree != null && tree.logPrefab != null)
            {
                sb.Append(" Chopping it gives a log ").Append(tree.logPrefab.length).Append(" long.");
                if (tree.treeStumpPrefab != null)
                    sb.Append(" Leaves a stump ").Append(tree.treeStumpPrefab.height).Append(" high.");
            }

            var raft = p as Raft;
            if (raft != null)
            {
                sb.Append(" Length ").Append(raft.length).Append('.');
                if (raft.floats) sb.Append(" Floating.");
            }

            // Stacking is where the later puzzles live: what is under a thing and what it is
            // carrying matter as much as the thing itself.
            string restingOn = Describe.RestingOn(p);
            if (restingOn != null) sb.Append(" Resting on ").Append(restingOn).Append('.');

            string carrying = Describe.StackedOn(p);
            if (carrying != null) sb.Append(" Carrying ").Append(carrying).Append('.');

            // IsStandable only says the top is a surface. Obstacle.IsStandable is a height
            // test - a tall boulder passes it - so saying "you can stand on it" about something
            // two steps up was an invitation to walk into a wall. Say it only when you could
            // actually get up there from where you are.
            if (p.IsStandable())
            {
                int climb = p.top.y - origin.y;
                if (climb <= 1) sb.Append(" You can stand on it.");
                else sb.Append(" Its top is walkable but it is ").Append(climb)
                       .Append(" steps up, too high to climb from here.");
            }

            var landmark = p as Landmark;
            if (landmark != null)
            {
                string title = GameText.ExhibitTitle(landmark);
                if (!string.IsNullOrEmpty(title)) sb.Append(" Exhibit: ").Append(title).Append('.');
                sb.Append(" Walk into it to ").Append(Describe.InteractVerbPublic(p)).Append('.');
            }
            else if (p.IsInteractable())
            {
                string verb = Describe.InteractVerbPublic(p);
                // Obstacles report as "blocked", which is a state and not something you do.
                if (verb == "blocked") sb.Append(" It is in the way.");
                else sb.Append(" Walk into it to ").Append(verb).Append('.');
            }

            // If it happens to be adjacent, say what walking into it would do. This is the same
            // information a sighted player gets from simply being next to it.
            var move = Refs.PlayerMove;
            var sim = Refs.Sim;
            if (move != null && sim != null && Cfg.UsePreviewInteraction.Value)
            {
                foreach (var dir in Describe.Cardinals)
                {
                    if (!(origin + dir == p.position)) continue;
                    var preview = sim.PreviewInteraction(origin, dir);
                    if (preview.type == Simulator.InteractionType.Chop) sb.Append(" Walk ").Append(Naming.Compass(dir)).Append(" to chop it.");
                    else if (preview.type == Simulator.InteractionType.Push) sb.Append(" Walk ").Append(Naming.Compass(dir)).Append(" to push it.");
                    else if (preview.type == Simulator.InteractionType.Balance) sb.Append(" Walk ").Append(Naming.Compass(dir)).Append(" to balance across.");
                    break;
                }
            }

            return sb.ToString();
        }

        private static string PushHint(Piece p, Vector3i dir)
        {
            var log = p as Log;
            if (log == null) return "push it";
            if (log.standing) return "knock it over";
            if (log.IsPerpendicularTo(dir)) return "roll it";
            if (log.IsParallelTo(dir)) return "slide it end on";
            return "push it";
        }
    }
}
