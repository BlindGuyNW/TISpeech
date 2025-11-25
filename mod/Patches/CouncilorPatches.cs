using System;
using System.Text;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for the council screen
    /// - Adds hover handlers to councilor grid items for accessibility
    /// - Adds hover handlers to recruitment list items for accessibility
    /// - Announces selection of councilors and candidates
    /// </summary>
    [HarmonyPatch]
    public class CouncilorPatches
    {
        private static string lastCouncilorText = "";
        private static float lastCouncilorTime = 0f;
        private const float COUNCILOR_DEBOUNCE_TIME = 0.3f;

        private static string lastRecruitText = "";
        private static float lastRecruitTime = 0f;
        private const float RECRUIT_DEBOUNCE_TIME = 0.3f;

        private static string lastSelectionText = "";
        private static float lastSelectionTime = 0f;
        private const float SELECTION_DEBOUNCE_TIME = 0.5f;

        #region CouncilorGridItemController Patches

        /// <summary>
        /// Patch UpdateListItem to add hover handlers to councilor grid items
        /// This is called when the council grid updates with councilor data
        /// </summary>
        [HarmonyPatch(typeof(CouncilorGridItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void UpdateListItem_Postfix(CouncilorGridItemController __instance, TICouncilorState councilor)
        {
            try
            {
                if (!TISpeechMod.IsReady || councilor == null)
                    return;

                AddCouncilorGridItemHandlers(__instance);

                // Add DialogAnnouncer to the advice panel if it exists
                if (__instance.councilorAdvicePanel != null)
                {
                    var announcer = __instance.councilorAdvicePanel.GetComponent<DialogAnnouncer>();
                    if (announcer == null)
                    {
                        string councilorName = TISpeechMod.CleanText(__instance.councilorName?.text ?? "Councilor");
                        announcer = __instance.councilorAdvicePanel.AddComponent<DialogAnnouncer>();
                        announcer.dialogName = $"Advice from {councilorName}";
                        announcer.announceOnEnable = true;
                        announcer.focusFirstButton = false; // Advice panel may not have buttons to focus
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorGridItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch ItemSelected to announce councilor selection with a consolidated summary
        /// </summary>
        [HarmonyPatch(typeof(CouncilorGridItemController), "ItemSelected")]
        [HarmonyPostfix]
        public static void ItemSelected_Postfix(CouncilorGridItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance.councilor == null)
                    return;

                AnnounceCouncilorSelection(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorGridItemController.ItemSelected patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add event handlers to all text fields in a councilor grid item
        /// </summary>
        private static void AddCouncilorGridItemHandlers(CouncilorGridItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady || controller == null)
                    return;

                // Get the councilor name for context
                string councilorName = controller.councilor != null ? TISpeechMod.CleanText(controller.councilorName.text) : "Councilor";

                // Basic info
                AddCouncilorTextHoverHandler(controller.councilorName, councilorName, "Name");
                AddCouncilorTextHoverHandler(controller.councilorProfession, councilorName, "Profession");
                AddCouncilorTextHoverHandler(controller.councilorLocation, councilorName, "Location");
                AddCouncilorTextHoverHandler(controller.councilorMission, councilorName, "Mission");

                // Stats (with stat name labels)
                AddCouncilorTextHoverHandler(controller.persuasion, councilorName, "Persuasion");
                AddCouncilorTextHoverHandler(controller.investigation, councilorName, "Investigation");
                AddCouncilorTextHoverHandler(controller.espionage, councilorName, "Espionage");
                AddCouncilorTextHoverHandler(controller.command, councilorName, "Command");
                AddCouncilorTextHoverHandler(controller.administration, councilorName, "Administration");
                AddCouncilorTextHoverHandler(controller.science, councilorName, "Science");
                AddCouncilorTextHoverHandler(controller.security, councilorName, "Security");
                AddCouncilorTextHoverHandler(controller.apparentLoyalty, councilorName, "Loyalty");

                // Income (with resource labels)
                AddCouncilorTextHoverHandler(controller.moneyIncome, councilorName, "Money Income");
                AddCouncilorTextHoverHandler(controller.influenceIncome, councilorName, "Influence Income");
                AddCouncilorTextHoverHandler(controller.opsIncome, councilorName, "Operations Income");
                AddCouncilorTextHoverHandler(controller.researchIncome, councilorName, "Research Income");
                AddCouncilorTextHoverHandler(controller.boostIncome, councilorName, "Boost Income");
                AddCouncilorTextHoverHandler(controller.mCIncome, councilorName, "Mission Control Income");

                // Other fields
                AddCouncilorTextHoverHandler(controller.projects, councilorName, "Projects");
                AddCouncilorTextHoverHandler(controller.XPValue, councilorName, "Experience");

                // Status text (only if visible)
                if (controller.statusText != null && controller.statusText.gameObject.activeInHierarchy)
                {
                    AddCouncilorTextHoverHandler(controller.statusText, councilorName, "Status");
                }

                MelonLogger.Msg($"Added councilor grid item handlers for {councilorName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding councilor grid item handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a hover event handler to a councilor text field
        /// </summary>
        private static void AddCouncilorTextHoverHandler(TMP_Text textField, string councilorName, string fieldLabel)
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
                string capturedCouncilorName = councilorName;
                string capturedFieldLabel = fieldLabel;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnCouncilorTextHover(textField, capturedCouncilorName, capturedFieldLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to {fieldLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a councilor text field
        /// </summary>
        private static void OnCouncilorTextHover(TMP_Text textField, string councilorName, string fieldLabel)
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

                // Build the announcement: "{Councilor Name}, {Field Label}: {Value}"
                string announcement = $"{councilorName}, {fieldLabel}: {value}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastCouncilorText && (currentTime - lastCouncilorTime) < COUNCILOR_DEBOUNCE_TIME)
                    return;

                lastCouncilorText = announcement;
                lastCouncilorTime = currentTime;

                // Announce with interrupt: false so tooltips take priority
                TISpeechMod.Speak(announcement, interrupt: false);

                MelonLogger.Msg($"Councilor text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in councilor text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce a consolidated summary when selecting a councilor
        /// </summary>
        private static void AnnounceCouncilorSelection(CouncilorGridItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady || controller == null || controller.councilor == null)
                    return;

                StringBuilder sb = new StringBuilder();

                // Name and profession
                if (controller.councilorName != null && !string.IsNullOrEmpty(controller.councilorName.text))
                {
                    sb.Append("Selected: ");
                    sb.Append(TISpeechMod.CleanText(controller.councilorName.text));
                }

                if (controller.councilorProfession != null && !string.IsNullOrEmpty(controller.councilorProfession.text))
                {
                    sb.Append(", ");
                    sb.Append(TISpeechMod.CleanText(controller.councilorProfession.text));
                }

                // Location or mission
                if (controller.councilorMission != null && !string.IsNullOrEmpty(controller.councilorMission.text))
                {
                    string mission = TISpeechMod.CleanText(controller.councilorMission.text);
                    if (!string.IsNullOrWhiteSpace(mission))
                    {
                        sb.Append(", ");
                        sb.Append(mission);
                    }
                }
                else if (controller.councilorLocation != null && !string.IsNullOrEmpty(controller.councilorLocation.text))
                {
                    sb.Append(", at ");
                    sb.Append(TISpeechMod.CleanText(controller.councilorLocation.text));
                }

                // Status (if visible)
                if (controller.statusText != null && controller.statusText.gameObject.activeInHierarchy &&
                    !string.IsNullOrEmpty(controller.statusText.text))
                {
                    sb.Append(", ");
                    sb.Append(TISpeechMod.CleanText(controller.statusText.text));
                }

                string announcement = sb.ToString();

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastSelectionText && (currentTime - lastSelectionTime) < SELECTION_DEBOUNCE_TIME)
                    return;

                lastSelectionText = announcement;
                lastSelectionTime = currentTime;

                // Announce with interrupt: true since this is a user action
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Councilor selected: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing councilor selection: {ex.Message}");
            }
        }

        #endregion

        #region CouncilorRecruitListItemController Patches

        /// <summary>
        /// Patch UpdateListItem to add hover handlers to recruitment list items
        /// This is called when the recruitment list updates with candidate data
        /// </summary>
        [HarmonyPatch(typeof(CouncilorRecruitListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void UpdateRecruitListItem_Postfix(CouncilorRecruitListItemController __instance, TICouncilorState councilor)
        {
            try
            {
                if (!TISpeechMod.IsReady || councilor == null)
                    return;

                AddRecruitListItemHandlers(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorRecruitListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch ItemSelected to announce candidate selection with a consolidated summary
        /// </summary>
        [HarmonyPatch(typeof(CouncilorRecruitListItemController), "ItemSelected")]
        [HarmonyPostfix]
        public static void RecruitItemSelected_Postfix(CouncilorRecruitListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance.councilor == null)
                    return;

                AnnounceRecruitSelection(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CouncilorRecruitListItemController.ItemSelected patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add event handlers to all text fields in a recruitment list item
        /// </summary>
        private static void AddRecruitListItemHandlers(CouncilorRecruitListItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady || controller == null)
                    return;

                // Get the candidate name for context
                string candidateName = controller.councilor != null ? TISpeechMod.CleanText(controller.candidateName.text) : "Candidate";

                // Basic info
                AddRecruitTextHoverHandler(controller.candidateName, candidateName, "Name");
                AddRecruitTextHoverHandler(controller.profession, candidateName, "Profession");
                AddRecruitTextHoverHandler(controller.cost, candidateName, "Recruitment Cost");

                // Stats (with stat name labels)
                AddRecruitTextHoverHandler(controller.persuasion, candidateName, "Persuasion");
                AddRecruitTextHoverHandler(controller.investigation, candidateName, "Investigation");
                AddRecruitTextHoverHandler(controller.espionage, candidateName, "Espionage");
                AddRecruitTextHoverHandler(controller.command, candidateName, "Command");
                AddRecruitTextHoverHandler(controller.administration, candidateName, "Administration");
                AddRecruitTextHoverHandler(controller.science, candidateName, "Science");
                AddRecruitTextHoverHandler(controller.security, candidateName, "Security");
                AddRecruitTextHoverHandler(controller.loyalty, candidateName, "Loyalty");

                MelonLogger.Msg($"Added recruitment list item handlers for {candidateName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding recruitment list item handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a hover event handler to a recruitment text field
        /// </summary>
        private static void AddRecruitTextHoverHandler(TMP_Text textField, string candidateName, string fieldLabel)
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
                string capturedCandidateName = candidateName;
                string capturedFieldLabel = fieldLabel;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnRecruitTextHover(textField, capturedCandidateName, capturedFieldLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to {fieldLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a recruitment text field
        /// </summary>
        private static void OnRecruitTextHover(TMP_Text textField, string candidateName, string fieldLabel)
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

                // Build the announcement: "{Candidate Name}, {Field Label}: {Value}"
                string announcement = $"{candidateName}, {fieldLabel}: {value}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastRecruitText && (currentTime - lastRecruitTime) < RECRUIT_DEBOUNCE_TIME)
                    return;

                lastRecruitText = announcement;
                lastRecruitTime = currentTime;

                // Announce with interrupt: false so tooltips take priority
                TISpeechMod.Speak(announcement, interrupt: false);

                MelonLogger.Msg($"Recruit text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in recruit text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce a consolidated summary when selecting a recruitment candidate
        /// </summary>
        private static void AnnounceRecruitSelection(CouncilorRecruitListItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady || controller == null || controller.councilor == null)
                    return;

                StringBuilder sb = new StringBuilder();

                // Name and profession
                if (controller.candidateName != null && !string.IsNullOrEmpty(controller.candidateName.text))
                {
                    sb.Append("Selected candidate: ");
                    sb.Append(TISpeechMod.CleanText(controller.candidateName.text));
                }

                if (controller.profession != null && !string.IsNullOrEmpty(controller.profession.text))
                {
                    sb.Append(", ");
                    sb.Append(TISpeechMod.CleanText(controller.profession.text));
                }

                // Recruitment cost
                if (controller.cost != null && !string.IsNullOrEmpty(controller.cost.text))
                {
                    sb.Append(", cost: ");
                    sb.Append(TISpeechMod.CleanText(controller.cost.text));
                }

                // Key stats (just the highest 2-3 to keep it concise)
                AppendTopStats(sb, controller);

                string announcement = sb.ToString();

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastSelectionText && (currentTime - lastSelectionTime) < SELECTION_DEBOUNCE_TIME)
                    return;

                lastSelectionText = announcement;
                lastSelectionTime = currentTime;

                // Announce with interrupt: true since this is a user action
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Candidate selected: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing recruit selection: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to append the top 2-3 stats for a recruitment candidate
        /// </summary>
        private static void AppendTopStats(StringBuilder sb, CouncilorRecruitListItemController controller)
        {
            try
            {
                // Get all stats with their values
                var stats = new System.Collections.Generic.List<(string name, int value)>();

                if (int.TryParse(controller.persuasion.text, out int persuasion))
                    stats.Add(("Persuasion", persuasion));
                if (int.TryParse(controller.investigation.text, out int investigation))
                    stats.Add(("Investigation", investigation));
                if (int.TryParse(controller.espionage.text, out int espionage))
                    stats.Add(("Espionage", espionage));
                if (int.TryParse(controller.command.text, out int command))
                    stats.Add(("Command", command));
                if (int.TryParse(controller.administration.text, out int administration))
                    stats.Add(("Administration", administration));
                if (int.TryParse(controller.science.text, out int science))
                    stats.Add(("Science", science));
                if (int.TryParse(controller.security.text, out int security))
                    stats.Add(("Security", security));

                // Sort by value descending and take top 2
                stats.Sort((a, b) => b.value.CompareTo(a.value));

                int count = 0;
                foreach (var stat in stats)
                {
                    if (count >= 2) break;
                    if (stat.value >= 10) // Only mention stats that are decent
                    {
                        sb.Append($", {stat.name} {stat.value}");
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error appending top stats: {ex.Message}");
            }
        }

        #endregion
    }
}
