using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Entities;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for TISpaceFleetState objects.
    /// Extracts and formats fleet information for accessibility.
    /// </summary>
    public class FleetReader : IGameStateReader<TISpaceFleetState>
    {
        /// <summary>
        /// Callback for entering transfer planning mode.
        /// </summary>
        public Action<TISpaceFleetState> OnEnterTransferMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for executing a simple fleet operation (undock, cancel, clear homeport, merge all).
        /// Parameters: fleet, operation type
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteSimpleOperation { get; set; }

        /// <summary>
        /// Callback for selecting a homeport from available habs.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectHomeport { get; set; }

        /// <summary>
        /// Callback for selecting a fleet to merge with.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectMergeTarget { get; set; }

        /// <summary>
        /// Callback for executing a maintenance operation (resupply, repair).
        /// Parameters: fleet, operation type
        /// </summary>
        public Action<TISpaceFleetState, Type> OnExecuteMaintenanceOperation { get; set; }

        /// <summary>
        /// Callback for selecting a landing site (hab site) for landing operation.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectLandingSite { get; set; }

        /// <summary>
        /// Callback for selecting a launch orbit for launch from surface operation.
        /// </summary>
        public Action<TISpaceFleetState> OnSelectLaunchOrbit { get; set; }

        public string ReadSummary(TISpaceFleetState fleet)
        {
            if (fleet == null)
                return "Unknown fleet";

            var sb = new StringBuilder();
            sb.Append(fleet.displayName ?? "Unknown Fleet");

            // Ship count
            int shipCount = fleet.ships?.Count ?? 0;
            sb.Append($", {shipCount} ship{(shipCount != 1 ? "s" : "")}");

            // Location/status
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                if (dockedAt != null)
                {
                    string locationName = dockedAt.ref_hab?.displayName ?? dockedAt.ref_habSite?.displayName ?? "unknown";
                    sb.Append(fleet.landed ? $", landed at {locationName}" : $", docked at {locationName}");
                }
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                if (destination != null)
                {
                    sb.Append($", in transit to {destination.displayName}");
                    // Add arrival time
                    var arrivalTime = fleet.trajectory?.finalArrivalTime;
                    if (arrivalTime != null)
                    {
                        sb.Append($", ETA {arrivalTime.ToCustomTimeDateString()}");
                    }
                }
                else
                {
                    sb.Append(", in transit");
                }
            }
            else if (fleet.inCombat)
            {
                sb.Append(", in combat");
            }
            else
            {
                // In orbit
                var body = fleet.ref_spaceBody;
                if (body != null)
                {
                    sb.Append($", orbiting {body.displayName}");
                }
            }

            return sb.ToString();
        }

        public string ReadDetail(TISpaceFleetState fleet)
        {
            if (fleet == null)
                return "Unknown fleet";

            var sb = new StringBuilder();
            sb.AppendLine($"Fleet: {fleet.displayName}");

            // Location details
            sb.AppendLine();
            sb.AppendLine("Location:");
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                if (dockedAt != null)
                {
                    string locationName = dockedAt.ref_hab?.displayName ?? dockedAt.ref_habSite?.displayName ?? "unknown location";
                    sb.AppendLine(fleet.landed ? $"  Landed at {locationName}" : $"  Docked at {locationName}");
                }
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                sb.AppendLine($"  In transit to: {destination?.displayName ?? "unknown"}");
                var arrivalTime = fleet.trajectory?.finalArrivalTime;
                if (arrivalTime != null)
                {
                    sb.AppendLine($"  Arrival: {arrivalTime.ToCustomTimeDateString()}");
                }
            }
            else
            {
                var body = fleet.ref_spaceBody;
                sb.AppendLine($"  Orbiting: {body?.displayName ?? "unknown"}");
                sb.AppendLine($"  Altitude: {fleet.altitude_km:N0} km");
            }

            // Combat status
            if (fleet.inCombat)
            {
                sb.AppendLine("  STATUS: IN COMBAT");
            }
            else if (fleet.waitingForCombat)
            {
                sb.AppendLine("  STATUS: Combat pending");
            }

            // Combat value
            try
            {
                float combatValue = fleet.SpaceCombatValue();
                sb.AppendLine($"Combat Value: {combatValue:N0}");
            }
            catch { }

            // Ships
            sb.AppendLine();
            int shipCount = fleet.ships?.Count ?? 0;
            sb.AppendLine($"Ships: {shipCount}");
            if (fleet.ships != null && fleet.ships.Count > 0)
            {
                // Group by class
                var shipsByClass = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown Class");
                foreach (var group in shipsByClass)
                {
                    sb.AppendLine($"  {group.Count()}x {group.Key}");
                }
            }

            // Delta-V and propellant
            sb.AppendLine();
            sb.AppendLine("Performance:");
            try
            {
                float currentDV = fleet.currentDeltaV_kps;
                float maxDV = fleet.maxDeltaV_kps;
                sb.AppendLine($"  Delta-V: {currentDV:F1} / {maxDV:F1} km/s");
            }
            catch { }

            try
            {
                float cruiseAccel = fleet.cruiseAcceleration_gs * 1000f; // Convert to milligees
                sb.AppendLine($"  Cruise Acceleration: {cruiseAccel:F1} mg");
            }
            catch { }

            // Homeport
            if (fleet.homeport != null)
            {
                sb.AppendLine($"  Homeport: {fleet.homeport.displayName}");
            }

            // Current operations
            if (fleet.currentOperations != null && fleet.currentOperations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Active Operations:");
                foreach (var op in fleet.currentOperations)
                {
                    if (op.operation != null)
                    {
                        sb.AppendLine($"  {op.operation.GetDisplayName()}");
                    }
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TISpaceFleetState fleet)
        {
            var sections = new List<ISection>();
            if (fleet == null)
                return sections;

            // Status section
            sections.Add(CreateStatusSection(fleet));

            // Ships section
            if (fleet.ships != null && fleet.ships.Count > 0)
            {
                sections.Add(CreateShipsSection(fleet));
            }

            // Performance section
            sections.Add(CreatePerformanceSection(fleet));

            // Operations section
            sections.Add(CreateOperationsSection(fleet));

            // Transfer planning section (only for player's own fleets that can transfer)
            var playerFaction = GameControl.control?.activePlayer;
            if (playerFaction != null && fleet.faction == playerFaction)
            {
                var transferSection = CreateTransferSection(fleet);
                if (transferSection != null)
                {
                    sections.Add(transferSection);
                }

                // Fleet Management section (Undock, Merge, Cancel)
                var mgmtSection = CreateFleetManagementSection(fleet);
                if (mgmtSection != null)
                {
                    sections.Add(mgmtSection);
                }

                // Homeport section
                var homeportSection = CreateHomeportSection(fleet);
                if (homeportSection != null)
                {
                    sections.Add(homeportSection);
                }

                // Maintenance section (Resupply, Repair)
                var maintenanceSection = CreateMaintenanceSection(fleet);
                if (maintenanceSection != null)
                {
                    sections.Add(maintenanceSection);
                }
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateStatusSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Status");

            // Location
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                string locationName = dockedAt?.ref_hab?.displayName ?? dockedAt?.ref_habSite?.displayName ?? "unknown";
                section.AddItem("Location", fleet.landed ? $"Landed at {locationName}" : $"Docked at {locationName}");
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                section.AddItem("Location", $"In transit to {destination?.displayName ?? "unknown"}");
                var arrivalTime = fleet.trajectory?.finalArrivalTime;
                if (arrivalTime != null)
                {
                    section.AddItem("Arrival", arrivalTime.ToCustomTimeDateString());
                }
            }
            else
            {
                var body = fleet.ref_spaceBody;
                section.AddItem("Location", $"Orbiting {body?.displayName ?? "unknown"}");
                section.AddItem("Altitude", $"{fleet.altitude_km:N0} km");
            }

            // Combat status
            if (fleet.inCombat)
            {
                section.AddItem("Combat", "IN COMBAT");
            }
            else if (fleet.waitingForCombat)
            {
                section.AddItem("Combat", "Pending");
            }
            else if (fleet.bombarding)
            {
                section.AddItem("Status", "Bombarding");
            }
            else
            {
                section.AddItem("Combat", "None");
            }

            // Homeport
            if (fleet.homeport != null)
            {
                section.AddItem("Homeport", fleet.homeport.displayName);
            }

            return section;
        }

        private ISection CreateShipsSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Ships");

            if (fleet.ships == null || fleet.ships.Count == 0)
            {
                section.AddItem("Ships", "None");
                return section;
            }

            // Group by class and show count
            var shipsByClass = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown");
            foreach (var group in shipsByClass.OrderByDescending(g => g.Count()))
            {
                string className = group.Key;
                int count = group.Count();

                // Get first ship for details and calculate total combat value for this class
                var sample = group.First();
                string hullType = sample.hull?.displayName ?? "";
                float classCombatValue = group.Sum(s => s.SpaceCombatValue());

                string detail = $"CV {classCombatValue:N0}";
                if (!string.IsNullOrEmpty(hullType))
                {
                    detail = $"{hullType}, {detail}";
                }

                section.AddItem($"{count}x {className}", detail);
            }

            // Summary
            int small = fleet.smallShips?.Count ?? 0;
            int medium = fleet.mediumShips?.Count ?? 0;
            int large = fleet.largeShips?.Count ?? 0;

            if (small > 0 || medium > 0 || large > 0)
            {
                var sizes = new List<string>();
                if (large > 0) sizes.Add($"{large} large");
                if (medium > 0) sizes.Add($"{medium} medium");
                if (small > 0) sizes.Add($"{small} small");
                section.AddItem("By Size", string.Join(", ", sizes));
            }

            // Special capabilities based on ship roles
            var capabilities = GetFleetCapabilities(fleet);
            if (capabilities.Count > 0)
            {
                section.AddItem("Capabilities", string.Join(", ", capabilities));
            }

            return section;
        }

        /// <summary>
        /// Get list of special capabilities based on ship roles in the fleet.
        /// </summary>
        private List<string> GetFleetCapabilities(TISpaceFleetState fleet)
        {
            var capabilities = new List<string>();
            if (fleet.ships == null) return capabilities;

            // Check for special utility roles
            if (fleet.ships.Any(s => s.role == ShipRole.CouncilorTransport))
                capabilities.Add("Councilor transport");
            if (fleet.ships.Any(s => s.role == ShipRole.TroopCarrier))
                capabilities.Add("Troop carrier");
            if (fleet.ships.Any(s => s.role == ShipRole.ArmyCarrier))
                capabilities.Add("Army carrier");
            if (fleet.ships.Any(s => s.role == ShipRole.Explorer))
                capabilities.Add("Explorer");
            if (fleet.ships.Any(s => s.role == ShipRole.InnerSystemColonyShip))
                capabilities.Add("Inner system colony");
            if (fleet.ships.Any(s => s.role == ShipRole.OuterSystemColonyShip))
                capabilities.Add("Outer system colony");
            if (fleet.ships.Any(s => s.role == ShipRole.EarthSurveillance))
                capabilities.Add("Earth surveillance");

            return capabilities;
        }

        private ISection CreatePerformanceSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Performance");

            // Combat value - important for comparing fleet strength
            try
            {
                float combatValue = fleet.SpaceCombatValue();
                string combatTooltip = GetLocString("UI.Fleets.SpaceCombatValueTab.Description");
                section.AddItem("Combat Value", $"{combatValue:N0}", combatTooltip);
            }
            catch { }

            try
            {
                float currentDV = fleet.currentDeltaV_kps;
                float maxDV = fleet.maxDeltaV_kps;
                string dvTooltip = GetLocString("UI.Fleets.CruiseDeltaVTab.Description");
                section.AddItem("Delta-V", $"{currentDV:F1} / {maxDV:F1} km/s", dvTooltip);
            }
            catch
            {
                section.AddItem("Delta-V", "Unknown");
            }

            try
            {
                float cruiseAccel = fleet.cruiseAcceleration_gs * 1000f;
                string cruiseTooltip = GetLocString("UI.Fleets.CruiseAccelerationTab.Description");
                section.AddItem("Cruise Accel", $"{cruiseAccel:F1} milligees", cruiseTooltip);
            }
            catch { }

            try
            {
                float maxAccel = fleet.maxAcceleration_gs * 1000f;
                string combatAccelTooltip = GetLocString("UI.Fleets.CombatAccelerationTab.Description");
                section.AddItem("Combat Accel", $"{maxAccel:F1} milligees", combatAccelTooltip);
            }
            catch { }

            try
            {
                double mass = fleet.mass_kg / 1000.0; // Convert to tons
                string massTooltip = GetLocString("UI.Fleets.MassTab.Description");
                section.AddItem("Mass", $"{mass:N0} tons", massTooltip);
            }
            catch { }

            return section;
        }

        /// <summary>
        /// Safely get a localized string and clean it for screen reader output.
        /// </summary>
        private string GetLocString(string key)
        {
            try
            {
                string text = Loc.T(key);
                return TISpeechMod.CleanText(text);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not get localization for {key}: {ex.Message}");
                return "";
            }
        }

        private ISection CreateOperationsSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Operations");

            // Current operations
            if (fleet.currentOperations != null && fleet.currentOperations.Count > 0)
            {
                foreach (var op in fleet.currentOperations)
                {
                    if (op.operation != null)
                    {
                        string target = op.target?.displayName ?? "";
                        section.AddItem(op.operation.GetDisplayName(), target);
                    }
                }
            }
            else
            {
                section.AddItem("Current", "None");
            }

            // Availability
            if (fleet.unavailableForOperations)
            {
                section.AddItem("Availability", "Unavailable");
            }
            else if (fleet.dockedOrLanded)
            {
                section.AddItem("Availability", "Ready (docked)");
            }
            else if (fleet.inTransfer)
            {
                section.AddItem("Availability", "In transit");
            }
            else
            {
                section.AddItem("Availability", "Ready");
            }

            return section;
        }

        /// <summary>
        /// Create the Transfer Planning section for eligible fleets.
        /// </summary>
        private ISection CreateTransferSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Transfer");

            // Check transfer eligibility
            bool canTransfer = CanPlanTransfer(fleet, out string blockedReason);

            if (fleet.inTransfer)
            {
                // Show current transfer info
                var traj = fleet.trajectory;
                if (traj != null)
                {
                    section.AddItem("Status", "Transfer in progress");
                    section.AddItem("Destination", traj.destination?.displayName ?? "Unknown");

                    if (traj.arrivalTime != null)
                    {
                        section.AddItem("Arrival", traj.arrivalTime.ToCustomTimeDateString());
                    }

                    section.AddItem("Delta-V Used", $"{traj.DV_kps:F1} km/s");
                }
                return section;
            }

            if (!canTransfer)
            {
                section.AddItem("Status", blockedReason);
                return section;
            }

            // Fleet can transfer - show relevant info and action
            section.AddItem("Status", "Ready for transfer");
            section.AddItem("Available Delta-V", $"{fleet.currentDeltaV_kps:F1} / {fleet.maxDeltaV_kps:F1} km/s");
            section.AddItem("Cruise Acceleration", $"{fleet.cruiseAcceleration_gs * 1000:F0} milligees");

            // Current orbit info
            if (fleet.orbitState != null)
            {
                section.AddItem("Current Orbit", fleet.orbitState.displayName);
                section.AddItem("Around", fleet.ref_spaceBody?.displayName ?? "Unknown");
            }

            // Add the "Plan Transfer" action
            var fleetCopy = fleet; // Capture for closure
            section.AddItem("Plan Transfer", "Open transfer planning wizard",
                onActivate: () => OnEnterTransferMode?.Invoke(fleetCopy));

            return section;
        }

        /// <summary>
        /// Check if a fleet can have a transfer planned.
        /// </summary>
        private bool CanPlanTransfer(TISpaceFleetState fleet, out string blockedReason)
        {
            blockedReason = null;

            if (fleet == null)
            {
                blockedReason = "Invalid fleet";
                return false;
            }

            if (fleet.inTransfer)
            {
                blockedReason = "Already has transfer assigned";
                return false;
            }

            if (fleet.dockedOrLanded)
            {
                blockedReason = "Must undock first";
                return false;
            }

            if (fleet.inCombat || fleet.waitingForCombat)
            {
                blockedReason = "In combat";
                return false;
            }

            if (fleet.unavailableForOperations)
            {
                blockedReason = "Unavailable for operations";
                return false;
            }

            if (fleet.currentDeltaV_kps <= 0)
            {
                blockedReason = "No delta-V available";
                return false;
            }

            if (fleet.cruiseAcceleration_gs <= 0)
            {
                blockedReason = "No propulsion available";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create the Fleet Management section with Undock, Merge, and Cancel operations.
        /// </summary>
        private ISection CreateFleetManagementSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Fleet Management");
            var fleetCopy = fleet; // Capture for closures

            try
            {
                // Undock From Station
                if (fleet.dockedAtStation)
                {
                    var undockOp = GetFleetOperation<UndockFromStationOperation>();
                    if (undockOp != null && undockOp.OpVisibleToActor(fleet))
                    {
                        bool canUndock = undockOp.ActorCanPerformOperation(fleet, fleet);
                        string status = canUndock ? "Undock from current station" : GetUndockBlockedReason(fleet);

                        section.AddItem("Undock From Station", status,
                            onActivate: canUndock ? () => OnExecuteSimpleOperation?.Invoke(fleetCopy, typeof(UndockFromStationOperation)) : null);
                    }
                }

                // Launch From Surface - when fleet is landed
                if (fleet.landed)
                {
                    var launchOp = GetFleetOperation<LaunchFromSurfaceOperation>();
                    if (launchOp != null && launchOp.OpVisibleToActor(fleet))
                    {
                        var launchTargets = launchOp.GetPossibleTargets(fleet);
                        bool canLaunch = launchOp.ActorCanPerformOperation(fleet, fleet) && launchTargets.Count > 0;
                        string status = canLaunch
                            ? $"{launchTargets.Count} orbit{(launchTargets.Count != 1 ? "s" : "")} available"
                            : GetLaunchBlockedReason(fleet, launchOp);

                        section.AddItem("Launch From Surface", status,
                            onActivate: canLaunch ? () => OnSelectLaunchOrbit?.Invoke(fleetCopy) : null);
                    }
                }

                // Land On Surface - when fleet is in orbit (not landed, not docked)
                if (!fleet.landed && !fleet.dockedAtHab)
                {
                    var landOp = GetFleetOperation<LandOnSurfaceOperation>();
                    if (landOp != null && landOp.OpVisibleToActor(fleet))
                    {
                        var landTargets = landOp.GetPossibleTargets(fleet);
                        bool canLand = landTargets.Count > 0 && landOp.ActorCanPerformOperation(fleet, fleet);
                        string status = canLand
                            ? $"{landTargets.Count} site{(landTargets.Count != 1 ? "s" : "")} available"
                            : GetLandBlockedReason(fleet, landOp);

                        section.AddItem("Land On Surface", status,
                            onActivate: canLand ? () => OnSelectLandingSite?.Invoke(fleetCopy) : null);
                    }
                }

                // Merge Fleet - find other fleets we can merge with
                var mergeOp = GetFleetOperation<MergeFleetOperation>();
                if (mergeOp != null && mergeOp.OpVisibleToActor(fleet))
                {
                    var mergeableFleets = GetMergeableFleets(fleet);
                    if (mergeableFleets.Count > 0)
                    {
                        bool canMerge = !fleet.inCombatOrWaitingForCombat && mergeableFleets.Count > 0;
                        string mergeStatus = canMerge
                            ? $"{mergeableFleets.Count} fleet{(mergeableFleets.Count != 1 ? "s" : "")} available to merge"
                            : "No fleets available to merge";

                        section.AddItem("Merge Fleet", mergeStatus,
                            onActivate: canMerge ? () => OnSelectMergeTarget?.Invoke(fleetCopy) : null);
                    }

                    // Merge All Fleets - if multiple fleets at same location
                    if (mergeableFleets.Count > 1)
                    {
                        var mergeAllOp = GetFleetOperation<MergeAllFleetOperation>();
                        if (mergeAllOp != null)
                        {
                            section.AddItem("Merge All Fleets", $"Merge all {mergeableFleets.Count + 1} fleets at this location",
                                onActivate: () => OnExecuteSimpleOperation?.Invoke(fleetCopy, typeof(MergeAllFleetOperation)));
                        }
                    }
                }

                // Cancel Operation - if fleet has active operations
                if (fleet.currentOperations != null && fleet.currentOperations.Count > 0)
                {
                    var cancelOp = GetFleetOperation<CancelFleetOperation>();
                    if (cancelOp != null)
                    {
                        var currentOp = fleet.currentOperations.FirstOrDefault();
                        string opName = currentOp?.operation?.GetDisplayName() ?? "current operation";
                        section.AddItem("Cancel Operation", $"Cancel {opName}",
                            onActivate: () => OnExecuteSimpleOperation?.Invoke(fleetCopy, typeof(CancelFleetOperation)));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Fleet Management section: {ex.Message}");
            }

            // Only return section if it has items
            return section.ItemCount > 0 ? section : null;
        }

        /// <summary>
        /// Create the Homeport section for setting/clearing fleet homeport.
        /// </summary>
        private ISection CreateHomeportSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Homeport");
            var fleetCopy = fleet; // Capture for closures

            try
            {
                // Current homeport
                string currentHomeport = fleet.homeport?.displayName ?? "None";
                section.AddItem("Current", currentHomeport);

                // Set Homeport - select from faction's habs
                var setHomeportOp = GetFleetOperation<SetHomeportOperation>();
                if (setHomeportOp != null)
                {
                    var availableHabs = GetHomeportOptions(fleet);
                    if (availableHabs.Count > 0)
                    {
                        section.AddItem("Set Homeport", $"{availableHabs.Count} station{(availableHabs.Count != 1 ? "s" : "")} available",
                            onActivate: () => OnSelectHomeport?.Invoke(fleetCopy));
                    }
                    else
                    {
                        section.AddItem("Set Homeport", "No stations available");
                    }
                }

                // Clear Homeport - only if one is set
                if (fleet.homeport != null)
                {
                    var clearHomeportOp = GetFleetOperation<ClearHomeportOperation>();
                    if (clearHomeportOp != null)
                    {
                        section.AddItem("Clear Homeport", $"Remove {fleet.homeport.displayName} as homeport",
                            onActivate: () => OnExecuteSimpleOperation?.Invoke(fleetCopy, typeof(ClearHomeportOperation)));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Homeport section: {ex.Message}");
            }

            return section;
        }

        /// <summary>
        /// Create the Maintenance section for Resupply and Repair operations.
        /// </summary>
        private ISection CreateMaintenanceSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Maintenance");
            var fleetCopy = fleet; // Capture for closures

            try
            {
                bool needsRefuel = fleet.NeedsRefuel();
                bool needsRearm = fleet.NeedsRearm();
                bool needsRepair = fleet.NeedsRepair();

                // If nothing needed, just show status
                if (!needsRefuel && !needsRearm && !needsRepair)
                {
                    section.AddItem("Status", "Fleet is fully supplied and repaired");
                    return section;
                }

                // Show what's needed
                var needs = new List<string>();
                if (needsRefuel) needs.Add("refuel");
                if (needsRearm) needs.Add("rearm");
                if (needsRepair) needs.Add("repair");
                section.AddItem("Needs", string.Join(", ", needs));

                // Check if docked (required for maintenance)
                if (!fleet.dockedAtHab)
                {
                    section.AddItem("Status", "Must dock at a station for maintenance");
                    return section;
                }

                // Resupply operation (refuel + rearm)
                if (needsRefuel || needsRearm)
                {
                    var resupplyOp = GetFleetOperation<ResupplyOperation>();
                    if (resupplyOp != null && resupplyOp.OpVisibleToActor(fleet))
                    {
                        bool canResupply = resupplyOp.ActorCanPerformOperation(fleet, fleet);
                        string costStr = GetOperationCostString(fleet, resupplyOp);
                        string status = canResupply ? costStr : "Cannot resupply here";

                        section.AddItem("Resupply", status,
                            onActivate: canResupply ? () => OnExecuteMaintenanceOperation?.Invoke(fleetCopy, typeof(ResupplyOperation)) : null);
                    }
                }

                // Repair operation
                if (needsRepair)
                {
                    var repairOp = GetFleetOperation<RepairFleetOperation>();
                    if (repairOp != null && repairOp.OpVisibleToActor(fleet))
                    {
                        bool canRepair = repairOp.ActorCanPerformOperation(fleet, fleet);
                        string costStr = GetOperationCostString(fleet, repairOp);
                        string status = canRepair ? costStr : "Cannot repair here";

                        // Check if hab can fully repair
                        if (canRepair && fleet.ref_hab != null)
                        {
                            if (!fleet.ref_hab.CanFullyRepairFleet(fleet))
                            {
                                status += " (partial repair only)";
                            }
                        }

                        section.AddItem("Repair", status,
                            onActivate: canRepair ? () => OnExecuteMaintenanceOperation?.Invoke(fleetCopy, typeof(RepairFleetOperation)) : null);
                    }
                }

                // Resupply & Repair combined (if both needed)
                if ((needsRefuel || needsRearm) && needsRepair)
                {
                    var combinedOp = GetFleetOperation<ResupplyAndRepairOperation>();
                    if (combinedOp != null && combinedOp.OpVisibleToActor(fleet))
                    {
                        bool canDoBoth = combinedOp.ActorCanPerformOperation(fleet, fleet);
                        string costStr = GetOperationCostString(fleet, combinedOp);
                        string status = canDoBoth ? costStr : "Cannot do both here";

                        section.AddItem("Resupply & Repair", status,
                            onActivate: canDoBoth ? () => OnExecuteMaintenanceOperation?.Invoke(fleetCopy, typeof(ResupplyAndRepairOperation)) : null);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating Maintenance section: {ex.Message}");
            }

            return section.ItemCount > 0 ? section : null;
        }

        /// <summary>
        /// Get a formatted cost string for an operation.
        /// </summary>
        private string GetOperationCostString(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation)
        {
            try
            {
                var costs = operation.ResourceCostOptions(fleet.faction, fleet, fleet, checkCanAfford: false);
                if (costs != null && costs.Count > 0 && costs[0].anyDebit)
                {
                    // Get the duration
                    float duration = costs[0].completionTime_days;
                    string durationStr = duration > 0 ? $", {duration:F1} days" : "";

                    // Get cost string - use the game's formatting
                    string costStr = costs[0].ToString("Relevant", false, false, fleet.faction);
                    costStr = TISpeechMod.CleanText(costStr);

                    return $"{costStr}{durationStr}";
                }
                return "No cost";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting operation cost: {ex.Message}");
                return "Cost unknown";
            }
        }

        /// <summary>
        /// Get a fleet operation template by type.
        /// </summary>
        private T GetFleetOperation<T>() where T : TISpaceFleetOperationTemplate
        {
            try
            {
                if (OperationsManager.operationsLookup.TryGetValue(typeof(T), out var op))
                {
                    return op as T;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not get operation {typeof(T).Name}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the reason why a fleet cannot undock.
        /// </summary>
        private string GetUndockBlockedReason(TISpaceFleetState fleet)
        {
            if (!fleet.dockedAtStation)
                return "Not docked at a station";
            if (fleet.inCombatOrWaitingForCombat)
                return "In combat";
            if (fleet.transferAssigned)
                return "Transfer already assigned";
            if (!fleet.allShipsHaveDeltaV)
                return "Some ships have no delta-V";
            if (!fleet.allShipsCanManeuver)
                return "Some ships cannot maneuver";
            return "Cannot undock";
        }

        /// <summary>
        /// Get the reason why a fleet cannot launch from surface.
        /// </summary>
        private string GetLaunchBlockedReason(TISpaceFleetState fleet, LaunchFromSurfaceOperation op)
        {
            if (!fleet.landed)
                return "Not on surface";
            if (fleet.inCombatOrWaitingForCombat)
                return "In combat";

            // Check if fleet has enough thrust to escape gravity
            try
            {
                var habSite = fleet.dockedLocation?.ref_habSite;
                if (habSite != null)
                {
                    double surfaceGravity = habSite.parentBody.surfaceGravity_g;
                    if (fleet.maxAcceleration_gs < surfaceGravity)
                        return $"Insufficient thrust ({fleet.maxAcceleration_gs:F2}g vs {surfaceGravity:F2}g gravity)";
                }
            }
            catch { }

            // Check delta-V
            var targets = op.GetPossibleTargets(fleet);
            if (targets.Count == 0)
                return "Insufficient delta-V for any orbit";

            return "Cannot launch";
        }

        /// <summary>
        /// Get the reason why a fleet cannot land on surface.
        /// </summary>
        private string GetLandBlockedReason(TISpaceFleetState fleet, LandOnSurfaceOperation op)
        {
            if (fleet.landed)
                return "Already on surface";
            if (fleet.dockedAtHab)
                return "Currently docked";
            if (fleet.transferAssigned)
                return "Transfer assigned";
            if (fleet.inCombatOrWaitingForCombat)
                return "In combat";

            // Check if in interface orbit
            if (fleet.orbitState == null || !fleet.orbitState.interfaceOrbit)
                return "Not in interface orbit";

            // Check available targets
            var targets = op.GetPossibleTargets(fleet);
            if (targets.Count == 0)
                return "No landing sites available";

            return "Cannot land";
        }

        /// <summary>
        /// Get list of fleets that can be merged with this fleet.
        /// </summary>
        public static List<TISpaceFleetState> GetMergeableFleets(TISpaceFleetState fleet)
        {
            var result = new List<TISpaceFleetState>();
            if (fleet?.faction == null)
                return result;

            try
            {
                foreach (var otherFleet in fleet.faction.fleets)
                {
                    if (otherFleet != fleet && fleet.CanMerge(otherFleet))
                    {
                        result.Add(otherFleet);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting mergeable fleets: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get list of habs that can be set as homeport.
        /// </summary>
        public static List<TIHabState> GetHomeportOptions(TISpaceFleetState fleet)
        {
            var result = new List<TIHabState>();
            if (fleet?.faction == null)
                return result;

            try
            {
                // Faction's own habs
                if (fleet.faction.habs != null)
                {
                    result.AddRange(fleet.faction.habs);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting homeport options: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all fleets belonging to the player's faction.
        /// </summary>
        public static List<TISpaceFleetState> GetPlayerFleets(TIFactionState faction)
        {
            if (faction == null)
                return new List<TISpaceFleetState>();

            try
            {
                return GameStateManager.IterateByClass<TISpaceFleetState>()
                    .Where(f => f.faction == faction && !f.archived && !f.dummyFleet)
                    .OrderBy(f => f.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player fleets: {ex.Message}");
                return new List<TISpaceFleetState>();
            }
        }

        /// <summary>
        /// Get all known enemy fleets (based on intel).
        /// </summary>
        public static List<TISpaceFleetState> GetKnownEnemyFleets(TIFactionState viewer)
        {
            if (viewer == null)
                return new List<TISpaceFleetState>();

            try
            {
                return GameStateManager.IterateByClass<TISpaceFleetState>()
                    .Where(f => f.faction != viewer &&
                                !f.archived &&
                                !f.dummyFleet &&
                                viewer.GetIntel(f) > 0)
                    .OrderBy(f => f.faction?.displayName ?? "")
                    .ThenBy(f => f.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting enemy fleets: {ex.Message}");
                return new List<TISpaceFleetState>();
            }
        }

        #endregion
    }
}
