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
    /// Patches for the mission selection panel to make mission details accessible
    /// Adds hover handlers to non-interactive text fields showing mission info and sliders
    /// </summary>
    [HarmonyPatch]
    public class MissionPanelPatches
    {
        private static float lastSliderValue = -1f;
        private static float lastSliderAnnounceTime = 0f;
        private const float SLIDER_ANNOUNCE_DELAY = 0.5f; // Only announce every 0.5 seconds while dragging

        /// <summary>
        /// Patch UpdateAllMissionData to add hover handlers to mission detail text fields
        /// This is called after a mission is selected and details are populated
        /// </summary>
        [HarmonyPatch(typeof(CouncilorMissionCanvasController), "UpdateAllMissionData")]
        [HarmonyPostfix]
        public static void UpdateAllMissionData_Postfix(CouncilorMissionCanvasController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Debug: Check what mission was selected
                var activeButtonField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "activeButton");
                if (activeButtonField != null)
                {
                    var activeButton = activeButtonField.GetValue(__instance);
                    if (activeButton != null)
                    {
                        var missionTypeField = AccessTools.Field(activeButton.GetType(), "missionType");
                        if (missionTypeField != null)
                        {
                            var missionType = missionTypeField.GetValue(activeButton);
                            if (missionType != null)
                            {
                                var displayNameProp = AccessTools.Property(missionType.GetType(), "displayName");
                                string missionName = displayNameProp?.GetValue(missionType) as string ?? "Unknown";
                                MelonLogger.Msg($"[MissionPanel] Mission selected: {missionName}");
                            }
                        }
                    }
                }

                // Add hover handlers to mission detail fields using reflection
                AddMissionDetailHandlers(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UpdateAllMissionData patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Unity's Selectable.OnPointerEnter to announce slider on hover
        /// Slider inherits from Selectable, so we patch the base class
        /// </summary>
        [HarmonyPatch(typeof(UnityEngine.UI.Selectable), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void Selectable_OnPointerEnter_Postfix(UnityEngine.UI.Selectable __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Only handle Slider instances, not all Selectables (buttons, toggles, etc.)
                var slider = __instance as UnityEngine.UI.Slider;
                if (slider == null)
                    return;

                MelonLogger.Msg($"[MissionPanel] Selectable.OnPointerEnter called on Slider! Slider name: {slider.name}");

                // Check if this is the mission resource slider by checking parent hierarchy
                var parent = slider.transform.parent;
                bool isMissionSlider = false;
                while (parent != null && !isMissionSlider)
                {
                    if (parent.name.Contains("Mission") || parent.name.Contains("Cost") || parent.name.Contains("Resource"))
                    {
                        isMissionSlider = true;
                        MelonLogger.Msg($"[MissionPanel] Found mission-related parent: {parent.name}");
                    }
                    parent = parent.parent;
                }

                if (!isMissionSlider)
                {
                    MelonLogger.Msg($"[MissionPanel] Not a mission slider (name: {slider.name}), skipping");
                    return;
                }

                if (!slider.interactable)
                {
                    MelonLogger.Msg($"[MissionPanel] Slider not interactable");
                    return;
                }

                // Try to find nearby text fields showing the value and type
                var texts = slider.GetComponentsInParent<TMP_Text>();
                string value = "";
                string type = "";

                foreach (var text in texts)
                {
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        string cleaned = TISpeechMod.CleanText(text.text);

                        // Skip labels/headers like "MSG Resource Spending"
                        if (cleaned.Contains("MSG") || cleaned.Contains("Spending") || cleaned.Contains("Resource"))
                            continue;

                        // Extract resource keyword only
                        if (cleaned.Contains("Money")) type = "Money";
                        else if (cleaned.Contains("Influence")) type = "Influence";
                        else if (cleaned.Contains("Operations")) type = "Operations";
                        else if (cleaned.Contains("Ops")) type = "Ops";
                        else if (cleaned.Contains("Boost")) type = "Boost";
                        else if (cleaned.Contains("Research")) type = "Research";
                        else if (cleaned.Contains("Mission Control")) type = "Mission Control";

                        if (!string.IsNullOrEmpty(type))
                        {
                            MelonLogger.Msg($"[MissionPanel] Found type: {type}");
                        }

                        // Also check for the value
                        if (string.IsNullOrEmpty(value) && float.TryParse(cleaned.Trim(), out _))
                        {
                            value = cleaned.Trim();
                            MelonLogger.Msg($"[MissionPanel] Found value: {value}");
                        }
                    }

                    if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(type))
                        break;
                }

                // Determine slider direction
                string directionHint = "";
                switch (slider.direction)
                {
                    case UnityEngine.UI.Slider.Direction.LeftToRight:
                    case UnityEngine.UI.Slider.Direction.RightToLeft:
                        directionHint = " horizontal,";
                        break;
                    case UnityEngine.UI.Slider.Direction.BottomToTop:
                    case UnityEngine.UI.Slider.Direction.TopToBottom:
                        directionHint = " vertical,";
                        break;
                }

                string announcement = string.IsNullOrEmpty(value) || string.IsNullOrEmpty(type)
                    ? $"Resource slider,{directionHint} minimum {slider.minValue:N0}, maximum {slider.maxValue:N0}"
                    : $"Resource slider,{directionHint} {value} {type}, minimum {slider.minValue:N0}, maximum {slider.maxValue:N0}";

                MelonLogger.Msg($"[MissionPanel] Announcing: {announcement}");
                TISpeechMod.Speak(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Selectable.OnPointerEnter patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OnResourceSliderChangedValue to announce value changes
        /// </summary>
        [HarmonyPatch(typeof(CouncilorMissionCanvasController), "OnResourceSliderChangedValue")]
        [HarmonyPostfix]
        public static void OnResourceSliderChangedValue_Postfix(CouncilorMissionCanvasController __instance, float newValue)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get current time
                float currentTime = Time.time;

                // Only announce if enough time has passed since last announcement (debouncing)
                if (currentTime - lastSliderAnnounceTime < SLIDER_ANNOUNCE_DELAY)
                    return;

                lastSliderAnnounceTime = currentTime;
                lastSliderValue = newValue;

                // Get the resource value text field to announce the actual cost
                var resourceValueField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "resourceValue");
                var resourcesTypeField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "resourcesType");

                if (resourceValueField != null && resourcesTypeField != null)
                {
                    var resourceValue = resourceValueField.GetValue(__instance) as TMP_Text;
                    var resourcesType = resourcesTypeField.GetValue(__instance) as TMP_Text;

                    if (resourceValue != null && resourcesType != null)
                    {
                        string value = TISpeechMod.CleanText(resourceValue.text);
                        string type = TISpeechMod.CleanText(resourcesType.text);

                        // Extract just the resource keyword (sprite + text causes duplication like "Influence Influence")
                        string resourceKeyword = "";
                        if (type.Contains("Money")) resourceKeyword = "Money";
                        else if (type.Contains("Influence")) resourceKeyword = "Influence";
                        else if (type.Contains("Operations")) resourceKeyword = "Operations";
                        else if (type.Contains("Ops")) resourceKeyword = "Ops";
                        else if (type.Contains("Boost")) resourceKeyword = "Boost";
                        else if (type.Contains("Research")) resourceKeyword = "Research";
                        else if (type.Contains("Mission Control")) resourceKeyword = "Mission Control";

                        if (!string.IsNullOrEmpty(value))
                        {
                            TISpeechMod.Speak($"{value} {resourceKeyword}", interrupt: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnResourceSliderChangedValue patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handlers to all mission detail text fields
        /// </summary>
        private static void AddMissionDetailHandlers(CouncilorMissionCanvasController controller)
        {
            try
            {
                // Get text field references using reflection
                var missionNameField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "missionName");
                var resourcesTypeField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "resourcesType");
                var resourceValueField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "resourceValue");
                var fixedResourcesTypeField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "fixedResourcesType");
                var successOrFailureTextField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "successOrFailureText");
                var successOrFailureValueField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "successOrFailureValue");
                var councilorHeaderTextField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "councilorHeaderText");
                var targetHeaderTextField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "targetHeaderText");
                var confirmButtonField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "confirmButton");

                // Add handlers to each text field
                if (missionNameField != null)
                {
                    var missionName = missionNameField.GetValue(controller) as TMP_Text;
                    AddTextHoverHandler(missionName, "Mission");
                }

                if (resourcesTypeField != null && resourceValueField != null)
                {
                    var resourcesType = resourcesTypeField.GetValue(controller) as TMP_Text;
                    var resourceValue = resourceValueField.GetValue(controller) as TMP_Text;

                    // Combine type and value for a complete cost announcement
                    if (resourcesType != null && resourceValue != null && resourcesType.enabled && resourceValue.enabled)
                    {
                        AddCombinedTextHoverHandler(resourceValue, resourcesType, "Cost");
                    }
                }

                if (fixedResourcesTypeField != null && resourceValueField != null)
                {
                    var fixedResourcesType = fixedResourcesTypeField.GetValue(controller) as TMP_Text;
                    var resourceValue = resourceValueField.GetValue(controller) as TMP_Text;

                    // For fixed cost missions
                    if (fixedResourcesType != null && resourceValue != null && fixedResourcesType.enabled)
                    {
                        AddCombinedTextHoverHandler(resourceValue, fixedResourcesType, "Cost");
                    }
                }

                if (successOrFailureTextField != null && successOrFailureValueField != null)
                {
                    var successText = successOrFailureTextField.GetValue(controller) as TMP_Text;
                    var successValue = successOrFailureValueField.GetValue(controller) as TMP_Text;

                    if (successText != null && successValue != null)
                    {
                        AddCombinedTextHoverHandler(successValue, successText, null); // Label is in successText itself
                    }
                }

                if (councilorHeaderTextField != null)
                {
                    var councilorHeaderText = councilorHeaderTextField.GetValue(controller) as TMP_Text;
                    AddTextHoverHandler(councilorHeaderText, null); // Text includes its own label
                }

                if (targetHeaderTextField != null)
                {
                    var targetHeaderText = targetHeaderTextField.GetValue(controller) as TMP_Text;
                    AddTextHoverHandler(targetHeaderText, null); // Text includes its own label
                }

                // Also add handler to confirm button if it doesn't have one
                if (confirmButtonField != null)
                {
                    var confirmButton = confirmButtonField.GetValue(controller) as UnityEngine.UI.Button;
                    AddButtonHoverHandler(confirmButton);
                }

                // Add hover handler to the resource slider
                var resourcesSliderField = AccessTools.Field(typeof(CouncilorMissionCanvasController), "resourcesSlider");
                if (resourcesSliderField != null)
                {
                    var slider = resourcesSliderField.GetValue(controller) as UnityEngine.UI.Slider;
                    if (slider != null)
                    {
                        bool isActive = slider.gameObject.activeInHierarchy;
                        bool isEnabled = slider.enabled;
                        bool isInteractable = slider.interactable;

                        MelonLogger.Msg($"[MissionPanel] Slider found - Active: {isActive}, Enabled: {isEnabled}, Interactable: {isInteractable}");

                        if (isActive)
                        {
                            MelonLogger.Msg($"[MissionPanel] Adding hover handler to slider");
                            AddSliderHoverHandler(slider, resourceValueField, resourcesTypeField, controller);
                        }
                        else
                        {
                            MelonLogger.Msg($"[MissionPanel] Slider not active - this mission doesn't use a resource slider");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg($"[MissionPanel] Slider is null");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[MissionPanel] Could not find resourcesSlider field");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding mission detail handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a single text field
        /// </summary>
        private static void AddTextHoverHandler(TMP_Text textField, string label)
        {
            if (textField == null || !textField.enabled)
                return;

            EventTrigger trigger = textField.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = textField.gameObject.AddComponent<EventTrigger>();
            }
            else
            {
                // Clear existing PointerEnter handlers to avoid duplicates
                trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter);
            }

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => OnTextHover(textField, label));
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// Add hover handler that combines two text fields (e.g., "Cost: 10 Money")
        /// </summary>
        private static void AddCombinedTextHoverHandler(TMP_Text valueField, TMP_Text typeField, string label)
        {
            if (valueField == null || !valueField.enabled)
                return;

            EventTrigger trigger = valueField.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = valueField.gameObject.AddComponent<EventTrigger>();
            }
            else
            {
                trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter);
            }

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => OnCombinedTextHover(valueField, typeField, label));
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// Add hover handler to slider - adds to both the slider itself and its handle
        /// </summary>
        private static void AddSliderHoverHandler(UnityEngine.UI.Slider slider, System.Reflection.FieldInfo resourceValueField, System.Reflection.FieldInfo resourcesTypeField, CouncilorMissionCanvasController controller)
        {
            if (slider == null)
                return;

            // Add handler to the slider itself
            AddEventTriggerToGameObject(slider.gameObject, slider, resourceValueField, resourcesTypeField, controller, "slider");

            // Also add to the handle if it exists (Unity sliders often have handles that intercept events)
            if (slider.handleRect != null)
            {
                MelonLogger.Msg($"[MissionPanel] Adding handler to slider handle");
                AddEventTriggerToGameObject(slider.handleRect.gameObject, slider, resourceValueField, resourcesTypeField, controller, "handle");
            }

            // Also try the fill rect
            if (slider.fillRect != null)
            {
                MelonLogger.Msg($"[MissionPanel] Adding handler to slider fill");
                AddEventTriggerToGameObject(slider.fillRect.gameObject, slider, resourceValueField, resourcesTypeField, controller, "fill");
            }
        }

        private static void AddEventTriggerToGameObject(GameObject targetObj, UnityEngine.UI.Slider slider, System.Reflection.FieldInfo resourceValueField, System.Reflection.FieldInfo resourcesTypeField, CouncilorMissionCanvasController controller, string targetName)
        {
            EventTrigger trigger = targetObj.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = targetObj.AddComponent<EventTrigger>();
                MelonLogger.Msg($"[MissionPanel] Created EventTrigger on {targetName}");
            }
            else
            {
                trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter);
                MelonLogger.Msg($"[MissionPanel] EventTrigger already exists on {targetName}, cleared existing handlers");
            }

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => OnSliderHover(slider, resourceValueField, resourcesTypeField, controller, targetName));
            trigger.triggers.Add(entry);
            MelonLogger.Msg($"[MissionPanel] Added PointerEnter callback to {targetName}");
        }

        /// <summary>
        /// Add hover handler to confirm button
        /// </summary>
        private static void AddButtonHoverHandler(UnityEngine.UI.Button button)
        {
            if (button == null)
                return;

            // Check if button already has UIButtonFeedback - if so, don't add handler
            if (button.GetComponent<UIButtonFeedback>() != null)
                return;

            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            else
            {
                // Check if we already added a handler
                foreach (var existingEntry in trigger.triggers)
                {
                    if (existingEntry.eventID == EventTriggerType.PointerEnter)
                        return; // Already has handler
                }
            }

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => OnButtonHover(button));
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// Handler for text field hover
        /// </summary>
        private static void OnTextHover(TMP_Text textField, string label)
        {
            try
            {
                if (textField == null || !textField.enabled)
                    return;

                string text = TISpeechMod.CleanText(textField.text);
                if (string.IsNullOrEmpty(text))
                    return;

                string announcement = string.IsNullOrEmpty(label)
                    ? text
                    : $"{label}: {text}";

                TISpeechMod.Speak(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler for combined text fields hover
        /// </summary>
        private static void OnCombinedTextHover(TMP_Text valueField, TMP_Text typeField, string label)
        {
            try
            {
                if (valueField == null || typeField == null)
                    return;

                if (!valueField.enabled || !typeField.enabled)
                    return;

                string value = TISpeechMod.CleanText(valueField.text);
                string type = TISpeechMod.CleanText(typeField.text);

                if (string.IsNullOrEmpty(value))
                    return;

                string announcement;
                if (!string.IsNullOrEmpty(label))
                {
                    announcement = $"{label}: {value} {type}";
                }
                else
                {
                    announcement = $"{type} {value}";
                }

                TISpeechMod.Speak(announcement, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in combined text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler for slider hover
        /// </summary>
        private static void OnSliderHover(UnityEngine.UI.Slider slider, System.Reflection.FieldInfo resourceValueField, System.Reflection.FieldInfo resourcesTypeField, CouncilorMissionCanvasController controller, string source)
        {
            try
            {
                MelonLogger.Msg($"[MissionPanel] OnSliderHover called from {source}!");

                if (slider == null)
                {
                    MelonLogger.Msg($"[MissionPanel] Slider is null in callback");
                    return;
                }

                if (!slider.interactable)
                {
                    MelonLogger.Msg($"[MissionPanel] Slider is not interactable");
                    return;
                }

                MelonLogger.Msg($"[MissionPanel] Getting resource value and type fields...");

                // Get current resource value and type
                if (resourceValueField != null && resourcesTypeField != null)
                {
                    var resourceValue = resourceValueField.GetValue(controller) as TMP_Text;
                    var resourcesType = resourcesTypeField.GetValue(controller) as TMP_Text;

                    if (resourceValue != null && resourcesType != null)
                    {
                        string value = TISpeechMod.CleanText(resourceValue.text);
                        string type = TISpeechMod.CleanText(resourcesType.text);

                        MelonLogger.Msg($"[MissionPanel] Value: {value}, Type: {type}");

                        // Determine slider direction for keyboard navigation hint
                        string directionHint = "";
                        switch (slider.direction)
                        {
                            case UnityEngine.UI.Slider.Direction.LeftToRight:
                            case UnityEngine.UI.Slider.Direction.RightToLeft:
                                directionHint = " horizontal,";
                                break;
                            case UnityEngine.UI.Slider.Direction.BottomToTop:
                            case UnityEngine.UI.Slider.Direction.TopToBottom:
                                directionHint = " vertical,";
                                break;
                        }

                        // Announce slider with direction, current value, min, and max
                        string announcement = $"Resource slider,{directionHint} {value} {type}, minimum {slider.minValue:N0}, maximum {slider.maxValue:N0}";
                        MelonLogger.Msg($"[MissionPanel] About to speak: {announcement}");
                        TISpeechMod.Speak(announcement, interrupt: false);
                        MelonLogger.Msg($"[MissionPanel] Speak call completed");
                    }
                    else
                    {
                        MelonLogger.Msg($"[MissionPanel] Resource value or type is null");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[MissionPanel] Resource field reflection failed");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in slider hover handler: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handler for button hover
        /// </summary>
        private static void OnButtonHover(UnityEngine.UI.Button button)
        {
            try
            {
                if (button == null || !button.enabled || !button.interactable)
                    return;

                // Try to find button text
                var tmpText = button.GetComponentInChildren<TMP_Text>();
                string buttonText = tmpText != null ? TISpeechMod.CleanText(tmpText.text) : "Confirm";

                TISpeechMod.Speak($"Button: {buttonText}", interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in button hover handler: {ex.Message}");
            }
        }
    }
}
