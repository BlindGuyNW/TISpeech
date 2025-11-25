using System;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for the research screen
    /// - Adds announcements when cycling research slot priorities
    /// - Adds hover handlers to research panel text elements for accessibility
    /// </summary>
    [HarmonyPatch]
    public class ResearchPriorityPatches
    {
        // Priority weight names (same as nation priorities)
        private static readonly string[] WeightNames = new string[]
        {
            "None",
            "Low",
            "Medium",
            "High"
        };

        private static string lastResearchPriorityText = "";
        private static float lastResearchPriorityTime = 0f;
        private const float RESEARCH_PRIORITY_DEBOUNCE_TIME = 0.3f;

        private static string lastResearchPanelText = "";
        private static float lastResearchPanelTime = 0f;
        private const float RESEARCH_PANEL_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch OnPriorityButtonClicked to announce priority after cycling forward
        /// </summary>
        [HarmonyPatch(typeof(ResearchPanelController), "OnPriorityButtonClicked")]
        [HarmonyPostfix]
        public static void OnPriorityButtonClicked_Postfix(ResearchPanelController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                AnnounceResearchPriority(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ResearchPanelController.OnPriorityButtonClicked patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OnRightPriorityButtonClicked to announce priority after cycling backward
        /// </summary>
        [HarmonyPatch(typeof(ResearchPanelController), "OnRightPriorityButtonClicked")]
        [HarmonyPostfix]
        public static void OnRightPriorityButtonClicked_Postfix(ResearchPanelController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                AnnounceResearchPriority(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ResearchPanelController.OnRightPriorityButtonClicked patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OnEnable to add hover handlers to text elements when panel is enabled
        /// OnEnable is called when the panel is activated, which is safe (no TIUtilities references)
        /// </summary>
        [HarmonyPatch(typeof(ResearchPanelController), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable_Postfix(ResearchPanelController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                AddResearchPanelEventHandlers(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ResearchPanelController.OnEnable patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce the current research priority for a research slot
        /// </summary>
        private static void AnnounceResearchPriority(ResearchPanelController panel)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the slot number (0-2 for techs, 3-5 for projects)
                int slot = panel.slot;

                // Get the controller to access the active player
                var controllerField = AccessTools.Field(typeof(ResearchPanelController), "controller");
                if (controllerField == null)
                {
                    MelonLogger.Warning("Could not find ResearchScreenController field");
                    return;
                }

                var controller = controllerField.GetValue(panel) as ResearchScreenController;
                if (controller == null || controller.activePlayer == null)
                {
                    MelonLogger.Warning("ResearchScreenController or activePlayer is null");
                    return;
                }

                // Get the current priority from the active player's research weights
                int priority = controller.activePlayer.researchWeights[slot];

                // Clamp priority to valid range
                if (priority < 0 || priority > 3)
                {
                    MelonLogger.Warning($"Invalid priority value: {priority}");
                    return;
                }

                string priorityName = WeightNames[priority];

                // Get the tech/project name
                string itemName = "";
                if (panel.projectName != null && !string.IsNullOrEmpty(panel.projectName.text))
                {
                    itemName = TISpeechMod.CleanText(panel.projectName.text);
                }

                // Determine if this is a tech or project slot
                string slotType = (slot <= 2) ? "Tech" : "Project";
                int slotNumber = (slot <= 2) ? (slot + 1) : (slot - 2); // Tech 1-3, Project 1-3

                // Build the announcement
                string announcement;
                if (!string.IsNullOrEmpty(itemName))
                {
                    announcement = $"{itemName}, Priority: {priorityName}";
                }
                else
                {
                    announcement = $"{slotType} Slot {slotNumber}, Priority: {priorityName}";
                }

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastResearchPriorityText && (currentTime - lastResearchPriorityTime) < RESEARCH_PRIORITY_DEBOUNCE_TIME)
                    return;

                lastResearchPriorityText = announcement;
                lastResearchPriorityTime = currentTime;

                // Announce the priority
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Research priority announced: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing research priority: {ex.Message}");
            }
        }

        /// <summary>
        /// Add event handlers to research panel text elements for hover announcements
        /// </summary>
        private static void AddResearchPanelEventHandlers(ResearchPanelController panel)
        {
            try
            {
                if (!TISpeechMod.IsReady || panel == null)
                    return;

                // Get slot info for context
                int slot = panel.slot;
                string slotType = (slot <= 2) ? "Tech" : "Project";
                int slotNumber = (slot <= 2) ? (slot + 1) : (slot - 2);
                string slotContext = $"{slotType} Slot {slotNumber}";

                // Add hover to project name
                if (panel.projectName != null)
                {
                    AddTextHoverHandler(panel.projectName, slotContext, "Name");
                }

                // Add hover to summary/description
                if (panel.summary != null)
                {
                    AddTextHoverHandler(panel.summary, slotContext, "Description");
                }

                // Add hover to progress fraction
                if (panel.progressFraction != null)
                {
                    AddTextHoverHandler(panel.progressFraction, slotContext, "Progress");
                }

                // Add hover to completion date
                if (panel.completionDate != null && panel.completionDate.enabled)
                {
                    AddTextHoverHandler(panel.completionDate, slotContext, "Completion");
                }

                // Add hover to contribution weight percentage
                if (panel.ContributionWeightPercentageText != null)
                {
                    AddTextHoverHandler(panel.ContributionWeightPercentageText, slotContext, "Daily Contribution");
                }

                // Add hover to tech category bonus (the percentage shown below the tech category icon)
                if (panel.techCategoryBonus != null)
                {
                    AddTextHoverHandler(panel.techCategoryBonus, slotContext, "Tech Category Bonus");
                }

                // Add hover to research type bonus (the percentage shown next to the faction icon/leader indicator)
                if (panel.researchTypeBonus != null)
                {
                    AddTextHoverHandler(panel.researchTypeBonus, slotContext, "Research Type Bonus");
                }

                MelonLogger.Msg($"Added research panel event handlers to {slotContext}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding research panel event handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a hover event handler to a text field
        /// </summary>
        private static void AddTextHoverHandler(TMP_Text textField, string slotContext, string fieldLabel)
        {
            try
            {
                if (textField == null || textField.gameObject == null)
                    return;

                // Check if we already added an event trigger to this text field
                EventTrigger trigger = textField.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = textField.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Only remove existing PointerEnter triggers to avoid duplicates
                    // Don't clear all triggers - game may have click handlers we need to preserve
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                // Capture variables for the closure
                string capturedSlotContext = slotContext;
                string capturedFieldLabel = fieldLabel;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnResearchPanelTextHover(textField, capturedSlotContext, capturedFieldLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to {fieldLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a research panel text field
        /// </summary>
        private static void OnResearchPanelTextHover(TMP_Text textField, string slotContext, string fieldLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textField == null)
                    return;

                // Get the text value and clean it
                string value = textField.text;
                if (string.IsNullOrWhiteSpace(value))
                    return;

                value = TISpeechMod.CleanText(value);
                if (string.IsNullOrWhiteSpace(value))
                    return;

                // Build the announcement: "{Slot Context}, {Field Label}: {Value}"
                string announcement = $"{slotContext}, {fieldLabel}: {value}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastResearchPanelText && (currentTime - lastResearchPanelTime) < RESEARCH_PANEL_DEBOUNCE_TIME)
                    return;

                lastResearchPanelText = announcement;
                lastResearchPanelTime = currentTime;

                // Announce with interrupt: false so tooltips take priority
                TISpeechMod.Speak(announcement, interrupt: false);

                MelonLogger.Msg($"Research panel text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in research panel text hover handler: {ex.Message}");
            }
        }
    }
}
