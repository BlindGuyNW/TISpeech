using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Represents a single navigable target in the mission target selection UI.
    /// Used for Sabotage Project and Steal Project missions.
    /// </summary>
    public class MissionTargetOption
    {
        public string Label { get; set; }
        public string DetailText { get; set; }
        public MissionTargetListItemController Controller { get; set; }
        public TIProjectTemplate Project { get; set; }
    }

    /// <summary>
    /// Sub-mode for navigating mission target selection UI.
    /// Activated when a Sabotage Project or Steal Project mission succeeds
    /// and the player must choose which project to target.
    /// </summary>
    public class MissionTargetSubMode
    {
        public List<MissionTargetOption> Options { get; private set; }
        public int CurrentIndex { get; private set; }

        private NotificationScreenController controller;
        private string headerText;
        private string promptType;

        public int Count => Options.Count;
        public MissionTargetOption CurrentOption => Options.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Options.Count
            ? Options[CurrentIndex]
            : null;

        public MissionTargetSubMode(NotificationScreenController controller, string promptType)
        {
            this.controller = controller;
            this.promptType = promptType;
            Options = new List<MissionTargetOption>();
            CurrentIndex = 0;

            try
            {
                // Extract header text
                headerText = controller.missionTargetingUIHeaderText?.text ?? "";
                headerText = TISpeechMod.CleanText(headerText);

                // Extract targets from the list
                BuildTargetOptions();

                MelonLogger.Msg($"MissionTargetSubMode created with {Options.Count} options for {promptType}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating MissionTargetSubMode: {ex.Message}");
            }
        }

        private void BuildTargetOptions()
        {
            if (controller.missionTargetingUIList == null)
            {
                MelonLogger.Error("missionTargetingUIList is null");
                return;
            }

            // Iterate through the list items
            foreach (object item in controller.missionTargetingUIList)
            {
                var targetController = item as MissionTargetListItemController;
                if (targetController == null)
                    continue;

                // Extract label from targetText
                string label = "";
                if (targetController.targetText != null)
                {
                    label = TISpeechMod.CleanText(targetController.targetText.text);
                }

                // Extract detail from tooltip
                string detail = ExtractTooltipText(targetController.targetTooltip);

                if (string.IsNullOrEmpty(label))
                {
                    label = "Unknown project";
                }

                // Get the project via reflection (it's a private field)
                TIProjectTemplate project = null;
                try
                {
                    var projectField = typeof(MissionTargetListItemController).GetField("project",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (projectField != null)
                    {
                        project = projectField.GetValue(targetController) as TIProjectTemplate;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not get project field: {ex.Message}");
                }

                Options.Add(new MissionTargetOption
                {
                    Label = label,
                    DetailText = detail,
                    Controller = targetController,
                    Project = project
                });

                MelonLogger.Msg($"Added target option: {label}");
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

        /// <summary>
        /// Select the current target. This simulates clicking on the list item.
        /// </summary>
        public void SelectCurrent()
        {
            try
            {
                var option = CurrentOption;
                if (option == null || option.Controller == null)
                {
                    MelonLogger.Msg("Cannot select: no option or controller");
                    return;
                }

                // Call OnClicked on the controller to select this target
                option.Controller.OnClicked();
                MelonLogger.Msg($"Selected target: {option.Label}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting target: {ex.Message}");
            }
        }

        /// <summary>
        /// Confirm the current selection. This clicks the confirm button.
        /// </summary>
        /// <returns>True if confirmation succeeded, false otherwise.</returns>
        public bool Confirm()
        {
            try
            {
                if (controller.missionTargetButton == null)
                {
                    MelonLogger.Error("Mission target confirm button is null");
                    return false;
                }

                if (!controller.missionTargetButton.interactable)
                {
                    MelonLogger.Msg("Mission target confirm button is not interactable - select a target first");
                    TISpeechMod.Speak("Select a target first by pressing Enter on a project", interrupt: true);
                    return false;
                }

                controller.missionTargetButton.onClick.Invoke();
                MelonLogger.Msg("Confirmed mission target selection");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error confirming target: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancel the selection. This aborts the mission.
        /// </summary>
        public void Cancel()
        {
            try
            {
                controller.OnClickMissionTargetCancel();
                MelonLogger.Msg("Cancelled mission target selection");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cancelling target selection: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a target has been selected (confirm button is interactable).
        /// </summary>
        public bool HasSelection => controller?.missionTargetButton?.interactable == true;

        /// <summary>
        /// Get the announcement text for entering mission target mode.
        /// </summary>
        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();

            string missionType = promptType == "PromptSabotageProject" ? "Sabotage Project" : "Steal Project";
            sb.Append(missionType);
            sb.Append(" selection. ");

            if (!string.IsNullOrEmpty(headerText))
            {
                sb.Append(headerText);
                sb.Append(". ");
            }

            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " target" : " targets");
            sb.Append(" available. ");

            if (Options.Count > 0)
            {
                sb.Append("1 of ");
                sb.Append(Options.Count);
                sb.Append(": ");
                sb.Append(Options[0].Label);
            }

            sb.Append(". Navigate with arrows, Enter to select, plus to confirm, Escape to cancel.");

            return sb.ToString();
        }

        /// <summary>
        /// Get the announcement for the current option.
        /// </summary>
        public string GetCurrentAnnouncement()
        {
            if (Options.Count == 0)
                return "No targets available";

            var option = CurrentOption;
            if (option == null)
                return "No target selected";

            return $"{option.Label}, {CurrentIndex + 1} of {Options.Count}";
        }

        /// <summary>
        /// Get the detail text for the current option (for Numpad *).
        /// Uses the stored TIProjectTemplate to provide comprehensive project information.
        /// </summary>
        public string GetCurrentDetail()
        {
            var option = CurrentOption;
            if (option == null)
                return "No target selected";

            // If we have the project template, build detailed info from it
            if (option.Project != null)
            {
                return BuildProjectDetailText(option.Project);
            }

            // Fallback to tooltip-extracted detail if available
            if (!string.IsNullOrEmpty(option.DetailText))
            {
                return $"{option.Label}: {option.DetailText}";
            }

            return option.Label;
        }

        /// <summary>
        /// Build detailed text from a TIProjectTemplate.
        /// </summary>
        private string BuildProjectDetailText(TIProjectTemplate project)
        {
            var sb = new StringBuilder();
            sb.Append(project.displayName);

            // Category
            string category = FormatCategory(project.techCategory);
            sb.Append($". Category: {category}");

            // Summary/description
            try
            {
                string summary = project.summary;
                if (!string.IsNullOrEmpty(summary) && summary != project.displayName)
                {
                    sb.Append($". {TISpeechMod.CleanText(summary)}");
                }
            }
            catch { /* Summary may throw if localization fails */ }

            // Repeatable
            if (project.repeatable)
                sb.Append(". Repeatable");

            // One-time globally (unique project)
            if (project.oneTimeGlobally)
                sb.Append(". Unique project, one-time globally");

            // Org granted
            if (!string.IsNullOrEmpty(project.orgGranted))
            {
                try
                {
                    var orgTemplate = TemplateManager.Find<TIOrgTemplate>(project.orgGranted);
                    sb.Append($". Grants org: {orgTemplate?.displayName ?? project.orgGranted}");
                }
                catch
                {
                    sb.Append($". Grants org: {project.orgGranted}");
                }
            }

            // Resources granted
            if (project.resourcesGranted != null && project.resourcesGranted.Count > 0)
            {
                var resources = project.resourcesGranted
                    .Where(r => r.resource != FactionResource.None && r.value != 0)
                    .Select(r => $"{r.resource}: {r.value:+#;-#;0}");
                var resourceStr = string.Join(", ", resources);
                if (!string.IsNullOrEmpty(resourceStr))
                    sb.Append($". Grants: {resourceStr}");
            }

            // Ship parts unlocked
            try
            {
                var shipParts = project.ShipPartUnlocks;
                if (shipParts != null && shipParts.Count > 0)
                {
                    var partNames = shipParts.Select(p => p.displayName);
                    sb.Append($". Unlocks ship parts: {string.Join(", ", partNames)}");
                }
            }
            catch { /* May fail if templates not loaded */ }

            // Hab modules unlocked
            try
            {
                var habModules = project.HabModuleUnlocks();
                if (habModules != null && habModules.Count > 0)
                {
                    var moduleNames = habModules.Select(m => m.displayName);
                    sb.Append($". Unlocks hab modules: {string.Join(", ", moduleNames)}");
                }
            }
            catch { /* May fail if templates not loaded */ }

            return sb.ToString();
        }

        private string FormatCategory(TechCategory category)
        {
            return category switch
            {
                TechCategory.Materials => "Materials",
                TechCategory.SpaceScience => "Space Science",
                TechCategory.Energy => "Energy",
                TechCategory.LifeScience => "Life Science",
                TechCategory.MilitaryScience => "Military Science",
                TechCategory.InformationScience => "Information Science",
                TechCategory.SocialScience => "Social Science",
                TechCategory.Xenology => "Xenology",
                _ => category.ToString()
            };
        }

        /// <summary>
        /// List all options (for Numpad /).
        /// </summary>
        public string ListAllOptions()
        {
            if (Options.Count == 0)
                return "No targets available";

            var sb = new StringBuilder();
            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " target: " : " targets: ");

            for (int i = 0; i < Options.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Options[i].Label);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Find the next option starting with the given letter after the current index.
        /// Returns -1 if no option found.
        /// </summary>
        public int FindNextByLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);

            // Search from current index + 1 to end
            for (int i = CurrentIndex + 1; i < Options.Count; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            // Wrap around: search from 0 to current index
            for (int i = 0; i <= CurrentIndex; i++)
            {
                string label = Options[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Jump to a specific index.
        /// </summary>
        public void SetIndex(int index)
        {
            if (index >= 0 && index < Options.Count)
                CurrentIndex = index;
        }
    }
}
