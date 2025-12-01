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
    /// Global Status screen - view world status information.
    /// Categories: Public Opinion, Environmental, Economy, Wars, Atrocities.
    /// </summary>
    public class GlobalStatusScreen : ScreenBase
    {
        private readonly GlobalStatusReader statusReader = new GlobalStatusReader();

        // Categories as top-level items
        private static readonly GlobalStatusReader.GlobalCategory[] categories = new[]
        {
            GlobalStatusReader.GlobalCategory.PublicOpinion,
            GlobalStatusReader.GlobalCategory.Environmental,
            GlobalStatusReader.GlobalCategory.Economy,
            GlobalStatusReader.GlobalCategory.Wars,
            GlobalStatusReader.GlobalCategory.Atrocities
        };

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Global Status";

        public override string Description => $"{categories.Length} status categories";

        public override IReadOnlyList<object> GetItems()
        {
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

            var category = categories[index];
            return statusReader.ReadCategorySummary(category);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= categories.Length)
                return "Invalid category";

            var category = categories[index];
            return statusReader.ReadCategoryDetail(category);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= categories.Length)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            var category = categories[index];
            cachedItemIndex = index;
            cachedSections = statusReader.GetSectionsForCategory(category);

            return cachedSections;
        }

        public override string GetActivationAnnouncement()
        {
            Refresh();
            return $"Global Status. {Description}. Press Enter to browse a category.";
        }
    }
}
