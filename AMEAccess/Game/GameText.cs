using System.Text;
using AMEAccess.Util;

namespace AMEAccess.Game
{
    /// <summary>
    /// Reads the game's own localised text.
    ///
    /// The shipped Localization.Strings asset holds 603 string ids across en-GB, en, en-AU, fr,
    /// fr-CA, it, de, es, zh-Hans, zh-Hant and more; 334 of those ids are LM_* landmark strings.
    /// Localization.ExhibitDescriptions holds 167 exhibit records, each mapping an id such as
    /// LM_Lighthouse to LM_Lighthouse_Item and LM_Lighthouse_Description.
    ///
    /// Strings.Get(id, language) resolves "auto" (or null) through Config.languageData, and
    /// falls back to returning the raw id when a string is missing, so a missing lookup is
    /// visible rather than silent.
    ///
    /// Reading plaques aloud is parity, not assistance: a sighted player simply reads the
    /// plaque on screen. It is a large slice of this game's content.
    /// </summary>
    internal static class GameText
    {
        private static string Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var strings = Globals.strings;
            if (strings == null) return null;

            string v = strings.Get(id, "auto");
            // Strings.Get returns the id itself when it cannot find a value.
            if (string.IsNullOrEmpty(v) || v == id) return null;
            return Rich.Strip(v);
        }

        /// <summary>Exhibit title, e.g. "LIGHTHOUSE".</summary>
        internal static string ExhibitTitle(Landmark landmark)
        {
            var desc = DescriptionFor(landmark);
            return desc == null ? null : Get(desc.itemId);
        }

        /// <summary>Exhibit title from a raw exhibit id, e.g. "LM_Lighthouse".</summary>
        internal static string ExhibitTitleById(string exhibitId)
        {
            if (string.IsNullOrEmpty(exhibitId)) return null;

            // Ids in the strings table are the exhibit id with _Item appended.
            string t = Get(exhibitId + "_Item");
            if (!string.IsNullOrEmpty(t)) return t;
            return Get(exhibitId);
        }

        /// <summary>Exhibit body text, i.e. the plaque prose.</summary>
        internal static string ExhibitDescription(Landmark landmark)
        {
            var desc = DescriptionFor(landmark);
            return desc == null ? null : Get(desc.descriptionId);
        }

        private static Localization.ExhibitDescription DescriptionFor(Landmark landmark)
        {
            if (landmark == null) return null;
            var group = landmark.group;
            if (group == null) return null;

            var viewer = Refs.Captions;
            if (viewer == null) return null;

            var table = viewer.exhibitDescriptions;
            if (table == null) return null;

            return table.GetExhibitDescriptionForLandmarkGroup(group);
        }

        /// <summary>Title and body for anything with a plaque, or null.</summary>
        internal static string PlaqueFor(Piece p)
        {
            var landmark = p as Landmark;
            if (landmark == null) return null;

            string title = ExhibitTitle(landmark);
            string body = ExhibitDescription(landmark);
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return null;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(title)) sb.Append(title).Append(". ");
            if (!string.IsNullOrEmpty(body)) sb.Append(body);
            return sb.ToString().Trim();
        }

        /// <summary>Villager display name via the Conversations table.</summary>
        internal static string VillagerDisplayName(Villager v)
        {
            if (v == null) return null;
            var sim = Refs.Sim;
            if (sim == null) return null;

            var conv = sim.conversations;
            if (conv == null) return null;

            string name = conv.GetDisplayName(v.place, v.character);
            if (string.IsNullOrEmpty(name)) return null;
            return Rich.Strip(name);
        }
    }
}
