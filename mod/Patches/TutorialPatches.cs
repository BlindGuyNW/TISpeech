using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace TISpeech.Patches
{
    /// <summary>
    /// Component that handles keyboard navigation for tutorial tips.
    /// Added to TutorialTip to enable keyboard control.
    /// </summary>
    public class TutorialKeyboardHandler : MonoBehaviour
    {
        private TutorialTip tutorialTip;

        void Awake()
        {
            tutorialTip = GetComponent<TutorialTip>();
        }

        void Update()
        {
            if (tutorialTip == null || !gameObject.activeInHierarchy)
                return;

            // Enter or Space = Next tip (or Close if on last tip)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (tutorialTip.nextTipButton != null && tutorialTip.nextTipButton.gameObject.activeSelf)
                {
                    tutorialTip.ClickedConfirm();
                    TISpeechMod.Speak("Next tip", interrupt: true);
                }
                else if (tutorialTip.closeTipButton != null && tutorialTip.closeTipButton.gameObject.activeSelf)
                {
                    tutorialTip.ClickedSkipTutorial();
                    TISpeechMod.Speak("Tutorial closed", interrupt: true);
                }
            }
            // Backspace = Previous tip
            else if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (tutorialTip.previousTipButton != null && tutorialTip.previousTipButton.interactable)
                {
                    tutorialTip.ClickedBack();
                    TISpeechMod.Speak("Previous tip", interrupt: true);
                }
                else
                {
                    TISpeechMod.Speak("No previous tip", interrupt: true);
                }
            }
            // Escape = Close/Skip tutorial
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                tutorialTip.ClickedSkipTutorial();
                TISpeechMod.Speak("Tutorial closed", interrupt: true);
            }
            // H = Hide tutorial (don't show again)
            else if (Input.GetKeyDown(KeyCode.H))
            {
                tutorialTip.ClickedDontShowAgain();
                TISpeechMod.Speak("Tutorial hidden, won't show again", interrupt: true);
            }
            // R = Repeat current tip
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RepeatCurrentTip();
            }
        }

        private void RepeatCurrentTip()
        {
            if (tutorialTip == null)
                return;

            string title = "";
            if (tutorialTip.tutorialTitleText != null && !string.IsNullOrEmpty(tutorialTip.tutorialTitleText.text))
            {
                title = TISpeechMod.CleanText(tutorialTip.tutorialTitleText.text);
            }

            string body = "";
            // Check which text field has content
            if (tutorialTip.tutorialDescriptionText != null &&
                tutorialTip.tutorialDescriptionText.gameObject.activeSelf &&
                !string.IsNullOrEmpty(tutorialTip.tutorialDescriptionText.text))
            {
                body = TISpeechMod.CleanText(tutorialTip.tutorialDescriptionText.text);
            }
            else if (tutorialTip.tutorialDescriptionTextOverflowPrimary != null &&
                     !string.IsNullOrEmpty(tutorialTip.tutorialDescriptionTextOverflowPrimary.text))
            {
                body = TISpeechMod.CleanText(tutorialTip.tutorialDescriptionTextOverflowPrimary.text);
            }

            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body))
            {
                string announcement = $"Tutorial: {title}. {body}";
                TISpeechMod.Speak(announcement, interrupt: true);
            }
        }
    }

    /// <summary>
    /// Harmony patches for tutorial tips to provide screen reader accessibility.
    /// Announces tutorial content when tips are displayed.
    /// </summary>
    [HarmonyPatch]
    public class TutorialPatches
    {
        /// <summary>
        /// Patch TutorialTip.SetTipTextAndImage to announce tutorial content when tips appear.
        /// This is the central method where all tip text is set, whether navigating forward/back or first display.
        /// </summary>
        [HarmonyPatch(typeof(TutorialTip), "SetTipTextAndImage")]
        [HarmonyPostfix]
        public static void SetTipTextAndImage_Postfix(TutorialTip __instance, Sprite image)
        {
            try
            {
                if (!TISpeechMod.IsReady || __instance == null)
                    return;

                // Ensure keyboard handler is attached
                EnsureKeyboardHandler(__instance);

                // Get the title text
                string title = "";
                if (__instance.tutorialTitleText != null && !string.IsNullOrEmpty(__instance.tutorialTitleText.text))
                {
                    title = TISpeechMod.CleanText(__instance.tutorialTitleText.text);
                }

                // Get the body text - depends on whether an image is present
                string body = "";
                if (image == null)
                {
                    // No image: text is in tutorialDescriptionText
                    if (__instance.tutorialDescriptionText != null && !string.IsNullOrEmpty(__instance.tutorialDescriptionText.text))
                    {
                        body = TISpeechMod.CleanText(__instance.tutorialDescriptionText.text);
                    }
                }
                else
                {
                    // With image: text is in tutorialDescriptionTextOverflowPrimary
                    if (__instance.tutorialDescriptionTextOverflowPrimary != null && !string.IsNullOrEmpty(__instance.tutorialDescriptionTextOverflowPrimary.text))
                    {
                        body = TISpeechMod.CleanText(__instance.tutorialDescriptionTextOverflowPrimary.text);
                    }
                }

                // Build and speak the announcement
                if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body))
                {
                    string announcement = $"Tutorial: {title}. {body}";
                    TISpeechMod.Speak(announcement, interrupt: true);
                    MelonLogger.Msg($"Announced tutorial tip: {title}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TutorialTip.SetTipTextAndImage patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the keyboard handler component is attached to the TutorialTip.
        /// </summary>
        private static void EnsureKeyboardHandler(TutorialTip tip)
        {
            try
            {
                if (tip.GetComponent<TutorialKeyboardHandler>() == null)
                {
                    tip.gameObject.AddComponent<TutorialKeyboardHandler>();
                    MelonLogger.Msg("Added keyboard handler to TutorialTip. Keys: Enter/Space=Next, Backspace=Previous, Escape=Close, H=Hide, R=Repeat");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error adding keyboard handler to TutorialTip: {ex.Message}");
            }
        }
    }
}
