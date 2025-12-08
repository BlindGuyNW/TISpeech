using System;
using System.Linq;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode;

namespace TISpeech
{
    /// <summary>
    /// Keyboard commands for accessibility
    /// Provides hotkeys to query current screen state and information
    /// </summary>
    public static class AccessibilityCommands
    {
        // Track key states to prevent repeated firing
        private static bool altSPressed = false;
        private static bool altRPressed = false;
        private static bool altLPressed = false;
        private static bool altDPressed = false;
        private static bool altOPressed = false;
        private static bool altCPressed = false;
        private static bool altBPressed = false;
        private static bool altMPressed = false;
        private static bool altTPressed = false;
        private static bool numpad0Pressed = false;
        private static bool ctrlRPressed = false;

        /// <summary>
        /// Check for accessibility keyboard commands each frame
        /// Call this from MelonMod.OnUpdate()
        /// </summary>
        public static void CheckKeyboardInput()
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                // Numpad 0 - Toggle review mode (no modifier needed)
                if (Input.GetKeyDown(KeyCode.Keypad0))
                {
                    if (!numpad0Pressed)
                    {
                        numpad0Pressed = true;
                        ToggleReviewMode();
                    }
                }
                else if (!Input.GetKey(KeyCode.Keypad0))
                {
                    numpad0Pressed = false;
                }

                // Ctrl+R - Toggle review mode (laptop-friendly alternative)
                bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrlHeld && Input.GetKeyDown(KeyCode.R))
                {
                    if (!ctrlRPressed)
                    {
                        ctrlRPressed = true;
                        ToggleReviewMode();
                    }
                }
                else if (!Input.GetKey(KeyCode.R))
                {
                    ctrlRPressed = false;
                }

                // Alt+S - Space resources
                if (altHeld && Input.GetKeyDown(KeyCode.S))
                {
                    if (!altSPressed)
                    {
                        altSPressed = true;
                        ReadSpaceResources();
                    }
                }
                else if (!Input.GetKey(KeyCode.S))
                {
                    altSPressed = false;
                }

                // Alt+R - Read resources (HUD)
                if (altHeld && Input.GetKeyDown(KeyCode.R))
                {
                    if (!altRPressed)
                    {
                        altRPressed = true;
                        ReadResources();
                    }
                }
                else if (!Input.GetKey(KeyCode.R))
                {
                    altRPressed = false;
                }

                // Alt+L - List items
                if (altHeld && Input.GetKeyDown(KeyCode.L))
                {
                    if (!altLPressed)
                    {
                        altLPressed = true;
                        ListScreenItems();
                    }
                }
                else if (!Input.GetKey(KeyCode.L))
                {
                    altLPressed = false;
                }

                // Alt+D - Detail selection
                if (altHeld && Input.GetKeyDown(KeyCode.D))
                {
                    if (!altDPressed)
                    {
                        altDPressed = true;
                        ReadDetailedSelection();
                    }
                }
                else if (!Input.GetKey(KeyCode.D))
                {
                    altDPressed = false;
                }

                // Alt+O - Objectives
                if (altHeld && Input.GetKeyDown(KeyCode.O))
                {
                    if (!altOPressed)
                    {
                        altOPressed = true;
                        ReadObjectives();
                    }
                }
                else if (!Input.GetKey(KeyCode.O))
                {
                    altOPressed = false;
                }

                // Alt+C - Control points
                if (altHeld && Input.GetKeyDown(KeyCode.C))
                {
                    if (!altCPressed)
                    {
                        altCPressed = true;
                        ReadControlPoints();
                    }
                }
                else if (!Input.GetKey(KeyCode.C))
                {
                    altCPressed = false;
                }

                // Alt+B - Boost resources
                if (altHeld && Input.GetKeyDown(KeyCode.B))
                {
                    if (!altBPressed)
                    {
                        altBPressed = true;
                        ReadBoostResources();
                    }
                }
                else if (!Input.GetKey(KeyCode.B))
                {
                    altBPressed = false;
                }

                // Alt+M - Mission control
                if (altHeld && Input.GetKeyDown(KeyCode.M))
                {
                    if (!altMPressed)
                    {
                        altMPressed = true;
                        ReadMissionControl();
                    }
                }
                else if (!Input.GetKey(KeyCode.M))
                {
                    altMPressed = false;
                }

                // Alt+T - Alien threat
                if (altHeld && Input.GetKeyDown(KeyCode.T))
                {
                    if (!altTPressed)
                    {
                        altTPressed = true;
                        ReadAlienThreat();
                    }
                }
                else if (!Input.GetKey(KeyCode.T))
                {
                    altTPressed = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in keyboard input: {ex.Message}");
            }
        }

        /// <summary>
        /// Numpad 0 - Toggle review mode
        /// </summary>
        private static void ToggleReviewMode()
        {
            try
            {
                // Create controller on first use if it doesn't exist
                var controller = ReviewModeController.Instance;
                if (controller == null)
                {
                    MelonLogger.Msg("Creating ReviewModeController on first use...");
                    controller = ReviewModeController.Create();
                }

                if (controller != null)
                {
                    controller.Toggle();
                }
                else
                {
                    MelonLogger.Error("Failed to create ReviewModeController");
                    TISpeechMod.Speak("Review mode not available", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling review mode: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
                TISpeechMod.Speak("Error toggling review mode", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+S - Read space resources (Water, Volatiles, Metals, NobleMetals, Fissiles, Antimatter, Exotics)
        /// </summary>
        private static void ReadSpaceResources()
        {
            try
            {
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;
                var announcement = new StringBuilder();
                announcement.Append("Space resources. ");

                var spaceResources = new[]
                {
                    FactionResource.Water,
                    FactionResource.Volatiles,
                    FactionResource.Metals,
                    FactionResource.NobleMetals,
                    FactionResource.Fissiles,
                    FactionResource.Antimatter,
                    FactionResource.Exotics
                };

                foreach (var resource in spaceResources)
                {
                    float current = faction.GetCurrentResourceAmount(resource);
                    float monthlyIncome = faction.GetMonthlyIncome(resource);

                    string resourceName = GetFriendlyResourceName(resource);
                    announcement.Append($"{resourceName}: {current:F1}");

                    if (monthlyIncome != 0)
                    {
                        string sign = monthlyIncome > 0 ? "plus" : "minus";
                        announcement.Append($", {sign} {Math.Abs(monthlyIncome):F1} per month. ");
                    }
                    else
                    {
                        announcement.Append(". ");
                    }
                }

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Space resources: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading space resources: {ex.Message}");
                TISpeechMod.Speak("Error reading space resources", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+R - Read HUD resources and faction status
        /// </summary>
        private static void ReadResources()
        {
            try
            {
                // Check if game is initialized and player is active
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;
                var announcement = new StringBuilder();
                announcement.Append($"Resources for {faction.displayName}. ");

                // Read primary resources: Money, Influence, Ops, Research, Boost
                var primaryResources = new[]
                {
                    FactionResource.Money,
                    FactionResource.Influence,
                    FactionResource.Operations,
                    FactionResource.Research,
                    FactionResource.Boost
                };

                foreach (var resource in primaryResources)
                {
                    float current = faction.GetCurrentResourceAmount(resource);
                    float monthlyIncome = faction.GetMonthlyIncome(resource);

                    string resourceName = resource.ToString();
                    announcement.Append($"{resourceName}: {current:F1}");

                    if (monthlyIncome != 0)
                    {
                        string sign = monthlyIncome > 0 ? "plus" : "minus";
                        announcement.Append($", {sign} {Math.Abs(monthlyIncome):F1} per month. ");
                    }
                    else
                    {
                        announcement.Append(". ");
                    }
                }

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Resources: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading resources: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
                TISpeechMod.Speak("Error reading resources", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+L - List items on current screen
        /// </summary>
        private static void ListScreenItems()
        {
            try
            {
                var announcement = new StringBuilder();
                announcement.Append("List items command. ");

                // TODO: Implement list reading
                announcement.Append("This feature is under development.");

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"List items: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error listing items: {ex.Message}");
                TISpeechMod.Speak("Error listing items", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+D - Read detailed information about current selection
        /// </summary>
        private static void ReadDetailedSelection()
        {
            try
            {
                var announcement = new StringBuilder();
                announcement.Append("Detail selection command. ");

                // TODO: Implement selection detail reading
                announcement.Append("This feature is under development.");

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Selection detail: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading selection: {ex.Message}");
                TISpeechMod.Speak("Error reading selection details", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+O - Read objectives status
        /// </summary>
        private static void ReadObjectives()
        {
            try
            {
                var announcement = new StringBuilder();
                announcement.Append("Objectives command. ");

                // TODO: Implement objectives reading
                announcement.Append("This feature is under development.");

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Objectives: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading objectives: {ex.Message}");
                TISpeechMod.Speak("Error reading objectives", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+C - Read control point status
        /// </summary>
        private static void ReadControlPoints()
        {
            try
            {
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;
                var announcement = new StringBuilder();
                announcement.Append("Control points. ");

                int cpCount = faction.controlPoints?.Count ?? 0;
                float cpUsage = faction.GetBaselineControlPointMaintenanceCost();
                float cpCap = faction.GetControlPointMaintenanceFreebieCap();

                // Main readout: usage / cap (count)
                announcement.Append($"Usage {cpUsage:F0} of {cpCap:F0} cap, {cpCount} held. ");

                // Check if over cap
                float overage = cpUsage - cpCap;
                if (overage > 0)
                {
                    float annualPenalty = faction.GetAnnualControlPointMaintenanceCost();
                    float monthlyPenalty = annualPenalty / 12f;
                    announcement.Append($"Over cap by {overage:F1}. Costing {monthlyPenalty:F1} influence per month. ");

                    float missionPenalty = faction.GetAveragedControlPointCapPenaltyToMissions();
                    if (missionPenalty > 0)
                    {
                        announcement.Append($"Mission penalty: {missionPenalty:F1}%. ");
                    }
                }
                else
                {
                    float headroom = cpCap - cpUsage;
                    announcement.Append($"Headroom: {headroom:F1} before penalty. ");
                }

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Control points: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading control points: {ex.Message}");
                TISpeechMod.Speak("Error reading control points", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+B - Read boost resources (detailed)
        /// </summary>
        private static void ReadBoostResources()
        {
            try
            {
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;
                var announcement = new StringBuilder();
                announcement.Append("Boost. ");

                float current = faction.GetCurrentResourceAmount(FactionResource.Boost);
                float monthlyIncome = faction.GetMonthlyIncome(FactionResource.Boost);

                announcement.Append($"{current:F1} stockpile");

                if (monthlyIncome != 0)
                {
                    string sign = monthlyIncome > 0 ? "plus" : "minus";
                    announcement.Append($", {sign} {Math.Abs(monthlyIncome):F1} per month");
                }
                announcement.Append(". ");

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Boost: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading boost: {ex.Message}");
                TISpeechMod.Speak("Error reading boost resources", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+M - Read mission control status
        /// </summary>
        private static void ReadMissionControl()
        {
            try
            {
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;
                var announcement = new StringBuilder();
                announcement.Append("Mission Control. ");

                int mcIncome = faction.MissionControlIncome;
                int mcUsage = faction.GetMissionControlUsage();
                int mcAvailable = faction.AvailableMissionControl;
                int mcShortage = faction.MissionControlShortage;

                announcement.Append($"Income: {mcIncome}. Usage: {mcUsage}. ");

                if (mcShortage > 0)
                {
                    announcement.Append($"Shortage: {mcShortage}. ");
                }
                else
                {
                    announcement.Append($"Available: {mcAvailable}. ");
                }

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Mission Control: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading mission control: {ex.Message}");
                TISpeechMod.Speak("Error reading mission control", interrupt: true);
            }
        }

        /// <summary>
        /// Alt+T - Read alien threat level
        /// </summary>
        private static void ReadAlienThreat()
        {
            try
            {
                if (GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    TISpeechMod.Speak("No active game session", interrupt: true);
                    return;
                }

                var faction = GameControl.control.activePlayer;

                // Check if player is an alien proxy (Servants) - they don't see threat meter
                if (faction.IsAlienProxy)
                {
                    TISpeechMod.Speak("Alien threat not applicable for Servants", interrupt: true);
                    return;
                }

                // Check if player can detect alien activity (requires 3+ effects to unlock the meter)
                float detectLevel = TIEffectsState.SumEffectsModifiers(Context.DetectAlienActivity, faction, 0f);
                if (detectLevel < 3f)
                {
                    TISpeechMod.Speak("Alien threat meter not yet unlocked. Need more intel capability.", interrupt: true);
                    return;
                }

                var announcement = new StringBuilder();
                announcement.Append("Alien threat. ");

                float warThreshold = TemplateManager.global.alienFactionHateWarValue; // 50 by default
                float estimatedHate = faction.GetEstimatedAlienHate();

                // Check for active war goal from aliens against us
                var alienFaction = GameStateManager.AlienFaction();
                TIFactionGoalState warGoal = null;
                FactionGoal_WarOnFaction warOnFactionGoal = null;

                if (alienFaction != null)
                {
                    warGoal = alienFaction.FindGoals(GoalType.WarOnFaction, alienFaction, faction).FirstOrDefault();
                    warOnFactionGoal = warGoal as FactionGoal_WarOnFaction;
                }

                // Check for Total War status
                bool isTotalWar = warOnFactionGoal != null && warOnFactionGoal.IsTotalWar;
                bool isAtWar = warGoal != null;

                if (isTotalWar)
                {
                    announcement.Append("Total War! All five lights red. ");
                    announcement.Append("Aliens are fully committed to destroying your faction. ");
                }
                else if (isAtWar)
                {
                    announcement.Append("At War. Five lights red. ");
                    announcement.Append("Aliens have declared war on your faction. ");
                }
                else
                {
                    // Calculate percentage toward war threshold
                    float percentage = (estimatedHate / warThreshold) * 100f;

                    // Describe gauge status based on lit lights
                    string gaugeColor;

                    if (percentage >= 100f)
                    {
                        gaugeColor = "five lights, red. War imminent";
                    }
                    else if (percentage >= 80f)
                    {
                        gaugeColor = "four lights, dark orange. High threat";
                    }
                    else if (percentage >= 60f)
                    {
                        gaugeColor = "three lights, orange. Elevated threat";
                    }
                    else if (percentage >= 40f)
                    {
                        gaugeColor = "two lights, yellow. Moderate threat";
                    }
                    else if (percentage >= 20f)
                    {
                        gaugeColor = "one light, green. Low threat";
                    }
                    else
                    {
                        gaugeColor = "no lights. Minimal threat";
                    }

                    announcement.Append($"{gaugeColor}. ");
                    announcement.Append($"Hate level: {estimatedHate:F0} of {warThreshold:F0} war threshold. ");
                    announcement.Append($"{percentage:F0}% to war. ");
                }

                // Add last intel update date if available
                var lastFixed = faction.GetLastDateofFixedAlienHate();
                if (lastFixed != null)
                {
                    announcement.Append($"Last updated: {lastFixed.ToCustomDateString()}. ");
                }

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Alien threat: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading alien threat: {ex.Message}");
                TISpeechMod.Speak("Error reading alien threat", interrupt: true);
            }
        }

        /// <summary>
        /// Get a friendly display name for a faction resource
        /// </summary>
        private static string GetFriendlyResourceName(FactionResource resource)
        {
            switch (resource)
            {
                case FactionResource.Money: return "Money";
                case FactionResource.Influence: return "Influence";
                case FactionResource.Operations: return "Operations";
                case FactionResource.Research: return "Research";
                case FactionResource.Projects: return "Projects";
                case FactionResource.Boost: return "Boost";
                case FactionResource.MissionControl: return "Mission Control";
                case FactionResource.Water: return "Water";
                case FactionResource.Volatiles: return "Volatiles";
                case FactionResource.Metals: return "Metals";
                case FactionResource.NobleMetals: return "Noble Metals";
                case FactionResource.Fissiles: return "Fissiles";
                case FactionResource.Antimatter: return "Antimatter";
                case FactionResource.Exotics: return "Exotics";
                default: return resource.ToString();
            }
        }
    }
}
