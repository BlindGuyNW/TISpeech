using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine.UI;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Represents a single navigable option within a notification.
    /// </summary>
    public class NotificationOption
    {
        public string Label { get; set; }
        public Button Button { get; set; }
        public string DetailText { get; set; }
        public bool IsNarrativeOption { get; set; }
    }

    /// <summary>
    /// Sub-mode for navigating notification popups.
    /// Activated when a notification appears while Review Mode is active.
    /// </summary>
    public class NotificationSubMode
    {
        public List<NotificationOption> Options { get; private set; }
        public int CurrentIndex { get; private set; }

        private string headline;
        private string bodyText;
        private bool isNarrativeEvent;

        public int Count => Options.Count;
        public NotificationOption CurrentOption => Options.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Options.Count
            ? Options[CurrentIndex]
            : null;

        public NotificationSubMode(NotificationScreenController controller)
        {
            Options = new List<NotificationOption>();
            CurrentIndex = 0;

            try
            {
                // Extract headline and body text
                headline = controller.alertHeadlineText?.text ?? "";
                bodyText = controller.alertBodyText?.text ?? "";

                // Clean the text
                headline = TISpeechMod.CleanText(headline);
                bodyText = TISpeechMod.CleanText(bodyText);

                // Check if this is a narrative event (has option buttons)
                isNarrativeEvent = controller.narrativeEventButtonsPanel != null &&
                                   controller.narrativeEventButtonsPanel.activeSelf;

                if (isNarrativeEvent)
                {
                    BuildNarrativeEventOptions(controller);
                }
                else
                {
                    BuildStandardAlertOptions(controller);
                }

                MelonLogger.Msg($"NotificationSubMode created with {Options.Count} options, isNarrativeEvent={isNarrativeEvent}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating NotificationSubMode: {ex.Message}");
            }
        }

        private void BuildNarrativeEventOptions(NotificationScreenController controller)
        {
            // Narrative events have up to 4 option buttons
            for (int i = 0; i < 4; i++)
            {
                if (i < controller.optionButtons.Length &&
                    controller.optionButtons[i] != null &&
                    controller.optionButtons[i].gameObject.activeSelf)
                {
                    var button = controller.optionButtons[i];
                    string label = "";
                    string detail = "";

                    // Get button text
                    if (i < controller.optionButtonText.Length && controller.optionButtonText[i] != null)
                    {
                        label = TISpeechMod.CleanText(controller.optionButtonText[i].text);
                    }

                    // Get tooltip detail
                    if (i < controller.optionButtonDetail.Length && controller.optionButtonDetail[i] != null)
                    {
                        detail = ExtractTooltipText(controller.optionButtonDetail[i]);
                    }

                    if (!string.IsNullOrEmpty(label))
                    {
                        Options.Add(new NotificationOption
                        {
                            Label = label,
                            Button = button,
                            DetailText = detail,
                            IsNarrativeOption = true
                        });
                    }
                }
            }
        }

        private void BuildStandardAlertOptions(NotificationScreenController controller)
        {
            // NOTE: We only check if button GameObjects are active, NOT if buttons are interactable.
            // The game sets buttons to activeSelf=true but interactable=false initially,
            // then enables interactivity via a coroutine delay. Our postfix runs before the
            // coroutine completes, so buttons are visible but not yet interactable.
            // We check interactability at activation time instead.

            // Add custom delegate buttons first (these are special actions like "Repeat Mission")
            if (controller.customDelegateButton != null)
            {
                for (int i = 0; i < controller.customDelegateButton.Length; i++)
                {
                    var button = controller.customDelegateButton[i];
                    if (button != null && button.gameObject.activeSelf)
                    {
                        string label = "";
                        string detail = "";

                        // Get button text
                        if (i < controller.customDelegateButtonText.Length &&
                            controller.customDelegateButtonText[i] != null)
                        {
                            label = TISpeechMod.CleanText(controller.customDelegateButtonText[i].text);
                        }

                        // Get tooltip if available
                        if (i < controller.customDelegateTooltip.Length &&
                            controller.customDelegateTooltip[i] != null)
                        {
                            detail = ExtractTooltipText(controller.customDelegateTooltip[i]);
                        }

                        if (!string.IsNullOrEmpty(label))
                        {
                            Options.Add(new NotificationOption
                            {
                                Label = label,
                                Button = button,
                                DetailText = detail,
                                IsNarrativeOption = false
                            });
                            MelonLogger.Msg($"Added custom delegate button: {label}");
                        }
                    }
                }
            }

            // Add standard buttons (Go To, OK, Close)
            // Go To button - check if GameObject is active
            if (controller.gotoButton != null &&
                controller.gotoButtonObject != null &&
                controller.gotoButtonObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.gotoButtonText?.text ?? "Go To");
                Options.Add(new NotificationOption
                {
                    Label = label,
                    Button = controller.gotoButton,
                    DetailText = "Navigate to the related location or object",
                    IsNarrativeOption = false
                });
                MelonLogger.Msg($"Added gotoButton: {label}");
            }

            // OK button - check if GameObject is active
            if (controller.okayButton != null &&
                controller.okayButtonObject != null &&
                controller.okayButtonObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.okayButtonText?.text ?? "OK");
                Options.Add(new NotificationOption
                {
                    Label = label,
                    Button = controller.okayButton,
                    DetailText = "Acknowledge and continue",
                    IsNarrativeOption = false
                });
                MelonLogger.Msg($"Added okayButton: {label}");
            }

            // Close button - check if GameObject is active
            if (controller.closeButton != null &&
                controller.closeButtonObject != null &&
                controller.closeButtonObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.closeButtonText?.text ?? "Close");
                Options.Add(new NotificationOption
                {
                    Label = label,
                    Button = controller.closeButton,
                    DetailText = "Dismiss this notification",
                    IsNarrativeOption = false
                });
                MelonLogger.Msg($"Added closeButton: {label}");
            }

            // Exit button - always add if visible, as a fallback dismiss option
            if (controller.exitButton != null &&
                controller.exitButtonObject != null &&
                controller.exitButtonObject.activeSelf)
            {
                Options.Add(new NotificationOption
                {
                    Label = "Exit",
                    Button = controller.exitButton,
                    DetailText = "Close this notification",
                    IsNarrativeOption = false
                });
                MelonLogger.Msg("Added exitButton: Exit");
            }
        }

        private string ExtractTooltipText(ModelShark.TooltipTrigger tooltip)
        {
            try
            {
                if (tooltip == null || tooltip.Tooltip == null)
                    return "";

                var textFields = tooltip.Tooltip.TextFields;
                if (textFields == null || textFields.Count == 0)
                    return "";

                var sb = new StringBuilder();
                foreach (var field in textFields)
                {
                    if (field?.Text != null && !string.IsNullOrEmpty(field.Text.text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(TISpeechMod.CleanText(field.Text.text));
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error extracting tooltip: {ex.Message}");
                return "";
            }
        }

        public void Next()
        {
            if (Options.Count == 0) return;
            CurrentIndex = (CurrentIndex + 1) % Options.Count;
        }

        public void Previous()
        {
            if (Options.Count == 0) return;
            CurrentIndex--;
            if (CurrentIndex < 0) CurrentIndex = Options.Count - 1;
        }

        public void Activate()
        {
            try
            {
                var option = CurrentOption;
                if (option?.Button == null)
                {
                    MelonLogger.Msg("Cannot activate: no option selected");
                    return;
                }

                if (!option.Button.interactable)
                {
                    // Button may still be in the delayed interactable state
                    MelonLogger.Msg($"Button '{option.Label}' not yet interactable - waiting briefly");
                    TISpeechMod.Speak("Button not ready, please wait", interrupt: true);
                    return;
                }

                MelonLogger.Msg($"Activating notification option: {option.Label}");
                option.Button.onClick.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating notification option: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the announcement text for entering notification mode.
        /// </summary>
        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append("Notification");

            if (!string.IsNullOrEmpty(headline))
            {
                sb.Append(": ");
                sb.Append(headline);
            }

            if (!string.IsNullOrEmpty(bodyText))
            {
                sb.Append(". ");
                // Truncate body text for initial announcement
                string truncated = bodyText.Length > 200 ? bodyText.Substring(0, 200) + "..." : bodyText;
                sb.Append(truncated);
            }

            sb.Append(". ");
            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " option" : " options");

            if (Options.Count > 0)
            {
                sb.Append(". 1 of ");
                sb.Append(Options.Count);
                sb.Append(": ");
                sb.Append(Options[0].Label);
                sb.Append(". Use up/down to navigate, Enter to select, * for option details.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get the announcement for the current option.
        /// </summary>
        public string GetCurrentAnnouncement()
        {
            if (Options.Count == 0)
                return "No options available";

            var option = CurrentOption;
            if (option == null)
                return "No option selected";

            return $"{CurrentIndex + 1} of {Options.Count}: {option.Label}";
        }

        /// <summary>
        /// Get the detail text for the current option (for Numpad *).
        /// </summary>
        public string GetCurrentDetail()
        {
            var option = CurrentOption;
            if (option == null)
                return "No option selected";

            if (!string.IsNullOrEmpty(option.DetailText))
            {
                return $"{option.Label}: {option.DetailText}";
            }

            return option.Label;
        }

        /// <summary>
        /// List all options (for Numpad /).
        /// </summary>
        public string ListAllOptions()
        {
            if (Options.Count == 0)
                return "No options available";

            var sb = new StringBuilder();
            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " option: " : " options: ");

            for (int i = 0; i < Options.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Options[i].Label);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Find and navigate to the Close/Cancel button.
        /// </summary>
        /// <returns>True if a close option was found and selected.</returns>
        public bool SelectCloseOption()
        {
            for (int i = 0; i < Options.Count; i++)
            {
                string label = Options[i].Label.ToLower();
                if (label.Contains("close") || label.Contains("cancel") || label.Contains("exit"))
                {
                    CurrentIndex = i;
                    return true;
                }
            }

            // If no explicit close, try the last option (usually Close or OK)
            if (Options.Count > 0)
            {
                CurrentIndex = Options.Count - 1;
                return true;
            }

            return false;
        }
    }
}
