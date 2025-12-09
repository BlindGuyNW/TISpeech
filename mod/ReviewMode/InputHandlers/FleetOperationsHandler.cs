using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Readers;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Handles fleet operations initiated from Review Mode.
    /// </summary>
    public class FleetOperationsHandler
    {
        private readonly Action<string, List<SelectionOption>, Action<int>> enterSelectionMode;
        private readonly Action refreshNavigation;

        public FleetOperationsHandler(
            Action<string, List<SelectionOption>, Action<int>> enterSelectionMode,
            Action refreshNavigation)
        {
            this.enterSelectionMode = enterSelectionMode;
            this.refreshNavigation = refreshNavigation;
        }

        /// <summary>
        /// Execute a simple fleet operation (undock, cancel, clear homeport, merge all).
        /// </summary>
        public void ExecuteSimpleOperation(TISpaceFleetState fleet, Type operationType)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                if (!OperationsManager.operationsLookup.TryGetValue(operationType, out var op))
                {
                    TISpeechMod.Speak("Operation not found", interrupt: true);
                    return;
                }

                var fleetOp = op as TISpaceFleetOperationTemplate;
                if (fleetOp == null)
                {
                    TISpeechMod.Speak("Invalid operation type", interrupt: true);
                    return;
                }

                string opName = fleetOp.GetDisplayName();

                // Handle different operation types
                if (operationType == typeof(UndockFromStationOperation))
                {
                    ExecuteUndockOperation(fleet, fleetOp);
                }
                else if (operationType == typeof(ClearHomeportOperation))
                {
                    ExecuteClearHomeportOperation(fleet, fleetOp);
                }
                else if (operationType == typeof(MergeAllFleetOperation))
                {
                    ExecuteMergeAllOperation(fleet, fleetOp);
                }
                else if (operationType == typeof(CancelFleetOperation))
                {
                    ExecuteCancelOperation(fleet, fleetOp);
                }
                else
                {
                    TISpeechMod.Speak($"Unhandled operation: {opName}", interrupt: true);
                }

                // Refresh navigation state after operation
                refreshNavigation?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing fleet operation: {ex.Message}");
                TISpeechMod.Speak("Error executing operation", interrupt: true);
            }
        }

        private void ExecuteUndockOperation(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation)
        {
            if (!fleet.dockedAtStation)
            {
                TISpeechMod.Speak("Fleet is not docked at a station", interrupt: true);
                return;
            }

            if (!operation.ActorCanPerformOperation(fleet, fleet))
            {
                TISpeechMod.Speak("Cannot undock at this time", interrupt: true);
                return;
            }

            // Get target orbit (the orbit we'll be in after undocking)
            var targets = operation.GetPossibleTargets(fleet);
            if (targets == null || targets.Count == 0)
            {
                TISpeechMod.Speak("Cannot determine undock destination", interrupt: true);
                return;
            }

            var targetOrbit = targets[0];
            operation.OnOperationConfirm(fleet, targetOrbit, null, null);
            TISpeechMod.Speak($"{fleet.displayName} undocking from station", interrupt: true);
            MelonLogger.Msg($"Fleet {fleet.displayName} undocking");
        }

        private void ExecuteClearHomeportOperation(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation)
        {
            if (fleet.homeport == null)
            {
                TISpeechMod.Speak("Fleet has no homeport set", interrupt: true);
                return;
            }

            string oldHomeport = fleet.homeport.displayName;
            // Use OnOperationConfirm for consistency with other operations
            operation.OnOperationConfirm(fleet, fleet, null, null);
            TISpeechMod.Speak($"Cleared homeport. {fleet.displayName} no longer assigned to {oldHomeport}", interrupt: true);
            MelonLogger.Msg($"Cleared homeport for {fleet.displayName}");
        }

        private void ExecuteMergeAllOperation(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation)
        {
            var mergeableFleets = FleetReader.GetMergeableFleets(fleet);
            if (mergeableFleets.Count == 0)
            {
                TISpeechMod.Speak("No fleets available to merge", interrupt: true);
                return;
            }

            // Merge all fleets at the same location into this fleet
            int mergedCount = 0;
            foreach (var otherFleet in mergeableFleets.ToList())
            {
                if (fleet.CanMerge(otherFleet))
                {
                    var mergeOp = OperationsManager.operationsLookup[typeof(MergeFleetOperation)] as TISpaceFleetOperationTemplate;
                    if (mergeOp != null)
                    {
                        mergeOp.OnOperationConfirm(fleet, otherFleet, null, null);
                        mergedCount++;
                    }
                }
            }

            TISpeechMod.Speak($"Merged {mergedCount} fleet{(mergedCount != 1 ? "s" : "")} into {fleet.displayName}", interrupt: true);
            MelonLogger.Msg($"Merged {mergedCount} fleets into {fleet.displayName}");
        }

        private void ExecuteCancelOperation(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation)
        {
            var currentOps = fleet.CurrentOperations();
            if (currentOps == null || currentOps.Count == 0)
            {
                TISpeechMod.Speak("Fleet has no active operations to cancel", interrupt: true);
                return;
            }

            // Find a cancellable operation
            var cancellableOp = currentOps.FirstOrDefault(x => (x.operation as TISpaceFleetOperationTemplate)?.CanCancel() == true);
            if (cancellableOp?.operation == null)
            {
                TISpeechMod.Speak("No cancellable operations", interrupt: true);
                return;
            }

            string opName = cancellableOp.operation.GetDisplayName();

            // Use the fleet's CancelOperation method which handles everything properly
            fleet.CancelOperation(cancellableOp);

            TISpeechMod.Speak($"Cancelled {opName}", interrupt: true);
            MelonLogger.Msg($"Cancelled {opName} for {fleet.displayName}");
        }

        /// <summary>
        /// Open selection mode to choose a homeport for a fleet.
        /// </summary>
        public void SelectHomeportForFleet(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            var habs = FleetReader.GetHomeportOptions(fleet);
            if (habs.Count == 0)
            {
                TISpeechMod.Speak("No stations available for homeport", interrupt: true);
                return;
            }

            // Build selection options
            var options = habs.Select(hab => new SelectionOption
            {
                Label = hab.displayName,
                DetailText = $"At {hab.ref_spaceBody?.displayName ?? "unknown location"}",
                Data = hab
            }).ToList();

            enterSelectionMode(
                $"Select homeport for {fleet.displayName}",
                options,
                selectedIndex =>
                {
                    if (selectedIndex >= 0 && selectedIndex < habs.Count)
                    {
                        var selectedHab = habs[selectedIndex];
                        var setHomeportOp = OperationsManager.operationsLookup[typeof(SetHomeportOperation)] as TISpaceFleetOperationTemplate;
                        if (setHomeportOp != null)
                        {
                            // Use OnOperationConfirm for consistency with other operations
                            setHomeportOp.OnOperationConfirm(fleet, selectedHab, null, null);
                            TISpeechMod.Speak($"Homeport set to {selectedHab.displayName}", interrupt: true);
                            MelonLogger.Msg($"Set homeport for {fleet.displayName} to {selectedHab.displayName}");
                            refreshNavigation?.Invoke();
                        }
                    }
                }
            );
        }

        /// <summary>
        /// Open selection mode to choose a fleet to merge with.
        /// </summary>
        public void SelectMergeTargetForFleet(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            var mergeableFleets = FleetReader.GetMergeableFleets(fleet);
            if (mergeableFleets.Count == 0)
            {
                TISpeechMod.Speak("No fleets available to merge with", interrupt: true);
                return;
            }

            // Build selection options
            var options = mergeableFleets.Select(f => new SelectionOption
            {
                Label = f.displayName,
                DetailText = $"{f.ships?.Count ?? 0} ship{((f.ships?.Count ?? 0) != 1 ? "s" : "")}",
                Data = f
            }).ToList();

            enterSelectionMode(
                $"Select fleet to merge into {fleet.displayName}",
                options,
                selectedIndex =>
                {
                    if (selectedIndex >= 0 && selectedIndex < mergeableFleets.Count)
                    {
                        var targetFleet = mergeableFleets[selectedIndex];
                        var mergeOp = OperationsManager.operationsLookup[typeof(MergeFleetOperation)] as TISpaceFleetOperationTemplate;
                        if (mergeOp != null && fleet.CanMerge(targetFleet))
                        {
                            int shipsAdded = targetFleet.ships?.Count ?? 0;
                            mergeOp.OnOperationConfirm(fleet, targetFleet, null, null);
                            TISpeechMod.Speak($"Merged {targetFleet.displayName} into {fleet.displayName}. Added {shipsAdded} ship{(shipsAdded != 1 ? "s" : "")}", interrupt: true);
                            MelonLogger.Msg($"Merged {targetFleet.displayName} into {fleet.displayName}");
                            refreshNavigation?.Invoke();
                        }
                        else
                        {
                            TISpeechMod.Speak("Cannot merge these fleets", interrupt: true);
                        }
                    }
                }
            );
        }

        /// <summary>
        /// Execute a maintenance operation (resupply, repair, or both).
        /// </summary>
        public void ExecuteMaintenanceOperation(TISpaceFleetState fleet, Type operationType)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                if (!OperationsManager.operationsLookup.TryGetValue(operationType, out var op))
                {
                    TISpeechMod.Speak("Operation not found", interrupt: true);
                    return;
                }

                var fleetOp = op as TISpaceFleetOperationTemplate;
                if (fleetOp == null)
                {
                    TISpeechMod.Speak("Invalid operation type", interrupt: true);
                    return;
                }

                string opName = fleetOp.GetDisplayName();

                // Check if operation can be performed
                if (!fleetOp.ActorCanPerformOperation(fleet, fleet))
                {
                    TISpeechMod.Speak($"Cannot perform {opName} at this time", interrupt: true);
                    return;
                }

                // Get cost info for announcement
                var costs = fleetOp.ResourceCostOptions(fleet.faction, fleet, fleet, checkCanAfford: false);
                string costInfo = "";
                float duration = 0;
                if (costs != null && costs.Count > 0 && costs[0].anyDebit)
                {
                    duration = costs[0].completionTime_days;
                    string costStr = costs[0].ToString("Relevant", false, false, fleet.faction);
                    costStr = TISpeechMod.CleanText(costStr);
                    costInfo = $" Cost: {costStr}.";
                    if (duration > 0)
                    {
                        costInfo += $" Duration: {duration:F1} days.";
                    }
                }

                // Execute the operation
                bool success = fleetOp.OnOperationConfirm(fleet, fleet, null, null);

                if (success)
                {
                    TISpeechMod.Speak($"{opName} started for {fleet.displayName}.{costInfo}", interrupt: true);
                    MelonLogger.Msg($"{opName} started for {fleet.displayName}");
                }
                else
                {
                    TISpeechMod.Speak($"Failed to start {opName}", interrupt: true);
                    MelonLogger.Warning($"Failed to start {opName} for {fleet.displayName}");
                }

                // Refresh navigation state after operation
                refreshNavigation?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing maintenance operation: {ex.Message}");
                TISpeechMod.Speak("Error executing operation", interrupt: true);
            }
        }

        /// <summary>
        /// Open selection mode to choose a landing site for the fleet.
        /// </summary>
        public void SelectLandingSiteForFleet(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                var landOp = OperationsManager.operationsLookup[typeof(LandOnSurfaceOperation)] as LandOnSurfaceOperation;
                if (landOp == null)
                {
                    TISpeechMod.Speak("Land operation not available", interrupt: true);
                    return;
                }

                var targets = landOp.GetPossibleTargets(fleet);
                if (targets.Count == 0)
                {
                    TISpeechMod.Speak("No landing sites available", interrupt: true);
                    return;
                }

                // Build selection options
                var options = targets.Select(t =>
                {
                    string name;
                    string detail;

                    // Target could be a hab site or a hab
                    if (t.ref_hab != null)
                    {
                        name = t.ref_hab.displayName;
                        detail = t.ref_hab.faction != null ? $"Owned by {t.ref_hab.faction.displayName}" : "Unoccupied";
                    }
                    else if (t.ref_habSite != null)
                    {
                        name = t.ref_habSite.displayName;
                        detail = "Empty site";
                    }
                    else
                    {
                        name = t.displayName ?? "Unknown";
                        detail = "";
                    }

                    return new SelectionOption
                    {
                        Label = name,
                        DetailText = detail,
                        Data = t
                    };
                }).ToList();

                enterSelectionMode(
                    $"Select landing site for {fleet.displayName}",
                    options,
                    selectedIndex =>
                    {
                        if (selectedIndex >= 0 && selectedIndex < targets.Count)
                        {
                            var target = targets[selectedIndex];
                            string targetName = target.ref_hab?.displayName ?? target.ref_habSite?.displayName ?? target.displayName;

                            if (landOp.ActorCanPerformOperation(fleet, target))
                            {
                                landOp.OnOperationConfirm(fleet, target, null, null);
                                TISpeechMod.Speak($"Landing at {targetName}", interrupt: true);
                                MelonLogger.Msg($"Fleet {fleet.displayName} landing at {targetName}");
                                refreshNavigation?.Invoke();
                            }
                            else
                            {
                                TISpeechMod.Speak($"Cannot land at {targetName}", interrupt: true);
                            }
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting landing site: {ex.Message}");
                TISpeechMod.Speak("Error selecting landing site", interrupt: true);
            }
        }

        /// <summary>
        /// Open selection mode to choose a launch orbit for the fleet.
        /// </summary>
        public void SelectLaunchOrbitForFleet(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                var launchOp = OperationsManager.operationsLookup[typeof(LaunchFromSurfaceOperation)] as LaunchFromSurfaceOperation;
                if (launchOp == null)
                {
                    TISpeechMod.Speak("Launch operation not available", interrupt: true);
                    return;
                }

                var targets = launchOp.GetPossibleTargets(fleet);
                if (targets.Count == 0)
                {
                    TISpeechMod.Speak("No launch orbits available - insufficient delta-V", interrupt: true);
                    return;
                }

                // Build selection options - targets are orbit states
                var options = targets.Select(t =>
                {
                    var orbit = t.ref_orbit;
                    string name = orbit?.displayName ?? t.displayName ?? "Unknown orbit";
                    string detail = "";

                    if (orbit != null)
                    {
                        // Calculate delta-V cost for this orbit
                        try
                        {
                            var habSite = fleet.dockedLocation?.ref_habSite;
                            if (habSite != null)
                            {
                                double dvCost = orbit.DeltaVToReachFromSurface_kps(habSite.latitude, fleet.maxAcceleration_mps2);
                                detail = $"{dvCost:F1} km/s delta-V, {orbit.altitude_km:N0} km altitude";
                            }
                            else
                            {
                                detail = $"{orbit.altitude_km:N0} km altitude";
                            }
                        }
                        catch
                        {
                            detail = $"{orbit.altitude_km:N0} km altitude";
                        }
                    }

                    return new SelectionOption
                    {
                        Label = name,
                        DetailText = detail,
                        Data = t
                    };
                }).ToList();

                enterSelectionMode(
                    $"Select launch orbit for {fleet.displayName}",
                    options,
                    selectedIndex =>
                    {
                        if (selectedIndex >= 0 && selectedIndex < targets.Count)
                        {
                            var target = targets[selectedIndex];
                            string orbitName = target.ref_orbit?.displayName ?? target.displayName ?? "orbit";

                            if (launchOp.ActorCanPerformOperation(fleet, target))
                            {
                                launchOp.OnOperationConfirm(fleet, target, null, null);
                                TISpeechMod.Speak($"Launching to {orbitName}", interrupt: true);
                                MelonLogger.Msg($"Fleet {fleet.displayName} launching to {orbitName}");
                                refreshNavigation?.Invoke();
                            }
                            else
                            {
                                TISpeechMod.Speak($"Cannot launch to {orbitName}", interrupt: true);
                            }
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting launch orbit: {ex.Message}");
                TISpeechMod.Speak("Error selecting launch orbit", interrupt: true);
            }
        }

        /// <summary>
        /// Open selection mode to choose a target for a combat operation (Assault, Bombard, Destroy).
        /// </summary>
        public void SelectCombatTargetForFleet(TISpaceFleetState fleet, Type operationType)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                if (!OperationsManager.operationsLookup.TryGetValue(operationType, out var op))
                {
                    TISpeechMod.Speak("Operation not found", interrupt: true);
                    return;
                }

                var fleetOp = op as TISpaceFleetOperationTemplate;
                if (fleetOp == null)
                {
                    TISpeechMod.Speak("Invalid operation type", interrupt: true);
                    return;
                }

                string opName = fleetOp.GetDisplayName();
                var targets = fleetOp.GetPossibleTargets(fleet);

                if (targets.Count == 0)
                {
                    TISpeechMod.Speak($"No valid targets for {opName}", interrupt: true);
                    return;
                }

                // Build selection options based on target type
                var options = targets.Select(t =>
                {
                    string name;
                    string detail;

                    if (t.ref_hab != null)
                    {
                        name = t.ref_hab.displayName;
                        detail = $"{t.ref_hab.faction?.displayName ?? "Unknown"} - {(t.ref_hab.IsBase ? "Base" : "Station")}";
                    }
                    else if (t.isRegionState)
                    {
                        var region = t as TIRegionState;
                        name = region?.displayName ?? t.displayName ?? "Unknown";
                        detail = region?.nation?.displayName ?? "";
                    }
                    else if (t.isArmyState)
                    {
                        var army = t as TIArmyState;
                        name = army?.displayName ?? "Army";
                        detail = army?.faction?.displayName ?? "";
                    }
                    else
                    {
                        name = t.displayName ?? "Unknown target";
                        detail = t.ref_spaceBody?.displayName ?? "";
                    }

                    return new SelectionOption
                    {
                        Label = name,
                        DetailText = detail,
                        Data = t
                    };
                }).ToList();

                enterSelectionMode(
                    $"Select target for {opName}",
                    options,
                    selectedIndex =>
                    {
                        if (selectedIndex >= 0 && selectedIndex < targets.Count)
                        {
                            var target = targets[selectedIndex];
                            ExecuteCombatOperation(fleet, fleetOp, target, opName);
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting combat target: {ex.Message}");
                TISpeechMod.Speak("Error selecting combat target", interrupt: true);
            }
        }

        /// <summary>
        /// Execute a combat operation against the selected target.
        /// </summary>
        private void ExecuteCombatOperation(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation, TIGameState target, string opName)
        {
            try
            {
                // Verify the operation can still be performed
                if (!operation.ActorCanPerformOperation(fleet, target))
                {
                    TISpeechMod.Speak($"Cannot perform {opName} on this target", interrupt: true);
                    return;
                }

                string targetName = target.ref_hab?.displayName ?? target.displayName ?? "target";

                // For contested operations like Assault, get success chance if available
                string successInfo = "";
                if (operation is IContestedOperation contestedOp)
                {
                    float successChance = contestedOp.GetSuccessChance(fleet, target);
                    successInfo = $" Success chance: {successChance * 100:F0}%.";
                }

                // Get duration
                float duration = operation.GetDuration_days(fleet, target, null);
                string durationInfo = duration > 0 ? $" Duration: {duration:F1} days." : "";

                // Execute the operation
                bool success = operation.OnOperationConfirm(fleet, target, null, null);

                if (success)
                {
                    TISpeechMod.Speak($"{opName} initiated against {targetName}.{successInfo}{durationInfo}", interrupt: true);
                    MelonLogger.Msg($"{opName} initiated by {fleet.displayName} against {targetName}");
                }
                else
                {
                    TISpeechMod.Speak($"Failed to initiate {opName}", interrupt: true);
                    MelonLogger.Warning($"Failed to initiate {opName} for {fleet.displayName}");
                }

                // Refresh navigation state after operation
                refreshNavigation?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing combat operation: {ex.Message}");
                TISpeechMod.Speak($"Error executing {opName}", interrupt: true);
            }
        }

        /// <summary>
        /// Open selection mode to choose a station to dock at.
        /// </summary>
        public void SelectDockingTargetForFleet(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            try
            {
                var stations = FleetReader.GetNearbyDockableStations(fleet);

                if (stations.Count == 0)
                {
                    TISpeechMod.Speak("No stations nearby to dock at", interrupt: true);
                    return;
                }

                // If only one station, dock directly
                if (stations.Count == 1)
                {
                    ExecuteDocking(fleet, stations[0]);
                    return;
                }

                // Build selection options
                var options = stations.Select(s =>
                {
                    string faction = s.faction?.displayName ?? "Unknown";
                    bool isFriendly = fleet.faction.permanentAlly(s.faction);
                    bool requiresCombat = s.DockingRequiresCombat(fleet, s.CanDefendHabWithSTOFighters());

                    string detail = isFriendly ? "Friendly" :
                                   requiresCombat ? "Combat required" : "Undefended";

                    return new SelectionOption
                    {
                        Label = s.displayName,
                        DetailText = $"{faction} - {detail}",
                        Data = s
                    };
                }).ToList();

                enterSelectionMode(
                    "Select station to dock at",
                    options,
                    selectedIndex =>
                    {
                        if (selectedIndex >= 0 && selectedIndex < stations.Count)
                        {
                            ExecuteDocking(fleet, stations[selectedIndex]);
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting docking target: {ex.Message}");
                TISpeechMod.Speak("Error selecting docking target", interrupt: true);
            }
        }

        /// <summary>
        /// Execute docking at the selected station.
        /// </summary>
        private void ExecuteDocking(TISpaceFleetState fleet, TIHabState station)
        {
            try
            {
                bool isFriendly = fleet.faction.permanentAlly(station.faction);
                bool requiresCombat = station.DockingRequiresCombat(fleet, station.CanDefendHabWithSTOFighters());

                if (requiresCombat)
                {
                    TISpeechMod.Speak($"Initiating combat to dock at {station.displayName}", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak($"Docking at {station.displayName}", interrupt: true);
                }

                // Use the game's ApproachDock method which handles combat if needed
                fleet.ApproachDock(station);

                MelonLogger.Msg($"{fleet.displayName} approaching to dock at {station.displayName}");

                // Refresh navigation state after operation
                refreshNavigation?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing docking: {ex.Message}");
                TISpeechMod.Speak("Error docking at station", interrupt: true);
            }
        }
    }
}
