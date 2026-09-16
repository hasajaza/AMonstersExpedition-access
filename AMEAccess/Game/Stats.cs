using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// Overall progress, the same numbers the game shows on its save-slot screen.
    ///
    /// SaveSlotDetails displays these from a ProfileData built out of the live Profile:
    /// islandsVisitedUnique.Count, exhibitsViewed.Count, huggedFriends.Count and playtime.
    /// We read the same fields off Progress.current.profile, so what you hear matches what a
    /// sighted player sees on the load menu.
    ///
    /// Note these are counts, not totals: the game never tells you how many islands exist, and
    /// neither do we. Finding out what is left to find IS the game.
    /// </summary>
    internal static class Stats
    {
        internal static string Summary()
        {
            var progress = Refs.Progress;
            if (progress == null) return "No save loaded.";

            var profile = progress.profile;
            if (profile == null) return "No save loaded.";

            var sb = new StringBuilder();

            int islands = profile.islandsVisitedUnique != null ? profile.islandsVisitedUnique.Count : 0;
            int exhibits = profile.exhibitsViewed != null ? profile.exhibitsViewed.Count : 0;
            int friends = profile.huggedFriends != null ? profile.huggedFriends.Count : 0;

            sb.Append(islands).Append(islands == 1 ? " island visited. " : " islands visited. ");
            sb.Append(exhibits).Append(exhibits == 1 ? " exhibit discovered. " : " exhibits discovered. ");
            if (friends > 0)
                sb.Append(friends).Append(friends == 1 ? " friend met. " : " friends met. ");

            sb.Append(Playtime(profile.playtime));

            return sb.ToString();
        }

        private static string Playtime(float seconds)
        {
            if (seconds <= 0f) return "No time played yet.";
            int total = (int)seconds;
            int h = total / 3600;
            int m = (total % 3600) / 60;

            if (h > 0)
                return "Played " + h + (h == 1 ? " hour " : " hours ") + m + (m == 1 ? " minute." : " minutes.");
            if (m > 0)
                return "Played " + m + (m == 1 ? " minute." : " minutes.");
            return "Played less than a minute.";
        }
    }
}
