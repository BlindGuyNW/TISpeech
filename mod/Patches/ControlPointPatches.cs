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
    /// Harmony patches for control point displays
    /// Adds hover announcements for status indicators and contextual information
    /// </summary>
    [HarmonyPatch]
    public class ControlPointPatches
    {
        private static string lastControlPointText = "";
        private static float lastControlPointTime = 0f;
        private const float CONTROL_POINT_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch ControlPointGridItemController.SetGridItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(ControlPointGridItemController), "SetGridItem")]
        [HarmonyPostfix]
        public static void SetGridItem_Postfix(ControlPointGridItemController __instance, NationInfoController controller,
            TINationState nationState, TIControlPoint controlPoint, Image flagImage)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get control point information
                string cpName = GetControlPointName(controlPoint);

                // Add hover handlers to status indicators
                AddStatusIndicatorHover(__instance.crackdownStatusPanel, cpName, "Under Crackdown - Benefits Disabled");
                AddStatusIndicatorHover(__instance.defendStatusPanel, cpName, "Defended");
                AddStatusIndicatorHover(__instance.executiveStatusPanel, cpName, "Executive Control Point");

                // Add hover handler to army count
                if (__instance.armyCount != null)
                    AddArmyCountHover(__instance.armyCount, cpName);

                // Add hover handler to success chance text
                if (__instance.toHitText != null)
                    AddSuccessChanceHover(__instance.toHitText, cpName);

                MelonLogger.Msg($"Added control point hover handlers for: {cpName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ControlPointGridItemController.SetGridItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a status indicator image
        /// </summary>
        private static void AddStatusIndicatorHover(Image statusImage, string cpName, string statusMessage)
        {
            try
            {
                if (statusImage == null)
                    return;

                // Only add handler if the image is visible
                if (!statusImage.enabled)
                    return;

                EventTrigger trigger = statusImage.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = statusImage.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.Clear();
                }

                // Capture variables for the closure
                string capturedCpName = cpName;
                string capturedStatus = statusMessage;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnStatusIndicatorHover(capturedCpName, capturedStatus));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding status indicator hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a status indicator
        /// </summary>
        private static void OnStatusIndicatorHover(string cpName, string statusMessage)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string announcement = $"{cpName}: {statusMessage}";

                float currentTime = Time.unscaledTime;
                if (announcement == lastControlPointText && (currentTime - lastControlPointTime) < CONTROL_POINT_DEBOUNCE_TIME)
                    return;

                lastControlPointText = announcement;
                lastControlPointTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Control point status hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in status indicator hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to army count text
        /// </summary>
        private static void AddArmyCountHover(TMP_Text armyCount, string cpName)
        {
            try
            {
                if (armyCount == null || !armyCount.enabled)
                    return;

                EventTrigger trigger = armyCount.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = armyCount.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.Clear();
                }

                string capturedCpName = cpName;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnArmyCountHover(armyCount, capturedCpName));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding army count hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over army count
        /// </summary>
        private static void OnArmyCountHover(TMP_Text armyCount, string cpName)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string countText = armyCount?.text ?? "";
                if (string.IsNullOrWhiteSpace(countText))
                    return;

                string announcement = $"{cpName}: {countText} Armies";

                float currentTime = Time.unscaledTime;
                if (announcement == lastControlPointText && (currentTime - lastControlPointTime) < CONTROL_POINT_DEBOUNCE_TIME)
                    return;

                lastControlPointText = announcement;
                lastControlPointTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Army count hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in army count hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to success chance text
        /// </summary>
        private static void AddSuccessChanceHover(TMP_Text toHitText, string cpName)
        {
            try
            {
                if (toHitText == null || !toHitText.enabled)
                    return;

                EventTrigger trigger = toHitText.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = toHitText.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.Clear();
                }

                string capturedCpName = cpName;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnSuccessChanceHover(toHitText, capturedCpName));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding success chance hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over success chance text
        /// </summary>
        private static void OnSuccessChanceHover(TMP_Text toHitText, string cpName)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string chanceText = toHitText?.text ?? "";
                if (string.IsNullOrWhiteSpace(chanceText))
                    return;

                string announcement = $"{cpName}: Success Chance {chanceText}";

                float currentTime = Time.unscaledTime;
                if (announcement == lastControlPointText && (currentTime - lastControlPointTime) < CONTROL_POINT_DEBOUNCE_TIME)
                    return;

                lastControlPointText = announcement;
                lastControlPointTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Success chance hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in success chance hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a human-readable name for the control point
        /// </summary>
        private static string GetControlPointName(TIControlPoint controlPoint)
        {
            try
            {
                // Try to get the control point type display name
                var displayNameProp = AccessTools.Property(typeof(TIControlPoint), "controlPointTypeDisplayName");
                if (displayNameProp != null)
                {
                    string displayName = displayNameProp.GetValue(controlPoint) as string;
                    if (!string.IsNullOrEmpty(displayName))
                        return displayName;
                }

                // Fallback to position-based naming
                if (controlPoint.executive)
                    return "Executive Control Point";

                return $"Control Point {controlPoint.positionInNation + 1}";
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting control point name: {ex.Message}");
                return "Control Point";
            }
        }
    }
}
