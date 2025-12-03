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

        // Cached button references for object-based identification
        private Button newGameButton;
        private Button loadGameButton;
        private Button optionsButton;
        private Button skirmishButton;
        private Button modsButton;
        private Button creditsButton;
        private Button exitButton;

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

                // Cache button references by finding buttons from their known TMP_Text fields
                CacheButtonReferences();

                // Add buttons in a logical order
                AddKnownButtons();

                MelonLogger.Msg($"MainMenuScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing MainMenuScreen: {ex.Message}");
            }
        }

        /// <summary>
        /// Cache button references by finding the Button component from known TMP_Text fields.
        /// This allows us to identify buttons by object reference, not by localized text.
        /// </summary>
        private void CacheButtonReferences()
        {
            newGameButton = FindButtonForText(menuController.newGameText);
            loadGameButton = FindButtonForText(menuController.loadGameText);
            optionsButton = FindButtonForText(menuController.optionsText);
            skirmishButton = FindButtonForText(menuController.skirmishModeText);
            modsButton = FindButtonForText(menuController.modsText);
            creditsButton = FindButtonForText(menuController.creditsText);
            exitButton = FindButtonForText(menuController.exitText);
        }

        /// <summary>
        /// Add buttons using known references, with semantic Action identifiers.
        /// </summary>
        private void AddKnownButtons()
        {
            // Continue button (has direct Button reference)
            if (menuController.continueButton != null && menuController.continueButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(menuController.continueButton,
                    menuController.continueButtonText, "Continue");
                if (control != null)
                    controls.Add(control);
            }

            // New Game
            if (newGameButton != null && newGameButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(newGameButton,
                    menuController.newGameText, "NewGame");
                if (control != null)
                    controls.Add(control);
            }

            // Load Game
            if (loadGameButton != null && loadGameButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(loadGameButton,
                    menuController.loadGameText, "LoadGame");
                if (control != null)
                    controls.Add(control);
            }

            // Options
            if (optionsButton != null && optionsButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(optionsButton,
                    menuController.optionsText, "Options");
                if (control != null)
                    controls.Add(control);
            }

            // Skirmish
            if (skirmishButton != null && skirmishButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(skirmishButton,
                    menuController.skirmishModeText, "Skirmish");
                if (control != null)
                    controls.Add(control);
            }

            // Mods
            if (modsButton != null && modsButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(modsButton,
                    menuController.modsText, "Mods");
                if (control != null)
                    controls.Add(control);
            }

            // Credits
            if (creditsButton != null && creditsButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(creditsButton,
                    menuController.creditsText, "Credits");
                if (control != null)
                    controls.Add(control);
            }

            // Exit
            if (exitButton != null && exitButton.gameObject.activeInHierarchy)
            {
                var control = CreateControlWithAction(exitButton,
                    menuController.exitText, "Exit");
                if (control != null)
                    controls.Add(control);
            }
        }

        /// <summary>
        /// Create a MenuControl with a semantic Action identifier.
        /// Label comes from the localized text (for announcement), Action is language-independent.
        /// </summary>
        private MenuControl CreateControlWithAction(Button button, TMP_Text textField, string action)
        {
            if (button == null)
                return null;

            // Get localized label for announcement
            string label = textField != null ? TISpeechMod.CleanText(textField.text) : action;
            if (string.IsNullOrWhiteSpace(label))
                label = action;

            var control = MenuControl.FromButton(button, label);
            if (control != null)
            {
                control.Action = action;
                MelonLogger.Msg($"MainMenuScreen: Added button '{label}' with action '{action}'");
            }
            return control;
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

            // Announce what we're activating (uses localized label)
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);

            // Invoke the button click
            control.Activate();

            MelonLogger.Msg($"MainMenuScreen: Activated '{control.Label}' (action: {control.Action})");

            // Switch to the appropriate sub-menu screen based on Action (language-independent)
            var controller = ReviewModeController.Instance;
            if (controller != null && !string.IsNullOrEmpty(control.Action))
            {
                switch (control.Action)
                {
                    case "NewGame":
                        controller.SwitchToMenuScreen("New Game");
                        break;
                    case "LoadGame":
                        controller.SwitchToMenuScreen("Load Game");
                        break;
                    case "Options":
                        controller.SwitchToMenuScreen("Options");
                        break;
                    case "Skirmish":
                        controller.SwitchToMenuScreen("Skirmish");
                        break;
                    case "Mods":
                        controller.SwitchToMenuScreen("Mods");
                        break;
                }
            }
        }
    }
}
