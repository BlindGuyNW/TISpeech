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
    /// Harmony patches for the nation view to provide context for resource columns
    /// Adds hover announcements for nation/council/federation value columns
    /// </summary>
    [HarmonyPatch]
    public class NationViewPatches
    {
        private static string lastNationValueText = "";
        private static float lastNationValueTime = 0f;
        private const float NATION_VALUE_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch the nation info controller update method to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(NationInfoController), "UpdatePrimaryDisplayElements")]
        [HarmonyPostfix]
        public static void UpdatePrimaryDisplayElements_Postfix(NationInfoController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Add event handlers to resource value text fields
                AddResourceValueHoverHandler(__instance.investmentNationValue, "Investment", "Nation");
                AddResourceValueHoverHandler(__instance.investmentCouncilValue, "Investment", "Council Share");

                AddResourceValueHoverHandler(__instance.spaceFundingNationValue, "Space Funding", "Nation");
                if (__instance.spaceFundingFederationValue != null)
                    AddResourceValueHoverHandler(__instance.spaceFundingFederationValue, "Space Funding", "Federation");
                AddResourceValueHoverHandler(__instance.spaceFundingCouncilValue, "Space Funding", "Council Share");

                AddResourceValueHoverHandler(__instance.scienceNationValue, "Science", "Nation");
                AddResourceValueHoverHandler(__instance.scienceCouncilValue, "Science", "Council Share");

                AddResourceValueHoverHandler(__instance.boostNationValue, "Boost", "Nation");
                if (__instance.boostFederationValue != null)
                    AddResourceValueHoverHandler(__instance.boostFederationValue, "Boost", "Federation");
                AddResourceValueHoverHandler(__instance.boostCouncilValue, "Boost", "Council Share");

                AddResourceValueHoverHandler(__instance.missionControlNationValue, "Mission Control", "Nation");
                AddResourceValueHoverHandler(__instance.missionControlCouncilValue, "Mission Control", "Council Share");

                MelonLogger.Msg("Added nation view resource value hover handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NationInfoController.RefreshInfoCanvas patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover event handler to a resource value text field
        /// </summary>
        private static void AddResourceValueHoverHandler(TMP_Text textField, string resourceName, string columnName)
        {
            try
            {
                if (textField == null)
                    return;

                // Check if we already added an event trigger to this field
                EventTrigger trigger = textField.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = textField.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Clear existing triggers to avoid duplicates
                    trigger.triggers.Clear();
                }

                // Capture variables for the closure
                string capturedResourceName = resourceName;
                string capturedColumnName = columnName;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnResourceValueHover(textField, capturedResourceName, capturedColumnName));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to {resourceName} {columnName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a resource value field
        /// </summary>
        private static void OnResourceValueHover(TMP_Text textField, string resourceName, string columnName)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the value text
                string valueText = textField?.text ?? "";
                if (string.IsNullOrWhiteSpace(valueText))
                    return;

                // Clean the value text (remove TextMeshPro and HTML tags)
                valueText = TISpeechMod.CleanText(valueText);

                if (string.IsNullOrWhiteSpace(valueText))
                    return;

                // Build the announcement: "{Resource Name}, {Column Name}: {Value}"
                string announcement = $"{resourceName}, {columnName}: {valueText}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastNationValueText && (currentTime - lastNationValueTime) < NATION_VALUE_DEBOUNCE_TIME)
                    return;

                lastNationValueText = announcement;
                lastNationValueTime = currentTime;

                // Announce the value with context
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Nation view value hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in resource value hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean text by removing TextMeshPro and HTML tags
        /// Also removes arrow symbols that indicate trends
        /// </summary>
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Remove TextMeshPro and HTML tags
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Remove arrow symbols (↑↓→) that indicate trends
            text = text.Replace("↑", " up");
            text = text.Replace("↓", " down");
            text = text.Replace("→", " stable");
            text = text.Replace("▲", " up");
            text = text.Replace("▼", " down");
            text = text.Replace("►", " stable");

            // Remove multiple spaces and trim
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }
    }
}
