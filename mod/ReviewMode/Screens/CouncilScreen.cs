using System;
using System.Collections.Generic;
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
    /// </summary>
    public class CouncilScreen : ScreenBase
    {
        // Item types in the list
        private enum ItemType { Councilor, RecruitmentDivider, RecruitCandidate }
        private class CouncilItem
        {
            public ItemType Type;
            public TICouncilorState Councilor; // For Councilor or RecruitCandidate types
        }

        private List<CouncilItem> items = new List<CouncilItem>();

        private readonly CouncilorReader councilorReader = new CouncilorReader();
        private readonly RecruitCandidateReader recruitReader = new RecruitCandidateReader();
        private readonly MissionModifierReader modifierReader = new MissionModifierReader();

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

        public override string Name => "Council";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int current = faction.councilors?.Count ?? 0;
                    int max = faction.maxCouncilSize;
                    int candidates = faction.availableCouncilors?.Count ?? 0;
                    return $"{current}/{max} councilors, {candidates} recruitment candidates";
                }
                return "Manage your councilors and recruit new ones";
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
                    // Add divider
                    items.Add(new CouncilItem { Type = ItemType.RecruitmentDivider });

                    // Add each candidate as a full item
                    foreach (var candidate in faction.availableCouncilors)
                    {
                        items.Add(new CouncilItem { Type = ItemType.RecruitCandidate, Councilor = candidate });
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing council screen: {ex.Message}");
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

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.Councilor:
                    return councilorReader.ReadSummary(item.Councilor);

                case ItemType.RecruitmentDivider:
                    var faction = GameControl.control?.activePlayer;
                    int candidateCount = faction?.availableCouncilors?.Count ?? 0;
                    int slots = (faction?.maxCouncilSize ?? 6) - (faction?.councilors?.Count ?? 0);
                    return $"--- Recruitment Pool: {candidateCount} candidates, {slots} slots available ---";

                case ItemType.RecruitCandidate:
                    return "Recruit: " + recruitReader.ReadSummary(item.Councilor);

                default:
                    return "Unknown";
            }
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.Councilor:
                    return councilorReader.ReadDetail(item.Councilor);

                case ItemType.RecruitmentDivider:
                    return recruitReader.GetRecruitmentStatus(GameControl.control?.activePlayer);

                case ItemType.RecruitCandidate:
                    return recruitReader.ReadDetail(item.Councilor);

                default:
                    return "Unknown";
            }
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cache if available
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.Councilor:
                    cachedSections = councilorReader.GetSections(item.Councilor);
                    break;

                case ItemType.RecruitmentDivider:
                    // Divider has no sections - just a marker
                    cachedSections = new List<ISection>();
                    break;

                case ItemType.RecruitCandidate:
                    cachedSections = recruitReader.GetSections(item.Councilor);
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

            // Divider can't be drilled into
            if (items[index].Type == ItemType.RecruitmentDivider)
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
            foreach (var target in targets)
            {
                string label = target.displayName ?? "Unknown target";

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
                    ExecuteMissionAssignment(councilor, mission, selectedTarget);
                }
            );
        }

        private void ExecuteMissionAssignment(TICouncilorState councilor, TIMissionTemplate mission, TIGameState target)
        {
            try
            {
                var faction = councilor.faction;
                var action = new AssignCouncilorToMission(councilor, mission, target, 0f, false);
                faction.playerControl.StartAction(action);

                string announcement = $"Assigned {councilor.displayName} to {mission.displayName}";
                if (target != null)
                    announcement += $" targeting {target.displayName}";

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
    }
}
