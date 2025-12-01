using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for army state data.
    /// Provides summary, detail, and operations for armies.
    /// </summary>
    public class ArmyReader
    {
        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        public string ReadSummary(TIArmyState army)
        {
            if (army == null)
                return "Unknown army";

            var sb = new StringBuilder();
            sb.Append(army.displayName);

            // Strength percentage
            int strengthPercent = (int)(army.strength * 100);
            sb.Append($", {strengthPercent}% strength");

            // Location
            if (army.currentRegion != null)
            {
                sb.Append($", in {army.currentRegion.displayName}");
            }

            // Status
            string status = GetArmyStatus(army);
            if (!string.IsNullOrEmpty(status))
            {
                sb.Append($", {status}");
            }

            return sb.ToString();
        }

        public string ReadDetail(TIArmyState army)
        {
            if (army == null)
                return "Unknown army";

            var sb = new StringBuilder();
            sb.AppendLine($"Army: {army.displayName}");
            sb.AppendLine();

            // Core stats
            sb.AppendLine("Status:");
            sb.AppendLine($"  Strength: {army.strength * 100:F0}%");
            sb.AppendLine($"  Tech Level: {army.techLevel:F1}");
            sb.AppendLine($"  Deployment: {army.deploymentType}");

            // Location info
            sb.AppendLine();
            sb.AppendLine("Location:");
            sb.AppendLine($"  Current: {army.currentRegion?.displayName ?? "Unknown"}");
            sb.AppendLine($"  Home: {army.homeRegion?.displayName ?? "Unknown"}");
            sb.AppendLine($"  Home Nation: {army.homeNation?.displayName ?? "Unknown"}");

            // Movement
            if (army.destinationQueue != null && army.destinationQueue.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Movement Queue:");
                foreach (var dest in army.destinationQueue)
                {
                    sb.AppendLine($"  -> {dest.displayName}");
                }
            }

            // Combat status
            sb.AppendLine();
            sb.AppendLine("Combat Status:");
            sb.AppendLine($"  In Friendly Region: {(army.InFriendlyRegion ? "Yes" : "No")}");
            sb.AppendLine($"  In Battle: {(army.InBattleWithArmies() ? "Yes" : "No")}");
            sb.AppendLine($"  Can Take Offensive Action: {(army.CanTakeOffensiveAction ? "Yes" : "No")}");
            sb.AppendLine($"  Can Heal: {(army.CanHeal() ? "Yes" : "No")}");

            // Special status
            if (army.huntingXenofauna)
            {
                sb.AppendLine($"  Hunting Xenofauna: Active");
            }
            if (army.atSea)
            {
                sb.AppendLine($"  At Sea: Yes");
            }

            // Current operations
            if (army.currentOperations != null && army.currentOperations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Current Operations:");
                foreach (var op in army.currentOperations)
                {
                    sb.AppendLine($"  {op.operation?.GetDisplayName() ?? "Unknown"} -> {op.target?.displayName ?? "Unknown"}");
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TIArmyState army, TIFactionState playerFaction)
        {
            var sections = new List<ISection>();
            if (army == null)
                return sections;

            // Info section
            sections.Add(CreateInfoSection(army));

            // Operations section - only for player's armies
            bool isPlayerArmy = army.faction == playerFaction ||
                               (army.homeNation?.executiveFaction == playerFaction);
            if (isPlayerArmy)
            {
                sections.Add(CreateOperationsSection(army, playerFaction));
            }

            return sections;
        }

        private ISection CreateInfoSection(TIArmyState army)
        {
            var section = new DataSection("Info");

            section.AddItem("Strength", $"{army.strength * 100:F0}%");
            section.AddItem("Tech Level", $"{army.techLevel:F1}");
            section.AddItem("Deployment", army.deploymentType.ToString());
            section.AddItem("Current Region", army.currentRegion?.displayName ?? "Unknown");
            section.AddItem("Home Region", army.homeRegion?.displayName ?? "Unknown");
            section.AddItem("Home Nation", army.homeNation?.displayName ?? "Unknown");

            // Status flags
            string status = GetArmyStatus(army);
            if (!string.IsNullOrEmpty(status))
            {
                section.AddItem("Status", status);
            }

            // Movement queue
            if (army.destinationQueue != null && army.destinationQueue.Count > 0)
            {
                var destinations = string.Join(" -> ", army.destinationQueue.Select(d => d.displayName));
                section.AddItem("Moving To", destinations);
            }

            return section;
        }

        private ISection CreateOperationsSection(TIArmyState army, TIFactionState playerFaction)
        {
            var section = new DataSection("Operations");

            try
            {
                // Get available operations
                var availableOps = army.AvailableOperationList();

                if (availableOps == null || availableOps.Count == 0)
                {
                    section.AddItem("No operations available");
                    return section;
                }

                foreach (var op in availableOps)
                {
                    string opName = op.GetDisplayName();
                    string opDesc = GetOperationDescription(op, army);

                    // Check if operation needs target selection
                    var possibleTargets = op.GetPossibleTargets(army);
                    bool needsTarget = possibleTargets != null && possibleTargets.Count > 0;

                    if (needsTarget)
                    {
                        section.AddItem(opName, opDesc, opDesc,
                            onActivate: () => StartOperationWithTargetSelection(army, op, playerFaction));
                    }
                    else
                    {
                        // Instant operation (like Cancel, Hunt Xenofauna toggle)
                        section.AddItem(opName, opDesc, opDesc,
                            onActivate: () => ExecuteInstantOperation(army, op, playerFaction));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building operations section: {ex.Message}");
                section.AddItem("Error loading operations");
            }

            return section;
        }

        private string GetOperationDescription(IOperation op, TIArmyState army)
        {
            string name = op.GetDisplayName();

            // Add specific info based on operation type
            if (op is DeployArmyOperation deployOp)
            {
                return "Move to a destination region";
            }
            else if (op is AnnexRegionOperation)
            {
                return $"Annex current region (requires 80% strength, currently {army.strength * 100:F0}%)";
            }
            else if (op is RazeRegionOperation)
            {
                return $"Damage region economy (requires 50% strength, currently {army.strength * 100:F0}%)";
            }
            else if (op is SetHuntXenoformingOperation)
            {
                return "Enable hunting of alien megafauna";
            }
            else if (op is CancelHuntXenoformingOperation)
            {
                return "Disable hunting of alien megafauna";
            }
            else if (op is CancelArmyOperation)
            {
                return "Cancel current operation and movement";
            }
            else if (op is ArmyGoHomeOperation)
            {
                return $"Return to home region ({army.homeRegion?.displayName ?? "Unknown"})";
            }

            return name;
        }

        private void StartOperationWithTargetSelection(TIArmyState army, IOperation operation, TIFactionState faction)
        {
            try
            {
                var possibleTargets = operation.GetPossibleTargets(army);
                if (possibleTargets == null || possibleTargets.Count == 0)
                {
                    OnSpeak?.Invoke("No valid targets for this operation", true);
                    return;
                }

                var options = new List<SelectionOption>();
                foreach (var target in possibleTargets)
                {
                    string label = target.displayName;
                    string detail = "";

                    if (target is TIRegionState region)
                    {
                        detail = $"Region in {region.nation?.displayName ?? "Unknown"}";
                        if (region.nation != null && army.homeNation != null)
                        {
                            if (army.homeNation.wars?.Contains(region.nation) == true)
                            {
                                detail += " (at war)";
                            }
                        }
                    }
                    else if (target is TIArmyState targetArmy)
                    {
                        detail = $"Army: {targetArmy.strength * 100:F0}% strength";
                    }

                    options.Add(new SelectionOption
                    {
                        Label = label,
                        DetailText = detail,
                        Data = target
                    });
                }

                OnEnterSelectionMode?.Invoke(
                    $"Select target for {operation.GetDisplayName()}",
                    options,
                    (index) => ExecuteOperationOnTarget(army, operation, (TIGameState)options[index].Data, faction)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting target selection: {ex.Message}");
                OnSpeak?.Invoke("Error selecting target", true);
            }
        }

        private void ExecuteOperationOnTarget(TIArmyState army, IOperation operation, TIGameState target, TIFactionState faction)
        {
            try
            {
                // Create the action to confirm the operation
                var action = new ConfirmOperationAction(army, target, operation);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Started {operation.GetDisplayName()} targeting {target.displayName}", true);
                MelonLogger.Msg($"Executed army operation: {operation.GetDisplayName()} on {target.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing operation: {ex.Message}");
                OnSpeak?.Invoke("Error executing operation", true);
            }
        }

        private void ExecuteInstantOperation(TIArmyState army, IOperation operation, TIFactionState faction)
        {
            try
            {
                // For instant operations, the target is usually the army itself or null
                TIGameState target = army;

                // Special handling for specific operation types
                if (operation is CancelArmyOperation)
                {
                    // Cancel clears operations
                    var action = new ConfirmOperationAction(army, army, operation);
                    faction.playerControl.StartAction(action);
                    OnSpeak?.Invoke("Cancelled current operation", true);
                }
                else if (operation is SetHuntXenoformingOperation || operation is CancelHuntXenoformingOperation)
                {
                    var action = new ConfirmOperationAction(army, army, operation);
                    faction.playerControl.StartAction(action);

                    string state = operation is SetHuntXenoformingOperation ? "enabled" : "disabled";
                    OnSpeak?.Invoke($"Xenofauna hunting {state}", true);
                }
                else
                {
                    // Generic instant operation
                    var action = new ConfirmOperationAction(army, army, operation);
                    faction.playerControl.StartAction(action);
                    OnSpeak?.Invoke($"Executed {operation.GetDisplayName()}", true);
                }

                MelonLogger.Msg($"Executed instant operation: {operation.GetDisplayName()}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing instant operation: {ex.Message}");
                OnSpeak?.Invoke("Error executing operation", true);
            }
        }

        private string GetArmyStatus(TIArmyState army)
        {
            var statuses = new List<string>();

            if (army.IsMoving)
            {
                statuses.Add("moving");
            }
            if (army.InBattleWithArmies())
            {
                statuses.Add("in battle");
            }
            if (army.atSea)
            {
                statuses.Add("at sea");
            }
            if (army.huntingXenofauna)
            {
                statuses.Add("hunting");
            }
            if (army.currentOperations != null && army.currentOperations.Count > 0)
            {
                var op = army.currentOperations.FirstOrDefault();
                if (op?.operation != null)
                {
                    statuses.Add(op.operation.GetDisplayName().ToLower());
                }
            }
            if (!army.InFriendlyRegion)
            {
                statuses.Add("in hostile territory");
            }
            if (army.CanHeal() && army.strength < 1.0f)
            {
                statuses.Add("healing");
            }

            return string.Join(", ", statuses);
        }

        /// <summary>
        /// Get armies for a nation grouped by status.
        /// </summary>
        public static Dictionary<string, List<TIArmyState>> GetArmiesByStatus(TINationState nation)
        {
            var result = new Dictionary<string, List<TIArmyState>>();

            if (nation?.armies == null)
                return result;

            foreach (var army in nation.armies)
            {
                string status;
                if (army.InBattleWithArmies())
                    status = "In Battle";
                else if (army.IsMoving)
                    status = "Moving";
                else if (army.currentOperations?.Count > 0)
                    status = "Operating";
                else if (!army.InFriendlyRegion)
                    status = "In Hostile Territory";
                else
                    status = "Ready";

                if (!result.ContainsKey(status))
                    result[status] = new List<TIArmyState>();

                result[status].Add(army);
            }

            return result;
        }
    }
}
