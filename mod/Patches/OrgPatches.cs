using System;
using System.Text;
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
    /// Harmony patches for the council org tab accessibility
    /// - Adds hover handlers to org items in single councilor view
    /// - Adds hover handlers to councilor cards in organizer view
    /// - Adds hover handlers to org items in organizer view
    /// - Adds handlers for org action buttons
    /// </summary>
    [HarmonyPatch]
    public class OrgPatches
    {
        private static string lastOrgText = "";
        private static float lastOrgTime = 0f;
        private const float ORG_DEBOUNCE_TIME = 0.3f;

        private static string lastOrganizerText = "";
        private static float lastOrganizerTime = 0f;
        private const float ORGANIZER_DEBOUNCE_TIME = 0.3f;

        #region OrgItemView Patches (Single Councilor View)

        /// <summary>
        /// Patch UpdateOrgItem to add hover handlers to org items in the single councilor view
        /// This is called when org grid updates with org data
        /// </summary>
        [HarmonyPatch(typeof(OrgItemView), "UpdateOrgItem")]
        [HarmonyPostfix]
        public static void UpdateOrgItem_Postfix(OrgItemView __instance, TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                if (!TISpeechMod.IsReady || org == null)
                    return;

                AddOrgItemHoverHandler(__instance, org, councilor);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OrgItemView.UpdateOrgItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch OnLeftClickItem to announce org selection
        /// </summary>
        [HarmonyPatch(typeof(OrgItemView), "OnLeftClickItem")]
        [HarmonyPostfix]
        public static void OnLeftClickItem_Postfix(OrgItemView __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var org = __instance.GetOrg();
                if (org == null)
                    return;

                string announcement = BuildOrgSelectionAnnouncement(__instance, org);
                if (!string.IsNullOrEmpty(announcement))
                {
                    TISpeechMod.Speak(announcement, interrupt: true);
                    MelonLogger.Msg($"Org selected: {announcement}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OrgItemView.OnLeftClickItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to an org item view
        /// </summary>
        private static void AddOrgItemHoverHandler(OrgItemView view, TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                if (view == null || view.gameObject == null)
                    return;

                // Add EventTrigger to the org item's GameObject
                EventTrigger trigger = view.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = view.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Clear existing triggers to avoid duplicates
                    trigger.triggers.Clear();
                }

                // Capture values for the closure
                OrgItemView capturedView = view;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnOrgItemHover(capturedView));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding org item hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over an org item
        /// </summary>
        private static void OnOrgItemHover(OrgItemView view)
        {
            try
            {
                if (!TISpeechMod.IsReady || view == null)
                    return;

                var org = view.GetOrg();
                if (org == null)
                    return;

                StringBuilder sb = new StringBuilder();

                // Org name
                sb.Append(org.displayName);

                // Tier (number of stars)
                sb.Append($", tier {org.tier}");

                // Status
                switch (view.status)
                {
                    case OrgItemView.OrgStatus.ASSIGNED:
                        sb.Append(", equipped");
                        break;
                    case OrgItemView.OrgStatus.UNASSIGNED:
                        sb.Append(", unassigned");
                        // Transfer cost
                        var transferCost = org.GetTransferCost();
                        if (transferCost != null)
                        {
                            string costStr = TISpeechMod.CleanText(transferCost.ToString("N0"));
                            if (!string.IsNullOrEmpty(costStr) && costStr != "0")
                            {
                                sb.Append($", transfer cost: {costStr}");
                            }
                        }
                        break;
                    case OrgItemView.OrgStatus.AVAILABLE:
                        sb.Append(", available to purchase");
                        // Purchase cost - need faction to calculate
                        if (GameControl.control?.activePlayer != null)
                        {
                            var purchaseCost = org.GetPurchaseCost(GameControl.control.activePlayer);
                            string costStr = TISpeechMod.CleanText(purchaseCost.ToString("N0"));
                            if (!string.IsNullOrEmpty(costStr))
                            {
                                sb.Append($", cost: {costStr}");
                            }
                        }
                        break;
                }

                string announcement = sb.ToString();

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastOrgText && (currentTime - lastOrgTime) < ORG_DEBOUNCE_TIME)
                    return;

                lastOrgText = announcement;
                lastOrgTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Org hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in org item hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Build announcement for when an org is selected (clicked)
        /// </summary>
        private static string BuildOrgSelectionAnnouncement(OrgItemView view, TIOrgState org)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Selected: ");
            sb.Append(org.displayName);
            sb.Append($", tier {org.tier}");

            // Add action hint based on status
            switch (view.status)
            {
                case OrgItemView.OrgStatus.ASSIGNED:
                    sb.Append(". Actions: Unequip, Sell");
                    break;
                case OrgItemView.OrgStatus.UNASSIGNED:
                    sb.Append(". Actions: Equip, Sell");
                    break;
                case OrgItemView.OrgStatus.AVAILABLE:
                    sb.Append(". Action: Purchase");
                    break;
            }

            return sb.ToString();
        }

        #endregion

        #region OrganizerCouncilorListItem Patches (Organizer View)

        /// <summary>
        /// Patch UpdateListItem to add hover handlers to councilor cards in the organizer view
        /// </summary>
        [HarmonyPatch(typeof(OrganizerCouncilorListItem), "UpdateListItem")]
        [HarmonyPostfix]
        public static void OrganizerCouncilorListItem_UpdateListItem_Postfix(OrganizerCouncilorListItem __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                AddOrganizerCouncilorHoverHandlers(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OrganizerCouncilorListItem.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handlers to councilor card fields in the organizer
        /// </summary>
        private static void AddOrganizerCouncilorHoverHandlers(OrganizerCouncilorListItem controller)
        {
            try
            {
                if (controller == null)
                    return;

                // Get councilor name for context
                string councilorName = controller.councilorNameText != null
                    ? TISpeechMod.CleanText(controller.councilorNameText.text)
                    : "Councilor";

                // Basic info
                AddOrganizerTextHoverHandler(controller.councilorNameText, councilorName, "Name");
                AddOrganizerTextHoverHandler(controller.professionText, councilorName, "Profession");
                AddOrganizerTextHoverHandler(controller.orgLimitText, councilorName, "Orgs assigned");

                // Attribute values
                AddOrganizerTextHoverHandler(controller.adminValueText, councilorName, "Administration");
                AddOrganizerTextHoverHandler(controller.adminValue2Text, councilorName, "Admin capacity");
                AddOrganizerTextHoverHandler(controller.persuasionValueText, councilorName, "Persuasion");
                AddOrganizerTextHoverHandler(controller.investigationValueText, councilorName, "Investigation");
                AddOrganizerTextHoverHandler(controller.espionageValueText, councilorName, "Espionage");
                AddOrganizerTextHoverHandler(controller.commandValueText, councilorName, "Command");
                AddOrganizerTextHoverHandler(controller.scienceValueText, councilorName, "Science");
                AddOrganizerTextHoverHandler(controller.securityValueText, councilorName, "Security");
                AddOrganizerTextHoverHandler(controller.loyaltyValueText, councilorName, "Loyalty");

                MelonLogger.Msg($"Added organizer councilor handlers for {councilorName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding organizer councilor hover handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a hover handler to a text field in the organizer view
        /// </summary>
        private static void AddOrganizerTextHoverHandler(TMP_Text textField, string contextName, string fieldLabel)
        {
            try
            {
                if (textField == null || textField.gameObject == null)
                    return;

                EventTrigger trigger = textField.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = textField.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.Clear();
                }

                // Capture for closure
                string capturedContext = contextName;
                string capturedLabel = fieldLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnOrganizerTextHover(textField, capturedContext, capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding organizer text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a text field in the organizer view
        /// </summary>
        private static void OnOrganizerTextHover(TMP_Text textField, string contextName, string fieldLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textField == null)
                    return;

                string value = textField.text;
                if (string.IsNullOrWhiteSpace(value))
                    return;

                value = TISpeechMod.CleanText(value);
                if (string.IsNullOrWhiteSpace(value))
                    return;

                string announcement = $"{contextName}, {fieldLabel}: {value}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastOrganizerText && (currentTime - lastOrganizerTime) < ORGANIZER_DEBOUNCE_TIME)
                    return;

                lastOrganizerText = announcement;
                lastOrganizerTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Organizer text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in organizer text hover handler: {ex.Message}");
            }
        }

        #endregion

        #region OrganizerOrgListItem Patches (Organizer View Org Items)

        /// <summary>
        /// Patch SetListItem to add hover handlers to org items in the organizer view
        /// </summary>
        [HarmonyPatch(typeof(OrganizerOrgListItem), "SetListItem")]
        [HarmonyPostfix]
        public static void OrganizerOrgListItem_SetListItem_Postfix(OrganizerOrgListItem __instance, TIOrgState orgState, OrganizerOrgListItem.OrgStatus status)
        {
            try
            {
                if (!TISpeechMod.IsReady || orgState == null)
                    return;

                AddOrganizerOrgHoverHandler(__instance, orgState, status);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OrganizerOrgListItem.SetListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to org items in the organizer view
        /// </summary>
        private static void AddOrganizerOrgHoverHandler(OrganizerOrgListItem view, TIOrgState org, OrganizerOrgListItem.OrgStatus status)
        {
            try
            {
                if (view == null || view.gameObject == null)
                    return;

                EventTrigger trigger = view.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = view.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.Clear();
                }

                // Capture for closure
                TIOrgState capturedOrg = org;
                OrganizerOrgListItem.OrgStatus capturedStatus = status;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnOrganizerOrgHover(capturedOrg, capturedStatus));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding organizer org hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over an org in the organizer view
        /// </summary>
        private static void OnOrganizerOrgHover(TIOrgState org, OrganizerOrgListItem.OrgStatus status)
        {
            try
            {
                if (!TISpeechMod.IsReady || org == null)
                    return;

                StringBuilder sb = new StringBuilder();

                // Org name
                sb.Append(org.displayName);

                // Tier
                sb.Append($", tier {org.tier}");

                // Status
                switch (status)
                {
                    case OrganizerOrgListItem.OrgStatus.ASSIGNED:
                        sb.Append(", assigned");
                        break;
                    case OrganizerOrgListItem.OrgStatus.UNASSIGNED:
                        sb.Append(", in faction pool");
                        break;
                    case OrganizerOrgListItem.OrgStatus.AVAILABLE:
                        sb.Append(", on market");
                        break;
                }

                // Brief description
                string desc = org.descriptionTruncated();
                if (!string.IsNullOrEmpty(desc))
                {
                    desc = TISpeechMod.CleanText(desc);
                    if (!string.IsNullOrEmpty(desc))
                    {
                        sb.Append($". {desc}");
                    }
                }

                string announcement = sb.ToString();

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastOrganizerText && (currentTime - lastOrganizerTime) < ORGANIZER_DEBOUNCE_TIME)
                    return;

                lastOrganizerText = announcement;
                lastOrganizerTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Organizer org hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in organizer org hover handler: {ex.Message}");
            }
        }

        #endregion

        #region Org Action Button Patches

        /// <summary>
        /// Patch CouncilGridController.Initialize to add handlers to org-related action buttons
        /// This covers the info panel action buttons (Purchase, Equip, Unequip, Sell)
        /// </summary>
        [HarmonyPatch(typeof(CouncilGridController), "Initialize")]
        [HarmonyPostfix]
        public static void CouncilGridController_Initialize_OrgButtons_Postfix(CouncilGridController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to org action buttons using their text labels
                AddOrgActionButtonHandler(__instance.orgActionButtonTop, __instance.orgActionButtonTextTop);
                AddOrgActionButtonHandler(__instance.orgActionButtonBottom2, __instance.orgActionButtonTextBottom2);

                // The bottom action button needs special handling - get it from the hierarchy
                // orgActionButtonBottom is the primary action button in the equip panel
                var actionButtonBottom = GetOrgActionButtonBottom(__instance);
                if (actionButtonBottom != null)
                {
                    AddOrgActionButtonHandler(actionButtonBottom, __instance.orgActionButtonTextBottom);
                }

                // Also add handler to the top sell button (orgActionButtonTextTop2)
                var actionButtonTop2 = GetOrgActionButtonTop2(__instance);
                if (actionButtonTop2 != null)
                {
                    AddOrgActionButtonHandler(actionButtonTop2, __instance.orgActionButtonTextTop2);
                }

                // Tab buttons for switching between Council Orgs and Available Orgs
                AddTabButtonHandler(__instance.unassignedOrgsButton, "Council Orgs tab");
                AddTabButtonHandler(__instance.orgMarketplaceButton, "Available Orgs tab");

                // Org management confirm/revert buttons
                AddOrgManagementButtonHandlers(__instance);

                MelonLogger.Msg("Added org action button handlers to CouncilGridController");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilGridController.Initialize org buttons patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the bottom action button (primary action in equip panel)
        /// </summary>
        private static Button GetOrgActionButtonBottom(CouncilGridController controller)
        {
            try
            {
                // The orgActionButtonTextBottom is on a button - get its parent button
                if (controller.orgActionButtonTextBottom != null)
                {
                    return controller.orgActionButtonTextBottom.GetComponentInParent<Button>();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting org action button bottom: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the top sell button (secondary action in my orgs panel)
        /// </summary>
        private static Button GetOrgActionButtonTop2(CouncilGridController controller)
        {
            try
            {
                if (controller.orgActionButtonTextTop2 != null)
                {
                    return controller.orgActionButtonTextTop2.GetComponentInParent<Button>();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting org action button top 2: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Add hover handler to an org action button
        /// </summary>
        private static void AddOrgActionButtonHandler(Button button, TMP_Text labelText)
        {
            try
            {
                if (button == null)
                    return;

                // Skip if button already has UIButtonFeedback or TooltipTrigger
                if (button.GetComponent<UIButtonFeedback>() != null)
                    return;
                if (button.GetComponent<ModelShark.TooltipTrigger>() != null)
                    return;

                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }

                // Check if we already have our handler
                bool hasHandler = false;
                foreach (var entry in trigger.triggers)
                {
                    if (entry.eventID == EventTriggerType.PointerEnter)
                    {
                        hasHandler = true;
                        break;
                    }
                }

                if (!hasHandler)
                {
                    TMP_Text capturedLabel = labelText;
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => OnOrgActionButtonHover(capturedLabel));
                    trigger.triggers.Add(enterEntry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding org action button handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over an org action button
        /// </summary>
        private static void OnOrgActionButtonHover(TMP_Text labelText)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string buttonText = labelText != null ? TISpeechMod.CleanText(labelText.text) : "Action";
                if (string.IsNullOrEmpty(buttonText))
                    buttonText = "Action";

                string announcement = $"Button: {buttonText}";

                // Use the same debounce as org items
                float currentTime = Time.unscaledTime;
                if (announcement == lastOrgText && (currentTime - lastOrgTime) < ORG_DEBOUNCE_TIME)
                    return;

                lastOrgText = announcement;
                lastOrgTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Org action button hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in org action button hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a tab button
        /// </summary>
        private static void AddTabButtonHandler(Button button, string tabName)
        {
            try
            {
                if (button == null)
                    return;

                // Skip if already has feedback
                if (button.GetComponent<UIButtonFeedback>() != null)
                    return;

                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }

                bool hasHandler = false;
                foreach (var entry in trigger.triggers)
                {
                    if (entry.eventID == EventTriggerType.PointerEnter)
                    {
                        hasHandler = true;
                        break;
                    }
                }

                if (!hasHandler)
                {
                    string capturedName = tabName;
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => OnTabButtonHover(capturedName));
                    trigger.triggers.Add(enterEntry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding tab button handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a tab button
        /// </summary>
        private static void OnTabButtonHover(string tabName)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string announcement = $"Tab: {tabName}";

                float currentTime = Time.unscaledTime;
                if (announcement == lastOrgText && (currentTime - lastOrgTime) < ORG_DEBOUNCE_TIME)
                    return;

                lastOrgText = announcement;
                lastOrgTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Tab button hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in tab button hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Add handlers to org management confirm/revert buttons
        /// </summary>
        private static void AddOrgManagementButtonHandlers(CouncilGridController controller)
        {
            try
            {
                // Use reflection to get the confirm and revert buttons
                var confirmButtonField = AccessTools.Field(typeof(CouncilGridController), "confirmOrgChangesButton");
                var revertButtonField = AccessTools.Field(typeof(CouncilGridController), "revertOrgChangesButton");

                if (confirmButtonField != null)
                {
                    var confirmButton = confirmButtonField.GetValue(controller) as Button;
                    if (confirmButton != null)
                    {
                        AddSimpleButtonHandler(confirmButton, "Confirm org changes");
                    }
                }

                if (revertButtonField != null)
                {
                    var revertButton = revertButtonField.GetValue(controller) as Button;
                    if (revertButton != null)
                    {
                        AddSimpleButtonHandler(revertButton, "Revert org changes");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding org management button handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a simple hover handler to a button with a fixed label
        /// </summary>
        private static void AddSimpleButtonHandler(Button button, string label)
        {
            try
            {
                if (button == null)
                    return;

                if (button.GetComponent<UIButtonFeedback>() != null)
                    return;

                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }

                bool hasHandler = false;
                foreach (var entry in trigger.triggers)
                {
                    if (entry.eventID == EventTriggerType.PointerEnter)
                    {
                        hasHandler = true;
                        break;
                    }
                }

                if (!hasHandler)
                {
                    string capturedLabel = label;
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) =>
                    {
                        if (TISpeechMod.IsReady)
                        {
                            TISpeechMod.Speak($"Button: {capturedLabel}", interrupt: false);
                        }
                    });
                    trigger.triggers.Add(enterEntry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding simple button handler: {ex.Message}");
            }
        }

        #endregion
    }
}
