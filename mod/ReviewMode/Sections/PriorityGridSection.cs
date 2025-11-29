using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;

namespace TISpeech.ReviewMode.Sections
{
    /// <summary>
    /// A 2D grid section for navigating national priorities.
    /// Rows = priorities, Columns = control points.
    /// </summary>
    public class PriorityGridSection : ISection
    {
        public enum DisplayMode { Percentage, IP, Bonus }

        private TINationState nation;
        private TIFactionState playerFaction;
        private List<PriorityType> priorities;  // rows
        private List<TIControlPoint> controlPoints;  // columns
        private bool readOnly;  // True when viewing a nation you don't control

        private DisplayMode displayMode = DisplayMode.Percentage;

        // Callbacks
        private Action<string> onSpeak;
        private Action<string, List<SelectionOption>, Action<int>> onEnterSelectionMode;

        public string Name => readOnly ? "Priority Grid (Read-Only)" : "Priority Grid";
        public bool IsReadOnly => readOnly;
        public int RowCount => priorities.Count;
        // +1 for the Allocation column on the right
        public int ColumnCount => controlPoints.Count + 1;

        // Index of the virtual Allocation column
        public int AllocationColumnIndex => controlPoints.Count;

        // ISection compatibility - total cells
        public int ItemCount => priorities.Count * ColumnCount;

        public PriorityGridSection(
            TINationState nation,
            TIFactionState playerFaction,
            Action<string> onSpeak,
            Action<string, List<SelectionOption>, Action<int>> onEnterSelectionMode,
            bool readOnly = false)
        {
            this.nation = nation;
            this.playerFaction = playerFaction;
            this.onSpeak = onSpeak;
            this.onEnterSelectionMode = onEnterSelectionMode;
            this.readOnly = readOnly;

            // Initialize rows (valid priorities for this nation)
            priorities = nation.ValidPriorities.ToList();

            // Initialize columns (all control points)
            controlPoints = nation.controlPoints.ToList();
        }

        #region Entry Announcement

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();

            if (readOnly)
            {
                sb.Append($"Priority Grid (read-only). {RowCount} priorities, {controlPoints.Count} control points plus allocation column. ");
            }
            else
            {
                sb.Append($"Priority Grid. {RowCount} priorities, {controlPoints.Count} control points plus allocation column. ");
            }

            // Describe CP ownership
            var yourCPs = controlPoints.Where(cp => cp.faction == playerFaction).ToList();
            var enemyCPs = controlPoints.Where(cp => cp.faction != null && cp.faction != playerFaction).ToList();
            var uncontrolledCPs = controlPoints.Where(cp => cp.faction == null || !cp.owned).ToList();

            if (yourCPs.Count > 0)
            {
                if (yourCPs.Count == 1)
                {
                    var cp = yourCPs[0];
                    var preset = GetCPPresetMatch(controlPoints.IndexOf(cp));
                    string presetInfo = !string.IsNullOrEmpty(preset) ? $" ({preset})" : "";
                    string defendedInfo = cp.defended ? ", defended" : "";
                    sb.Append($"CP {cp.positionInNation + 1} is yours{presetInfo}{defendedInfo}. ");
                }
                else
                {
                    // List each CP with defended status
                    foreach (var cp in yourCPs)
                    {
                        string defendedInfo = cp.defended ? " (defended)" : "";
                        sb.Append($"CP {cp.positionInNation + 1}{defendedInfo}, ");
                    }
                    sb.Length -= 2; // Remove trailing ", "
                    sb.Append(" are yours. ");
                }
            }

            if (enemyCPs.Count > 0)
            {
                foreach (var cp in enemyCPs)
                {
                    string defendedInfo = cp.defended ? ", defended" : "";
                    sb.Append($"CP {cp.positionInNation + 1} is {cp.faction.displayName}{defendedInfo}. ");
                }
            }

            if (uncontrolledCPs.Count > 0)
            {
                if (uncontrolledCPs.Count == 1)
                {
                    sb.Append($"CP {uncontrolledCPs[0].positionInNation + 1} is uncontrolled. ");
                }
                else
                {
                    var cpNums = string.Join(", ", uncontrolledCPs.Select(cp => (cp.positionInNation + 1).ToString()));
                    sb.Append($"CPs {cpNums} are uncontrolled. ");
                }
            }

            sb.Append($"Showing {GetDisplayModeName()}. ");
            sb.Append("Up/down for priorities, left/right for control points.");

            return sb.ToString();
        }

        #endregion

        #region Cell Reading

        public string ReadCell(int row, int col)
        {
            if (row < 0 || row >= RowCount || col < 0 || col >= ColumnCount)
                return "Invalid cell";

            var priority = priorities[row];
            string priorityName = GetPriorityDisplayName(priority);

            // Allocation column (rightmost)
            if (col == AllocationColumnIndex)
            {
                string allocationValue = GetAllocationValue(priority);
                return $"{priorityName}, Allocation: {allocationValue}";
            }

            // Regular CP column
            var cp = controlPoints[col];
            int value = cp.GetControlPointPriority(priority, checkValid: true);
            string cpOwner = GetCPOwnerShort(col);

            return $"{priorityName}, CP {col + 1} ({cpOwner}): {value}";
        }

        public string GetRowHeader(int row)
        {
            if (row < 0 || row >= RowCount)
                return "Invalid row";
            return GetPriorityDisplayName(priorities[row]);
        }

        public string GetColumnHeader(int col)
        {
            if (col < 0 || col >= ColumnCount)
                return "Invalid column";
            if (col == AllocationColumnIndex)
                return $"Allocation ({GetDisplayModeName()})";
            return $"CP {col + 1} ({GetCPOwnerShort(col)})";
        }

        public string ReadRowSummary(int row)
        {
            if (row < 0 || row >= RowCount)
                return "Invalid row";

            var priority = priorities[row];
            var sb = new StringBuilder();

            // Priority name
            sb.Append($"{GetPriorityDisplayName(priority)}: ");

            // All CP values, grouped by owner
            var yourValues = new List<string>();
            var otherValues = new List<string>();

            // Only iterate through CP columns (not the allocation column)
            for (int col = 0; col < AllocationColumnIndex; col++)
            {
                var cp = controlPoints[col];
                int value = cp.GetControlPointPriority(priority, checkValid: true);

                if (cp.faction == playerFaction)
                {
                    yourValues.Add(value.ToString());
                }
                else
                {
                    string owner = GetCPOwnerShort(col);
                    otherValues.Add($"{owner}={value}");
                }
            }

            if (yourValues.Count > 0)
            {
                sb.Append($"You={string.Join(",", yourValues)}");
                if (otherValues.Count > 0) sb.Append(", ");
            }
            if (otherValues.Count > 0)
            {
                sb.Append(string.Join(", ", otherValues));
            }

            sb.Append(". ");

            // Nation allocation based on display mode
            sb.Append(GetRowAllocationInfo(priority));

            // Effect summary with proper labels
            try
            {
                string effect = GetPriorityEffectSummary(priority);
                if (!string.IsNullOrWhiteSpace(effect))
                {
                    sb.Append($" Effect per 0.5 IP: {effect}");
                }
            }
            catch { }

            return sb.ToString();
        }

        public string ReadColumnSummary(int col)
        {
            if (col < 0 || col >= ColumnCount)
                return "Invalid column";

            var cp = controlPoints[col];
            var sb = new StringBuilder();

            // CP header with preset match
            string preset = GetCPPresetMatch(col);
            string presetInfo = !string.IsNullOrEmpty(preset) ? $", matches {preset}" : "";
            sb.AppendLine($"CP {col + 1} ({GetCPOwnerShort(col)}{presetInfo}):");

            // All priorities for this CP
            foreach (var priority in priorities)
            {
                int value = cp.GetControlPointPriority(priority, checkValid: true);
                sb.Append($"{GetPriorityDisplayName(priority)}={value}, ");
            }

            // Diversity bonus if it's the player's CP
            if (cp.faction == playerFaction)
            {
                try
                {
                    float avgBonus = cp.diversityBonus
                        .Where(kvp => nation.ValidPriority(kvp.Key))
                        .Average(kvp => kvp.Value);
                    if (avgBonus > 0)
                    {
                        sb.Append($"Average diversity bonus: {avgBonus:P0}");
                    }
                }
                catch { }
            }

            return sb.ToString().TrimEnd(',', ' ');
        }

        private string GetRowAllocationInfo(PriorityType priority)
        {
            try
            {
                switch (displayMode)
                {
                    case DisplayMode.Percentage:
                        float percent = nation.percentWeighttoPriority(priority) * 100f;
                        return $"Nation allocates {percent:F0}%.";

                    case DisplayMode.IP:
                        // IP per month = weight * base IP * days per month factor
                        float ip = nation.ControlPointWeightsTotalToPriorityIP(priority) * 30.436874f;
                        return $"Nation invests {ip:F1} IP/month.";

                    case DisplayMode.Bonus:
                        float bonus = nation.controlPoints
                            .Average(cp => nation.ControlPointPriorityBonuses_Uncached(cp, priority, checkDisabled: true));
                        if (bonus > 0)
                            return $"Average bonus: +{bonus:P0}.";
                        else if (bonus < 0)
                            return $"Average bonus: {bonus:P0}.";
                        else
                            return "No bonus.";
                }
            }
            catch { }
            return "";
        }

        /// <summary>
        /// Get the allocation value for a cell in the Allocation column.
        /// </summary>
        private string GetAllocationValue(PriorityType priority)
        {
            try
            {
                switch (displayMode)
                {
                    case DisplayMode.Percentage:
                        float percent = nation.percentWeighttoPriority(priority) * 100f;
                        return $"{percent:F0}%";

                    case DisplayMode.IP:
                        float ip = nation.ControlPointWeightsTotalToPriorityIP(priority) * 30.436874f;
                        return $"{ip:F1} IP/month";

                    case DisplayMode.Bonus:
                        float bonus = nation.controlPoints
                            .Average(cp => nation.ControlPointPriorityBonuses_Uncached(cp, priority, checkDisabled: true));
                        if (bonus > 0)
                            return $"+{bonus:P0}";
                        else if (bonus < 0)
                            return $"{bonus:P0}";
                        else
                            return "0%";
                }
            }
            catch { }
            return "?";
        }

        #endregion

        #region CP Info

        public string GetCPOwnerShort(int col)
        {
            if (col < 0 || col >= ColumnCount)
                return "?";
            if (col == AllocationColumnIndex)
                return GetDisplayModeName();

            var cp = controlPoints[col];
            string defended = cp.defended ? ", defended" : "";

            if (cp.faction == playerFaction)
                return $"yours{defended}";
            if (cp.faction != null && cp.owned)
                return $"{cp.faction.displayName}{defended}";
            return "uncontrolled";
        }

        public string GetCPPresetMatch(int col)
        {
            if (col < 0 || col >= ColumnCount)
                return null;
            if (col == AllocationColumnIndex)
                return null;

            try
            {
                var preset = nation.PlayerSettingsMatchTemplate(col);
                return preset?.displayName;
            }
            catch
            {
                return null;
            }
        }

        public bool IsCPOurs(int col)
        {
            if (col < 0 || col >= ColumnCount)
                return false;
            // Allocation column is always "editable" (toggles display mode)
            if (col == AllocationColumnIndex)
                return true;
            return controlPoints[col].faction == playerFaction;
        }

        #endregion

        #region Editing

        public bool CanEditCell(int row, int col)
        {
            if (row < 0 || row >= RowCount || col < 0 || col >= ColumnCount)
                return false;
            // Allocation column is always "editable" (toggles display mode) even in read-only
            if (col == AllocationColumnIndex)
                return true;
            // In read-only mode, no CP editing is allowed
            if (readOnly)
                return false;
            return IsCPOurs(col);
        }

        public void CycleCell(int row, int col, bool decrement = false)
        {
            // Allocation column - toggle display mode
            if (col == AllocationColumnIndex)
            {
                ToggleDisplayMode();
                // Re-announce the cell with the new display mode
                var priority = priorities[row];
                string allocationValue = GetAllocationValue(priority);
                onSpeak?.Invoke($"Allocation: {allocationValue}");
                return;
            }

            if (!CanEditCell(row, col))
            {
                onSpeak?.Invoke("Cannot edit. This control point is not yours.");
                return;
            }

            try
            {
                var cp = controlPoints[col];
                var priority = priorities[row];

                var action = new CyclePrioritySettingAction(cp, playerFaction, priority, decrement);
                playerFaction.playerControl.StartAction(action);

                // Read new value
                int newValue = cp.GetControlPointPriority(priority, checkValid: true);
                onSpeak?.Invoke($"{GetPriorityDisplayName(priority)}: {newValue}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cycling priority: {ex.Message}");
                onSpeak?.Invoke("Error changing priority");
            }
        }

        public void MassCycleRow(int row, bool decrement = false)
        {
            if (row < 0 || row >= RowCount)
                return;

            if (readOnly)
            {
                onSpeak?.Invoke("Cannot edit. This is a read-only view.");
                return;
            }

            var priority = priorities[row];
            int changeCount = 0;

            try
            {
                foreach (var cp in controlPoints.Where(cp => cp.faction == playerFaction))
                {
                    var action = new CyclePrioritySettingAction(cp, playerFaction, priority, decrement);
                    playerFaction.playerControl.StartAction(action);
                    changeCount++;
                }

                if (changeCount > 0)
                {
                    // Read new value from first CP
                    var firstCP = controlPoints.FirstOrDefault(cp => cp.faction == playerFaction);
                    int newValue = firstCP?.GetControlPointPriority(priority, checkValid: true) ?? 0;
                    onSpeak?.Invoke($"{GetPriorityDisplayName(priority)}: all your CPs set to {newValue}");
                }
                else
                {
                    onSpeak?.Invoke("You have no control points in this nation");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error mass cycling priority: {ex.Message}");
                onSpeak?.Invoke("Error changing priorities");
            }
        }

        #endregion

        #region Actions

        public void SyncFromCP(int col)
        {
            if (readOnly)
            {
                onSpeak?.Invoke("Cannot sync. This is a read-only view.");
                return;
            }

            // Can't sync from allocation column
            if (col == AllocationColumnIndex)
            {
                onSpeak?.Invoke("Cannot sync from allocation column. Navigate to a control point.");
                return;
            }

            if (col < 0 || col >= controlPoints.Count || controlPoints[col].faction != playerFaction)
            {
                onSpeak?.Invoke("Cannot sync. This control point is not yours.");
                return;
            }

            try
            {
                var cp = controlPoints[col];
                var action = new SyncPrioritiesAction(cp);
                playerFaction.playerControl.StartAction(action);

                onSpeak?.Invoke($"Synced priorities from CP {col + 1} to all your control points");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error syncing priorities: {ex.Message}");
                onSpeak?.Invoke("Error syncing priorities");
            }
        }

        public void ToggleDisplayMode()
        {
            displayMode = (DisplayMode)(((int)displayMode + 1) % 3);
            onSpeak?.Invoke($"Now showing {GetDisplayModeName()}");
        }

        private string GetDisplayModeName()
        {
            switch (displayMode)
            {
                case DisplayMode.Percentage: return "percentages";
                case DisplayMode.IP: return "investment points per month";
                case DisplayMode.Bonus: return "bonuses";
                default: return "unknown";
            }
        }

        public void StartPresetSelection()
        {
            if (readOnly)
            {
                onSpeak?.Invoke("Cannot apply presets. This is a read-only view.");
                return;
            }

            try
            {
                var presets = GetAvailablePresets();
                if (presets.Count == 0)
                {
                    onSpeak?.Invoke("No presets available");
                    return;
                }

                var options = presets.Select(p => new SelectionOption
                {
                    Label = p.displayName,
                    Data = p
                }).ToList();

                onEnterSelectionMode?.Invoke(
                    "Select a preset to apply to all your control points",
                    options,
                    (index) => ApplyPreset(presets[index])
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting preset selection: {ex.Message}");
                onSpeak?.Invoke("Error loading presets");
            }
        }

        private List<TIPriorityPresetTemplate> GetAvailablePresets()
        {
            return TemplateManager.IterateByClass<TIPriorityPresetTemplate>()
                .Where(p => p.ValidPreset(nation, playerFaction))
                .OrderByDescending(p => p.customDesign)
                .ToList();
        }

        private void ApplyPreset(TIPriorityPresetTemplate preset)
        {
            try
            {
                int applyCount = 0;
                foreach (var cp in controlPoints.Where(cp => cp.faction == playerFaction))
                {
                    var action = new ApplyPriorityPresetToControlPoint(cp, playerFaction, preset.dataName);
                    playerFaction.playerControl.StartAction(action);
                    applyCount++;
                }

                if (applyCount > 0)
                {
                    onSpeak?.Invoke($"Applied {preset.displayName} preset to {applyCount} control point{(applyCount > 1 ? "s" : "")}");
                }
                else
                {
                    onSpeak?.Invoke("You have no control points to apply preset to");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying preset: {ex.Message}");
                onSpeak?.Invoke("Error applying preset");
            }
        }

        #endregion

        #region ISection Implementation

        public string ReadItem(int index)
        {
            // Map linear index to grid coordinates
            int row = index / ColumnCount;
            int col = index % ColumnCount;
            return ReadCell(row, col);
        }

        public string ReadSummary()
        {
            return $"{RowCount} priorities, {ColumnCount} control points";
        }

        public bool CanActivate(int index)
        {
            int row = index / ColumnCount;
            int col = index % ColumnCount;
            return CanEditCell(row, col);
        }

        public void Activate(int index)
        {
            int row = index / ColumnCount;
            int col = index % ColumnCount;
            CycleCell(row, col, decrement: false);
        }

        public bool CanDrillIntoItem(int index) => false;
        public string GetItemSecondaryValue(int index) => null;
        public string ReadItemDetail(int index) => ReadItem(index);
        public bool HasTooltip(int index) => false;
        public void ShowTooltip(int index) { }

        #endregion

        #region Helpers

        private string GetPriorityDisplayName(PriorityType priority)
        {
            try
            {
                return TIUtilities.GetPriorityString(priority, icon: false);
            }
            catch
            {
                return priority.ToString();
            }
        }

        /// <summary>
        /// Get a properly labeled effect summary for a priority.
        /// </summary>
        private string GetPriorityEffectSummary(PriorityType priority)
        {
            var effects = new List<string>();

            try
            {
                switch (priority)
                {
                    case PriorityType.Economy:
                        effects.Add($"+${nation.economyPriorityPerCapitaIncomeChange:N2} per capita GDP");
                        if (nation.economyPriorityInequalityChange != 0)
                            effects.Add($"{nation.economyPriorityInequalityChange:+0.####;-0.####} inequality");
                        break;

                    case PriorityType.Welfare:
                        effects.Add($"{nation.welfarePriorityInequalityChange * -1f:+0.####;-0.####} inequality");
                        break;

                    case PriorityType.Environment:
                        effects.Add($"{nation.SustainabilityChangeForDisplay(nation.environmentPrioritySustainabilityChange)} sustainability");
                        break;

                    case PriorityType.Knowledge:
                        effects.Add($"{nation.knowledgePriorityEducationChange:+0.####;-0.####} education");
                        if (nation.knowledgePriorityCohesionChange != 0)
                            effects.Add($"{nation.knowledgePriorityCohesionChange:+0.####;-0.####} cohesion");
                        break;

                    case PriorityType.Government:
                        effects.Add($"{nation.governmentPriorityDemocracyChange:+0.####;-0.####} democracy");
                        break;

                    case PriorityType.Unity:
                        effects.Add($"{nation.unityPriorityCohesionChange:+0.####;-0.####} cohesion");
                        if (nation.unityPriorityEducationChange != 0)
                            effects.Add($"{nation.unityPriorityEducationChange:+0.####;-0.####} education");
                        break;

                    case PriorityType.Military:
                        effects.Add($"{nation.militaryPriorityTechLevelChange:+0.####;-0.####} military tech");
                        break;

                    case PriorityType.Oppression:
                        effects.Add($"{nation.OppressionPriorityUnrestChange:+0.####;-0.####} unrest");
                        if (nation.OppressionPriorityDemocracyChange != 0)
                            effects.Add($"{nation.OppressionPriorityDemocracyChange:+0.####;-0.####} democracy");
                        if (nation.OppressionPriorityCohesionChange != 0)
                            effects.Add($"{nation.OppressionPriorityCohesionChange:+0.####;-0.####} cohesion");
                        break;

                    case PriorityType.Spoils:
                        effects.Add($"${nation.spoilsPriorityMoneyPerControlPoint:N0} per CP");
                        if (nation.spoilsPriorityInequalityChange != 0)
                            effects.Add($"{nation.spoilsPriorityInequalityChange:+0.####;-0.####} inequality");
                        if (nation.spoilsSustainabilityChange != 0)
                            effects.Add($"{nation.SustainabilityChangeForDisplay(nation.spoilsSustainabilityChange)} sustainability");
                        break;

                    case PriorityType.Funding:
                        effects.Add($"${nation.spaceFundingPriorityIncomeChange:N0} space funding");
                        break;

                    default:
                        // For accumulating priorities (Build Army, Build Navy, etc.), no per-turn effect
                        return "";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting effect summary: {ex.Message}");
            }

            return string.Join(", ", effects);
        }

        /// <summary>
        /// Get the full description of a priority (from localization).
        /// </summary>
        public string GetPriorityDescription(int row)
        {
            if (row < 0 || row >= RowCount)
                return "";

            var priority = priorities[row];
            try
            {
                string priorityLine = GetPriorityDisplayName(priority);
                string tooltip = PriorityListItemController.priorityTipStr(playerFaction, nation, priority, priorityLine);
                return TISpeechMod.CleanText(tooltip);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting priority description: {ex.Message}");
                return "";
            }
        }

        #endregion
    }
}
