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
    }
}
