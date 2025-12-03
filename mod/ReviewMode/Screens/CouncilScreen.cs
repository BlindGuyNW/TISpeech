using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    // SelectionOption is defined in ReviewModeController

    /// <summary>
    /// Council screen - browse councilors and recruitment candidates.
    /// Items include your councilors, a divider, then recruitment candidates.
    /// Both councilors and candidates have navigable sections.
    /// Supports Tab toggle to view known enemy councilors (intel-gated).
    /// </summary>
    public class CouncilScreen : ScreenBase
    {
        /// <summary>
        /// View modes for the Council screen.
        /// </summary>
        public enum ViewMode
        {
            MyCouncil,       // Own councilors + recruitment candidates
            EnemyCouncilors  // Known enemy councilors (intel-gated)
        }

        // Item types in the list
        private enum ItemType { Councilor, RecruitmentDivider, RecruitCandidate, EnemyCouncilor, FactionDivider }
        private class CouncilItem
        {
            public ItemType Type;
            public TICouncilorState Councilor; // For Councilor, RecruitCandidate, or EnemyCouncilor types
            public TIFactionState Faction;     // For faction divider
        }

        private List<CouncilItem> items = new List<CouncilItem>();
        private ViewMode currentMode = ViewMode.MyCouncil;

        private readonly CouncilorReader councilorReader = new CouncilorReader();
        private readonly RecruitCandidateReader recruitReader = new RecruitCandidateReader();
        private readonly MissionModifierReader modifierReader = new MissionModifierReader();

        /// <summary>
        /// Gets or sets the current view mode.
        /// </summary>
        public ViewMode CurrentMode
        {
            get => currentMode;
            set
            {
                if (currentMode != value)
                {
                    currentMode = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Toggles between MyCouncil and EnemyCouncilors modes.
        /// </summary>
        public void ToggleMode()
        {
            CurrentMode = currentMode == ViewMode.MyCouncil ? ViewMode.EnemyCouncilors : ViewMode.MyCouncil;
        }

        /// <summary>
        /// Gets a description of the current mode for announcements.
        /// </summary>
        public string GetModeDescription()
        {
            return currentMode == ViewMode.MyCouncil ? "My Council" : "Known Enemy Councilors";
        }

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for entering selection mode (for mission target selection).
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => currentMode == ViewMode.MyCouncil ? "Council" : "Enemy Councilors";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return currentMode == ViewMode.MyCouncil
                        ? "Manage your councilors and recruit new ones"
                        : "View known enemy councilors";

                if (currentMode == ViewMode.MyCouncil)
                {
                    int current = faction.councilors?.Count ?? 0;
                    int max = faction.maxCouncilSize;
                    int candidates = faction.availableCouncilors?.Count ?? 0;
                    return $"{current}/{max} councilors, {candidates} recruitment candidates. Press Tab to view enemies.";
                }
                else
                {
                    int knownEnemies = CountKnownEnemyCouncilors(faction);
                    return $"{knownEnemies} known enemy councilors. Press Tab to view your council.";
                }
            }
        }

        private int CountKnownEnemyCouncilors(TIFactionState faction)
        {
            int count = 0;
            try
            {
                foreach (var councilor in GameStateManager.IterateByClass<TICouncilorState>())
                {
                    if (councilor.faction != faction &&
                        councilor.status == CouncilorStatus.Active &&
                        councilor.faction != null &&
                        faction.HasIntelOnCouncilorLocation(councilor))  // Use location intel (0.10) threshold
                    {
                        count++;
                    }
                }
            }
            catch { }
            return count;
        }

        public override bool SupportsViewModeToggle => true;

        public override string ToggleViewMode()
        {
            ToggleMode();
            var faction = GameControl.control?.activePlayer;

            if (currentMode == ViewMode.EnemyCouncilors)
            {
                int knownEnemies = CountKnownEnemyCouncilors(faction);
                return $"Enemy Councilors: {knownEnemies} known. Press Tab to return to your council.";
            }
            else
            {
                int myCouncilors = faction?.councilors?.Count ?? 0;
                int candidates = faction?.availableCouncilors?.Count ?? 0;
                return $"My Council: {myCouncilors} councilors, {candidates} recruitment candidates. Press Tab to view enemies.";
            }
        }

        public CouncilScreen()
        {
            // Wire up reader callbacks
            councilorReader.OnAssignMission = StartMissionAssignment;
            councilorReader.OnToggleAutomation = ToggleAutomation;
            councilorReader.OnApplyAugmentation = StartAugmentation;
            councilorReader.OnManageOrg = StartManageOrg;
            councilorReader.OnAcquireOrg = StartAcquireOrg;
            councilorReader.OnDismissCouncilor = StartDismissCouncilor;
            councilorReader.OnAbortMission = StartAbortMission;
            councilorReader.OnSetAutofailValue = ExecuteSetAutofailValue;
            recruitReader.OnRecruit = ExecuteRecruitment;
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

                if (currentMode == ViewMode.MyCouncil)
                {
                    RefreshMyCouncil(faction);
                }
                else
                {
                    RefreshEnemyCouncilors(faction);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing council screen: {ex.Message}");
            }
        }

        private void RefreshMyCouncil(TIFactionState faction)
        {
            // Add councilors
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    items.Add(new CouncilItem { Type = ItemType.Councilor, Councilor = councilor });
                }
            }

            // Add recruitment divider and candidates if there are any
            if (faction.availableCouncilors != null && faction.availableCouncilors.Count > 0)
            {
                items.Add(new CouncilItem { Type = ItemType.RecruitmentDivider });

                foreach (var candidate in faction.availableCouncilors)
                {
                    items.Add(new CouncilItem { Type = ItemType.RecruitCandidate, Councilor = candidate });
                }
            }
        }

        private void RefreshEnemyCouncilors(TIFactionState faction)
        {
            // Group enemy councilors by faction
            // Include councilors at location intel (0.10) or higher - same threshold as Investigate mission
            var enemiesByFaction = new Dictionary<TIFactionState, List<TICouncilorState>>();

            foreach (var councilor in GameStateManager.IterateByClass<TICouncilorState>())
            {
                // Skip own councilors, inactive, factionless
                if (councilor.faction == null ||
                    councilor.faction == faction ||
                    councilor.status != CouncilorStatus.Active)
                {
                    continue;
                }

                // Include if we have location intel (0.10) - same as Investigate Councilor targeting
                if (!faction.HasIntelOnCouncilorLocation(councilor))
                {
                    continue;
                }

                if (!enemiesByFaction.ContainsKey(councilor.faction))
                {
                    enemiesByFaction[councilor.faction] = new List<TICouncilorState>();
                }
                enemiesByFaction[councilor.faction].Add(councilor);
            }

            // Sort factions by name for consistent ordering
            var sortedFactions = enemiesByFaction.Keys.OrderBy(f => f.displayName).ToList();

            foreach (var enemyFaction in sortedFactions)
            {
                // Add faction divider
                items.Add(new CouncilItem { Type = ItemType.FactionDivider, Faction = enemyFaction });

                // Add councilors for this faction
                // Sort: known names first (alphabetically), then unknown agents
                var councilors = enemiesByFaction[enemyFaction]
                    .OrderBy(c => faction.HasIntelOnCouncilorBasicData(c) ? 0 : 1)
                    .ThenBy(c => faction.HasIntelOnCouncilorBasicData(c) ? c.displayName : c.location?.displayName ?? "")
                    .ToList();

                foreach (var councilor in councilors)
                {
                    items.Add(new CouncilItem { Type = ItemType.EnemyCouncilor, Councilor = councilor });
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
                case ItemType.Councilor:
                    return councilorReader.ReadSummary(item.Councilor);

                case ItemType.RecruitmentDivider:
                    int candidateCount = faction?.availableCouncilors?.Count ?? 0;
                    int slots = (faction?.maxCouncilSize ?? 6) - (faction?.councilors?.Count ?? 0);
                    return $"--- Recruitment Pool: {candidateCount} candidates, {slots} slots available ---";

                case ItemType.RecruitCandidate:
                    return "Recruit: " + recruitReader.ReadSummary(item.Councilor);

                case ItemType.FactionDivider:
                    int factionCouncilorCount = items.Count(i => i.Type == ItemType.EnemyCouncilor && i.Councilor?.faction == item.Faction);
                    return $"--- {item.Faction?.displayName ?? "Unknown"}: {factionCouncilorCount} known councilors ---";

                case ItemType.EnemyCouncilor:
                    return councilorReader.ReadSummary(item.Councilor, faction);

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
                case ItemType.Councilor:
                    return councilorReader.ReadDetail(item.Councilor);

                case ItemType.RecruitmentDivider:
                    return recruitReader.GetRecruitmentStatus(faction);

                case ItemType.RecruitCandidate:
                    return recruitReader.ReadDetail(item.Councilor);

                case ItemType.FactionDivider:
                    return GetFactionIntelSummary(item.Faction, faction);

                case ItemType.EnemyCouncilor:
                    return councilorReader.ReadDetail(item.Councilor, faction);

                default:
                    return "Unknown";
            }
        }

        private string GetFactionIntelSummary(TIFactionState enemyFaction, TIFactionState viewer)
        {
            if (enemyFaction == null || viewer == null)
                return "Unknown faction";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Faction: {enemyFaction.displayName}");

            // Count councilors by intel level
            int basicOnly = 0;
            int detailLevel = 0;
            int missionLevel = 0;
            int fullIntel = 0;

            foreach (var councilor in GameStateManager.IterateByClass<TICouncilorState>())
            {
                if (councilor.faction != enemyFaction || councilor.status != CouncilorStatus.Active)
                    continue;

                if (viewer.HasIntelOnCouncilorSecrets(councilor))
                    fullIntel++;
                else if (viewer.HasIntelOnCouncilorMission(councilor))
                    missionLevel++;
                else if (viewer.HasIntelOnCouncilorDetails(councilor))
                    detailLevel++;
                else if (viewer.HasIntelOnCouncilorBasicData(councilor))
                    basicOnly++;
            }

            int total = basicOnly + detailLevel + missionLevel + fullIntel;
            sb.AppendLine($"Known councilors: {total}");

            if (fullIntel > 0)
                sb.AppendLine($"  Full intel: {fullIntel}");
            if (missionLevel > 0)
                sb.AppendLine($"  Mission intel: {missionLevel}");
            if (detailLevel > 0)
                sb.AppendLine($"  Detail intel: {detailLevel}");
            if (basicOnly > 0)
                sb.AppendLine($"  Basic intel only: {basicOnly}");

            return sb.ToString();
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
                case ItemType.Councilor:
                    cachedSections = councilorReader.GetSections(item.Councilor);
                    break;

                case ItemType.RecruitmentDivider:
                case ItemType.FactionDivider:
                    // Dividers have no sections - just markers
                    cachedSections = new List<ISection>();
                    break;

                case ItemType.RecruitCandidate:
                    cachedSections = recruitReader.GetSections(item.Councilor);
                    break;

                case ItemType.EnemyCouncilor:
                    // Pass viewer faction for intel-gated sections
                    cachedSections = councilorReader.GetSections(item.Councilor, faction);
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

            // Dividers can't be drilled into
            var itemType = items[index].Type;
            if (itemType == ItemType.RecruitmentDivider || itemType == ItemType.FactionDivider)
                return false;

            return base.CanDrillIntoItem(index);
        }

        #region Recruitment

        private void ExecuteRecruitment(TICouncilorState candidate)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || candidate == null)
                {
                    OnSpeak?.Invoke("Cannot recruit: invalid state", true);
                    return;
                }

                if (!recruitReader.CanRecruitCandidate(candidate, faction))
                {
                    OnSpeak?.Invoke("Cannot recruit: check cost and available slots", true);
                    return;
                }

                // Get cost string for confirmation
                string costString = TISpeechMod.CleanText(candidate.GetRecruitCostString(faction));
                string actionDescription = $"Recruit {candidate.displayName}";
                string details = ConfirmationHelper.FormatCostDetails(costString);

                // Request confirmation before recruiting
                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformRecruitment(candidate, faction),
                    onCancel: () => OnSpeak?.Invoke("Recruitment cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating recruitment: {ex.Message}");
                OnSpeak?.Invoke("Error initiating recruitment", true);
            }
        }

        private void PerformRecruitment(TICouncilorState candidate, TIFactionState faction)
        {
            try
            {
                // Execute recruitment action
                var action = new RecruitCouncilorAction(candidate, faction);
                faction.playerControl.StartAction(action);

                string announcement = $"Recruited {candidate.displayName} as a new councilor!";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error recruiting councilor: {ex.Message}");
                OnSpeak?.Invoke("Error recruiting councilor", true);
            }
        }

        #endregion

        #region Mission Assignment

        private void StartMissionAssignment(TICouncilorState councilor, TIMissionTemplate mission)
        {
            MelonLogger.Msg($"Starting mission assignment: {councilor.displayName} -> {mission.displayName}");

            // Get valid targets
            IList<TIGameState> targets = null;
            try
            {
                targets = mission.GetValidTargets(councilor);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting valid targets: {ex.Message}");
            }

            if (targets == null || targets.Count == 0)
            {
                OnSpeak?.Invoke($"No valid targets for {mission.displayName}", true);
                return;
            }

            // Build selection options with modifier breakdown
            var options = new List<SelectionOption>();
            var faction = GameControl.control?.activePlayer;
            foreach (var target in targets)
            {
                string label;

                // For councilor targets, use intel-gated display name
                if (target.isCouncilorState && faction != null)
                {
                    var targetCouncilor = target.ref_councilor;
                    var view = new CouncilorView(targetCouncilor, faction);
                    label = view.displayNameCurrent;
                    // If it's just "Unknown", add location context
                    if (label == Loc.T("UI.CouncilorView.Unknown") || string.IsNullOrEmpty(label))
                    {
                        label = $"Unknown Agent at {targetCouncilor.location?.displayName ?? "unknown location"}";
                    }
                }
                else
                {
                    label = target.displayName ?? "Unknown target";
                }

                // Get modifier breakdown
                var breakdown = modifierReader.GetModifiers(mission, councilor, target, 0f);

                // Summary includes success chance
                label += $", {breakdown.SuccessChance}";

                // Store detailed breakdown for verbose mode
                string detailText = modifierReader.FormatForSpeech(breakdown, verbose: true);

                options.Add(new SelectionOption
                {
                    Label = label,
                    DetailText = detailText,
                    Data = target
                });
            }

            // Enter selection mode
            OnEnterSelectionMode?.Invoke(
                $"Select target for {mission.displayName}",
                options,
                (index) =>
                {
                    var selectedTarget = (TIGameState)options[index].Data;

                    // Check if mission has a resource boost slider
                    if (mission.cost is TIMissionCost_Bonus)
                    {
                        StartBoostSelection(councilor, mission, selectedTarget);
                    }
                    else
                    {
                        ExecuteMissionAssignment(councilor, mission, selectedTarget, 0f);
                    }
                }
            );
        }

        private void StartBoostSelection(TICouncilorState councilor, TIMissionTemplate mission, TIGameState target)
        {
            try
            {
                var faction = councilor.faction;
                int maxSteps = councilor.CurrentMaxSliderSteps(mission);
                var resourceType = mission.cost.resourceType;
                string resourceName = GetResourceName(resourceType);

                var options = new List<SelectionOption>();

                for (int step = 0; step <= maxSteps; step++)
                {
                    float cost = mission.cost.GetCost(step, councilor);
                    var breakdown = modifierReader.GetModifiers(mission, councilor, target, step);

                    string label;
                    if (step == 0)
                    {
                        label = $"No boost, {breakdown.SuccessChance}";
                    }
                    else
                    {
                        label = $"Boost {step}: {cost:F0} {resourceName}, {breakdown.SuccessChance}";
                    }

                    string detail = modifierReader.FormatForSpeech(breakdown, verbose: true);

                    options.Add(new SelectionOption
                    {
                        Label = label,
                        DetailText = detail,
                        Data = (float)step  // Store the slider step value
                    });
                }

                // Show current resource amount
                float available = faction.GetCurrentResourceAmount(resourceType);
                string prompt = $"Select boost level ({available:F0} {resourceName} available)";

                OnEnterSelectionMode?.Invoke(
                    prompt,
                    options,
                    (index) =>
                    {
                        float boostLevel = (float)options[index].Data;
                        ExecuteMissionAssignment(councilor, mission, target, boostLevel);
                    }
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in boost selection: {ex.Message}");
                // Fall back to no boost
                ExecuteMissionAssignment(councilor, mission, target, 0f);
            }
        }

        private string GetResourceName(FactionResource resource)
        {
            switch (resource)
            {
                case FactionResource.Money: return "Money";
                case FactionResource.Influence: return "Influence";
                case FactionResource.Operations: return "Ops";
                case FactionResource.Research: return "Research";
                case FactionResource.Boost: return "Boost";
                default: return resource.ToString();
            }
        }

        /// <summary>
        /// Gets intel-gated display name for a target.
        /// For councilor targets, respects intel level. For other targets, returns displayName.
        /// </summary>
        private string GetIntelGatedTargetName(TIGameState target, TIFactionState viewer)
        {
            if (target == null)
                return "Unknown";

            if (target.isCouncilorState && viewer != null)
            {
                var targetCouncilor = target.ref_councilor;
                var view = new CouncilorView(targetCouncilor, viewer);
                string name = view.displayNameCurrent;

                // If it's "Unknown", add location context
                if (string.IsNullOrEmpty(name) || name == Loc.T("UI.CouncilorView.Unknown"))
                {
                    return $"Unknown Agent at {targetCouncilor.location?.displayName ?? "unknown location"}";
                }
                return name;
            }

            return target.displayName ?? "Unknown";
        }

        private void ExecuteMissionAssignment(TICouncilorState councilor, TIMissionTemplate mission, TIGameState target, float resourcesSpent)
        {
            try
            {
                var faction = councilor.faction;
                var action = new AssignCouncilorToMission(councilor, mission, target, resourcesSpent, false);
                faction.playerControl.StartAction(action);

                string announcement = $"Assigned {councilor.displayName} to {mission.displayName}";
                if (target != null)
                {
                    // Use intel-gated display name for councilor targets
                    string targetName = GetIntelGatedTargetName(target, faction);
                    announcement += $" targeting {targetName}";
                }
                if (resourcesSpent > 0 && mission.cost != null)
                {
                    float cost = mission.cost.GetCost(resourcesSpent, councilor);
                    string resourceName = GetResourceName(mission.cost.resourceType);
                    announcement += $" with {cost:F0} {resourceName} boost";
                }

                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1; // Invalidate section cache
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing mission assignment: {ex.Message}");
                OnSpeak?.Invoke("Error assigning mission", true);
            }
        }

        #endregion

        #region Automation

        private void ToggleAutomation(TICouncilorState councilor)
        {
            try
            {
                bool newState = !councilor.permanentDefenseMode;
                var action = new ToggleAutomateCouncilorAction(councilor, newState);
                councilor.faction.playerControl.StartAction(action);

                string status = newState ? "enabled" : "disabled";
                OnSpeak?.Invoke($"Automation {status} for {councilor.displayName}", true);

                // Refresh
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling automation: {ex.Message}");
                OnSpeak?.Invoke("Error toggling automation", true);
            }
        }

        #endregion

        #region Augmentation (Spend XP)

        private void StartAugmentation(TICouncilorState councilor, CouncilorAugmentationOption augOption)
        {
            try
            {
                // Get display strings for confirmation
                augOption.SetAugmentationStrings(out string description1, out string description2, out string tooltipDescription, out string costString);

                string label = TISpeechMod.CleanText(description1);
                string value = TISpeechMod.CleanText(description2);
                string cost = TISpeechMod.CleanText(costString);

                string actionDescription = !string.IsNullOrEmpty(value) ? $"{label}: {value}" : label;
                string details = ConfirmationHelper.FormatCostDetails(cost);

                // Request confirmation
                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformAugmentation(councilor, augOption, actionDescription),
                    onCancel: () => OnSpeak?.Invoke("Augmentation cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating augmentation: {ex.Message}");
                OnSpeak?.Invoke("Error initiating augmentation", true);
            }
        }

        private void PerformAugmentation(TICouncilorState councilor, CouncilorAugmentationOption augOption, string description)
        {
            try
            {
                var faction = councilor.faction;
                var action = new AugmentCouncilorAction(councilor, augOption);
                faction.playerControl.StartAction(action);

                string announcement = $"Applied augmentation: {description} to {councilor.displayName}";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying augmentation: {ex.Message}");
                OnSpeak?.Invoke("Error applying augmentation", true);
            }
        }

        #endregion

        #region Organization Management

        private void StartManageOrg(TICouncilorState councilor, TIOrgState org)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || councilor == null || org == null)
                {
                    OnSpeak?.Invoke("Cannot manage organization: invalid state", true);
                    return;
                }

                // Build options for managing this org
                var options = new List<SelectionOption>();

                // Move to pool
                options.Add(new SelectionOption
                {
                    Label = "Move to Faction Pool",
                    DetailText = "Remove from this councilor and add to the faction's unassigned org pool",
                    Data = "pool"
                });

                // Transfer to other councilors
                foreach (var otherCouncilor in faction.councilors)
                {
                    if (otherCouncilor == councilor)
                        continue;

                    try
                    {
                        if (org.IsEligibleForCouncilor(otherCouncilor) && otherCouncilor.SufficientCapacityForOrg(org))
                        {
                            options.Add(new SelectionOption
                            {
                                Label = $"Transfer to {otherCouncilor.displayName}",
                                DetailText = $"Transfer this org to {otherCouncilor.displayName}",
                                Data = otherCouncilor
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking transfer eligibility: {ex.Message}");
                    }
                }

                // Sell
                string salePrice = GetSalePriceString(org);
                options.Add(new SelectionOption
                {
                    Label = $"Sell ({salePrice})",
                    DetailText = $"Sell this org back to the market for {salePrice}",
                    Data = "sell"
                });

                // Enter selection mode
                OnEnterSelectionMode?.Invoke(
                    $"Manage {org.displayName}",
                    options,
                    (index) => ExecuteOrgAction(councilor, org, options[index])
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting org management: {ex.Message}");
                OnSpeak?.Invoke("Error managing organization", true);
            }
        }

        private void ExecuteOrgAction(TICouncilorState councilor, TIOrgState org, SelectionOption option)
        {
            try
            {
                var faction = councilor.faction;

                if (option.Data is string action)
                {
                    if (action == "pool")
                    {
                        ConfirmationHelper.RequestConfirmation(
                            $"Move {org.displayName} to faction pool",
                            "The org will be unassigned from this councilor",
                            OnEnterSelectionMode,
                            onConfirm: () =>
                            {
                                var poolAction = new TransferOrgToFactionPoolAction(org, councilor);
                                faction.playerControl.StartAction(poolAction);
                                OnSpeak?.Invoke($"Moved {org.displayName} to faction pool", true);
                                Refresh();
                                cachedItemIndex = -1;
                            },
                            onCancel: () => OnSpeak?.Invoke("Action cancelled", true)
                        );
                    }
                    else if (action == "sell")
                    {
                        string salePrice = GetSalePriceString(org);
                        ConfirmationHelper.RequestConfirmation(
                            $"Sell {org.displayName}",
                            $"Returns: {salePrice}",
                            OnEnterSelectionMode,
                            onConfirm: () =>
                            {
                                var sellAction = new SellOrgAction(org, faction, councilor);
                                faction.playerControl.StartAction(sellAction);
                                OnSpeak?.Invoke($"Sold {org.displayName}", true);
                                Refresh();
                                cachedItemIndex = -1;
                            },
                            onCancel: () => OnSpeak?.Invoke("Sale cancelled", true)
                        );
                    }
                }
                else if (option.Data is TICouncilorState targetCouncilor)
                {
                    ConfirmationHelper.RequestConfirmation(
                        $"Transfer {org.displayName} to {targetCouncilor.displayName}",
                        "The org will be moved to the other councilor",
                        OnEnterSelectionMode,
                        onConfirm: () =>
                        {
                            var transferAction = new TransferOrgToCouncilorAction(org, faction, targetCouncilor, councilor);
                            faction.playerControl.StartAction(transferAction);
                            OnSpeak?.Invoke($"Transferred {org.displayName} to {targetCouncilor.displayName}", true);
                            Refresh();
                            cachedItemIndex = -1;
                        },
                        onCancel: () => OnSpeak?.Invoke("Transfer cancelled", true)
                    );
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing org action: {ex.Message}");
                OnSpeak?.Invoke("Error managing organization", true);
            }
        }

        private void StartAcquireOrg(TICouncilorState councilor)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot acquire organization: invalid state", true);
                    return;
                }

                // Build options from pool orgs and market orgs
                var options = new List<SelectionOption>();

                // Pool orgs (can be assigned for free)
                if (faction.unassignedOrgs != null)
                {
                    foreach (var org in faction.unassignedOrgs)
                    {
                        try
                        {
                            if (org.IsEligibleForCouncilor(councilor) && councilor.SufficientCapacityForOrg(org))
                            {
                                options.Add(new SelectionOption
                                {
                                    Label = $"[Pool] {org.displayName}, Tier {org.tier}",
                                    DetailText = $"Assign from faction pool (free)",
                                    Data = new OrgAcquireData { Org = org, FromPool = true }
                                });
                            }
                        }
                        catch { }
                    }
                }

                // Market orgs (cost to purchase) - only show affordable ones
                if (faction.availableOrgs != null)
                {
                    foreach (var org in faction.availableOrgs)
                    {
                        try
                        {
                            // Must be eligible, have capacity, AND be affordable
                            if (org.IsEligibleForCouncilor(councilor) &&
                                councilor.SufficientCapacityForOrg(org) &&
                                faction.CanPurchaseOrg(org))
                            {
                                string cost = GetPurchaseCostString(org, faction);
                                options.Add(new SelectionOption
                                {
                                    Label = $"[Market] {org.displayName}, Tier {org.tier}",
                                    DetailText = $"Purchase for: {cost}",
                                    Data = new OrgAcquireData { Org = org, FromPool = false }
                                });
                            }
                        }
                        catch { }
                    }
                }

                if (options.Count == 0)
                {
                    OnSpeak?.Invoke("No available organizations for this councilor", true);
                    return;
                }

                OnEnterSelectionMode?.Invoke(
                    $"Acquire Organization for {councilor.displayName}",
                    options,
                    (index) => ExecuteAcquireOrg(councilor, options[index])
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting org acquisition: {ex.Message}");
                OnSpeak?.Invoke("Error acquiring organization", true);
            }
        }

        private void ExecuteAcquireOrg(TICouncilorState councilor, SelectionOption option)
        {
            try
            {
                var faction = councilor.faction;
                var data = (OrgAcquireData)option.Data;

                if (data.FromPool)
                {
                    // Assign from pool - request confirmation
                    ConfirmationHelper.RequestConfirmation(
                        $"Assign {data.Org.displayName} to {councilor.displayName}",
                        "This org will be assigned from the faction pool",
                        OnEnterSelectionMode,
                        onConfirm: () =>
                        {
                            var action = new PurchaseOrgAction(data.Org, faction, councilor);
                            faction.playerControl.StartAction(action);
                            OnSpeak?.Invoke($"Assigned {data.Org.displayName} to {councilor.displayName}", true);
                            Refresh();
                            cachedItemIndex = -1;
                        },
                        onCancel: () => OnSpeak?.Invoke("Assignment cancelled", true)
                    );
                }
                else
                {
                    // Check affordability before showing confirmation
                    if (!faction.CanPurchaseOrg(data.Org))
                    {
                        OnSpeak?.Invoke("Cannot afford this organization", true);
                        return;
                    }

                    // Purchase from market - request confirmation
                    string cost = GetPurchaseCostString(data.Org, faction);
                    ConfirmationHelper.RequestConfirmation(
                        $"Purchase {data.Org.displayName} for {councilor.displayName}",
                        ConfirmationHelper.FormatCostDetails(cost),
                        OnEnterSelectionMode,
                        onConfirm: () =>
                        {
                            // Final affordability check before executing
                            if (!faction.CanPurchaseOrg(data.Org))
                            {
                                OnSpeak?.Invoke("Cannot afford this organization", true);
                                return;
                            }
                            var action = new PurchaseOrgAction(data.Org, faction, councilor);
                            faction.playerControl.StartAction(action);
                            OnSpeak?.Invoke($"Purchased {data.Org.displayName} for {councilor.displayName}", true);
                            Refresh();
                            cachedItemIndex = -1;
                        },
                        onCancel: () => OnSpeak?.Invoke("Purchase cancelled", true)
                    );
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error acquiring org: {ex.Message}");
                OnSpeak?.Invoke("Error acquiring organization", true);
            }
        }

        private class OrgAcquireData
        {
            public TIOrgState Org;
            public bool FromPool;
        }

        private string GetSalePriceString(TIOrgState org)
        {
            try
            {
                var salePrice = org.GetSalePrice();
                return TISpeechMod.CleanText(salePrice.ToString());
            }
            catch
            {
                return "Price unknown";
            }
        }

        private string GetPurchaseCostString(TIOrgState org, TIFactionState faction)
        {
            try
            {
                var cost = org.GetPurchaseCost(faction);
                if (cost != null)
                {
                    return TISpeechMod.CleanText(cost.ToString());
                }
            }
            catch { }
            return "Cost unknown";
        }

        #endregion

        #region Dismiss Councilor

        private void StartDismissCouncilor(TICouncilorState councilor, bool keepOrgs)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot dismiss: invalid state", true);
                    return;
                }

                // Check if this is a turned enemy councilor
                bool isTurnedEnemy = councilor.agentForFaction == faction && councilor.faction != faction;

                string actionDescription;
                string details;

                if (isTurnedEnemy)
                {
                    actionDescription = $"Release {councilor.displayName}";
                    details = $"This turned councilor will return to {councilor.faction?.displayName ?? "their faction"}";
                }
                else
                {
                    int orgCount = councilor.orgs?.Count ?? 0;
                    if (orgCount > 0)
                    {
                        if (keepOrgs)
                        {
                            actionDescription = $"Dismiss {councilor.displayName} (Keep Orgs)";
                            details = $"{orgCount} organization(s) will be moved to faction pool";
                        }
                        else
                        {
                            string saleValue = GetOrgsSaleValueString(councilor);
                            actionDescription = $"Dismiss {councilor.displayName} (Sell Orgs)";
                            details = $"{orgCount} organization(s) will be sold for {saleValue}";
                        }
                    }
                    else
                    {
                        actionDescription = $"Dismiss {councilor.displayName}";
                        details = "This councilor will be removed from your council";
                    }
                }

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformDismissCouncilor(councilor, keepOrgs),
                    onCancel: () => OnSpeak?.Invoke("Dismissal cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating dismiss: {ex.Message}");
                OnSpeak?.Invoke("Error initiating dismissal", true);
            }
        }

        private void PerformDismissCouncilor(TICouncilorState councilor, bool keepOrgs)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null) return;

                // Check if this is a turned enemy councilor
                bool isTurnedEnemy = councilor.agentForFaction == faction && councilor.faction != faction;
                TIFactionState councilorFaction = councilor.faction;

                if (isTurnedEnemy)
                {
                    // For turned enemy, just dismiss - orgs stay with the enemy faction
                    var action = new DismissCouncilorAction(councilor, councilorFaction, faction);
                    faction.playerControl.StartAction(action);

                    OnSpeak?.Invoke($"Released {councilor.displayName} back to {councilorFaction?.displayName ?? "their faction"}", true);
                }
                else
                {
                    // For our own councilor
                    if (!keepOrgs && councilor.orgs != null && councilor.orgs.Count > 0)
                    {
                        // Sell all orgs first
                        foreach (var org in councilor.orgs.ToList())
                        {
                            var sellAction = new SellOrgAction(org, faction, councilor);
                            faction.playerControl.StartAction(sellAction);
                        }
                    }
                    else if (keepOrgs && councilor.orgs != null && councilor.orgs.Count > 0)
                    {
                        // Move all orgs to pool first
                        foreach (var org in councilor.orgs.ToList())
                        {
                            var poolAction = new TransferOrgToFactionPoolAction(org, councilor);
                            faction.playerControl.StartAction(poolAction);
                        }
                    }

                    // Then dismiss the councilor
                    var dismissAction = new DismissCouncilorAction(councilor, faction, faction);
                    faction.playerControl.StartAction(dismissAction);

                    OnSpeak?.Invoke($"Dismissed {councilor.displayName}", true);
                }

                MelonLogger.Msg($"Dismissed councilor: {councilor.displayName}");

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error dismissing councilor: {ex.Message}");
                OnSpeak?.Invoke("Error dismissing councilor", true);
            }
        }

        private string GetOrgsSaleValueString(TICouncilorState councilor)
        {
            try
            {
                var saleValue = councilor.AllOrgsSaleValue;
                return TISpeechMod.CleanText(saleValue.ToString("N0"));
            }
            catch
            {
                return "unknown value";
            }
        }

        #endregion

        #region Abort Mission

        private void StartAbortMission(TICouncilorState councilor)
        {
            try
            {
                if (councilor == null || !councilor.HasMission)
                {
                    OnSpeak?.Invoke("No mission to abort", true);
                    return;
                }

                if (TIMissionPhaseState.InMissionPhase())
                {
                    OnSpeak?.Invoke("Cannot abort mission during mission phase", true);
                    return;
                }

                string missionName = councilor.activeMission?.missionTemplate?.displayName ?? "mission";
                string targetName = councilor.activeMission?.target?.displayName ?? "";

                string actionDescription = $"Abort {missionName}";
                string details = !string.IsNullOrEmpty(targetName)
                    ? $"Cancel mission targeting {targetName}"
                    : "Cancel the current mission assignment";

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformAbortMission(councilor),
                    onCancel: () => OnSpeak?.Invoke("Abort cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating abort: {ex.Message}");
                OnSpeak?.Invoke("Error initiating abort", true);
            }
        }

        private void PerformAbortMission(TICouncilorState councilor)
        {
            try
            {
                var faction = councilor.faction;
                if (faction == null || !councilor.HasMission) return;

                string missionName = councilor.activeMission?.missionTemplate?.displayName ?? "mission";

                var action = new AbortMission(councilor, false, TIMissionState.AbortReason.VoluntaryAbort);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Aborted {missionName} for {councilor.displayName}", true);
                MelonLogger.Msg($"Aborted mission: {councilor.displayName} - {missionName}");

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error aborting mission: {ex.Message}");
                OnSpeak?.Invoke("Error aborting mission", true);
            }
        }

        #endregion

        #region Autofail Value

        private void ExecuteSetAutofailValue(TICouncilorState councilor, float newValue)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot set autofail value: invalid state", true);
                    return;
                }

                // Verify this is a turned councilor we control
                if (councilor.agentForFaction != faction)
                {
                    OnSpeak?.Invoke("This is not a turned councilor under your control", true);
                    return;
                }

                var action = new SetAutofailValueForTurnedCouncilorAction(councilor, newValue);
                faction.playerControl.StartAction(action);

                string percentStr = $"{(newValue * 100):F0}%";
                OnSpeak?.Invoke($"Set mission failure rate to {percentStr} for {councilor.displayName}", true);
                MelonLogger.Msg($"Set autofail value: {councilor.displayName} = {percentStr}");

                // Refresh to show updated state
                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting autofail value: {ex.Message}");
                OnSpeak?.Invoke("Error setting mission failure rate", true);
            }
        }

        #endregion
    }
}
