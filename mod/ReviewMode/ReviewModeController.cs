using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using TISpeech.ReviewMode.Screens;
using TISpeech.ReviewMode.Sections;
using TISpeech.ReviewMode.MenuMode;
using TISpeech.ReviewMode.MenuMode.Screens;

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
        private FleetsScreen fleetsScreen;
        private SpaceBodiesScreen spaceBodiesScreen;
        private HabsScreen habsScreen;
        private ShipClassesScreen shipClassesScreen;
        private FactionIntelScreen factionIntelScreen;
        private AlienThreatScreen alienThreatScreen;
        private GlobalStatusScreen globalStatusScreen;

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

        // Transfer planning sub-mode (for fleet transfer planning)
        private TransferSubMode transferMode = null;
        public bool IsInTransferMode => transferMode != null;

        // Combat sub-mode (for space combat pre-combat and live combat navigation)
        private CombatSubMode combatMode = null;
        public bool IsInCombatMode => combatMode != null;

        // Mission target sub-mode (for Sabotage Project and Steal Project missions)
        private MissionTargetSubMode missionTargetMode = null;
        public bool IsInMissionTargetMode => missionTargetMode != null;

        // Special prompt sub-mode (for Army Removal, Diplomatic Response, Call to War)
        private SpecialPromptSubMode specialPromptMode = null;
        public bool IsInSpecialPromptMode => specialPromptMode != null;

        // Diplomacy sub-mode (for faction trade negotiations)
        private DiplomacySubMode diplomacyMode = null;
        public bool IsInDiplomacyMode => diplomacyMode != null;

        // Ship designer sub-mode (for creating/editing ship designs)
        private ShipDesignerSubMode shipDesignerMode = null;
        public bool IsInShipDesignerMode => shipDesignerMode != null;

        // Diplomacy greeting mode (for the initial greeting before trade)
        private DiplomacyGreetingMode greetingMode = null;
        public bool IsInGreetingMode => greetingMode != null;

        // Menu mode (for pre-game menu navigation)
        private bool isInMenuMode = false;
        public bool IsInMenuMode => isInMenuMode;
        private List<MenuScreenBase> menuScreens = new List<MenuScreenBase>();
        private int currentMenuScreenIndex = 0;
        private int currentMenuControlIndex = 0;
        private Stack<MenuContext> menuContextStack = new Stack<MenuContext>();

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
            // Create in-game screens
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

            fleetsScreen = new FleetsScreen();
            fleetsScreen.OnEnterSelectionMode = EnterSelectionMode;
            fleetsScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            fleetsScreen.OnEnterTransferMode = EnterTransferMode;

            spaceBodiesScreen = new SpaceBodiesScreen();
            spaceBodiesScreen.OnEnterSelectionMode = EnterSelectionMode;
            spaceBodiesScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            spaceBodiesScreen.OnEnterTransferFromOrbit = EnterTransferModeFromOrbit;

            habsScreen = new HabsScreen();
            habsScreen.OnEnterSelectionMode = EnterSelectionMode;
            habsScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            shipClassesScreen = new ShipClassesScreen();
            shipClassesScreen.OnEnterSelectionMode = EnterSelectionMode;
            shipClassesScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            shipClassesScreen.OnEnterShipDesignerMode = EnterShipDesignerMode;

            factionIntelScreen = new FactionIntelScreen();
            factionIntelScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            alienThreatScreen = new AlienThreatScreen();
            alienThreatScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            globalStatusScreen = new GlobalStatusScreen();
            globalStatusScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            // Register in-game screens with navigation
            var screens = new List<ScreenBase>
            {
                councilScreen,
                technologyScreen,
                nationScreen,
                orgMarketScreen,
                fleetsScreen,
                spaceBodiesScreen,
                habsScreen,
                shipClassesScreen,
                factionIntelScreen,
                alienThreatScreen,
                globalStatusScreen
            };

            navigation.RegisterScreens(screens);
            MelonLogger.Msg($"Registered {screens.Count} in-game screens for review mode");

            // Create menu screens
            InitializeMenuScreens();
        }

        private void InitializeMenuScreens()
        {
            menuScreens.Clear();
            menuScreens.Add(new MainMenuScreen());   // 0
            menuScreens.Add(new LoadGameScreen());   // 1
            menuScreens.Add(new NewGameScreen());    // 2
            menuScreens.Add(new OptionsScreen());    // 3
            menuScreens.Add(new SkirmishScreen());   // 4
            menuScreens.Add(new ModsScreen());       // 5
            MelonLogger.Msg($"Registered {menuScreens.Count} menu screens for menu mode");
        }

        /// <summary>
        /// Get the appropriate menu screen based on current game state.
        /// Returns the index of the screen to use.
        /// </summary>
        private int GetActiveMenuScreenIndex()
        {
            // Check for specific sub-menus first (most specific to least)
            if (LoadGameScreen.IsVisible())
                return 1; // LoadGameScreen

            if (NewGameScreen.IsVisible())
                return 2; // NewGameScreen

            if (OptionsScreen.IsVisible())
                return 3; // OptionsScreen

            if (SkirmishScreen.IsVisible())
                return 4; // SkirmishScreen

            if (ModsScreen.IsVisible())
                return 5; // ModsScreen

            // Default to main menu
            return 0; // MainMenuScreen
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

        /// <summary>
        /// Switch to a specific menu screen by name.
        /// Called by menu screens when the user activates a button that opens a sub-menu.
        /// </summary>
        public void SwitchToMenuScreen(string screenName)
        {
            if (!isInMenuMode || menuScreens.Count == 0)
                return;

            for (int i = 0; i < menuScreens.Count; i++)
            {
                if (menuScreens[i].Name == screenName)
                {
                    // Deactivate the previous screen
                    if (currentMenuScreenIndex >= 0 && currentMenuScreenIndex < menuScreens.Count)
                    {
                        menuScreens[currentMenuScreenIndex].OnDeactivate();
                    }

                    currentMenuScreenIndex = i;
                    currentMenuControlIndex = 0;
                    var screen = menuScreens[i];
                    screen.OnActivate();

                    string announcement = screen.GetActivationAnnouncement();
                    TISpeechMod.Speak(announcement, interrupt: true);

                    if (screen.ControlCount > 0)
                    {
                        string firstControl = screen.ReadControl(0);
                        TISpeechMod.Speak($"1 of {screen.ControlCount}: {firstControl}", interrupt: false);
                    }

                    MelonLogger.Msg($"Switched to menu screen: {screenName}");
                    return;
                }
            }

            MelonLogger.Warning($"Menu screen not found: {screenName}");
        }

        /// <summary>
        /// Go back to the main menu screen.
        /// </summary>
        public void ReturnToMainMenu()
        {
            SwitchToMenuScreen("Main Menu");
        }

        /// <summary>
        /// Reset the menu control index to 0. Called by screens when their
        /// control list changes significantly (e.g., confirmation dialog appears).
        /// </summary>
        public void ResetMenuControlIndex()
        {
            currentMenuControlIndex = 0;
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

                // If in menu mode, use menu input handler
                if (isInMenuMode)
                {
                    inputHandled = HandleMenuModeInput();
                }
                // Combat mode takes highest priority when in pre-combat or live combat
                else if (combatMode != null)
                {
                    inputHandled = HandleCombatModeInput();
                }
                // Priority order: Policy > MissionTarget > Notification > Transfer > Selection > Grid > Navigation
                // Policy mode takes highest priority (handles Set National Policy mission results)
                else if (policyMode != null)
                {
                    inputHandled = HandlePolicyModeInput();
                }
                // Mission target mode (Sabotage Project, Steal Project)
                else if (missionTargetMode != null)
                {
                    inputHandled = HandleMissionTargetModeInput();
                }
                // Special prompt mode (Army Removal, Diplomatic Response, Call to War)
                else if (specialPromptMode != null)
                {
                    inputHandled = HandleSpecialPromptModeInput();
                }
                // Ship designer sub-mode (creating/editing ship designs)
                else if (shipDesignerMode != null)
                {
                    inputHandled = HandleShipDesignerModeInput();
                }
                // If in notification sub-mode, handle notification input
                else if (notificationMode != null)
                {
                    inputHandled = HandleNotificationModeInput();
                }
                // If in diplomacy greeting mode, handle greeting input
                else if (greetingMode != null)
                {
                    inputHandled = HandleGreetingModeInput();
                }
                // If in diplomacy sub-mode, handle diplomacy input
                else if (diplomacyMode != null)
                {
                    inputHandled = HandleDiplomacyModeInput();
                }
                // If in transfer planning sub-mode, handle transfer input
                else if (transferMode != null)
                {
                    inputHandled = HandleTransferModeInput();
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
                // Check context: main menu vs in-game
                if (IsInMainMenu())
                {
                    ActivateMenuMode();
                    return;
                }

                if (!IsGameReady())
                {
                    TISpeechMod.Speak("Game not ready for review mode", interrupt: true);
                    return;
                }

                ActivateInGameMode();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating review mode: {ex.Message}");
                TISpeechMod.Speak("Error activating review mode", interrupt: true);
            }
        }

        private void ActivateInGameMode()
        {
            TIInputManager.BlockKeybindings();
            isActive = true;
            isInMenuMode = false;

            // Check if there's already a notification showing - go directly to notification mode
            var notificationController = NotificationScreenController.singleton;
            if (notificationController != null &&
                notificationController.singleAlertBox != null &&
                notificationController.singleAlertBox.activeSelf)
            {
                MelonLogger.Msg("Review mode activated with pending notification - entering notification mode");
                EnterNotificationMode(notificationController);
                return;
            }

            // Reset navigation to initial state (Council screen)
            navigation.Reset();

            // Announce activation
            var screen = navigation.CurrentScreen;
            if (screen != null)
            {
                string announcement = $"Review mode. {screen.GetActivationAnnouncement()} ";
                announcement += "Use arrows or Numpad to navigate, Enter to drill in, Escape to back out.";
                TISpeechMod.Speak(announcement, interrupt: true);
            }
            else
            {
                TISpeechMod.Speak("Review mode. No screens available.", interrupt: true);
            }

            MelonLogger.Msg("Review mode activated (in-game) with hierarchical navigation");
        }

        /// <summary>
        /// Activate Review Mode directly into notification sub-mode.
        /// Called when a notification appears while Review Mode is not active.
        /// </summary>
        public void ActivateForNotification(NotificationScreenController controller)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("ActivateForNotification: controller is null");
                    return;
                }

                TIInputManager.BlockKeybindings();
                isActive = true;
                isInMenuMode = false;

                // Go directly to notification mode without initializing normal navigation
                EnterNotificationMode(controller);

                MelonLogger.Msg("Review mode activated directly into notification mode");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForNotification: {ex.Message}");
                // Fall back to normal activation if something goes wrong
                isActive = false;
                TIInputManager.RestoreKeybindings();
            }
        }

        private void ActivateMenuMode()
        {
            isActive = true;
            isInMenuMode = true;
            currentMenuControlIndex = 0;
            menuContextStack.Clear();

            // Always start at the main menu screen - we control our own state
            currentMenuScreenIndex = 0;

            // Refresh the current menu screen
            if (menuScreens.Count > 0 && currentMenuScreenIndex < menuScreens.Count)
            {
                var screen = menuScreens[currentMenuScreenIndex];
                screen.OnActivate();

                string announcement = $"Menu navigation. {screen.GetActivationAnnouncement()} ";
                announcement += "Use arrows to navigate, Enter to activate, Escape to exit.";
                TISpeechMod.Speak(announcement, interrupt: true);

                // Also announce first control if there are any
                if (screen.ControlCount > 0)
                {
                    string firstControl = screen.ReadControl(0);
                    TISpeechMod.Speak($"1 of {screen.ControlCount}: {firstControl}", interrupt: false);
                }
            }
            else
            {
                TISpeechMod.Speak("Menu navigation. No menus available.", interrupt: true);
            }

            MelonLogger.Msg($"Menu mode activated with screen index {currentMenuScreenIndex}");
        }

        private void DeactivateReviewMode()
        {
            try
            {
                // Only restore keybindings if we were in in-game mode
                if (!isInMenuMode)
                {
                    TIInputManager.RestoreKeybindings();
                }

                isActive = false;
                isInMenuMode = false;
                selectionMode = null;
                gridMode = null;
                notificationMode = null;
                policyMode = null;
                transferMode = null;
                combatMode = null;
                missionTargetMode = null;
                specialPromptMode = null;
                diplomacyMode = null;
                menuContextStack.Clear();

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
            // Navigate up/previous (Numpad 8, Up arrow)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                navigation.Previous();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Navigate down/next (Numpad 2, Down arrow)
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
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

            // Drill down / Activate (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
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

            // Back out (Escape, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
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

            // Read detail (Numpad *, Minus/Dash key)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(navigation.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List items at current level (Numpad /, Equals key)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
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

            // Confirm Assignments (Numpad +, Backslash) - global action during mission phase
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
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

            // Faction filter ([ and ]) - cycle through factions in All mode
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                HandleFactionFilter(previous: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                HandleFactionFilter(previous: false);
                return true;
            }

            // Sort (Ctrl+S) - open sort menu on Nations or Space Bodies screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.S))
            {
                if (HandleNationSort())
                    return true;
            }

            // Probe All (Ctrl+P) - launch probes to all eligible bodies on Space Bodies screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.P))
            {
                if (HandleProbeAll())
                    return true;
            }

            // Filter (Ctrl+F) - cycle faction filter on Nations screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
            {
                if (HandleNationFilter())
                    return true;
            }

            // Transfer planner (T) - enter theoretical transfer planner
            if (Input.GetKeyDown(KeyCode.T))
            {
                EnterTheoreticalTransferMode();
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

        /// <summary>
        /// Check if menu context has changed and switch screens if needed.
        /// Called every frame before debounce to catch menu transitions.
        /// </summary>
        private void CheckMenuContextChange()
        {
            if (menuScreens.Count == 0)
                return;

            int newScreenIndex = GetActiveMenuScreenIndex();
            bool screenChanged = newScreenIndex != currentMenuScreenIndex;

            // Also check if the current screen's internal state has changed (e.g., dialog opened)
            bool stateChanged = false;
            if (!screenChanged && currentMenuScreenIndex < menuScreens.Count)
            {
                var currentScreen = menuScreens[currentMenuScreenIndex];
                if (currentScreen is LoadGameScreen loadScreen)
                {
                    stateChanged = loadScreen.HasStateChanged();
                }
                // Add similar checks for other screens with dialogs as needed
            }

            if (screenChanged || stateChanged)
            {
                // Context or state changed - refresh the screen
                currentMenuScreenIndex = newScreenIndex;
                currentMenuControlIndex = 0;

                if (currentMenuScreenIndex < menuScreens.Count)
                {
                    var newScreen = menuScreens[currentMenuScreenIndex];
                    newScreen.OnActivate();

                    string announcement = $"{newScreen.GetActivationAnnouncement()}";
                    TISpeechMod.Speak(announcement, interrupt: true);

                    // Announce first control
                    if (newScreen.ControlCount > 0)
                    {
                        string firstControl = newScreen.ReadControl(0);
                        TISpeechMod.Speak($"1 of {newScreen.ControlCount}: {firstControl}", interrupt: false);
                    }

                    MelonLogger.Msg($"Menu context changed to screen {currentMenuScreenIndex}: {newScreen.Name} (screenChanged={screenChanged}, stateChanged={stateChanged})");
                }
            }
        }

        private bool HandleMenuModeInput()
        {
            if (menuScreens.Count == 0 || currentMenuScreenIndex >= menuScreens.Count)
                return false;

            var screen = menuScreens[currentMenuScreenIndex];
            int controlCount = screen.ControlCount;

            // Navigate up/previous (Numpad 8, Up arrow)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (controlCount > 0)
                {
                    currentMenuControlIndex--;
                    if (currentMenuControlIndex < 0)
                        currentMenuControlIndex = controlCount - 1;
                    AnnounceCurrentMenuControl();
                }
                return true;
            }

            // Navigate down/next (Numpad 2, Down arrow)
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (controlCount > 0)
                {
                    currentMenuControlIndex++;
                    if (currentMenuControlIndex >= controlCount)
                        currentMenuControlIndex = 0;
                    AnnounceCurrentMenuControl();
                }
                return true;
            }

            // Adjust control left (Numpad 4, Left arrow) - for sliders/dropdowns
            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (screen.CanAdjustControl(currentMenuControlIndex))
                {
                    screen.AdjustControl(currentMenuControlIndex, increment: false);
                }
                return true;
            }

            // Adjust control right (Numpad 6, Right arrow) - for sliders/dropdowns
            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (screen.CanAdjustControl(currentMenuControlIndex))
                {
                    screen.AdjustControl(currentMenuControlIndex, increment: true);
                }
                return true;
            }

            // Activate control (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                screen.ActivateControl(currentMenuControlIndex);
                return true;
            }

            // Back out / Exit (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // If on a sub-screen, go back to main menu
                if (currentMenuScreenIndex > 0)
                {
                    ReturnToMainMenu();
                }
                else
                {
                    // At main menu - deactivate menu mode
                    DeactivateReviewMode();
                }
                return true;
            }

            // Read detail (Numpad *, Minus/Dash key)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = screen.ReadControlDetail(currentMenuControlIndex);
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            // List all controls (Numpad /, Equals key)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(screen.ListAllControls(), interrupt: true);
                return true;
            }

            // Letter navigation (A-Z) - jump to control starting with that letter
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                int newIndex = screen.FindNextControlByLetter(letter.Value, currentMenuControlIndex);
                if (newIndex >= 0)
                {
                    currentMenuControlIndex = newIndex;
                    AnnounceCurrentMenuControl();
                }
                else
                {
                    TISpeechMod.Speak($"No controls starting with {letter.Value}", interrupt: true);
                }
                return true;
            }

            return false;
        }

        private void AnnounceCurrentMenuControl()
        {
            if (menuScreens.Count == 0 || currentMenuScreenIndex >= menuScreens.Count)
                return;

            var screen = menuScreens[currentMenuScreenIndex];
            int controlCount = screen.ControlCount;

            if (controlCount == 0)
            {
                TISpeechMod.Speak("No controls", interrupt: true);
                return;
            }

            string controlText = screen.ReadControl(currentMenuControlIndex);
            TISpeechMod.Speak($"{currentMenuControlIndex + 1} of {controlCount}: {controlText}", interrupt: true);
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

        /// <summary>
        /// Handle faction filter cycling.
        /// </summary>
        private void HandleFactionFilter(bool previous)
        {
            var screen = navigation.CurrentScreen;
            if (screen == null)
            {
                TISpeechMod.Speak("No screen active", interrupt: true);
                return;
            }

            if (!screen.SupportsFactionFilter)
            {
                TISpeechMod.Speak("This screen does not support faction filtering", interrupt: true);
                return;
            }

            string announcement = previous ? screen.PreviousFactionFilter() : screen.NextFactionFilter();
            if (announcement != null)
            {
                // Reset item index since the list has changed
                navigation.ResetItemIndex();
                TISpeechMod.Speak(announcement, interrupt: true);
            }
        }

        /// <summary>
        /// Handle sort request - works on Nations and Space Bodies screens
        /// </summary>
        private bool HandleNationSort()
        {
            // Try Nations screen first
            var nationScreen = navigation.CurrentScreen as Screens.NationScreen;
            if (nationScreen != null)
            {
                nationScreen.StartSortSelection();
                return true;
            }

            // Try Space Bodies screen
            var spaceBodiesScreen = navigation.CurrentScreen as Screens.SpaceBodiesScreen;
            if (spaceBodiesScreen != null)
            {
                spaceBodiesScreen.StartSortSelection();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle filter request - only works on Nations screen
        /// </summary>
        private bool HandleNationFilter()
        {
            var screen = navigation.CurrentScreen as Screens.NationScreen;
            if (screen == null)
                return false;

            screen.CycleFactionFilter();
            // Reset item index since the list has changed
            navigation.ResetItemIndex();
            return true;
        }

        /// <summary>
        /// Handle Probe All request - only works on Space Bodies screen
        /// </summary>
        private bool HandleProbeAll()
        {
            var screen = navigation.CurrentScreen as Screens.SpaceBodiesScreen;
            if (screen == null)
                return false;

            screen.StartProbeAll();
            return true;
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
            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectionMode.Previous();
                AnnounceSelectionItem();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectionMode.Next();
                AnnounceSelectionItem();
                return true;
            }

            // Confirm selection (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                ConfirmSelection();
                return true;
            }

            // Cancel (Escape, Numpad 0, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Keypad0) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CancelSelection();
                return true;
            }

            // Read detail (Numpad *, Minus) - show full modifier breakdown
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
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
            // Navigation: Numpad 8/2 or Up/Down for rows (priorities), Numpad 4/6 or Left/Right for columns (CPs)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (gridMode.MoveUp())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak($"First priority: {gridMode.Grid.GetRowHeader(0)}", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (gridMode.MoveDown())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak($"Last priority: {gridMode.Grid.GetRowHeader(gridMode.RowCount - 1)}", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (gridMode.MoveLeft())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak("First control point", interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (gridMode.MoveRight())
                    TISpeechMod.Speak(gridMode.GetCellAnnouncement(), interrupt: true);
                else
                    TISpeechMod.Speak("Last control point", interrupt: true);
                return true;
            }

            // Edit cell: Enter to increment, Numpad - or Minus to decrement
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
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

            // Row summary: Numpad * (note: Minus is used for decrement in grid mode)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(gridMode.GetRowSummary(), interrupt: true);
                return true;
            }

            // Column summary: Numpad /, Equals
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
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

                // Clear any EventSystem selection to prevent Enter from submitting focused buttons
                // This is a backup for the check in DialogAnnouncer.FocusFirstInteractable()
                EventSystem.current?.SetSelectedGameObject(null);

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

            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                notificationMode.Previous();
                TISpeechMod.Speak(notificationMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                notificationMode.Next();
                TISpeechMod.Speak(notificationMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate selected option (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
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

            // Read current option detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(notificationMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
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

            // Block exit keys (don't allow exiting Review Mode while notification is open)
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
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

        #region Diplomacy Greeting Mode

        /// <summary>
        /// Enter greeting mode for the diplomacy greeting screen.
        /// Called by patch when OpenDiplomacyGreetingUI is called.
        /// </summary>
        public void EnterGreetingMode(NotificationScreenController controller)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("EnterGreetingMode: controller is null");
                    return;
                }

                greetingMode = new DiplomacyGreetingMode(controller);

                if (greetingMode.Count == 0)
                {
                    MelonLogger.Msg("Greeting has no navigable items");
                    greetingMode = null;
                    return;
                }

                // Clear any EventSystem selection
                EventSystem.current?.SetSelectedGameObject(null);

                TISpeechMod.Speak(greetingMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered greeting mode with {greetingMode.Count} items");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering greeting mode: {ex.Message}");
                greetingMode = null;
            }
        }

        /// <summary>
        /// Exit greeting mode.
        /// </summary>
        public void ExitGreetingMode()
        {
            if (greetingMode == null)
                return;

            greetingMode = null;
            MelonLogger.Msg("Exited greeting mode");
        }

        private bool HandleGreetingModeInput()
        {
            if (greetingMode == null) return false;

            // Navigate (Up/Down arrows, Numpad 8/2)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                greetingMode.Previous();
                TISpeechMod.Speak(greetingMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                greetingMode.Next();
                TISpeechMod.Speak(greetingMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate / Continue (Enter, Numpad 5, Numpad Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                if (greetingMode.Activate())
                {
                    // Continue button was clicked, exit greeting mode
                    TISpeechMod.Speak("Continuing to trade", interrupt: true);
                    ExitGreetingMode();
                }
                else
                {
                    // Just read the current item
                    TISpeechMod.Speak(greetingMode.CurrentItem, interrupt: true);
                }
                return true;
            }

            // Read full content (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(greetingMode.GetFullContent(), interrupt: true);
                return true;
            }

            // Escape - just exit (cancel the diplomacy)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TISpeechMod.Speak("Cancelling diplomacy", interrupt: true);
                ExitGreetingMode();
                return true;
            }

            // No key handled - return false to avoid debounce issues
            return false;
        }

        #endregion

        #region Diplomacy Sub-Mode

        /// <summary>
        /// Enter diplomacy mode. Called by patch when DiplomacyController.Setup() is called.
        /// </summary>
        public void EnterDiplomacyMode(DiplomacyController controller)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("EnterDiplomacyMode: controller is null");
                    return;
                }

                diplomacyMode = new DiplomacySubMode(controller);

                if (diplomacyMode.SectionCount == 0)
                {
                    MelonLogger.Msg("Diplomacy has no navigable sections, staying in current mode");
                    diplomacyMode = null;
                    return;
                }

                // Clear any EventSystem selection
                EventSystem.current?.SetSelectedGameObject(null);

                TISpeechMod.Speak(diplomacyMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered diplomacy mode with {diplomacyMode.SectionCount} sections");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering diplomacy mode: {ex.Message}");
                diplomacyMode = null;
            }
        }

        /// <summary>
        /// Exit diplomacy mode. Called when diplomacy is closed.
        /// </summary>
        public void ExitDiplomacyMode()
        {
            if (diplomacyMode == null)
                return;

            diplomacyMode = null;
            MelonLogger.Msg("Exited diplomacy mode");
        }

        private bool HandleDiplomacyModeInput()
        {
            if (diplomacyMode == null) return false;

            // QUANTITY INPUT MODE - special handling when entering quantities
            if (diplomacyMode.IsEnteringQuantity)
            {
                // Handle digit input
                for (int i = 0; i <= 9; i++)
                {
                    KeyCode keyCode = (KeyCode)((int)KeyCode.Alpha0 + i);
                    KeyCode keypadCode = (KeyCode)((int)KeyCode.Keypad0 + i);

                    if (Input.GetKeyDown(keyCode) || Input.GetKeyDown(keypadCode))
                    {
                        diplomacyMode.HandleDigit((char)('0' + i));
                        TISpeechMod.Speak(diplomacyMode.GetQuantityInputAnnouncement(), interrupt: true);
                        return true;
                    }
                }

                // Backspace - delete last digit
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    diplomacyMode.HandleBackspace();
                    TISpeechMod.Speak(diplomacyMode.GetQuantityInputAnnouncement(), interrupt: true);
                    return true;
                }

                // Enter - apply quantity
                if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.Keypad5))
                {
                    string result = diplomacyMode.ApplyQuantity();
                    TISpeechMod.Speak(result, interrupt: true);
                    return true;
                }

                // Escape - cancel quantity input
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    string result = diplomacyMode.CancelQuantityMode();
                    TISpeechMod.Speak(result, interrupt: true);
                    return true;
                }

                // No key handled in quantity mode - return false to avoid debounce issues
                return false;
            }

            // NORMAL NAVIGATION MODE

            // Navigate (Up/Down arrows, Numpad 8/2) - navigates sections OR items depending on drill state
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                diplomacyMode.Previous();
                TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                diplomacyMode.Next();
                TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Also Numpad 4/6 for navigation (same behavior as 8/2)
            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                diplomacyMode.Previous();
                TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                diplomacyMode.Next();
                TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Drill down / Activate (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                string result = diplomacyMode.DrillDown();
                if (result == "CANCEL_DIPLOMACY")
                {
                    TISpeechMod.Speak("Cancelling diplomacy", interrupt: true);
                    ExitDiplomacyMode();
                }
                else
                {
                    TISpeechMod.Speak(result, interrupt: true);
                }
                return true;
            }

            // Back out (Escape, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.Backspace))
            {
                if (diplomacyMode.BackOut())
                {
                    // Backed out to section level
                    TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // At section level, exit diplomacy
                    TISpeechMod.Speak("Exiting diplomacy", interrupt: true);
                    ExitDiplomacyMode();
                }
                return true;
            }

            // Delete - remove item from trade
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                string result = diplomacyMode.RemoveCurrentItem();
                TISpeechMod.Speak(result, interrupt: true);
                return true;
            }

            // Read current detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(diplomacyMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all items in current section (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(diplomacyMode.ListCurrentSection(), interrupt: true);
                return true;
            }

            // Block Review Mode exit keys while in diplomacy
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                TISpeechMod.Speak("Press Escape to exit diplomacy mode", interrupt: true);
                return true;
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R))
            {
                TISpeechMod.Speak("Press Escape to exit diplomacy mode", interrupt: true);
                return true;
            }

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

            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                policyMode.Previous();
                TISpeechMod.Speak(policyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                policyMode.Next();
                TISpeechMod.Speak(policyMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate selected option (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                bool continueMode = policyMode.Activate();
                if (!continueMode)
                {
                    // Policy was confirmed or mode should exit
                    policyMode = null;
                }
                return true;
            }

            // Read current option detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(policyMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(policyMode.ListAll(), interrupt: true);
                return true;
            }

            // Go back (Escape, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
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

            // Block exit keys (don't allow exiting Review Mode while in policy selection)
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
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

        #region Mission Target Sub-Mode

        /// <summary>
        /// Activate Review Mode directly into mission target sub-mode.
        /// Called when a mission target selection appears while Review Mode is not active.
        /// </summary>
        public void ActivateForMissionTarget(NotificationScreenController controller, string promptType)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("ActivateForMissionTarget: controller is null");
                    return;
                }

                TIInputManager.BlockKeybindings();
                isActive = true;
                isInMenuMode = false;

                // Go directly to mission target mode without initializing normal navigation
                EnterMissionTargetMode(controller, promptType);

                MelonLogger.Msg("Review mode activated directly into mission target mode");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForMissionTarget: {ex.Message}");
                // Fall back to normal activation if something goes wrong
                isActive = false;
                TIInputManager.RestoreKeybindings();
            }
        }

        /// <summary>
        /// Enter mission target mode. Called by patch when mission target selection appears.
        /// </summary>
        public void EnterMissionTargetMode(NotificationScreenController controller, string promptType)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("EnterMissionTargetMode: controller is null");
                    return;
                }

                missionTargetMode = new MissionTargetSubMode(controller, promptType);

                if (missionTargetMode.Count == 0)
                {
                    MelonLogger.Msg("Mission target selection has no targets available");
                    missionTargetMode = null;
                    return;
                }

                // Clear any EventSystem selection to prevent Enter from submitting focused buttons
                EventSystem.current?.SetSelectedGameObject(null);

                TISpeechMod.Speak(missionTargetMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered mission target mode with {missionTargetMode.Count} targets");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering mission target mode: {ex.Message}");
                missionTargetMode = null;
            }
        }

        /// <summary>
        /// Exit mission target mode. Called by patch when mission target UI is dismissed.
        /// </summary>
        public void ExitMissionTargetMode()
        {
            if (missionTargetMode == null)
                return;

            missionTargetMode = null;
            MelonLogger.Msg("Exited mission target mode");
        }

        private bool HandleMissionTargetModeInput()
        {
            if (missionTargetMode == null) return false;

            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                missionTargetMode.Previous();
                TISpeechMod.Speak(missionTargetMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                missionTargetMode.Next();
                TISpeechMod.Speak(missionTargetMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Select current target (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                var option = missionTargetMode.CurrentOption;
                if (option != null)
                {
                    missionTargetMode.SelectCurrent();
                    TISpeechMod.Speak($"Selected: {option.Label}. Press plus or backslash to confirm.", interrupt: true);
                }
                return true;
            }

            // Confirm selection (Numpad +, Backslash)
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
            {
                if (missionTargetMode.HasSelection)
                {
                    TISpeechMod.Speak("Confirming target selection", interrupt: true);
                    missionTargetMode.Confirm();
                    // Mode will be exited by the patch when confirm completes
                }
                else
                {
                    TISpeechMod.Speak("Select a target first by pressing Enter", interrupt: true);
                }
                return true;
            }

            // Read current option detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(missionTargetMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(missionTargetMode.ListAllOptions(), interrupt: true);
                return true;
            }

            // Cancel and abort mission (Escape, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                TISpeechMod.Speak("Cancelling and aborting mission", interrupt: true);
                missionTargetMode.Cancel();
                // Mode will be exited by the patch when cancel completes
                return true;
            }

            // Block exit keys (don't allow exiting Review Mode while in mission target selection)
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Cannot exit Review Mode during target selection. Press Escape to cancel.", interrupt: true);
                return true;
            }

            // Letter navigation (A-Z) - jump to option starting with that letter
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                int newIndex = missionTargetMode.FindNextByLetter(letter.Value);
                if (newIndex >= 0)
                {
                    missionTargetMode.SetIndex(newIndex);
                    TISpeechMod.Speak(missionTargetMode.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak($"No targets starting with {letter.Value}", interrupt: true);
                }
                return true;
            }

            // Allow time controls even in mission target mode
            if (HandleTimeControls())
                return true;

            return false;
        }

        #endregion

        #region Special Prompt Sub-Mode

        /// <summary>
        /// Activate Review Mode directly into special prompt sub-mode.
        /// Called when a special prompt panel appears while Review Mode is not active.
        /// </summary>
        public void ActivateForSpecialPrompt(NotificationScreenController controller, SpecialPromptType promptType)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("ActivateForSpecialPrompt: controller is null");
                    return;
                }

                TIInputManager.BlockKeybindings();
                isActive = true;
                isInMenuMode = false;

                // Go directly to special prompt mode without initializing normal navigation
                EnterSpecialPromptMode(controller, promptType);

                MelonLogger.Msg($"Review mode activated directly into special prompt mode: {promptType}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForSpecialPrompt: {ex.Message}");
                // Fall back to normal activation if something goes wrong
                isActive = false;
                TIInputManager.RestoreKeybindings();
            }
        }

        /// <summary>
        /// Enter special prompt mode. Called by patch when a special prompt panel appears.
        /// </summary>
        public void EnterSpecialPromptMode(NotificationScreenController controller, SpecialPromptType promptType)
        {
            try
            {
                if (controller == null)
                {
                    MelonLogger.Error("EnterSpecialPromptMode: controller is null");
                    return;
                }

                specialPromptMode = new SpecialPromptSubMode(controller, promptType);

                if (specialPromptMode.Count == 0)
                {
                    MelonLogger.Msg("Special prompt panel has no options available");
                    specialPromptMode = null;
                    return;
                }

                // Clear any EventSystem selection to prevent Enter from submitting focused buttons
                EventSystem.current?.SetSelectedGameObject(null);

                TISpeechMod.Speak(specialPromptMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered special prompt mode ({promptType}) with {specialPromptMode.Count} options");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering special prompt mode: {ex.Message}");
                specialPromptMode = null;
            }
        }

        /// <summary>
        /// Exit special prompt mode. Called by patch when special prompt panel is dismissed.
        /// </summary>
        public void ExitSpecialPromptMode()
        {
            if (specialPromptMode == null)
                return;

            specialPromptMode = null;
            MelonLogger.Msg("Exited special prompt mode");
        }

        private bool HandleSpecialPromptModeInput()
        {
            if (specialPromptMode == null) return false;

            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                specialPromptMode.Previous();
                TISpeechMod.Speak(specialPromptMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                specialPromptMode.Next();
                TISpeechMod.Speak(specialPromptMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate current option (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                var option = specialPromptMode.CurrentOption;
                if (option != null)
                {
                    if (option.IsInformational)
                    {
                        // Re-read informational items
                        TISpeechMod.Speak(option.Label, interrupt: true);
                    }
                    else
                    {
                        TISpeechMod.Speak($"Activating: {option.Label}", interrupt: true);
                        specialPromptMode.Activate();
                        // Mode will be exited by the patch when the panel is dismissed
                    }
                }
                return true;
            }

            // Read current option detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(specialPromptMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(specialPromptMode.ListAllOptions(), interrupt: true);
                return true;
            }

            // Allow exiting Review Mode with Numpad 0 or Ctrl+R (dismisses the prompt too)
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                // Just exit review mode - the user will need to handle the prompt with mouse
                TISpeechMod.Speak("Exiting Review Mode. Prompt still pending.", interrupt: true);
                DeactivateReviewMode();
                return true;
            }

            // Escape also exits (user may want to dismiss via mouse)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                TISpeechMod.Speak("Exiting Review Mode. Prompt still pending.", interrupt: true);
                DeactivateReviewMode();
                return true;
            }

            // Allow time controls even in special prompt mode
            if (HandleTimeControls())
                return true;

            return false;
        }

        #endregion

        #region Ship Designer Mode

        /// <summary>
        /// Enter ship designer mode for creating a new design or editing an existing one.
        /// </summary>
        /// <param name="existingDesign">The design to edit, or null for a new design</param>
        public void EnterShipDesignerMode(TISpaceShipTemplate existingDesign = null)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                {
                    TISpeechMod.Speak("No active faction", interrupt: true);
                    return;
                }

                if (!Readers.ShipClassReader.CanDesignShips(faction))
                {
                    TISpeechMod.Speak("Cannot design ships yet. Research hull technologies first.", interrupt: true);
                    return;
                }

                shipDesignerMode = new ShipDesignerSubMode(faction, existingDesign);
                shipDesignerMode.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
                shipDesignerMode.OnDesignSaved = (design) =>
                {
                    ExitShipDesignerMode();
                    // Refresh the ship classes screen
                    shipClassesScreen?.Refresh();
                    TISpeechMod.Speak($"Design saved: {design?.className ?? "Unknown"}", interrupt: true);
                };
                shipDesignerMode.OnCancelled = () =>
                {
                    ExitShipDesignerMode();
                    TISpeechMod.Speak("Design cancelled", interrupt: true);
                };
                shipDesignerMode.OnEnterTextInput = (prompt, callback) =>
                {
                    // For now, use a default name - proper text input would need a different mechanism
                    // TODO: Implement text input mode
                    TISpeechMod.Speak($"{prompt}. Using default name. Text input not yet implemented.", interrupt: true);
                    callback(null);
                };

                // Announce entry
                if (existingDesign != null)
                {
                    TISpeechMod.Speak($"Ship Designer: Editing {existingDesign.className}. Navigate with arrows, Enter to select, Escape to back out.", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Ship Designer: New design. Select a hull to begin.", interrupt: true);
                }

                shipDesignerMode.AnnounceCurrentState();
                MelonLogger.Msg($"Entered ship designer mode. Editing: {existingDesign?.className ?? "New design"}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering ship designer mode: {ex.Message}");
                TISpeechMod.Speak("Failed to enter ship designer mode", interrupt: true);
            }
        }

        /// <summary>
        /// Exit ship designer mode.
        /// </summary>
        public void ExitShipDesignerMode()
        {
            shipDesignerMode = null;
            MelonLogger.Msg("Exited ship designer mode");
        }

        private bool HandleShipDesignerModeInput()
        {
            if (shipDesignerMode == null) return false;

            // Navigate options (Numpad 8/2, Numpad 4/6, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                shipDesignerMode.Previous();
                shipDesignerMode.AnnounceCurrentState();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                shipDesignerMode.Next();
                shipDesignerMode.AnnounceCurrentState();
                return true;
            }

            // Select/drill in (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                shipDesignerMode.Select();
                return true;
            }

            // Back out (Escape, Left arrow, Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                shipDesignerMode.Back();
                return true;
            }

            // Read current detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = shipDesignerMode.ReadCurrentDetail();
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            // Adjust value up (Numpad +, Backslash)
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
            {
                // Used for propellant tanks and armor points
                if (shipDesignerMode.CurrentStep == DesignerStep.NavigateZoneItems &&
                    shipDesignerMode.CurrentZone == DesignZone.Propulsion &&
                    shipDesignerMode.CurrentZoneItemIndex == 3) // Propellant
                {
                    shipDesignerMode.AdjustPropellant(1);
                }
                else if (shipDesignerMode.CurrentStep == DesignerStep.SelectComponent)
                {
                    shipDesignerMode.AdjustArmor(1);
                }
                return true;
            }

            // Adjust value down (Numpad -)
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                if (shipDesignerMode.CurrentStep == DesignerStep.NavigateZoneItems &&
                    shipDesignerMode.CurrentZone == DesignZone.Propulsion &&
                    shipDesignerMode.CurrentZoneItemIndex == 3) // Propellant
                {
                    shipDesignerMode.AdjustPropellant(-1);
                }
                else if (shipDesignerMode.CurrentStep == DesignerStep.SelectComponent)
                {
                    shipDesignerMode.AdjustArmor(-1);
                }
                return true;
            }

            // Autodesign (A key) - let the AI fill in the design
            if (Input.GetKeyDown(KeyCode.A))
            {
                shipDesignerMode.ApplyAutodesign();
                return true;
            }

            // Block exit from Review Mode while in designer
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Press Escape to exit designer first", interrupt: true);
                return true;
            }

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
            string announcement = $"{prompt}. {options.Count} options. 1 of {options.Count}: {firstOption.Label}. Use arrows to browse, Enter to select, minus for detail, Escape to cancel.";
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

        #region Transfer Sub-Mode

        /// <summary>
        /// Enter transfer planning mode for a specific fleet.
        /// </summary>
        public void EnterTransferMode(TISpaceFleetState fleet)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("No fleet selected", interrupt: true);
                return;
            }

            if (fleet.faction != GameControl.control?.activePlayer)
            {
                TISpeechMod.Speak("Cannot plan transfers for other factions' fleets", interrupt: true);
                return;
            }

            if (fleet.inTransfer)
            {
                TISpeechMod.Speak("Fleet already has a transfer assigned", interrupt: true);
                return;
            }

            if (fleet.dockedOrLanded)
            {
                TISpeechMod.Speak("Fleet must undock before planning a transfer", interrupt: true);
                return;
            }

            transferMode = new TransferSubMode(fleet);
            transferMode.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            transferMode.OnTransferConfirmed = OnTransferConfirmed;
            transferMode.OnCancelled = ExitTransferMode;

            string announcement = $"Transfer planning for {fleet.displayName}. {fleet.currentDeltaV_kps:F1} km/s available. ";
            announcement += transferMode.GetStepAnnouncement();
            TISpeechMod.Speak(announcement, interrupt: true);

            // Announce first option
            transferMode.AnnounceCurrentItem();
        }

        /// <summary>
        /// Enter transfer planning mode for theoretical calculations.
        /// Starts with acceleration/delta-V selection, then origin, then destination.
        /// </summary>
        public void EnterTheoreticalTransferMode()
        {
            transferMode = new TransferSubMode();
            transferMode.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            transferMode.OnCancelled = ExitTransferMode;

            string announcement = "Theoretical transfer planner. ";
            announcement += transferMode.GetStepAnnouncement();
            TISpeechMod.Speak(announcement, interrupt: true);

            transferMode.AnnounceCurrentItem();
        }

        /// <summary>
        /// Enter transfer planning mode from a specific orbit (context-aware).
        /// Origin is pre-selected, skips to acceleration/delta-V then destination.
        /// </summary>
        public void EnterTransferModeFromOrbit(TIOrbitState origin)
        {
            if (origin == null)
            {
                TISpeechMod.Speak("No orbit selected", interrupt: true);
                return;
            }

            transferMode = new TransferSubMode(origin);
            transferMode.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            transferMode.OnCancelled = ExitTransferMode;

            string bodyName = origin.barycenter?.displayName ?? "unknown body";
            string announcement = $"Transfer planner from {origin.displayName} at {bodyName}. ";
            announcement += transferMode.GetStepAnnouncement();
            TISpeechMod.Speak(announcement, interrupt: true);

            transferMode.AnnounceCurrentItem();
        }

        private void ExitTransferMode()
        {
            transferMode = null;
            TISpeechMod.Speak("Transfer planning cancelled", interrupt: true);
        }

        private void OnTransferConfirmed(Trajectory trajectory)
        {
            if (transferMode == null || transferMode.Fleet == null || trajectory == null)
            {
                ExitTransferMode();
                return;
            }

            try
            {
                var fleet = transferMode.Fleet;

                // Assign the transfer
                fleet.AssignTrajectory(trajectory);

                string dest = trajectory.destination?.displayName ?? "destination";
                TISpeechMod.Speak($"Transfer assigned. {fleet.displayName} departing for {dest}", interrupt: true);

                MelonLogger.Msg($"Transfer assigned: {fleet.displayName} -> {dest}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error assigning transfer: {ex.Message}");
                TISpeechMod.Speak("Error assigning transfer", interrupt: true);
            }

            transferMode = null;
        }

        private bool HandleTransferModeInput()
        {
            if (transferMode == null)
                return false;

            // Handle numeric input mode (acceleration/delta-V entry)
            if (transferMode.IsInputStep)
            {
                // Digits (number row 0-9)
                for (int i = 0; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        transferMode.HandleDigit((char)('0' + i));
                        return true;
                    }
                }

                // Decimal point (period key)
                if (Input.GetKeyDown(KeyCode.Period))
                {
                    transferMode.HandleDecimal();
                    return true;
                }

                // Backspace - delete last character (not go back)
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    transferMode.HandleBackspace();
                    return true;
                }

                // Enter - confirm input and proceed
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    transferMode.Select();
                    return true;
                }

                // Escape - cancel and go back
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    transferMode.Back();
                    return true;
                }

                return false;
            }

            // Navigate options (Numpad 8/2, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                transferMode.Previous();
                transferMode.AnnounceCurrentItem();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                transferMode.Next();
                transferMode.AnnounceCurrentItem();
                return true;
            }

            // Select/Drill down (Numpad Enter, Numpad 5, Enter, Right arrow)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                transferMode.Select();
                return true;
            }

            // Go back (Escape, Backspace, Left arrow)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) ||
                Input.GetKeyDown(KeyCode.LeftArrow))
            {
                transferMode.Back();
                return true;
            }

            // Read detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = transferMode.ReadCurrentItemDetail();
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            // Cycle sort mode (Tab) - only in trajectory view
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                transferMode.CycleSortMode();
                return true;
            }

            // Letter navigation (A-Z)
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                transferMode.JumpToLetter(letter.Value);
                return true;
            }

            return false;
        }

        #endregion

        #region Combat Sub-Mode

        /// <summary>
        /// Check if we should automatically enter combat mode (when pre-combat begins).
        /// Called by external patch when space combat is initiated.
        /// </summary>
        public void CheckForCombatMode()
        {
            try
            {
                // Don't auto-enter if not in review mode or already in combat mode
                if (!isActive || combatMode != null)
                    return;

                // Check if we're in pre-combat
                if (CombatSubMode.IsInPreCombat())
                {
                    EnterCombatMode();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking for combat mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Enter combat mode for pre-combat navigation.
        /// </summary>
        public void EnterCombatMode()
        {
            try
            {
                if (combatMode != null)
                {
                    // Already in combat mode - just refresh
                    combatMode.Refresh();
                    return;
                }

                combatMode = new CombatSubMode();
                if (!combatMode.Initialize())
                {
                    MelonLogger.Msg("Failed to initialize combat mode - no active combat");
                    combatMode = null;
                    return;
                }

                // Activate review mode if not already active
                if (!isActive)
                {
                    TIInputManager.BlockKeybindings();
                    isActive = true;
                    isInMenuMode = false;
                }

                TISpeechMod.Speak(combatMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered combat mode: Phase={combatMode.CurrentPhase}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering combat mode: {ex.Message}");
                combatMode = null;
            }
        }

        /// <summary>
        /// Exit combat mode and return to normal review mode.
        /// </summary>
        public void ExitCombatMode()
        {
            if (combatMode == null)
                return;

            combatMode = null;
            MelonLogger.Msg("Exited combat mode");

            // Announce return to normal navigation
            TISpeechMod.Speak("Combat mode ended", interrupt: true);
        }

        private bool HandleCombatModeInput()
        {
            if (combatMode == null) return false;

            // Refresh combat state to detect phase changes
            combatMode.Refresh();

            // Check if combat ended or phase changed
            if (combatMode.CurrentPhase == PreCombatPhase.None)
            {
                ExitCombatMode();
                return true;
            }

            // Navigate options (Numpad 8/2, Up/Down arrows)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                combatMode.Previous();
                TISpeechMod.Speak(combatMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                combatMode.Next();
                TISpeechMod.Speak(combatMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Adjust value left/right (for bidding slider)
            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                combatMode.AdjustValue(increment: false);
                TISpeechMod.Speak(combatMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                combatMode.AdjustValue(increment: true);
                TISpeechMod.Speak(combatMode.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Activate selected option (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                combatMode.Activate();
                // Refresh after action
                combatMode.Refresh();
                return true;
            }

            // Read detail (Numpad *, Minus)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(combatMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List all options (Numpad /, Equals)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(combatMode.ListAllOptions(), interrupt: true);
                return true;
            }

            // Fleet summary (F key)
            if (Input.GetKeyDown(KeyCode.F))
            {
                TISpeechMod.Speak(combatMode.GetFleetSummary(), interrupt: true);
                return true;
            }

            // Exit combat review mode (Escape) - but don't exit combat itself
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TISpeechMod.Speak("Press a stance option to proceed with combat, or Cancel Attack if available", interrupt: true);
                return true;
            }

            // Block other exit keys
            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Cannot exit Review Mode during combat. Select an option to proceed.", interrupt: true);
                return true;
            }

            return false;
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

        /// <summary>
        /// Check if we're in the main menu (pre-game) rather than in-game.
        /// </summary>
        private bool IsInMainMenu()
        {
            // StartMenuController exists when we're in the main menu scene
            return UnityEngine.Object.FindObjectOfType<StartMenuController>() != null &&
                   (GameControl.control == null || GameControl.control.activePlayer == null);
        }

        #endregion
    }

    /// <summary>
    /// Menu context for tracking position in menu hierarchy.
    /// </summary>
    public enum MenuContext
    {
        MainMenu,
        LoadGame,
        NewGame,
        Options,
        Skirmish,
        Mods,
        Credits
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
