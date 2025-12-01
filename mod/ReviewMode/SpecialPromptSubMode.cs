using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Type of special prompt panel being displayed.
    /// </summary>
    public enum SpecialPromptType
    {
        None,
        RemoveArmies,       // Army removal request from another nation
        DiplomaticResponse, // Response to diplomatic proposals (alliance, federation, etc.)
        CallToWar           // Ally calling you to join their war
    }

    /// <summary>
    /// Represents a single navigable option within a special prompt.
    /// </summary>
    public class SpecialPromptOption
    {
        public string Label { get; set; }
        public Button Button { get; set; }
        public string DetailText { get; set; }
        public bool IsInformational { get; set; }
    }

    /// <summary>
    /// Sub-mode for navigating special prompt panels that appear outside the standard notification flow.
    /// Handles: Army Removal prompts, Diplomatic Response prompts, Call to War prompts.
    /// </summary>
    public class SpecialPromptSubMode
    {
        public List<SpecialPromptOption> Options { get; private set; }
        public int CurrentIndex { get; private set; }
        public SpecialPromptType PromptType { get; private set; }

        private NotificationScreenController controller;
        private string promptText;
        private string panelTitle;

        public int Count => Options.Count;
        public SpecialPromptOption CurrentOption => Options.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Options.Count
            ? Options[CurrentIndex]
            : null;

        public SpecialPromptSubMode(NotificationScreenController controller, SpecialPromptType promptType)
        {
            this.controller = controller;
            this.PromptType = promptType;
            Options = new List<SpecialPromptOption>();
            CurrentIndex = 0;

            try
            {
                switch (promptType)
                {
                    case SpecialPromptType.RemoveArmies:
                        BuildRemoveArmiesOptions();
                        break;
                    case SpecialPromptType.DiplomaticResponse:
                        BuildDiplomaticResponseOptions();
                        break;
                    case SpecialPromptType.CallToWar:
                        BuildCallToWarOptions();
                        break;
                }

                MelonLogger.Msg($"SpecialPromptSubMode created with {Options.Count} options for {promptType}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating SpecialPromptSubMode: {ex.Message}");
            }
        }

        private void BuildRemoveArmiesOptions()
        {
            panelTitle = "Army Removal Request";

            // Extract the prompt text
            if (controller.removeArmiesPromptText != null)
            {
                promptText = TISpeechMod.CleanText(controller.removeArmiesPromptText.text);
            }

            // Add informational item with the prompt text
            if (!string.IsNullOrEmpty(promptText))
            {
                Options.Add(new SpecialPromptOption
                {
                    Label = promptText,
                    Button = null,
                    DetailText = "The request details",
                    IsInformational = true
                });
            }

            // Add Propose Alliance button
            if (controller.removeArmies_proposeAllianceButton != null &&
                controller.removeArmies_proposeAllianceButton.gameObject.activeInHierarchy)
            {
                string label = "Propose Alliance";
                if (controller.proposeAllianceButtonText != null)
                {
                    label = TISpeechMod.CleanText(controller.proposeAllianceButtonText.text);
                }

                Options.Add(new SpecialPromptOption
                {
                    Label = label,
                    Button = controller.removeArmies_proposeAllianceButton,
                    DetailText = controller.removeArmies_proposeAllianceButton.interactable
                        ? "Offer an alliance to avoid conflict"
                        : "Not available - cannot propose alliance",
                    IsInformational = false
                });
            }

            // Add Declare War button
            if (controller.removeArmies_declareWarButton != null &&
                controller.removeArmies_declareWarButton.gameObject.activeInHierarchy)
            {
                string label = "Declare War";
                if (controller.declareWarButtonText != null)
                {
                    label = TISpeechMod.CleanText(controller.declareWarButtonText.text);
                }

                Options.Add(new SpecialPromptOption
                {
                    Label = label,
                    Button = controller.removeArmies_declareWarButton,
                    DetailText = controller.removeArmies_declareWarButton.interactable
                        ? "Declare war rather than withdraw"
                        : "Not available - cannot declare war",
                    IsInformational = false
                });
            }

            // Add Send Armies Home button (find by searching the panel)
            // The go home button doesn't have a direct field, so we find it via the text field's parent
            if (controller.sendArmiesHomeButtonText != null)
            {
                var goHomeButton = controller.sendArmiesHomeButtonText.GetComponentInParent<Button>();
                if (goHomeButton != null && goHomeButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(controller.sendArmiesHomeButtonText.text);
                    if (string.IsNullOrEmpty(label)) label = "Send Armies Home";

                    Options.Add(new SpecialPromptOption
                    {
                        Label = label,
                        Button = goHomeButton,
                        DetailText = "Comply with the request and withdraw armies",
                        IsInformational = false
                    });
                }
            }
        }

        private void BuildDiplomaticResponseOptions()
        {
            panelTitle = "Diplomatic Response";

            // Extract nation name and prompt text
            string nationName = "";
            if (controller.responsePanelNationName != null)
            {
                nationName = TISpeechMod.CleanText(controller.responsePanelNationName.text);
            }

            if (controller.responsePanelText != null)
            {
                promptText = TISpeechMod.CleanText(controller.responsePanelText.text);
            }

            // Add informational item with the prompt text
            if (!string.IsNullOrEmpty(promptText))
            {
                Options.Add(new SpecialPromptOption
                {
                    Label = promptText,
                    Button = null,
                    DetailText = !string.IsNullOrEmpty(nationName) ? $"From {nationName}" : "Diplomatic proposal",
                    IsInformational = true
                });
            }

            // Find the confirm and decline buttons by their text fields
            if (controller.responseConfirmButtonText != null)
            {
                var confirmButton = controller.responseConfirmButtonText.GetComponentInParent<Button>();
                if (confirmButton != null && confirmButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(controller.responseConfirmButtonText.text);
                    if (string.IsNullOrEmpty(label)) label = "Accept";

                    Options.Add(new SpecialPromptOption
                    {
                        Label = label,
                        Button = confirmButton,
                        DetailText = "Accept the proposal",
                        IsInformational = false
                    });
                }
            }

            if (controller.responseDeclineButtonText != null)
            {
                var declineButton = controller.responseDeclineButtonText.GetComponentInParent<Button>();
                if (declineButton != null && declineButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(controller.responseDeclineButtonText.text);
                    if (string.IsNullOrEmpty(label)) label = "Decline";

                    Options.Add(new SpecialPromptOption
                    {
                        Label = label,
                        Button = declineButton,
                        DetailText = "Decline the proposal",
                        IsInformational = false
                    });
                }
            }
        }

        private void BuildCallToWarOptions()
        {
            panelTitle = "Call to War";

            // Extract the call to war prompt text
            if (controller.callAllyPrompt != null)
            {
                promptText = TISpeechMod.CleanText(controller.callAllyPrompt.text);
            }

            // Add informational item with the prompt text
            if (!string.IsNullOrEmpty(promptText))
            {
                Options.Add(new SpecialPromptOption
                {
                    Label = promptText,
                    Button = null,
                    DetailText = "Your ally is calling you to join their war",
                    IsInformational = true
                });
            }

            // Find the accept button by its text field
            if (controller.allyAcceptButtonText != null)
            {
                var acceptButton = controller.allyAcceptButtonText.GetComponentInParent<Button>();
                if (acceptButton != null && acceptButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(controller.allyAcceptButtonText.text);
                    if (string.IsNullOrEmpty(label)) label = "Join War";

                    Options.Add(new SpecialPromptOption
                    {
                        Label = label,
                        Button = acceptButton,
                        DetailText = "Honor your alliance and join the war",
                        IsInformational = false
                    });
                }
            }

            // Find the decline button by its text field
            if (controller.allyDeclineButtonText != null)
            {
                var declineButton = controller.allyDeclineButtonText.GetComponentInParent<Button>();
                if (declineButton != null && declineButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(controller.allyDeclineButtonText.text);
                    if (string.IsNullOrEmpty(label)) label = "Decline";

                    Options.Add(new SpecialPromptOption
                    {
                        Label = label,
                        Button = declineButton,
                        DetailText = "Refuse to join the war",
                        IsInformational = false
                    });
                }
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
                if (option == null)
                {
                    MelonLogger.Msg("Cannot activate: no option selected");
                    return;
                }

                // Informational items just re-read their content
                if (option.IsInformational)
                {
                    MelonLogger.Msg($"Re-reading informational item: {option.Label.Substring(0, Math.Min(50, option.Label.Length))}...");
                    TISpeechMod.Speak(option.Label, interrupt: true);
                    return;
                }

                if (option.Button == null)
                {
                    MelonLogger.Msg("Cannot activate: option has no button");
                    return;
                }

                if (!option.Button.interactable)
                {
                    MelonLogger.Msg($"Button '{option.Label}' is not interactable");
                    TISpeechMod.Speak($"{option.Label} is not available", interrupt: true);
                    return;
                }

                MelonLogger.Msg($"Activating special prompt option: {option.Label}");
                option.Button.onClick.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating special prompt option: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the announcement text for entering special prompt mode.
        /// </summary>
        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append(panelTitle);
            sb.Append(". ");

            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " item" : " items");

            if (Options.Count > 0)
            {
                sb.Append(". 1 of ");
                sb.Append(Options.Count);
                sb.Append(": ");
                sb.Append(Options[0].Label);
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
        /// Detect which special prompt type is currently active.
        /// Returns None if no special prompt is active.
        /// </summary>
        public static SpecialPromptType DetectActivePromptType(NotificationScreenController controller)
        {
            if (controller == null)
                return SpecialPromptType.None;

            // Check in order of specificity
            if (controller.removeArmiesPromptObject != null &&
                controller.removeArmiesPromptObject.activeInHierarchy)
            {
                return SpecialPromptType.RemoveArmies;
            }

            if (controller.callAllyResponseObject != null &&
                controller.callAllyResponseObject.activeInHierarchy)
            {
                return SpecialPromptType.CallToWar;
            }

            if (controller.responsePanelObject != null &&
                controller.responsePanelObject.activeInHierarchy)
            {
                return SpecialPromptType.DiplomaticResponse;
            }

            return SpecialPromptType.None;
        }
    }
}
