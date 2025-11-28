using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Screens;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Main controller for review mode.
    /// Uses hierarchical navigation through screens, items, sections, and section items.
    /// </summary>
    public class ReviewModeController : MonoBehaviour
    {
        private static ReviewModeController instance;
        public static ReviewModeController Instance => instance;

        private bool isActive = false;
        public bool IsActive => isActive;

        // Hierarchical navigation state
        private NavigationState navigation = new NavigationState();

        // Available screens
        private CouncilScreen councilScreen;
        private TechnologyScreen technologyScreen;
        private NationScreen nationScreen;
        private OrgMarketScreen orgMarketScreen;

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
                InitializeScreens();
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

            MelonLogger.Msg("ReviewModeController created with hierarchical screen navigation");
            return controller;
        }

        private void InitializeScreens()
        {
            // Create screens
            councilScreen = new CouncilScreen();
            councilScreen.OnEnterSelectionMode = EnterSelectionMode;
            councilScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            technologyScreen = new TechnologyScreen();
            technologyScreen.OnEnterSelectionMode = EnterSelectionMode;
            technologyScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            technologyScreen.OnSectionsInvalidated = () => navigation.RefreshSections();

            nationScreen = new NationScreen();
            nationScreen.OnEnterSelectionMode = EnterSelectionMode;
            nationScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            orgMarketScreen = new OrgMarketScreen();
            orgMarketScreen.OnEnterSelectionMode = EnterSelectionMode;
            orgMarketScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            // Register screens with navigation
            var screens = new List<ScreenBase>
            {
                councilScreen,
                technologyScreen,
                nationScreen,
                orgMarketScreen
            };

            navigation.RegisterScreens(screens);
            MelonLogger.Msg($"Registered {screens.Count} screens for review mode");
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
                    inputHandled = HandleNavigationInput();
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

                // Reset navigation to initial state (Council screen)
                navigation.Reset();

                // Announce activation
                var screen = navigation.CurrentScreen;
                if (screen != null)
                {
                    string announcement = $"Review mode. {screen.GetActivationAnnouncement()} ";
                    announcement += "Use Numpad 8/2 to navigate, Enter to drill in, Escape to back out.";
                    TISpeechMod.Speak(announcement, interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Review mode. No screens available.", interrupt: true);
                }

                MelonLogger.Msg("Review mode activated with hierarchical navigation");
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

        private bool HandleNavigationInput()
        {
            // Navigate up/previous (Numpad 8)
            if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                navigation.Previous();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Navigate down/next (Numpad 2)
            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                navigation.Next();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Alternative item navigation (Numpad 4/6) - same as 8/2 for consistency
            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                navigation.Previous();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                navigation.Next();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Drill down / Activate (Numpad Enter or Numpad 5)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                var result = navigation.DrillDown();
                switch (result)
                {
                    case DrillResult.Drilled:
                        // Actually drilled into a new level - announce new position
                        TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                        break;
                    case DrillResult.Activated:
                        // Item was activated - don't re-announce, the action handles its own speech
                        break;
                    case DrillResult.Nothing:
                        // Couldn't drill or activate - re-read current position
                        TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                        break;
                }
                return true;
            }

            // Back out (Escape only - Numpad 0 is used for toggle)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (navigation.BackOut())
                {
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // At top level - deactivate review mode
                    DeactivateReviewMode();
                }
                return true;
            }

            // Read detail (Numpad *)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(navigation.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List items at current level (Numpad /)
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                TISpeechMod.Speak(navigation.ListCurrentLevel(), interrupt: true);
                return true;
            }

            // PageUp/PageDown for quick screen switching (when at Screens level)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                if (navigation.CurrentLevel == NavigationLevel.Screens)
                {
                    navigation.Previous();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // Back out to screens level
                    while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.BackOut()) { }
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                if (navigation.CurrentLevel == NavigationLevel.Screens)
                {
                    navigation.Next();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // Back out to screens level
                    while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.BackOut()) { }
                    navigation.Next();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                return true;
            }

            // Confirm Assignments (Numpad +) - global action during mission phase
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                HandleConfirmAssignments();
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

            // Read detail (Numpad *) - show full modifier breakdown
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                AnnounceSelectionDetail();
                return true;
            }

            return false;
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
            var firstOption = selectionMode.CurrentOption;
            // Combine prompt with first item into single announcement to avoid interruption
            string announcement = $"{prompt}. {options.Count} options. 1 of {options.Count}: {firstOption.Label}. Use up/down to browse, Enter to select, * for detail, Escape to cancel.";
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private void AnnounceSelectionItem()
        {
            if (selectionMode == null) return;

            var option = selectionMode.CurrentOption;
            TISpeechMod.Speak($"{selectionMode.CurrentIndex + 1} of {selectionMode.Count}: {option.Label}", interrupt: true);
        }

        private void AnnounceSelectionDetail()
        {
            if (selectionMode == null) return;

            var option = selectionMode.CurrentOption;
            string detail = !string.IsNullOrEmpty(option.DetailText) ? option.DetailText : option.Label;
            TISpeechMod.Speak(detail, interrupt: true);
        }

        private void ConfirmSelection()
        {
            if (selectionMode == null) return;

            int selectedIndex = selectionMode.CurrentIndex;
            var onSelect = selectionMode.OnSelect;

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

        #region Global Actions

        private void HandleConfirmAssignments()
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                {
                    TISpeechMod.Speak("No active player", interrupt: true);
                    return;
                }

                // Check if we're in mission phase
                var missionPhase = GameStateManager.MissionPhase();
                if (missionPhase == null || !missionPhase.phaseActive)
                {
                    TISpeechMod.Speak("Not in mission phase", interrupt: true);
                    return;
                }

                // Check if already confirmed
                if (missionPhase.factionsSignallingComplete.Contains(faction))
                {
                    TISpeechMod.Speak("Assignments already confirmed", interrupt: true);
                    return;
                }

                // Get councilor status
                var activeCouncilors = faction.activeCouncilors;
                int total = activeCouncilors.Count;
                int assigned = activeCouncilors.Count(c => c.HasMission);
                int unassigned = total - assigned;

                string statusMessage;
                if (unassigned == 0)
                {
                    statusMessage = $"All {total} councilors have missions assigned";
                }
                else
                {
                    statusMessage = $"{assigned} of {total} councilors have missions. {unassigned} unassigned";
                }

                // Request confirmation
                ConfirmationHelper.RequestConfirmation(
                    "Confirm assignments",
                    statusMessage,
                    EnterSelectionMode,
                    onConfirm: () => PerformConfirmAssignments(faction),
                    onCancel: () => TISpeechMod.Speak("Cancelled", interrupt: true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in HandleConfirmAssignments: {ex.Message}");
                TISpeechMod.Speak("Error checking assignments", interrupt: true);
            }
        }

        private void PerformConfirmAssignments(TIFactionState faction)
        {
            try
            {
                var action = new FinalizeCouncilorMissions(faction);
                faction.playerControl.StartAction(action);

                TISpeechMod.Speak("Assignments confirmed. Missions will now execute.", interrupt: true);
                MelonLogger.Msg("Confirmed councilor mission assignments via review mode");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error confirming assignments: {ex.Message}");
                TISpeechMod.Speak("Error confirming assignments", interrupt: true);
            }
        }

        #endregion

        #region Helpers

        private bool IsGameReady()
        {
            return GameControl.control != null &&
                   GameControl.control.activePlayer != null;
        }

        #endregion
    }

    #region Selection Sub-Mode Types

    /// <summary>
    /// Option for selection mode with optional detail text.
    /// </summary>
    public class SelectionOption
    {
        public string Label { get; set; }
        public string DetailText { get; set; }
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
