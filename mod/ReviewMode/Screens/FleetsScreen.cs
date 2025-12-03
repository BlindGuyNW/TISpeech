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
    /// Fleets screen - browse your space fleets and their ships.
    /// Supports view mode toggle to see known enemy fleets.
    /// Supports faction filtering when viewing all fleets.
    /// </summary>
    public class FleetsScreen : ScreenBase
    {
        private List<TISpaceFleetState> items = new List<TISpaceFleetState>();
        private readonly FleetReader fleetReader = new FleetReader();

        // View mode: false = your fleets only, true = all known fleets
        private bool showAllMode = false;

        // Faction filtering (in showAllMode)
        private List<TIFactionState> knownFactions = new List<TIFactionState>();
        private int factionFilterIndex = -1; // -1 = show all, 0+ = specific faction

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Fleets";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int myFleets = FleetReader.GetPlayerFleets(faction).Count;
                    if (showAllMode)
                    {
                        if (factionFilterIndex >= 0 && factionFilterIndex < knownFactions.Count)
                        {
                            // Filtered to specific faction
                            var filteredFaction = knownFactions[factionFilterIndex];
                            int factionFleets = items.Count;
                            return $"{filteredFaction.displayName}: {factionFleets} fleet{(factionFleets != 1 ? "s" : "")}";
                        }
                        int knownEnemy = FleetReader.GetKnownEnemyFleets(faction).Count;
                        return $"All known fleets: {myFleets} yours, {knownEnemy} enemy";
                    }
                    return $"{myFleets} fleet{(myFleets != 1 ? "s" : "")}";
                }
                return "Manage your space fleets";
            }
        }

        public override bool SupportsViewModeToggle => true;
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

                if (showAllMode)
                {
                    // Get all known enemy fleets
                    var enemyFleets = FleetReader.GetKnownEnemyFleets(faction);

                    // Build list of known factions (player first, then enemies by name)
                    knownFactions.Clear();
                    knownFactions.Add(faction); // Player faction always first
                    var enemyFactions = enemyFleets
                        .Where(f => f.faction != null)
                        .Select(f => f.faction)
                        .Distinct()
                        .OrderBy(f => f.displayName);
                    knownFactions.AddRange(enemyFactions);

                    // Apply faction filter
                    if (factionFilterIndex >= 0 && factionFilterIndex < knownFactions.Count)
                    {
                        var filterFaction = knownFactions[factionFilterIndex];
                        if (filterFaction == faction)
                        {
                            items.AddRange(FleetReader.GetPlayerFleets(faction));
                        }
                        else
                        {
                            items.AddRange(enemyFleets.Where(f => f.faction == filterFaction));
                        }
                    }
                    else
                    {
                        // No filter - show all, grouped by faction
                        items.AddRange(FleetReader.GetPlayerFleets(faction));
                        // Group enemy fleets by faction
                        foreach (var enemyFaction in enemyFactions)
                        {
                            items.AddRange(enemyFleets.Where(f => f.faction == enemyFaction));
                        }
                    }
                }
                else
                {
                    items.AddRange(FleetReader.GetPlayerFleets(faction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing fleets: {ex.Message}");
            }
        }

        public override string ToggleViewMode()
        {
            showAllMode = !showAllMode;
            factionFilterIndex = -1; // Reset faction filter when toggling view mode
            Refresh();

            var faction = GameControl.control?.activePlayer;
            if (showAllMode)
            {
                int myFleets = FleetReader.GetPlayerFleets(faction).Count;
                int knownEnemy = FleetReader.GetKnownEnemyFleets(faction).Count;
                string factionHint = knownFactions.Count > 1 ? " Use [ and ] to filter by faction." : "";
                return $"Showing all known fleets: {myFleets} yours, {knownEnemy} enemy.{factionHint}";
            }
            else
            {
                return $"Showing your fleets only: {items.Count} fleet{(items.Count != 1 ? "s" : "")}";
            }
        }

        /// <summary>
        /// Indicates this screen supports faction filtering in all mode.
        /// </summary>
        public override bool SupportsFactionFilter => showAllMode && knownFactions.Count > 1;

        /// <summary>
        /// Cycle to the next faction filter.
        /// </summary>
        public override string NextFactionFilter()
        {
            if (!showAllMode || knownFactions.Count <= 1)
                return null;

            factionFilterIndex++;
            if (factionFilterIndex >= knownFactions.Count)
                factionFilterIndex = -1; // Back to "all"

            Refresh();
            return GetFactionFilterAnnouncement();
        }

        /// <summary>
        /// Cycle to the previous faction filter.
        /// </summary>
        public override string PreviousFactionFilter()
        {
            if (!showAllMode || knownFactions.Count <= 1)
                return null;

            factionFilterIndex--;
            if (factionFilterIndex < -1)
                factionFilterIndex = knownFactions.Count - 1;

            Refresh();
            return GetFactionFilterAnnouncement();
        }

        private string GetFactionFilterAnnouncement()
        {
            if (factionFilterIndex < 0 || factionFilterIndex >= knownFactions.Count)
            {
                return $"All factions: {items.Count} fleet{(items.Count != 1 ? "s" : "")}";
            }

            var filteredFaction = knownFactions[factionFilterIndex];
            var playerFaction = GameControl.control?.activePlayer;
            string factionLabel = filteredFaction == playerFaction ? "Your fleets" : filteredFaction.displayName;
            return $"{factionLabel}: {items.Count} fleet{(items.Count != 1 ? "s" : "")}";
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid fleet";

            var fleet = items[index];

            // Add faction prefix for enemy fleets
            var faction = GameControl.control?.activePlayer;
            if (fleet.faction != faction && fleet.faction != null)
            {
                return $"[{fleet.faction.displayName}] {fleetReader.ReadSummary(fleet)}";
            }

            return fleetReader.ReadSummary(fleet);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid fleet";

            var fleet = items[index];
            var sb = new System.Text.StringBuilder();

            // Add faction info for enemy fleets
            var faction = GameControl.control?.activePlayer;
            if (fleet.faction != faction && fleet.faction != null)
            {
                sb.AppendLine($"Faction: {fleet.faction.displayName}");
            }

            sb.Append(fleetReader.ReadDetail(fleet));
            return sb.ToString();
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            // Wire up callbacks for fleet operations
            fleetReader.OnEnterTransferMode = OnEnterTransferMode;
            fleetReader.OnSpeak = OnSpeak;
            fleetReader.OnExecuteSimpleOperation = OnExecuteSimpleOperation;
            fleetReader.OnSelectHomeport = OnSelectHomeport;
            fleetReader.OnSelectMergeTarget = OnSelectMergeTarget;
            fleetReader.OnExecuteMaintenanceOperation = OnExecuteMaintenanceOperation;

            var fleet = items[index];
            cachedItemIndex = index;
            cachedSections = fleetReader.GetSections(fleet);

            return cachedSections;
        }

        /// <summary>
        /// Callback for entering transfer planning mode.
        /// </summary>
        public Action<TISpaceFleetState> OnEnterTransferMode { get; set; }

        /// <summary>
        /// Callback for executing a simple fleet operation (undock, cancel, clear homeport, merge all).
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteSimpleOperation { get; set; }

        /// <summary>
        /// Callback for selecting a homeport for a fleet.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectHomeport { get; set; }

        /// <summary>
        /// Callback for selecting a fleet to merge with.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectMergeTarget { get; set; }

        /// <summary>
        /// Callback for executing a maintenance operation (resupply, repair).
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteMaintenanceOperation { get; set; }

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
                return "Fleets. No fleets available.";
            }
            return $"Fleets. {Description}. Press Enter to browse.";
        }
    }
}
