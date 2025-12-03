using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the first-game tutorial recommendation dialog.
    /// This dialog appears on first game launch and recommends enabling the tutorial.
    /// The user must dismiss it before they can access the main menu.
    /// </summary>
    public class TutorialRecommendationScreen : MenuScreenBase
    {
        public override string Name => "Tutorial Recommendation";

        private List<MenuControl> controls = new List<MenuControl>();
        private StartMenuController menuController;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the tutorial recommendation dialog is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var controller = UnityEngine.Object.FindObjectOfType<StartMenuController>();
            if (controller == null)
                return false;

            // Check if firstGameTutorialObject is active
            if (controller.firstGameTutorialObject != null &&
                controller.firstGameTutorialObject.activeInHierarchy)
            {
                return true;
            }

            return false;
        }

        public override void Refresh()
        {
            controls.Clear();

            try
            {
                menuController = UnityEngine.Object.FindObjectOfType<StartMenuController>();
                if (menuController == null)
                {
                    MelonLogger.Msg("TutorialRecommendationScreen: StartMenuController not found");
                    return;
                }

                if (menuController.firstGameTutorialObject == null ||
                    !menuController.firstGameTutorialObject.activeInHierarchy)
                {
                    MelonLogger.Msg("TutorialRecommendationScreen: Dialog not visible");
                    return;
                }

                // Add the description text as a readable item
                if (menuController.recommendTutorialDescText != null)
                {
                    string descText = TISpeechMod.CleanText(menuController.recommendTutorialDescText.text);
                    if (!string.IsNullOrWhiteSpace(descText))
                    {
                        controls.Add(new MenuControl
                        {
                            Type = MenuControlType.Button,
                            Label = descText,
                            IsInteractable = false,
                            DetailText = descText
                        });
                    }
                }

                // Find the close/dismiss button in the tutorial recommendation dialog
                // The button text is in recommendTutorialButtonText
                var buttons = menuController.firstGameTutorialObject.GetComponentsInChildren<Button>(includeInactive: false);
                foreach (var button in buttons)
                {
                    if (button == null || !button.gameObject.activeInHierarchy)
                        continue;

                    // Get button label
                    string label = null;

                    // First check if this is the main recommendation button
                    var tmpText = button.GetComponentInChildren<TMP_Text>();
                    if (tmpText != null)
                    {
                        label = TISpeechMod.CleanText(tmpText.text);
                    }

                    if (string.IsNullOrWhiteSpace(label))
                        label = "Dismiss";

                    var control = MenuControl.FromButton(button, label);
                    if (control != null)
                    {
                        controls.Add(control);
                        MelonLogger.Msg($"TutorialRecommendationScreen: Added button '{label}'");
                    }
                }

                MelonLogger.Msg($"TutorialRecommendationScreen: Found {controls.Count} controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing TutorialRecommendationScreen: {ex.Message}");
            }
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            if (!control.IsInteractable)
            {
                // For the description text, just re-read it
                TISpeechMod.Speak(control.Label, interrupt: true);
                return;
            }

            // Announce what we're activating
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);

            // Invoke the button click
            control.Activate();

            MelonLogger.Msg($"TutorialRecommendationScreen: Activated '{control.Label}'");

            // After dismissing the tutorial recommendation, switch to main menu
            var controller = ReviewModeController.Instance;
            if (controller != null)
            {
                // Give Unity a moment to process the button click and hide the dialog
                // The SwitchToMenuScreen will happen via the normal menu detection on next refresh
                controller.SwitchToMenuScreen("Main Menu");
            }
        }

        public override string GetActivationAnnouncement()
        {
            string desc = "";
            if (menuController?.recommendTutorialDescText != null)
            {
                desc = TISpeechMod.CleanText(menuController.recommendTutorialDescText.text);
            }

            if (!string.IsNullOrWhiteSpace(desc))
            {
                return $"First time playing. {desc}";
            }

            return "First time playing. Tutorial recommendation dialog. Use Enter to dismiss.";
        }
    }
}
