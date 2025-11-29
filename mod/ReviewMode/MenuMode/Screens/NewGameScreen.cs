using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the New Game / Campaign Setup menu.
    /// Provides navigation through faction selection, difficulty, tutorial toggle, and start buttons.
    /// Also includes faction participation toggles for customizing which factions are in the game.
    /// </summary>
    public class NewGameScreen : MenuScreenBase
    {
        public override string Name => "New Game";

        private List<MenuControl> controls = new List<MenuControl>();
        private StartMenuController menuController;
        private List<FactionToggleListItemController> factionToggles = new List<FactionToggleListItemController>();

        public override List<MenuControl> GetControls()
        {
            return controls;
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

            try
            {
                menuController = UnityEngine.Object.FindObjectOfType<StartMenuController>();
                if (menuController == null)
                {
                    MelonLogger.Msg("NewGameScreen: StartMenuController not found");
                    return;
                }

                // Faction dropdown (your faction)
                if (menuController.newCampaignChooseFactionDropdown != null &&
                    menuController.newCampaignChooseFactionDropdown.gameObject.activeInHierarchy)
                {
                    var factionControl = MenuControl.FromDropdown(
                        menuController.newCampaignChooseFactionDropdown,
                        "Your Faction");
                    if (factionControl != null)
                    {
                        // Add faction description as detail
                        if (menuController.selectedFactionDescription != null)
                        {
                            factionControl.DetailText = TISpeechMod.CleanText(
                                menuController.selectedFactionDescription.text);
                        }
                        controls.Add(factionControl);
                    }
                }

                // Difficulty dropdown
                if (menuController.selectDifficultyDropdown != null &&
                    menuController.selectDifficultyDropdown.gameObject.activeInHierarchy)
                {
                    var difficultyControl = MenuControl.FromDropdown(
                        menuController.selectDifficultyDropdown,
                        "Difficulty");
                    if (difficultyControl != null)
                        controls.Add(difficultyControl);
                }

                // Tutorial toggle
                if (menuController.tutorialToggle != null &&
                    menuController.tutorialToggle.gameObject.activeInHierarchy)
                {
                    var tutorialControl = MenuControl.FromToggle(
                        menuController.tutorialToggle,
                        "Tutorial");
                    if (tutorialControl != null)
                        controls.Add(tutorialControl);
                }

                // Previous campaign settings button (if available)
                if (menuController.previousCampaignSettingsButton != null &&
                    menuController.previousCampaignSettingsButton.gameObject.activeInHierarchy)
                {
                    var prevControl = MenuControl.FromButton(
                        menuController.previousCampaignSettingsButton,
                        "Use Previous Settings");
                    if (prevControl != null)
                        controls.Add(prevControl);
                }

                // Faction participation toggles (which factions are in the game)
                if (menuController.factionToggleListManager != null)
                {
                    var toggleItems = menuController.factionToggleListManager.GetComponentsInChildren<FactionToggleListItemController>(includeInactive: false);
                    if (toggleItems != null && toggleItems.Length > 0)
                    {
                        // Add divider before faction toggles
                        controls.Add(new MenuControl
                        {
                            Type = MenuControlType.Button,
                            Label = "--- Participating Factions ---",
                            IsInteractable = false
                        });

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
                                IsInteractable = canToggle
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
                }

                // Add divider before start buttons
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Start Game ---",
                    IsInteractable = false
                });

                // Start Long Campaign button
                if (menuController.startLongCampaignButton != null &&
                    menuController.startLongCampaignButton.gameObject.activeInHierarchy)
                {
                    var startControl = MenuControl.FromButton(
                        menuController.startLongCampaignButton,
                        "Start Campaign");
                    if (startControl != null)
                        controls.Add(startControl);
                }

                // Start Accelerated Campaign button
                if (menuController.startAcceleratedCampaignButton != null &&
                    menuController.startAcceleratedCampaignButton.gameObject.activeInHierarchy)
                {
                    var accelControl = MenuControl.FromButton(
                        menuController.startAcceleratedCampaignButton,
                        "Start Accelerated Campaign");
                    if (accelControl != null)
                        controls.Add(accelControl);
                }

                MelonLogger.Msg($"NewGameScreen: Found {controls.Count} controls, {factionToggles.Count} faction toggles");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing NewGameScreen: {ex.Message}");
            }
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

                // For faction dropdown, also read the description
                if (control.Label == "Your Faction" && menuController?.selectedFactionDescription != null)
                {
                    string desc = TISpeechMod.CleanText(menuController.selectedFactionDescription.text);
                    TISpeechMod.Speak($"{control.Label}: {control.CurrentValue}. {desc}", interrupt: true);
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

                // Regular toggle (like Tutorial)
                control.Activate();
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For buttons, announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"NewGameScreen: Activated '{control.Label}'");
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For faction dropdown, read the current description dynamically
            if (control.Label == "Your Faction" && menuController?.selectedFactionDescription != null)
            {
                // Refresh the dropdown value first
                control.RefreshValue();
                string desc = TISpeechMod.CleanText(menuController.selectedFactionDescription.text);
                return $"{control.Label}: {control.CurrentValue}. {desc}";
            }

            return control.GetDetail();
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
    }
}
