using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using TISpeech.ReviewMode.Screens;
using TISpeech.ReviewMode.Sections;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.MenuMode;
using TISpeech.ReviewMode.MenuMode.Screens;
using TISpeech.ReviewMode.EscapeMenu;
using TISpeech.ReviewMode.EscapeMenu.Codex;
using TISpeech.ReviewMode.InputHandlers;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Main controller for review mode.
    /// Uses hierarchical navigation through screens, items, sections, and section items.
    /// </summary>
    public class ReviewModeController : MonoBehaviour
    {
        #region Singleton and State

        private static ReviewModeController instance;
        public static ReviewModeController Instance => instance;

        private bool isActive = false;
        public bool IsActive => isActive;

        // Hierarchical navigation state
        private NavigationState navigation = new NavigationState();

        #endregion

        #region Screens

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
        private LedgerScreen ledgerScreen;
        private EventsScreen eventsScreen;
        private ResourcesScreen resourcesScreen;

        #endregion

        #region Sub-Modes

        private SelectionSubMode selectionMode = null;
        private GridSubMode gridMode = null;
        private NotificationSubMode notificationMode = null;
        private PolicySelectionMode policyMode = null;
        private TransferSubMode transferMode = null;
        private CombatSubMode combatMode = null;
        private MissionTargetSubMode missionTargetMode = null;
        private SpecialPromptSubMode specialPromptMode = null;
        private DiplomacySubMode diplomacyMode = null;
        private ShipDesignerSubMode shipDesignerMode = null;
        private DiplomacyGreetingMode greetingMode = null;
        private EscapeMenuSubMode escapeMenuMode = null;

        public bool IsInNotificationMode => notificationMode != null;
        public bool IsInPolicyMode => policyMode != null;
        public bool IsInTransferMode => transferMode != null;
        public bool IsInCombatMode => combatMode != null;
        public bool IsInMissionTargetMode => missionTargetMode != null;
        public bool IsInSpecialPromptMode => specialPromptMode != null;
        public bool IsInDiplomacyMode => diplomacyMode != null;
        public bool IsInShipDesignerMode => shipDesignerMode != null;
        public bool IsInGreetingMode => greetingMode != null;
        public bool IsInEscapeMenuMode => escapeMenuMode != null;

        #endregion

        #region Menu Mode

        private bool isInMenuMode = false;
        public bool IsInMenuMode => isInMenuMode;
        private List<MenuScreenBase> menuScreens = new List<MenuScreenBase>();
        private int currentMenuScreenIndex = 0;
        private int currentMenuControlIndex = 0;
        private Stack<MenuContext> menuContextStack = new Stack<MenuContext>();

        #endregion

        #region Handlers

        private FleetOperationsHandler fleetOpsHandler;
        private NavigationHelper navigationHelper;
        private NavigationInputHandler navigationInputHandler;
        private MenuInputHandler menuInputHandler;

        #endregion

        #region Input State

        private float lastInputTime = 0f;
        private const float INPUT_DEBOUNCE = 0.15f;
        private bool needToClearHandlingException = false;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                InitializeScreens();
                InitializeHandlers();
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
            nationScreen.OnSectionsInvalidated = () => navigation.RefreshSections();

            orgMarketScreen = new OrgMarketScreen();
            orgMarketScreen.OnEnterSelectionMode = EnterSelectionMode;
            orgMarketScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

            fleetsScreen = new FleetsScreen();
            fleetsScreen.OnEnterSelectionMode = EnterSelectionMode;
            fleetsScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            fleetsScreen.OnEnterTransferMode = EnterTransferMode;
            fleetsScreen.OnExecuteSimpleOperation = (fleet, opType) => fleetOpsHandler?.ExecuteSimpleOperation(fleet, opType);
            fleetsScreen.OnSelectHomeport = (fleet) => fleetOpsHandler?.SelectHomeportForFleet(fleet);
            fleetsScreen.OnSelectMergeTarget = (fleet) => fleetOpsHandler?.SelectMergeTargetForFleet(fleet);
            fleetsScreen.OnExecuteMaintenanceOperation = (fleet, opType) => fleetOpsHandler?.ExecuteMaintenanceOperation(fleet, opType);
            fleetsScreen.OnSelectLandingSite = (fleet) => fleetOpsHandler?.SelectLandingSiteForFleet(fleet);
            fleetsScreen.OnSelectLaunchOrbit = (fleet) => fleetOpsHandler?.SelectLaunchOrbitForFleet(fleet);

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

            ledgerScreen = new LedgerScreen();
            ledgerScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            ledgerScreen.OnSectionsInvalidated = () => navigation.RefreshSections();

            eventsScreen = new EventsScreen();
            eventsScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);
            eventsScreen.OnNavigateToGameState = (target) => navigationHelper?.NavigateToGameState(target);

            resourcesScreen = new ResourcesScreen();
            resourcesScreen.OnSpeak = (text, interrupt) => TISpeechMod.Speak(text, interrupt);

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
                globalStatusScreen,
                ledgerScreen,
                eventsScreen,
                resourcesScreen
            };

            navigation.RegisterScreens(screens);
            MelonLogger.Msg($"Registered {screens.Count} in-game screens for review mode");

            // Create menu screens
            InitializeMenuScreens();
        }

        private void InitializeMenuScreens()
        {
            menuScreens.Clear();
            menuScreens.Add(new MainMenuScreen());              // 0
            menuScreens.Add(new LoadGameScreen());              // 1
            menuScreens.Add(new NewGameScreen());               // 2
            menuScreens.Add(new OptionsScreen());               // 3
            menuScreens.Add(new SkirmishScreen());              // 4
            menuScreens.Add(new ModsScreen());                  // 5
            menuScreens.Add(new TutorialRecommendationScreen()); // 6
            MelonLogger.Msg($"Registered {menuScreens.Count} menu screens for menu mode");
        }

        private void InitializeHandlers()
        {
            // Initialize fleet operations handler
            fleetOpsHandler = new FleetOperationsHandler(
                EnterSelectionMode,
                () => navigation.RefreshSections()
            );

            // Initialize navigation helper
            navigationHelper = new NavigationHelper(navigation);

            // Initialize navigation input handler
            navigationInputHandler = new NavigationInputHandler(
                navigation,
                EnterGridMode,
                OpenEscapeMenu,
                BlockGameEscapeProcessing,
                HandleConfirmAssignments,
                HandleViewModeToggle,
                HandleFactionFilter,
                HandleNationSort,
                HandleProbeAll,
                HandleNationFilter,
                EnterTheoreticalTransferMode
            );

            // Initialize menu input handler
            menuInputHandler = new MenuInputHandler(
                menuScreens,
                () => currentMenuScreenIndex,
                () => currentMenuControlIndex,
                (index) => currentMenuControlIndex = index,
                BlockGameEscapeProcessing,
                ReturnToMainMenu,
                DeactivateReviewMode
            );
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

        public void SwitchToMenuScreen(string screenName)
        {
            if (!isInMenuMode || menuScreens.Count == 0)
                return;

            for (int i = 0; i < menuScreens.Count; i++)
            {
                if (menuScreens[i].Name == screenName)
                {
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
                        TISpeechMod.Speak($"{firstControl}, 1 of {screen.ControlCount}", interrupt: false);
                    }

                    MelonLogger.Msg($"Switched to menu screen: {screenName}");
                    return;
                }
            }

            MelonLogger.Warning($"Menu screen not found: {screenName}");
        }

        public void ReturnToMainMenu()
        {
            SwitchToMenuScreen("Main Menu");
        }

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
                // Clear the handlingException flag if we set it last frame
                if (needToClearHandlingException)
                {
                    GameControl.handlingException = false;
                    needToClearHandlingException = false;
                }

                float currentTime = Time.unscaledTime;
                if (currentTime - lastInputTime < INPUT_DEBOUNCE)
                    return;

                bool inputHandled = false;

                // Auto-detect escape menu opening during in-game mode
                if (!isInMenuMode && escapeMenuMode == null && EscapeMenuSubMode.IsEscapeMenuVisible())
                {
                    escapeMenuMode = new EscapeMenuSubMode();
                    escapeMenuMode.Activate();
                    lastInputTime = currentTime;
                    return;
                }

                // Route input to the appropriate handler
                inputHandled = RouteInput();

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

        #region Input Routing

        private bool RouteInput()
        {
            // If in menu mode, use menu input handler
            if (isInMenuMode)
            {
                // Note: CheckMenuContextChange() is not called here - it was defined but never used
                // in the original code. Menu screen switching is handled by the screens themselves.
                return menuInputHandler.HandleInput();
            }

            // Escape menu mode (in-game pause menu)
            if (escapeMenuMode != null)
            {
                return HandleEscapeMenuModeInput();
            }

            // Combat mode takes highest priority when in pre-combat or live combat
            if (combatMode != null)
            {
                return HandleCombatModeInput();
            }

            // Priority order: Policy > MissionTarget > SpecialPrompt > ShipDesigner >
            // Notification > Greeting > Diplomacy > Transfer > Selection > Grid > Navigation

            if (policyMode != null)
                return HandlePolicyModeInput();

            if (missionTargetMode != null)
                return HandleMissionTargetModeInput();

            if (specialPromptMode != null)
                return HandleSpecialPromptModeInput();

            if (shipDesignerMode != null)
                return HandleShipDesignerModeInput();

            if (notificationMode != null)
                return HandleNotificationModeInput();

            if (greetingMode != null)
                return HandleGreetingModeInput();

            if (diplomacyMode != null)
                return HandleDiplomacyModeInput();

            if (transferMode != null)
                return HandleTransferModeInput();

            if (selectionMode != null)
                return HandleSelectionModeInput();

            if (gridMode != null)
                return HandleGridModeInput();

            // Default: use navigation input handler
            return navigationInputHandler.HandleInput();
        }

        #endregion

        #region Mode Activation

        private void ActivateReviewMode()
        {
            try
            {
                if (IsInMainMenu())
                {
                    ActivateMenuMode();
                    return;
                }

                if (EscapeMenuSubMode.IsEscapeMenuVisible())
                {
                    ActivateEscapeMenuMode();
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

            // Check for pending notifications, combat, or policy selection
            var notificationController = NotificationScreenController.singleton;
            if (notificationController != null &&
                notificationController.singleAlertBox != null &&
                notificationController.singleAlertBox.activeSelf)
            {
                MelonLogger.Msg("Review mode activated with pending notification - entering notification mode");
                EnterNotificationMode(notificationController);
                return;
            }

            if (Patches.SpaceCombatPatches.CheckAndClearCombatPendingFlag())
            {
                MelonLogger.Msg("Review mode activated with combat pending from load - entering combat mode");
                EnterCombatMode();
                return;
            }

            if (CombatSubMode.IsInPreCombat())
            {
                MelonLogger.Msg("Review mode activated during pre-combat - entering combat mode");
                EnterCombatMode();
                return;
            }

            if (PolicySelectionMode.IsPolicySelectionVisible())
            {
                var policyNotificationCtrl = UnityEngine.Object.FindObjectOfType<PavonisInteractive.TerraInvicta.NotificationScreenController>();
                if (policyNotificationCtrl != null)
                {
                    var context = PolicySelectionMode.GetPolicyContext(policyNotificationCtrl);
                    if (context.HasValue)
                    {
                        MelonLogger.Msg($"Review mode activated with policy selection open - entering policy mode");
                        EnterPolicySelectionMode(policyNotificationCtrl, context.Value.nation, context.Value.councilor,
                            context.Value.state, context.Value.currentPolicy);
                        return;
                    }
                }
            }

            if (DiplomacySubMode.IsDiplomacyVisible())
            {
                var diplomacyController = UnityEngine.Object.FindObjectOfType<DiplomacyController>();
                if (diplomacyController != null)
                {
                    MelonLogger.Msg("Review mode activated with diplomacy screen open - entering diplomacy mode");
                    EnterDiplomacyMode(diplomacyController);
                    return;
                }
            }

            // Reset navigation to initial state
            navigation.Reset();

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

                EnterNotificationMode(controller);

                MelonLogger.Msg("Review mode activated directly into notification mode");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForNotification: {ex.Message}");
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
            currentMenuScreenIndex = 0;
            lastInputTime = Time.unscaledTime; // Debounce to prevent activation key from triggering menu input

            if (menuScreens.Count > 0 && currentMenuScreenIndex < menuScreens.Count)
            {
                var screen = menuScreens[currentMenuScreenIndex];
                screen.OnActivate();

                string announcement = $"Menu navigation. {screen.GetActivationAnnouncement()} ";
                announcement += "Use arrows to navigate, Enter to activate, Escape to exit.";
                TISpeechMod.Speak(announcement, interrupt: true);

                if (screen.ControlCount > 0)
                {
                    string firstControl = screen.ReadControl(0);
                    TISpeechMod.Speak($"{firstControl}, 1 of {screen.ControlCount}", interrupt: false);
                }
            }
            else
            {
                TISpeechMod.Speak("Menu navigation. No menus available.", interrupt: true);
            }

            MelonLogger.Msg($"Menu mode activated with screen index {currentMenuScreenIndex}");
        }

        private void ActivateEscapeMenuMode()
        {
            TIInputManager.BlockKeybindings();
            isActive = true;
            isInMenuMode = false;

            escapeMenuMode = new EscapeMenuSubMode();
            escapeMenuMode.Activate();

            MelonLogger.Msg("Escape menu mode activated");
        }

        private void DeactivateReviewMode()
        {
            try
            {
                if (!isInMenuMode && escapeMenuMode == null)
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
                escapeMenuMode = null;
                shipDesignerMode = null;
                greetingMode = null;
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

        #region Helpers

        private void BlockGameEscapeProcessing()
        {
            GameControl.handlingException = true;
            needToClearHandlingException = true;
        }

        private void OpenEscapeMenu()
        {
            var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
            if (optionsScreen != null)
            {
                optionsScreen.Show();
                escapeMenuMode = new EscapeMenuSubMode();
                escapeMenuMode.Activate();
            }
            else
            {
                TISpeechMod.Speak("Could not open menu", interrupt: true);
            }
        }

        private bool IsGameReady()
        {
            return GameControl.control != null &&
                   GameControl.control.activePlayer != null;
        }

        private bool IsInMainMenu()
        {
            return UnityEngine.Object.FindObjectOfType<StartMenuController>() != null &&
                   (GameControl.control == null || GameControl.control.activePlayer == null);
        }

        private char? GetPressedLetter()
        {
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    return (char)('A' + (key - KeyCode.A));
                }
            }
            return null;
        }

        #endregion

        #region Navigation Helpers

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

            while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.CurrentLevel != NavigationLevel.Items)
            {
                navigation.BackOut();
            }

            if (navigation.CurrentLevel == NavigationLevel.Screens)
            {
                navigation.DrillDown();
            }

            string announcement = screen.ToggleViewMode();
            navigation.ResetItemIndex();
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private bool HandleFactionFilter(bool previous)
        {
            var screen = navigation.CurrentScreen;
            if (screen == null)
            {
                TISpeechMod.Speak("No screen active", interrupt: true);
                return true;
            }

            if (!screen.SupportsFactionFilter)
            {
                TISpeechMod.Speak("This screen does not support faction filtering", interrupt: true);
                return true;
            }

            string announcement = previous ? screen.PreviousFactionFilter() : screen.NextFactionFilter();
            if (announcement != null)
            {
                navigation.ResetItemIndex();
                TISpeechMod.Speak(announcement, interrupt: true);
            }
            return true;
        }

        private bool HandleNationSort()
        {
            var nationScr = navigation.CurrentScreen as Screens.NationScreen;
            if (nationScr != null)
            {
                nationScr.StartSortSelection();
                return true;
            }

            var spaceBodiesScr = navigation.CurrentScreen as Screens.SpaceBodiesScreen;
            if (spaceBodiesScr != null)
            {
                spaceBodiesScr.StartSortSelection();
                return true;
            }

            return false;
        }

        private bool HandleNationFilter()
        {
            var screen = navigation.CurrentScreen as Screens.NationScreen;
            if (screen == null)
                return false;

            screen.CycleFactionFilter();
            navigation.ResetItemIndex();
            return true;
        }

        private bool HandleProbeAll()
        {
            var screen = navigation.CurrentScreen as Screens.SpaceBodiesScreen;
            if (screen == null)
                return false;

            screen.StartProbeAll();
            return true;
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
            string announcement = $"{prompt}. {options.Count} options. {firstOption.Label}, 1 of {options.Count}. Use arrows to browse, Enter to select, minus for detail, Escape to cancel.";
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private bool HandleSelectionModeInput()
        {
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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                ConfirmSelection();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Keypad0) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();
                CancelSelection();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                AnnounceSelectionDetail();
                return true;
            }

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

        private void AnnounceSelectionItem()
        {
            if (selectionMode == null) return;
            var option = selectionMode.CurrentOption;
            TISpeechMod.Speak($"{option.Label}, {selectionMode.CurrentIndex + 1} of {selectionMode.Count}", interrupt: true);
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

        #region Grid Sub-Mode

        private void EnterGridMode(PriorityGridSection grid)
        {
            gridMode = new GridSubMode(grid);
            string announcement = grid.GetEntryAnnouncement();
            announcement += $" {gridMode.GetCellAnnouncement()}";
            TISpeechMod.Speak(announcement, interrupt: true);
        }

        private void ExitGridMode()
        {
            gridMode = null;
            navigation.BackOut();
            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
        }

        private bool HandleGridModeInput()
        {
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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    gridMode.MassCycleCurrentRow(decrement: false);
                else
                    gridMode.CycleCurrentCell(decrement: false);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    gridMode.MassCycleCurrentRow(decrement: true);
                else
                    gridMode.CycleCurrentCell(decrement: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                TISpeechMod.Speak(gridMode.GetRowSummary(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(gridMode.GetColumnSummary(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                gridMode.SyncFromCurrentColumn();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                gridMode.StartPresetSelection();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                string description = gridMode.GetPriorityDescription();
                if (!string.IsNullOrWhiteSpace(description))
                    TISpeechMod.Speak(description, interrupt: true);
                else
                    TISpeechMod.Speak("No description available", interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameEscapeProcessing();
                ExitGridMode();
                return true;
            }

            return false;
        }

        #endregion

        #region Notification Sub-Mode

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

        public void ExitNotificationMode()
        {
            if (notificationMode == null)
                return;

            notificationMode = null;
            MelonLogger.Msg("Exited notification mode");
        }

        private bool HandleNotificationModeInput()
        {
            if (notificationMode == null) return false;

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                var option = notificationMode.CurrentOption;
                if (option != null)
                {
                    TISpeechMod.Speak($"Activating {option.Label}", interrupt: true);
                    notificationMode.Activate();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(notificationMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(notificationMode.ListAllOptions(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameEscapeProcessing();
                if (notificationMode.SelectCloseOption())
                {
                    TISpeechMod.Speak($"Closing notification", interrupt: true);
                    notificationMode.Activate();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Cannot exit Review Mode while notification is open. Use Enter to select an option, or Escape to close.", interrupt: true);
                return true;
            }

            if (TimeControlHandler.HandleInput())
                return true;

            return false;
        }

        #endregion

        #region Escape Menu Mode

        private bool HandleEscapeMenuModeInput()
        {
            if (escapeMenuMode == null)
                return false;

            // TEXT INPUT MODE
            if (escapeMenuMode.IsEnteringText)
            {
                return HandleEscapeMenuTextInput();
            }

            // CODEX MODE
            if (escapeMenuMode.IsInCodexMode)
            {
                return HandleCodexModeInput();
            }

            // Check if escape menu was closed
            if (!escapeMenuMode.IsInActivationGracePeriod() && !EscapeMenuSubMode.IsEscapeMenuVisible())
            {
                escapeMenuMode.Deactivate();
                escapeMenuMode = null;
                TISpeechMod.Speak("Returned to game. Review mode.", interrupt: true);

                navigation.Reset();
                var screen = navigation.CurrentScreen;
                if (screen != null)
                {
                    TISpeechMod.Speak(screen.GetActivationAnnouncement(), interrupt: false);
                }
                return true;
            }

            escapeMenuMode.CheckContextChange();

            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                escapeMenuMode.NavigatePrevious();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                escapeMenuMode.NavigateNext();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                escapeMenuMode.AdjustCurrentControl(increment: false);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                escapeMenuMode.AdjustCurrentControl(increment: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                escapeMenuMode.ActivateCurrentControl();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && !escapeMenuMode.IsInActivationGracePeriod())
            {
                BlockGameEscapeProcessing();
                escapeMenuMode.InvokeBackToGame();
                escapeMenuMode.Deactivate();
                escapeMenuMode = null;

                navigation.Reset();
                var screen = navigation.CurrentScreen;
                if (screen != null)
                {
                    TISpeechMod.Speak(screen.GetActivationAnnouncement(), interrupt: false);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                escapeMenuMode.ReadDetail();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                escapeMenuMode.ListAllControls();
                return true;
            }

            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                escapeMenuMode.NavigateByLetter(letter.Value);
                return true;
            }

            return false;
        }

        private bool HandleCodexModeInput()
        {
            if (escapeMenuMode == null || !escapeMenuMode.IsInCodexMode)
                return false;

            var codexMode = escapeMenuMode.CodexMode;
            if (codexMode == null)
                return false;

            escapeMenuMode.CheckContextChange();

            if (!escapeMenuMode.IsInCodexMode)
                return true;

            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                codexMode.NavigatePrevious();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                codexMode.NavigateNext();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                codexMode.DrillDown();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) ||
                Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                if (codexMode.CurrentLevel == CodexSubMode.NavigationLevel.Content)
                {
                    codexMode.BackOut();
                }
                else
                {
                    codexMode.CloseCodex();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                codexMode.ReadDetail();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                codexMode.ListAllItems();
                return true;
            }

            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                codexMode.NavigateByLetter(letter.Value);
                return true;
            }

            return false;
        }

        private bool HandleEscapeMenuTextInput()
        {
            if (escapeMenuMode == null || !escapeMenuMode.IsEnteringText)
                return false;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                string result = escapeMenuMode.ApplyTextInput();
                TISpeechMod.Speak(result, interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameEscapeProcessing();
                string result = escapeMenuMode.CancelTextInput();
                TISpeechMod.Speak(result, interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (escapeMenuMode.HandleBackspace())
                {
                    if (string.IsNullOrEmpty(escapeMenuMode.TextInput))
                        TISpeechMod.Speak("empty", interrupt: true);
                    else
                        TISpeechMod.Speak(escapeMenuMode.TextInput, interrupt: true);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (escapeMenuMode.HandleCharacter(' '))
                {
                    TISpeechMod.Speak("space", interrupt: true);
                }
                return true;
            }

            for (int i = 0; i < 26; i++)
            {
                KeyCode keyCode = (KeyCode)((int)KeyCode.A + i);
                if (Input.GetKeyDown(keyCode))
                {
                    bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    char c = shiftHeld ? (char)('A' + i) : (char)('a' + i);
                    if (escapeMenuMode.HandleCharacter(c))
                    {
                        TISpeechMod.Speak(c.ToString(), interrupt: true);
                    }
                    return true;
                }
            }

            for (int i = 0; i <= 9; i++)
            {
                KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha0 + i);
                KeyCode numpadKey = (KeyCode)((int)KeyCode.Keypad0 + i);

                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(numpadKey))
                {
                    char c = (char)('0' + i);
                    if (escapeMenuMode.HandleCharacter(c))
                    {
                        TISpeechMod.Speak(c.ToString(), interrupt: true);
                    }
                    return true;
                }
            }

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                char c = shiftHeld ? '_' : '-';
                if (escapeMenuMode.HandleCharacter(c))
                {
                    TISpeechMod.Speak(c == '_' ? "underscore" : "dash", interrupt: true);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                if (escapeMenuMode.HandleCharacter('.'))
                {
                    TISpeechMod.Speak("period", interrupt: true);
                }
                return true;
            }

            return false;
        }

        #endregion

        #region Diplomacy Greeting Mode

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                if (greetingMode.Activate())
                {
                    TISpeechMod.Speak("Continuing to trade", interrupt: true);
                    ExitGreetingMode();
                }
                else
                {
                    TISpeechMod.Speak(greetingMode.CurrentItem, interrupt: true);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(greetingMode.GetFullContent(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameEscapeProcessing();
                TISpeechMod.Speak("Cancelling diplomacy", interrupt: true);
                ExitGreetingMode();
                return true;
            }

            return false;
        }

        #endregion

        #region Diplomacy Sub-Mode

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

            // QUANTITY INPUT MODE
            if (diplomacyMode.IsEnteringQuantity)
            {
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

                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    diplomacyMode.HandleBackspace();
                    TISpeechMod.Speak(diplomacyMode.GetQuantityInputAnnouncement(), interrupt: true);
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.Keypad5))
                {
                    string result = diplomacyMode.ApplyQuantity();
                    TISpeechMod.Speak(result, interrupt: true);
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    string result = diplomacyMode.CancelQuantityMode();
                    TISpeechMod.Speak(result, interrupt: true);
                    return true;
                }

                return false;
            }

            // NORMAL NAVIGATION MODE
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

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                if (diplomacyMode.BackOut())
                {
                    TISpeechMod.Speak(diplomacyMode.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Exiting diplomacy", interrupt: true);
                    diplomacyMode.CloseDiplomacyWindow();
                    ExitDiplomacyMode();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                string result = diplomacyMode.RemoveCurrentItem();
                TISpeechMod.Speak(result, interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(diplomacyMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(diplomacyMode.ListCurrentSection(), interrupt: true);
                return true;
            }

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

        #region Policy Selection Mode

        public void EnterPolicySelectionMode(PavonisInteractive.TerraInvicta.NotificationScreenController controller, TINationState nation, TICouncilorState councilor)
        {
            EnterPolicySelectionMode(controller, nation, councilor, PolicySelectionState.SelectPolicy, null);
        }

        public void EnterPolicySelectionMode(PavonisInteractive.TerraInvicta.NotificationScreenController controller,
            TINationState nation, TICouncilorState councilor, PolicySelectionState initialState, TIPolicyOption currentPolicy)
        {
            try
            {
                if (controller == null || nation == null)
                {
                    MelonLogger.Error("EnterPolicySelectionMode: controller or nation is null");
                    return;
                }

                policyMode = new PolicySelectionMode(controller, nation, councilor, initialState, currentPolicy);

                if (policyMode.Policies.Count == 0)
                {
                    MelonLogger.Msg("No policies available, staying in standard Review Mode");
                    policyMode = null;
                    return;
                }

                TISpeechMod.Speak(policyMode.GetEntryAnnouncement(), interrupt: true);
                MelonLogger.Msg($"Entered policy selection mode for {nation.displayName} with {policyMode.Policies.Count} policies, state: {initialState}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error entering policy selection mode: {ex.Message}");
                policyMode = null;
            }
        }

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                bool continueMode = policyMode.Activate();
                if (!continueMode)
                {
                    policyMode = null;
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(policyMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(policyMode.ListAll(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                bool stayInMode = policyMode.GoBack();
                if (!stayInMode)
                {
                    TISpeechMod.Speak("Cancelled policy selection", interrupt: true);
                    policyMode = null;
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Cannot exit Review Mode during policy selection. Press Escape to cancel.", interrupt: true);
                return true;
            }

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

            if (TimeControlHandler.HandleInput())
                return true;

            return false;
        }

        #endregion

        #region Mission Target Mode

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

                EnterMissionTargetMode(controller, promptType);

                MelonLogger.Msg("Review mode activated directly into mission target mode");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForMissionTarget: {ex.Message}");
                isActive = false;
                TIInputManager.RestoreKeybindings();
            }
        }

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

            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
            {
                if (missionTargetMode.HasSelection)
                {
                    TISpeechMod.Speak("Confirming target selection", interrupt: true);
                    missionTargetMode.Confirm();
                }
                else
                {
                    TISpeechMod.Speak("Select a target first by pressing Enter", interrupt: true);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(missionTargetMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(missionTargetMode.ListAllOptions(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                TISpeechMod.Speak("Cancelling and aborting mission", interrupt: true);
                missionTargetMode.Cancel();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Cannot exit Review Mode during target selection. Press Escape to cancel.", interrupt: true);
                return true;
            }

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

            if (TimeControlHandler.HandleInput())
                return true;

            return false;
        }

        #endregion

        #region Special Prompt Mode

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

                EnterSpecialPromptMode(controller, promptType);

                MelonLogger.Msg($"Review mode activated directly into special prompt mode: {promptType}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ActivateForSpecialPrompt: {ex.Message}");
                isActive = false;
                TIInputManager.RestoreKeybindings();
            }
        }

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                var option = specialPromptMode.CurrentOption;
                if (option != null)
                {
                    if (option.IsInformational)
                    {
                        TISpeechMod.Speak(option.Label, interrupt: true);
                    }
                    else
                    {
                        TISpeechMod.Speak($"Activating: {option.Label}", interrupt: true);
                        specialPromptMode.Activate();
                    }
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(specialPromptMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(specialPromptMode.ListAllOptions(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Exiting Review Mode. Prompt still pending.", interrupt: true);
                DeactivateReviewMode();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                TISpeechMod.Speak("Exiting Review Mode. Prompt still pending.", interrupt: true);
                DeactivateReviewMode();
                return true;
            }

            if (TimeControlHandler.HandleInput())
                return true;

            return false;
        }

        #endregion

        #region Ship Designer Mode

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
                    TISpeechMod.Speak($"{prompt}. Using default name. Text input not yet implemented.", interrupt: true);
                    callback(null);
                };

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

        public void ExitShipDesignerMode()
        {
            shipDesignerMode = null;
            MelonLogger.Msg("Exited ship designer mode");
        }

        private bool HandleShipDesignerModeInput()
        {
            if (shipDesignerMode == null) return false;

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                shipDesignerMode.Select();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                shipDesignerMode.Back();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = shipDesignerMode.ReadCurrentDetail();
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
            {
                if (shipDesignerMode.CurrentStep == DesignerStep.NavigateZoneItems &&
                    shipDesignerMode.CurrentZone == DesignZone.Propulsion &&
                    shipDesignerMode.CurrentZoneItemIndex == 3)
                {
                    shipDesignerMode.AdjustPropellant(1);
                }
                else if (shipDesignerMode.CurrentStep == DesignerStep.SelectComponent)
                {
                    shipDesignerMode.AdjustArmor(1);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                if (shipDesignerMode.CurrentStep == DesignerStep.NavigateZoneItems &&
                    shipDesignerMode.CurrentZone == DesignZone.Propulsion &&
                    shipDesignerMode.CurrentZoneItemIndex == 3)
                {
                    shipDesignerMode.AdjustPropellant(-1);
                }
                else if (shipDesignerMode.CurrentStep == DesignerStep.SelectComponent)
                {
                    shipDesignerMode.AdjustArmor(-1);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                shipDesignerMode.ApplyAutodesign();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0) ||
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R)))
            {
                TISpeechMod.Speak("Press Escape to exit designer first", interrupt: true);
                return true;
            }

            return false;
        }

        #endregion

        #region Transfer Sub-Mode

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

            transferMode.AnnounceCurrentItem();
        }

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
                transferMode = null;
                return;
            }

            try
            {
                var fleet = transferMode.Fleet;

                var transferOp = OperationsManager.operationsLookup[typeof(TransferOperation)] as TISpaceFleetOperationTemplate;
                if (transferOp == null)
                {
                    MelonLogger.Error("Could not find TransferOperation");
                    TISpeechMod.Speak("Error: Transfer operation not found", interrupt: true);
                    transferMode = null;
                    return;
                }

                var target = trajectory.destination ?? trajectory.destinationOrbit?.ref_gameState;
                bool success = transferOp.OnOperationConfirm(fleet, target, null, trajectory);

                if (success)
                {
                    string dest = trajectory.destination?.displayName ?? "destination";
                    TISpeechMod.Speak($"Transfer assigned. {fleet.displayName} departing for {dest}", interrupt: true);
                    MelonLogger.Msg($"Transfer assigned: {fleet.displayName} -> {dest}");
                }
                else
                {
                    MelonLogger.Warning("TransferOperation.OnOperationConfirm returned false");
                    TISpeechMod.Speak("Transfer could not be assigned", interrupt: true);
                }
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

            // Handle numeric input mode
            if (transferMode.IsInputStep)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        transferMode.HandleDigit((char)('0' + i));
                        return true;
                    }
                }

                if (Input.GetKeyDown(KeyCode.Period))
                {
                    transferMode.HandleDecimal();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    transferMode.HandleBackspace();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    transferMode.Select();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    BlockGameEscapeProcessing();
                    transferMode.Back();
                    return true;
                }

                return false;
            }

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                transferMode.Select();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) ||
                Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    BlockGameEscapeProcessing();

                transferMode.Back();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = transferMode.ReadCurrentItemDetail();
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                transferMode.CycleSortMode();
                return true;
            }

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

        public void CheckForCombatMode()
        {
            try
            {
                if (!isActive || combatMode != null)
                    return;

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

        public void EnterCombatMode()
        {
            try
            {
                if (combatMode != null)
                {
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

        public void ExitCombatMode()
        {
            if (combatMode == null)
                return;

            combatMode = null;
            MelonLogger.Msg("Exited combat mode");

            TISpeechMod.Speak("Combat mode ended", interrupt: true);
        }

        private bool HandleCombatModeInput()
        {
            if (combatMode == null) return false;

            combatMode.Refresh();

            if (combatMode.CurrentPhase == PreCombatPhase.None)
            {
                ExitCombatMode();
                return true;
            }

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

            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Return))
            {
                combatMode.Activate();
                combatMode.Refresh();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(combatMode.GetCurrentDetail(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(combatMode.ListAllOptions(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                TISpeechMod.Speak(combatMode.GetFleetSummary(), interrupt: true);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameEscapeProcessing();
                TISpeechMod.Speak("Press a stance option to proceed with combat, or Cancel Attack if available", interrupt: true);
                return true;
            }

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

                var missionPhase = GameStateManager.MissionPhase();
                if (missionPhase == null || !missionPhase.phaseActive)
                {
                    TISpeechMod.Speak("Not in mission phase", interrupt: true);
                    return;
                }

                if (missionPhase.factionsSignallingComplete.Contains(faction))
                {
                    TISpeechMod.Speak("Assignments already confirmed", interrupt: true);
                    return;
                }

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
    }

    #region Supporting Types

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

        public int FindNextOptionByLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);

            for (int i = CurrentIndex + 1; i < Options.Count; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            for (int i = 0; i <= CurrentIndex; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            return -1;
        }

        public void SetIndex(int index)
        {
            if (index >= 0 && index < Options.Count)
                CurrentIndex = index;
        }
    }

    public class GridSubMode
    {
        public PriorityGridSection Grid { get; }
        public int CurrentRow { get; private set; }
        public int CurrentColumn { get; private set; }

        public int RowCount => Grid.RowCount;
        public int ColumnCount => Grid.ColumnCount;

        public GridSubMode(PriorityGridSection grid)
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

        public string GetCellAnnouncement() => Grid.ReadCell(CurrentRow, CurrentColumn);
        public string GetRowSummary() => Grid.ReadRowSummary(CurrentRow);
        public string GetColumnSummary() => Grid.ReadColumnSummary(CurrentColumn);
        public bool CanEditCurrentCell() => Grid.CanEditCell(CurrentRow, CurrentColumn);
        public void CycleCurrentCell(bool decrement = false) => Grid.CycleCell(CurrentRow, CurrentColumn, decrement);
        public void MassCycleCurrentRow(bool decrement = false) => Grid.MassCycleRow(CurrentRow, decrement);
        public void SyncFromCurrentColumn() => Grid.SyncFromCP(CurrentColumn);
        public void ToggleDisplayMode() => Grid.ToggleDisplayMode();
        public void StartPresetSelection() => Grid.StartPresetSelection();
        public string GetPriorityDescription() => Grid.GetPriorityDescription(CurrentRow);
    }

    #endregion
}
