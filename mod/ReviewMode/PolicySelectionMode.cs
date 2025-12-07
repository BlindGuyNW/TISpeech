using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Represents a policy option for selection.
    /// </summary>
    public class PolicyOption
    {
        public TIPolicyOption Policy { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsAllowed { get; set; }
    }

    /// <summary>
    /// Represents a policy target option.
    /// </summary>
    public class PolicyTargetOption
    {
        public TIGameState Target { get; set; }
        public string DisplayName { get; set; }
        public float? SuccessChance { get; set; }  // For policies that require agreement
        public TIFactionState ControllingFaction { get; set; }
    }

    /// <summary>
    /// State machine for the policy selection flow.
    /// </summary>
    public enum PolicySelectionState
    {
        SelectPolicy,
        SelectTarget,
        Confirm
    }

    /// <summary>
    /// Sub-mode for navigating the Set National Policy mission result.
    /// This handles the multi-step policy selection flow:
    /// 1. Select a policy from available options
    /// 2. Select a target (if policy requires one)
    /// 3. Confirm the policy
    /// </summary>
    public class PolicySelectionMode
    {
        public PolicySelectionState State { get; private set; }
        public List<PolicyOption> Policies { get; private set; }
        public List<PolicyTargetOption> Targets { get; private set; }
        public int CurrentIndex { get; private set; }

        private NotificationScreenController controller;
        private TINationState enactingNation;
        private TICouncilorState triggeringCouncilor;
        private TIPolicyOption selectedPolicy;
        private TIGameState selectedTarget;

        public int Count => State == PolicySelectionState.SelectPolicy ? Policies.Count :
                           State == PolicySelectionState.SelectTarget ? Targets.Count : 2; // Confirm/Cancel

        public string NationName => enactingNation?.displayName ?? "Unknown Nation";

        /// <summary>
        /// Check if the policy selection UI is currently visible.
        /// Used by ReviewModeController to detect if we should enter policy mode on activation.
        /// </summary>
        public static bool IsPolicySelectionVisible()
        {
            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<NotificationScreenController>();
                if (controller == null)
                    return false;

                // Check if the master policy panel is active
                return controller.masterPolicyPanelObject != null && controller.masterPolicyPanelObject.activeSelf;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the current state of the policy UI based on which panels are visible.
        /// </summary>
        public static PolicySelectionState? GetCurrentPolicyUIState(NotificationScreenController controller)
        {
            if (controller == null || controller.masterPolicyPanelObject == null || !controller.masterPolicyPanelObject.activeSelf)
                return null;

            if (controller.selectPolicyPanelObject != null && controller.selectPolicyPanelObject.activeSelf)
                return PolicySelectionState.SelectPolicy;

            if (controller.selectPolicyTargetPanelObject != null && controller.selectPolicyTargetPanelObject.activeSelf)
                return PolicySelectionState.SelectTarget;

            // If master panel is visible but neither sub-panel, we're likely in confirm state
            // However, confirm state uses a different confirm panel mechanism
            return PolicySelectionState.SelectPolicy; // Default to policy selection
        }

        /// <summary>
        /// Get the current nation, councilor, and policy from the NotificationScreenController's private fields.
        /// Returns null if the fields cannot be accessed.
        /// </summary>
        public static (TINationState nation, TICouncilorState councilor, TIPolicyOption currentPolicy, PolicySelectionState state)? GetPolicyContext(NotificationScreenController controller)
        {
            try
            {
                if (controller == null)
                    return null;

                // Access the private fields from NotificationScreenController
                var nationField = typeof(NotificationScreenController).GetField("currentNation", BindingFlags.NonPublic | BindingFlags.Instance);
                var councilorField = typeof(NotificationScreenController).GetField("currentPolicyCouncilor", BindingFlags.NonPublic | BindingFlags.Instance);
                var policyField = typeof(NotificationScreenController).GetField("currentPolicy", BindingFlags.NonPublic | BindingFlags.Instance);

                if (nationField == null || councilorField == null)
                {
                    MelonLogger.Warning("Could not find currentNation or currentPolicyCouncilor fields");
                    return null;
                }

                var nation = nationField.GetValue(controller) as TINationState;
                var councilor = councilorField.GetValue(controller) as TICouncilorState;
                var policy = policyField?.GetValue(controller) as TIPolicyOption;

                if (nation == null)
                {
                    MelonLogger.Warning("currentNation is null");
                    return null;
                }

                // Determine state based on which panel is visible
                var state = PolicySelectionState.SelectPolicy;
                if (controller.selectPolicyTargetPanelObject != null && controller.selectPolicyTargetPanelObject.activeSelf)
                {
                    state = PolicySelectionState.SelectTarget;
                }
                else if (controller.selectPolicyPanelObject != null && controller.selectPolicyPanelObject.activeSelf)
                {
                    state = PolicySelectionState.SelectPolicy;
                }

                return (nation, councilor, policy, state);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting policy context: {ex.Message}");
                return null;
            }
        }

        public PolicySelectionMode(NotificationScreenController controller, TINationState nation, TICouncilorState councilor)
            : this(controller, nation, councilor, PolicySelectionState.SelectPolicy, null)
        {
        }

        /// <summary>
        /// Constructor that supports restoring to a specific state (e.g., when re-entering review mode).
        /// </summary>
        public PolicySelectionMode(NotificationScreenController controller, TINationState nation, TICouncilorState councilor,
            PolicySelectionState initialState, TIPolicyOption currentPolicy)
        {
            this.controller = controller;
            this.enactingNation = nation;
            this.triggeringCouncilor = councilor;
            this.CurrentIndex = 0;

            Policies = new List<PolicyOption>();
            Targets = new List<PolicyTargetOption>();

            BuildPolicyList();

            // If restoring to SelectTarget state and we have a policy, set it up
            if (initialState == PolicySelectionState.SelectTarget && currentPolicy != null)
            {
                this.selectedPolicy = currentPolicy;
                this.State = PolicySelectionState.SelectTarget;
                BuildTargetList();
                MelonLogger.Msg($"PolicySelectionMode: Restored to SelectTarget state with policy {currentPolicy.GetDisplayName()}");
            }
            else
            {
                this.State = PolicySelectionState.SelectPolicy;
            }
        }

        private void BuildPolicyList()
        {
            try
            {
                // Get all policies that aren't handled at faction level
                var allPolicies = PolicyManager.policies.Values
                    .Where(x => !x.HandledAtFactionLevel())
                    .Cast<TIPolicyOption>()
                    .ToList();

                foreach (var policy in allPolicies)
                {
                    bool allowed = policy.Allowed(enactingNation);
                    Policies.Add(new PolicyOption
                    {
                        Policy = policy,
                        DisplayName = policy.GetDisplayName(),
                        Description = policy.GetDescription(),
                        IsAllowed = allowed
                    });
                }

                MelonLogger.Msg($"PolicySelectionMode: Built list of {Policies.Count} policies for {enactingNation.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building policy list: {ex.Message}");
            }
        }

        private void BuildTargetList()
        {
            Targets.Clear();

            try
            {
                if (selectedPolicy == null)
                    return;

                var possibleTargets = selectedPolicy.GetPossibleTargets(enactingNation);
                if (possibleTargets == null || possibleTargets.Count == 0)
                {
                    MelonLogger.Msg("No targets available for selected policy");
                    return;
                }

                // Check if policy requires target confirmation (shows success chance)
                var confirmablePolicy = selectedPolicy as TIPolicyOptionWithConfirm;

                // Sort by success chance if applicable
                IEnumerable<TIGameState> sortedTargets = possibleTargets;
                if (confirmablePolicy != null)
                {
                    sortedTargets = possibleTargets
                        .OrderByDescending(x => confirmablePolicy.AIAgreeChance(enactingNation, x))
                        .ThenBy(x => x.displayName);
                }

                foreach (var target in sortedTargets)
                {
                    var option = new PolicyTargetOption
                    {
                        Target = target,
                        DisplayName = GetTargetDisplayName(target)
                    };

                    // Get success chance if confirmable
                    if (confirmablePolicy != null)
                    {
                        option.SuccessChance = confirmablePolicy.AIAgreeChance(enactingNation, target);
                    }

                    // Get controlling faction for nations
                    var nation = target as TINationState;
                    if (nation?.executiveFaction != null)
                    {
                        option.ControllingFaction = nation.executiveFaction;
                    }

                    Targets.Add(option);
                }

                MelonLogger.Msg($"PolicySelectionMode: Built list of {Targets.Count} targets for {selectedPolicy.GetDisplayName()}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building target list: {ex.Message}");
            }
        }

        private string GetTargetDisplayName(TIGameState target)
        {
            try
            {
                // Handle different target types
                var war = target as TIWarState;
                if (war != null)
                {
                    var enemy = war.EnemyWarLeader(enactingNation, includeNonWarringAlliances: true);
                    return $"War with {enemy?.displayName ?? "Unknown"}";
                }

                var federation = target as TIFederationState;
                if (federation != null)
                {
                    return federation.displayName;
                }

                var nation = target as TINationState;
                if (nation != null)
                {
                    return nation.displayName;
                }

                var region = target as TIRegionState;
                if (region != null)
                {
                    return region.displayName;
                }

                var army = target as TIArmyState;
                if (army != null)
                {
                    return army.displayName;
                }

                return target.displayName ?? "Unknown";
            }
            catch
            {
                return target?.displayName ?? "Unknown";
            }
        }

        #region Navigation

        public void Next()
        {
            int count = GetCurrentCount();
            if (count == 0) return;
            CurrentIndex = (CurrentIndex + 1) % count;
        }

        public void Previous()
        {
            int count = GetCurrentCount();
            if (count == 0) return;
            CurrentIndex--;
            if (CurrentIndex < 0) CurrentIndex = count - 1;
        }

        private int GetCurrentCount()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    return Policies.Count;
                case PolicySelectionState.SelectTarget:
                    return Targets.Count;
                case PolicySelectionState.Confirm:
                    return 2; // Confirm and Cancel
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Find next item starting with the given letter.
        /// </summary>
        public int FindNextByLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);
            int count = GetCurrentCount();

            for (int i = 1; i <= count; i++)
            {
                int idx = (CurrentIndex + i) % count;
                string name = GetItemName(idx);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return idx;
            }

            return -1;
        }

        private string GetItemName(int index)
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    return index < Policies.Count ? Policies[index].DisplayName : "";
                case PolicySelectionState.SelectTarget:
                    return index < Targets.Count ? Targets[index].DisplayName : "";
                case PolicySelectionState.Confirm:
                    return index == 0 ? "Confirm" : "Cancel";
                default:
                    return "";
            }
        }

        public void SetIndex(int index)
        {
            if (index >= 0 && index < GetCurrentCount())
                CurrentIndex = index;
        }

        #endregion

        #region Activation

        /// <summary>
        /// Activate the current selection.
        /// Returns true if the mode should continue, false if it should exit.
        /// </summary>
        public bool Activate()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    return ActivatePolicy();
                case PolicySelectionState.SelectTarget:
                    return ActivateTarget();
                case PolicySelectionState.Confirm:
                    return ActivateConfirm();
                default:
                    return false;
            }
        }

        private bool ActivatePolicy()
        {
            if (CurrentIndex < 0 || CurrentIndex >= Policies.Count)
                return true;

            var policy = Policies[CurrentIndex];
            if (!policy.IsAllowed)
            {
                TISpeechMod.Speak($"{policy.DisplayName} is not available for {enactingNation.displayName}", interrupt: true);
                return true;
            }

            selectedPolicy = policy.Policy;
            MelonLogger.Msg($"Selected policy: {policy.DisplayName}");

            // Call the controller's PolicySelected method
            try
            {
                controller.PolicySelected(selectedPolicy);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error calling PolicySelected: {ex.Message}");
            }

            // Check if policy requires targets
            if (selectedPolicy.RequiresTargets())
            {
                BuildTargetList();
                if (Targets.Count > 0)
                {
                    State = PolicySelectionState.SelectTarget;
                    CurrentIndex = 0;
                    TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    return true;
                }
                else
                {
                    TISpeechMod.Speak("No valid targets for this policy", interrupt: true);
                    // Reset to policy selection
                    State = PolicySelectionState.SelectPolicy;
                    CurrentIndex = 0;
                    return true;
                }
            }
            else
            {
                // No targets needed, set target and go to confirm
                if (selectedPolicy.TargetsMyFederation)
                {
                    selectedTarget = enactingNation.federation;
                }
                else
                {
                    selectedTarget = enactingNation;
                }
                State = PolicySelectionState.Confirm;
                CurrentIndex = 0;
                TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                return true;
            }
        }

        private bool ActivateTarget()
        {
            if (CurrentIndex < 0 || CurrentIndex >= Targets.Count)
                return true;

            selectedTarget = Targets[CurrentIndex].Target;
            MelonLogger.Msg($"Selected target: {Targets[CurrentIndex].DisplayName}");

            // Call the controller's PolicyTargetSelected method
            try
            {
                controller.PolicyTargetSelected(selectedTarget);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error calling PolicyTargetSelected: {ex.Message}");
            }

            // Move to confirm state
            State = PolicySelectionState.Confirm;
            CurrentIndex = 0;
            TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
            return true;
        }

        private bool ActivateConfirm()
        {
            if (CurrentIndex == 0)
            {
                // Confirm
                try
                {
                    controller.OnConfirmPolicy();
                    TISpeechMod.Speak($"Policy enacted: {selectedPolicy.GetDisplayName()}", interrupt: true);
                    MelonLogger.Msg($"Confirmed policy: {selectedPolicy.GetDisplayName()}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error confirming policy: {ex.Message}");
                    TISpeechMod.Speak("Error confirming policy", interrupt: true);
                }
                return false; // Exit policy mode
            }
            else
            {
                // Cancel
                try
                {
                    controller.OnCancelPolicy();
                    TISpeechMod.Speak("Policy cancelled", interrupt: true);
                    MelonLogger.Msg("Cancelled policy selection");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error cancelling policy: {ex.Message}");
                }
                // Go back to target selection (or policy selection if no targets)
                if (selectedPolicy.RequiresTargets())
                {
                    State = PolicySelectionState.SelectTarget;
                    CurrentIndex = 0;
                    TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    return true;
                }
                else
                {
                    State = PolicySelectionState.SelectPolicy;
                    CurrentIndex = 0;
                    TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    return true;
                }
            }
        }

        /// <summary>
        /// Go back one step in the flow.
        /// Returns true if we backed up, false if we're at the beginning and should exit.
        /// </summary>
        public bool GoBack()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    // At the beginning - exit policy mode entirely
                    return false;

                case PolicySelectionState.SelectTarget:
                    // Go back to policy selection
                    try
                    {
                        controller.OnClickBackButton();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error clicking back: {ex.Message}");
                    }
                    State = PolicySelectionState.SelectPolicy;
                    CurrentIndex = 0;
                    selectedPolicy = null;
                    TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    return true;

                case PolicySelectionState.Confirm:
                    // Go back to target selection (or policy if no targets)
                    try
                    {
                        controller.OnCancelPolicy();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error cancelling: {ex.Message}");
                    }
                    if (selectedPolicy != null && selectedPolicy.RequiresTargets())
                    {
                        State = PolicySelectionState.SelectTarget;
                        CurrentIndex = 0;
                        TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    }
                    else
                    {
                        State = PolicySelectionState.SelectPolicy;
                        CurrentIndex = 0;
                        selectedPolicy = null;
                        TISpeechMod.Speak(GetEntryAnnouncement(), interrupt: true);
                    }
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region Announcements

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();

            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    sb.Append($"Select policy for {enactingNation.displayName}. ");
                    sb.Append($"{Policies.Count} policies available. ");
                    if (Policies.Count > 0)
                    {
                        sb.Append($"1 of {Policies.Count}: {Policies[0].DisplayName}");
                        if (!Policies[0].IsAllowed)
                            sb.Append(" (unavailable)");
                    }
                    sb.Append(". Use up/down to navigate, Enter to select, Escape to cancel.");
                    break;

                case PolicySelectionState.SelectTarget:
                    sb.Append($"Select target for {selectedPolicy.GetDisplayName()}. ");
                    sb.Append($"{Targets.Count} targets available. ");
                    if (Targets.Count > 0)
                    {
                        sb.Append($"1 of {Targets.Count}: {Targets[0].DisplayName}");
                        if (Targets[0].SuccessChance.HasValue)
                            sb.Append($", {Targets[0].SuccessChance.Value:P0} chance");
                    }
                    sb.Append(". Use up/down to navigate, Enter to select, Escape to go back.");
                    break;

                case PolicySelectionState.Confirm:
                    string targetName = selectedTarget?.displayName ?? "";
                    sb.Append($"Confirm {selectedPolicy.GetDisplayName()}");
                    if (!string.IsNullOrEmpty(targetName))
                        sb.Append($" targeting {targetName}");
                    sb.Append("? ");
                    sb.Append("1 of 2: Confirm. Use up/down to switch, Enter to select.");
                    break;
            }

            return sb.ToString();
        }

        public string GetCurrentAnnouncement()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    if (CurrentIndex < 0 || CurrentIndex >= Policies.Count)
                        return "No policy selected";
                    var policy = Policies[CurrentIndex];
                    string policyAnnouncement = $"{CurrentIndex + 1} of {Policies.Count}: {policy.DisplayName}";
                    if (!policy.IsAllowed)
                        policyAnnouncement += " (unavailable)";
                    return policyAnnouncement;

                case PolicySelectionState.SelectTarget:
                    if (CurrentIndex < 0 || CurrentIndex >= Targets.Count)
                        return "No target selected";
                    var target = Targets[CurrentIndex];
                    string targetAnnouncement = $"{CurrentIndex + 1} of {Targets.Count}: {target.DisplayName}";
                    if (target.SuccessChance.HasValue)
                        targetAnnouncement += $", {target.SuccessChance.Value:P0} success";
                    if (target.ControllingFaction != null)
                        targetAnnouncement += $", controlled by {target.ControllingFaction.displayName}";
                    return targetAnnouncement;

                case PolicySelectionState.Confirm:
                    return CurrentIndex == 0 ? "1 of 2: Confirm" : "2 of 2: Cancel";

                default:
                    return "Unknown state";
            }
        }

        public string GetCurrentDetail()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    if (CurrentIndex < 0 || CurrentIndex >= Policies.Count)
                        return "No policy selected";
                    var policy = Policies[CurrentIndex];
                    var sb = new StringBuilder();
                    sb.Append(policy.DisplayName);
                    if (!string.IsNullOrEmpty(policy.Description))
                    {
                        sb.Append(". ");
                        sb.Append(TISpeechMod.CleanText(policy.Description));
                    }
                    if (!policy.IsAllowed)
                    {
                        sb.Append(". This policy is not currently available for ");
                        sb.Append(enactingNation.displayName);
                    }
                    return sb.ToString();

                case PolicySelectionState.SelectTarget:
                    if (CurrentIndex < 0 || CurrentIndex >= Targets.Count)
                        return "No target selected";
                    var target = Targets[CurrentIndex];
                    var tsb = new StringBuilder();
                    tsb.Append(target.DisplayName);
                    if (target.SuccessChance.HasValue)
                    {
                        tsb.Append($". {target.SuccessChance.Value:P0} chance of acceptance");
                    }
                    if (target.ControllingFaction != null)
                    {
                        tsb.Append($". Controlled by {target.ControllingFaction.displayName}");
                    }
                    return tsb.ToString();

                case PolicySelectionState.Confirm:
                    string targetName = selectedTarget?.displayName ?? "";
                    string confirmText = selectedPolicy.GetConfirmPrompt(enactingNation, selectedTarget);
                    return TISpeechMod.CleanText(confirmText);

                default:
                    return "Unknown state";
            }
        }

        public string ListAll()
        {
            switch (State)
            {
                case PolicySelectionState.SelectPolicy:
                    var pNames = Policies.Select(p => p.DisplayName + (p.IsAllowed ? "" : " (unavailable)"));
                    return $"{Policies.Count} policies: {string.Join(", ", pNames)}";

                case PolicySelectionState.SelectTarget:
                    var tNames = Targets.Select(t => t.DisplayName);
                    return $"{Targets.Count} targets: {string.Join(", ", tNames)}";

                case PolicySelectionState.Confirm:
                    return "Options: Confirm, Cancel";

                default:
                    return "Unknown state";
            }
        }

        #endregion
    }
}
