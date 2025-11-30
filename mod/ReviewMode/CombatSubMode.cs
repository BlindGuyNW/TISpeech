using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.SpaceCombat.UI;
using PavonisInteractive.TerraInvicta.Systems.UI;
using Unity.Entities;
using UnityEngine;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Represents the current phase of pre-combat.
    /// </summary>
    public enum PreCombatPhase
    {
        StanceSelection,
        Bidding,
        Resolution,
        PostCombat,
        None
    }

    /// <summary>
    /// An option in the pre-combat menu.
    /// </summary>
    public class CombatOption
    {
        public string Label { get; set; }
        public string Description { get; set; }
        public bool IsAvailable { get; set; }
        public Action OnActivate { get; set; }
    }

    /// <summary>
    /// Sub-mode for navigating space combat pre-combat screens.
    /// Handles stance selection, delta-V bidding, and resolution choice.
    /// </summary>
    public class CombatSubMode
    {
        public PreCombatPhase CurrentPhase { get; private set; }
        public List<CombatOption> Options { get; private set; }
        public int CurrentIndex { get; private set; }

        private TISpaceCombatState combat;
        private PrecombatController precombatController;
        private TIFactionState player;

        // Bidding state
        private float currentBid;
        private float maxBid;
        private float bidIncrement = 0.5f; // km/s

        public int Count => Options.Count;
        public CombatOption CurrentOption => Options.Count > 0 && CurrentIndex >= 0 && CurrentIndex < Options.Count
            ? Options[CurrentIndex]
            : null;

        public CombatSubMode()
        {
            Options = new List<CombatOption>();
            CurrentIndex = 0;
            CurrentPhase = PreCombatPhase.None;
        }

        /// <summary>
        /// Initialize the combat sub-mode with the current combat state.
        /// Returns true if successfully initialized, false if no combat is active.
        /// </summary>
        public bool Initialize()
        {
            try
            {
                // Get current combat state
                combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null)
                {
                    MelonLogger.Msg("CombatSubMode: No active combat");
                    return false;
                }

                // Get player faction
                player = GameControl.control?.activePlayer;
                if (player == null)
                {
                    MelonLogger.Msg("CombatSubMode: No active player");
                    return false;
                }

                // Check if player is involved in this combat
                if (!combat.factions.Contains(player))
                {
                    MelonLogger.Msg("CombatSubMode: Player not involved in combat");
                    return false;
                }

                // Get the precombat controller
                var canvasManager = World.Active?.GetExistingManager<CanvasManager>();
                if (canvasManager != null)
                {
                    precombatController = canvasManager.PrecombatControllerCanvas as PrecombatController;
                }

                // Determine current phase
                DetermineCurrentPhase();

                if (CurrentPhase == PreCombatPhase.None)
                {
                    MelonLogger.Msg("CombatSubMode: Unable to determine combat phase");
                    return false;
                }

                // Build options for current phase
                BuildOptions();

                MelonLogger.Msg($"CombatSubMode initialized: Phase={CurrentPhase}, Options={Options.Count}");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initializing CombatSubMode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refresh the combat state and rebuild options.
        /// </summary>
        public void Refresh()
        {
            try
            {
                combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null)
                {
                    CurrentPhase = PreCombatPhase.None;
                    Options.Clear();
                    return;
                }

                var previousPhase = CurrentPhase;
                DetermineCurrentPhase();

                // If phase changed, rebuild options
                if (CurrentPhase != previousPhase)
                {
                    BuildOptions();
                    CurrentIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing CombatSubMode: {ex.Message}");
            }
        }

        private void DetermineCurrentPhase()
        {
            if (combat == null)
            {
                CurrentPhase = PreCombatPhase.None;
                return;
            }

            // Check stance selection
            if (!combat.HaveStancesBeenSelected)
            {
                // Check if we've already submitted our stance
                if (combat.stances.TryGetValue(player, out var stance) && stance != CombatStance.NotYetSet)
                {
                    // We've submitted, waiting for opponent
                    CurrentPhase = PreCombatPhase.StanceSelection; // Still show stance options but indicate waiting
                }
                else
                {
                    CurrentPhase = PreCombatPhase.StanceSelection;
                }
                return;
            }

            // Check bidding phase
            if (combat.requiresBidding && !combat.HaveBidsBeenSubmitted)
            {
                // Check if we've already submitted our bid
                if (combat.bids_kps.ContainsKey(player))
                {
                    // We've submitted, waiting for opponent
                    CurrentPhase = PreCombatPhase.Bidding;
                }
                else
                {
                    CurrentPhase = PreCombatPhase.Bidding;
                }
                return;
            }

            // Check if combat will occur (resolution phase)
            if (combat.HaveStancesBeenSelected && (!combat.requiresBidding || combat.HaveBidsBeenSubmitted))
            {
                // Check if combat is active (live combat started)
                if (combat.active)
                {
                    CurrentPhase = PreCombatPhase.None; // Live combat mode, not pre-combat
                    return;
                }

                // Resolution selection phase
                CurrentPhase = PreCombatPhase.Resolution;
                return;
            }

            CurrentPhase = PreCombatPhase.None;
        }

        private void BuildOptions()
        {
            Options.Clear();

            switch (CurrentPhase)
            {
                case PreCombatPhase.StanceSelection:
                    BuildStanceOptions();
                    break;
                case PreCombatPhase.Bidding:
                    BuildBiddingOptions();
                    break;
                case PreCombatPhase.Resolution:
                    BuildResolutionOptions();
                    break;
            }
        }

        private void BuildStanceOptions()
        {
            var allowedStances = combat.allowedStances[player];

            // Check if we've already submitted
            bool alreadySubmitted = combat.stances.TryGetValue(player, out var currentStance) && currentStance != CombatStance.NotYetSet;

            if (alreadySubmitted)
            {
                Options.Add(new CombatOption
                {
                    Label = $"Waiting for opponent (selected: {GetStanceName(currentStance)})",
                    Description = "You have submitted your stance. Waiting for the enemy to respond.",
                    IsAvailable = false,
                    OnActivate = null
                });
                return;
            }

            // Engage (Pursue)
            if (allowedStances.Contains(CombatStance.Pursue))
            {
                Options.Add(new CombatOption
                {
                    Label = "Engage",
                    Description = "Attack the enemy fleet. You will pursue if they try to flee.",
                    IsAvailable = true,
                    OnActivate = () => SelectStance(CombatStance.Pursue)
                });
            }

            // Defend
            if (allowedStances.Contains(CombatStance.Defend))
            {
                Options.Add(new CombatOption
                {
                    Label = "Defend",
                    Description = "Accept battle but do not pursue if enemy flees.",
                    IsAvailable = true,
                    OnActivate = () => SelectStance(CombatStance.Defend)
                });
            }

            // Evade (Flee)
            if (allowedStances.Contains(CombatStance.Evade))
            {
                Options.Add(new CombatOption
                {
                    Label = "Evade",
                    Description = "Attempt to flee. Enemy may pursue based on delta-V.",
                    IsAvailable = true,
                    OnActivate = () => SelectStance(CombatStance.Evade)
                });
            }

            // Cancel attack option (if we're the attacker)
            if (combat.attackingFaction == player)
            {
                Options.Add(new CombatOption
                {
                    Label = "Cancel Attack",
                    Description = "Abort the attack and withdraw.",
                    IsAvailable = true,
                    OnActivate = CancelAttack
                });
            }
        }

        private void BuildBiddingOptions()
        {
            // Check if we've already submitted bid
            if (combat.bids_kps.ContainsKey(player))
            {
                float ourBid = combat.bids_kps[player];
                Options.Add(new CombatOption
                {
                    Label = $"Waiting for opponent (bid: {ourBid:F1} km/s)",
                    Description = "You have submitted your delta-V bid. Waiting for the enemy.",
                    IsAvailable = false,
                    OnActivate = null
                });
                return;
            }

            // Calculate max bid
            var ourFleet = combat.FleetFor(player);
            var theirFleet = combat.FleetAgainst(player);
            maxBid = combat.MaxDVBidForPursuit_mps(ourFleet, theirFleet) / 1000f;

            bool isEvading = combat.stances[player] == CombatStance.Evade;
            string roleText = isEvading ? "fleeing" : "pursuing";

            // Current bid display/adjust option
            Options.Add(new CombatOption
            {
                Label = $"Delta-V Bid: {currentBid:F1} km/s (max: {maxBid:F1})",
                Description = $"You are {roleText}. Use Left/Right to adjust bid, Enter to submit.",
                IsAvailable = true,
                OnActivate = SubmitBid
            });

            // Quick bid options
            Options.Add(new CombatOption
            {
                Label = "Bid Minimum (0 km/s)",
                Description = "Bid the minimum delta-V.",
                IsAvailable = true,
                OnActivate = () => { currentBid = 0; SubmitBid(); }
            });

            if (maxBid > 0)
            {
                Options.Add(new CombatOption
                {
                    Label = $"Bid Half ({maxBid / 2:F1} km/s)",
                    Description = "Bid half of your available delta-V.",
                    IsAvailable = true,
                    OnActivate = () => { currentBid = maxBid / 2; SubmitBid(); }
                });

                Options.Add(new CombatOption
                {
                    Label = $"Bid Maximum ({maxBid:F1} km/s)",
                    Description = "Bid all available delta-V for maximum pursuit/evasion.",
                    IsAvailable = true,
                    OnActivate = () => { currentBid = maxBid; SubmitBid(); }
                });
            }
        }

        private void BuildResolutionOptions()
        {
            // Check if combat will occur
            bool combatOccurs = DoesCombatOccur();

            // Check if we're in skirmish mode (auto-resolve not available)
            bool isSkirmishMode = GameControl.control?.skirmishMode ?? false;

            if (combatOccurs)
            {
                // Auto-resolve only available in campaign mode
                if (!isSkirmishMode)
                {
                    Options.Add(new CombatOption
                    {
                        Label = "Auto-Resolve",
                        Description = "Let the computer simulate the battle. You can review the results afterward.",
                        IsAvailable = true,
                        OnActivate = SelectAutoResolve
                    });
                }

                Options.Add(new CombatOption
                {
                    Label = "Fight Manually",
                    Description = "Take direct control of the battle in real-time (with pause).",
                    IsAvailable = true,
                    OnActivate = SelectLiveCombat
                });
            }
            else
            {
                // No combat - either fleet fled successfully or combat was avoided
                string resultText = GetNoCombatResultText();
                Options.Add(new CombatOption
                {
                    Label = "Close",
                    Description = resultText,
                    IsAvailable = true,
                    OnActivate = ClosePreCombat
                });
            }
        }

        private bool DoesCombatOccur()
        {
            // Combat doesn't occur if one side evades and the pursuer doesn't catch them
            if (combat.fleeingFleet != null && combat.chasingFleet != null)
            {
                // Check if chase is successful based on bids
                float chaserBid = combat.bids_kps.ContainsKey(combat.chasingFleet.faction)
                    ? combat.bids_kps[combat.chasingFleet.faction] : 0;
                float fleeBid = combat.bids_kps.ContainsKey(combat.fleeingFleet.faction)
                    ? combat.bids_kps[combat.fleeingFleet.faction] : 0;

                // If fleeing fleet bid more than chaser, they escape
                // (Simplified check - actual logic is more complex)
                if (fleeBid > chaserBid)
                    return false;
            }
            return true;
        }

        private string GetNoCombatResultText()
        {
            if (combat.fleeingFleet != null)
            {
                return $"{combat.fleeingFleet.GetDisplayName(player)} escaped pursuit.";
            }
            return "No combat occurred.";
        }

        #region Actions

        private void SelectStance(CombatStance stance)
        {
            try
            {
                MelonLogger.Msg($"Selecting combat stance: {stance}");
                player.playerControl.StartAction(new SelectCombatStance(combat, player, stance));
                TISpeechMod.Speak($"Selected {GetStanceName(stance)}. Waiting for opponent.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting stance: {ex.Message}");
                TISpeechMod.Speak("Error selecting stance", interrupt: true);
            }
        }

        private void CancelAttack()
        {
            try
            {
                MelonLogger.Msg("Cancelling attack");
                if (precombatController != null)
                {
                    precombatController.CancelAttackButton();
                    TISpeechMod.Speak("Attack cancelled", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("Cannot cancel - precombat controller not available", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cancelling attack: {ex.Message}");
                TISpeechMod.Speak("Error cancelling attack", interrupt: true);
            }
        }

        private void SubmitBid()
        {
            try
            {
                MelonLogger.Msg($"Submitting delta-V bid: {currentBid:F1} km/s");

                // Need to determine extension stance (for extended pursuit)
                var extensionStance = CombatStance.NotYetSet;
                var attackerShips = new List<TISpaceShipState>();

                player.playerControl.StartAction(new SelectCombatBid(combat, player, currentBid, extensionStance, attackerShips));
                TISpeechMod.Speak($"Bid {currentBid:F1} kilometers per second submitted. Waiting for opponent.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error submitting bid: {ex.Message}");
                TISpeechMod.Speak("Error submitting bid", interrupt: true);
            }
        }

        private void SelectAutoResolve()
        {
            try
            {
                MelonLogger.Msg("Selecting auto-resolve");
                if (precombatController != null)
                {
                    precombatController.AutoresolveSelected();
                    TISpeechMod.Speak("Auto-resolve selected. Battle will be simulated.", interrupt: true);
                }
                else
                {
                    // Try direct approach
                    TIPromptQueueState.RemovePromptStatic(player, combat, null, "PromptBeginCombat");
                    TISpeechMod.Speak("Auto-resolve selected", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting auto-resolve: {ex.Message}");
                TISpeechMod.Speak("Error selecting auto-resolve", interrupt: true);
            }
        }

        private void SelectLiveCombat()
        {
            try
            {
                MelonLogger.Msg("Selecting live combat");
                if (precombatController != null)
                {
                    precombatController.LiveResolveSelected();
                    TISpeechMod.Speak("Live combat selected. Battle will begin shortly.", interrupt: true);
                }
                else
                {
                    combat.autoresolve = false;
                    TIPromptQueueState.RemovePromptStatic(player, combat, null, "PromptBeginCombat");
                    TISpeechMod.Speak("Live combat selected", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting live combat: {ex.Message}");
                TISpeechMod.Speak("Error selecting live combat", interrupt: true);
            }
        }

        private void ClosePreCombat()
        {
            try
            {
                MelonLogger.Msg("Closing pre-combat screen");
                if (precombatController != null)
                {
                    precombatController.CloseResolveSelected();
                }
                else
                {
                    TIPromptQueueState.RemovePromptStatic(player, combat, null, "PromptBeginCombat");
                }
                TISpeechMod.Speak("Pre-combat closed", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error closing pre-combat: {ex.Message}");
            }
        }

        #endregion

        #region Navigation

        public void Next()
        {
            if (Options.Count == 0) return;
            CurrentIndex = (CurrentIndex + 1) % Options.Count;
        }

        public void Previous()
        {
            if (Options.Count == 0) return;
            CurrentIndex--;
            if (CurrentIndex < 0) CurrentIndex = Options.Count - 1;
        }

        /// <summary>
        /// Adjust the current value (for bidding slider).
        /// </summary>
        public void AdjustValue(bool increment)
        {
            if (CurrentPhase != PreCombatPhase.Bidding) return;

            if (increment)
            {
                currentBid = Mathf.Min(currentBid + bidIncrement, maxBid);
            }
            else
            {
                currentBid = Mathf.Max(currentBid - bidIncrement, 0);
            }

            // Update the first option's label
            if (Options.Count > 0)
            {
                Options[0].Label = $"Delta-V Bid: {currentBid:F1} km/s (max: {maxBid:F1})";
            }
        }

        public void Activate()
        {
            var option = CurrentOption;
            if (option == null || !option.IsAvailable)
            {
                TISpeechMod.Speak("Option not available", interrupt: true);
                return;
            }

            option.OnActivate?.Invoke();
        }

        #endregion

        #region Announcements

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append("Space Combat. ");

            // Fleet info
            var ourFleet = combat.FleetFor(player);
            var theirFleet = combat.FleetAgainst(player);

            if (ourFleet != null && theirFleet != null)
            {
                sb.Append($"Your fleet: {ourFleet.ships.Count} ships. ");
                sb.Append($"Enemy fleet: {theirFleet.ships.Count} ships. ");
            }
            else if (theirFleet != null)
            {
                sb.Append($"Enemy fleet: {theirFleet.ships.Count} ships. ");
            }

            // Hab info
            if (combat.hab != null)
            {
                sb.Append($"At {combat.hab.GetDisplayName(player)}. ");
            }

            // Phase info
            sb.Append(GetPhaseAnnouncement());

            return sb.ToString();
        }

        private string GetPhaseAnnouncement()
        {
            switch (CurrentPhase)
            {
                case PreCombatPhase.StanceSelection:
                    return $"Select your stance. {Options.Count} options available.";
                case PreCombatPhase.Bidding:
                    bool isEvading = combat.stances[player] == CombatStance.Evade;
                    return isEvading
                        ? "Bidding phase. Set delta-V for escape attempt."
                        : "Bidding phase. Set delta-V for pursuit.";
                case PreCombatPhase.Resolution:
                    bool isSkirmish = GameControl.control?.skirmishMode ?? false;
                    if (isSkirmish)
                        return "Skirmish mode. Manual combat only.";
                    return "Select battle resolution.";
                default:
                    return "";
            }
        }

        public string GetCurrentAnnouncement()
        {
            if (Options.Count == 0)
                return "No options available";

            var option = CurrentOption;
            if (option == null)
                return "No option selected";

            return $"{CurrentIndex + 1} of {Options.Count}: {option.Label}";
        }

        public string GetCurrentDetail()
        {
            var option = CurrentOption;
            if (option == null)
                return "No option selected";

            var sb = new StringBuilder();
            sb.Append(option.Label);

            if (!string.IsNullOrEmpty(option.Description))
            {
                sb.Append(". ");
                sb.Append(option.Description);
            }

            if (!option.IsAvailable)
            {
                sb.Append(" (Not available)");
            }

            return sb.ToString();
        }

        public string ListAllOptions()
        {
            if (Options.Count == 0)
                return "No options available";

            var sb = new StringBuilder();
            sb.Append(Options.Count);
            sb.Append(Options.Count == 1 ? " option: " : " options: ");

            for (int i = 0; i < Options.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Options[i].Label);
                if (!Options[i].IsAvailable) sb.Append(" (unavailable)");
            }

            return sb.ToString();
        }

        public string GetFleetSummary()
        {
            var sb = new StringBuilder();

            var ourFleet = combat.FleetFor(player);
            var theirFleet = combat.FleetAgainst(player);

            if (ourFleet != null)
            {
                sb.Append($"Your fleet: {ourFleet.displayName}. ");
                sb.Append($"{ourFleet.ships.Count} ships. ");
                sb.Append($"Combat value: {ourFleet.SpaceCombatValue():N0}. ");
                sb.Append($"Delta-V: {ourFleet.currentDeltaV_kps:F1} km/s. ");
            }

            if (theirFleet != null)
            {
                sb.Append($"Enemy fleet: {theirFleet.GetDisplayName(player)}. ");
                sb.Append($"{theirFleet.ships.Count} ships. ");
                sb.Append($"Combat value: {theirFleet.SpaceCombatValue():N0}. ");
            }

            if (combat.hab != null)
            {
                sb.Append($"Habitat: {combat.hab.GetDisplayName(player)}, ");
                sb.Append($"owned by {combat.hab.faction.GetDisplayName(player)}. ");
            }

            return sb.ToString();
        }

        private string GetStanceName(CombatStance stance)
        {
            switch (stance)
            {
                case CombatStance.Pursue: return "Engage";
                case CombatStance.Defend: return "Defend";
                case CombatStance.Evade: return "Evade";
                case CombatStance.ExtendedPursuit_Envelop: return "Extended Pursuit (Envelop)";
                case CombatStance.ExtendedPursuit_Stretch: return "Extended Pursuit (Stretch)";
                default: return stance.ToString();
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Check if we're currently in a pre-combat state.
        /// </summary>
        public static bool IsInPreCombat()
        {
            try
            {
                var combat = TISpaceCombatState.CurrentActiveCombat;
                if (combat == null) return false;

                // If combat is active (live battle), we're not in pre-combat
                if (combat.active) return false;

                var player = GameControl.control?.activePlayer;
                if (player == null) return false;

                // Check if player is involved
                if (!combat.factions.Contains(player)) return false;

                // We're in pre-combat if stances aren't selected, bidding isn't done, or resolution isn't chosen
                if (!combat.HaveStancesBeenSelected) return true;
                if (combat.requiresBidding && !combat.HaveBidsBeenSubmitted) return true;

                // Also in pre-combat during resolution selection
                // This is harder to detect - check if precombat UI is visible
                var canvasManager = World.Active?.GetExistingManager<CanvasManager>();
                var precombat = canvasManager?.PrecombatControllerCanvas as PrecombatController;
                if (precombat != null && precombat.Visible())
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if we're in live combat (battle in progress).
        /// </summary>
        public static bool IsInLiveCombat()
        {
            try
            {
                var combat = TISpaceCombatState.CurrentActiveCombat;
                return combat != null && combat.active;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
