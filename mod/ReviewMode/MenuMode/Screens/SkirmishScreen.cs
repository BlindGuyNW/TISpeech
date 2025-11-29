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
    /// Menu screen for the Skirmish Mode setup menu.
    /// Provides navigation through location, faction, ship selection, and combat options.
    /// </summary>
    public class SkirmishScreen : MenuScreenBase
    {
        public override string Name => "Skirmish";

        private List<MenuControl> controls = new List<MenuControl>();
        private StartMenuController startController;
        private SkirmishMenuController skirmishController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Skirmish menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var controller = UnityEngine.Object.FindObjectOfType<SkirmishMenuController>();
            if (controller == null)
                return false;

            // Use the Menu.IsOpen property - this is the definitive check
            if (controller.menu != null && controller.menu.IsOpen)
                return true;

            return false;
        }

        public override void Refresh()
        {
            controls.Clear();

            try
            {
                startController = UnityEngine.Object.FindObjectOfType<StartMenuController>();
                skirmishController = UnityEngine.Object.FindObjectOfType<SkirmishMenuController>();

                if (startController == null || skirmishController == null)
                {
                    MelonLogger.Msg("SkirmishScreen: Controllers not found");
                    return;
                }

                // Location dropdown
                if (startController.skirmishLocationSettingDropdown != null &&
                    startController.skirmishLocationSettingDropdown.gameObject.activeInHierarchy)
                {
                    var locControl = MenuControl.FromDropdown(
                        startController.skirmishLocationSettingDropdown,
                        "Location");
                    if (locControl != null)
                        controls.Add(locControl);
                }

                // Hab dropdown
                if (startController.skirmishHabDropdown != null &&
                    startController.skirmishHabDropdown.gameObject.activeInHierarchy)
                {
                    var habControl = MenuControl.FromDropdown(
                        startController.skirmishHabDropdown,
                        "Hab");
                    if (habControl != null)
                        controls.Add(habControl);
                }

                // Player 1 section
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Player 1 ---",
                    IsInteractable = false
                });

                // Player 1 faction dropdown
                if (startController.skirmishFactionDropdown != null &&
                    startController.skirmishFactionDropdown.Length > 0 &&
                    startController.skirmishFactionDropdown[0] != null &&
                    startController.skirmishFactionDropdown[0].gameObject.activeInHierarchy)
                {
                    var p1FactionControl = MenuControl.FromDropdown(
                        startController.skirmishFactionDropdown[0],
                        "Player 1 Faction");
                    if (p1FactionControl != null)
                    {
                        // Add fleet score to detail
                        if (startController.skirmishModePlayer1FleetScore != null)
                        {
                            p1FactionControl.DetailText = $"Fleet Score: {startController.skirmishModePlayer1FleetScore.text}";
                        }
                        controls.Add(p1FactionControl);
                    }
                }

                // Player 2 section
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Player 2 ---",
                    IsInteractable = false
                });

                // Player 2 faction dropdown
                if (startController.skirmishFactionDropdown != null &&
                    startController.skirmishFactionDropdown.Length > 1 &&
                    startController.skirmishFactionDropdown[1] != null &&
                    startController.skirmishFactionDropdown[1].gameObject.activeInHierarchy)
                {
                    var p2FactionControl = MenuControl.FromDropdown(
                        startController.skirmishFactionDropdown[1],
                        "Player 2 Faction");
                    if (p2FactionControl != null)
                    {
                        // Add fleet score to detail
                        if (startController.skirmishModePlayer2FleetScore != null)
                        {
                            p2FactionControl.DetailText = $"Fleet Score: {startController.skirmishModePlayer2FleetScore.text}";
                        }
                        controls.Add(p2FactionControl);
                    }
                }

                // Combat options section
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Options ---",
                    IsInteractable = false
                });

                // Tutorial toggle
                if (startController.skirmishTutorialToggle != null &&
                    startController.skirmishTutorialToggle.gameObject.activeInHierarchy)
                {
                    var tutorialControl = MenuControl.FromToggle(
                        startController.skirmishTutorialToggle,
                        "Combat Tutorial");
                    if (tutorialControl != null)
                        controls.Add(tutorialControl);
                }

                // Realism toggles
                if (startController.skirmishRealismCombatScaleToggle != null &&
                    startController.skirmishRealismCombatScaleToggle.gameObject.activeInHierarchy)
                {
                    var scaleControl = MenuControl.FromToggle(
                        startController.skirmishRealismCombatScaleToggle,
                        "Cinematic Combat Scaling");
                    if (scaleControl != null)
                        controls.Add(scaleControl);
                }

                if (startController.skirmishRealismCombatDVMovementToggle != null &&
                    startController.skirmishRealismCombatDVMovementToggle.gameObject.activeInHierarchy)
                {
                    var dvControl = MenuControl.FromToggle(
                        startController.skirmishRealismCombatDVMovementToggle,
                        "Cinematic Combat Movement");
                    if (dvControl != null)
                        controls.Add(dvControl);
                }

                // Start button
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Start ---",
                    IsInteractable = false
                });

                // Find the Begin button by searching for button with "Begin" text
                var buttons = skirmishController.GetComponentsInChildren<Button>(includeInactive: false);
                foreach (var button in buttons)
                {
                    var tmpText = button.GetComponentInChildren<TMP_Text>();
                    if (tmpText != null)
                    {
                        string text = TISpeechMod.CleanText(tmpText.text).ToLower();
                        if (text.Contains("begin") || text.Contains("start"))
                        {
                            var beginControl = MenuControl.FromButton(button, "Begin Skirmish");
                            if (beginControl != null)
                            {
                                beginControl.DetailText = "Start the skirmish battle";
                                controls.Add(beginControl);
                                break;
                            }
                        }
                    }
                }

                MelonLogger.Msg($"SkirmishScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing SkirmishScreen: {ex.Message}");
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

            // For toggles and dropdowns, activate cycles the value
            if (control.Type == MenuControlType.Toggle || control.Type == MenuControlType.Dropdown)
            {
                control.Activate();
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For buttons, announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"SkirmishScreen: Activated '{control.Label}'");
        }

        public override string GetActivationAnnouncement()
        {
            // Get current location if available
            string locationInfo = "";
            if (startController?.skirmishLocationSettingDropdown != null)
            {
                var dropdown = startController.skirmishLocationSettingDropdown;
                if (dropdown.options.Count > 0 && dropdown.value < dropdown.options.Count)
                {
                    locationInfo = $" at {TISpeechMod.CleanText(dropdown.options[dropdown.value].text)}";
                }
            }

            return $"{Name}{locationInfo}. {controls.Count} options.";
        }
    }
}
