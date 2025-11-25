using System;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for the national priorities system
    /// Adds hover announcements for control point priority weights and helper values
    /// </summary>
    [HarmonyPatch]
    public class PriorityPatches
    {
        // Priority weight names
        private static readonly string[] WeightNames = new string[]
        {
            "None",
            "Low",
            "Medium",
            "High"
        };

        private static string lastPriorityText = "";
        private static float lastPriorityTime = 0f;
        private const float PRIORITY_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch PriorityListItemController.SetListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(PriorityListItemController), "SetListItem")]
        [HarmonyPostfix]
        public static void SetListItem_Postfix(PriorityListItemController __instance, TINationState nation, PriorityType priority)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the priority name
                string priorityName = GetPriorityName(priority);

                // Add hover handlers to each control point weight button
                for (int i = 0; i <= nation.maxControlPointIndex; i++)
                {
                    TIControlPoint controlPoint = nation.GetControlPoint(i);
                    if (controlPoint != null)
                    {
                        AddControlPointWeightHoverHandler(__instance.priorityButton[i], __instance.controlPointWeight_PH[i],
                            priorityName, i, controlPoint, priority);
                    }
                }

                // Add hover handler to the helper value column
                AddHelperValueHoverHandler(__instance.helperValue, priorityName, __instance);

                MelonLogger.Msg($"Added priority hover handlers for: {priorityName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PriorityListItemController.SetListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover event handler to a control point weight button
        /// </summary>
        private static void AddControlPointWeightHoverHandler(Button button, Image weightImage, string priorityName,
            int controlPointIndex, TIControlPoint controlPoint, PriorityType priority)
        {
            try
            {
                if (button == null)
                    return;

                // Add event trigger to the button's game object
                EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Only remove existing PointerEnter triggers to avoid duplicates
                    // Don't clear all triggers - game may have click handlers we need to preserve
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                // Capture variables for the closure
                string capturedPriorityName = priorityName;
                int capturedIndex = controlPointIndex;
                TIControlPoint capturedCP = controlPoint;
                PriorityType capturedPriority = priority;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnControlPointWeightHover(capturedPriorityName, capturedIndex, capturedCP, capturedPriority));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to control point weight button: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a control point weight button
        /// </summary>
        private static void OnControlPointWeightHover(string priorityName, int controlPointIndex, TIControlPoint controlPoint, PriorityType priority)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the priority weight (0-3)
                int weight = controlPoint.GetControlPointPriority(priority, checkValid: false);

                // Get the weight name
                string weightName = WeightNames[weight];

                // Build the announcement: "{Priority Name}, Control Point {#}: {Weight}"
                string announcement = $"{priorityName}, Control Point {controlPointIndex + 1}: {weightName}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastPriorityText && (currentTime - lastPriorityTime) < PRIORITY_DEBOUNCE_TIME)
                    return;

                lastPriorityText = announcement;
                lastPriorityTime = currentTime;

                // Announce the control point weight
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Priority weight hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in control point weight hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover event handler to the helper value column
        /// </summary>
        private static void AddHelperValueHoverHandler(TMP_Text helperValue, string priorityName, PriorityListItemController controller)
        {
            try
            {
                if (helperValue == null)
                    return;

                // Add event trigger to the text's game object
                EventTrigger trigger = helperValue.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = helperValue.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Only remove existing PointerEnter triggers to avoid duplicates
                    // Don't clear all triggers - game may have click handlers we need to preserve
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                // Capture variables for the closure
                string capturedPriorityName = priorityName;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnHelperValueHover(helperValue, capturedPriorityName, controller));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to helper value: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over the helper value column
        /// </summary>
        private static void OnHelperValueHover(TMP_Text helperValue, string priorityName, PriorityListItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the value text
                string valueText = helperValue?.text ?? "";
                if (string.IsNullOrWhiteSpace(valueText))
                    return;

                // Clean the value text
                valueText = TISpeechMod.CleanText(valueText);

                if (string.IsNullOrWhiteSpace(valueText))
                    return;

                // Get the column name based on the column setting
                // Access the controller's parent NationInfoController to get proportionColumnSetting
                string columnName = GetHelperColumnName(controller);

                // Build the announcement: "{Priority Name}, {Column Name}: {Value}"
                string announcement = $"{priorityName}, {columnName}: {valueText}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastPriorityText && (currentTime - lastPriorityTime) < PRIORITY_DEBOUNCE_TIME)
                    return;

                lastPriorityText = announcement;
                lastPriorityTime = currentTime;

                // Announce the helper value
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Priority helper value hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in helper value hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the helper column name based on the controller's column setting
        /// </summary>
        private static string GetHelperColumnName(PriorityListItemController controller)
        {
            try
            {
                // Try to get the parent NationInfoController
                var controllerField = AccessTools.Field(typeof(PriorityListItemController), "controller");
                if (controllerField != null)
                {
                    var nationController = controllerField.GetValue(controller) as NationInfoController;
                    if (nationController != null)
                    {
                        var proportionField = AccessTools.Field(typeof(NationInfoController), "proportionColumnSetting");
                        if (proportionField != null)
                        {
                            int setting = (int)proportionField.GetValue(nationController);
                            switch (setting)
                            {
                                case 0:
                                    return "Percentage";
                                case 1:
                                    return "IP per Year";
                                case 2:
                                    return "Bonuses";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting helper column name: {ex.Message}");
            }

            return "Value";
        }

        /// <summary>
        /// Get human-readable priority name
        /// </summary>
        private static string GetPriorityName(PriorityType priority)
        {
            switch (priority)
            {
                case PriorityType.Economy:
                    return "Economy";
                case PriorityType.Welfare:
                    return "Welfare";
                case PriorityType.Environment:
                    return "Environment";
                case PriorityType.Knowledge:
                    return "Knowledge";
                case PriorityType.Government:
                    return "Government";
                case PriorityType.Unity:
                    return "Unity";
                case PriorityType.Oppression:
                    return "Oppression";
                case PriorityType.Funding:
                    return "Funding";
                case PriorityType.Spoils:
                    return "Spoils";
                case PriorityType.Military:
                    return "Military";
                case PriorityType.Military_BuildArmy:
                    return "Build Army";
                case PriorityType.Military_BuildNavy:
                    return "Build Navy";
                case PriorityType.Military_FoundMilitary:
                    return "Found Military";
                case PriorityType.Military_InitiateNuclearProgram:
                    return "Initiate Nuclear Program";
                case PriorityType.Military_BuildNuclearWeapons:
                    return "Build Nuclear Weapons";
                case PriorityType.Military_BuildSpaceDefenses:
                    return "Build Space Defenses";
                case PriorityType.Military_BuildSTOSquadron:
                    return "Build STO Squadron";
                case PriorityType.Civilian_InitiateSpaceflightProgram:
                    return "Initiate Spaceflight Program";
                case PriorityType.LaunchFacilities:
                    return "Launch Facilities";
                case PriorityType.MissionControl:
                    return "Mission Control";
                default:
                    return priority.ToString();
            }
        }

        /// <summary>
        /// Clean text by removing TextMeshPro and HTML tags
        /// </summary>
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Remove TextMeshPro and HTML tags
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Remove multiple spaces and trim
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }
    }
}
