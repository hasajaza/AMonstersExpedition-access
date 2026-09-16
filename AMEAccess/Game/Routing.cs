using System;
using System.Collections.Generic;
using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Turn-by-turn walking directions, shared by the review cursor and by bookmarks.
    ///
    /// The steps come from the game's own A*, in the order the route takes them. A straight
    /// line offset is not good enough: "1 north, 2 east" and "2 east then 1 north" reach the
    /// same tile but only one of them may be walkable.
    /// </summary>
    internal static class Routing
    {
        /// <summary>Directions from one point to another, or why there are none.</summary>
        internal static string Describe(Vector3i from, Vector3i to)
        {
            if (from == to) return "You are standing there.";

            List<Vector3i> path = null;
            try { path = PathFinding.PathFinding.Route(from, to, null, false); }
            catch { }
            finally { try { PathFinding.PathFinding.Reset(); } catch { } }

            if (path == null || path.Count == 0)
                return "No walkable route yet. " + Survey.Relative(from, to) + " away, "
                     + Survey.Manhattan(from, to) + " tiles in a straight line.";

            return Steps(from, path);
        }

        private static string Steps(Vector3i from, List<Vector3i> path)
        {
            var pts = new List<Vector3i>(path);

            // Route may hand the waypoints back either way round; anchor on the end nearest us.
            if (pts.Count > 1 &&
                Survey.Manhattan(from, pts[0]) > Survey.Manhattan(from, pts[pts.Count - 1]))
                pts.Reverse();

            if (pts.Count == 0 || !(pts[0] == from)) pts.Insert(0, from);

            var sb = new StringBuilder();
            string runDir = null;
            int runLen = 0, climbs = 0, drops = 0, total = 0;

            for (int i = 1; i < pts.Count; i++)
            {
                int dx = pts[i].x - pts[i - 1].x;
                int dz = pts[i].z - pts[i - 1].z;
                int dy = pts[i].y - pts[i - 1].y;

                if (dy > 0) climbs += dy; else if (dy < 0) drops += -dy;
                if (dx == 0 && dz == 0) continue;

                string dir = dz > 0 ? "north" : dz < 0 ? "south" : dx > 0 ? "east" : "west";
                int len = dx != 0 ? Math.Abs(dx) : Math.Abs(dz);
                total += len;

                if (dir == runDir) { runLen += len; continue; }
                if (runDir != null) Append(sb, runDir, runLen);
                runDir = dir; runLen = len;
            }
            if (runDir != null) Append(sb, runDir, runLen);

            if (sb.Length == 0) return "Right where you are.";

            var outSb = new StringBuilder();
            outSb.Append(total).Append(total == 1 ? " step. " : " steps. ").Append(sb);
            if (climbs > 0) outSb.Append(". ").Append(climbs).Append(climbs == 1 ? " climb up" : " climbs up");
            if (drops > 0) outSb.Append(". ").Append(drops).Append(drops == 1 ? " drop down" : " drops down");
            return outSb.Append('.').ToString();
        }

        private static void Append(StringBuilder sb, string dir, int len)
        {
            if (sb.Length > 0) sb.Append(", then ");
            sb.Append(len).Append(' ').Append(dir);
        }
    }
}
