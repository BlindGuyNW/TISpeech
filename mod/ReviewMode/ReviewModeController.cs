using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Main controller for review mode.
    /// Provides direct access to game state without relying on UI structure.
    /// </summary>
    public class ReviewModeController : MonoBehaviour
    {
        private static ReviewModeController instance;
        public static ReviewModeController Instance => instance;

        private bool isActive = false;
        public bool IsActive => isActive;

        // Current councilor being reviewed
        private int currentCouncilorIndex = 0;
        private TICouncilorState currentCouncilor = null;

        // Sections for current councilor
        private List<ISection> sections = new List<ISection>();
        private int currentSectionIndex = 0;
        private int currentItemIndex = 0;

        // Selection sub-mode (for multi-step actions like mission assignment)
        private SelectionSubMode selectionMode = null;

        // Debouncing
        private float lastInputTime = 0f;
        private const float INPUT_DEBOUNCE = 0.15f;

        #region Lifecycle

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static ReviewModeController Create()
        {
            if (instance != null)
                return instance;

            var go = new GameObject("TISpeech_ReviewMode");
            var controller = go.AddComponent<ReviewModeController>();
            instance = controller;
            UnityEngine.Object.DontDestroyOnLoad(go);

            MelonLogger.Msg("ReviewModeController created with direct game state access");
            return controller;
        }

        #endregion

        #region Public API

        public void Toggle()
        {
            if (isActive)
                DeactivateReviewMode();
            else
                ActivateReviewMode();
        }

        public void CheckInput()
        {
            if (!TISpeechMod.IsReady || !isActive)
                return;

            try
            {
                float currentTime = Time.unscaledTime;
                if (currentTime - lastInputTime < INPUT_DEBOUNCE)
                    return;

                bool inputHandled = false;

                // If in selection sub-mode, handle input there
                if (selectionMode != null)
                {
                    inputHandled = HandleSelectionModeInput();
                }
                else
                {
                    inputHandled = HandleNormalModeInput();
                }

                if (inputHandled)
                {
                    lastInputTime = currentTime;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ReviewModeController.CheckInput: {ex.Message}");
            }
        }

        #endregion

        #region Mode Activation

        private void ActivateReviewMode()
        {
            try
            {
                if (!IsGameReady())
                {
                    TISpeechMod.Speak("Game not ready for review mode", interrupt: true);
                    return;
                }

                TIInputManager.BlockKeybindings();
                isActive = true;

                // Initialize with first councilor
                var councilors = GetCouncilors();
                if (councilors.Count > 0)
                {
                    currentCouncilorIndex = 0;
                    RefreshCurrentCouncilor();

                    string announcement = $"Review mode. {councilors.Count} councilors. ";
                    announcement += $"Councilor {currentCouncilorIndex + 1}: {currentCouncilor.displayName}. ";
                    announcement += $"{sections.Count} sections. ";
                    announcement += "Use Page Up/Down for councilors, Numpad 8/2 for sections, 4/6 for items, Enter to activate.";
                    TISpeechMod.Speak(announcement, interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Review mode. No councilors available.", interrupt: true);
                }

                MelonLogger.Msg("Review mode activated");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating review mode: {ex.Message}");
                TISpeechMod.Speak("Error activating review mode", interrupt: true);
            }
        }

        private void DeactivateReviewMode()
        {
            try
            {
                TIInputManager.RestoreKeybindings();
                isActive = false;
                selectionMode = null;

                TISpeechMod.Speak("Review mode off", interrupt: true);
                MelonLogger.Msg("Review mode deactivated");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error deactivating review mode: {ex.Message}");
            }
        }

        #endregion

        #region Input Handling

        private bool HandleNormalModeInput()
        {
            // Councilor navigation (PageUp/PageDown)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                PreviousCouncilor();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                NextCouncilor();
                return true;
            }

            // Section navigation (Numpad 8/2)
            if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                PreviousSection();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                NextSection();
                return true;
            }

            // Item navigation (Numpad 4/6)
            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                PreviousItem();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                NextItem();
                return true;
            }

            // Activate (Numpad Enter or Numpad 5)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                ActivateCurrentItem();
                return true;
            }

            // Read current (Numpad *)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                ReadCurrentState();
                return true;
            }

            // List sections (Numpad /)
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                ListSections();
                return true;
            }

            return false;
        }

        private bool HandleSelectionModeInput()
        {
            // Navigate options (Numpad 8/2 or 4/6)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                selectionMode.Previous();
                AnnounceSelectionItem();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                selectionMode.Next();
                AnnounceSelectionItem();
                return true;
            }

            // Confirm selection (Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                ConfirmSelection();
                return true;
            }

            // Cancel (Escape or Numpad 0)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                CancelSelection();
                return true;
            }

            // Read current selection (Numpad *)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                AnnounceSelectionItem();
                return true;
            }

            return false;
        }

        #endregion

        #region Councilor Navigation

        private void PreviousCouncilor()
        {
            var councilors = GetCouncilors();
            if (councilors.Count == 0)
            {
                TISpeechMod.Speak("No councilors", interrupt: true);
                return;
            }

            currentCouncilorIndex--;
            if (currentCouncilorIndex < 0)
                currentCouncilorIndex = councilors.Count - 1;

            RefreshCurrentCouncilor();
            AnnounceCouncilor();
        }

        private void NextCouncilor()
        {
            var councilors = GetCouncilors();
            if (councilors.Count == 0)
            {
                TISpeechMod.Speak("No councilors", interrupt: true);
                return;
            }

            currentCouncilorIndex++;
            if (currentCouncilorIndex >= councilors.Count)
                currentCouncilorIndex = 0;

            RefreshCurrentCouncilor();
            AnnounceCouncilor();
        }

        private void AnnounceCouncilor()
        {
            var councilors = GetCouncilors();
            string announcement = $"Councilor {currentCouncilorIndex + 1} of {councilors.Count}: {currentCouncilor.displayName}";

            // Add current mission if any
            if (currentCouncilor.activeMission != null)
            {
                announcement += $", {currentCouncilor.activeMission.missionTemplate.displayName}";
            }
            else
            {
                announcement += ", no mission";
            }

            TISpeechMod.Speak(announcement, interrupt: true);
        }

        #endregion

        #region Section/Item Navigation

        private void PreviousSection()
        {
            if (sections.Count == 0)
            {
                TISpeechMod.Speak("No sections", interrupt: true);
                return;
            }

            currentSectionIndex--;
            if (currentSectionIndex < 0)
                currentSectionIndex = sections.Count - 1;
            currentItemIndex = 0;

            AnnounceCurrentSection();
        }

        private void NextSection()
        {
            if (sections.Count == 0)
            {
                TISpeechMod.Speak("No sections", interrupt: true);
                return;
            }

            currentSectionIndex++;
            if (currentSectionIndex >= sections.Count)
                currentSectionIndex = 0;
            currentItemIndex = 0;

            AnnounceCurrentSection();
        }

        private void PreviousItem()
        {
            var section = GetCurrentSection();
            if (section == null || section.ItemCount == 0)
            {
                TISpeechMod.Speak("No items", interrupt: true);
                return;
            }

            currentItemIndex--;
            if (currentItemIndex < 0)
                currentItemIndex = section.ItemCount - 1;

            AnnounceCurrentItem();
        }

        private void NextItem()
        {
            var section = GetCurrentSection();
            if (section == null || section.ItemCount == 0)
            {
                TISpeechMod.Speak("No items", interrupt: true);
                return;
            }

            currentItemIndex++;
            if (currentItemIndex >= section.ItemCount)
                currentItemIndex = 0;

            AnnounceCurrentItem();
        }

        private void ActivateCurrentItem()
        {
            var section = GetCurrentSection();
            if (section == null || section.ItemCount == 0)
            {
                TISpeechMod.Speak("No item to activate", interrupt: true);
                return;
            }

            if (!section.CanActivate(currentItemIndex))
            {
                // Just re-read the item if not activatable
                AnnounceCurrentItem();
                return;
            }

            try
            {
                section.Activate(currentItemIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating item: {ex.Message}");
                TISpeechMod.Speak("Error activating item", interrupt: true);
            }
        }

        private ISection GetCurrentSection()
        {
            if (sections.Count == 0 || currentSectionIndex < 0 || currentSectionIndex >= sections.Count)
                return null;
            return sections[currentSectionIndex];
        }

        private void AnnounceCurrentSection()
        {
            var section = GetCurrentSection();
            if (section == null) return;

            TISpeechMod.Speak($"{section.Name}, {section.ItemCount} items", interrupt: true);
        }

        private void AnnounceCurrentItem()
        {
            var section = GetCurrentSection();
            if (section == null || section.ItemCount == 0) return;

            string itemText = section.ReadItem(currentItemIndex);
            bool canActivate = section.CanActivate(currentItemIndex);
            string suffix = canActivate ? " (press Enter to activate)" : "";

            TISpeechMod.Speak($"{currentItemIndex + 1} of {section.ItemCount}: {itemText}{suffix}", interrupt: true);
        }

        private void ListSections()
        {
            if (sections.Count == 0)
            {
                TISpeechMod.Speak("No sections", interrupt: true);
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"{sections.Count} sections: ");
            for (int i = 0; i < sections.Count; i++)
            {
                if (i == currentSectionIndex)
                    sb.Append($"{sections[i].Name} (current), ");
                else
                    sb.Append($"{sections[i].Name}, ");
            }

            TISpeechMod.Speak(sb.ToString().TrimEnd(',', ' '), interrupt: true);
        }

        private void ReadCurrentState()
        {
            if (currentCouncilor == null)
            {
                TISpeechMod.Speak("No councilor selected", interrupt: true);
                return;
            }

            var section = GetCurrentSection();
            if (section != null)
            {
                TISpeechMod.Speak($"{currentCouncilor.displayName}, {section.Name}: {section.ReadSummary()}", interrupt: true);
            }
            else
            {
                TISpeechMod.Speak(currentCouncilor.displayName, interrupt: true);
            }
        }

        #endregion

        #region Selection Sub-Mode

        public void EnterSelectionMode(string prompt, List<SelectionOption> options, Action<int> onSelect)
        {
            if (options.Count == 0)
            {
                TISpeechMod.Speak("No options available", interrupt: true);
                return;
            }

            selectionMode = new SelectionSubMode(prompt, options, onSelect);
            TISpeechMod.Speak($"{prompt}. {options.Count} options. Use up/down to browse, Enter to select, Escape to cancel.", interrupt: true);
            AnnounceSelectionItem();
        }

        private void AnnounceSelectionItem()
        {
            if (selectionMode == null) return;

            var option = selectionMode.CurrentOption;
            TISpeechMod.Speak($"{selectionMode.CurrentIndex + 1} of {selectionMode.Count}: {option.Label}", interrupt: true);
        }

        private void ConfirmSelection()
        {
            if (selectionMode == null) return;

            int selectedIndex = selectionMode.CurrentIndex;
            var onSelect = selectionMode.OnSelect;
            string label = selectionMode.CurrentOption.Label;

            selectionMode = null;

            try
            {
                onSelect(selectedIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing selection: {ex.Message}");
                TISpeechMod.Speak("Error executing action", interrupt: true);
            }
        }

        private void CancelSelection()
        {
            selectionMode = null;
            TISpeechMod.Speak("Cancelled", interrupt: true);
        }

        #endregion

        #region Build Sections from Game State

        private void RefreshCurrentCouncilor()
        {
            var councilors = GetCouncilors();
            if (councilors.Count == 0 || currentCouncilorIndex < 0 || currentCouncilorIndex >= councilors.Count)
            {
                currentCouncilor = null;
                sections.Clear();
                return;
            }

            currentCouncilor = councilors[currentCouncilorIndex];
            BuildSectionsForCouncilor(currentCouncilor);
            currentSectionIndex = 0;
            currentItemIndex = 0;
        }

        private void BuildSectionsForCouncilor(TICouncilorState councilor)
        {
            sections.Clear();

            // Info section
            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", councilor.displayName);
            infoSection.AddItem("Type", councilor.typeTemplate?.displayName ?? "Unknown");

            if (councilor.activeMission != null)
            {
                var mission = councilor.activeMission;
                string missionInfo = mission.missionTemplate.displayName;
                if (mission.target != null)
                    missionInfo += $" on {mission.target.displayName}";
                infoSection.AddItem("Current Mission", missionInfo);
            }
            else
            {
                infoSection.AddItem("Current Mission", "None");
            }

            infoSection.AddItem("Location", GetLocationString(councilor));
            sections.Add(infoSection);

            // Stats section
            var statsSection = new DataSection("Stats");
            statsSection.AddItem("Persuasion", councilor.GetAttribute(CouncilorAttribute.Persuasion).ToString());
            statsSection.AddItem("Investigation", councilor.GetAttribute(CouncilorAttribute.Investigation).ToString());
            statsSection.AddItem("Espionage", councilor.GetAttribute(CouncilorAttribute.Espionage).ToString());
            statsSection.AddItem("Command", councilor.GetAttribute(CouncilorAttribute.Command).ToString());
            statsSection.AddItem("Administration", councilor.GetAttribute(CouncilorAttribute.Administration).ToString());
            statsSection.AddItem("Science", councilor.GetAttribute(CouncilorAttribute.Science).ToString());
            statsSection.AddItem("Security", councilor.GetAttribute(CouncilorAttribute.Security).ToString());
            statsSection.AddItem("Loyalty", councilor.GetAttribute(CouncilorAttribute.Loyalty).ToString());
            sections.Add(statsSection);

            // Traits section
            if (councilor.traits != null && councilor.traits.Count > 0)
            {
                var traitsSection = new DataSection("Traits");
                foreach (var trait in councilor.traits)
                {
                    traitsSection.AddItem(trait.displayName);
                }
                sections.Add(traitsSection);
            }

            // Orgs section
            if (councilor.orgs != null && councilor.orgs.Count > 0)
            {
                var orgsSection = new DataSection("Organizations");
                foreach (var org in councilor.orgs)
                {
                    orgsSection.AddItem(org.displayName);
                }
                sections.Add(orgsSection);
            }

            // Missions section (actionable)
            // Filter to only missions that can be afforded AND have valid targets
            var missionsSection = new DataSection("Assign Mission");
            var possibleMissions = councilor.GetPossibleMissionList(filterForCouncilorConditions: true, sort: true);
            int actionableMissionCount = 0;

            foreach (var mission in possibleMissions)
            {
                try
                {
                    // Check if mission can be afforded and has valid targets (same check as UI)
                    bool canAfford = mission.CanAfford(councilor.faction, councilor);
                    int targetCount = mission.target?.GetValidTargets(mission, councilor)?.Count ?? 0;

                    if (canAfford && targetCount > 0)
                    {
                        // Capture for closure
                        var m = mission;
                        var c = councilor;

                        missionsSection.AddItem(mission.displayName, onActivate: () =>
                        {
                            StartMissionAssignment(c, m);
                        });
                        actionableMissionCount++;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error checking mission {mission.displayName}: {ex.Message}");
                }
            }

            if (actionableMissionCount == 0)
            {
                missionsSection.AddItem("No available missions");
            }
            sections.Add(missionsSection);

            // Automation section
            var automationSection = new DataSection("Automation");
            string autoStatus = councilor.permanentDefenseMode ? "Enabled" : "Disabled";
            automationSection.AddItem("Auto-assign missions", autoStatus, onActivate: () =>
            {
                ToggleAutomation(councilor);
            });
            sections.Add(automationSection);

            MelonLogger.Msg($"Built {sections.Count} sections for {councilor.displayName}");
        }

        private string GetLocationString(TICouncilorState councilor)
        {
            try
            {
                if (councilor.location != null)
                    return councilor.location.displayName ?? "Unknown location";
                return "Unknown location";
            }
            catch
            {
                return "Unknown location";
            }
        }

        #endregion

        #region Actions

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
                TISpeechMod.Speak($"No valid targets for {mission.displayName}", interrupt: true);
                return;
            }

            // Build selection options with success chance
            var options = new List<SelectionOption>();
            foreach (var target in targets)
            {
                string label = target.displayName ?? "Unknown target";

                // Try to get success chance
                try
                {
                    if (mission.resolutionMethod != null)
                    {
                        string successChance = mission.resolutionMethod.GetSuccessChanceString(mission, councilor, target, 0f);
                        if (!string.IsNullOrEmpty(successChance))
                        {
                            label += $", {successChance}";
                        }
                    }
                }
                catch
                {
                    // Some missions may not have contested resolution
                }

                options.Add(new SelectionOption
                {
                    Label = label,
                    Data = target
                });
            }

            // Enter selection mode
            EnterSelectionMode($"Select target for {mission.displayName}", options, (index) =>
            {
                var selectedTarget = (TIGameState)options[index].Data;
                ExecuteMissionAssignment(councilor, mission, selectedTarget);
            });
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

                TISpeechMod.Speak(announcement, interrupt: true);
                MelonLogger.Msg(announcement);

                // Refresh to show updated state
                RefreshCurrentCouncilor();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing mission assignment: {ex.Message}");
                TISpeechMod.Speak("Error assigning mission", interrupt: true);
            }
        }

        private void ToggleAutomation(TICouncilorState councilor)
        {
            try
            {
                bool newState = !councilor.permanentDefenseMode;
                var action = new ToggleAutomateCouncilorAction(councilor, newState);
                councilor.faction.playerControl.StartAction(action);

                string status = newState ? "enabled" : "disabled";
                TISpeechMod.Speak($"Automation {status} for {councilor.displayName}", interrupt: true);

                // Refresh
                RefreshCurrentCouncilor();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling automation: {ex.Message}");
                TISpeechMod.Speak("Error toggling automation", interrupt: true);
            }
        }

        #endregion

        #region Helpers

        private bool IsGameReady()
        {
            return GameControl.control != null &&
                   GameControl.control.activePlayer != null;
        }

        private List<TICouncilorState> GetCouncilors()
        {
            if (!IsGameReady())
                return new List<TICouncilorState>();

            return GameControl.control.activePlayer.councilors ?? new List<TICouncilorState>();
        }

        #endregion
    }

    #region Selection Sub-Mode Types

    public class SelectionOption
    {
        public string Label { get; set; }
        public object Data { get; set; }
    }

    public class SelectionSubMode
    {
        public string Prompt { get; }
        public List<SelectionOption> Options { get; }
        public Action<int> OnSelect { get; }
        public int CurrentIndex { get; private set; }

        public int Count => Options.Count;
        public SelectionOption CurrentOption => Options[CurrentIndex];

        public SelectionSubMode(string prompt, List<SelectionOption> options, Action<int> onSelect)
        {
            Prompt = prompt;
            Options = options;
            OnSelect = onSelect;
            CurrentIndex = 0;
        }

        public void Previous()
        {
            CurrentIndex--;
            if (CurrentIndex < 0)
                CurrentIndex = Options.Count - 1;
        }

        public void Next()
        {
            CurrentIndex++;
            if (CurrentIndex >= Options.Count)
                CurrentIndex = 0;
        }
    }

    #endregion
}
