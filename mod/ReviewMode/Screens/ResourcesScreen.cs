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
    /// Resources screen - flat list of all HUD resources.
    /// Navigate with 8/2, press * for detailed breakdown (game tooltip info).
    /// </summary>
    public class ResourcesScreen : ScreenBase
    {
        private readonly ResourceReader resourceReader = new ResourceReader();

        // All resources in HUD order
        private static readonly ResourceReader.ResourceItem[] resources = new[]
        {
            ResourceReader.ResourceItem.Money,
            ResourceReader.ResourceItem.Influence,
            ResourceReader.ResourceItem.Operations,
            ResourceReader.ResourceItem.Boost,
            ResourceReader.ResourceItem.Research,
            ResourceReader.ResourceItem.MissionControl,
            ResourceReader.ResourceItem.ControlPointCap,
            ResourceReader.ResourceItem.Water,
            ResourceReader.ResourceItem.Volatiles,
            ResourceReader.ResourceItem.Metals,
            ResourceReader.ResourceItem.NobleMetals,
            ResourceReader.ResourceItem.Fissiles,
            ResourceReader.ResourceItem.Antimatter,
            ResourceReader.ResourceItem.Exotics
        };

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Resources";

        public override string Description => $"{resources.Length} resources";

        public override IReadOnlyList<object> GetItems()
        {
            return resources.Cast<object>().ToList();
        }

        public override void Refresh()
        {
            // No caching needed for this simple screen
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= resources.Length)
                return "Invalid resource";

            return resourceReader.ReadSummary(resources[index]);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= resources.Length)
                return "Invalid resource";

            return resourceReader.ReadDetail(resources[index]);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            // Resources don't have drill-down sections - use * for details instead
            return new List<ISection>();
        }

        public override string GetActivationAnnouncement()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return "Resources. No active faction.";

            return $"Resources for {faction.displayName}. {resources.Length} items. Press star for details.";
        }
    }
}
