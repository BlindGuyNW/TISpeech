using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PavonisInteractive.TerraInvicta;
using ModelShark;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the New Game / Campaign Setup menu.
    /// Provides navigation through:
    /// - Scenario selection (Scenario, FactionCouncils, SolarSystem dropdowns)
    /// - Faction selection and participation toggles
    /// - Difficulty selection and customization sliders
    /// - Toggle options (tutorial, variable projects, realism, etc.)
    /// - Additional dropdowns (councilor professions, ship naming, nation group)
    /// - Faction customization input fields
    /// - Start campaign buttons
    /// </summary>
    public class NewGameScreen : MenuScreenBase
    {
        public override string Name => "New Game";

        private List<MenuControl> controls = new List<MenuControl>();
        private StartMenuController menuController;
        private List<FactionToggleListItemController> factionToggles = new List<FactionToggleListItemController>();

        // Action identifiers for localization-safe control identification
        private const string ACTION_SCENARIO = "Scenario";
        private const string ACTION_FACTION_COUNCILS = "FactionCouncils";
        private const string ACTION_SOLAR_SYSTEM = "SolarSystem";
        private const string ACTION_YOUR_FACTION = "YourFaction";
        private const string ACTION_DIFFICULTY = "Difficulty";

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Override ReadControl to refresh slider values from the game's UI text.
        /// Terra Invicta uses value * 0.05 = multiplier, so we need to read from
        /// the game's formatted valueText, not calculate our own percentage.
        /// </summary>
        public override string ReadControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For sliders, read the value from the game's UI text
            if (control.Type == MenuControlType.Slider && !string.IsNullOrEmpty(control.Action))
            {
                string gameValue = GetSliderValueFromGame(control.Action);
                if (!string.IsNullOrEmpty(gameValue))
                {
                    control.CurrentValue = gameValue;
                }
            }
            else
            {
                // For other controls, use standard refresh
                control.RefreshValue();
            }

            return control.GetAnnouncement();
        }

        /// <summary>
        /// Check if the New Game panel is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var controller = UnityEngine.Object.FindObjectOfType<StartMenuController>();
            if (controller == null)
                return false;

            // Check if the MenuManager has a current menu open that's not the start menu
            // If so, we're not in new game (we're in load, options, etc.)
            if (controller.menuManager != null &&
                controller.menuManager.currentMenu != null &&
                controller.menuManager.currentMenu != controller.menuManager.startMenu)
            {
                return false;
            }

            // Check if the faction dropdown is active AND interactable (indicates new game panel is shown)
            if (controller.newCampaignChooseFactionDropdown != null &&
                controller.newCampaignChooseFactionDropdown.gameObject.activeInHierarchy &&
                controller.newCampaignChooseFactionDropdown.IsInteractable())
            {
                return true;
            }

            // Also check if start campaign buttons are visible and interactable
            if (controller.startLongCampaignButton != null &&
                controller.startLongCampaignButton.gameObject.activeInHierarchy &&
                controller.startLongCampaignButton.interactable)
            {
                return true;
            }

            return false;
        }

        public override void Refresh()
        {
            controls.Clear();
            factionToggles.Clear();
            sliderValueTexts.Clear();
            controlTooltips.Clear();

            try
            {
                menuController = UnityEngine.Object.FindObjectOfType<StartMenuController>();
                if (menuController == null)
                {
                    MelonLogger.Msg("NewGameScreen: StartMenuController not found");
                    return;
                }

                // ===== SCENARIO SELECTION DROPDOWNS =====
                // These come from NewGameOptionController instances in newCampaignOptionList
                AddScenarioDropdowns();

                // ===== FACTION SELECTION =====
                AddFactionSelection();

                // ===== DIFFICULTY SELECTION =====
                AddDifficultySelection();

                // ===== DIFFICULTY CUSTOMIZATION SLIDERS =====
                AddDifficultySliders();

                // ===== TOGGLE OPTIONS =====
                AddToggleOptions();

                // ===== ADDITIONAL DROPDOWNS (councilor professions, naming, nation group) =====
                AddAdditionalDropdowns();

                // ===== FACTION CUSTOMIZATION INPUT FIELDS =====
                AddFactionCustomizationInputs();

                // ===== FACTION PARTICIPATION TOGGLES =====
                AddFactionParticipationToggles();

                // ===== START BUTTONS =====
                AddStartButtons();

                MelonLogger.Msg($"NewGameScreen: Found {controls.Count} controls, {factionToggles.Count} faction toggles");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing NewGameScreen: {ex.Message}");
            }
        }

        /// <summary>
        /// Add scenario selection dropdowns (Scenario, FactionCouncils, SolarSystem).
        /// These are managed by NewGameOptionController instances.
        /// </summary>
        private void AddScenarioDropdowns()
        {
            if (menuController.newCampaignOptionList == null)
                return;

            var optionControllers = menuController.newCampaignOptionList.GetComponentsInChildren<NewGameOptionController>(includeInactive: false);
            if (optionControllers == null || optionControllers.Length == 0)
                return;

            foreach (var optionController in optionControllers)
            {
                if (optionController.optionDropdown == null || !optionController.optionDropdown.gameObject.activeInHierarchy)
                    continue;

                // The category is stored in the GameObject name (set by InitWithMetaTemplateCategory)
                string category = optionController.gameObject.name;

                // Get the localized label from the optionName TMP_Text
                string label = optionController.optionName != null
                    ? TISpeechMod.CleanText(optionController.optionName.text)
                    : category; // Fall back to category name if no text

                var control = MenuControl.FromDropdown(optionController.optionDropdown, label);
                if (control != null)
                {
                    // Use the category as the action identifier for localization-safe handling
                    control.Action = category;
                    controls.Add(control);
                }
            }
        }

        /// <summary>
        /// Add faction selection dropdown.
        /// </summary>
        private void AddFactionSelection()
        {
            if (menuController.newCampaignChooseFactionDropdown == null ||
                !menuController.newCampaignChooseFactionDropdown.gameObject.activeInHierarchy)
                return;

            // Get label from the header text (localized)
            string label = menuController.selectFactionDropdownHeader != null
                ? TISpeechMod.CleanText(menuController.selectFactionDropdownHeader.text)
                : "Your Faction";

            var factionControl = MenuControl.FromDropdown(
                menuController.newCampaignChooseFactionDropdown,
                label);

            if (factionControl != null)
            {
                factionControl.Action = ACTION_YOUR_FACTION;

                // Add faction description as detail
                if (menuController.selectedFactionDescription != null)
                {
                    factionControl.DetailText = TISpeechMod.CleanText(
                        menuController.selectedFactionDescription.text);
                }
                controls.Add(factionControl);
            }
        }

        /// <summary>
        /// Add difficulty selection dropdown and tutorial toggle.
        /// </summary>
        private void AddDifficultySelection()
        {
            // Difficulty dropdown
            if (menuController.selectDifficultyDropdown != null &&
                menuController.selectDifficultyDropdown.gameObject.activeInHierarchy)
            {
                string label = menuController.selectDifficultyHeader != null
                    ? TISpeechMod.CleanText(menuController.selectDifficultyHeader.text)
                    : "Difficulty";

                var difficultyControl = MenuControl.FromDropdown(
                    menuController.selectDifficultyDropdown,
                    label);

                if (difficultyControl != null)
                {
                    difficultyControl.Action = ACTION_DIFFICULTY;
                    controls.Add(difficultyControl);
                }
            }

            // Tutorial toggle
            if (menuController.tutorialToggle != null &&
                menuController.tutorialToggle.gameObject.activeInHierarchy)
            {
                string label = menuController.tutorialToggleText != null
                    ? TISpeechMod.CleanText(menuController.tutorialToggleText.text)
                    : "Tutorial";

                var tutorialControl = MenuControl.FromToggle(menuController.tutorialToggle, label);
                if (tutorialControl != null)
                {
                    tutorialControl.Action = "Tutorial";
                    controls.Add(tutorialControl);
                }
            }
        }

        /// <summary>
        /// Add all difficulty customization sliders.
        /// Sliders may be in a scrollable area, so we check for component existence
        /// rather than strict activeInHierarchy (they're always present on new game panel).
        /// </summary>
        private void AddDifficultySliders()
        {
            // Sliders exist on the new game panel but may be in a scroll view
            // Check if the slider exists and its root is active (the new game panel itself)
            if (menuController.researchSpeedMultiplierSlider == null)
                return;

            // Check if the new game panel is showing at all by checking if a core element is active
            if (!menuController.newCampaignChooseFactionDropdown.gameObject.activeInHierarchy)
                return;

            controls.Add(CreateDivider("--- Difficulty Settings ---"));

            // Research Speed (use Title text for localized label)
            AddSliderControl(
                menuController.researchSpeedMultiplierSlider,
                menuController.researchSpeedTitle,
                menuController.researchSpeedValue,
                "Research Speed",
                "researchSpeed",
                menuController.researchSpeedTooltip);

            // Alien Progression
            AddSliderControl(
                menuController.alienProgressionMultiplierSlider,
                menuController.alienProgressionRateTitle,
                menuController.alienProgressionRateValue,
                "Alien Progression",
                "alienProgression",
                menuController.alienProgressionTooltip);

            // Mining Productivity
            AddSliderControl(
                menuController.miningProductivityMultiplierSlider,
                menuController.miningProductivityTitle,
                menuController.miningProductivityValue,
                "Mining Productivity",
                "miningProductivity",
                menuController.miningProductivityTooltip);

            // National IP Modifier
            AddSliderControl(
                menuController.nationalIPModifierSlider,
                menuController.nationalIPModifierTitle,
                menuController.nationalIPModifierValue,
                "National IP Modifier",
                "nationalIP",
                menuController.nationalIPModifierTooltip);

            // Average Monthly Events
            AddSliderControl(
                menuController.averageMonthlyEventsModifierSlider,
                menuController.averageMonthlyEventsModifierTitle,
                menuController.averageMonthlyEventsModifierValue,
                "Monthly Events",
                "monthlyEvents",
                menuController.averageMonthlyEventsModifierTooltip);

            // Control Point Freebies (Player)
            AddSliderControl(
                menuController.controlPointFreebieBonusSlider,
                menuController.controlPointFreebieTitle,
                menuController.controlPointFreebieValue,
                "Player Control Points",
                "cpPlayer",
                menuController.CPFreebieTooltip);

            // Control Point Freebies (AI)
            AddSliderControl(
                menuController.controlPointAIFreebieBonusSlider,
                menuController.controlPointAIFreebieTitle,
                menuController.controlPointAIFreebieValue,
                "AI Control Points",
                "cpAI",
                menuController.AICPFreebieTooltip);

            // Mission Control Freebies (Player)
            AddSliderControl(
                menuController.missionControlFreebieBonusSlider,
                menuController.missionControlFreebieTitle,
                menuController.missionControlFreebieValue,
                "Player Mission Control",
                "mcPlayer",
                menuController.MCFreebieTooltip);

            // Mission Control Freebies (AI)
            AddSliderControl(
                menuController.missionControlAIFreebieBonusSlider,
                menuController.missionControlAIFreebieTitle,
                menuController.missionControlAIFreebieValue,
                "AI Mission Control",
                "mcAI",
                menuController.AIMCFreebieTooltip);

            // Mining Rates section
            AddSliderControl(
                menuController.miningRatePlayerSlider,
                menuController.miningRatePlayerTitle,
                menuController.miningRatePlayerValue,
                "Player Mining Rate",
                "miningRatePlayer",
                menuController.miningRatePlayerTooltip);

            AddSliderControl(
                menuController.miningRateHumanAISlider,
                menuController.miningRateHumanAITitle,
                menuController.miningRateHumanAIValue,
                "AI Mining Rate",
                "miningRateAI",
                menuController.miningRateHumanAITooltip);

            AddSliderControl(
                menuController.miningRateAlienSlider,
                menuController.miningRateAlienTitle,
                menuController.miningRateAlienValue,
                "Alien Mining Rate",
                "miningRateAlien",
                menuController.miningRateAlienTooltip);

            // Hab Construction Speed section
            AddSliderControl(
                menuController.habConstructionSpeedPlayerSlider,
                menuController.habConstructionSpeedPlayerTitle,
                menuController.habConstructionSpeedPlayerValue,
                "Player Hab Construction",
                "habSpeedPlayer",
                menuController.habConstructionSpeedPlayerTooltip);

            AddSliderControl(
                menuController.habConstructionSpeedHumanAISlider,
                menuController.habConstructionSpeedHumanAITitle,
                menuController.habConstructionSpeedHumanAIValue,
                "AI Hab Construction",
                "habSpeedAI",
                menuController.habConstructionSpeedHumanAITooltip);

            AddSliderControl(
                menuController.habConstructionSpeedAlienSlider,
                menuController.habConstructionSpeedAlienTitle,
                menuController.habConstructionSpeedAlienValue,
                "Alien Hab Construction",
                "habSpeedAlien",
                menuController.habConstructionSpeedAlienTooltip);

            // Ship Construction Speed section
            AddSliderControl(
                menuController.shipConstructionSpeedPlayerSlider,
                menuController.shipConstructionSpeedPlayerTitle,
                menuController.shipConstructionSpeedPlayerValue,
                "Player Ship Construction",
                "shipSpeedPlayer",
                menuController.shipConstructionSpeedPlayerTooltip);

            AddSliderControl(
                menuController.shipConstructionSpeedHumanAISlider,
                menuController.shipConstructionSpeedHumanAITitle,
                menuController.shipConstructionSpeedHumanAIValue,
                "AI Ship Construction",
                "shipSpeedAI",
                menuController.shipConstructionSpeedHumanAITooltip);

            AddSliderControl(
                menuController.shipConstructionSpeedAlienSlider,
                menuController.shipConstructionSpeedAlienTitle,
                menuController.shipConstructionSpeedAlienValue,
                "Alien Ship Construction",
                "shipSpeedAlien",
                menuController.shipConstructionSpeedAlienTooltip);
        }

        /// <summary>
        /// Helper to add a slider control with its title and value text labels.
        /// Note: We don't check activeInHierarchy here because sliders may be in a scroll view.
        ///
        /// IMPORTANT: Terra Invicta sliders use integer values (typically 0-40) where
        /// the actual multiplier is value * 0.05. So slider value 20 = 1.0x = 100%.
        /// We read the game's formatted valueText instead of calculating our own.
        /// </summary>
        private void AddSliderControl(Slider slider, TMP_Text titleText, TMP_Text valueText, string fallbackLabel, string action, TooltipTrigger tooltip = null)
        {
            if (slider == null)
                return;

            // Get localized label from title text, fall back to provided label
            string label = titleText != null && !string.IsNullOrEmpty(titleText.text)
                ? TISpeechMod.CleanText(titleText.text)
                : fallbackLabel;

            // Read the game's formatted value from valueText instead of calculating our own
            // The game formats these as percentages correctly (e.g., "100%", "50%")
            string currentValue = valueText != null && !string.IsNullOrEmpty(valueText.text)
                ? TISpeechMod.CleanText(valueText.text)
                : $"{(int)slider.value}";

            var control = new MenuControl
            {
                Type = MenuControlType.Slider,
                Label = label,
                CurrentValue = currentValue,
                GameObject = slider.gameObject,
                IsInteractable = slider.interactable,
                MinValue = slider.minValue,
                MaxValue = slider.maxValue,
                Action = action,
                // Store reference to valueText for dynamic refresh
                DetailText = $"{label}: {currentValue}"
            };

            // Store the valueText reference so we can refresh from it
            sliderValueTexts[action] = valueText;

            // Store tooltip reference if provided
            if (tooltip != null)
                controlTooltips[action] = tooltip;

            controls.Add(control);
        }

        // Store references to slider value texts for dynamic refresh
        private Dictionary<string, TMP_Text> sliderValueTexts = new Dictionary<string, TMP_Text>();

        // Store references to tooltips for controls (keyed by action)
        private Dictionary<string, TooltipTrigger> controlTooltips = new Dictionary<string, TooltipTrigger>();

        /// <summary>
        /// Refresh slider value from the game's UI text.
        /// Called when adjusting sliders to get the updated game-formatted value.
        /// </summary>
        private string GetSliderValueFromGame(string action)
        {
            if (sliderValueTexts.TryGetValue(action, out var valueText) && valueText != null)
            {
                return TISpeechMod.CleanText(valueText.text);
            }
            return null;
        }

        /// <summary>
        /// Get tooltip text for a control by its action ID.
        /// Terra Invicta tooltips use SetDelegate() with Loc.T() calls, so we need to
        /// call Loc.T() directly with the known localization keys rather than reading
        /// from the tooltip's TextField (which is empty until displayed).
        /// </summary>
        private string GetTooltipForControl(string action)
        {
            if (string.IsNullOrEmpty(action))
                return null;

            // Map action IDs to their localization keys
            // These are the keys used in StartMenuController.SetDelegate() calls
            string locKey = GetTooltipLocalizationKey(action);
            if (string.IsNullOrEmpty(locKey))
                return null;

            try
            {
                string tooltipText = Loc.T(locKey);
                // Only return if we got actual text (not the key itself)
                if (!string.IsNullOrEmpty(tooltipText) && tooltipText != locKey)
                {
                    return TISpeechMod.CleanText(tooltipText);
                }
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting tooltip for {action}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Map action IDs to their tooltip localization keys.
        /// These keys match what StartMenuController uses in SetDelegate() calls.
        /// </summary>
        private string GetTooltipLocalizationKey(string action)
        {
            switch (action)
            {
                // Sliders
                case "researchSpeed":
                    return "UI.StartScreen.CustomizeCampaign.ResearchSpeedTooltip";
                case "alienProgression":
                    return "UI.StartScreen.CustomizeCampaign.AlienProgressionRateTooltip";
                case "miningProductivity":
                    return "UI.StartScreen.CustomizeCampaign.MiningProductivityTooltip";
                case "nationalIP":
                    return "UI.StartScreen.CustomizeCampaign.NationalIPModifierTooltip";
                case "monthlyEvents":
                    return "UI.StartScreen.CustomizeCampaign.AverageMonthlyEventsTooltip";
                case "cpPlayer":
                    return "UI.StartScreen.CustomizeCampaign.CPFreebiesTooltip";
                case "cpAI":
                    return "UI.StartScreen.CustomizeCampaign.CPFreebiesAITooltip";
                case "mcPlayer":
                    return "UI.StartScreen.CustomizeCampaign.MCFreebiesTooltip";
                case "mcAI":
                    return "UI.StartScreen.CustomizeCampaign.MCFreebiesAITooltip";
                case "miningRatePlayer":
                    return "UI.StartScreen.CustomizeCampaign.MiningRatePlayerTooltip";
                case "miningRateAI":
                    return "UI.StartScreen.CustomizeCampaign.MiningRateHumanAITooltip";
                case "miningRateAlien":
                    return "UI.StartScreen.CustomizeCampaign.MiningRateAlienTooltip";
                case "habSpeedPlayer":
                    return "UI.StartScreen.CustomizeCampaign.HabConstructionSpeedPlayerTooltip";
                case "habSpeedAI":
                    return "UI.StartScreen.CustomizeCampaign.HabConstructionSpeedHumanAITooltip";
                case "habSpeedAlien":
                    return "UI.StartScreen.CustomizeCampaign.HabConstructionSpeedAlienTooltip";
                case "shipSpeedPlayer":
                    return "UI.StartScreen.CustomizeCampaign.ShipConstructionSpeedPlayerTooltip";
                case "shipSpeedAI":
                    return "UI.StartScreen.CustomizeCampaign.ShipConstructionSpeedHumanAITooltip";
                case "shipSpeedAlien":
                    return "UI.StartScreen.CustomizeCampaign.ShipConstructionSpeedAlienTooltip";

                // Toggles
                case "variableProjects":
                    return "UI.StartScreen.CustomizeCampaign.VariableProjectUnlocksTooltip";
                case "triggeredProjects":
                    return "UI.StartScreen.CustomizeCampaign.ShowTriggeredProjectsTooltip";
                case "homeNation":
                    return "UI.StartScreen.CustomizeCampaign.HomeNationCouncilorTip";
                case "realismScale":
                    return "UI.StartScreen.CustomizeCampaign.CinematicCombatScalingTooltip";
                case "realismDV":
                    return "UI.StartScreen.CustomizeCampaign.CinematicCombatMovementTooltip";
                case "alienFleet":
                    return "UI.StartScreen.CustomizeCampaign.AddAlienAssaultFleetTooltip";
                case "otherNations":
                    return "UI.StartScreen.CustomizeCampaign.OtherFactionsReceivesGroupTooltip";

                // Dropdowns
                case "nationGroup":
                    return "UI.StartScreen.CustomizeCampaign.CustomStartingNationGroupTooltip";

                // Buttons - these don't have standard tooltip keys in the game
                // case "startLong":
                // case "startAccelerated":

                default:
                    return null;
            }
        }

        /// <summary>
        /// Add toggle options (variable projects, realism, etc.).
        /// </summary>
        private void AddToggleOptions()
        {
            // Variable Project Unlocks
            AddToggleControl(
                menuController.variableProjectUnlocksToggle,
                menuController.variableProjectUnlocksText,
                "Variable Project Unlocks",
                "variableProjects",
                menuController.variableProjectUnlocksTooltip);

            // Show Triggered Projects
            AddToggleControl(
                menuController.showtriggeredProjectsToggle,
                menuController.showtriggeredProjectsText,
                "Show Triggered Projects",
                "triggeredProjects",
                menuController.showtriggeredProjectsTooltip);

            // First Councilor Home Nation
            AddToggleControl(
                menuController.firstCouncilorHomeNationToggle,
                menuController.firstCouncilorHomeNationText,
                "First Councilor From Home Nation",
                "homeNation",
                menuController.firstCouncilorHomeNationTooltip);

            // Realism Combat Scale
            AddToggleControl(
                menuController.realismCombatScaleToggle,
                menuController.realismCombatScaleText,
                "Realism Combat Scale",
                "realismScale",
                menuController.realismCombatScaleTooltip);

            // Realism Combat DV Movement
            AddToggleControl(
                menuController.realismCombatDVMovementToggle,
                menuController.realismCombatDVMovementText,
                "Realism Combat DV",
                "realismDV",
                menuController.realismCombatDVMovementTooltip);

            // Add Alien Assault Fleet
            AddToggleControl(
                menuController.addAlienAssaultFleetToggle,
                menuController.AddAlienAssaultFleetText,
                "Add Alien Assault Fleet",
                "alienFleet",
                menuController.addAlienAssaultFleetTooltip);

            // Other Faction Starting Nations
            AddToggleControl(
                menuController.otherFactionStartingNations,
                menuController.otherFactionStartingNationsText,
                "Other Factions Get Starting Nations",
                "otherNations",
                menuController.otherFactionStartingNationGroupTooltip);
        }

        /// <summary>
        /// Helper to add a toggle control with its label text.
        /// Note: We don't check activeInHierarchy here because toggles may be in a scroll view.
        /// </summary>
        private void AddToggleControl(Toggle toggle, TMP_Text labelText, string fallbackLabel, string action, TooltipTrigger tooltip = null)
        {
            if (toggle == null)
                return;

            // Get label from the associated text (localized)
            string label = labelText != null && !string.IsNullOrEmpty(labelText.text)
                ? TISpeechMod.CleanText(labelText.text)
                : fallbackLabel;

            var control = MenuControl.FromToggle(toggle, label);
            if (control != null)
            {
                control.Action = action;

                // Store tooltip reference if provided
                if (tooltip != null)
                    controlTooltips[action] = tooltip;

                controls.Add(control);
            }
        }

        /// <summary>
        /// Add additional dropdowns (councilor professions, ship naming, nation group).
        /// </summary>
        private void AddAdditionalDropdowns()
        {
            // Starting Councilor 1 Profession
            AddDropdownControl(
                menuController.startingCouncilor1Profession,
                menuController.startingCouncilor1ProfessionText,
                "Starting Councilor 1",
                "councilor1");

            // Starting Councilor 2 Profession
            AddDropdownControl(
                menuController.startingCouncilor2Profession,
                menuController.startingCouncilor2ProfessionText,
                "Starting Councilor 2",
                "councilor2");

            // Ship Naming Lists
            AddDropdownControl(
                menuController.smallShipNameListIdxDropdown,
                menuController.smallShipNameListIdxText,
                "Small Ship Names",
                "smallShipNames");

            AddDropdownControl(
                menuController.mediumShipNameListIdxDropdown,
                menuController.mediumShipNameListIdxText,
                "Medium Ship Names",
                "mediumShipNames");

            AddDropdownControl(
                menuController.largeShipNameListIdxDropdown,
                menuController.largeShipNameListIdxText,
                "Large Ship Names",
                "largeShipNames");

            // Hab Naming
            AddDropdownControl(
                menuController.habNameListIdxDropdown,
                menuController.habNameListIdxText,
                "Habitat Names",
                "habNames");

            // Custom Starting Nation Group
            AddDropdownControl(
                menuController.customStartingNationGroupDropdown,
                menuController.customStartingNationGroupText,
                "Starting Nation Group",
                "nationGroup",
                menuController.nationGroupTooltip);
        }

        /// <summary>
        /// Helper to add a dropdown control with its label text.
        /// Note: We don't check activeInHierarchy here because dropdowns may be in a scroll view.
        /// </summary>
        private void AddDropdownControl(TMP_Dropdown dropdown, TMP_Text labelText, string fallbackLabel, string action, TooltipTrigger tooltip = null)
        {
            if (dropdown == null)
                return;

            // Get label from the associated text (localized)
            string label = labelText != null && !string.IsNullOrEmpty(labelText.text)
                ? TISpeechMod.CleanText(labelText.text)
                : fallbackLabel;

            var control = MenuControl.FromDropdown(dropdown, label);
            if (control != null)
            {
                control.Action = action;

                // Store tooltip reference if provided
                if (tooltip != null)
                    controlTooltips[action] = tooltip;

                controls.Add(control);
            }
        }

        /// <summary>
        /// Add faction customization input fields (display name, adjective, etc.).
        /// </summary>
        private void AddFactionCustomizationInputs()
        {
            // Check if faction customization panel is visible
            if (menuController.factionCustomizationObject == null ||
                !menuController.factionCustomizationObject.activeInHierarchy)
                return;

            controls.Add(CreateDivider("--- Faction Customization ---"));

            // Custom Display Name
            AddInputFieldControl(
                menuController.customDisplayNameInput,
                "Faction Name",
                "customName");

            // Custom Adjective
            AddInputFieldControl(
                menuController.customAdjectiveInput,
                "Faction Adjective",
                "customAdjective");

            // Custom Leader Address
            AddInputFieldControl(
                menuController.customLeaderAddressInput,
                "Leader Address",
                "leaderAddress");

            // Custom Fleet Name
            AddInputFieldControl(
                menuController.customFleetInput,
                "Fleet Name Base",
                "fleetName");
        }

        /// <summary>
        /// Helper to add an input field control.
        /// </summary>
        private void AddInputFieldControl(TMP_InputField inputField, string label, string action)
        {
            if (inputField == null || !inputField.gameObject.activeInHierarchy)
                return;

            var control = new MenuControl
            {
                Type = MenuControlType.InputField,
                Label = label,
                CurrentValue = inputField.text ?? "",
                GameObject = inputField.gameObject,
                IsInteractable = inputField.interactable,
                Action = action
            };
            controls.Add(control);
        }

        /// <summary>
        /// Add faction participation toggles (which factions are in the game).
        /// </summary>
        private void AddFactionParticipationToggles()
        {
            if (menuController.factionToggleListManager == null)
                return;

            var toggleItems = menuController.factionToggleListManager.GetComponentsInChildren<FactionToggleListItemController>(includeInactive: false);
            if (toggleItems == null || toggleItems.Length == 0)
                return;

            controls.Add(CreateDivider("--- Participating Factions ---"));

            foreach (var toggleItem in toggleItems)
            {
                if (toggleItem.factionToggle == null)
                    continue;

                factionToggles.Add(toggleItem);

                string factionName = toggleItem.factionNameText != null
                    ? TISpeechMod.CleanText(toggleItem.factionNameText.text)
                    : (toggleItem.faction?.capitalizedFactionNameCurrent ?? "Unknown");

                bool isOn = toggleItem.factionToggle.isOn;
                bool canToggle = toggleItem.factionToggle.interactable;

                var toggleControl = new MenuControl
                {
                    Type = MenuControlType.Toggle,
                    Label = factionName,
                    CurrentValue = isOn ? "Enabled" : "Disabled",
                    GameObject = toggleItem.gameObject,
                    IsInteractable = canToggle,
                    Action = "factionToggle_" + (toggleItem.faction?.dataName ?? factionName)
                };

                // Add detail explaining why it can't be toggled if applicable
                if (!canToggle)
                {
                    if (toggleItem.faction?.isAlien == true)
                        toggleControl.DetailText = $"{factionName}: Always included (aliens)";
                    else
                        toggleControl.DetailText = $"{factionName}: Your faction (always included)";
                }
                else
                {
                    toggleControl.DetailText = $"{factionName}: {(isOn ? "Will participate" : "Will not participate")} in this game";
                }

                controls.Add(toggleControl);
            }
        }

        /// <summary>
        /// Add start campaign buttons and previous settings button.
        /// </summary>
        private void AddStartButtons()
        {
            controls.Add(CreateDivider("--- Start Game ---"));

            // Previous campaign settings button (if available)
            if (menuController.previousCampaignSettingsButton != null &&
                menuController.previousCampaignSettingsButton.gameObject.activeInHierarchy)
            {
                // Get localized text from button label
                string label = menuController.campaignCustomizationPreviousCampaignText != null
                    ? TISpeechMod.CleanText(menuController.campaignCustomizationPreviousCampaignText.text)
                    : "Use Previous Settings";

                var prevControl = MenuControl.FromButton(menuController.previousCampaignSettingsButton, label);
                if (prevControl != null)
                {
                    prevControl.Action = "previousSettings";
                    controls.Add(prevControl);
                }
            }

            // Start Long Campaign button
            if (menuController.startLongCampaignButton != null &&
                menuController.startLongCampaignButton.gameObject.activeInHierarchy)
            {
                string label = menuController.newGameStartButtonText != null
                    ? TISpeechMod.CleanText(menuController.newGameStartButtonText.text)
                    : "Start Campaign";

                var startControl = MenuControl.FromButton(menuController.startLongCampaignButton, label);
                if (startControl != null)
                {
                    startControl.Action = "startLong";
                    // Store tooltip for long campaign button
                    if (menuController.longCampaignTooltip != null)
                        controlTooltips["startLong"] = menuController.longCampaignTooltip;
                    controls.Add(startControl);
                }
            }

            // Start Accelerated Campaign button
            if (menuController.startAcceleratedCampaignButton != null &&
                menuController.startAcceleratedCampaignButton.gameObject.activeInHierarchy)
            {
                string label = menuController.StartAcceleratedCampaignButtonText != null
                    ? TISpeechMod.CleanText(menuController.StartAcceleratedCampaignButtonText.text)
                    : "Start Accelerated Campaign";

                var accelControl = MenuControl.FromButton(menuController.startAcceleratedCampaignButton, label);
                if (accelControl != null)
                {
                    accelControl.Action = "startAccelerated";
                    // Store tooltip for accelerated campaign button
                    if (menuController.acceleratedCampaignTooltip != null)
                        controlTooltips["startAccelerated"] = menuController.acceleratedCampaignTooltip;
                    controls.Add(accelControl);
                }
            }
        }

        /// <summary>
        /// Create a divider/header control.
        /// </summary>
        private MenuControl CreateDivider(string label)
        {
            return new MenuControl
            {
                Type = MenuControlType.Button,
                Label = label,
                IsInteractable = false
            };
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            if (!control.IsInteractable)
            {
                if (control.Label.StartsWith("---"))
                    return; // Divider, do nothing
                TISpeechMod.Speak($"{control.Label} is not available", interrupt: true);
                return;
            }

            // For dropdowns, activate cycles the value
            if (control.Type == MenuControlType.Dropdown)
            {
                control.Activate();
                control.RefreshValue();

                // For faction dropdown (use Action for localization-safe check), also read the description
                if (control.Action == ACTION_YOUR_FACTION && menuController?.selectedFactionDescription != null)
                {
                    string desc = GetFactionDescriptionWithDifficulty();
                    TISpeechMod.Speak($"{control.Label}: {control.CurrentValue}. {desc}", interrupt: true);
                }
                // For scenario dropdowns, notify the controller to update game state
                else if (control.Action == ACTION_SCENARIO ||
                         control.Action == ACTION_FACTION_COUNCILS ||
                         control.Action == ACTION_SOLAR_SYSTEM)
                {
                    // Find and notify the NewGameOptionController
                    NotifyScenarioDropdownChanged(control);
                    TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                }
                return;
            }

            // For faction participation toggles, find the corresponding controller
            if (control.Type == MenuControlType.Toggle && control.GameObject != null)
            {
                // Check if this is a faction participation toggle (using Action prefix)
                if (control.Action?.StartsWith("factionToggle_") == true)
                {
                    var factionToggleController = control.GameObject.GetComponent<FactionToggleListItemController>();
                    if (factionToggleController != null && factionToggleController.factionToggle != null)
                    {
                        // Toggle the faction
                        factionToggleController.factionToggle.isOn = !factionToggleController.factionToggle.isOn;
                        factionToggleController.OnUpdateToggle(); // Notify controller to validate requirements

                        bool isNowOn = factionToggleController.factionToggle.isOn;
                        string factionName = control.Label;
                        TISpeechMod.Speak($"{factionName}: {(isNowOn ? "Enabled" : "Disabled")}", interrupt: true);

                        // Update the control's current value
                        control.CurrentValue = isNowOn ? "Enabled" : "Disabled";
                        return;
                    }
                }

                // Regular toggle (Tutorial, Variable Projects, Realism options, etc.)
                control.Activate();
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For sliders, announce the current value
            if (control.Type == MenuControlType.Slider)
            {
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For input fields, announce the current value
            if (control.Type == MenuControlType.InputField)
            {
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For buttons, announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"NewGameScreen: Activated '{control.Label}' (Action: {control.Action ?? "none"})");
        }

        /// <summary>
        /// Notify the NewGameOptionController that a scenario dropdown value changed.
        /// This ensures the game properly updates its state.
        /// </summary>
        private void NotifyScenarioDropdownChanged(MenuControl control)
        {
            if (menuController?.newCampaignOptionList == null || control.GameObject == null)
                return;

            // Find the dropdown and its associated NewGameOptionController
            var dropdown = control.GameObject.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
                return;

            // Find parent NewGameOptionController
            var optionController = dropdown.GetComponentInParent<NewGameOptionController>();
            if (optionController != null)
            {
                // Call the selection handler to update game state
                optionController.OnDropdownOptionSelected();
            }
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For faction dropdown (use Action for localization-safe check), read the description dynamically
            if (control.Action == ACTION_YOUR_FACTION && menuController?.selectedFactionDescription != null)
            {
                // Refresh the dropdown value first
                control.RefreshValue();
                string desc = GetFactionDescriptionWithDifficulty();
                return $"{control.Label}: {control.CurrentValue}. {desc}";
            }

            // For scenario dropdowns, provide additional context
            if (control.Action == ACTION_SCENARIO ||
                control.Action == ACTION_FACTION_COUNCILS ||
                control.Action == ACTION_SOLAR_SYSTEM)
            {
                control.RefreshValue();
                // Try to get description from the selected meta template
                return GetScenarioDropdownDetail(control);
            }

            // For controls with tooltips, include tooltip text in detail
            string tooltipText = GetTooltipForControl(control.Action);
            if (!string.IsNullOrEmpty(tooltipText))
            {
                // For sliders, refresh value first
                if (control.Type == MenuControlType.Slider)
                {
                    string gameValue = GetSliderValueFromGame(control.Action);
                    if (!string.IsNullOrEmpty(gameValue))
                        control.CurrentValue = gameValue;
                }

                return $"{control.Label}: {control.CurrentValue}. {tooltipText}";
            }

            return control.GetDetail();
        }

        /// <summary>
        /// Get faction description with difficulty as a number instead of star sprites.
        /// The game uses inline sprite tags for stars which get stripped by CleanText.
        /// We count the star sprite occurrences to determine difficulty.
        /// </summary>
        private string GetFactionDescriptionWithDifficulty()
        {
            try
            {
                string rawText = menuController.selectedFactionDescription.text;
                if (string.IsNullOrEmpty(rawText))
                    return "";

                // Count star sprites in the raw text
                // The pattern is: <sprite name=star> (possibly with color tags around it)
                int starCount = 0;
                int searchIndex = 0;
                while ((searchIndex = rawText.IndexOf("sprite name=star", searchIndex, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    starCount++;
                    searchIndex++;
                }

                // Clean the text (removes sprite and color tags)
                string cleanedText = TISpeechMod.CleanText(rawText);

                // If we found stars, append the count
                if (starCount > 0)
                {
                    string difficultyText = starCount == 1 ? "1 star" : $"{starCount} stars";
                    // The cleaned text likely ends with "Faction Difficulty:" - append the count
                    return $"{cleanedText} {difficultyText}";
                }

                return cleanedText;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting faction description: {ex.Message}");
                return TISpeechMod.CleanText(menuController.selectedFactionDescription.text);
            }
        }

        /// <summary>
        /// Get detailed information about a scenario dropdown selection.
        /// </summary>
        private string GetScenarioDropdownDetail(MenuControl control)
        {
            if (menuController?.newCampaignOptionList == null || control.GameObject == null)
                return control.GetDetail();

            var dropdown = control.GameObject.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
                return control.GetDetail();

            var optionController = dropdown.GetComponentInParent<NewGameOptionController>();
            if (optionController == null || optionController.templateOptions == null)
                return control.GetDetail();

            int selectedIndex = dropdown.value;
            if (selectedIndex >= 0 && selectedIndex < optionController.templateOptions.Count)
            {
                var template = optionController.templateOptions[selectedIndex];
                // Get the description using the localization system
                // Pattern: TIMetaTemplate.description.{dataName}
                string locKey = $"TIMetaTemplate.description.{template.dataName}";
                string desc = Loc.T(locKey);
                // Only use description if it's not the same as the key (i.e., localization exists)
                if (!string.IsNullOrEmpty(desc) && desc != locKey)
                {
                    return $"{control.Label}: {control.CurrentValue}. {TISpeechMod.CleanText(desc)}";
                }
            }

            return $"{control.Label}: {control.CurrentValue}";
        }

        public override string GetActivationAnnouncement()
        {
            // Get current faction name if available
            string factionInfo = "";
            if (menuController?.newCampaignChooseFactionDropdown != null)
            {
                var dropdown = menuController.newCampaignChooseFactionDropdown;
                if (dropdown.options.Count > 0 && dropdown.value < dropdown.options.Count)
                {
                    factionInfo = $" Current faction: {dropdown.options[dropdown.value].text}.";
                }
            }

            return $"{Name}.{factionInfo} {controls.Count} options.";
        }

        /// <summary>
        /// Override AdjustControl to properly read game-formatted values for sliders.
        /// Terra Invicta sliders use integer values where multiplier = value * 0.05,
        /// so we need to read from the game's UI text, not calculate our own.
        /// </summary>
        public override void AdjustControl(int index, bool increment)
        {
            var controlsList = GetControls();
            if (controlsList == null || index < 0 || index >= controlsList.Count)
                return;

            var control = controlsList[index];
            if (!control.IsInteractable)
            {
                TISpeechMod.Speak($"{control.Label} is disabled", interrupt: true);
                return;
            }

            if (control.Type == MenuControlType.Button)
            {
                // Buttons don't have adjustable values
                return;
            }

            // For sliders, adjust and then read the game's formatted value
            if (control.Type == MenuControlType.Slider && control.GameObject != null)
            {
                var slider = control.GameObject.GetComponent<Slider>();
                if (slider != null)
                {
                    // Calculate step size (typically 1 for integer sliders)
                    float step = 1f;
                    float delta = increment ? step : -step;
                    float newValue = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
                    slider.value = newValue;
                    // Invoke the callback so the game updates its UI
                    slider.onValueChanged?.Invoke(newValue);

                    // Read the game's formatted value from the UI text
                    string gameValue = GetSliderValueFromGame(control.Action);
                    if (!string.IsNullOrEmpty(gameValue))
                    {
                        control.CurrentValue = gameValue;
                        control.DetailText = $"{control.Label}: {gameValue}";
                    }

                    TISpeechMod.Speak($"{control.Label}: {control.CurrentValue}", interrupt: true);
                    return;
                }
            }

            // For other controls, use base behavior
            control.Adjust(increment);
            control.RefreshValue();
            TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
        }
    }
}
