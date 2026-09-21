using System;
using System.Collections.Generic;
using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// A virtual cursor you can walk around the map without moving your monster.
    ///
    /// This is the screen-reader equivalent of something sighted players already do. The game
    /// grants Capabilities.AllowsPanning during normal play and GameplayCamera has a full
    /// panning implementation (isPanning, panningOffset, panSpeed), so looking around the
    /// island while standing still is an ordinary player ability, not an advantage.
    ///
    /// The cursor never touches the character. It only reads.
    /// </summary>
    internal static class ReviewCursor
    {
        private static Vector3i _pos;
        private static bool _placed;

        internal static bool Active => _placed;

        internal static Vector3i Position
        {
            get { if (!_placed) Home(); return _pos; }
        }

        /// <summary>Move the cursor to the player without saying anything.</summary>
        internal static void FollowPlayer()
        {
            _pos = Refs.PlayerPos;
            _placed = true;
        }

        /// <summary>Put the cursor back on the player.</summary>
        internal static string Home()
        {
            _pos = Refs.PlayerPos;
            _placed = true;
            return "Cursor on you. " + Read();
        }

        internal static void FollowPlayerIfIdle()
        {
            // When the cursor has never been used, keep it under the player so the first
            // press starts somewhere sensible.
            if (!_placed) { _pos = Refs.PlayerPos; _placed = true; }
        }

        /// <summary>Move the cursor and read what is there.</summary>
        internal static string Move(Vector3i dir, int steps)
        {
            FollowPlayerIfIdle();
            _pos = _pos + dir * steps;
            return Read();
        }

        /// <summary>Jump the cursor to a piece.</summary>
        internal static string JumpTo(Piece p)
        {
            if (p == null) return "Nothing to jump to.";
            _pos = p.position;
            _placed = true;
            return Read();
        }

        /// <summary>What is under the cursor, and where it is relative to you.</summary>
        internal static string Read()
        {
            FollowPlayerIfIdle();

            var top = Survey.TopOfColumn(_pos);
            string what = top == null ? "water" : Describe.PieceName(top);

            var sb = new StringBuilder();
            sb.Append(what).Append(". ");
            sb.Append(Survey.Relative(Refs.PlayerPos, _pos));

            if (Cfg.CoordsOn)
                sb.Append(". ").Append(Naming.Coords(_pos));

            return sb.Append('.').ToString();
        }

        /// <summary>Everything stacked in the cursor's column, not just the most notable thing.</summary>
        internal static string ReadColumn()
        {
            FollowPlayerIfIdle();

            var list = new List<Piece>();
            Survey.Column(_pos, list);
            if (list.Count == 0) return "Water. " + Survey.Relative(Refs.PlayerPos, _pos) + ".";

            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Describe.PieceName(list[i]));
                sb.Append(" at height ").Append(list[i].top.y);
                string on = Describe.StackedOn(list[i]);
                if (on != null) sb.Append(" carrying ").Append(on);
            }
            sb.Append(". ").Append(Survey.Relative(Refs.PlayerPos, _pos)).Append('.');
            return sb.ToString();
        }

        /// <summary>Full detail on whatever the cursor is over.</summary>
        internal static string Inspect()
        {
            FollowPlayerIfIdle();
            var top = Survey.TopOfColumn(_pos);
            if (top == null)
                return "Open water. " + Survey.Relative(Refs.PlayerPos, _pos) + ".";
            return Details.Detail(top);
        }

        /// <summary>
        /// Can you walk from where you stand to the cursor, and how far is it?
        ///
        /// This mirrors the path preview the game already draws for mouse and touch players
        /// (PathFinding.PathPreview, shown when Config.showSuggestedPath is on). It routes over
        /// terrain that is already walkable; it does not work out how to move logs, so it tells
        /// you about the world as it is, not how to solve it.
        /// </summary>
        internal static string RouteHere()
        {
            FollowPlayerIfIdle();
            return Routing.Describe(Refs.PlayerPos, _pos);
        }

    }
}
