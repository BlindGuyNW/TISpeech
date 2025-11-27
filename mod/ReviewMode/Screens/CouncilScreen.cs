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
    }
}
