using System;
using System.Collections.Generic;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.MenuMode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TISpeech.ReviewMode.EscapeMenu.Screens
{
    /// <summary>
    /// Escape menu screen for game settings.
    /// Presents all settings in a flat list with section dividers, similar to main menu.
    /// </summary>
    public class InGameSettingsScreen : EscapeMenuScreenBase
    {
        public override string Name => "Settings";

        private List<MenuControl> controls = new List<MenuControl>();
        private OptionsMenuController optionsController;
        private AudioMenuController audioController;
        private GraphicsMenuController graphicsController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Settings menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
            if (optionsScreen == null)
                return false;

            if (optionsScreen.settingsMenuObject == null)
                return false;

            var menu = optionsScreen.settingsMenuObject.GetComponent<Menu>();
            return menu != null && menu.IsOpen;
        }

        public override void Refresh()
        {
            controls.Clear();

            try
            {
                // Get settings controllers
                var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsScreen?.optionsMenuController == null)
                {
                    MelonLogger.Msg("InGameSettingsScreen: optionsMenuController not found");
                    return;
                }

                optionsController = optionsScreen.optionsMenuController;
                audioController = UnityEngine.Object.FindObjectOfType<AudioMenuController>();
                graphicsController = UnityEngine.Object.FindObjectOfType<GraphicsMenuController>();

                // Build all settings in a flat list with section dividers
                BuildGameplayControls();
                BuildAudioControls();
                BuildGraphicsControls();

                MelonLogger.Msg($"InGameSettingsScreen: Found {controls.Count} total controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing InGameSettingsScreen: {ex.Message}");
            }
        }

        private void BuildGameplayControls()
        {
            if (optionsController == null)
                return;

            // Section header
            controls.Add(new MenuControl
            {
                Type = MenuControlType.Button,
                Label = "--- Gameplay ---",
                IsInteractable = false
            });

            // Language dropdown
            AddDropdown(optionsController.languageSelection, "Language");

            // Gameplay toggles
            AddToggle(optionsController.missionPhaseSummaryStartsOpen, "Mission Phase Summary Starts Open");
            AddToggle(optionsController.unpauseAfterMissionAssignment, "Unpause After Mission Assignment");
            AddToggle(optionsController.assignmentPhaseCouncilorCameraFocusToggle, "Camera Focus on Councilor");
            AddToggle(optionsController.cycleNextCouncilorWhenAssigningMissionsToggle, "Cycle to Next Councilor");
            AddToggle(optionsController.alertSpaceTimerToggle, "Alert Space Timer");
            AddToggle(optionsController.monthlyIncomeToggle, "Show Monthly Incomes");
            AddToggle(optionsController.displaySystemClockToggle, "Display System Clock");
            AddToggle(optionsController.showHighSpeedOrbitTrailsToggle, "Show High Speed Orbit Trails");
            AddToggle(optionsController.showEarthLightsToggle, "Show Earth Lights");
            AddToggle(optionsController.compressSavesToggle, "Compress Saves");

            // Sliders
            AddSlider(optionsController.maxShipsInCombatSlider, "Max Ships in Combat");
            AddSlider(optionsController.tooltipDelayPrimarySlider, "Tooltip Delay (Primary)");
            AddSlider(optionsController.tooltipDelaySupplementalSlider, "Tooltip Delay (Supplemental)");

            // Waypoint snap dropdown
            AddDropdown(optionsController.waypointAngleSnapDropdown, "Waypoint Angle Snap");

            // Cursor toggle
            AddToggle(optionsController.customCursorToggle, "Use Default Cursor");
        }

        private void BuildAudioControls()
        {
            if (audioController == null)
            {
                MelonLogger.Msg("InGameSettingsScreen: AudioMenuController not found");
                return;
            }

            // Section header
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

            // Mute toggle
            AddToggle(audioController.toggleMuteInBackgroundToggle, "Mute When In Background");
        }

        private void BuildGraphicsControls()
        {
            if (graphicsController == null)
            {
                MelonLogger.Msg("InGameSettingsScreen: GraphicsMenuController not found");
                return;
            }

            // Section header
            controls.Add(new MenuControl
            {
                Type = MenuControlType.Button,
                Label = "--- Graphics ---",
                IsInteractable = false
            });

            // Quality dropdowns
            AddDropdown(graphicsController.qualitySettingsDropdown, "Quality");
            AddDropdown(graphicsController.textureSettingsDropdown, "Texture Quality");
            AddDropdown(graphicsController.antiAliasingSettingsDropdown, "Anti-Aliasing");
            AddDropdown(graphicsController.antiAliasingModeDropdown, "Anti-Aliasing Mode");
            AddDropdown(graphicsController.resolutionSettingsDropdown, "Resolution");
            AddDropdown(graphicsController.skyboxVariantDropdown, "Skybox Variant");

            // Toggles
            AddToggle(graphicsController.fullscreenSettingToggle, "Fullscreen");
            AddToggle(graphicsController.enableVSyncToggle, "V-Sync");
            AddToggle(graphicsController.confineCursorToggle, "Confine Cursor");
            AddToggle(graphicsController.textureStreamingToggle, "Texture Streaming");
            AddToggle(graphicsController.largeUIScaleToggle, "Large UI Scale");
            AddToggle(graphicsController.useCouncilorVideoToggle, "Use Councilor Video");
        }

        private void AddToggle(Toggle toggle, string label)
        {
            // Don't check activeInHierarchy - controls exist but may be on inactive tab panels
            if (toggle != null)
            {
                var control = MenuControl.FromToggle(toggle, label);
                if (control != null)
                    controls.Add(control);
            }
        }

        private void AddSlider(Slider slider, string label)
        {
            // Don't check activeInHierarchy - controls exist but may be on inactive tab panels
            if (slider != null)
            {
                var control = MenuControl.FromSlider(slider, label);
                if (control != null)
                    controls.Add(control);
            }
        }

        private void AddDropdown(TMP_Dropdown dropdown, string label)
        {
            // Don't check activeInHierarchy - controls exist but may be on inactive tab panels
            if (dropdown != null)
            {
                var control = MenuControl.FromDropdown(dropdown, label);
                if (control != null)
                    controls.Add(control);
            }
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];
            if (!control.IsInteractable)
            {
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

            MelonLogger.Msg($"InGameSettingsScreen: Activated '{control.Label}'");
        }

        public override string GetActivationAnnouncement()
        {
            return $"Settings. {controls.Count} items.";
        }

        public override void AdjustControl(int index, bool increment)
        {
            base.AdjustControl(index, increment);

            // Save settings after adjustment
            TIPlayerProfileManager.SavePlayerConfig();
        }

        public override void OnDeactivate()
        {
            // Save settings when leaving
            try
            {
                TIPlayerProfileManager.SavePlayerConfig();
                MelonLogger.Msg("InGameSettingsScreen: Saved player settings");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"InGameSettingsScreen: Failed to save settings: {ex.Message}");
            }
        }
    }
}
