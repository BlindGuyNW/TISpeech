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
    /// Escape menu screen for the main pause menu.
    /// Provides navigation through: Back to Game, Save Game, Load Game, Settings,
    /// Exit to Main Menu, Exit Game
    /// </summary>
    public class InGameEscapeMenuScreen : EscapeMenuScreenBase
    {
        public override string Name => "Escape Menu";

        private List<MenuControl> controls = new List<MenuControl>();
        private OptionsScreenController optionsController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        public override void Refresh()
        {
            controls.Clear();

            try
            {
                optionsController = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsController == null)
                {
                    MelonLogger.Msg("InGameEscapeMenuScreen: OptionsScreenController not found");
                    return;
                }

                // Back to Game button
                AddButtonFromText(optionsController.backtoGameText, "BackToGame");

                // Save Game button
                AddButtonFromText(optionsController.saveGameText, "SaveGame");

                // Load Game button
                AddButtonFromText(optionsController.loadGameText, "LoadGame");

                // Settings button
                AddButtonFromText(optionsController.settingsText, "Settings");

                // Exit to Main Menu button
                AddButtonFromText(optionsController.exitToMainMenuText, "ExitToMainMenu");

                // Exit Game button
                AddButtonFromText(optionsController.exitGameText, "ExitGame");

                // Codex button (if available)
                if (optionsController.codexButtonText != null)
                {
                    AddButtonFromText(optionsController.codexButtonText, "Codex");
                }

                MelonLogger.Msg($"InGameEscapeMenuScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing InGameEscapeMenuScreen: {ex.Message}");
            }
        }

        private void AddButtonFromText(TMP_Text textField, string action)
        {
            if (textField == null)
                return;

            var button = FindButtonForText(textField);
            if (button == null || !button.gameObject.activeInHierarchy)
                return;

            string label = TISpeechMod.CleanText(textField.text);
            if (string.IsNullOrWhiteSpace(label))
                label = action;

            var control = MenuControl.FromButton(button, label);
            if (control != null)
            {
                control.Action = action;
                controls.Add(control);
                MelonLogger.Msg($"InGameEscapeMenuScreen: Added button '{label}' with action '{action}'");
            }
        }

        private Button FindButtonForText(TMP_Text textField)
        {
            if (textField == null)
                return null;

            // The button is usually a parent of the text
            Transform current = textField.transform;
            while (current != null)
            {
                var button = current.GetComponent<Button>();
                if (button != null)
                    return button;
                current = current.parent;
            }

            return null;
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

            // Announce what we're activating
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);

            // Invoke the button click
            control.Activate();

            MelonLogger.Msg($"InGameEscapeMenuScreen: Activated '{control.Label}' (action: {control.Action})");

            // Note: Screen transitions (to Save, Load, Settings) will be detected by
            // EscapeMenuSubMode.CheckContextChange() based on which GameObject becomes active
        }

        public override string GetActivationAnnouncement()
        {
            return $"{Name}. {controls.Count} options.";
        }
    }
}
