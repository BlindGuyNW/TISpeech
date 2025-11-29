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
    /// Menu screen for the Options/Settings menu.
    /// Provides navigation through language, audio, gameplay toggles, sliders, etc.
    /// </summary>
    public class OptionsScreen : MenuScreenBase
    {
        public override string Name => "Options";

        private List<MenuControl> controls = new List<MenuControl>();
        private OptionsMenuController optionsController;
        private AudioMenuController audioController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Options menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var controller = UnityEngine.Object.FindObjectOfType<OptionsMenuController>();
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
                optionsController = UnityEngine.Object.FindObjectOfType<OptionsMenuController>();
                audioController = UnityEngine.Object.FindObjectOfType<AudioMenuController>();

                if (optionsController == null)
                {
                    MelonLogger.Msg("OptionsScreen: OptionsMenuController not found");
                    return;
                }

                // Language dropdown
                if (optionsController.languageSelection != null &&
                    optionsController.languageSelection.gameObject.activeInHierarchy)
                {
                    var langControl = MenuControl.FromDropdown(
                        optionsController.languageSelection,
                        "Language");
                    if (langControl != null)
                        controls.Add(langControl);
                }

                // Audio section (from AudioMenuController)
                if (audioController != null)
                {
                    controls.Add(new MenuControl
                    {
                        Type = MenuControlType.Button,
                        Label = "--- Audio ---",
                        IsInteractable = false
                    });

                    // Volume sliders
                    AddSlider(audioController.volumeMasterSlider, "Master Volume");
                    AddSlider(audioController.volumeMusicSlider, "Music Volume");
                    AddSlider(audioController.volumeEffectsSlider, "Effects Volume");
                    AddSlider(audioController.volumeUISlider, "UI Volume");
                    AddSlider(audioController.volumeVoiceSlider, "Voice Volume");
                    AddSlider(audioController.volumeAmbienceSlider, "Ambience Volume");

                    // Mute in background toggle
                    AddToggle(audioController.toggleMuteInBackgroundToggle, "Mute When In Background");
                }

                // Add gameplay section header
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Gameplay ---",
                    IsInteractable = false
                });

                // Gameplay toggles
                AddToggle(optionsController.missionPhaseSummaryStartsOpen, "Mission Phase Summary Starts Open");
                AddToggle(optionsController.unpauseAfterMissionAssignment, "Unpause After Mission Assignment");
                AddToggle(optionsController.assignmentPhaseCouncilorCameraFocusToggle, "Camera Focus on Councilor");
                AddToggle(optionsController.cycleNextCouncilorWhenAssigningMissionsToggle, "Cycle to Next Councilor");
                AddToggle(optionsController.alertSpaceTimerToggle, "Alert Space Timer");
                AddToggle(optionsController.monthlyIncomeToggle, "Show Monthly Incomes");

                // Add controls section header
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Controls ---",
                    IsInteractable = false
                });

                // Waypoint snap dropdown
                if (optionsController.waypointAngleSnapDropdown != null &&
                    optionsController.waypointAngleSnapDropdown.gameObject.activeInHierarchy)
                {
                    var snapControl = MenuControl.FromDropdown(
                        optionsController.waypointAngleSnapDropdown,
                        "Waypoint Angle Snap");
                    if (snapControl != null)
                        controls.Add(snapControl);
                }

                AddToggle(optionsController.customCursorToggle, "Use Default Cursor");

                // Add display section header
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Display ---",
                    IsInteractable = false
                });

                AddToggle(optionsController.displaySystemClockToggle, "Display System Clock");
                AddToggle(optionsController.showHighSpeedOrbitTrailsToggle, "Show High Speed Orbit Trails");
                AddToggle(optionsController.showEarthLightsToggle, "Show Earth Lights");

                // Tooltip delay sliders
                AddSlider(optionsController.tooltipDelayPrimarySlider, "Tooltip Delay (Primary)");
                AddSlider(optionsController.tooltipDelaySupplementalSlider, "Tooltip Delay (Supplemental)");

                // Add advanced section header
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Advanced ---",
                    IsInteractable = false
                });

                // Max ships slider
                AddSlider(optionsController.maxShipsInCombatSlider, "Max Ships in Combat");

                AddToggle(optionsController.compressSavesToggle, "Compress Saves");

                // Reset tutorial button (only shown during tutorial)
                if (optionsController.resetTutorialButton != null &&
                    optionsController.resetTutorialButton.activeInHierarchy)
                {
                    var resetButton = optionsController.resetTutorialButton.GetComponent<Button>();
                    if (resetButton != null)
                    {
                        var resetControl = MenuControl.FromButton(resetButton, "Reset Tutorial");
                        if (resetControl != null)
                            controls.Add(resetControl);
                    }
                }

                MelonLogger.Msg($"OptionsScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing OptionsScreen: {ex.Message}");
            }
        }

        private void AddToggle(Toggle toggle, string label)
        {
            if (toggle != null && toggle.gameObject.activeInHierarchy)
            {
                var control = MenuControl.FromToggle(toggle, label);
                if (control != null)
                    controls.Add(control);
            }
        }

        private void AddSlider(Slider slider, string label)
        {
            if (slider != null && slider.gameObject.activeInHierarchy)
            {
                var control = MenuControl.FromSlider(slider, label);
                if (control != null)
                {
                    controls.Add(control);
                }
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

            // For toggles, sliders, and dropdowns, activate cycles/adjusts the value
            if (control.Type == MenuControlType.Toggle ||
                control.Type == MenuControlType.Slider ||
                control.Type == MenuControlType.Dropdown)
            {
                control.Activate();
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);

                // Save settings after each change
                TIPlayerProfileManager.SavePlayerConfig();
                return;
            }

            // For buttons, announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"OptionsScreen: Activated '{control.Label}'");
        }

        public override string GetActivationAnnouncement()
        {
            return $"{Name}. {controls.Count} settings.";
        }

        public override void AdjustControl(int index, bool increment)
        {
            base.AdjustControl(index, increment);

            // Save settings after adjustment
            TIPlayerProfileManager.SavePlayerConfig();
        }

        public override void OnDeactivate()
        {
            // Save settings when leaving the Options screen
            try
            {
                TIPlayerProfileManager.SavePlayerConfig();
                MelonLogger.Msg("OptionsScreen: Saved player settings");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"OptionsScreen: Failed to save settings: {ex.Message}");
            }
        }
    }
}
