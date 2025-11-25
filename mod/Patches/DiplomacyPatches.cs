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
    /// Harmony patches for the Diplomacy/Trade window
    /// - Adds hover handlers to faction headers and attitude text
    /// - Adds hover handlers to trade feedback text
    /// - Adds hover handlers to category tab buttons
    /// - Adds hover handlers to resource bank items
    /// - Adds hover handlers to Execute Trade button
    /// </summary>
    [HarmonyPatch]
    public class DiplomacyPatches
    {
        private static string lastDiplomacyText = "";
        private static float lastDiplomacyTime = 0f;
        private const float DIPLOMACY_DEBOUNCE_TIME = 0.3f;

        #region DiplomacyController Setup Patch

        /// <summary>
        /// Patch Setup to add accessibility handlers to the diplomacy window
        /// </summary>
        [HarmonyPatch(typeof(DiplomacyController), "Setup")]
        [HarmonyPostfix]
        public static void DiplomacyController_Setup_Postfix(DiplomacyController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to all UI elements
                AddFactionHeaderHandlers(__instance);
                AddFeedbackTextHandler(__instance);
                AddTabButtonHandlers(__instance);
                AddResourceBankHandlers(__instance);
                AddExecuteTradeButtonHandler(__instance);

                MelonLogger.Msg("Added Diplomacy window accessibility handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DiplomacyController.Setup patch: {ex.Message}");
            }
        }

        #endregion

        #region Handler Setup Methods

        /// <summary>
        /// Add handlers to faction name and attitude text fields
        /// </summary>
        private static void AddFactionHeaderHandlers(DiplomacyController controller)
        {
            // Player faction name
            AddDiplomacyTextHoverHandler(controller.playerFactionText, "Your Faction", "Name");

            // AI faction name and attitude
            AddDiplomacyTextHoverHandler(controller.aiFactionText, "Trading With", "Faction");
            AddDiplomacyTextHoverHandler(controller.aiFactionAttitudeText, "Their Attitude", "Status");
        }

        /// <summary>
        /// Add handler to trade feedback/evaluation text
        /// </summary>
        private static void AddFeedbackTextHandler(DiplomacyController controller)
        {
            AddDiplomacyTextHoverHandler(controller.aiFeedbackDialogText, "Trade Status", "Evaluation");
        }

        /// <summary>
        /// Add handlers to category tab buttons
        /// NOTE: We add handlers to the TAB BUTTONS, not the text labels.
        /// Adding EventTrigger to text labels blocks click propagation to the parent Button.
        /// </summary>
        private static void AddTabButtonHandlers(DiplomacyController controller)
        {
            // Player side tabs
            AddTabButtonHandler(controller.playerResourcesTab, "Your Resources Tab");
            AddTabButtonHandler(controller.playerOrgTab, "Your Organizations Tab");
            AddTabButtonHandler(controller.playerHabsTab, "Your Habitats Tab");
            AddTabButtonHandler(controller.playerProjectsTab, "Your Projects Tab");

            // AI side tabs
            AddTabButtonHandler(controller.aiResourcesTab, "Their Resources Tab");
            AddTabButtonHandler(controller.aiOrgTab, "Their Organizations Tab");
            AddTabButtonHandler(controller.aiHabsTab, "Their Habitats Tab");
            AddTabButtonHandler(controller.aiProjectsTab, "Their Projects Tab");
        }

        /// <summary>
        /// Add handler to a tab button (DiplomacyBankListItem used as tab)
        /// </summary>
        private static void AddTabButtonHandler(DiplomacyBankListItem tabItem, string tabLabel)
        {
            try
            {
                if (tabItem == null || tabItem.button == null)
                    return;

                // Add EventTrigger to the Button component, not the text
                EventTrigger trigger = tabItem.button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = tabItem.button.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                string capturedLabel = tabLabel;
                DiplomacyBankListItem capturedTab = tabItem;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnTabButtonHover(capturedTab, capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding tab button handler for {tabLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Add handlers to resource bank items (shows resource amounts)
        /// </summary>
        private static void AddResourceBankHandlers(DiplomacyController controller)
        {
            // Player bank resources
            AddBankItemHandler(controller.playerBankCashItem, "Your", "Money");
            AddBankItemHandler(controller.playerBankInfluenceItem, "Your", "Influence");
            AddBankItemHandler(controller.playerBankOpsItem, "Your", "Operations");
            AddBankItemHandler(controller.playerBankBoostItem, "Your", "Boost");
            AddBankItemHandler(controller.playerBankWaterItem, "Your", "Water");
            AddBankItemHandler(controller.playerBankVolatilesItem, "Your", "Volatiles");
            AddBankItemHandler(controller.playerBankBaseMetalsItem, "Your", "Metals");
            AddBankItemHandler(controller.playerBankNobleMetalsItem, "Your", "Noble Metals");
            AddBankItemHandler(controller.playerBankFissilesItem, "Your", "Fissiles");
            AddBankItemHandler(controller.playerBankAntimatterItem, "Your", "Antimatter");
            AddBankItemHandler(controller.playerBankExoticsItem, "Your", "Exotics");

            // Player treaty/special items
            AddBankItemHandler(controller.playerBankTreatyItem, "Your", "Treaty");
            AddBankItemHandler(controller.playerBankExchangeIntelItem, "Your", "Exchange Intel");

            // AI bank resources
            AddBankItemHandler(controller.aiBankCashItem, "Their", "Money");
            AddBankItemHandler(controller.aiBankInfluenceItem, "Their", "Influence");
            AddBankItemHandler(controller.aiBankOpsItem, "Their", "Operations");
            AddBankItemHandler(controller.aiBankBoostItem, "Their", "Boost");
            AddBankItemHandler(controller.aiBankWaterItem, "Their", "Water");
            AddBankItemHandler(controller.aiBankVolatilesItem, "Their", "Volatiles");
            AddBankItemHandler(controller.aiBankBaseMetalsItem, "Their", "Metals");
            AddBankItemHandler(controller.aiBankNobleMetalsItem, "Their", "Noble Metals");
            AddBankItemHandler(controller.aiBankFissilesItem, "Their", "Fissiles");
            AddBankItemHandler(controller.aiBankAntimatterItem, "Their", "Antimatter");
            AddBankItemHandler(controller.aiBankExoticsItem, "Their", "Exotics");

            // AI treaty/special items
            AddBankItemHandler(controller.aiBankTreatyItem, "Their", "Treaty");
            AddBankItemHandler(controller.aiBankExchangeIntelItem, "Their", "Exchange Intel");
        }

        /// <summary>
        /// Add handler to a bank item (resource slot)
        /// </summary>
        private static void AddBankItemHandler(DiplomacyBankListItem bankItem, string side, string resourceLabel)
        {
            try
            {
                if (bankItem == null)
                    return;

                // Add handler to the quantityText field
                if (bankItem.quantityText != null)
                {
                    EventTrigger trigger = bankItem.quantityText.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null)
                    {
                        trigger = bankItem.quantityText.gameObject.AddComponent<EventTrigger>();
                    }
                    else
                    {
                        trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                    }

                    string capturedSide = side;
                    string capturedLabel = resourceLabel;
                    TMP_Text capturedText = bankItem.quantityText;

                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => OnBankItemHover(capturedText, capturedSide, capturedLabel));
                    trigger.triggers.Add(enterEntry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding bank item handler for {side} {resourceLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Add handler to the Execute Trade button
        /// </summary>
        private static void AddExecuteTradeButtonHandler(DiplomacyController controller)
        {
            try
            {
                if (controller.executeTradeButton == null)
                    return;

                EventTrigger trigger = controller.executeTradeButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = controller.executeTradeButton.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                Button capturedButton = controller.executeTradeButton;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnExecuteTradeButtonHover(capturedButton));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding Execute Trade button handler: {ex.Message}");
            }
        }

        #endregion

        #region Hover Event Handlers

        /// <summary>
        /// Called when hovering over a tab button
        /// </summary>
        private static void OnTabButtonHover(DiplomacyBankListItem tabItem, string tabLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || tabItem == null)
                    return;

                // Check if tab is expanded or collapsed
                string expandState = tabItem.tabText?.text == "-" ? "expanded" : "collapsed";
                string announcement = $"{tabLabel}, {expandState}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastDiplomacyText && (currentTime - lastDiplomacyTime) < DIPLOMACY_DEBOUNCE_TIME)
                    return;

                lastDiplomacyText = announcement;
                lastDiplomacyTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Diplomacy tab hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in tab button hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a bank item (resource)
        /// </summary>
        private static void OnBankItemHover(TMP_Text textField, string side, string resourceLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textField == null)
                    return;

                string value = TISpeechMod.CleanText(textField.text);
                if (string.IsNullOrWhiteSpace(value))
                    return;

                string announcement = $"{side} {resourceLabel}: {value}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastDiplomacyText && (currentTime - lastDiplomacyTime) < DIPLOMACY_DEBOUNCE_TIME)
                    return;

                lastDiplomacyText = announcement;
                lastDiplomacyTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Diplomacy bank hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in bank item hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over the Execute Trade button
        /// </summary>
        private static void OnExecuteTradeButtonHover(Button button)
        {
            try
            {
                if (!TISpeechMod.IsReady || button == null)
                    return;

                string announcement = button.interactable
                    ? "Execute Trade button"
                    : "Execute Trade button, unavailable";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastDiplomacyText && (currentTime - lastDiplomacyTime) < DIPLOMACY_DEBOUNCE_TIME)
                    return;

                lastDiplomacyText = announcement;
                lastDiplomacyTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Diplomacy button hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Execute Trade button hover: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Add a hover event handler to a diplomacy text field
        /// </summary>
        private static void AddDiplomacyTextHoverHandler(TMP_Text textField, string context, string fieldLabel)
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
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                string capturedContext = context;
                string capturedLabel = fieldLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnDiplomacyTextHover(textField, capturedContext, capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding diplomacy text hover handler for {fieldLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a diplomacy text field
        /// </summary>
        private static void OnDiplomacyTextHover(TMP_Text textField, string context, string fieldLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textField == null)
                    return;

                string value = TISpeechMod.CleanText(textField.text);
                if (string.IsNullOrWhiteSpace(value))
                    return;

                string announcement;
                if (fieldLabel == "Name" || fieldLabel == "Faction")
                {
                    // For faction names, include context
                    announcement = $"{context}: {value}";
                }
                else if (fieldLabel == "Evaluation")
                {
                    // For trade evaluation, just announce the status
                    announcement = $"Trade evaluation: {value}";
                }
                else
                {
                    announcement = $"{context}: {value}";
                }

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastDiplomacyText && (currentTime - lastDiplomacyTime) < DIPLOMACY_DEBOUNCE_TIME)
                    return;

                lastDiplomacyText = announcement;
                lastDiplomacyTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Diplomacy text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in diplomacy text hover: {ex.Message}");
            }
        }

        #endregion
    }
}
