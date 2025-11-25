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
    /// Harmony patches for faction contribution list items on the research screen
    /// - Adds hover handlers to faction icons and contribution amounts
    /// </summary>
    [HarmonyPatch]
    public class FactionContributionPatches
    {
        private static string lastFactionContributionText = "";
        private static float lastFactionContributionTime = 0f;
        private const float FACTION_CONTRIBUTION_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch UpdateListItem to add hover handlers when faction contribution items are updated
        /// </summary>
        [HarmonyPatch(typeof(FactionContributionListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void UpdateListItem_Postfix(FactionContributionListItemController __instance, TIFactionState factionState, TechProgress currentTechProgress)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                AddFactionContributionHandlers(__instance, factionState, currentTechProgress);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in FactionContributionListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handlers to the faction icon and contribution text
        /// </summary>
        private static void AddFactionContributionHandlers(FactionContributionListItemController controller, TIFactionState factionState, TechProgress currentTechProgress)
        {
            try
            {
                if (!TISpeechMod.IsReady || controller == null || factionState == null)
                    return;

                // Add hover to faction icon
                if (controller.factionImage != null && controller.factionImage.gameObject != null)
                {
                    AddImageHoverHandler(controller.factionImage.gameObject, factionState, currentTechProgress);
                }

                // Add hover to contribution text
                if (controller.factionContribution != null && controller.factionContribution.gameObject != null)
                {
                    AddTextHoverHandler(controller.factionContribution, factionState, currentTechProgress);
                }

                MelonLogger.Msg($"Added faction contribution handlers for {factionState.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding faction contribution handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to faction icon image
        /// </summary>
        private static void AddImageHoverHandler(GameObject iconObject, TIFactionState factionState, TechProgress currentTechProgress)
        {
            try
            {
                if (iconObject == null)
                    return;

                EventTrigger trigger = iconObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = iconObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Only remove existing PointerEnter triggers to avoid duplicates
                    // Don't clear all triggers - game may have click handlers we need to preserve
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => OnFactionContributionHover(factionState, currentTechProgress));
                trigger.triggers.Add(entry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding image hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to contribution text field
        /// </summary>
        private static void AddTextHoverHandler(TMP_Text textField, TIFactionState factionState, TechProgress currentTechProgress)
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
                    // Only remove existing PointerEnter triggers to avoid duplicates
                    // Don't clear all triggers - game may have click handlers we need to preserve
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => OnFactionContributionHover(factionState, currentTechProgress));
                trigger.triggers.Add(entry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when user hovers over a faction contribution element
        /// </summary>
        private static void OnFactionContributionHover(TIFactionState factionState, TechProgress currentTechProgress)
        {
            try
            {
                if (!TISpeechMod.IsReady || factionState == null || currentTechProgress == null)
                    return;

                // Get faction name and contribution amount
                string factionName = factionState.displayName;
                float contribution = currentTechProgress.factionContributions.ContainsKey(factionState)
                    ? currentTechProgress.factionContributions[factionState]
                    : 0f;

                // Check if this faction is the leader
                TIFactionState leader = currentTechProgress.GetExpectedWinner();
                string leaderStatus = (leader == factionState) ? " (Leader)" : "";

                // Build announcement
                string announcement = $"{factionName}{leaderStatus}: {contribution:N0} research points";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastFactionContributionText && (currentTime - lastFactionContributionTime) < FACTION_CONTRIBUTION_DEBOUNCE_TIME)
                    return;

                lastFactionContributionText = announcement;
                lastFactionContributionTime = currentTime;

                // Announce with interrupt: false so tooltips take priority
                TISpeechMod.Speak(announcement, interrupt: false);

                MelonLogger.Msg($"Faction contribution hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in faction contribution hover handler: {ex.Message}");
            }
        }
    }
}
