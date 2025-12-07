using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for notification/event data.
    /// Provides summary, detail, and categorized views of game events.
    /// </summary>
    public class EventReader
    {
        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Read a short summary of an event (for list navigation).
        /// The event text typically already includes the date.
        /// </summary>
        public string ReadSummary(NotificationSummaryItem item)
        {
            if (item == null)
                return "Unknown event";

            var sb = new StringBuilder();

            // Add event text (cleaned) - date is typically already included
            string eventText = TISpeechMod.CleanText(item.itemSummary);
            sb.Append(eventText);

            // Add outcome for missions if not None
            if (item.outcome != TIMissionOutcome.None)
            {
                sb.Append($" ({FormatOutcome(item.outcome)})");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Read detailed information about an event (for Numpad * reading).
        /// </summary>
        public string ReadDetail(NotificationSummaryItem item)
        {
            if (item == null)
                return "Unknown event";

            var sb = new StringBuilder();

            // Event text already includes the date
            sb.AppendLine(TISpeechMod.CleanText(item.itemSummary));

            if (item.outcome != TIMissionOutcome.None)
            {
                sb.AppendLine($"Outcome: {FormatOutcome(item.outcome)}");
            }

            if (item.alienRelated)
            {
                sb.AppendLine("Alien-related event");
            }

            if (item.gotoGameState != null)
            {
                string targetName = item.gotoGameState.displayName ?? "location";
                string targetType = GetGameStateTypeName(item.gotoGameState);
                sb.AppendLine($"Navigation target: {targetName} ({targetType})");
                sb.AppendLine("Press Enter to navigate");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format a category name for display.
        /// </summary>
        public string FormatCategoryName(SummaryCategory category)
        {
            switch (category)
            {
                case SummaryCategory.Missions:
                    return "Missions";
                case SummaryCategory.CouncilorSightings:
                    return "Councilor Sightings";
                case SummaryCategory.EarthEvents:
                    return "Earth Events";
                case SummaryCategory.SpaceEvents:
                    return "Space Events";
                case SummaryCategory.Bombardment:
                    return "Bombardment";
                default:
                    return category.ToString();
            }
        }

        /// <summary>
        /// Get the count of events in a category visible to the active player.
        /// </summary>
        public int GetCategoryEventCount(SummaryCategory category, TIFactionState activePlayer)
        {
            try
            {
                var queue = GameStateManager.NotificationQueue();
                if (queue?.panelSummaryQueue == null || activePlayer == null)
                    return 0;

                if (!queue.panelSummaryQueue.TryGetValue(category, out var events))
                    return 0;

                return events.Count(e => e.summaryLogFactions?.Contains(activePlayer) == true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting category event count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get events for a specific category as individual sections.
        /// Each event becomes its own section so they appear directly when drilling into a category.
        /// </summary>
        public List<ISection> GetSectionsForCategory(SummaryCategory category, TIFactionState activePlayer, Action<NotificationSummaryItem> onNavigate)
        {
            var sections = new List<ISection>();

            try
            {
                var queue = GameStateManager.NotificationQueue();
                if (queue?.panelSummaryQueue == null || activePlayer == null)
                {
                    var emptySection = new DataSection("No events");
                    sections.Add(emptySection);
                    return sections;
                }

                if (!queue.panelSummaryQueue.TryGetValue(category, out var events))
                {
                    var emptySection = new DataSection("No events in this category");
                    sections.Add(emptySection);
                    return sections;
                }

                var playerEvents = events
                    .Where(e => e.summaryLogFactions?.Contains(activePlayer) == true)
                    .ToList();

                if (playerEvents.Count == 0)
                {
                    var emptySection = new DataSection("No events in this category");
                    sections.Add(emptySection);
                    return sections;
                }

                // Each event becomes its own section
                foreach (var item in playerEvents)
                {
                    string sectionName = ReadSummary(item);
                    var section = new DataSection(sectionName);

                    // Add navigation action as an item if there's a target
                    if (item.gotoGameState != null && onNavigate != null)
                    {
                        string targetName = item.gotoGameState.displayName ?? "location";
                        string targetType = GetGameStateTypeName(item.gotoGameState);
                        var capturedItem = item;
                        section.AddItem($"Navigate to {targetName} ({targetType})", () => onNavigate(capturedItem));
                    }

                    sections.Add(section);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building category sections: {ex.Message}");
                var errorSection = new DataSection("Error loading events");
                sections.Add(errorSection);
            }

            return sections;
        }

        /// <summary>
        /// Format mission outcome for display.
        /// </summary>
        public string FormatOutcome(TIMissionOutcome outcome)
        {
            switch (outcome)
            {
                case TIMissionOutcome.CriticalSuccess:
                    return "Critical Success";
                case TIMissionOutcome.Success:
                    return "Success";
                case TIMissionOutcome.Failure:
                    return "Failure";
                case TIMissionOutcome.CriticalFailure:
                    return "Critical Failure";
                case TIMissionOutcome.Aborted:
                    return "Aborted";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Get a human-readable name for a game state type.
        /// </summary>
        public string GetGameStateTypeName(TIGameState state)
        {
            if (state == null)
                return "Unknown";

            if (state.isNationState)
                return "Nation";
            if (state.isRegionState)
                return "Region";
            if (state.isCouncilorState)
                return "Councilor";
            if (state.isSpaceFleetState)
                return "Fleet";
            if (state.isHabState)
                return "Hab";
            if (state.isSpaceBodyState)
                return "Space Body";
            if (state.isArmyState)
                return "Army";
            if (state.isFactionState)
                return "Faction";
            if (state.isOrgState)
                return "Organization";

            return "Object";
        }

        /// <summary>
        /// Get recent events from the newsfeed queue.
        /// </summary>
        public List<NotificationSummaryItem> GetRecentEvents(TIFactionState activePlayer, int maxCount = 30)
        {
            try
            {
                var queue = GameStateManager.NotificationQueue();
                if (queue?.notificationSummaryQueue == null || activePlayer == null)
                    return new List<NotificationSummaryItem>();

                return queue.notificationSummaryQueue
                    .Where(e => e.newsFeedFactions?.Contains(activePlayer) == true)
                    .Take(maxCount)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting recent events: {ex.Message}");
                return new List<NotificationSummaryItem>();
            }
        }

        /// <summary>
        /// Get events for a specific category.
        /// </summary>
        public List<NotificationSummaryItem> GetCategoryEvents(SummaryCategory category, TIFactionState activePlayer)
        {
            try
            {
                var queue = GameStateManager.NotificationQueue();
                if (queue?.panelSummaryQueue == null || activePlayer == null)
                    return new List<NotificationSummaryItem>();

                if (!queue.panelSummaryQueue.TryGetValue(category, out var events))
                    return new List<NotificationSummaryItem>();

                return events
                    .Where(e => e.summaryLogFactions?.Contains(activePlayer) == true)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting category events: {ex.Message}");
                return new List<NotificationSummaryItem>();
            }
        }

        /// <summary>
        /// Get all valid summary categories (excluding None).
        /// </summary>
        public static SummaryCategory[] GetValidCategories()
        {
            return new[]
            {
                SummaryCategory.Missions,
                SummaryCategory.CouncilorSightings,
                SummaryCategory.EarthEvents,
                SummaryCategory.SpaceEvents,
                SummaryCategory.Bombardment
            };
        }
    }
}
