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
    /// </summary>
    public class FleetsScreen : ScreenBase
    {
        private List<TISpaceFleetState> items = new List<TISpaceFleetState>();
        private readonly FleetReader fleetReader = new FleetReader();

        // View mode: false = your fleets only, true = all known fleets
        private bool showAllMode = false;

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
                    // Add your fleets first, then known enemy fleets
                    items.AddRange(FleetReader.GetPlayerFleets(faction));
                    items.AddRange(FleetReader.GetKnownEnemyFleets(faction));
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
            Refresh();

            var faction = GameControl.control?.activePlayer;
            if (showAllMode)
            {
                int myFleets = FleetReader.GetPlayerFleets(faction).Count;
                int knownEnemy = FleetReader.GetKnownEnemyFleets(faction).Count;
                return $"Showing all known fleets: {myFleets} yours, {knownEnemy} enemy";
            }
            else
            {
                return $"Showing your fleets only: {items.Count} fleet{(items.Count != 1 ? "s" : "")}";
            }
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

            // Wire up callbacks for transfer planning
            fleetReader.OnEnterTransferMode = OnEnterTransferMode;
            fleetReader.OnSpeak = OnSpeak;

            var fleet = items[index];
            cachedItemIndex = index;
            cachedSections = fleetReader.GetSections(fleet);

            return cachedSections;
        }

        /// <summary>
        /// Callback for entering transfer planning mode.
        /// </summary>
        public Action<TISpaceFleetState> OnEnterTransferMode { get; set; }

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
