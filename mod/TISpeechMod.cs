using System;
using MelonLoader;
using UnityEngine;
using TISpeech.ReviewMode;

[assembly: MelonInfo(typeof(TISpeech.TISpeechMod), "TI Speech", "1.0.0", "TISpeech")]
[assembly: MelonGame("Pavonis Interactive", "TerraInvicta")]

namespace TISpeech
{
    /// <summary>
    /// Terra Invicta Screen Reader Accessibility Mod
    /// Provides speech output for tooltips, buttons, and UI elements using Tolk library
    /// </summary>
    public class TISpeechMod : MelonMod
    {
        private static Tolk.Tolk tolk;
        private static bool tolkInitialized = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Terra Invicta Screen Reader Mod initializing...");

            try
            {
                // Initialize Tolk for screen reader support
                tolk = new Tolk.Tolk();
                tolk.Load();

                if (tolk.IsLoaded())
                {
                    tolkInitialized = true;
                    string screenReader = tolk.DetectScreenReader();
                    LoggerInstance.Msg($"Screen reader detected: {screenReader ?? "SAPI (fallback)"}");

                    // Announce mod loaded
                    Speak("Terra Invicta accessibility mod loaded", true);
                }
                else
                {
                    LoggerInstance.Warning("Tolk failed to load. Screen reader support may not be available.");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to initialize Tolk: {ex.Message}");
                LoggerInstance.Error($"Stack trace: {ex.StackTrace}");
            }

            LoggerInstance.Msg("Terra Invicta Screen Reader Mod initialized successfully!");
            LoggerInstance.Msg("Press Numpad 0 to toggle review mode.");
        }


        public override void OnDeinitializeMelon()
        {
            if (tolkInitialized && tolk != null)
            {
                LoggerInstance.Msg("Unloading Tolk...");
                tolk.Unload();
                tolkInitialized = false;
            }
        }

        public override void OnUpdate()
        {
            // Check for accessibility keyboard commands each frame
            AccessibilityCommands.CheckKeyboardInput();

            // Check for review mode navigation input (only when active)
            var controller = ReviewModeController.Instance;
            if (controller != null)
            {
                controller.CheckInput();
            }
        }

        /// <summary>
        /// Speak text through the screen reader
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">Whether to interrupt current speech</param>
        public static void Speak(string text, bool interrupt = false)
        {
            if (!tolkInitialized || tolk == null || string.IsNullOrEmpty(text))
                return;

            try
            {
                tolk.Speak(text, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error speaking text: {ex.Message}");
            }
        }

        /// <summary>
        /// Output text to both speech and braille
        /// </summary>
        /// <param name="text">Text to output</param>
        /// <param name="interrupt">Whether to interrupt current speech</param>
        public static void Output(string text, bool interrupt = false)
        {
            if (!tolkInitialized || tolk == null || string.IsNullOrEmpty(text))
                return;

            try
            {
                tolk.Output(text, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error outputting text: {ex.Message}");
            }
        }

        /// <summary>
        /// Silence current speech
        /// </summary>
        public static void Silence()
        {
            if (!tolkInitialized || tolk == null)
                return;

            try
            {
                tolk.Silence();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error silencing speech: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if Tolk is initialized and ready
        /// </summary>
        public static bool IsReady => tolkInitialized && tolk != null;

        /// <summary>
        /// Clean text by converting sprite icons to readable labels and removing HTML tags
        /// Shared utility for all patches to ensure consistent text processing
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Replace councilor attribute sprite icons with text labels BEFORE removing other tags
            // These appear in tooltips like: "<sprite name="attribute_persuasion">10<sprite name="attribute_investigation">2"
            // or without quotes: "<sprite name=attribute_persuasion>"
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_persuasion""?>", " Persuasion: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_investigation""?>", " Investigation: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_espionage""?>", " Espionage: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_command""?>", " Command: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_administration""?>", " Administration: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_science""?>", " Science: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_security""?>", " Security: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?attribute_loyalty""?>", " Loyalty: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace resource sprite icons with text labels BEFORE removing other tags
            // Sprites appear as: "42.5<sprite name="currency">/month" or "<sprite name=currency>" (with or without quotes)
            // We use a pattern that matches both quoted and unquoted attribute values
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?currency""?>", " Money ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?influence""?>", " Influence ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?boost""?>", " Boost ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?ops""?>", " Operations ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?mission_control""?>", " Mission Control ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?research""?>", " Research ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?projects""?>", " Projects ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?water""?>", " Water ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?volatiles""?>", " Volatiles ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Note: Game uses "metal" not "metals", "metal_noble" not "noble_metals", "radioactive" not "fissiles"
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?metal""?>", " Metals ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?metal_noble""?>", " Noble Metals ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?radioactive""?>", " Fissiles ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?antimatter""?>", " Antimatter ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?exotics""?>", " Exotics ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?population""?>", " Population ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?nukes""?>", " Nuclear Weapons ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace hab/space infrastructure sprite icons
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?hab_power""?>", " Power ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?hab_power_alert""?>", " Power (deficit) ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?construction_shipyard""?>", " Shipyard ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?supply""?>", " Resupply ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?construction_module""?>", " Construction ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?combat_score""?>", " Defense ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?control_point""?>", " Control Points ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?radius_of_orbit""?>", " Orbit ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?space_assault_score""?>", " Assault ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace nation stat sprite icons with text labels
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?democracy""?>", " Democracy ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?gov_type""?>", " Democracy ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?nation_unrest""?>", " Unrest ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?unrest""?>", " Unrest ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?inequality""?>", " Inequality ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?per_capita_GDP""?>", " GDP ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?gdp""?>", " GDP ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?culture""?>", " Cohesion ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?cohesion""?>", " Cohesion ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?army_level""?>", " Miltech ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?miltech""?>", " Miltech ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?education""?>", " Education ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace tech category sprite icons
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_Energy""?>", " Energy ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_InformationScience""?>", " Information ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_MilitaryScience""?>", " Military ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_Materials""?>", " Materials ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_LifeScience""?>", " Life Science ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_SocialScience""?>", " Social Science ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_SpaceScience""?>", " Space Science ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?tech_Xenology""?>", " Xenology ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace misc UI sprite icons
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?xp""?>", " XP ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?star""?>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?warning""?>", " Warning: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?space_combat_score""?>", " Combat Score ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?core_res""?>", " Mining ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""?sun""?>", " Solar ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove remaining TextMeshPro and HTML tags
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Collapse multiple spaces (but NOT newlines) into single space
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");

            // Collapse 3+ consecutive newlines into double newline (paragraph break)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

            // Trim whitespace from each line
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }
            text = string.Join("\n", lines);

            // Remove leading/trailing newlines
            text = text.Trim();

            return text;
        }
    }
}
