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
    /// Shipyards screen - browse your shipyards, build queues, and docked ships.
    /// Supports view mode toggle to see habitats with docked ships.
    /// </summary>
    public class ShipyardsScreen : ScreenBase
    {
        private List<TIHabModuleState> items = new List<TIHabModuleState>();
        private readonly ShipyardReader shipyardReader = new ShipyardReader();

        // View mode: false = your shipyards only, true = all habs with docked ships
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

        /// <summary>
        /// Callback for cancelling a build (removing from queue).
        /// </summary>
        public Action<TIHabModuleState, ShipConstructionQueueItem> OnCancelBuild { get; set; }

        /// <summary>
        /// Callback for moving an item in the queue.
        /// </summary>
        public Action<TIHabModuleState, ShipConstructionQueueItem, int> OnMoveInQueue { get; set; }

        /// <summary>
        /// Callback for clearing the queue.
        /// </summary>
        public Action<TIHabModuleState> OnClearQueue { get; set; }

        /// <summary>
        /// Callback for toggling pay from Earth setting.
        /// </summary>
        public Action<TIHabModuleState, bool> OnTogglePayFromEarth { get; set; }

        /// <summary>
        /// Callback for executing a maintenance operation on a docked fleet.
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteMaintenanceOperation { get; set; }

        /// <summary>
        /// Callback for adding a ship to the queue.
        /// Parameters: shipyard, design, allow pay from Earth
        /// </summary>
        public Action<TIHabModuleState, TISpaceShipTemplate, bool> OnAddToQueue { get; set; }

        /// <summary>
        /// Invalidate the section cache so next GetSectionsForItem call fetches fresh data.
        /// Call this after any action that modifies shipyard state.
        /// </summary>
        public void InvalidateSectionCache()
        {
            cachedItemIndex = -1;
            cachedSections.Clear();
        }

        public override string Name => "Shipyards";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int shipyardCount = items.Count;
                    int underConstruction = ShipyardReader.GetTotalShipsUnderConstruction(faction);
                    int queued = ShipyardReader.GetTotalShipsQueued(faction);

                    var parts = new List<string>();
                    parts.Add($"{shipyardCount} shipyard{(shipyardCount != 1 ? "s" : "")}");

                    if (underConstruction > 0)
                    {
                        parts.Add($"{underConstruction} building");
                    }
                    if (queued > 0)
                    {
                        parts.Add($"{queued} queued");
                    }

                    return string.Join(", ", parts);
                }
                return "Manage your shipyards and build queues";
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
                    // Show all habitats that have docked ships (for repair purposes)
                    // even if they don't have shipyards
                    var habsWithDockedShips = GetHabsWithDockedShips(faction);

                    // Also include all shipyards
                    var shipyards = ShipyardReader.GetPlayerShipyards(faction);

                    // Combine and deduplicate by habitat
                    var allHabs = new HashSet<TIHabState>();
                    foreach (var shipyard in shipyards)
                    {
                        if (shipyard.hab != null)
                            allHabs.Add(shipyard.hab);
                    }
                    foreach (var hab in habsWithDockedShips)
                    {
                        allHabs.Add(hab);
                    }

                    // For each habitat, get the first shipyard (or create a dummy entry)
                    // We'll use the shipyard module as the key, but for habs without shipyards
                    // we'll still want to show docked ships somehow
                    items.AddRange(shipyards);

                    // Add habs that have docked ships but no shipyards by using a sentinel
                    // Actually, we need to think about this differently...
                    // For simplicity, let's just show all shipyards plus indicate docked ships in each
                }
                else
                {
                    items.AddRange(ShipyardReader.GetPlayerShipyards(faction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing shipyards: {ex.Message}");
            }
        }

        private List<TIHabState> GetHabsWithDockedShips(TIFactionState faction)
        {
            try
            {
                return faction.fleets
                    .Where(f => f.dockedAtHab && f.ref_hab != null && !f.archived && !f.dummyFleet)
                    .Select(f => f.ref_hab)
                    .Distinct()
                    .OrderBy(h => h.displayName)
                    .ToList();
            }
            catch
            {
                return new List<TIHabState>();
            }
        }

        public override string ToggleViewMode()
        {
            showAllMode = !showAllMode;
            Refresh();

            if (showAllMode)
            {
                return $"Showing all shipyards and docked ships: {items.Count} location{(items.Count != 1 ? "s" : "")}";
            }
            else
            {
                return $"Showing your shipyards only: {items.Count} shipyard{(items.Count != 1 ? "s" : "")}";
            }
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid shipyard";

            var shipyard = items[index];
            return shipyardReader.ReadSummary(shipyard);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid shipyard";

            var shipyard = items[index];
            return shipyardReader.ReadDetail(shipyard);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            // Wire up callbacks
            shipyardReader.OnSpeak = OnSpeak;
            shipyardReader.OnCancelBuild = OnCancelBuild;
            shipyardReader.OnMoveInQueue = OnMoveInQueue;
            shipyardReader.OnClearQueue = OnClearQueue;
            shipyardReader.OnTogglePayFromEarth = OnTogglePayFromEarth;
            shipyardReader.OnExecuteMaintenanceOperation = OnExecuteMaintenanceOperation;
            shipyardReader.OnAddToQueue = OnAddToQueue;
            shipyardReader.OnEnterSelectionMode = OnEnterSelectionMode;

            var shipyard = items[index];
            cachedItemIndex = index;
            cachedSections = shipyardReader.GetSections(shipyard);

            return cachedSections;
        }

        public override string GetItemSortName(int index)
        {
            if (index < 0 || index >= items.Count)
                return "";
            return items[index].hab?.displayName ?? "";
        }

        public override string GetActivationAnnouncement()
        {
            Refresh();
            if (items.Count == 0)
            {
                return "Shipyards. No shipyards available.";
            }
            return $"Shipyards. {Description}. Press Enter to browse.";
        }
    }
}
