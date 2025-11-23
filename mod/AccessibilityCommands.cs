using System;
using System.Text;
using MelonLoader;
using UnityEngine;
using PavonisInteractive.TerraInvicta;

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

                // Alt+S - Screen info/status
                if (altHeld && Input.GetKeyDown(KeyCode.S))
                {
                    if (!altSPressed)
                    {
                        altSPressed = true;
                        ReadScreenInfo();
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
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in keyboard input: {ex.Message}");
            }
        }

        /// <summary>
        /// Alt+S - Read information about the current screen
        /// </summary>
        private static void ReadScreenInfo()
        {
            try
            {
                var announcement = new StringBuilder();
                announcement.Append("Screen info command. ");

                // TODO: Implement screen detection and info reading
                announcement.Append("This feature is under development.");

                TISpeechMod.Speak(announcement.ToString(), interrupt: true);
                MelonLogger.Msg($"Screen info: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading screen info: {ex.Message}");
                TISpeechMod.Speak("Error reading screen information", interrupt: true);
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
    }
}
