using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TISpeech.ReviewMode;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the main start menu.
    /// Provides navigation through: Continue, New Game, Load Game, Options, Skirmish, Mods, Credits, Exit
    /// </summary>
    public class MainMenuScreen : MenuScreenBase
    {
        public override string Name => "Main Menu";

        private List<MenuControl> controls = new List<MenuControl>();
        private StartMenuController menuController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        public override void Refresh()
        {
            controls.Clear();

            try
            {
                menuController = UnityEngine.Object.FindObjectOfType<StartMenuController>();
                if (menuController == null)
                {
                    MelonLogger.Msg("MainMenuScreen: StartMenuController not found");
                    return;
                }

                // Get the main menu buttons in order
                // These are typically children of the buttons canvas group

                // Continue button
                if (menuController.continueButton != null && menuController.continueButton.gameObject.activeInHierarchy)
                {
                    var control = MenuControl.FromButton(menuController.continueButton, "Continue");
                    if (control != null)
                        controls.Add(control);
                }

                // Find buttons by their text labels
                // The main menu buttons are typically in the buttonsCanvasGroup
                var buttonsParent = menuController.buttonsCanvasGroup?.transform;
                if (buttonsParent != null)
                {
                    AddButtonsFromParent(buttonsParent);
                }
                else
                {
                    // Fallback: Find buttons in the scene root
                    FindMainMenuButtons();
                }

                MelonLogger.Msg($"MainMenuScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing MainMenuScreen: {ex.Message}");
            }
        }

        private void AddButtonsFromParent(Transform parent)
        {
            // Get all buttons in the parent
            var buttons = parent.GetComponentsInChildren<Button>(includeInactive: false);

            foreach (var button in buttons)
            {
                // Skip the continue button if we already added it
                if (menuController.continueButton != null && button == menuController.continueButton)
                    continue;

                // Get button label from TMP_Text
                var tmpText = button.GetComponentInChildren<TMP_Text>();
                if (tmpText == null)
                    continue;

                string label = TISpeechMod.CleanText(tmpText.text);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                // Skip if already added
                bool alreadyExists = controls.Exists(c => c.Label == label);
                if (alreadyExists)
                    continue;

                var control = MenuControl.FromButton(button, label);
                if (control != null)
                {
                    controls.Add(control);
                    MelonLogger.Msg($"MainMenuScreen: Added button '{label}'");
                }
            }
        }

        private void FindMainMenuButtons()
        {
            // Known button text labels we're looking for
            var buttonLabels = new Dictionary<string, TMP_Text>
            {
                { "Continue", menuController.continueButtonText },
                { "New Game", menuController.newGameText },
                { "Load Game", menuController.loadGameText },
                { "Options", menuController.optionsText },
                { "Skirmish", menuController.skirmishModeText },
                { "Mods", menuController.modsText },
                { "Credits", menuController.creditsText },
                { "Exit", menuController.exitText }
            };

            foreach (var kvp in buttonLabels)
            {
                string label = kvp.Key;
                TMP_Text textField = kvp.Value;

                if (textField == null)
                    continue;

                // Skip Continue if already added
                if (label == "Continue" && controls.Exists(c => c.Label == "Continue"))
                    continue;

                // Find the button that contains this text
                Button button = FindButtonForText(textField);
                if (button != null)
                {
                    // Use the actual text content (may be localized)
                    string actualLabel = TISpeechMod.CleanText(textField.text);
                    if (string.IsNullOrWhiteSpace(actualLabel))
                        actualLabel = label;

                    var control = MenuControl.FromButton(button, actualLabel);
                    if (control != null)
                    {
                        controls.Add(control);
                        MelonLogger.Msg($"MainMenuScreen: Added button '{actualLabel}'");
                    }
                }
            }
        }

        private Button FindButtonForText(TMP_Text textField)
        {
            if (textField == null)
                return null;

            // The button is usually a parent of the text, or on the same object
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

            MelonLogger.Msg($"MainMenuScreen: Activated '{control.Label}'");

            // Switch to the appropriate sub-menu screen based on button label
            var controller = ReviewModeController.Instance;
            if (controller != null)
            {
                string labelLower = control.Label.ToLower();

                if (labelLower.Contains("new game") || labelLower.Contains("new campaign"))
                {
                    controller.SwitchToMenuScreen("New Game");
                }
                else if (labelLower.Contains("load"))
                {
                    controller.SwitchToMenuScreen("Load Game");
                }
                else if (labelLower.Contains("option") || labelLower.Contains("settings"))
                {
                    controller.SwitchToMenuScreen("Options");
                }
                else if (labelLower.Contains("skirmish"))
                {
                    controller.SwitchToMenuScreen("Skirmish");
                }
                else if (labelLower.Contains("mod"))
                {
                    controller.SwitchToMenuScreen("Mods");
                }
            }
        }
    }
}
