using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Faction Intel screen - browse enemy factions with intel-gated details.
    /// Shows all 7 non-player human factions with their intel levels.
    /// </summary>
    public class FactionIntelScreen : ScreenBase
    {
        private List<TIFactionState> items = new List<TIFactionState>();
        private readonly FactionIntelReader factionReader = new FactionIntelReader();

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Faction Intel";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int count = items.Count;
                    return $"{count} enemy faction{(count != 1 ? "s" : "")}";
                }
                return "Browse enemy faction intelligence";
            }
        }

        public override bool SupportsLetterNavigation => true;

        public override IReadOnlyList<object> GetItems()
        {
            return items.Cast<object>().ToList();
        }

        public override void Refresh()
        {
            items.Clear();
            cachedItemIndex = -1;
            cachedSections.Clear();

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return;

                items.AddRange(FactionIntelReader.GetAllEnemyFactions(faction));

                // Sort by intel level (highest first), then by name
                items = items.OrderByDescending(f => faction.GetIntel(f))
                             .ThenBy(f => f.displayName)
                             .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing faction intel: {ex.Message}");
            }
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid faction";

            var faction = items[index];
            var viewer = GameControl.control?.activePlayer;
            return factionReader.ReadSummary(faction, viewer);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid faction";

            var faction = items[index];
            var viewer = GameControl.control?.activePlayer;
            return factionReader.ReadDetail(faction, viewer);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            var faction = items[index];
            var viewer = GameControl.control?.activePlayer;
            cachedItemIndex = index;
            cachedSections = factionReader.GetSections(faction, viewer);

            return cachedSections;
        }

        public override string GetItemSortName(int index)
        {
            if (index < 0 || index >= items.Count)
                return "";
            return items[index].displayName ?? "";
        }

        public override string GetActivationAnnouncement()
        {
            Refresh();
            if (items.Count == 0)
            {
                return "Faction Intel. No enemy factions found.";
            }
            return $"Faction Intel. {Description}. Press Enter to browse.";
        }
    }
}
