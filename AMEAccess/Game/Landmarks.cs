using System.Text;

namespace AMEAccess.Game
{
    /// <summary>
    /// What a Landmark piece actually is.
    ///
    /// The game uses the Landmark piece type for far more than exhibits. The same type carries
    /// benches, coffee and popcorn huts, trophies, friends, the ferry - and purely decorative
    /// props, some of which are solid (a post standing in the water that stops a raft) and some
    /// of which have no substance at all. Calling every one of them an "exhibit" put a post in
    /// the water and an empty patch of sea into the exhibit count.
    ///
    /// An exhibit is precisely a landmark with plaque text. Everything else is classified by
    /// what the game says it does, and a prop that does nothing and blocks nothing is left out
    /// of the survey entirely, the same as scenery.
    /// </summary>
    internal enum LandmarkKind
    {
        Exhibit,        // has plaque text
        Friend,         // one of the monsters you can hug
        Bench,
        CoffeeHut,
        PopcornHut,
        Trophy,
        Ferry,
        Fixed,          // does nothing, but is solid: blocks you and stops rafts
        Decoration      // does nothing and blocks nothing
    }

    internal static class Landmarks
    {
        internal static LandmarkKind KindOf(Landmark lm)
        {
            if (lm == null) return LandmarkKind.Decoration;

            // Plaque text is the definition of an exhibit.
            if (!string.IsNullOrEmpty(GameText.ExhibitTitle(lm))) return LandmarkKind.Exhibit;

            if (lm.group != null && lm.group.isFriend) return LandmarkKind.Friend;

            switch (lm.interactionBehaviour)
            {
                case Landmark.InteractionBehaviour.Bench: return LandmarkKind.Bench;
                case Landmark.InteractionBehaviour.CoffeeHut: return LandmarkKind.CoffeeHut;
                case Landmark.InteractionBehaviour.PopcornHut: return LandmarkKind.PopcornHut;
                case Landmark.InteractionBehaviour.TrophyCollectible: return LandmarkKind.Trophy;
                case Landmark.InteractionBehaviour.FerryOutro: return LandmarkKind.Ferry;
                case Landmark.InteractionBehaviour.Hug: return LandmarkKind.Friend;
            }

            // No behaviour and no text: a prop. Whether it is solid is what matters to you.
            // IsPhysical() returns the private "physical" flag directly.
            return lm.IsPhysical() ? LandmarkKind.Fixed : LandmarkKind.Decoration;
        }

        /// <summary>Category for the survey's Page Up / Page Down.</summary>
        internal static string Category(LandmarkKind k)
        {
            switch (k)
            {
                case LandmarkKind.Exhibit: return "exhibit";
                case LandmarkKind.Friend: return "friend";
                case LandmarkKind.Bench: return "bench";
                case LandmarkKind.CoffeeHut: return "coffee hut";
                case LandmarkKind.PopcornHut: return "popcorn hut";
                case LandmarkKind.Trophy: return "trophy";
                case LandmarkKind.Ferry: return "ferry";
                case LandmarkKind.Fixed: return "fixed object";
                default: return "decoration";
            }
        }

        /// <summary>
        /// How interesting it is to the survey. Decorations fall below the cut, so they are
        /// never listed - the same treatment as plain ground.
        /// </summary>
        internal static int Interest(LandmarkKind k)
        {
            switch (k)
            {
                case LandmarkKind.Exhibit: return 88;
                case LandmarkKind.Friend: return 87;
                case LandmarkKind.Bench:
                case LandmarkKind.CoffeeHut:
                case LandmarkKind.PopcornHut:
                case LandmarkKind.Trophy:
                case LandmarkKind.Ferry: return 85;
                case LandmarkKind.Fixed: return 60;
                default: return 5;
            }
        }

        /// <summary>
        /// A spoken name. Exhibits use their plaque title; everything else uses the name the
        /// game gave its prefab, tidied up, falling back to the kind.
        /// </summary>
        internal static string Name(Landmark lm)
        {
            var k = KindOf(lm);
            if (k == LandmarkKind.Exhibit)
            {
                string t = GameText.ExhibitTitle(lm);
                if (!string.IsNullOrEmpty(t)) return Tidy(t);
            }

            string prefab = PrefabName(lm);
            if (k == LandmarkKind.Fixed || k == LandmarkKind.Decoration)
                return prefab ?? Category(k);

            return Category(k);
        }

        /// <summary>"LM_MooringPost (Clone)" -> "mooring post". Null when there is none.</summary>
        internal static string PrefabName(Landmark lm)
        {
            if (lm == null || lm.group == null || lm.group.prefab == null) return null;

            string n = lm.group.prefab.name;
            if (string.IsNullOrEmpty(n)) return null;

            n = n.Replace("(Clone)", "").Trim();
            if (n.StartsWith("LM_")) n = n.Substring(3);
            n = n.Replace('_', ' ');

            var sb = new StringBuilder();
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (char.IsDigit(c)) continue;
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(n[i - 1]) && n[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            string outp = sb.ToString().Trim();
            while (outp.Contains("  ")) outp = outp.Replace("  ", " ");

            // Designers file props under prefixes like "decor" - part of how the asset is
            // organised, not part of what the thing is. "decor intro archway" is an archway.
            foreach (var noise in new[] { "decor ", "deco ", "prop ", "props " })
                if (outp.StartsWith(noise)) { outp = outp.Substring(noise.Length); break; }

            return outp.Length == 0 ? null : outp;
        }

        private static string Tidy(string s)
        {
            string t = s.Trim();
            bool caps = true;
            foreach (char c in t) if (char.IsLower(c)) { caps = false; break; }
            if (!caps) return t;
            return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
        }
    }
}
