using System;
using MelonLoader;
using UnityEngine;

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
        private static SlotCursor slotCursor;

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

            // Initialize slot cursor navigation system
            try
            {
                slotCursor = new SlotCursor();
                LoggerInstance.Msg("Slot cursor navigation system initialized");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to initialize slot cursor: {ex.Message}");
            }

            LoggerInstance.Msg("Terra Invicta Screen Reader Mod initialized successfully!");
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

            // Update slot cursor navigation
            if (slotCursor != null)
            {
                slotCursor.Update();
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
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_persuasion"">", " Persuasion: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_investigation"">", " Investigation: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_espionage"">", " Espionage: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_command"">", " Command: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_administration"">", " Administration: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_science"">", " Science: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_security"">", " Security: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""attribute_loyalty"">", " Loyalty: ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Replace resource sprite icons with text labels BEFORE removing other tags
            // Sprites appear as: "42.5<sprite name="currency">/month" so we just replace with a space and the label
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""currency"">", " Money ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""influence"">", " Influence ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""boost"">", " Boost ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""ops"">", " Operations ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""mission_control"">", " Mission Control ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""research"">", " Research ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""projects"">", " Projects ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""water"">", " Water ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""volatiles"">", " Volatiles ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""metals"">", " Metals ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""noble_metals"">", " Noble Metals ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""fissiles"">", " Fissiles ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""antimatter"">", " Antimatter ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""exotics"">", " Exotics ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""population"">", " Population ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<sprite\s+name=""nukes"">", " Nuclear Weapons ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove remaining TextMeshPro and HTML tags
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

            // Remove multiple spaces and trim
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }
    }
}
