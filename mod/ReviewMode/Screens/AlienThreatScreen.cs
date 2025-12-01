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
    /// Alien Threat screen - browse alien threats organized by category.
    /// Categories: Councilors, Fleets, Habs, Earth Assets, Xenoforming.
    /// Works with standard navigation: categories are items, drilling shows their contents as sections.
    /// </summary>
    public class AlienThreatScreen : ScreenBase
    {
        private readonly AlienThreatReader alienReader = new AlienThreatReader();

        // Categories as top-level items
        private static readonly AlienThreatReader.AlienCategory[] categories = new[]
        {
            AlienThreatReader.AlienCategory.Councilors,
            AlienThreatReader.AlienCategory.Fleets,
            AlienThreatReader.AlienCategory.Habs,
            AlienThreatReader.AlienCategory.EarthAssets,
            AlienThreatReader.AlienCategory.Xenoforming
        };

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Alien Threats";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int totalThreats = GetTotalThreatCount(faction);
                    return $"{totalThreats} known threat{(totalThreats != 1 ? "s" : "")} in {categories.Length} categories";
                }
                return "Browse known alien threats";
            }
        }

        public override IReadOnlyList<object> GetItems()
        {
            // Return categories as objects
            return categories.Cast<object>().ToList();
        }

        public override void Refresh()
        {
            cachedItemIndex = -1;
            cachedSections.Clear();
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= categories.Length)
                return "Invalid category";

            var faction = GameControl.control?.activePlayer;
            var category = categories[index];
            return alienReader.ReadCategorySummary(category, faction);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= categories.Length)
                return "Invalid category";

            var faction = GameControl.control?.activePlayer;
            var category = categories[index];
            int count = alienReader.GetCategoryCount(category, faction);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(AlienThreatReader.GetCategoryName(category));
            sb.AppendLine($"{count} known item{(count != 1 ? "s" : "")}");
            sb.AppendLine();

            if (count > 0)
            {
                sb.AppendLine("Press Enter to browse items in this category.");
            }
            else
            {
                sb.AppendLine("No threats detected in this category.");
            }

            return sb.ToString();
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= categories.Length)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;
            cachedSections = BuildCategorySections(categories[index]);
            return cachedSections;
        }

        private List<ISection> BuildCategorySections(AlienThreatReader.AlienCategory category)
        {
            var sections = new List<ISection>();
            var faction = GameControl.control?.activePlayer;

            if (faction == null)
            {
                var errorSection = new DataSection("Error");
                errorSection.AddItem("No player faction available");
                sections.Add(errorSection);
                return sections;
            }

            try
            {
                var items = alienReader.GetCategoryItems(category, faction);
                string categoryName = AlienThreatReader.GetCategoryName(category);

                if (items.Count == 0)
                {
                    var emptySection = new DataSection(categoryName);
                    emptySection.AddItem("No threats detected");
                    sections.Add(emptySection);
                    return sections;
                }

                // Create a section with all items
                var itemsSection = new DataSection($"{categoryName} ({items.Count})");

                foreach (var item in items)
                {
                    string summary = alienReader.ReadItemSummary(item, faction);
                    string detail = alienReader.ReadItemDetail(item, faction);
                    itemsSection.AddItem(summary, "", detail);
                }

                sections.Add(itemsSection);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building sections for {category}: {ex.Message}");
                var errorSection = new DataSection("Error");
                errorSection.AddItem($"Failed to load {category}: {ex.Message}");
                sections.Add(errorSection);
            }

            return sections;
        }

        public override bool CanDrillIntoItem(int index)
        {
            if (index < 0 || index >= categories.Length)
                return false;

            // Can drill if the category has any items
            var faction = GameControl.control?.activePlayer;
            if (faction != null)
            {
                return alienReader.GetCategoryCount(categories[index], faction) > 0;
            }
            return false;
        }

        public override string GetActivationAnnouncement()
        {
            Refresh();

            int totalThreats = GetTotalThreatCount(GameControl.control?.activePlayer);
            if (totalThreats == 0)
            {
                return "Alien Threats. No known alien threats.";
            }
            return $"Alien Threats. {Description}. Press Enter to browse a category.";
        }

        private int GetTotalThreatCount(TIFactionState faction)
        {
            if (faction == null) return 0;

            int total = 0;
            foreach (var category in categories)
            {
                total += alienReader.GetCategoryCount(category, faction);
            }
            return total;
        }
    }
}
