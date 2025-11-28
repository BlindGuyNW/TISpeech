using System;
using System.Collections.Generic;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Technology screen - browse research slots and tech tree.
    /// Items include research slots (0-5) and a Tech Browser entry.
    /// Tech Browser provides category-based sections for browsing all technologies.
    /// </summary>
    public class TechnologyScreen : ScreenBase
    {
        // Item types in the list
        private enum ItemType { ResearchSlot, TechBrowser }

        private class TechScreenItem
        {
            public ItemType Type;
            public int SlotIndex; // For ResearchSlot type
        }

        private List<TechScreenItem> items = new List<TechScreenItem>();

        private readonly ResearchSlotReader slotReader = new ResearchSlotReader();
        private readonly TechBrowserReader techBrowserReader = new TechBrowserReader();

        /// <summary>
        /// Callback for entering selection mode (for tech/project selection).
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback when sections are invalidated (after actions).
        /// The controller should re-fetch sections from NavigationState.
        /// </summary>
        public Action OnSectionsInvalidated { get; set; }

        public override string Name => "Technology";

        public override string Description
        {
            get
            {
                try
                {
                    var faction = GameControl.control?.activePlayer;
                    var globalResearch = TIGlobalResearchState.globalResearch;

                    if (faction != null && globalResearch != null)
                    {
                        // Count active slots with priority > 0
                        int activeSlots = 0;
                        for (int i = 0; i < 6; i++)
                        {
                            if (faction.researchWeights[i] > 0)
                                activeSlots++;
                        }

                        float dailyResearch = faction.GetDailyIncome(FactionResource.Research);
                        int completedTechs = TIGlobalResearchState.FinishedTechs()?.Count ?? 0;

                        return $"{activeSlots} active slots, {dailyResearch:F1} daily research, {completedTechs} techs completed";
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error getting tech screen description: {ex.Message}");
                }
                return "Manage research priorities and browse technologies";
            }
        }

        public TechnologyScreen()
        {
            // Wire up reader callbacks
            slotReader.OnEnterSelectionMode = (prompt, options, callback) => OnEnterSelectionMode?.Invoke(prompt, options, callback);
            slotReader.OnSpeak = (text, interrupt) => OnSpeak?.Invoke(text, interrupt);
            slotReader.OnRefresh = () =>
            {
                Refresh();
                // Tell controller to re-fetch navigation sections
                OnSectionsInvalidated?.Invoke();
            };

            // TechBrowserReader doesn't need callbacks - it's read-only browsing
        }

        public override void Refresh()
        {
            items.Clear();

            try
            {
                var faction = GameControl.control?.activePlayer;
                var globalResearch = TIGlobalResearchState.globalResearch;

                if (faction == null || globalResearch == null)
                    return;

                // Add research slots (0-5)
                // Slots 0-2: Global techs
                for (int slot = 0; slot < 3; slot++)
                {
                    items.Add(new TechScreenItem { Type = ItemType.ResearchSlot, SlotIndex = slot });
                }

                // Slots 3-5: Faction projects
                for (int slot = 3; slot < 6; slot++)
                {
                    items.Add(new TechScreenItem { Type = ItemType.ResearchSlot, SlotIndex = slot });
                }

                // Add tech browser item (provides category-based sections)
                items.Add(new TechScreenItem { Type = ItemType.TechBrowser });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing technology screen: {ex.Message}");
            }
        }

        public override IReadOnlyList<object> GetItems()
        {
            return items.ConvertAll(i => (object)i);
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.ResearchSlot:
                    return slotReader.ReadSummary(item.SlotIndex);

                case ItemType.TechBrowser:
                    return techBrowserReader.ReadSummary();

                default:
                    return "Unknown";
            }
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.ResearchSlot:
                    return slotReader.ReadDetail(item.SlotIndex);

                case ItemType.TechBrowser:
                    return techBrowserReader.ReadDetail();

                default:
                    return "Unknown";
            }
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Always return fresh sections - game state may have changed
            var item = items[index];
            switch (item.Type)
            {
                case ItemType.ResearchSlot:
                    return slotReader.GetSections(item.SlotIndex);

                case ItemType.TechBrowser:
                    return techBrowserReader.GetSections();

                default:
                    return new List<ISection>();
            }
        }

        public override bool CanDrillIntoItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;

            // All items can be drilled into (slots have sections, tech browser has category sections)
            return base.CanDrillIntoItem(index);
        }

        /// <summary>
        /// Get sections for a drilled-into section item (e.g., a tech in the tech browser).
        /// </summary>
        public override IReadOnlyList<ISection> GetSectionsForSectionItem(string secondaryId)
        {
            if (string.IsNullOrEmpty(secondaryId))
                return new List<ISection>();

            // The secondaryId is a tech dataName - get its detailed sections
            return techBrowserReader.GetSectionsForTech(secondaryId);
        }
    }
}
