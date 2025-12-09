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
    /// Reader for TIHabModuleState shipyard objects.
    /// Extracts and formats shipyard information for accessibility.
    /// </summary>
    public class ShipyardReader : IGameStateReader<TIHabModuleState>
    {
        /// <summary>
        /// Callback for cancelling a build (removing from queue).
        /// Parameters: shipyard, queue item
        /// </summary>
        public Action<TIHabModuleState, ShipConstructionQueueItem> OnCancelBuild { get; set; }

        /// <summary>
        /// Callback for moving an item in the queue.
        /// Parameters: shipyard, queue item, new index
        /// </summary>
        public Action<TIHabModuleState, ShipConstructionQueueItem, int> OnMoveInQueue { get; set; }

        /// <summary>
        /// Callback for clearing the queue (except current build).
        /// </summary>
        public Action<TIHabModuleState> OnClearQueue { get; set; }

        /// <summary>
        /// Callback for toggling pay from Earth setting.
        /// </summary>
        public Action<TIHabModuleState, bool> OnTogglePayFromEarth { get; set; }

        /// <summary>
        /// Callback for executing a maintenance operation on a docked fleet.
        /// Parameters: fleet, operation type
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteMaintenanceOperation { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for entering selection mode (to choose ship designs).
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for adding a ship to the queue.
        /// Parameters: shipyard, design, allow pay from Earth
        /// </summary>
        public Action<TIHabModuleState, TISpaceShipTemplate, bool> OnAddToQueue { get; set; }

        public string ReadSummary(TIHabModuleState shipyard)
        {
            if (shipyard == null)
                return "Unknown shipyard";

            var sb = new StringBuilder();
            var faction = GameControl.control?.activePlayer;

            // Habitat name
            sb.Append(shipyard.hab?.displayName ?? "Unknown");

            // Current build status
            var queue = faction?.GetShipyardQueue(shipyard);
            if (queue != null && queue.Count > 0)
            {
                var current = queue[0];
                string shipClass = current.shipDesign?.fullClassName ?? "Unknown";
                if (current.costPaid)
                {
                    int days = (int)Math.Ceiling(current.daysToCompletion);
                    sb.Append($" - Building: {shipClass} ({days} day{(days != 1 ? "s" : "")})");
                }
                else
                {
                    sb.Append($" - Waiting: {shipClass}");
                }

                // Queue count (excluding current)
                int queueCount = queue.Count - 1;
                if (queueCount > 0)
                {
                    sb.Append($" | Queue: {queueCount}");
                }
            }
            else
            {
                sb.Append(" - Idle");
            }

            // Docked ships count
            int dockedCount = GetDockedShipsAtHab(shipyard.hab, faction).Count;
            if (dockedCount > 0)
            {
                sb.Append($" | Docked: {dockedCount}");
            }

            // Inactive status
            if (!shipyard.active)
            {
                sb.Append(" [INACTIVE]");
            }

            return sb.ToString();
        }

        public string ReadDetail(TIHabModuleState shipyard)
        {
            if (shipyard == null)
                return "Unknown shipyard";

            var sb = new StringBuilder();
            var faction = GameControl.control?.activePlayer;

            // Header
            sb.AppendLine($"Shipyard: {shipyard.hab?.displayName ?? "Unknown"}");
            sb.AppendLine();

            // Location info
            sb.AppendLine("Location:");
            sb.AppendLine($"  {shipyard.hab?.LocationName ?? "Unknown location"}");
            sb.AppendLine($"  Module: {shipyard.moduleTemplate?.displayName ?? "Unknown type"}");
            sb.AppendLine($"  Sector: {shipyard.sector?.shortSectorString ?? "Unknown"}");
            sb.AppendLine($"  Tier: {shipyard.hab?.tier ?? 0}");
            sb.AppendLine();

            // Status
            sb.AppendLine("Status:");
            sb.AppendLine($"  Active: {(shipyard.active ? "Yes" : "No")}");
            sb.AppendLine($"  Pay from Earth: {(shipyard.shipyardAllowPayFromEarth ? "Yes" : "No")}");

            // Gravity
            try
            {
                float gravity = (float)(shipyard.hab?.ref_habSite?.surfaceGravity_g ?? 0);
                if (gravity > 0.000001f)
                {
                    sb.AppendLine($"  Surface gravity: {gravity:F2}g");
                }
            }
            catch { }
            sb.AppendLine();

            // Current build
            var queue = faction?.GetShipyardQueue(shipyard);
            sb.AppendLine("Current Build:");
            if (queue != null && queue.Count > 0)
            {
                var current = queue[0];
                string shipClass = current.shipDesign?.fullClassName ?? "Unknown";

                if (current.costPaid)
                {
                    int progress = (int)(current.progressFraction * 100);
                    int days = (int)Math.Ceiling(current.daysToCompletion);
                    sb.AppendLine($"  {shipClass}");
                    sb.AppendLine($"  Progress: {progress}%");
                    sb.AppendLine($"  Days remaining: {days}");
                    if (current.isRefit)
                    {
                        sb.AppendLine($"  Type: Refit from {current.refit_originalShipDesign?.fullClassName ?? "unknown"}");
                    }
                }
                else
                {
                    // Waiting for resources
                    sb.AppendLine($"  {shipClass} - WAITING FOR RESOURCES");
                    try
                    {
                        var lacking = current.resourcesCost.LackingResources(faction);
                        if (lacking != null && lacking.Count > 0)
                        {
                            sb.AppendLine($"  Needs: {string.Join(", ", lacking.Select(r => r.ToString()))}");
                        }
                    }
                    catch { }
                }
            }
            else
            {
                sb.AppendLine("  None - Shipyard idle");
            }
            sb.AppendLine();

            // Build queue
            sb.AppendLine("Build Queue:");
            if (queue != null && queue.Count > 1)
            {
                for (int i = 1; i < queue.Count; i++)
                {
                    var item = queue[i];
                    string itemClass = item.shipDesign?.fullClassName ?? "Unknown";
                    int itemDays = (int)Math.Ceiling(item.durationInDays);
                    sb.AppendLine($"  {i}. {itemClass} ({itemDays} days)");
                }
            }
            else
            {
                sb.AppendLine("  Empty");
            }
            sb.AppendLine();

            // Docked ships
            var dockedShips = GetDockedShipsAtHab(shipyard.hab, faction);
            sb.AppendLine("Docked Ships:");
            if (dockedShips.Count > 0)
            {
                foreach (var ship in dockedShips)
                {
                    string damage = ship.damaged ? " [DAMAGED]" : "";
                    sb.AppendLine($"  {ship.displayName} - {ship.template?.fullClassName ?? "Unknown"}{damage}");
                }
            }
            else
            {
                sb.AppendLine("  None");
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TIHabModuleState shipyard)
        {
            var sections = new List<ISection>();
            var faction = GameControl.control?.activePlayer;

            if (shipyard == null || faction == null)
                return sections;

            try
            {
                // Section 1: Info
                var infoSection = CreateInfoSection(shipyard);
                if (infoSection != null)
                    sections.Add(infoSection);

                // Section 2: Current Build
                var currentBuildSection = CreateCurrentBuildSection(shipyard, faction);
                if (currentBuildSection != null)
                    sections.Add(currentBuildSection);

                // Section 3: Build Queue
                var queueSection = CreateQueueSection(shipyard, faction);
                if (queueSection != null)
                    sections.Add(queueSection);

                // Section 4: Add to Queue (ship design selection)
                var addToQueueSection = CreateAddToQueueSection(shipyard, faction);
                if (addToQueueSection != null)
                    sections.Add(addToQueueSection);

                // Section 5: Docked Ships
                var dockedSection = CreateDockedShipsSection(shipyard, faction);
                if (dockedSection != null)
                    sections.Add(dockedSection);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting sections for shipyard: {ex.Message}");
            }

            return sections;
        }

        private ISection CreateInfoSection(TIHabModuleState shipyard)
        {
            var section = new DataSection("Info");
            var shipyardCopy = shipyard; // Capture for closures

            try
            {
                // Habitat name
                section.AddItem("Habitat", shipyard.hab?.displayName ?? "Unknown");

                // Location
                section.AddItem("Location", shipyard.hab?.LocationName ?? "Unknown");

                // Module type and sector
                string moduleInfo = shipyard.moduleTemplate?.displayName ?? "Unknown";
                string sector = shipyard.sector?.shortSectorString;
                if (!string.IsNullOrEmpty(sector))
                {
                    moduleInfo += $" ({sector})";
                }
                section.AddItem("Module", moduleInfo);

                // Tier
                section.AddItem("Tier", (shipyard.hab?.tier ?? 0).ToString());

                // Status
                section.AddItem("Status", shipyard.active ? "Active" : "Inactive");

                // Gravity - with launch requirements for surface shipyards
                try
                {
                    float gravity = 0;
                    string gravityDetail = null;

                    if (shipyard.hab?.IsStation == true)
                    {
                        gravity = (float)(shipyard.hab.orbitState?.localGravity_gs ?? 0);
                    }
                    else
                    {
                        gravity = (float)(shipyard.hab?.ref_habSite?.surfaceGravity_g ?? 0);
                        // Surface shipyard - calculate minimum delta-V to launch
                        if (gravity > 0.000001f && shipyard.hab?.habSite != null)
                        {
                            try
                            {
                                float minDeltaV = (float)shipyard.hab.habSite.MinDeltaVToLaunch_kps((float)(shipyard.hab.ref_habSite?.surfaceGravity_mps2 ?? 0));
                                gravityDetail = $"Min {minDeltaV:F1} km/s delta-V to launch";
                            }
                            catch { }
                        }
                    }

                    if (gravity > 0.000001f)
                    {
                        string gravityStr = $"{gravity:F3}g";
                        if (gravityDetail != null)
                        {
                            section.AddItem("Gravity", gravityStr, gravityDetail);
                        }
                        else
                        {
                            section.AddItem("Gravity", gravityStr);
                        }
                    }
                    else
                    {
                        section.AddItem("Gravity", "Negligible");
                    }
                }
                catch
                {
                    section.AddItem("Gravity", "Unknown");
                }

                // Pay from Earth (toggleable)
                bool payFromEarth = shipyard.shipyardAllowPayFromEarth;
                string payDescription = Loc.T("UI.Fleets.PayFromEarthTooltip");
                section.AddItem("Pay from Earth", payFromEarth ? "On" : "Off", payDescription,
                    onActivate: () =>
                    {
                        // Toggle to opposite of current value
                        OnTogglePayFromEarth?.Invoke(shipyardCopy, !shipyardCopy.shipyardAllowPayFromEarth);
                    });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Info section: {ex.Message}");
            }

            return section;
        }

        private ISection CreateCurrentBuildSection(TIHabModuleState shipyard, TIFactionState faction)
        {
            var section = new DataSection("Current Build");
            var shipyardCopy = shipyard; // Capture for closures

            try
            {
                var queue = faction.GetShipyardQueue(shipyard);
                if (queue == null || queue.Count == 0)
                {
                    section.AddItem("Status", "Idle - No ships in queue");
                    return section;
                }

                var current = queue[0];
                var currentCopy = current; // Capture for closures
                string shipClass = current.shipDesign?.fullClassName ?? "Unknown";

                // Ship class
                section.AddItem("Ship", shipClass);

                // Refit info (show early so user knows what this is)
                if (current.isRefit)
                {
                    section.AddItem("Type", $"Refit from {current.refit_originalShipDesign?.fullClassName ?? "unknown"}");
                }

                // Check if uses Boost (Earth resources)
                bool usesBoost = false;
                try
                {
                    usesBoost = current.resourcesCost.GetSingleCostValue(FactionResource.Boost) > 0f;
                }
                catch { }
                if (usesBoost)
                {
                    section.AddItem("Resources", "Uses Earth resources (Boost)");
                }

                if (current.costPaid)
                {
                    // Progress
                    int progress = (int)(current.progressFraction * 100);
                    section.AddItem("Progress", $"{progress}%");

                    // Days remaining
                    int days = (int)Math.Ceiling(current.daysToCompletion);
                    section.AddItem("Days remaining", days.ToString());

                    // Can cancel even after construction started (will get partial refund)
                    section.AddItem("Cancel build", "Cancel and get partial refund",
                        onActivate: () =>
                        {
                            OnCancelBuild?.Invoke(shipyardCopy, currentCopy);
                        });
                }
                else
                {
                    // Waiting for resources
                    section.AddItem("Status", "Waiting for resources");

                    try
                    {
                        var lacking = current.resourcesCost.LackingResources(faction);
                        if (lacking != null && lacking.Count > 0)
                        {
                            string lackingStr = string.Join(", ", lacking.Select(r => r.ToString()));
                            section.AddItem("Needs", lackingStr);
                        }
                    }
                    catch { }

                    // Total build time
                    int totalDays = (int)Math.Ceiling(current.durationInDays);
                    section.AddItem("Build time", $"{totalDays} days");

                    // Can cancel if not cost-paid (full refund)
                    section.AddItem("Cancel build", "Remove from queue (full refund)",
                        onActivate: () =>
                        {
                            OnCancelBuild?.Invoke(shipyardCopy, currentCopy);
                        });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Current Build section: {ex.Message}");
            }

            return section;
        }

        private ISection CreateQueueSection(TIHabModuleState shipyard, TIFactionState faction)
        {
            var section = new DataSection("Build Queue");
            var shipyardCopy = shipyard; // Capture for closures

            try
            {
                var queue = faction.GetShipyardQueue(shipyard);
                if (queue == null || queue.Count <= 1)
                {
                    section.AddItem("Status", "Queue is empty");
                    return section;
                }

                // Queue count (excluding current)
                int queueCount = queue.Count - 1;
                section.AddItem("Queued ships", queueCount.ToString());

                // Each queued item (starting from index 1)
                bool firstItemCostPaid = queue[0].costPaid;

                for (int i = 1; i < queue.Count; i++)
                {
                    var item = queue[i];
                    var itemCopy = item; // Capture for closure
                    int position = i;
                    string shipClass = item.shipDesign?.fullClassName ?? "Unknown";
                    int buildDays = (int)Math.Ceiling(item.durationInDays);

                    // Check if uses Boost (Earth resources)
                    bool usesBoost = false;
                    try
                    {
                        usesBoost = item.resourcesCost.GetSingleCostValue(FactionResource.Boost) > 0f;
                    }
                    catch { }

                    string label = $"#{position}: {shipClass}";
                    string value = $"{buildDays} day{(buildDays != 1 ? "s" : "")}";
                    if (usesBoost)
                    {
                        value += " (uses Earth resources)";
                    }

                    // Build detail text
                    var detailSb = new StringBuilder();
                    detailSb.AppendLine($"{shipClass}");
                    detailSb.AppendLine($"Build time: {buildDays} days");
                    detailSb.AppendLine($"Queue position: {position}");
                    if (item.isRefit)
                    {
                        detailSb.AppendLine($"Refit from: {item.refit_originalShipDesign?.fullClassName ?? "unknown"}");
                    }
                    if (usesBoost)
                    {
                        detailSb.AppendLine("Uses Earth resources (Boost)");
                    }

                    // Determine available actions based on queue constraints
                    // Can move up if: position > 1, OR (position == 1 AND first item not started)
                    bool canMoveUp = position > 1 || (position == 1 && !firstItemCostPaid);
                    // Can move down if: not last item AND this item not started
                    bool canMoveDown = position < queue.Count - 1 && !item.costPaid;

                    if (canMoveUp) detailSb.AppendLine("Can move up in queue");
                    if (canMoveDown) detailSb.AppendLine("Can move down in queue");

                    section.AddItem(label, value, detailSb.ToString(),
                        onActivate: () =>
                        {
                            // Default action is cancel
                            OnCancelBuild?.Invoke(shipyardCopy, itemCopy);
                        });
                }

                // Clear queue action (if more than 1 item in queue, or 1 item that's not cost-paid)
                bool canClear = queue.Count > 1 || (queue.Count == 1 && !queue[0].costPaid);
                if (canClear)
                {
                    section.AddItem("Clear queue", "Remove all queued ships",
                        onActivate: () =>
                        {
                            OnClearQueue?.Invoke(shipyardCopy);
                        });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Queue section: {ex.Message}");
            }

            return section;
        }

        private ISection CreateAddToQueueSection(TIHabModuleState shipyard, TIFactionState faction)
        {
            var section = new DataSection("Add to Queue");
            var shipyardCopy = shipyard; // Capture for closures

            try
            {
                // Get designs that can be built at this shipyard
                var availableDesigns = GetBuildableDesigns(shipyard, faction);

                if (availableDesigns.Count == 0)
                {
                    // Check why no designs available
                    var allDesigns = faction.shipDesigns?.ToList() ?? new List<TISpaceShipTemplate>();
                    if (allDesigns.Count == 0)
                    {
                        section.AddItem("Status", "No ship designs - create designs in Ship Classes screen");
                    }
                    else
                    {
                        section.AddItem("Status", "No designs can be built at this shipyard");
                        section.AddItem("Reason", "Check ship thrust vs gravity, or shipyard tier");
                    }
                    return section;
                }

                section.AddItem("Available designs", $"{availableDesigns.Count} design{(availableDesigns.Count != 1 ? "s" : "")} can be built here");

                // Show each design with cost and build time
                foreach (var design in availableDesigns)
                {
                    var designCopy = design; // Capture for closure
                    string className = design.fullClassName ?? design.className ?? "Unknown";

                    // Get cost and build time for this shipyard
                    string costInfo = "";
                    string buildTime = "";
                    bool canAfford = true; // Default to true if cost check fails
                    try
                    {
                        var cost = design.spaceResourceConstructionCost(forceUpdateToCache: false, shipyard: shipyard);
                        float days = cost.completionTime_days;
                        buildTime = $"{days:N0} days";

                        // Format cost summary
                        var costParts = new List<string>();
                        if (cost.GetSingleCostValue(FactionResource.Money) > 0)
                            costParts.Add($"${cost.GetSingleCostValue(FactionResource.Money):N0}M");
                        if (cost.GetSingleCostValue(FactionResource.Volatiles) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Volatiles):N0} Volatiles");
                        if (cost.GetSingleCostValue(FactionResource.Metals) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Metals):N0} Metals");
                        if (cost.GetSingleCostValue(FactionResource.NobleMetals) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.NobleMetals):N0} Nobles");
                        if (cost.GetSingleCostValue(FactionResource.Fissiles) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Fissiles):N0} Fissiles");
                        if (cost.GetSingleCostValue(FactionResource.Antimatter) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Antimatter):N1} Antimatter");
                        if (cost.GetSingleCostValue(FactionResource.Exotics) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Exotics):N1} Exotics");
                        if (cost.GetSingleCostValue(FactionResource.Boost) > 0)
                            costParts.Add($"{cost.GetSingleCostValue(FactionResource.Boost):N0} Boost");

                        costInfo = string.Join(", ", costParts);

                        // Check affordability
                        canAfford = cost.CanAfford(faction);
                        if (!canAfford)
                        {
                            costInfo = "[Cannot afford] " + costInfo;
                        }
                    }
                    catch { }

                    string label = className;
                    string value = buildTime;

                    // Build detail text
                    var detailSb = new StringBuilder();
                    detailSb.AppendLine($"Ship: {className}");
                    detailSb.AppendLine($"Build time: {buildTime}");
                    if (!string.IsNullOrEmpty(costInfo))
                    {
                        detailSb.AppendLine($"Cost: {costInfo}");
                    }
                    detailSb.AppendLine($"Mass: {design.wetMass_tons:N0} tons");
                    detailSb.AppendLine($"Combat value: {design.TemplateSpaceCombatValue():N0}");

                    // Only allow activation if player can afford it
                    if (canAfford)
                    {
                        section.AddItem(label, value, detailSb.ToString(),
                            onActivate: () =>
                            {
                                // Queue this design at this shipyard
                                bool allowPayFromEarth = shipyardCopy.shipyardAllowPayFromEarth;
                                OnAddToQueue?.Invoke(shipyardCopy, designCopy, allowPayFromEarth);
                            });
                    }
                    else
                    {
                        // No action - item is informational only when unaffordable
                        section.AddItem(label, value, detailSb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Add to Queue section: {ex.Message}");
            }

            return section;
        }

        /// <summary>
        /// Get ship designs that can be built at this shipyard.
        /// </summary>
        private List<TISpaceShipTemplate> GetBuildableDesigns(TIHabModuleState shipyard, TIFactionState faction)
        {
            var result = new List<TISpaceShipTemplate>();

            try
            {
                var designs = faction.shipDesigns?.ToList() ?? new List<TISpaceShipTemplate>();

                foreach (var design in designs)
                {
                    // Skip obsolete designs
                    try
                    {
                        if (design.Obsolete(faction))
                            continue;
                    }
                    catch { }

                    // Check if can build at this shipyard
                    if (design.CanBuildAtShipyard(shipyard))
                    {
                        result.Add(design);
                    }
                }

                // Sort by name
                result = result.OrderBy(d => d.className ?? "").ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting buildable designs: {ex.Message}");
            }

            return result;
        }

        private ISection CreateDockedShipsSection(TIHabModuleState shipyard, TIFactionState faction)
        {
            var section = new DataSection("Docked Ships");

            try
            {
                var dockedShips = GetDockedShipsAtHab(shipyard.hab, faction);
                if (dockedShips.Count == 0)
                {
                    section.AddItem("Status", "No ships docked at this habitat");
                    return section;
                }

                // Summary
                int damagedCount = dockedShips.Count(s => s.damaged);
                section.AddItem("Total docked", $"{dockedShips.Count} ship{(dockedShips.Count != 1 ? "s" : "")}" +
                    (damagedCount > 0 ? $", {damagedCount} damaged" : ""));

                // Group ships by fleet
                var fleetGroups = dockedShips
                    .Where(s => s.fleet != null)
                    .GroupBy(s => s.fleet)
                    .OrderBy(g => g.Key.displayName);

                foreach (var fleetGroup in fleetGroups)
                {
                    var fleet = fleetGroup.Key;
                    var fleetCopy = fleet; // Capture for closures
                    int shipCount = fleetGroup.Count();
                    int fleetDamaged = fleetGroup.Count(s => s.damaged);

                    // Fleet header with maintenance status
                    bool needsRefuel = fleet.NeedsRefuel();
                    bool needsRearm = fleet.NeedsRearm();
                    bool needsRepair = fleet.NeedsRepair();

                    var statusParts = new List<string>();
                    if (needsRefuel) statusParts.Add("needs refuel");
                    if (needsRearm) statusParts.Add("needs rearm");
                    if (needsRepair) statusParts.Add("needs repair");

                    string status = statusParts.Count > 0 ? string.Join(", ", statusParts) : "ready";

                    string fleetLabel = $"{fleet.displayName}";
                    string fleetValue = $"{shipCount} ship{(shipCount != 1 ? "s" : "")}, {status}";

                    // Build detail with damage info
                    var detailSb = new StringBuilder();
                    detailSb.AppendLine($"Fleet: {fleet.displayName}");
                    detailSb.AppendLine($"Ships: {shipCount}");
                    if (fleetDamaged > 0)
                    {
                        detailSb.AppendLine($"Damaged: {fleetDamaged}");
                        foreach (var ship in fleetGroup.Where(s => s.damaged))
                        {
                            detailSb.AppendLine($"  {ship.displayName}:");
                            var damagedSystems = ship.DamagedSystems();
                            if (damagedSystems.Count > 0)
                            {
                                detailSb.AppendLine($"    Systems: {string.Join(", ", damagedSystems)}");
                            }
                            // Armor damage
                            var damagedArmor = ship.armor?.Where(kv => kv.Value?.damaged == true).Select(kv => kv.Key.ToString());
                            if (damagedArmor != null && damagedArmor.Any())
                            {
                                detailSb.AppendLine($"    Armor: {string.Join(", ", damagedArmor)}");
                            }
                        }
                    }

                    section.AddItem(fleetLabel, fleetValue, detailSb.ToString(),
                        onActivate: () =>
                        {
                            // Show maintenance options
                            ShowFleetMaintenanceOptions(fleetCopy);
                        });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Docked Ships section: {ex.Message}");
            }

            return section;
        }

        private void ShowFleetMaintenanceOptions(TISpaceFleetState fleet)
        {
            bool needsRefuel = fleet.NeedsRefuel();
            bool needsRearm = fleet.NeedsRearm();
            bool needsRepair = fleet.NeedsRepair();

            if (!needsRefuel && !needsRearm && !needsRepair)
            {
                OnSpeak?.Invoke($"{fleet.displayName} is fully supplied and repaired", true);
                return;
            }

            // Determine best action
            if ((needsRefuel || needsRearm) && needsRepair)
            {
                OnSpeak?.Invoke($"Starting resupply and repair for {fleet.displayName}", true);
                OnExecuteMaintenanceOperation?.Invoke(fleet, typeof(ResupplyAndRepairOperation));
            }
            else if (needsRepair)
            {
                OnSpeak?.Invoke($"Starting repair for {fleet.displayName}", true);
                OnExecuteMaintenanceOperation?.Invoke(fleet, typeof(RepairFleetOperation));
            }
            else if (needsRefuel || needsRearm)
            {
                OnSpeak?.Invoke($"Starting resupply for {fleet.displayName}", true);
                OnExecuteMaintenanceOperation?.Invoke(fleet, typeof(ResupplyOperation));
            }
        }

        #region Static Helpers

        /// <summary>
        /// Get all shipyards belonging to the player's faction.
        /// </summary>
        public static List<TIHabModuleState> GetPlayerShipyards(TIFactionState faction)
        {
            if (faction == null)
                return new List<TIHabModuleState>();

            try
            {
                return faction.nShipyardQueues.Keys
                    .Where(s => s.completed && s.moduleTemplate?.allowsShipConstruction == true)
                    .OrderBy(s => s.hab?.displayName ?? "")
                    .ThenBy(s => s.moduleTemplate?.displayName ?? "")
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player shipyards: {ex.Message}");
                return new List<TIHabModuleState>();
            }
        }

        /// <summary>
        /// Get all ships docked at a specific habitat belonging to the faction.
        /// </summary>
        public static List<TISpaceShipState> GetDockedShipsAtHab(TIHabState hab, TIFactionState faction)
        {
            if (hab == null || faction == null)
                return new List<TISpaceShipState>();

            try
            {
                return faction.fleets
                    .Where(f => f.dockedAtHab && f.ref_hab == hab && !f.archived && !f.dummyFleet)
                    .SelectMany(f => f.ships ?? Enumerable.Empty<TISpaceShipState>())
                    .Where(s => s != null && !s.archived)
                    .OrderBy(s => s.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting docked ships: {ex.Message}");
                return new List<TISpaceShipState>();
            }
        }

        /// <summary>
        /// Get total ships under construction across all shipyards.
        /// </summary>
        public static int GetTotalShipsUnderConstruction(TIFactionState faction)
        {
            if (faction == null)
                return 0;

            try
            {
                return faction.nShipyardQueues.Values
                    .Sum(queue => queue.Count(item => item.costPaid));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get total ships queued (not yet started) across all shipyards.
        /// </summary>
        public static int GetTotalShipsQueued(TIFactionState faction)
        {
            if (faction == null)
                return 0;

            try
            {
                return faction.nShipyardQueues.Values
                    .Sum(queue => queue.Count(item => !item.costPaid));
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
