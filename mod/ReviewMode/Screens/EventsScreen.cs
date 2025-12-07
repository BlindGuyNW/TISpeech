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
    /// Events screen - browse game notifications and event logs.
    /// Supports two view modes:
    /// - Recent: Chronological newsfeed (individual events)
    /// - Summary: Categorized by type (Missions, Earth Events, etc.)
    /// </summary>
    public class EventsScreen : ScreenBase
    {
        /// <summary>
        /// View modes for the Events screen.
        /// </summary>
        public enum EventViewMode
        {
            Recent,     // Chronological newsfeed
            Summary     // Categorized by SummaryCategory
        }

        // Item types in the list
        private enum ItemType { Event, CategoryHeader }
        private class EventItem
        {
            public ItemType Type;
            public NotificationSummaryItem Event;      // For Event type
            public SummaryCategory Category;           // For CategoryHeader type
        }

        private List<EventItem> items = new List<EventItem>();
        private EventViewMode currentMode = EventViewMode.Recent;
        private readonly EventReader eventReader = new EventReader();

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for navigating to a game state within Review Mode.
        /// The action receives the target game state.
        /// </summary>
        public Action<TIGameState> OnNavigateToGameState { get; set; }

        public override string Name => currentMode == EventViewMode.Recent ? "Recent Events" : "Event Summary";

        public override string Description
        {
            get
            {
                if (currentMode == EventViewMode.Recent)
                {
                    return "Recent notifications. Press Tab for categories.";
                }
                else
                {
                    return "Events by category. Press Tab for recent.";
                }
            }
        }

        public override bool SupportsViewModeToggle => true;

        public override string ToggleViewMode()
        {
            currentMode = currentMode == EventViewMode.Recent
                ? EventViewMode.Summary
                : EventViewMode.Recent;
            Refresh();

            if (currentMode == EventViewMode.Recent)
            {
                return $"Recent Events: {ItemCount} items. Tab for categories.";
            }
            else
            {
                return $"Event Summary: {ItemCount} categories. Tab for recent events.";
            }
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

                if (currentMode == EventViewMode.Recent)
                {
                    RefreshRecentEvents(faction);
                }
                else
                {
                    RefreshCategorizedEvents(faction);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing events screen: {ex.Message}");
            }
        }

        private void RefreshRecentEvents(TIFactionState faction)
        {
            var recentEvents = eventReader.GetRecentEvents(faction);
            foreach (var evt in recentEvents)
            {
                items.Add(new EventItem { Type = ItemType.Event, Event = evt });
            }
        }

        private void RefreshCategorizedEvents(TIFactionState faction)
        {
            // Add category headers for categories that have events
            foreach (var category in EventReader.GetValidCategories())
            {
                int count = eventReader.GetCategoryEventCount(category, faction);
                if (count > 0)
                {
                    items.Add(new EventItem { Type = ItemType.CategoryHeader, Category = category });
                }
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

            var faction = GameControl.control?.activePlayer;
            var item = items[index];

            switch (item.Type)
            {
                case ItemType.Event:
                    return eventReader.ReadSummary(item.Event);

                case ItemType.CategoryHeader:
                    int count = eventReader.GetCategoryEventCount(item.Category, faction);
                    string categoryName = eventReader.FormatCategoryName(item.Category);
                    return $"{categoryName}: {count} event{(count != 1 ? "s" : "")}";

                default:
                    return "Unknown";
            }
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var faction = GameControl.control?.activePlayer;
            var item = items[index];

            switch (item.Type)
            {
                case ItemType.Event:
                    return eventReader.ReadDetail(item.Event);

                case ItemType.CategoryHeader:
                    int count = eventReader.GetCategoryEventCount(item.Category, faction);
                    string categoryName = eventReader.FormatCategoryName(item.Category);
                    return $"{categoryName} contains {count} event{(count != 1 ? "s" : "")}. Press Enter to browse events in this category.";

                default:
                    return "Unknown";
            }
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cache if available
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;
            var faction = GameControl.control?.activePlayer;
            var item = items[index];

            switch (item.Type)
            {
                case ItemType.Event:
                    // Events in Recent mode don't have sub-sections
                    cachedSections = new List<ISection>();
                    break;

                case ItemType.CategoryHeader:
                    // Categories have events as their sections (each event is a section)
                    cachedSections = eventReader.GetSectionsForCategory(
                        item.Category,
                        faction,
                        evt => NavigateToEvent(evt)
                    );
                    break;

                default:
                    cachedSections = new List<ISection>();
                    break;
            }

            return cachedSections;
        }

        public override bool CanDrillIntoItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;

            var item = items[index];

            // Only category headers can be drilled into
            if (item.Type == ItemType.CategoryHeader)
                return true;

            // Events can be drilled into if they have a navigation target
            // (handled by activation instead)
            return false;
        }

        /// <summary>
        /// Try to activate an item (for events with navigation targets in Recent mode).
        /// </summary>
        public void TryActivateItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return;

            var item = items[index];

            if (item.Type == ItemType.Event && item.Event?.gotoGameState != null)
            {
                NavigateToEvent(item.Event);
            }
        }

        /// <summary>
        /// Check if an item can be activated (has a navigation target).
        /// </summary>
        public bool CanActivateItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;

            var item = items[index];
            return item.Type == ItemType.Event && item.Event?.gotoGameState != null;
        }

        private void NavigateToEvent(NotificationSummaryItem evt)
        {
            if (evt?.gotoGameState == null)
            {
                OnSpeak?.Invoke("This event has no navigation target", true);
                return;
            }

            var target = evt.gotoGameState;

            // Try to navigate within Review Mode
            if (OnNavigateToGameState != null)
            {
                OnNavigateToGameState(target);
            }
            else
            {
                // Fallback: just announce what the target is
                string targetName = target.displayName ?? "Unknown";
                string typeName = eventReader.GetGameStateTypeName(target);
                OnSpeak?.Invoke($"Target: {targetName}, {typeName}", true);
            }
        }

        /// <summary>
        /// Get the current view mode.
        /// </summary>
        public EventViewMode CurrentMode => currentMode;
    }
}
