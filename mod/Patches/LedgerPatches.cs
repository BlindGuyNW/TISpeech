using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for the ledger system to provide context for numbers
    /// Adds hover announcements for ledger cells to announce row + column + value
    /// </summary>
    [HarmonyPatch]
    public class LedgerPatches
    {
        // Map LedgerEntryCategory enum to human-readable column names
        private static readonly Dictionary<LedgerEntryCategory, string> CategoryNames = new Dictionary<LedgerEntryCategory, string>
        {
            { LedgerEntryCategory.money_Income, "Money Income" },
            { LedgerEntryCategory.money_Cost, "Money Cost" },
            { LedgerEntryCategory.influence_Income, "Influence Income" },
            { LedgerEntryCategory.influence_Cost, "Influence Cost" },
            { LedgerEntryCategory.ops_Income, "Operations Income" },
            { LedgerEntryCategory.boost_income, "Boost Income" },
            { LedgerEntryCategory.boost_cost, "Boost Cost" },
            { LedgerEntryCategory.missionControl_Income, "Mission Control Income" },
            { LedgerEntryCategory.missionControl_Cost, "Mission Control Cost" },
            { LedgerEntryCategory.research_Income, "Research Income" },
            { LedgerEntryCategory.projects_Income, "Projects Income" },
            { LedgerEntryCategory.CPCapacity_Gain, "Control Point Capacity Gain" },
            { LedgerEntryCategory.CPCapacity_Cost, "Control Point Capacity Cost" },
            { LedgerEntryCategory.water_Income, "Water Income" },
            { LedgerEntryCategory.water_Cost, "Water Cost" },
            { LedgerEntryCategory.volatiles_Income, "Volatiles Income" },
            { LedgerEntryCategory.volatiles_Cost, "Volatiles Cost" },
            { LedgerEntryCategory.metals_Income, "Metals Income" },
            { LedgerEntryCategory.metals_Cost, "Metals Cost" },
            { LedgerEntryCategory.nobles_Income, "Noble Metals Income" },
            { LedgerEntryCategory.nobles_Cost, "Noble Metals Cost" },
            { LedgerEntryCategory.fissiles_Income, "Fissiles Income" },
            { LedgerEntryCategory.fissiles_Cost, "Fissiles Cost" },
            { LedgerEntryCategory.antimatter_Income, "Antimatter Income" },
            { LedgerEntryCategory.antimatter_Cost, "Antimatter Cost" },
            { LedgerEntryCategory.exotics_Income, "Exotics Income" },
            { LedgerEntryCategory.exotics_Cost, "Exotics Cost" },
            { LedgerEntryCategory.energy_Bonus, "Energy Tech Bonus" },
            { LedgerEntryCategory.materials_Bonus, "Materials Tech Bonus" },
            { LedgerEntryCategory.spaceScience_Bonus, "Space Science Tech Bonus" },
            { LedgerEntryCategory.lifeScience_Bonus, "Life Science Tech Bonus" },
            { LedgerEntryCategory.infoScience_Bonus, "Information Science Tech Bonus" },
            { LedgerEntryCategory.militaryScience_Bonus, "Military Science Tech Bonus" },
            { LedgerEntryCategory.socialScience_Bonus, "Social Science Tech Bonus" },
            { LedgerEntryCategory.xenology_Bonus, "Xenology Tech Bonus" }
        };

        private static string lastLedgerCellText = "";
        private static float lastLedgerCellTime = 0f;
        private const float LEDGER_DEBOUNCE_TIME = 0.3f;

        /// <summary>
        /// Patch all SetListItem methods to add hover handlers to ledger cells
        /// </summary>
        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TIHabState) })]
        [HarmonyPostfix]
        public static void SetListItem_Hab_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TIHabModuleState) })]
        [HarmonyPostfix]
        public static void SetListItem_HabModule_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TIFactionState), typeof(int) })]
        [HarmonyPostfix]
        public static void SetListItem_Faction_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TISpaceFleetState) })]
        [HarmonyPostfix]
        public static void SetListItem_Fleet_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TISpaceShipState) })]
        [HarmonyPostfix]
        public static void SetListItem_Ship_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TINationState), typeof(TIFactionState) })]
        [HarmonyPostfix]
        public static void SetListItem_Nation_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TICouncilorState) })]
        [HarmonyPostfix]
        public static void SetListItem_Councilor_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TIOrgState) })]
        [HarmonyPostfix]
        public static void SetListItem_Org_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        [HarmonyPatch(typeof(LedgerListItemController), "SetListItem", new Type[] { typeof(LedgerListItem_Data), typeof(TITraitTemplate), typeof(TICouncilorState) })]
        [HarmonyPostfix]
        public static void SetListItem_Trait_Postfix(LedgerListItemController __instance)
        {
            AddLedgerCellEventHandlers(__instance);
        }

        /// <summary>
        /// Add event handlers to all ledger entry cells for hover announcements
        /// </summary>
        private static void AddLedgerCellEventHandlers(LedgerListItemController controller)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                if (controller == null || controller.ledgerEntry == null)
                    return;

                // Get the row name from the entry name and clean it
                string rowName = controller.entryName?.text ?? "Unknown";
                rowName = TISpeechMod.CleanText(rowName);

                // Add event handlers to each ledger cell
                for (int i = 0; i < controller.ledgerEntry.Length; i++)
                {
                    TMP_Text cellText = controller.ledgerEntry[i];
                    if (cellText == null)
                        continue;

                    // Check if we already added an event trigger to this cell
                    EventTrigger trigger = cellText.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null)
                    {
                        trigger = cellText.gameObject.AddComponent<EventTrigger>();
                    }
                    else
                    {
                        // Clear existing triggers to avoid duplicates
                        trigger.triggers.Clear();
                    }

                    // Capture variables for the closure
                    int categoryIndex = i;
                    string capturedRowName = rowName;

                    // Add pointer enter event
                    EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => OnLedgerCellHover(controller, capturedRowName, categoryIndex));
                    trigger.triggers.Add(enterEntry);
                }

                MelonLogger.Msg($"Added ledger cell event handlers to: {rowName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding ledger cell event handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over a ledger cell
        /// </summary>
        private static void OnLedgerCellHover(LedgerListItemController controller, string rowName, int categoryIndex)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Get the column name
                if (!Enum.IsDefined(typeof(LedgerEntryCategory), categoryIndex))
                    return;

                LedgerEntryCategory category = (LedgerEntryCategory)categoryIndex;
                if (!CategoryNames.TryGetValue(category, out string columnName))
                    columnName = category.ToString();

                // Get the cell text value
                string cellValue = controller.ledgerEntry[categoryIndex]?.text ?? "";

                // Skip empty cells
                if (string.IsNullOrWhiteSpace(cellValue))
                    return;

                // Clean the value text (remove TextMeshPro and HTML tags)
                cellValue = TISpeechMod.CleanText(cellValue);

                if (string.IsNullOrWhiteSpace(cellValue))
                    return;

                // Build the announcement
                string announcement = $"{rowName}, {columnName}: {cellValue}";

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastLedgerCellText && (currentTime - lastLedgerCellTime) < LEDGER_DEBOUNCE_TIME)
                    return;

                lastLedgerCellText = announcement;
                lastLedgerCellTime = currentTime;

                // Announce the cell content
                TISpeechMod.Speak(announcement, interrupt: true);

                MelonLogger.Msg($"Ledger cell hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ledger cell hover handler: {ex.Message}");
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
