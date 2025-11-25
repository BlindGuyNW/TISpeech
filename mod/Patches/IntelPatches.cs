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
    /// Harmony patches for the Intel screen
    /// - Adds hover handlers to faction grid items for accessibility
    /// - Adds hover handlers to councilor, project, objective, and relations list items
    /// - Adds hover handlers for Global tab (environment, commodity prices, wars, etc.)
    /// - Adds hover handlers for Alien tab (alien councilors, fleets, habs)
    /// - Provides faction context for all announcements
    /// </summary>
    [HarmonyPatch]
    public class IntelPatches
    {
        private static string lastIntelText = "";
        private static float lastIntelTime = 0f;
        private const float INTEL_DEBOUNCE_TIME = 0.3f;

        #region IntelScreenController Main Tab Patches

        /// <summary>
        /// Patch Show to add handlers to main tab buttons
        /// NOTE: We add handlers to the TAB BUTTONS, not the text labels.
        /// Adding EventTrigger to text labels blocks click propagation to the parent Button.
        /// </summary>
        [HarmonyPatch(typeof(IntelScreenController), "Show")]
        [HarmonyPostfix]
        public static void IntelScreen_Show_Postfix(IntelScreenController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to main tab BUTTONS (not text labels!)
                // Using TabbedPaneController.TabButton to get the actual button
                AddTabButtonHoverHandler(__instance.alienTab?.TabButton, __instance.alienTabText);
                AddTabButtonHoverHandler(__instance.factionTab?.TabButton, __instance.factionTabText);
                AddTabButtonHoverHandler(__instance.globalTab?.TabButton, __instance.globalTabText);
                AddTabButtonHoverHandler(__instance.spaceBodyTab?.TabButton, __instance.spaceBodyTabText);
                AddTabButtonHoverHandler(__instance.transferTab?.TabButton, __instance.transferPlannerTabText);
                AddTabButtonHoverHandler(__instance.habSiteTab?.TabButton, __instance.habSiteTabText);

                MelonLogger.Msg("Added Intel screen main tab handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelScreenController.Show patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a tab button that reads text from the associated text label.
        /// This avoids blocking clicks by not adding EventTrigger to the text child.
        /// </summary>
        private static void AddTabButtonHoverHandler(Button button, TMP_Text textLabel)
        {
            try
            {
                if (button == null || textLabel == null)
                    return;

                // Check if we already have a PointerEnter handler
                EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    // Only remove existing PointerEnter triggers
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                // Capture for closure
                TMP_Text capturedLabel = textLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnTabButtonHover(capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding tab button hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a tab button
        /// </summary>
        private static void OnTabButtonHover(TMP_Text textLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textLabel == null)
                    return;

                string text = TISpeechMod.CleanText(textLabel.text);
                if (string.IsNullOrWhiteSpace(text))
                    return;

                string announcement = $"Tab: {text}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Intel tab hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in tab button hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch RefreshGlobalTab to add handlers to global data fields
        /// </summary>
        [HarmonyPatch(typeof(IntelScreenController), "RefreshGlobalTab")]
        [HarmonyPostfix]
        public static void RefreshGlobalTab_Postfix(IntelScreenController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                AddGlobalTabHandlers(__instance);
                MelonLogger.Msg("Added Global tab handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelScreenController.RefreshGlobalTab patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add handlers to all Global tab fields
        /// </summary>
        private static void AddGlobalTabHandlers(IntelScreenController controller)
        {
            // Public Opinion section
            AddIntelTextHoverHandler(controller.globalPublicOpinionHeader, "Global", "Section");
            AddIntelTextHoverHandler(controller.globalPublicOpinionBreakdown, "Global", "Public Opinion");

            // Environmental Damage section
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamageHeader, "Global", "Section");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_GTA, "Environment", "Temperature Anomaly");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_GSLA, "Environment", "Sea Level Anomaly");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_MAGDPI, "Environment", "GDP Impact");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ACD, "Environment", "Carbon Dioxide Label");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ACDC, "Environment", "CO2 Current");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ACDS, "Environment", "CO2 Safe Level");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ACDY, "Environment", "CO2 Previous");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_AM, "Environment", "Methane Label");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_AMC, "Environment", "Methane Current");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_AMS, "Environment", "Methane Safe Level");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_AMY, "Environment", "Methane Previous");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ANO, "Environment", "Nitrous Oxide Label");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ANOC, "Environment", "N2O Current");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ANOS, "Environment", "N2O Safe Level");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ANOY, "Environment", "N2O Previous");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ESA, "Environment", "Aerosols Label");
            AddIntelTextHoverHandler(controller.globalEnvironmentalDamage_ESAC, "Environment", "Aerosols Current");

            // Commodity Prices section
            AddIntelTextHoverHandler(controller.globalCommodityPricesHeader, "Global", "Section");
            AddIntelTextHoverHandler(controller.globalCommodityPricesHeaderDescription, "Prices", "Description");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Water, "Prices", "Water");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Volatiles, "Prices", "Volatiles");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Metals, "Prices", "Metals");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_NobleMetals, "Prices", "Noble Metals");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Fissiles, "Prices", "Fissiles");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Antimatter, "Prices", "Antimatter");
            AddIntelTextHoverHandler(controller.globalCommodityPricesText_Exotics, "Prices", "Exotics");

            // Wars section
            AddIntelTextHoverHandler(controller.globalWarsHeader, "Global", "Section");

            // Atrocities section
            AddIntelTextHoverHandler(controller.globalAtrocitiesHeader, "Global", "Section");

            // Global Data section
            AddIntelTextHoverHandler(controller.globalDataHeader, "Global", "Section");
            AddIntelTextHoverHandler(controller.globalData_EarthPop, "Global Data", "Earth Population");
            AddIntelTextHoverHandler(controller.globalData_SpacePop, "Global Data", "Space Population");
            AddIntelTextHoverHandler(controller.globalData_GDP, "Global Data", "GDP");
            AddIntelTextHoverHandler(controller.globalData_PerCapitaGDP, "Global Data", "Per Capita GDP");
        }

        #endregion

        #region IntelFactionGridItemController Patches

        /// <summary>
        /// Patch Initialize to add hover handlers to faction grid items
        /// This is called when each faction's grid item is set up
        /// </summary>
        [HarmonyPatch(typeof(IntelFactionGridItemController), "Initialize")]
        [HarmonyPostfix]
        public static void Initialize_Postfix(IntelFactionGridItemController __instance, TIFactionState faction)
        {
            try
            {
                if (!TISpeechMod.IsReady || faction == null)
                    return;

                AddFactionGridItemHandlers(__instance, faction);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelFactionGridItemController.Initialize patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Refresh to update handlers when faction data changes
        /// This ensures the text values are current when announced
        /// </summary>
        [HarmonyPatch(typeof(IntelFactionGridItemController), "Refresh")]
        [HarmonyPostfix]
        public static void Refresh_Postfix(IntelFactionGridItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Get faction name from the text field since the faction field is private
                string factionName = GetFactionName(__instance);

                // Re-add handlers to ensure they have current text values
                AddFactionHeaderHandlers(__instance, factionName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelFactionGridItemController.Refresh patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch RefreshResources to add handlers to resource fields
        /// This is called when the Resources tab is selected
        /// </summary>
        [HarmonyPatch(typeof(IntelFactionGridItemController), "RefreshResources")]
        [HarmonyPostfix]
        public static void RefreshResources_Postfix(IntelFactionGridItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                string factionName = GetFactionName(__instance);
                AddResourceHandlers(__instance, factionName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelFactionGridItemController.RefreshResources patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the faction name from the grid item's text field
        /// </summary>
        private static string GetFactionName(IntelFactionGridItemController controller)
        {
            if (controller.factionName != null && !string.IsNullOrEmpty(controller.factionName.text))
            {
                return TISpeechMod.CleanText(controller.factionName.text);
            }
            return "Unknown Faction";
        }

        /// <summary>
        /// Add all handlers to a faction grid item
        /// </summary>
        private static void AddFactionGridItemHandlers(IntelFactionGridItemController controller, TIFactionState faction)
        {
            try
            {
                string factionName = faction.displayNameCapitalized;

                // Header fields
                AddFactionHeaderHandlers(controller, factionName);

                // Resource fields (will be updated when tab is selected)
                AddResourceHandlers(controller, factionName);

                // Tab buttons
                AddTabButtonHandlers(controller, factionName);

                MelonLogger.Msg($"Added Intel grid item handlers for {factionName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding faction grid item handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Add handlers to faction header fields
        /// </summary>
        private static void AddFactionHeaderHandlers(IntelFactionGridItemController controller, string factionName)
        {
            // Faction name - just announce the name without label
            AddIntelTextHoverHandler(controller.factionName, factionName, "Faction");

            // Leader, goal, victory condition
            AddIntelTextHoverHandler(controller.factionLeader, factionName, "Leader");
            AddIntelTextHoverHandler(controller.factionGoal, factionName, "Goal");
            AddIntelTextHoverHandler(controller.factionVictory, factionName, "Victory Condition");

            // Penetration status
            AddIntelTextHoverHandler(controller.penetratedLabel, factionName, "Status");
        }

        /// <summary>
        /// Add handlers to resource fields
        /// </summary>
        private static void AddResourceHandlers(IntelFactionGridItemController controller, string factionName)
        {
            // Core resources
            AddIntelTextHoverHandler(controller.money, factionName, "Money");
            AddIntelTextHoverHandler(controller.influence, factionName, "Influence");
            AddIntelTextHoverHandler(controller.ops, factionName, "Operations");
            AddIntelTextHoverHandler(controller.boost, factionName, "Boost");
            AddIntelTextHoverHandler(controller.missionControl, factionName, "Mission Control");
            AddIntelTextHoverHandler(controller.research, factionName, "Research");
            AddIntelTextHoverHandler(controller.projects, factionName, "Projects");
            AddIntelTextHoverHandler(controller.controlPoints, factionName, "Control Points");

            // Space resources
            AddIntelTextHoverHandler(controller.water, factionName, "Water");
            AddIntelTextHoverHandler(controller.volatiles, factionName, "Volatiles");
            AddIntelTextHoverHandler(controller.metals, factionName, "Metals");
            AddIntelTextHoverHandler(controller.nobles, factionName, "Noble Metals");
            AddIntelTextHoverHandler(controller.fertiles, factionName, "Fissiles");
            AddIntelTextHoverHandler(controller.antimatter, factionName, "Antimatter");
            AddIntelTextHoverHandler(controller.exotics, factionName, "Exotics");
        }

        /// <summary>
        /// Add handlers to tab buttons to announce tab changes
        /// </summary>
        private static void AddTabButtonHandlers(IntelFactionGridItemController controller, string factionName)
        {
            AddIntelTextHoverHandler(controller.councilorsTabTitle, factionName, "Tab");
            AddIntelTextHoverHandler(controller.resourcesTabTitle, factionName, "Tab");
            AddIntelTextHoverHandler(controller.objectivesTabTitle, factionName, "Tab");
            AddIntelTextHoverHandler(controller.relationsTabTitle, factionName, "Tab");
            AddIntelTextHoverHandler(controller.techTabTitle, factionName, "Tab");
        }

        #endregion

        #region IntelCouncilorListItem Patches

        /// <summary>
        /// Patch UpdateListItem to add hover handlers to councilor list items
        /// </summary>
        [HarmonyPatch(typeof(IntelCouncilorListItem), "UpdateListItem")]
        [HarmonyPostfix]
        public static void CouncilorListItem_UpdateListItem_Postfix(IntelCouncilorListItem __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Get faction context from parent grid item
                string factionName = GetParentFactionName(__instance.transform);
                string councilorName = TISpeechMod.CleanText(__instance.councilorName?.text ?? "Councilor");

                // Create combined context: "Faction, Councilor Name"
                string context = $"{factionName}, {councilorName}";

                AddIntelTextHoverHandler(__instance.councilorName, factionName, "Councilor");
                AddIntelTextHoverHandler(__instance.councilorJob, context, "Job");
                AddIntelTextHoverHandler(__instance.location, context, "Location");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelCouncilorListItem.UpdateListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region IntelProjectsListItemController Patches

        /// <summary>
        /// Patch SetListItem to add hover handlers to project list items
        /// </summary>
        [HarmonyPatch(typeof(IntelProjectsListItemController), "SetListItem")]
        [HarmonyPostfix]
        public static void ProjectsListItem_SetListItem_Postfix(IntelProjectsListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                string factionName = GetParentFactionName(__instance.transform);
                AddIntelTextHoverHandler(__instance.projectName, factionName, "Project");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelProjectsListItemController.SetListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region IntelObjectiveListItemController Patches

        /// <summary>
        /// Patch SetListItem to add hover handlers to objective list items
        /// </summary>
        [HarmonyPatch(typeof(IntelObjectiveListItemController), "SetListItem")]
        [HarmonyPostfix]
        public static void ObjectiveListItem_SetListItem_Postfix(IntelObjectiveListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                string factionName = GetParentFactionName(__instance.transform);
                AddIntelTextHoverHandler(__instance.objectiveTitle, factionName, "Objective");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelObjectiveListItemController.SetListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region IntelFactionRelationsGridItemController Patches

        /// <summary>
        /// Patch SetListItem to add hover handlers to relations grid items
        /// </summary>
        [HarmonyPatch(typeof(IntelFactionRelationsGridItemController), "SetListItem")]
        [HarmonyPostfix]
        public static void RelationsGridItem_SetListItem_Postfix(IntelFactionRelationsGridItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                string factionName = GetParentFactionName(__instance.transform);
                AddIntelTextHoverHandler(__instance.attitudeDescription, factionName, "Relation");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelFactionRelationsGridItemController.SetListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region Global Tab List Item Patches

        /// <summary>
        /// Patch PublicOpinionListItemController.InitListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(PublicOpinionListItemController), "InitListItem")]
        [HarmonyPostfix]
        public static void PublicOpinion_InitListItem_Postfix(PublicOpinionListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                AddIntelTextHoverHandler(__instance.factionNameText, "Public Opinion", "Faction");
                AddIntelTextHoverHandler(__instance.factionObjectiveText, "Public Opinion", "Ideology");
                AddIntelTextHoverHandler(__instance.globalPercentageText, "Public Opinion", "Percentage");

                MelonLogger.Msg("Added Public Opinion list item handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PublicOpinionListItemController.InitListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelWarsListItemController.SetListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelWarsListItemController), "SetListItem")]
        [HarmonyPostfix]
        public static void WarsListItem_SetListItem_Postfix(IntelWarsListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                AddIntelTextHoverHandler(__instance.warName, "War", "Name");
                AddIntelTextHoverHandler(__instance.warActiveText, "War", "Status");
                AddIntelTextHoverHandler(__instance.warDurationText, "War", "Duration");
                AddIntelTextHoverHandler(__instance.attackerLeaderNation, "War", "Attacker");
                AddIntelTextHoverHandler(__instance.defenderLeaderNation, "War", "Defender");
                AddIntelTextHoverHandler(__instance.attackerArmiesText, "War", "Attacker Armies");
                AddIntelTextHoverHandler(__instance.defenderArmiesText, "War", "Defender Armies");

                MelonLogger.Msg("Added Wars list item handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelWarsListItemController.SetListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAtrocitiesGridItemController.InitListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAtrocitiesGridItemController), "InitListItem")]
        [HarmonyPostfix]
        public static void AtrocitiesGridItem_InitListItem_Postfix(IntelAtrocitiesGridItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Get faction name from the faction property
                string factionName = __instance.faction?.displayNameCapitalized ?? "Unknown";
                AddIntelTextHoverHandler(__instance.numAtrocities, factionName, "Atrocities");

                MelonLogger.Msg($"Added Atrocities grid item handlers for {factionName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAtrocitiesGridItemController.InitListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region Alien Tab Patches

        /// <summary>
        /// Patch RefreshAlienTab to add handlers to alien tab headers
        /// </summary>
        [HarmonyPatch(typeof(IntelScreenController), "RefreshAlienTab")]
        [HarmonyPostfix]
        public static void RefreshAlienTab_Postfix(IntelScreenController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Add handlers to section headers
                AddIntelTextHoverHandler(__instance.alienCouncilorsHeaderText, "Alien", "Section");
                AddIntelTextHoverHandler(__instance.alienEventsHeaderText, "Alien", "Section");
                AddIntelTextHoverHandler(__instance.alienSitesHeaderText, "Alien", "Section");
                AddIntelTextHoverHandler(__instance.alienFleetsHeaderText, "Alien", "Section");
                AddIntelTextHoverHandler(__instance.alienHabsHeaderText, "Alien", "Section");

                MelonLogger.Msg("Added Alien tab header handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelScreenController.RefreshAlienTab patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAlienCouncilorListItemController.UpdateListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAlienCouncilorListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void AlienCouncilorListItem_UpdateListItem_Postfix(IntelAlienCouncilorListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                string councilorName = TISpeechMod.CleanText(__instance.councilorName?.text ?? "Alien Councilor");
                AddIntelTextHoverHandler(__instance.councilorName, "Alien", "Councilor");
                AddIntelTextHoverHandler(__instance.councilorLocation, $"Alien, {councilorName}", "Location");

                MelonLogger.Msg($"Added Alien councilor handlers for {councilorName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAlienCouncilorListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAlienFleetListItemController.UpdateListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAlienFleetListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void AlienFleetListItem_UpdateListItem_Postfix(IntelAlienFleetListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Fields: fleetName, location
                string name = TISpeechMod.CleanText(__instance.fleetName?.text ?? "Alien Fleet");
                AddIntelTextHoverHandler(__instance.fleetName, "Alien", "Fleet");
                AddIntelTextHoverHandler(__instance.location, $"Alien, {name}", "Location");

                MelonLogger.Msg($"Added Alien fleet handlers for {name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAlienFleetListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAlienHabListItemController.UpdateListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAlienHabListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void AlienHabListItem_UpdateListItem_Postfix(IntelAlienHabListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Fields: habName, habLocation
                string name = TISpeechMod.CleanText(__instance.habName?.text ?? "Alien Hab");
                AddIntelTextHoverHandler(__instance.habName, "Alien", "Hab");
                AddIntelTextHoverHandler(__instance.habLocation, $"Alien, {name}", "Location");

                MelonLogger.Msg($"Added Alien hab handlers for {name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAlienHabListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAlienEarthAssetListItemController.UpdateListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAlienEarthAssetListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void AlienEarthAssetListItem_UpdateListItem_Postfix(IntelAlienEarthAssetListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Fields: assetName, regionName
                string name = TISpeechMod.CleanText(__instance.assetName?.text ?? "Alien Asset");
                AddIntelTextHoverHandler(__instance.assetName, "Alien", "Earth Asset");
                AddIntelTextHoverHandler(__instance.regionName, $"Alien, {name}", "Region");

                MelonLogger.Msg($"Added Alien earth asset handlers for {name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAlienEarthAssetListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch IntelAlienEventListItemController.UpdateListItem to add hover handlers
        /// </summary>
        [HarmonyPatch(typeof(IntelAlienEventListItemController), "UpdateListItem")]
        [HarmonyPostfix]
        public static void AlienEventListItem_UpdateListItem_Postfix(IntelAlienEventListItemController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Field: eventSummary
                AddIntelTextHoverHandler(__instance.eventSummary, "Alien", "Event");

                MelonLogger.Msg("Added Alien event handlers");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelAlienEventListItemController.UpdateListItem patch: {ex.Message}");
            }
        }

        #endregion

        #region SpaceBody Tab Patches

        /// <summary>
        /// Patch IntelScreenSpacebodyListItemViewsHolder.UpdateFromModel to add hover handlers for space body list items
        /// NOTE: The Space Body tab uses OSA (Optimized ScrollView Adapter), which creates/recycles views dynamically.
        /// We can't patch IntelSpaceBodyListItemController.Initialize directly because it references AssetCacheManager,
        /// which causes a TypeInitializationException during mod loading.
        /// UpdateFromModel is called by OSA for each visible item and is safe to patch.
        /// </summary>
        [HarmonyPatch(typeof(IntelScreenSpacebodyListItemViewsHolder), "UpdateFromModel")]
        [HarmonyPostfix]
        public static void IntelScreenSpacebodyListItemViewsHolder_UpdateFromModel_Postfix(IntelScreenSpacebodyListItemViewsHolder __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                IntelSpaceBodyListItemController controller = __instance.IntelScreenSpacebodyListItem;
                if (controller == null || controller.spaceBody == null)
                    return;

                AddSpaceBodyItemHandlers(controller);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IntelScreenSpacebodyListItemViewsHolder.UpdateFromModel patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Add all hover handlers to a space body list item
        /// </summary>
        private static void AddSpaceBodyItemHandlers(IntelSpaceBodyListItemController controller)
        {
            TISpaceBodyState spaceBody = controller.spaceBody;
            string bodyName = spaceBody.displayName;

            // Main identification fields
            AddSpaceBodyTextHoverHandler(controller.spaceBodyName, bodyName, "Name");
            AddSpaceBodyTextHoverHandler(controller.description, bodyName, "Type");
            AddSpaceBodyTextHoverHandler(controller.description2, bodyName, "Mining Potential");

            // Orbital and physical data
            AddSpaceBodyTextHoverHandler(controller.orbitSemimajor_Axis, bodyName, "Orbit");
            AddSpaceBodyTextHoverHandler(controller.dimensions, bodyName, "Size");
            AddSpaceBodyTextHoverHandler(controller.habSitesCount, bodyName, "Hab Sites");
            AddSpaceBodyTextHoverHandler(controller.earthLaunchWindow, bodyName, "Launch Window");

            // Resource icons - announce the resource type and rating
            AddSpaceBodyResourceIconHandler(controller.waterValueIcon, bodyName, "Water", spaceBody);
            AddSpaceBodyResourceIconHandler(controller.volatilesValueIcon, bodyName, "Volatiles", spaceBody);
            AddSpaceBodyResourceIconHandler(controller.metalsValueIcon, bodyName, "Metals", spaceBody);
            AddSpaceBodyResourceIconHandler(controller.noblesValueIcon, bodyName, "Noble Metals", spaceBody);
            AddSpaceBodyResourceIconHandler(controller.fertilesValueIcon, bodyName, "Fissiles", spaceBody);
            AddSpaceBodyResourceIconHandler(controller.solarValueIcon, bodyName, "Solar", spaceBody);

            // Prospecting button
            if (controller.orderProspecting != null)
            {
                AddSpaceBodyButtonHandler(controller.orderProspecting, bodyName, "Order Prospecting");
            }

            // Space body icon (for clicking to go to location)
            if (controller.spaceBodyIcon != null)
            {
                AddSpaceBodyImageHandler(controller.spaceBodyIcon, bodyName, "Go To");
            }
        }

        /// <summary>
        /// Add hover handler to a space body text field
        /// </summary>
        private static void AddSpaceBodyTextHoverHandler(TMP_Text textField, string bodyName, string fieldLabel)
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

                string capturedBodyName = bodyName;
                string capturedLabel = fieldLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnSpaceBodyTextHover(textField, capturedBodyName, capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding space body text hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a space body resource icon
        /// </summary>
        private static void AddSpaceBodyResourceIconHandler(Image icon, string bodyName, string resourceName, TISpaceBodyState spaceBody)
        {
            try
            {
                if (icon == null || icon.gameObject == null)
                    return;

                EventTrigger trigger = icon.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = icon.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                string capturedBodyName = bodyName;
                string capturedResource = resourceName;
                TISpaceBodyState capturedSpaceBody = spaceBody;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnSpaceBodyResourceHover(capturedBodyName, capturedResource, capturedSpaceBody));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding space body resource hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a space body button
        /// </summary>
        private static void AddSpaceBodyButtonHandler(Button button, string bodyName, string buttonLabel)
        {
            try
            {
                if (button == null || button.gameObject == null)
                    return;

                EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                string capturedBodyName = bodyName;
                string capturedLabel = buttonLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnSpaceBodyButtonHover(capturedBodyName, capturedLabel, button.interactable));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding space body button hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Add hover handler to a space body image (like the icon)
        /// </summary>
        private static void AddSpaceBodyImageHandler(Image image, string bodyName, string imageLabel)
        {
            try
            {
                if (image == null || image.gameObject == null)
                    return;

                EventTrigger trigger = image.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = image.gameObject.AddComponent<EventTrigger>();
                }
                else
                {
                    trigger.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
                }

                string capturedBodyName = bodyName;
                string capturedLabel = imageLabel;

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnSpaceBodyImageHover(capturedBodyName, capturedLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding space body image hover handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a space body text field
        /// </summary>
        private static void OnSpaceBodyTextHover(TMP_Text textField, string bodyName, string fieldLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady || textField == null)
                    return;

                string value = TISpeechMod.CleanText(textField.text);
                if (string.IsNullOrWhiteSpace(value))
                    return;

                string announcement;
                if (fieldLabel == "Name")
                {
                    announcement = value;
                }
                else
                {
                    announcement = $"{fieldLabel}: {value}";
                }

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Space body text hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in space body text hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a space body resource icon
        /// </summary>
        private static void OnSpaceBodyResourceHover(string bodyName, string resourceName, TISpaceBodyState spaceBody)
        {
            try
            {
                if (!TISpeechMod.IsReady || spaceBody == null)
                    return;

                // Get the resource rating
                string rating = "Unknown";
                bool prospected = GameControl.control?.activePlayer?.Prospected(spaceBody) ?? false;

                if (spaceBody.habSites.Length > 0)
                {
                    SiteProfileRating profileRating = SiteProfileRating.empty;

                    switch (resourceName)
                    {
                        case "Water":
                            profileRating = spaceBody.GetSiteProfileRating(FactionResource.Water, prospected);
                            break;
                        case "Volatiles":
                            profileRating = spaceBody.GetSiteProfileRating(FactionResource.Volatiles, prospected);
                            break;
                        case "Metals":
                            profileRating = spaceBody.GetSiteProfileRating(FactionResource.Metals, prospected);
                            break;
                        case "Noble Metals":
                            profileRating = spaceBody.GetSiteProfileRating(FactionResource.NobleMetals, prospected);
                            break;
                        case "Fissiles":
                            profileRating = spaceBody.GetSiteProfileRating(FactionResource.Fissiles, prospected);
                            break;
                    }

                    // Convert enum to human-readable string
                    switch (profileRating)
                    {
                        case SiteProfileRating.empty: rating = "None"; break;
                        case SiteProfileRating.possible: rating = "Possible"; break;
                        case SiteProfileRating.low: rating = "Low"; break;
                        case SiteProfileRating.medium: rating = "Medium"; break;
                        case SiteProfileRating.high: rating = "High"; break;
                        case SiteProfileRating.max: rating = "Maximum"; break;
                        default: rating = profileRating.ToString(); break;
                    }
                }
                else if (resourceName == "Solar")
                {
                    float solarMultiplier = TIHabModuleState.NaturalSolarPowerMultiplier(
                        spaceBody.orbits.Count > 0 ? spaceBody.orbits[0] : null);
                    rating = $"{(solarMultiplier * 100):F0}%";
                }

                string announcement = $"{resourceName}: {rating}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Space body resource hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in space body resource hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a space body button
        /// </summary>
        private static void OnSpaceBodyButtonHover(string bodyName, string buttonLabel, bool interactable)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string announcement = interactable
                    ? $"{buttonLabel} button"
                    : $"{buttonLabel} button, unavailable";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Space body button hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in space body button hover: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when hovering over a space body image
        /// </summary>
        private static void OnSpaceBodyImageHover(string bodyName, string imageLabel)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                string announcement = $"{bodyName}, {imageLabel}";

                // Debounce
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                TISpeechMod.Speak(announcement, interrupt: false);
                MelonLogger.Msg($"Space body image hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in space body image hover: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Traverse up the transform hierarchy to find the parent faction grid item and get its faction name
        /// </summary>
        private static string GetParentFactionName(Transform transform)
        {
            try
            {
                Transform current = transform;
                while (current != null)
                {
                    IntelFactionGridItemController gridItem = current.GetComponent<IntelFactionGridItemController>();
                    if (gridItem != null)
                    {
                        return GetFactionName(gridItem);
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting parent faction name: {ex.Message}");
            }
            return "Unknown Faction";
        }

        /// <summary>
        /// Add a hover event handler to an Intel screen text field
        /// </summary>
        private static void AddIntelTextHoverHandler(TMP_Text textField, string context, string fieldLabel)
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
                string capturedContext = context;
                string capturedFieldLabel = fieldLabel;

                // Add pointer enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => OnIntelTextHover(textField, capturedContext, capturedFieldLabel));
                trigger.triggers.Add(enterEntry);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding hover handler to {fieldLabel}: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the user hovers over an Intel screen text field
        /// </summary>
        private static void OnIntelTextHover(TMP_Text textField, string context, string fieldLabel)
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

                // Build the announcement based on field type
                string announcement;
                if (fieldLabel == "Faction")
                {
                    // For faction name, just announce the name
                    announcement = value;
                }
                else if (fieldLabel == "Tab")
                {
                    // For tabs, announce: "{Faction}, {Tab Name}"
                    announcement = $"{context}, {value}";
                }
                else
                {
                    // For other fields: "{Context}, {Field Label}: {Value}"
                    announcement = $"{context}, {fieldLabel}: {value}";
                }

                // Debounce to prevent rapid re-announcement
                float currentTime = Time.unscaledTime;
                if (announcement == lastIntelText && (currentTime - lastIntelTime) < INTEL_DEBOUNCE_TIME)
                    return;

                lastIntelText = announcement;
                lastIntelTime = currentTime;

                // Announce with interrupt: false so tooltips take priority
                TISpeechMod.Speak(announcement, interrupt: false);

                MelonLogger.Msg($"Intel hover: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Intel text hover handler: {ex.Message}");
            }
        }

        #endregion
    }
}
