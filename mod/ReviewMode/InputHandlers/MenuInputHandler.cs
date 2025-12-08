using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using TISpeech.ReviewMode.MenuMode;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Handles keyboard input for menu mode (pre-game menu navigation).
    /// </summary>
    public class MenuInputHandler : IInputHandler
    {
        private readonly List<MenuScreenBase> menuScreens;
        private readonly Func<int> getCurrentScreenIndex;
        private readonly Func<int> getCurrentControlIndex;
        private readonly Action<int> setCurrentControlIndex;
        private readonly Action blockGameEscapeProcessing;
        private readonly Action returnToMainMenu;
        private readonly Action deactivateReviewMode;

        public MenuInputHandler(
            List<MenuScreenBase> menuScreens,
            Func<int> getCurrentScreenIndex,
            Func<int> getCurrentControlIndex,
            Action<int> setCurrentControlIndex,
            Action blockGameEscapeProcessing,
            Action returnToMainMenu,
            Action deactivateReviewMode)
        {
            this.menuScreens = menuScreens;
            this.getCurrentScreenIndex = getCurrentScreenIndex;
            this.getCurrentControlIndex = getCurrentControlIndex;
            this.setCurrentControlIndex = setCurrentControlIndex;
            this.blockGameEscapeProcessing = blockGameEscapeProcessing;
            this.returnToMainMenu = returnToMainMenu;
            this.deactivateReviewMode = deactivateReviewMode;
        }

        public bool HandleInput()
        {
            int screenIndex = getCurrentScreenIndex();
            if (menuScreens.Count == 0 || screenIndex >= menuScreens.Count)
                return false;

            var screen = menuScreens[screenIndex];
            int controlCount = screen.ControlCount;
            int controlIndex = getCurrentControlIndex();

            // Navigate up/previous (Numpad 8, Up arrow)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (controlCount > 0)
                {
                    controlIndex--;
                    if (controlIndex < 0)
                        controlIndex = controlCount - 1;
                    setCurrentControlIndex(controlIndex);
                    AnnounceCurrentControl(screen, controlIndex, controlCount);
                }
                return true;
            }

            // Navigate down/next (Numpad 2, Down arrow)
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (controlCount > 0)
                {
                    controlIndex++;
                    if (controlIndex >= controlCount)
                        controlIndex = 0;
                    setCurrentControlIndex(controlIndex);
                    AnnounceCurrentControl(screen, controlIndex, controlCount);
                }
                return true;
            }

            // Adjust control left (Numpad 4, Left arrow) - for sliders/dropdowns
            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (screen.CanAdjustControl(controlIndex))
                {
                    screen.AdjustControl(controlIndex, increment: false);
                }
                return true;
            }

            // Adjust control right (Numpad 6, Right arrow) - for sliders/dropdowns
            if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (screen.CanAdjustControl(controlIndex))
                {
                    screen.AdjustControl(controlIndex, increment: true);
                }
                return true;
            }

            // Activate control (Numpad Enter, Numpad 5, Enter)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return))
            {
                screen.ActivateControl(controlIndex);
                return true;
            }

            // Back out / Exit (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                blockGameEscapeProcessing?.Invoke();
                // If on a sub-screen, go back to main menu
                if (screenIndex > 0)
                {
                    returnToMainMenu?.Invoke();
                }
                else
                {
                    // At main menu - deactivate menu mode
                    deactivateReviewMode?.Invoke();
                }
                return true;
            }

            // Read detail (Numpad *, Minus/Dash key)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                string detail = screen.ReadControlDetail(controlIndex);
                TISpeechMod.Speak(detail, interrupt: true);
                return true;
            }

            // List all controls (Numpad /, Equals key)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(screen.ListAllControls(), interrupt: true);
                return true;
            }

            // Letter navigation (A-Z) - jump to control starting with that letter
            char? letter = GetPressedLetter();
            if (letter.HasValue)
            {
                int newIndex = screen.FindNextControlByLetter(letter.Value, controlIndex);
                if (newIndex >= 0)
                {
                    setCurrentControlIndex(newIndex);
                    AnnounceCurrentControl(screen, newIndex, controlCount);
                }
                else
                {
                    TISpeechMod.Speak($"No controls starting with {letter.Value}", interrupt: true);
                }
                return true;
            }

            return false;
        }

        private void AnnounceCurrentControl(MenuScreenBase screen, int controlIndex, int controlCount)
        {
            if (controlCount == 0)
            {
                TISpeechMod.Speak("No controls", interrupt: true);
                return;
            }

            string controlText = screen.ReadControl(controlIndex);
            TISpeechMod.Speak($"{controlText}, {controlIndex + 1} of {controlCount}", interrupt: true);
        }

        private char? GetPressedLetter()
        {
            // Check for letter keys A-Z
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    return (char)('A' + (key - KeyCode.A));
                }
            }
            return null;
        }
    }
}
