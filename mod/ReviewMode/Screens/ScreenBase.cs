using System;
using System.Collections.Generic;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// View mode for screens that support browsing all items vs only player-owned items.
    /// </summary>
    public enum ViewMode
    {
        /// <summary>Only show items owned/controlled by the player</summary>
        Mine,
        /// <summary>Show all items in the game</summary>
        All
    }

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
        /// Whether this screen supports switching between Mine/All view modes.
        /// Override and return true if the screen can show all game objects.
        /// </summary>
        public virtual bool SupportsViewModeToggle => false;

        /// <summary>
        /// Current view mode. Only relevant if SupportsViewModeToggle is true.
        /// </summary>
        public virtual ViewMode CurrentViewMode { get; set; } = ViewMode.Mine;

        /// <summary>
        /// Toggle view mode between Mine and All.
        /// Returns announcement text describing the new mode.
        /// </summary>
        public virtual string ToggleViewMode()
        {
            if (!SupportsViewModeToggle)
                return "This screen does not support view mode toggle";

            CurrentViewMode = CurrentViewMode == ViewMode.Mine ? ViewMode.All : ViewMode.Mine;
            Refresh();
            string modeName = CurrentViewMode == ViewMode.Mine ? "Your items" : "All items";
            return $"{modeName}. {ItemCount} items.";
        }

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
            string viewModeInfo = SupportsViewModeToggle
                ? (CurrentViewMode == ViewMode.Mine ? " (your items, Tab to show all)" : " (all items, Tab to show yours)")
                : "";
            if (!string.IsNullOrEmpty(Description))
                return $"{Name} screen. {Description}.{viewModeInfo} {count} items.";
            return $"{Name} screen.{viewModeInfo} {count} items.";
        }

        /// <summary>
        /// Whether this screen supports letter navigation (pressing A-Z to jump to items).
        /// Override and return true for screens with alphabetically-sorted items.
        /// </summary>
        public virtual bool SupportsLetterNavigation => false;

        /// <summary>
        /// Whether this screen supports faction filtering.
        /// Override and return true for screens that can filter by faction (e.g., Fleets in All mode).
        /// </summary>
        public virtual bool SupportsFactionFilter => false;

        /// <summary>
        /// Cycle to the next faction filter.
        /// Returns announcement text describing the new filter, or null if not supported.
        /// </summary>
        public virtual string NextFactionFilter() => null;

        /// <summary>
        /// Cycle to the previous faction filter.
        /// Returns announcement text describing the new filter, or null if not supported.
        /// </summary>
        public virtual string PreviousFactionFilter() => null;

        /// <summary>
        /// Get the display name of an item for letter navigation purposes.
        /// Override to return the sortable name of the item at the given index.
        /// </summary>
        public virtual string GetItemSortName(int index)
        {
            return ReadItemSummary(index);
        }

        /// <summary>
        /// Find the index of the first item starting with the given letter.
        /// Returns -1 if no item found.
        /// </summary>
        public virtual int FindItemByLetter(char letter)
        {
            if (!SupportsLetterNavigation)
                return -1;

            letter = char.ToUpperInvariant(letter);
            var items = GetItems();
            if (items == null || items.Count == 0)
                return -1;

            for (int i = 0; i < items.Count; i++)
            {
                string name = GetItemSortName(i);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Find the next item starting with the given letter after the current index.
        /// If no more items with that letter exist, wraps to the first one.
        /// Returns -1 if no item found.
        /// </summary>
        public virtual int FindNextItemByLetter(char letter, int currentIndex)
        {
            if (!SupportsLetterNavigation)
                return -1;

            letter = char.ToUpperInvariant(letter);
            var items = GetItems();
            if (items == null || items.Count == 0)
                return -1;

            // Search from current index + 1 to end
            for (int i = currentIndex + 1; i < items.Count; i++)
            {
                string name = GetItemSortName(i);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            // Wrap around: search from 0 to current index
            for (int i = 0; i <= currentIndex; i++)
            {
                string name = GetItemSortName(i);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            return -1;
        }
    }
}
