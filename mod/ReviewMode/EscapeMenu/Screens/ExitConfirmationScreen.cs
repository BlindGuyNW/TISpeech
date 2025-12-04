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
    /// Escape menu screen for exit confirmation dialogs.
    /// Handles "Exit to Main Menu" and "Exit Game" confirmation prompts.
    /// </summary>
    public class ExitConfirmationScreen : EscapeMenuScreenBase
    {
        public override string Name => "Exit Confirmation";

        private List<MenuControl> controls = new List<MenuControl>();
        private OptionsScreenController optionsController;
        private string warningText = "";

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Exit Confirmation dialog is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
            if (optionsScreen == null)
                return false;

            return optionsScreen.exitWithoutSaveWarningObject != null &&
                   optionsScreen.exitWithoutSaveWarningObject.activeSelf;
        }

        public override void Refresh()
        {
            controls.Clear();
            warningText = "";

            try
            {
                optionsController = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsController == null)
                {
                    MelonLogger.Msg("ExitConfirmationScreen: OptionsScreenController not found");
                    return;
                }

                // Get the warning text
                if (optionsController.exitWithoutSaveWarningText != null)
                {
                    warningText = TISpeechMod.CleanText(optionsController.exitWithoutSaveWarningText.text);
                }

                // Find Confirm button
                if (optionsController.exitWithoutSaveConfirm != null)
                {
                    var confirmButton = optionsController.exitWithoutSaveConfirm.GetComponentInParent<Button>();
                    if (confirmButton != null && confirmButton.gameObject.activeInHierarchy)
                    {
                        string label = TISpeechMod.CleanText(optionsController.exitWithoutSaveConfirm.text);
                        if (string.IsNullOrWhiteSpace(label)) label = "Confirm";

                        var control = MenuControl.FromButton(confirmButton, label);
                        if (control != null)
                        {
                            control.Action = "Confirm";
                            control.DetailText = "Confirm exit";
                            controls.Add(control);
                        }
                    }
                }

                // Find Cancel button
                if (optionsController.exitWithoutSaveCancel != null)
                {
                    var cancelButton = optionsController.exitWithoutSaveCancel.GetComponentInParent<Button>();
                    if (cancelButton != null && cancelButton.gameObject.activeInHierarchy)
                    {
                        string label = TISpeechMod.CleanText(optionsController.exitWithoutSaveCancel.text);
                        if (string.IsNullOrWhiteSpace(label)) label = "Cancel";

                        var control = MenuControl.FromButton(cancelButton, label);
                        if (control != null)
                        {
                            control.Action = "Cancel";
                            control.DetailText = "Cancel and return to escape menu";
                            controls.Add(control);
                        }
                    }
                }

                MelonLogger.Msg($"ExitConfirmationScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing ExitConfirmationScreen: {ex.Message}");
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

            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"ExitConfirmationScreen: Activated '{control.Label}'");
        }

        public override string GetActivationAnnouncement()
        {
            if (!string.IsNullOrEmpty(warningText))
            {
                return $"{warningText} {controls.Count} options.";
            }
            return $"Exit confirmation. {controls.Count} options.";
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // Include the warning text in the detail
            if (!string.IsNullOrEmpty(warningText))
            {
                return $"{warningText} {control.Label}: {control.DetailText}";
            }

            return control.GetDetail();
        }
    }
}
