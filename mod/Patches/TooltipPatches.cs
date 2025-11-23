using System;
using HarmonyLib;
using MelonLoader;
using ModelShark;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for the ModelShark tooltip system
    /// Intercepts tooltip display events to announce tooltip content via screen reader
    /// </summary>
    [HarmonyPatch]
    public class TooltipPatches
    {
        private static string lastTooltipText = "";
        private static float lastTooltipTime = 0f;
        private const float TOOLTIP_DEBOUNCE_TIME = 0.2f; // Prevent rapid re-announcement

        /// <summary>
        /// Patch TooltipManager.SetTextAndSize to announce tooltips AFTER text has been populated
        /// This is called after placeholders like %BodyText% are replaced with actual content
        /// </summary>
        [HarmonyPatch(typeof(TooltipManager), "SetTextAndSize")]
        [HarmonyPostfix]
        public static void SetTextAndSize_Postfix(TooltipTrigger trigger)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get tooltip text from the trigger - text has now been populated
                string tooltipText = ExtractTooltipText(trigger);

                if (!string.IsNullOrEmpty(tooltipText))
                {
                    AnnounceTooltip(tooltipText);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TooltipManager.SetTextAndSize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch TooltipTrigger.OnPointerExit to stop speaking when tooltip disappears
        /// </summary>
        [HarmonyPatch(typeof(TooltipTrigger), "OnPointerExit")]
        [HarmonyPostfix]
        public static void OnPointerExit_Postfix(TooltipTrigger __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Clear the last tooltip when mouse leaves
                lastTooltipText = "";
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TooltipTrigger.OnPointerExit patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract tooltip text from a TooltipTrigger component
        /// </summary>
        private static string ExtractTooltipText(TooltipTrigger trigger)
        {
            try
            {
                // Get the Tooltip object from the trigger
                var tooltipProperty = AccessTools.Property(typeof(TooltipTrigger), "Tooltip");
                if (tooltipProperty != null)
                {
                    var tooltip = tooltipProperty.GetValue(trigger);
                    if (tooltip != null)
                    {
                        // Get the TextFields list from the Tooltip
                        var textFieldsProperty = AccessTools.Property(tooltip.GetType(), "TextFields");
                        if (textFieldsProperty != null)
                        {
                            var textFields = textFieldsProperty.GetValue(tooltip) as System.Collections.IList;
                            if (textFields != null && textFields.Count > 0)
                            {
                                var sb = new System.Text.StringBuilder();

                                // Concatenate all text fields
                                foreach (var textField in textFields)
                                {
                                    var textProperty = AccessTools.Property(textField.GetType(), "Text");
                                    if (textProperty != null)
                                    {
                                        var tmpText = textProperty.GetValue(textField);
                                        if (tmpText != null)
                                        {
                                            var textContentField = AccessTools.Property(tmpText.GetType(), "text");
                                            if (textContentField != null)
                                            {
                                                string text = textContentField.GetValue(tmpText) as string;
                                                if (!string.IsNullOrEmpty(text))
                                                {
                                                    sb.AppendLine(text);
                                                }
                                            }
                                        }
                                    }
                                }

                                string result = sb.ToString().Trim();
                                if (!string.IsNullOrEmpty(result))
                                {
                                    return TISpeechMod.CleanText(result);
                                }
                            }
                        }
                    }
                }

                // Fallback: try to get text from the GameObject name
                if (trigger.gameObject != null)
                {
                    string objectName = trigger.gameObject.name;
                    if (!string.IsNullOrEmpty(objectName))
                    {
                        return CleanObjectName(objectName);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error extracting tooltip text: {ex.Message}");
            }

            return "";
        }


        /// <summary>
        /// Clean up GameObject names for announcement
        /// Convert PascalCase and remove common suffixes
        /// </summary>
        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            // Remove common Unity object suffixes
            name = name.Replace("(Clone)", "")
                       .Replace("GameObject", "")
                       .Replace("Button", "")
                       .Replace("Text", "")
                       .Trim();

            // Add spaces before capital letters (PascalCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        /// <summary>
        /// Announce tooltip text via screen reader with debouncing
        /// </summary>
        private static void AnnounceTooltip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            float currentTime = Time.unscaledTime;

            // Debounce: don't re-announce the same tooltip within a short time window
            if (text == lastTooltipText && (currentTime - lastTooltipTime) < TOOLTIP_DEBOUNCE_TIME)
                return;

            lastTooltipText = text;
            lastTooltipTime = currentTime;

            // Speak the tooltip with interruption to replace previous announcements
            TISpeechMod.Speak(text, interrupt: true);

            MelonLogger.Msg($"Announced tooltip: {text}");
        }
    }
}
