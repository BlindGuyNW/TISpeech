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
    /// </summary>
    public static class SpaceCombatPatches
    {
        /// <summary>
        /// Patch PrecombatController.Show to detect when pre-combat UI opens.
        /// This triggers combat mode entry in Review Mode.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "Show")]
        [HarmonyPostfix]
        public static void PrecombatController_Show_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat screen opened");

                // Get combat state info for announcement
                var combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null)
                {
                    MelonLogger.Msg("No active combat state");
                    return;
                }

                var player = GameControl.control?.activePlayer;
                if (player == null || !combat.factions.Contains(player))
                {
                    // Player not involved, just announce
                    TISpeechMod.Speak("Space combat detected", interrupt: true);
                    return;
                }

                // Check if Review Mode is active
                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null && reviewMode.IsActive)
                {
                    // Automatically enter combat mode
                    reviewMode.EnterCombatMode();
                }
                else
                {
                    // Just announce combat started
                    var ourFleet = combat.FleetFor(player);
                    var theirFleet = combat.FleetAgainst(player);

                    string announcement = "Space combat! ";
                    if (ourFleet != null)
                        announcement += $"Your fleet: {ourFleet.ships.Count} ships. ";
                    if (theirFleet != null)
                        announcement += $"Enemy: {theirFleet.ships.Count} ships. ";

                    announcement += "Enter Review Mode (Numpad 0 or Ctrl+R) to navigate combat options.";

                    TISpeechMod.Speak(announcement, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.Show patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch PrecombatController.Hide to detect when pre-combat ends.
        /// </summary>
        [HarmonyPatch(typeof(PrecombatController), "Hide")]
        [HarmonyPostfix]
        public static void PrecombatController_Hide_Postfix(PrecombatController __instance)
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                MelonLogger.Msg("Pre-combat screen closed");

                var reviewMode = ReviewModeController.Instance;
                if (reviewMode != null && reviewMode.IsInCombatMode)
                {
                    // Check if combat is now active (live combat starting)
                    var combat = TISpaceCombatState.CurrentActiveCombat;
                    if (combat != null && combat.active)
                    {
                        // Live combat starting
                        TISpeechMod.Speak("Live combat beginning. Combat mode will be available after implementation.", interrupt: true);
                    }

                    // Exit combat mode for now (until live combat mode is implemented)
                    reviewMode.ExitCombatMode();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PrecombatController.Hide patch: {ex.Message}");
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

                    // Refresh combat mode if active
                    var reviewMode = ReviewModeController.Instance;
                    if (reviewMode != null && reviewMode.IsInCombatMode)
                    {
                        reviewMode.CheckForCombatMode(); // This will refresh the state
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

                    // Refresh combat mode if active
                    var reviewMode = ReviewModeController.Instance;
                    if (reviewMode != null && reviewMode.IsInCombatMode)
                    {
                        reviewMode.CheckForCombatMode();
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
