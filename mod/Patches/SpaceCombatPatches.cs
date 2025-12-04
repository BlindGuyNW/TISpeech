using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.SpaceCombat.UI;
using TISpeech.ReviewMode;

namespace TISpeech.Patches
{
    /// <summary>
    /// Harmony patches for space combat accessibility.
    /// Detects when pre-combat begins and notifies Review Mode.
    ///
    /// NOTE: We patch OpenStanceUI/OpenBiddingUI/OpenResolutionUI instead of Show/Hide
    /// because Show/Hide are defined in the base class CanvasControllerBase, and Harmony
    /// may not correctly patch inherited virtual methods on derived types.
    ///
    /// We also patch TIPromptQueueState.PostAllStartUpInit_5 to detect combat prompts
    /// on game load - the game only handles nation prompts on load, not faction prompts
    /// like space combat prompts.
    /// </summary>
    public static class SpaceCombatPatches
    {
        /// <summary>
        /// Combat-related prompt names that indicate pre-combat UI should be shown.
        /// </summary>
        private static readonly string[] CombatPromptNames = new[]
        {
            "PromptBeginCombat",
            "PromptSelectSpaceCombatStance",
            "PromptSelectSpaceCombatBid"
        };

        /// <summary>
        /// Flag set when combat is pending on game load.
        /// Checked by ReviewModeController when activating.
        /// </summary>
        public static bool CombatPendingOnLoad { get; private set; } = false;

        /// <summary>
        /// Patch TIPromptQueueState.PostAllStartUpInit_5 to detect combat prompts on game load.
        /// The game only triggers BlockingPromptOnStartup for nation prompts, not faction prompts
        /// like space combat. This patch fills that gap.
        /// </summary>
        [HarmonyPatch(typeof(TIPromptQueueState), "PostAllStartUpInit_5")]
        [HarmonyPostfix]
        public static void TIPromptQueueState_PostAllStartUpInit_5_Postfix(TIPromptQueueState __instance)
        {
            try
            {
                // Reset flag on each game load
                CombatPendingOnLoad = false;

                // Check for combat-related faction prompts
                var factionPrompts = __instance.activePlayerFactionPromptList;
                if (factionPrompts == null || factionPrompts.Count == 0)
                {
                    MelonLogger.Msg("PostAllStartUpInit_5: No faction prompts");
                    return;
                }

                MelonLogger.Msg($"PostAllStartUpInit_5: Found {factionPrompts.Count} faction prompts: {string.Join(", ", factionPrompts.Select(p => p.name))}");

                bool hasCombatPrompt = factionPrompts.Any(p => CombatPromptNames.Contains(p.name));
                if (!hasCombatPrompt)
                {
                    MelonLogger.Msg("PostAllStartUpInit_5: No combat prompts found");
                    return;
                }

                MelonLogger.Msg("Combat prompt detected on game load - setting CombatPendingOnLoad flag");
                CombatPendingOnLoad = true;

                // Speak announcement (TISpeechMod.IsReady should be true by now)
                if (TISpeechMod.IsReady)
                {
                    TISpeechMod.Speak("Space combat pending. Press Numpad 0 or Ctrl+R to navigate.", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TIPromptQueueState.PostAllStartUpInit_5 patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Called by ReviewModeController when activating to check if combat mode should be entered.
        /// </summary>
        public static bool CheckAndClearCombatPendingFlag()
        {
            if (CombatPendingOnLoad)
            {
                MelonLogger.Msg("Combat was pending on load - entering combat mode");
                CombatPendingOnLoad = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Patch PrecombatController.OpenStanceUI to detect when stance selection UI opens.
        /// This is the first phase of pre-combat where player chooses Engage/Defend/Evade.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "OpenStanceUI")]
        [HarmonyPostfix]
        public static void PrecombatController_OpenStanceUI_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat stance selection UI opened");

                // Get combat state info for announcement
                var combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null)
                {
                    MelonLogger.Msg("OpenStanceUI: No active combat state");
                    return;
                }

                var player = GameControl.control?.activePlayer;
                if (player == null || !combat.factions.Contains(player))
                {
                    // Player not involved, just announce
                    TISpeechMod.Speak("Space combat detected", interrupt: true);
                    return;
                }

                // Auto-activate Review Mode and enter combat mode (same behavior as notifications)
                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null)
                {
                    // EnterCombatMode will auto-activate Review Mode if not already active
                    reviewMode.EnterCombatMode();
                }
                else
                {
                    // Fallback if ReviewModeController not initialized
                    var ourFleet = combat.FleetFor(player);
                    var theirFleet = combat.FleetAgainst(player);

                    string announcement = "Space combat! ";
                    if (ourFleet != null)
                        announcement += $"Your fleet: {ourFleet.ships.Count} ships. ";
                    if (theirFleet != null)
                        announcement += $"Enemy: {theirFleet.ships.Count} ships. ";
                    announcement += "Select stance: Engage, Defend, or Evade.";
                    TISpeechMod.Speak(announcement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.OpenStanceUI patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.OpenBiddingUI to detect when bidding phase begins.
        /// This is called when both sides have selected stances and a pursuit/evasion occurs.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "OpenBiddingUI")]
        [HarmonyPostfix]
        public static void PrecombatController_OpenBiddingUI_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat bidding UI opened");

                var combat = TISpaceCombatState.CurrentActiveCombat;
                var player = GameControl.control?.activePlayer;
                if (combat == null || player == null)
                    return;

                // Check player's stance to determine if chasing or fleeing
                bool isEvading = false;
                if (combat.stances.TryGetValue(player, out var stance))
                {
                    isEvading = stance == CombatStance.Evade;
                }

                // Auto-activate Review Mode and enter/refresh combat mode
                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null)
                {
                    // EnterCombatMode will auto-activate Review Mode if not already active
                    reviewMode.EnterCombatMode();
                }
                else
                {
                    string phaseAnnouncement = isEvading
                        ? "Bidding phase. Set delta-V for escape attempt."
                        : "Bidding phase. Set delta-V for pursuit.";
                    TISpeechMod.Speak(phaseAnnouncement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.OpenBiddingUI patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.OpenResolutionUI to detect when resolution phase begins.
        /// This is when player chooses Auto-resolve vs Live combat.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "OpenResolutionUI")]
        [HarmonyPostfix]
        public static void PrecombatController_OpenResolutionUI_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat resolution UI opened");

                var combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null)
                    return;

                // Auto-activate Review Mode and enter/refresh combat mode
                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null)
                {
                    // EnterCombatMode will auto-activate Review Mode if not already active
                    reviewMode.EnterCombatMode();
                }
                else
                {
                    bool combatOccurs = combat.combatOccurs;
                    string phaseAnnouncement = combatOccurs
                        ? "Select battle resolution: Auto-resolve or Fight manually."
                        : "Combat avoided. Select Close to continue.";
                    TISpeechMod.Speak(phaseAnnouncement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.OpenResolutionUI patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.EndPrecombatInteraction to detect when pre-combat ends.
        /// NOTE: We do NOT exit combat mode here - wait for the results screen to be dismissed.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "EndPrecombatInteraction")]
        [HarmonyPostfix]
        public static void PrecombatController_EndPrecombatInteraction_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat interaction ended - waiting for results");

                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null && reviewMode.IsInCombatMode)
                {
                    // Check if combat is now active (live combat starting)
                    var combat = TISpaceCombatState.CurrentActiveCombat;
                    if (combat != null && combat.active)
                    {
                        // Live combat starting - exit combat mode since we're entering tactical combat
                        TISpeechMod.Speak("Live combat beginning.", interrupt: true);
                        reviewMode.ExitCombatMode();
                    }
                    // Otherwise, combat will auto-resolve and results will be shown
                    // Don't exit combat mode - wait for OnClosePostCombatButtonSelected
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.EndPrecombatInteraction patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.DisplayCombatResults to announce combat outcome.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "DisplayCombatResults")]
        [HarmonyPostfix]
        public static void PrecombatController_DisplayCombatResults_Postfix(PrecombatController __instance, CombatRecord combatRecord, float progress, bool isSimulationResult)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                // Only announce when progress is complete (100%)
                if (progress < 1f && isSimulationResult)
                    return;

                MelonLogger.Msg($"DisplayCombatResults: progress={progress}, isSimulation={isSimulationResult}");

                var player = GameControl.control?.activePlayer;
                if (player == null)
                    return;

                // Build result announcement
                var sb = new System.Text.StringBuilder();
                sb.Append("Combat results. ");
                sb.Append($"{combatRecord.combatName}. ");

                if (combatRecord.singleAssetRecords == null || combatRecord.singleAssetRecords.Count == 0)
                {
                    sb.Append("No asset records available.");
                    TISpeechMod.Speak(sb.ToString(), interrupt: true);
                    return;
                }

                // Our forces
                var ourRecords = combatRecord.singleAssetRecords
                    .Where(x => x.faction == player)
                    .ToList();
                int ourDestroyed = ourRecords.Count(x => x.outcome == SingleAssetCombatOutcome.Destroyed);
                int ourFled = ourRecords.Count(x => x.fled);
                int ourSurvived = ourRecords.Count - ourDestroyed;
                if (ourRecords.Count > 0)
                {
                    sb.Append($"Your forces: {ourSurvived} survived");
                    if (ourDestroyed > 0) sb.Append($", {ourDestroyed} destroyed");
                    if (ourFled > 0) sb.Append($", {ourFled} fled");
                    sb.Append(". ");
                }

                // Enemy forces
                var enemyRecords = combatRecord.singleAssetRecords
                    .Where(x => x.faction != player)
                    .ToList();
                int enemyDestroyed = enemyRecords.Count(x => x.outcome == SingleAssetCombatOutcome.Destroyed);
                int enemyFled = enemyRecords.Count(x => x.fled);
                int enemySurvived = enemyRecords.Count - enemyDestroyed;
                if (enemyRecords.Count > 0)
                {
                    sb.Append($"Enemy forces: {enemySurvived} survived");
                    if (enemyDestroyed > 0) sb.Append($", {enemyDestroyed} destroyed");
                    if (enemyFled > 0) sb.Append($", {enemyFled} fled");
                    sb.Append(". ");
                }

                // Salvage
                if (combatRecord.winnerSalvage != null)
                {
                    sb.Append("Salvage collected. ");
                }

                sb.Append("Press OK to continue.");

                TISpeechMod.Speak(sb.ToString(), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DisplayCombatResults patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.OnClosePostCombatButtonSelected to exit combat mode when results are dismissed.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "OnClosePostCombatButtonSelected")]
        [HarmonyPostfix]
        public static void PrecombatController_OnClosePostCombatButtonSelected_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Post-combat results dismissed");

                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null && reviewMode.IsInCombatMode)
                {
                    reviewMode.ExitCombatMode();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnClosePostCombatButtonSelected patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch to announce combat stance selection by the opponent.
        /// </summary>
        [HarmonyPatch(typeof(TISpaceCombatState), "SetStance")]
        [HarmonyPostfix]
        public static void TISpaceCombatState_SetStance_Postfix(TISpaceCombatState __instance, TIFactionState faction, CombatStance stance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var player = GameControl.control?.activePlayer;
                if (player == null)
                    return;

                // Only announce opponent's stance selection
                if (faction != player)
                {
                    string stanceName = GetStanceName(stance);
                    TISpeechMod.Speak($"Enemy selected {stanceName}", interrupt: false);

                    // Refresh combat mode if active (EnterCombatMode refreshes when already in combat mode)
                    var reviewMode = ReviewModeController.Instance;
                    if (reviewMode != null && reviewMode.IsInCombatMode)
                    {
                        reviewMode.EnterCombatMode();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SetStance patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch to announce combat bid submission by the opponent.
        /// </summary>
        [HarmonyPatch(typeof(TISpaceCombatState), "SetBid")]
        [HarmonyPostfix]
        public static void TISpaceCombatState_SetBid_Postfix(TISpaceCombatState __instance, TIFactionState faction, float bid_kps)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                var player = GameControl.control?.activePlayer;
                if (player == null)
                    return;

                // Only announce opponent's bid
                if (faction != player)
                {
                    TISpeechMod.Speak($"Enemy submitted bid", interrupt: false);

                    // Refresh combat mode if active (EnterCombatMode refreshes when already in combat mode)
                    var reviewMode = ReviewModeController.Instance;
                    if (reviewMode != null && reviewMode.IsInCombatMode)
                    {
                        reviewMode.EnterCombatMode();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SetBid patch: {ex.Message}");
            }
        }

        private static string GetStanceName(CombatStance stance)
        {
            switch (stance)
            {
                case CombatStance.Pursue: return "Engage";
                case CombatStance.Defend: return "Defend";
                case CombatStance.Evade: return "Evade";
                case CombatStance.ExtendedPursuit_Envelop: return "Extended Pursuit";
                case CombatStance.ExtendedPursuit_Stretch: return "Extended Pursuit";
                default: return stance.ToString();
            }
        }
    }
}
