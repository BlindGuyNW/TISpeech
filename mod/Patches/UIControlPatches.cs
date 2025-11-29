using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ModelShark;

namespace TISpeech.Patches
{
    /// <summary>
    /// Component that announces when a dialog/panel becomes active via SetActive(true)
    /// Add this to confirmation dialogs during Initialize to detect when they appear
    /// </summary>
    public class DialogAnnouncer : MonoBehaviour
    {
        public string dialogName = "Dialog";
        public bool announceOnEnable = true;
        public bool focusFirstButton = true;

        private static float lastAnnounceTime = 0f;
        private const float ANNOUNCE_COOLDOWN = 0.3f;

        void OnEnable()
        {
            if (!announceOnEnable || !TISpeechMod.IsReady)
                return;

            // Cooldown to prevent rapid-fire announcements
            float currentTime = Time.unscaledTime;
            if (currentTime - lastAnnounceTime < ANNOUNCE_COOLDOWN)
                return;
            lastAnnounceTime = currentTime;

            // Delay slightly to let Unity finish activating children
            StartCoroutine(AnnounceAfterFrame());
        }

        private System.Collections.IEnumerator AnnounceAfterFrame()
        {
            yield return null; // Wait one frame

            if (!gameObject.activeInHierarchy)
                yield break;

            // Build announcement from dialog content
            string announcement = BuildDialogAnnouncement();

            if (!string.IsNullOrEmpty(announcement))
            {
                TISpeechMod.Speak(announcement, interrupt: true);
                MelonLogger.Msg($"Dialog appeared: {announcement}");
            }

            // Optionally focus the first button
            if (focusFirstButton)
            {
                FocusFirstInteractable();
            }
        }

        private string BuildDialogAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append(dialogName);

            // Try to find and include dialog text content
            var textComponents = GetComponentsInChildren<TMP_Text>(includeInactive: false);
            foreach (var text in textComponents)
            {
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;

                // Skip button labels (short text), look for body text
                string cleanedText = TISpeechMod.CleanText(text.text);
                if (cleanedText.Length > 20 && !cleanedText.Contains("Button"))
                {
                    sb.Append(". ");
                    sb.Append(cleanedText);
                    break; // Just get the first substantial text
                }
            }

            // List available buttons
            var buttons = GetComponentsInChildren<Button>(includeInactive: false);
            var buttonNames = new List<string>();
            foreach (var button in buttons)
            {
                if (button == null || !button.interactable)
                    continue;

                var buttonText = button.GetComponentInChildren<TMP_Text>();
                if (buttonText != null && !string.IsNullOrEmpty(buttonText.text))
                {
                    string btnName = TISpeechMod.CleanText(buttonText.text);
                    if (!string.IsNullOrEmpty(btnName) && btnName.Length < 30)
                    {
                        buttonNames.Add(btnName);
                    }
                }
            }

            if (buttonNames.Count > 0)
            {
                sb.Append(". Options: ");
                sb.Append(string.Join(", ", buttonNames));
            }

            return sb.ToString();
        }

        private void FocusFirstInteractable()
        {
            // Find first interactable button
            var buttons = GetComponentsInChildren<Button>(includeInactive: false);
            foreach (var button in buttons)
            {
                if (button != null && button.interactable && button.gameObject.activeInHierarchy)
                {
                    // Set as selected in EventSystem
                    var eventSystem = EventSystem.current;
                    if (eventSystem != null)
                    {
                        eventSystem.SetSelectedGameObject(button.gameObject);
                        MelonLogger.Msg($"Focused button: {button.gameObject.name}");
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Harmony patches for UI controls (buttons, toggles, dropdowns) to provide screen reader accessibility
    /// Announces control information when no tooltip is present
    /// </summary>
    [HarmonyPatch]
    public class UIControlPatches
    {
        private static string lastControlText = "";
        private static float lastControlTime = 0f;
        private const float CONTROL_DEBOUNCE_TIME = 0.2f;

        #region Button Patches

        /// <summary>
        /// Patch UIButtonFeedback to announce button content when hovering
        /// Announces control type and context, using interrupt: false to avoid overriding tooltips
        /// </summary>
        [HarmonyPatch(typeof(UIButtonFeedback), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void UIButtonFeedback_OnPointerEnter_Postfix(UIButtonFeedback __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Check if button is enabled/interactable
                var button = __instance.GetComponent<Button>();
                if (button == null || !button.enabled || !button.interactable)
                    return;

                // Try to extract context from list item controllers first (most detailed)
                string context = TryExtractListItemContext(__instance.gameObject);

                if (string.IsNullOrEmpty(context))
                {
                    // Fallback: simple button text extraction
                    context = ExtractButtonText(__instance.gameObject);
                }

                if (!string.IsNullOrEmpty(context))
                {
                    // Always announce, even if tooltip exists
                    // Use interrupt: false so we don't override tooltip speech
                    AnnounceControl($"Button: {context}");
                }
                // Note: If we can't extract context, we just don't announce (common for icon-only buttons)
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UIButtonFeedback patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch CanvasControllerBase.Show to add EventTriggers to plain Unity buttons
        /// This catches buttons that don't have UIButtonFeedback component
        /// </summary>
        [HarmonyPatch(typeof(CanvasControllerBase), "Show")]
        [HarmonyPostfix]
        public static void CanvasControllerBase_Show_Postfix(CanvasControllerBase __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Find all buttons in this canvas that don't have UIButtonFeedback
                AddGenericButtonHandlers(__instance.gameObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CanvasControllerBase.Show patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch CouncilGridController.Initialize to add EventTriggers to confirmation dialog buttons
        /// This catches the recruitment confirmation (Confirm/Decline) buttons
        /// </summary>
        [HarmonyPatch(typeof(CouncilGridController), "Initialize")]
        [HarmonyPostfix]
        public static void CouncilGridController_Initialize_Postfix(CouncilGridController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to buttons in the recruitment confirmation dialog
                if (__instance.confirmRecruitBox != null)
                {
                    AddGenericButtonHandlers(__instance.confirmRecruitBox);
                    AddDialogAnnouncer(__instance.confirmRecruitBox, "Recruitment Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to recruitment confirmation");
                }

                // Add handlers to org operation confirmation dialogs (purchase, sell, move)
                if (__instance.confirmMovePanel != null)
                {
                    AddGenericButtonHandlers(__instance.confirmMovePanel);
                    AddDialogAnnouncer(__instance.confirmMovePanel, "Move Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to confirmMovePanel");
                }
                if (__instance.confirmPurchase != null)
                {
                    AddGenericButtonHandlers(__instance.confirmPurchase);
                    AddDialogAnnouncer(__instance.confirmPurchase, "Purchase Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to confirmPurchase");
                }
                if (__instance.confirmSell != null)
                {
                    AddGenericButtonHandlers(__instance.confirmSell);
                    AddDialogAnnouncer(__instance.confirmSell, "Sell Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to confirmSell");
                }

                // Add hover handler to spendXPButton
                if (__instance.spendXPButton != null)
                {
                    AddButtonHoverHandler(__instance.spendXPButton, "Spend XP");
                }

                // Check for XP spending panel (the augmentation selection panel)
                if (__instance.spendXPPanel != null)
                {
                    AddGenericButtonHandlers(__instance.spendXPPanel);
                    AddDialogAnnouncer(__instance.spendXPPanel, "Spend XP - Select Augmentation");
                }

                // Check for XP spending confirmation
                if (__instance.confirmSpendXPSelectionPanel != null)
                {
                    AddGenericButtonHandlers(__instance.confirmSpendXPSelectionPanel);
                    AddDialogAnnouncer(__instance.confirmSpendXPSelectionPanel, "Spend XP Confirmation");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilGridController.Initialize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OperationCanvasController.Initialize to add EventTriggers to operation confirmation buttons
        /// This catches operation confirmation dialogs
        /// </summary>
        [HarmonyPatch(typeof(OperationCanvasController), "Initialize")]
        [HarmonyPostfix]
        public static void OperationCanvasController_Initialize_Postfix(OperationCanvasController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to buttons in the operation confirmation dialog
                if (__instance.confirmPanel != null)
                {
                    AddGenericButtonHandlers(__instance.confirmPanel);
                    AddDialogAnnouncer(__instance.confirmPanel, "Operation Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to operation confirmation");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OperationCanvasController.Initialize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch NotificationScreenController.Initialize to add EventTriggers to alert dialog buttons
        /// This catches notification alert dialogs (OK/Go To buttons)
        /// </summary>
        [HarmonyPatch(typeof(NotificationScreenController), "Initialize")]
        [HarmonyPostfix]
        public static void NotificationScreenController_Initialize_Postfix(NotificationScreenController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to buttons in alert dialogs
                if (__instance.singleAlertBox != null)
                {
                    AddGenericButtonHandlers(__instance.singleAlertBox);
                    AddDialogAnnouncer(__instance.singleAlertBox, "Alert");
                    MelonLogger.Msg("Added button handlers and announcer to alert dialog");
                }

                // Also handle the confirm panel
                if (__instance.confirmPanelObject != null)
                {
                    AddGenericButtonHandlers(__instance.confirmPanelObject);
                    AddDialogAnnouncer(__instance.confirmPanelObject, "Notification Confirmation");
                    MelonLogger.Msg("Added button handlers and announcer to notification confirm panel");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NotificationScreenController.Initialize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch CouncilorMissionCanvasController.Initialize to add EventTriggers to mission confirmation buttons
        /// This catches mission confirmation dialogs
        /// </summary>
        [HarmonyPatch(typeof(CouncilorMissionCanvasController), "Initialize")]
        [HarmonyPostfix]
        public static void CouncilorMissionCanvasController_Initialize_Postfix(CouncilorMissionCanvasController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to all buttons in the mission canvas
                AddGenericButtonHandlers(__instance.gameObject);

                // Add dialog announcer to the abort confirmation UI
                if (__instance.abortConfirmUI != null)
                {
                    AddDialogAnnouncer(__instance.abortConfirmUI, "Abort Mission Confirmation");
                    MelonLogger.Msg("Added announcer to mission abort confirmation");
                }

                MelonLogger.Msg("Added button handlers to CouncilorMissionCanvasController dialogs");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorMissionCanvasController.Initialize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add EventTrigger to all buttons without UIButtonFeedback in a GameObject hierarchy
        /// </summary>
        private static void AddGenericButtonHandlers(GameObject root)
        {
            try
            {
                if (root == null)
                    return;

                // Get all Button components in children
                var buttons = root.GetComponentsInChildren<Button>(includeInactive: false);

                foreach (var button in buttons)
                {
                    if (button == null)
                        continue;

                    // Skip if this button already has UIButtonFeedback (already handled)
                    if (button.GetComponent<UIButtonFeedback>() != null)
                        continue;

                    // Skip if this button already has a TooltipTrigger (will be announced by tooltip)
                    if (button.GetComponent<TooltipTrigger>() != null)
                        continue;

                    // Check if we already added an EventTrigger
                    EventTrigger trigger = button.GetComponent<EventTrigger>();
                    if (trigger != null)
                    {
                        // Check if we already added our handler
                        bool hasOurHandler = false;
                        foreach (var entry in trigger.triggers)
                        {
                            if (entry.eventID == EventTriggerType.PointerEnter)
                            {
                                hasOurHandler = true;
                                break;
                            }
                        }
                        if (hasOurHandler)
                            continue; // Already handled
                    }
                    else
                    {
                        trigger = button.gameObject.AddComponent<EventTrigger>();
                    }

                    // Add pointer enter event
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => OnGenericButtonHover(button));
                    trigger.triggers.Add(enterEntry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding generic button handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add DialogAnnouncer component to a dialog GameObject to announce when it becomes active
        /// </summary>
        private static void AddDialogAnnouncer(GameObject dialog, string dialogName)
        {
            try
            {
                if (dialog == null)
                    return;

                // Check if we already added an announcer
                var existingAnnouncer = dialog.GetComponent<DialogAnnouncer>();
                if (existingAnnouncer != null)
                    return; // Already has announcer

                // Add the announcer component
                var announcer = dialog.AddComponent<DialogAnnouncer>();
                announcer.dialogName = dialogName;
                announcer.announceOnEnable = true;
                announcer.focusFirstButton = true;

                MelonLogger.Msg($"Added DialogAnnouncer to: {dialogName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding DialogAnnouncer to {dialogName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a hover handler directly to a specific button
        /// </summary>
        private static void AddButtonHoverHandler(Button button, string label)
        {
            if (button == null) return;

            try
            {
                EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                else
                    trigger.triggers.Clear();

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) =>
                {
                    if (button.interactable)
                    {
                        AnnounceControl($"Button: {label}");
                    }
                    else
                    {
                        AnnounceControl($"Button: {label} (disabled)");
                    }
                });
                trigger.triggers.Add(entry);

                MelonLogger.Msg($"Added direct hover handler to button: {label}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding button hover handler for {label}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a generic Unity button (no UIButtonFeedback)
        /// </summary>
        private static void OnGenericButtonHover(Button button)
        {
            try
            {
                if (!TISpeechMod.IsReady || button == null)
                    return;

                // Check if button is enabled/interactable
                if (!button.enabled || !button.interactable)
                    return;

                // Extract button text
                string context = ExtractButtonText(button.gameObject);

                if (!string.IsNullOrEmpty(context))
                {
                    AnnounceControl($"Button: {context}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in generic button hover handler: {ex.Message}");
            }
        }

        #endregion

        #region Toggle Patches

        /// <summary>
        /// Patch UIToggleFeedback to announce toggle state
        /// </summary>
        [HarmonyPatch(typeof(UIToggleFeedback), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void UIToggleFeedback_OnPointerEnter_Postfix(UIToggleFeedback __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var toggle = __instance.GetComponent<Toggle>();
                if (toggle == null || !toggle.enabled || !toggle.interactable)
                    return;

                string toggleText = ExtractToggleText(__instance.gameObject);
                string state = toggle.isOn ? "checked" : "unchecked";

                if (!string.IsNullOrEmpty(toggleText))
                {
                    AnnounceControl($"Toggle: {toggleText}, {state}");
                }
                // Note: If we can't extract context, we just don't announce
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UIToggleFeedback patch: {ex.Message}");
            }
        }

        #endregion

        #region Dropdown Patches

        /// <summary>
        /// Patch UIDropdownFeedback to announce dropdown and current selection
        /// </summary>
        [HarmonyPatch(typeof(UIDropdownFeedback), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void UIDropdownFeedback_OnPointerEnter_Postfix(UIDropdownFeedback __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var dropdown = __instance.GetComponent<TMP_Dropdown>();
                if (dropdown == null || !dropdown.enabled || !dropdown.interactable)
                    return;

                string dropdownLabel = ExtractDropdownText(__instance.gameObject);
                string currentValue = "";

                if (dropdown.options != null && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
                {
                    currentValue = dropdown.options[dropdown.value].text;
                }

                if (!string.IsNullOrEmpty(dropdownLabel))
                {
                    string announcement = string.IsNullOrEmpty(currentValue)
                        ? $"Dropdown: {dropdownLabel}"
                        : $"Dropdown: {dropdownLabel}, current: {currentValue}";
                    AnnounceControl(announcement);
                }
                // Note: If we can't extract context, we just don't announce
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UIDropdownFeedback patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Universal patch for TMP_Dropdown to add EventTrigger for accessibility
        /// This ensures all 50+ dropdowns in the game are accessible, including mission target selection
        /// Patches Start() to add hover handlers dynamically to dropdowns without UIDropdownFeedback
        /// </summary>
        [HarmonyPatch(typeof(TMP_Dropdown), "Start")]
        [HarmonyPostfix]
        public static void TMP_Dropdown_Start_Postfix(TMP_Dropdown __instance)
        {
            try
            {
                // Don't add handlers before Tolk is initialized
                if (!TISpeechMod.IsReady)
                    return;

                // If this dropdown already has UIDropdownFeedback, skip it
                // Our UIDropdownFeedback patch will handle those cases
                if (__instance.GetComponent<UIDropdownFeedback>() != null)
                    return;

                // Add EventTrigger if not already present
                var eventTrigger = __instance.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (eventTrigger == null)
                {
                    eventTrigger = __instance.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }

                // Check if we already added our handler (avoid duplicates if Start() is called multiple times)
                bool hasOurHandler = false;
                foreach (var trigger in eventTrigger.triggers)
                {
                    if (trigger.eventID == UnityEngine.EventSystems.EventTriggerType.PointerEnter)
                    {
                        hasOurHandler = true;
                        break;
                    }
                }

                if (!hasOurHandler)
                {
                    // Add pointer enter handler
                    var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
                    entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                    entry.callback.AddListener((data) => OnDropdownHover(__instance));
                    eventTrigger.triggers.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TMP_Dropdown Start patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler called when hovering over a dropdown without UIDropdownFeedback
        /// </summary>
        private static void OnDropdownHover(TMP_Dropdown dropdown)
        {
            try
            {
                // Check if dropdown is enabled and interactable
                if (!dropdown.enabled || !dropdown.interactable)
                    return;

                string dropdownLabel = ExtractDropdownText(dropdown.gameObject);
                string currentValue = "";

                if (dropdown.options != null && dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
                {
                    currentValue = TISpeechMod.CleanText(dropdown.options[dropdown.value].text);
                }

                if (!string.IsNullOrEmpty(dropdownLabel))
                {
                    string announcement = string.IsNullOrEmpty(currentValue)
                        ? $"Dropdown: {dropdownLabel}"
                        : $"Dropdown: {dropdownLabel}, current: {currentValue}";
                    AnnounceControl(announcement);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in dropdown hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch TMP_Dropdown.DropdownItem to announce individual dropdown options when hovering
        /// This makes the dropdown option list accessible when opened
        /// </summary>
        [HarmonyPatch]
        public static class DropdownItemPatch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                // Get the nested DropdownItem class from TMP_Dropdown
                var dropdownType = typeof(TMP_Dropdown);
                var dropdownItemType = dropdownType.GetNestedType("DropdownItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (dropdownItemType == null)
                {
                    MelonLogger.Error("Could not find TMP_Dropdown.DropdownItem type");
                    return null;
                }

                // Get the OnPointerEnter method
                var method = dropdownItemType.GetMethod("OnPointerEnter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    MelonLogger.Error("Could not find OnPointerEnter method on DropdownItem");
                }
                return method;
            }

            [HarmonyPostfix]
            public static void OnPointerEnter_Postfix(object __instance, PointerEventData eventData)
            {
                try
                {
                    if (!TISpeechMod.IsReady)
                        return;

                    // Get the text component using reflection
                    var instanceType = __instance.GetType();
                    var textField = instanceType.GetField("m_Text", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (textField != null)
                    {
                        var text = textField.GetValue(__instance) as TMP_Text;
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            string cleanedText = TISpeechMod.CleanText(text.text);
                            TISpeechMod.Speak(cleanedText, interrupt: false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in DropdownItem OnPointerEnter patch: {ex.Message}");
                }
            }
        }

        #endregion

        #region Mission Button Patches

        /// <summary>
        /// Patch CouncilorMissionButtonController for mission selection buttons
        /// These buttons don't use UIButtonFeedback, so they need their own patch
        /// </summary>
        [HarmonyPatch(typeof(CouncilorMissionButtonController), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void CouncilorMissionButtonController_OnPointerEnter_Postfix(CouncilorMissionButtonController __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Check if button is interactable
                if (!__instance.interactable)
                    return; // Tooltip will handle disabled missions

                // Get mission name from the mission template
                if (__instance.missionType != null)
                {
                    var displayNameProp = AccessTools.Property(typeof(TIMissionTemplate), "displayName");
                    if (displayNameProp != null)
                    {
                        string missionName = displayNameProp.GetValue(__instance.missionType) as string;
                        if (!string.IsNullOrEmpty(missionName))
                        {
                            AnnounceControl($"Mission: {missionName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorMissionButtonController patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OperationButtonController for army/fleet operation buttons
        /// These buttons also don't use UIButtonFeedback
        /// </summary>
        [HarmonyPatch(typeof(OperationButtonController), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void OperationButtonController_OnPointerEnter_Postfix(OperationButtonController __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Check if button is interactable
                if (!__instance.interactable)
                    return;

                // Get operation name from the operation type
                if (__instance.operationType != null)
                {
                    var getDisplayNameMethod = AccessTools.Method(__instance.operationType.GetType(), "GetDisplayName");
                    if (getDisplayNameMethod != null)
                    {
                        string operationName = getDisplayNameMethod.Invoke(__instance.operationType, null) as string;
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            AnnounceControl($"Operation: {operationName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OperationButtonController patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch TransferPlannerLocationButton for transfer planner location selection
        /// Another custom button type that implements IPointerEnterHandler directly
        /// </summary>
        [HarmonyPatch(typeof(TransferPlannerLocationButton), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void TransferPlannerLocationButton_OnPointerEnter_Postfix(TransferPlannerLocationButton __instance, PointerEventData eventData)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get location text
                if (__instance.text != null && !string.IsNullOrEmpty(__instance.text.text))
                {
                    string locationName = TISpeechMod.CleanText(__instance.text.text);
                    string locationType = __instance.isOrigin ? "Origin" : "Destination";
                    AnnounceControl($"{locationType}: {locationName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TransferPlannerLocationButton patch: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods - Text Extraction

        private static string ExtractButtonText(GameObject obj)
        {
            // Try to find TMP_Text component in children
            var tmpText = obj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                return TISpeechMod.CleanText(tmpText.text);
            }

            // Fallback to GameObject name
            return CleanObjectName(obj.name);
        }

        private static string ExtractToggleText(GameObject obj)
        {
            var tmpText = obj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                return TISpeechMod.CleanText(tmpText.text);
            }
            return CleanObjectName(obj.name);
        }

        private static string ExtractDropdownText(GameObject obj)
        {
            // Look for label text (usually a sibling or child)
            var tmpTexts = obj.GetComponentsInChildren<TMP_Text>();
            if (tmpTexts != null && tmpTexts.Length > 0)
            {
                // First text component is usually the label
                return TISpeechMod.CleanText(tmpTexts[0].text);
            }
            return CleanObjectName(obj.name);
        }

        #endregion

        #region Helper Methods - List Item Context Extraction

        private static string TryExtractListItemContext(GameObject obj)
        {
            // Try each list item controller type
            // Check self and parents up to 3 levels

            var finder = obj.GetComponentInParent<FinderListItemController>();
            if (finder != null)
                return BuildFinderItemContext(finder);

            var councilor = obj.GetComponentInParent<CouncilorsListItemController>();
            if (councilor != null)
                return BuildCouncilorItemContext(councilor);

            var project = obj.GetComponentInParent<ProjectsButtonListItemController>();
            if (project != null)
                return BuildProjectItemContext(project);

            var region = obj.GetComponentInParent<RegionListItemController>();
            if (region != null)
                return BuildRegionItemContext(region);

            var army = obj.GetComponentInParent<ArmyListItemController>();
            if (army != null)
                return BuildArmyItemContext(army);

            var targeting = obj.GetComponentInParent<TargetingListItemController>();
            if (targeting != null)
                return BuildTargetingItemContext(targeting);

            var tech = obj.GetComponentInParent<TechsButtonListItemController>();
            if (tech != null)
                return BuildTechItemContext(tech);

            var combinedResearch = obj.GetComponentInParent<CombinedResearchListItemController>();
            if (combinedResearch != null)
                return BuildCombinedResearchItemContext(combinedResearch);

            // EffectContextListItemController is internal, use reflection
            var effectContext = GetEffectContextController(obj);
            if (effectContext != null)
                return BuildEffectContextItemContext(effectContext);

            // Add more list item types as needed

            return null;
        }

        private static string BuildFinderItemContext(FinderListItemController controller)
        {
            try
            {
                var sb = new StringBuilder();

                // Get the item name
                if (controller.itemName != null && !string.IsNullOrEmpty(controller.itemName.text))
                {
                    sb.Append(TISpeechMod.CleanText(controller.itemName.text));
                }

                // Get location info if available
                if (controller.itemLocation != null && controller.itemLocation.enabled && controller.itemLocation.sprite != null)
                {
                    sb.Append($", location: {CleanObjectName(controller.itemLocation.sprite.name)}");
                }

                // Try to get detailed status from game state objects using reflection
                string detailedStatus = TryGetDetailedFinderStatus(controller);
                if (!string.IsNullOrEmpty(detailedStatus))
                {
                    sb.Append($", {detailedStatus}");
                }
                else if (controller.statusIcon != null && controller.statusIcon.enabled && controller.statusIcon.sprite != null)
                {
                    // Fallback: generic status from icon name
                    string statusName = CleanObjectName(controller.statusIcon.sprite.name);
                    if (statusName.Contains("Combat") || statusName.Contains("combat"))
                    {
                        sb.Append(", in combat");
                    }
                    else if (statusName.Contains("Mission") || statusName.Contains("mission"))
                    {
                        sb.Append(", on mission");
                    }
                    else if (statusName.Contains("Operation") || statusName.Contains("operation"))
                    {
                        sb.Append(", on operation");
                    }
                    else
                    {
                        sb.Append($", status: {statusName}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building finder item context: {ex.Message}");
                return null;
            }
        }

        private static string TryGetDetailedFinderStatus(FinderListItemController controller)
        {
            try
            {
                // Use reflection to access private game state fields
                var councilorField = AccessTools.Field(typeof(FinderListItemController), "councilor");
                var armyField = AccessTools.Field(typeof(FinderListItemController), "army");
                var fleetField = AccessTools.Field(typeof(FinderListItemController), "fleet");
                var habField = AccessTools.Field(typeof(FinderListItemController), "hab");

                // Check for councilor
                if (councilorField != null)
                {
                    var councilor = councilorField.GetValue(controller) as TICouncilorState;
                    if (councilor != null)
                    {
                        // Check for active mission
                        var activeMissionProp = AccessTools.Property(typeof(TICouncilorState), "activeMission");
                        if (activeMissionProp != null)
                        {
                            var activeMission = activeMissionProp.GetValue(councilor);
                            if (activeMission != null)
                            {
                                // Get mission template and display name
                                var missionTemplateProp = AccessTools.Property(activeMission.GetType(), "missionTemplate");
                                if (missionTemplateProp != null)
                                {
                                    var missionTemplate = missionTemplateProp.GetValue(activeMission);
                                    if (missionTemplate != null)
                                    {
                                        var displayNameProp = AccessTools.Property(missionTemplate.GetType(), "displayName");
                                        if (displayNameProp != null)
                                        {
                                            string missionName = displayNameProp.GetValue(missionTemplate) as string;
                                            if (!string.IsNullOrEmpty(missionName))
                                            {
                                                return $"mission: {missionName}";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Check for army
                if (armyField != null)
                {
                    var army = armyField.GetValue(controller) as TIArmyState;
                    if (army != null)
                    {
                        // Check for current operations
                        var currentOpsMethod = AccessTools.Method(typeof(TIArmyState), "CurrentOperations");
                        if (currentOpsMethod != null)
                        {
                            var ops = currentOpsMethod.Invoke(army, null) as System.Collections.IList;
                            if (ops != null && ops.Count > 0)
                            {
                                var firstOp = ops[0];
                                var operationProp = AccessTools.Property(firstOp.GetType(), "operation");
                                if (operationProp != null)
                                {
                                    var operation = operationProp.GetValue(firstOp);
                                    if (operation != null)
                                    {
                                        var getDisplayNameMethod = AccessTools.Method(operation.GetType(), "GetDisplayName");
                                        if (getDisplayNameMethod != null)
                                        {
                                            string opName = getDisplayNameMethod.Invoke(operation, null) as string;
                                            if (!string.IsNullOrEmpty(opName))
                                            {
                                                return $"operation: {opName}";
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Check for combat
                        var inBattleMethod = AccessTools.Method(typeof(TIArmyState), "InBattleWithArmiesOrRegionDefenses");
                        if (inBattleMethod != null)
                        {
                            bool inBattle = (bool)inBattleMethod.Invoke(army, null);
                            if (inBattle)
                            {
                                return "in combat";
                            }
                        }
                    }
                }

                // Check for fleet
                if (fleetField != null)
                {
                    var fleet = fleetField.GetValue(controller) as TISpaceFleetState;
                    if (fleet != null)
                    {
                        // Check for current operations
                        var currentOpsMethod = AccessTools.Method(typeof(TISpaceFleetState), "CurrentOperations");
                        if (currentOpsMethod != null)
                        {
                            var ops = currentOpsMethod.Invoke(fleet, null) as System.Collections.IList;
                            if (ops != null && ops.Count > 0)
                            {
                                var firstOp = ops[0];
                                var operationProp = AccessTools.Property(firstOp.GetType(), "operation");
                                if (operationProp != null)
                                {
                                    var operation = operationProp.GetValue(firstOp);
                                    if (operation != null)
                                    {
                                        var getDisplayNameMethod = AccessTools.Method(operation.GetType(), "GetDisplayName");
                                        if (getDisplayNameMethod != null)
                                        {
                                            string opName = getDisplayNameMethod.Invoke(operation, null) as string;
                                            if (!string.IsNullOrEmpty(opName))
                                            {
                                                return $"operation: {opName}";
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Check for docked status
                        var dockedProp = AccessTools.Property(typeof(TISpaceFleetState), "dockedOrLanded");
                        if (dockedProp != null)
                        {
                            bool docked = (bool)dockedProp.GetValue(fleet);
                            if (docked)
                            {
                                return "docked";
                            }
                        }
                    }
                }

                // Check for hab
                if (habField != null)
                {
                    var hab = habField.GetValue(controller) as TIHabState;
                    if (hab != null)
                    {
                        // Check for under bombardment
                        var underBombardmentProp = AccessTools.Property(typeof(TIHabState), "underBombardment");
                        if (underBombardmentProp != null)
                        {
                            bool underBombardment = (bool)underBombardmentProp.GetValue(hab);
                            if (underBombardment)
                            {
                                return "under attack";
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting detailed finder status: {ex.Message}");
                return null;
            }
        }

        private static string BuildCouncilorItemContext(CouncilorsListItemController controller)
        {
            try
            {
                var sb = new StringBuilder();

                if (controller.CouncilorName != null && !string.IsNullOrEmpty(controller.CouncilorName.text))
                {
                    sb.Append(TISpeechMod.CleanText(controller.CouncilorName.text));
                }

                if (controller.CouncilorProfession != null && !string.IsNullOrEmpty(controller.CouncilorProfession.text))
                {
                    sb.Append($", {TISpeechMod.CleanText(controller.CouncilorProfession.text)}");
                }

                if (controller.CurrentMission != null && controller.CurrentMission.enabled)
                {
                    sb.Append(", on mission");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building councilor item context: {ex.Message}");
                return null;
            }
        }

        private static string BuildProjectItemContext(ProjectsButtonListItemController controller)
        {
            try
            {
                if (controller.SelectProjectButtonText != null && !string.IsNullOrEmpty(controller.SelectProjectButtonText.text))
                {
                    var sb = new StringBuilder("Project: ");
                    sb.Append(TISpeechMod.CleanText(controller.SelectProjectButtonText.text));

                    // Check toggles
                    if (controller.favoriteToggle != null && controller.favoriteToggle.isOn)
                    {
                        sb.Append(", favorited");
                    }
                    if (controller.obsoleteToggle != null && controller.obsoleteToggle.isOn)
                    {
                        sb.Append(", obsolete");
                    }

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building project item context: {ex.Message}");
            }
            return null;
        }

        private static string BuildRegionItemContext(RegionListItemController controller)
        {
            try
            {
                var sb = new StringBuilder();

                if (controller.regionName != null && !string.IsNullOrEmpty(controller.regionName.text))
                {
                    sb.Append(TISpeechMod.CleanText(controller.regionName.text));
                }

                if (controller.regionPop != null && !string.IsNullOrEmpty(controller.regionPop.text))
                {
                    sb.Append($", population: {TISpeechMod.CleanText(controller.regionPop.text)} million");
                }

                if (controller.regionBoost != null && !string.IsNullOrEmpty(controller.regionBoost.text))
                {
                    string boost = TISpeechMod.CleanText(controller.regionBoost.text);
                    if (boost != "-")
                    {
                        sb.Append($", boost: {boost}");
                    }
                }

                if (controller.regionMC != null && !string.IsNullOrEmpty(controller.regionMC.text))
                {
                    sb.Append($", mission control: {TISpeechMod.CleanText(controller.regionMC.text)}");
                }

                // Check for occupation
                if (controller.occupierFlag != null && controller.occupierFlag.enabled &&
                    controller.occupationPct != null && !string.IsNullOrEmpty(controller.occupationPct.text))
                {
                    sb.Append($", occupied: {TISpeechMod.CleanText(controller.occupationPct.text)}");
                }

                // Check for abductions
                if (controller.abductions != null && controller.abductions.enabled &&
                    !string.IsNullOrEmpty(controller.abductions.text))
                {
                    sb.Append($", abductions: {TISpeechMod.CleanText(controller.abductions.text)}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building region item context: {ex.Message}");
                return null;
            }
        }

        private static string BuildArmyItemContext(ArmyListItemController controller)
        {
            try
            {
                // This is a placeholder - ArmyListItemController may have different properties
                // Will need to inspect actual structure when testing
                var tmpText = controller.GetComponentInChildren<TMP_Text>();
                if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                {
                    return "Army: " + TISpeechMod.CleanText(tmpText.text);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building army item context: {ex.Message}");
            }
            return null;
        }

        private static string BuildTargetingItemContext(TargetingListItemController controller)
        {
            try
            {
                var sb = new StringBuilder();

                // Get councilor name
                if (controller.councilorName != null && !string.IsNullOrEmpty(controller.councilorName.text))
                {
                    sb.Append(TISpeechMod.CleanText(controller.councilorName.text));
                }

                // Get mission name
                if (controller.missionName != null && !string.IsNullOrEmpty(controller.missionName.text))
                {
                    sb.Append($", mission: {TISpeechMod.CleanText(controller.missionName.text)}");
                }

                // Get success chance
                if (controller.successChanceText != null && !string.IsNullOrEmpty(controller.successChanceText.text))
                {
                    sb.Append($", success chance: {TISpeechMod.CleanText(controller.successChanceText.text)}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building targeting item context: {ex.Message}");
                return null;
            }
        }

        private static string BuildTechItemContext(TechsButtonListItemController controller)
        {
            try
            {
                if (controller.SelectTechButtonText != null && !string.IsNullOrEmpty(controller.SelectTechButtonText.text))
                {
                    return "Tech: " + TISpeechMod.CleanText(controller.SelectTechButtonText.text);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building tech item context: {ex.Message}");
            }
            return null;
        }

        private static string BuildCombinedResearchItemContext(CombinedResearchListItemController controller)
        {
            try
            {
                if (controller.selectTechButtonText != null && !string.IsNullOrEmpty(controller.selectTechButtonText.text))
                {
                    return TISpeechMod.CleanText(controller.selectTechButtonText.text);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building combined research item context: {ex.Message}");
            }
            return null;
        }

        private static string BuildEffectContextItemContext(object controller)
        {
            try
            {
                // EffectContextListItemController is internal, use reflection
                var textField = AccessTools.Field(controller.GetType(), "selectContextButtonText");
                if (textField != null)
                {
                    var tmpText = textField.GetValue(controller) as TMP_Text;
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        return TISpeechMod.CleanText(tmpText.text);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building effect context item context: {ex.Message}");
            }
            return null;
        }

        private static object GetEffectContextController(GameObject obj)
        {
            try
            {
                // EffectContextListItemController is internal, find it by name
                var components = obj.GetComponentsInParent<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component != null && component.GetType().Name == "EffectContextListItemController")
                    {
                        return component;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting EffectContextListItemController: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Helper Methods - Text Cleaning

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

        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            // Remove common Unity/game suffixes
            name = name.Replace("(Clone)", "")
                       .Replace("GameObject", "")
                       .Replace("Button", "")
                       .Replace("Toggle", "")
                       .Replace("Text", "")
                       .Replace("ICO_", "")
                       .Replace("_off", "")
                       .Replace("_on", "")
                       .Trim();

            // Add spaces before capital letters (PascalCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        #endregion

        #region Helper Methods - Announcement

        private static void AnnounceControl(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            float currentTime = Time.unscaledTime;

            // Debounce: don't re-announce the same control within a short time window
            if (text == lastControlText && (currentTime - lastControlTime) < CONTROL_DEBOUNCE_TIME)
                return;

            lastControlText = text;
            lastControlTime = currentTime;

            // Use interrupt: false so tooltips always take priority
            TISpeechMod.Speak(text, interrupt: false);

            MelonLogger.Msg($"Announced control: {text}");
        }

        #endregion

        #region Notification Review Mode Patches

        /// <summary>
        /// Patch NotificationScreenController.PushNextAlert to enter notification mode when Review Mode is active.
        /// This allows keyboard navigation through notification options.
        /// </summary>
        [HarmonyPatch(typeof(NotificationScreenController), "PushNextAlert")]
        [HarmonyPostfix]
        public static void NotificationScreenController_PushNextAlert_Postfix(NotificationScreenController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Only enter notification mode if Review Mode is already active
                var reviewMode = ReviewMode.ReviewModeController.Instance;
                if (reviewMode == null || !reviewMode.IsActive)
                {
                    MelonLogger.Msg("Notification appeared but Review Mode is not active - skipping notification mode");
                    return;
                }

                // Don't enter notification mode if we're already in it (shouldn't happen but be safe)
                if (reviewMode.IsInNotificationMode)
                {
                    MelonLogger.Msg("Already in notification mode - refreshing");
                    reviewMode.ExitNotificationMode();
                }

                // Enter notification mode
                reviewMode.EnterNotificationMode(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PushNextAlert patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch NotificationScreenController.CleanUp to exit notification mode when notification is dismissed.
        /// </summary>
        [HarmonyPatch(typeof(NotificationScreenController), "CleanUp", new Type[] { typeof(NotificationQueueItem) })]
        [HarmonyPostfix]
        public static void NotificationScreenController_CleanUp_Postfix()
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var reviewMode = ReviewMode.ReviewModeController.Instance;
                if (reviewMode?.IsInNotificationMode == true)
                {
                    reviewMode.ExitNotificationMode();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CleanUp patch: {ex.Message}");
            }
        }

        #endregion

        #region Policy Selection Patches

        /// <summary>
        /// Patch NotificationScreenController.OnSetPolicyMission to enter policy selection mode when Review Mode is active.
        /// This is called when the Set National Policy mission succeeds.
        /// </summary>
        [HarmonyPatch(typeof(NotificationScreenController), "OnSetPolicyMission")]
        [HarmonyPostfix]
        public static void NotificationScreenController_OnSetPolicyMission_Postfix(NotificationScreenController __instance, TICouncilorState councilor)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Only enter policy mode if Review Mode is active
                var reviewMode = ReviewMode.ReviewModeController.Instance;
                if (reviewMode == null || !reviewMode.IsActive)
                {
                    MelonLogger.Msg("Policy selection appeared but Review Mode is not active - skipping policy mode");
                    return;
                }

                // Get the nation from the councilor
                var nation = councilor?.currentNation;
                if (nation == null)
                {
                    MelonLogger.Error("OnSetPolicyMission: councilor or nation is null");
                    return;
                }

                // Exit notification mode if we're in it (policy selection replaces notification handling)
                if (reviewMode.IsInNotificationMode)
                {
                    reviewMode.ExitNotificationMode();
                }

                // Enter policy selection mode
                reviewMode.EnterPolicySelectionMode(__instance, nation, councilor);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnSetPolicyMission patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch NotificationScreenController.ShutdownPolicyPanels to exit policy selection mode.
        /// </summary>
        [HarmonyPatch(typeof(NotificationScreenController), "ShutdownPolicyPanels")]
        [HarmonyPostfix]
        public static void NotificationScreenController_ShutdownPolicyPanels_Postfix()
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var reviewMode = ReviewMode.ReviewModeController.Instance;
                if (reviewMode?.IsInPolicyMode == true)
                {
                    reviewMode.ExitPolicySelectionMode();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ShutdownPolicyPanels patch: {ex.Message}");
            }
        }

        #endregion
    }
}
