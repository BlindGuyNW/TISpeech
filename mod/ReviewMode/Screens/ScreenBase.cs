using System;
using System.Collections.Generic;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Base class for review mode screens.
    /// Each screen represents a high-level category (Council, Nations, Research, etc.)
    /// and provides navigation through its items and sections.
    /// </summary>
    public abstract class ScreenBase
    {
        /// <summary>
        /// Display name of the screen (e.g., "Council", "Nations")
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Short description shown when screen is announced
        /// </summary>
        public virtual string Description => "";

        /// <summary>
        /// Get the list of top-level items on this screen.
        /// For Council screen, this would be councilors.
        /// For Nations screen, this would be controlled nations.
        /// </summary>
        public abstract IReadOnlyList<object> GetItems();

        /// <summary>
        /// Read a summary of an item (used when navigating the item list).
        /// </summary>
        public abstract string ReadItemSummary(int index);

        /// <summary>
        /// Read detailed information about an item.
        /// </summary>
        public abstract string ReadItemDetail(int index);

        /// <summary>
        /// Get the sections available for a specific item.
        /// Returns empty list if item has no sub-sections.
        /// </summary>
        public abstract IReadOnlyList<ISection> GetSectionsForItem(int index);

        /// <summary>
        /// Check if the item at the given index can be drilled into (has sections).
        /// </summary>
        public virtual bool CanDrillIntoItem(int index)
        {
            var sections = GetSectionsForItem(index);
            return sections != null && sections.Count > 0;
        }

        /// <summary>
        /// Get sections for a drilled-into section item (e.g., a tech in the tech browser).
        /// Override this to support drilling into section items that have sub-content.
        /// </summary>
        /// <param name="secondaryId">The secondary identifier of the section item (e.g., tech dataName)</param>
        /// <returns>Sections for the drilled-into item, or empty list if not supported</returns>
        public virtual IReadOnlyList<ISection> GetSectionsForSectionItem(string secondaryId)
        {
            return new List<ISection>();
        }

        /// <summary>
        /// Called when the screen becomes active.
        /// Override to refresh data.
        /// </summary>
        public virtual void OnActivate()
        {
            Refresh();
        }

        /// <summary>
        /// Called when the screen becomes inactive.
        /// </summary>
        public virtual void OnDeactivate() { }

        /// <summary>
        /// Refresh the screen's data from game state.
        /// </summary>
        public virtual void Refresh() { }

        /// <summary>
        /// Get the number of items on this screen.
        /// </summary>
        public int ItemCount => GetItems()?.Count ?? 0;

        /// <summary>
        /// Get announcement text when this screen becomes active.
        /// </summary>
        public virtual string GetActivationAnnouncement()
        {
            int count = ItemCount;
            if (!string.IsNullOrEmpty(Description))
                return $"{Name} screen. {Description}. {count} items.";
            return $"{Name} screen. {count} items.";
        }
    }
}
