using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
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

        // Grid sub-mode (for 2D priority grid navigation)
        private GridSubMode gridMode = null;

        // Notification sub-mode (for navigating notification popups)
        private NotificationSubMode notificationMode = null;
        public bool IsInNotificationMode => notificationMode != null;

        // Policy selection sub-mode (for Set National Policy mission results)
        private PolicySelectionMode policyMode = null;
        public bool IsInPolicyMode => policyMode != null;

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

                // Priority order: Policy > Notification > Selection > Grid > Navigation
                // Policy mode takes highest priority (handles Set National Policy mission results)
                if (policyMode != null)
                {
                    inputHandled = HandlePolicyModeInput();
                }
                // If in notification sub-mode, handle notification input
                else if (notificationMode != null)
                {
                    inputHandled = HandleNotificationModeInput();
                }
                // If in selection sub-mode, handle input there
                else if (selectionMode != null)
                {
                    inputHandled = HandleSelectionModeInput();
                }
                // If in grid sub-mode, handle grid navigation
                else if (gridMode != null)
                {
                    inputHandled = HandleGridModeInput();
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
                gridMode = null;
                notificationMode = null;
                policyMode = null;

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
                        // Check if we drilled into a PriorityGridSection
                        var currentSection = navigation.CurrentSection;
                        if (currentSection is Sections.PriorityGridSection gridSection)
                        {
                            // Enter grid mode instead of normal section navigation
                            EnterGridMode(gridSection);
                        }
                        else
                        {
                            // Actually drilled into a new level - announce new position
                            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                        }
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

            // Toggle view mode (Tab) - switch between Mine/All modes
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                HandleViewModeToggle();
                return true;
            }

            // Letter navigation (A-Z) - jump to item starting with that letter
            if (navigation.CurrentLevel == NavigationLevel.Items)
            {
                char? letter = GetPressedLetter();
                if (letter.HasValue)
                {
                    HandleLetterNavigation(letter.Value);
                    return true;
                }
            }

            // Time controls (Space, 1-6) - work in all Review Mode states
            if (HandleTimeControls())
                return true;

            return false;
        }

        private void HandleViewModeToggle()
        {
            var screen = navigation.CurrentScreen;
            if (screen == null)
            {
                TISpeechMod.Speak("No screen active", interrupt: true);
                return;
            }

            if (!screen.SupportsViewModeToggle)
            {
                TISpeechMod.Speak("This screen does not support view mode toggle", interrupt: true);
                return;
            }

            // If we're not at Items level, go back to Items level first
            while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.CurrentLevel != NavigationLevel.Items)
            {
                navigation.BackOut();
            }

            if (navigation.CurrentLevel == NavigationLevel.Screens)
            {
                // Drill into items first
                navigation.DrillDown();
            }

            string announcement = screen.ToggleViewMode();
            // Reset item index since the list has changed
            navigation.ResetItemIndex();
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private char? GetPressedLetter()
        {
            // Check for letter keys A-Z
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    return (char)('A' + (key - KeyCode.A));
                }
            }
            return null;
        }

        private void HandleLetterNavigation(char letter)
        {
            var screen = navigation.CurrentScreen;
            if (screen == null || !screen.SupportsLetterNavigation)
            {
                TISpeechMod.Speak($"Letter navigation not supported", interrupt: true);
                return;
            }

            int currentIndex = navigation.CurrentItemIndex;
            int newIndex = screen.FindNextItemByLetter(letter, currentIndex);

            if (newIndex < 0)
            {
                TISpeechMod.Speak($"No items starting with {letter}", interrupt: true);
                return;
            }

            navigation.SetItemIndex(newIndex);
            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
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

            // Letter navigation (A-Z) - jump to option starting with that letter
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                int newIndex = selectionMode.FindNextOptionByLetter(letter.Value);
                if (newIndex >= 0)
                {
                    selectionMode.SetIndex(newIndex);
                    AnnounceSelectionItem();
                }
                else
                {
                    TISpeechMod.Speak($"No options starting with {letter.Value}", interrupt: true);
                }
                return true;
            }

            return false;
        }

        private bool HandleGridModeInput()
        {
            // Navigation: Numpad 8/2 for rows (priorities), 4/6 for columns (CPs)
            if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                if (gridMode.MoveUp())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak($"First priority: {gridMode.Grid.GetRowHeader(0)}", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                if (gridMode.MoveDown())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak($"Last priority: {gridMode.Grid.GetRowHeader(gridMode.RowCount - 1)}", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                if (gridMode.MoveLeft())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak("First control point", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                if (gridMode.MoveRight())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak("Last control point", interrupt: true);
                return true;
            }

            // Edit cell: Enter to increment, Numpad - to decrement
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    // Ctrl+Enter = mass change all your CPs for this priority
                    gridMode.MassCycleCurrentRow(decrement: false);
                }
                else
                {
                    gridMode.CycleCurrentCell(decrement: false);
                }
                return true;
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    gridMode.MassCycleCurrentRow(decrement: true);
                }
                else
                {
                    gridMode.CycleCurrentCell(decrement: true);
                }
                return true;
            }

            // Row summary: Numpad *
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(gridMode.GetRowSummary(), interrupt: true);
                return true;
            }

            // Column summary: Numpad /
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                TISpeechMod.Speak(gridMode.GetColumnSummary(), interrupt: true);
                return true;
            }

            // Sync: S key - copy current CP's priorities to all your other CPs
            if (Input.GetKeyDown(KeyCode.S))
            {
                gridMode.SyncFromCurrentColumn();
                return true;
            }

            // Preset: P key - enter preset selection
            if (Input.GetKeyDown(KeyCode.P))
            {
                gridMode.StartPresetSelection();
                return true;
            }

            // Description: D key - read full priority description
            if (Input.GetKeyDown(KeyCode.D))
            {
                string description = gridMode.GetPriorityDescription();
                if (!string.IsNullOrWhiteSpace(description))
                    TISpeechMod.Speak(description, interrupt: true);
                else
                    TISpeechMod.Speak("No description available", interrupt: true);
                return true;
            }

            // Exit grid mode: Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitGridMode();
                return true;
            }

            return false;
        }

        #endregion

        #region Grid Sub-Mode

        private void EnterGridMode(Sections.PriorityGridSection grid)
        {
            gridMode = new GridSubMode(grid);
            string announcement = grid.GetEntryAnnouncement();
            announcement += $" {gridMode.GetCellAnnouncement()}";
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private void ExitGridMode()
        {
            gridMode = null;
            // Back out one level in navigation and announce
            navigation.BackOut();
            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
        }

        #endregion

        #region Notification Sub-Mode

        /// <summary>
        /// Enter notification mode. Called by patch when a notification appears while Review Mode is active.
        /// </summary>
        public void EnterNotificationMode(PavonisInteractive.TerraInvicta.NotificationScreenController controller)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("EnterNotificationMode: controller is null");
                    return;
                }

                notificationMode = new NotificationSubMode(controller);

                if (notificationMode.Count == 0)
                {
                    MelonLogger.Msg("Notification has no navigable options, staying in standard Review Mode");
                    notificationMode = null;
                    return;
                }

                TISpeechMod.Speak(notificationMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered notification mode with {notificationMode.Count} options");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering notification mode: {ex.Message}");
                notificationMode = null;
            }
        }

        /// <summary>
        /// Exit notification mode. Called by patch when notification is dismissed.
        /// </summary>
        public void ExitNotificationMode()
        {
            if (notificationMode == null)
                return;

            notificationMode = null;
            MelonLogger.Msg("Exited notification mode");

            // Don't announce anything - if another notification is coming, it will announce itself
            // If we're returning to Review Mode navigation, the user can use navigation keys to hear position
        }

        private bool HandleNotificationModeInput()
        {
            if (notificationMode == null) return false;

            // Navigate options (Numpad 8/2 or 4/6)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                notificationMode.Previous();
                TISpeechMod.Speak(notificationMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                notificationMode.Next();
                TISpeechMod.Speak(notificationMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate selected option (Enter or Numpad 5)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                var option = notificationMode.CurrentOption;
                if (option != null)
                {
                    TISpeechMod.Speak($"Activating {option.Label}", interrupt: true);
                    notificationMode.Activate();
                    // Note: Notification cleanup is handled by the game's CleanUp method,
                    // which triggers our ExitNotificationMode via patch
                }
                return true;
            }

            // Read current option detail (Numpad *)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(notificationMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /)
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                TISpeechMod.Speak(notificationMode.ListAllOptions(), interrupt: true);
                return true;
            }

            // Escape - select and activate close option
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (notificationMode.SelectCloseOption())
                {
                    var option = notificationMode.CurrentOption;
                    TISpeechMod.Speak($"Closing notification", interrupt: true);
                    notificationMode.Activate();
                }
                return true;
            }

            // Block Numpad 0 (don't allow exiting Review Mode while notification is open)
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                TISpeechMod.Speak("Cannot exit Review Mode while notification is open. Use Enter to select an option, or Escape to close.", interrupt: true);
                return true;
            }

            // Allow time controls even in notification mode
            if (HandleTimeControls())
                return true;

            return false;
        }

        #endregion

        #region Policy Selection Sub-Mode

        /// <summary>
        /// Enter policy selection mode. Called by patch when Set National Policy mission succeeds.
        /// </summary>
        public void EnterPolicySelectionMode(PavonisInteractive.TerraInvicta.NotificationScreenController controller, TINationState nation, TICouncilorState councilor)
        {
            try
            {
                if (controller == null || nation == null)
                {
                    MelonLogger.Error("EnterPolicySelectionMode: controller or nation is null");
                    return;
                }

                policyMode = new PolicySelectionMode(controller, nation, councilor);

                if (policyMode.Policies.Count == 0)
                {
                    MelonLogger.Msg("No policies available, staying in standard Review Mode");
                    policyMode = null;
                    return;
                }

                TISpeechMod.Speak(policyMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered policy selection mode for {nation.displayName} with {policyMode.Policies.Count} policies");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering policy selection mode: {ex.Message}");
                policyMode = null;
            }
        }

        /// <summary>
        /// Exit policy selection mode. Called by patch when policy panels are shut down.
        /// </summary>
        public void ExitPolicySelectionMode()
        {
            if (policyMode == null)
                return;

            policyMode = null;
            MelonLogger.Msg("Exited policy selection mode");
        }

        private bool HandlePolicyModeInput()
        {
            if (policyMode == null) return false;

            // Navigate options (Numpad 8/2 or 4/6)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                policyMode.Previous();
                TISpeechMod.Speak(policyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                policyMode.Next();
                TISpeechMod.Speak(policyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate selected option (Enter or Numpad 5)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                bool continueMode = policyMode.Activate();
                if (!continueMode)
                {
                    // Policy was confirmed or mode should exit
                    policyMode = null;
                }
                return true;
            }

            // Read current option detail (Numpad *)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(policyMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /)
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                TISpeechMod.Speak(policyMode.ListAll(), interrupt: true);
                return true;
            }

            // Go back (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                bool stayInMode = policyMode.GoBack();
                if (!stayInMode)
                {
                    // At the beginning of policy selection - exit mode entirely
                    TISpeechMod.Speak("Cancelled policy selection", interrupt: true);
                    policyMode = null;
                }
                return true;
            }

            // Block Numpad 0 (don't allow exiting Review Mode while in policy selection)
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                TISpeechMod.Speak("Cannot exit Review Mode during policy selection. Press Escape to cancel.", interrupt: true);
                return true;
            }

            // Letter navigation (A-Z) - jump to option starting with that letter
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                int newIndex = policyMode.FindNextByLetter(letter.Value);
                if (newIndex >= 0)
                {
                    policyMode.SetIndex(newIndex);
                    TISpeechMod.Speak(policyMode.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak($"No options starting with {letter.Value}", interrupt: true);
                }
                return true;
            }

            // Allow time controls even in policy mode
            if (HandleTimeControls())
                return true;

            return false;
        }

        #endregion

        #region Time Controls

        /// <summary>
        /// Handle time control keys (Space for pause, 1-6 for speed, 7 for status).
        /// These work in any Review Mode state since we block normal keybindings.
        /// </summary>
        private bool HandleTimeControls()
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return false;

            // Numpad 7 - Read full time status (date, time, speed)
            if (Input.GetKeyDown(KeyCode.Keypad7))
            {
                AnnounceFullTimeStatus();
                return true;
            }

            // Space - Toggle pause
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (gameTime.Paused)
                {
                    // Check if time is blocked (e.g., mission assignments not confirmed)
                    if (gameTime.IsBlocked)
                    {
                        string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                        if (!string.IsNullOrEmpty(blockReason))
                        {
                            TISpeechMod.Speak($"Cannot unpause: {TISpeechMod.CleanText(blockReason)}", interrupt: true);
                        }
                        else
                        {
                            TISpeechMod.Speak("Cannot unpause: time is blocked", interrupt: true);
                        }
                    }
                    else
                    {
                        gameTime.Play();
                        AnnounceTimeState();
                    }
                }
                else
                {
                    gameTime.Pause();
                    TISpeechMod.Speak("Paused", interrupt: true);
                }
                return true;
            }

            // Number keys 1-6 - Set speed directly
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetSpeedAndAnnounce(1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                // Only handle if not in navigation mode (Numpad 2 is used for navigation)
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    SetSpeedAndAnnounce(2);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetSpeedAndAnnounce(3);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                // Only handle Alpha4 (Numpad 4 is used for navigation)
                if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    SetSpeedAndAnnounce(4);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                // Only handle Alpha5 (Numpad 5 is used for activation)
                if (Input.GetKeyDown(KeyCode.Alpha5))
                {
                    SetSpeedAndAnnounce(5);
                    return true;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                // Only handle Alpha6 (Numpad 6 is used for navigation)
                if (Input.GetKeyDown(KeyCode.Alpha6))
                {
                    SetSpeedAndAnnounce(6);
                    return true;
                }
            }

            return false;
        }

        private void SetSpeedAndAnnounce(int speedIndex)
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return;

            // Check if time is blocked before trying to set speed
            if (gameTime.IsBlocked && speedIndex > 0)
            {
                string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                if (!string.IsNullOrEmpty(blockReason))
                {
                    TISpeechMod.Speak($"Cannot set speed: {TISpeechMod.CleanText(blockReason)}", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Cannot set speed: time is blocked", interrupt: true);
                }
                return;
            }

            gameTime.SetSpeed(speedIndex, pushBeyondCap: false);
            AnnounceTimeState();
        }

        private void AnnounceTimeState()
        {
            var gameTime = GameTimeManager.Singleton;
            if (gameTime == null)
                return;

            if (gameTime.Paused)
            {
                TISpeechMod.Speak("Paused", interrupt: true);
            }
            else
            {
                var setting = gameTime.CurrentSpeedSetting;
                string speedText = !string.IsNullOrEmpty(setting.description)
                    ? setting.description
                    : $"Speed {gameTime.currentSpeedIndex}";
                TISpeechMod.Speak(speedText, interrupt: true);
            }
        }

        private void AnnounceFullTimeStatus()
        {
            var sb = new StringBuilder();

            // Get current game date
            try
            {
                var now = TITimeState.Now();
                if (now != null)
                {
                    sb.Append(now.ToCustomDateString());
                }
            }
            catch
            {
                sb.Append("Date unknown");
            }

            // Get speed status
            var gameTime = GameTimeManager.Singleton;
            if (gameTime != null)
            {
                sb.Append(". ");
                if (gameTime.Paused)
                {
                    sb.Append("Paused");
                }
                else
                {
                    var setting = gameTime.CurrentSpeedSetting;
                    string speedText = !string.IsNullOrEmpty(setting.description)
                        ? setting.description
                        : $"Speed {gameTime.currentSpeedIndex}";
                    sb.Append(speedText);
                }
            }

            // Check if in mission phase
            try
            {
                var missionPhase = GameStateManager.MissionPhase();
                if (missionPhase != null && missionPhase.phaseActive)
                {
                    sb.Append(". Mission phase active");
                }
            }
            catch { }

            // Check if time is blocked and why
            if (gameTime != null && gameTime.IsBlocked)
            {
                string blockReason = TIPromptQueueState.GetBlockingDetailStr();
                if (!string.IsNullOrEmpty(blockReason))
                {
                    sb.Append(". Blocked: ");
                    sb.Append(TISpeechMod.CleanText(blockReason));
                }
                else
                {
                    sb.Append(". Time blocked");
                }
            }

            TISpeechMod.Speak(sb.ToString(), interrupt: true);
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

        /// <summary>
        /// Find the next option starting with the given letter after the current index.
        /// If no more options with that letter exist, wraps to the first one.
        /// Returns -1 if no option found.
        /// </summary>
        public int FindNextOptionByLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);

            // Search from current index + 1 to end
            for (int i = CurrentIndex + 1; i < Options.Count; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            // Wrap around: search from 0 to current index
            for (int i = 0; i <= CurrentIndex; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Jump to a specific index.
        /// </summary>
        public void SetIndex(int index)
        {
            if (index >= 0 && index < Options.Count)
                CurrentIndex = index;
        }
    }

    #endregion

    #region Grid Sub-Mode Types

    /// <summary>
    /// Sub-mode for 2D grid navigation (priority grid).
    /// </summary>
    public class GridSubMode
    {
        public Sections.PriorityGridSection Grid { get; }
        public int CurrentRow { get; private set; }
        public int CurrentColumn { get; private set; }

        public int RowCount => Grid.RowCount;
        public int ColumnCount => Grid.ColumnCount;

        public GridSubMode(Sections.PriorityGridSection grid)
        {
            Grid = grid;
            CurrentRow = 0;
            CurrentColumn = 0;
        }

        public bool MoveUp()
        {
            if (CurrentRow > 0)
            {
                CurrentRow--;
                return true;
            }
            return false;
        }

        public bool MoveDown()
        {
            if (CurrentRow < RowCount - 1)
            {
                CurrentRow++;
                return true;
            }
            return false;
        }

        public bool MoveLeft()
        {
            if (CurrentColumn > 0)
            {
                CurrentColumn--;
                return true;
            }
            return false;
        }

        public bool MoveRight()
        {
            if (CurrentColumn < ColumnCount - 1)
            {
                CurrentColumn++;
                return true;
            }
            return false;
        }

        public string GetCellAnnouncement()
        {
            return Grid.ReadCell(CurrentRow, CurrentColumn);
        }

        public string GetRowSummary()
        {
            return Grid.ReadRowSummary(CurrentRow);
        }

        public string GetColumnSummary()
        {
            return Grid.ReadColumnSummary(CurrentColumn);
        }

        public bool CanEditCurrentCell()
        {
            return Grid.CanEditCell(CurrentRow, CurrentColumn);
        }

        public void CycleCurrentCell(bool decrement = false)
        {
            Grid.CycleCell(CurrentRow, CurrentColumn, decrement);
        }

        public void MassCycleCurrentRow(bool decrement = false)
        {
            Grid.MassCycleRow(CurrentRow, decrement);
        }

        public void SyncFromCurrentColumn()
        {
            Grid.SyncFromCP(CurrentColumn);
        }

        public void ToggleDisplayMode()
        {
            Grid.ToggleDisplayMode();
        }

        public void StartPresetSelection()
        {
            Grid.StartPresetSelection();
        }

        public string GetPriorityDescription()
        {
            return Grid.GetPriorityDescription(CurrentRow);
        }
    }

    #endregion
}
