using System;
using MelonLoader;
using UnityEngine;
using TISpeech.ReviewMode.Screens;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Handles keyboard input for standard Review Mode navigation (screens, items, sections).
    /// </summary>
    public class NavigationInputHandler : IInputHandler
    {
        private readonly NavigationState navigation;
        private readonly Action<PriorityGridSection> enterGridMode;
        private readonly Action openEscapeMenu;
        private readonly Action blockGameEscapeProcessing;
        private readonly Action handleConfirmAssignments;
        private readonly Action handleViewModeToggle;
        private readonly Func<bool, bool> handleFactionFilter;
        private readonly Func<bool> handleNationSort;
        private readonly Func<bool> handleProbeAll;
        private readonly Func<bool> handleNationFilter;
        private readonly Action enterTheoreticalTransferMode;

        public NavigationInputHandler(
            NavigationState navigation,
            Action<PriorityGridSection> enterGridMode,
            Action openEscapeMenu,
            Action blockGameEscapeProcessing,
            Action handleConfirmAssignments,
            Action handleViewModeToggle,
            Func<bool, bool> handleFactionFilter,
            Func<bool> handleNationSort,
            Func<bool> handleProbeAll,
            Func<bool> handleNationFilter,
            Action enterTheoreticalTransferMode)
        {
            this.navigation = navigation;
            this.enterGridMode = enterGridMode;
            this.openEscapeMenu = openEscapeMenu;
            this.blockGameEscapeProcessing = blockGameEscapeProcessing;
            this.handleConfirmAssignments = handleConfirmAssignments;
            this.handleViewModeToggle = handleViewModeToggle;
            this.handleFactionFilter = handleFactionFilter;
            this.handleNationSort = handleNationSort;
            this.handleProbeAll = handleProbeAll;
            this.handleNationFilter = handleNationFilter;
            this.enterTheoreticalTransferMode = enterTheoreticalTransferMode;
        }

        public bool HandleInput()
        {
            // Navigate up/previous (Numpad 8, Up arrow)
            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                navigation.Previous();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Navigate down/next (Numpad 2, Down arrow)
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                navigation.Next();
                TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                return true;
            }

            // Drill down / Activate (Numpad Enter, Numpad 5, Enter, Right arrow, Numpad 6)
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Keypad5) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.Keypad6))
            {
                var result = navigation.DrillDown();
                switch (result)
                {
                    case DrillResult.Drilled:
                        // Check if we drilled into a PriorityGridSection
                        var currentSection = navigation.CurrentSection;
                        if (currentSection is PriorityGridSection gridSection)
                        {
                            // Enter grid mode instead of normal section navigation
                            enterGridMode?.Invoke(gridSection);
                        }
                        else
                        {
                            // Actually drilled into a new level - announce new position
                            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                        }
                        break;
                    case DrillResult.Activated:
                        // Item was activated - don't re-announce, the action handles its own speech
                        break;
                    case DrillResult.Nothing:
                        // Couldn't drill or activate - re-read current position
                        TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                        break;
                }
                return true;
            }

            // Back out (Left arrow, Numpad 4, Backspace, Escape)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4) ||
                Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                // Block game from also processing Escape
                if (Input.GetKeyDown(KeyCode.Escape))
                    blockGameEscapeProcessing?.Invoke();

                if (navigation.BackOut())
                {
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                    return true;
                }
                else
                {
                    // At top level - open the escape menu
                    openEscapeMenu?.Invoke();
                    return true;
                }
            }

            // Read detail (Numpad *, Minus/Dash key)
            if (Input.GetKeyDown(KeyCode.KeypadMultiply) || Input.GetKeyDown(KeyCode.Minus))
            {
                TISpeechMod.Speak(navigation.GetCurrentDetail(), interrupt: true);
                return true;
            }

            // List items at current level (Numpad /, Equals key)
            if (Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Equals))
            {
                TISpeechMod.Speak(navigation.ListCurrentLevel(), interrupt: true);
                return true;
            }

            // PageUp/PageDown for quick screen switching (when at Screens level)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                if (navigation.CurrentLevel == NavigationLevel.Screens)
                {
                    navigation.Previous();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // Back out to screens level
                    while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.BackOut()) { }
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                if (navigation.CurrentLevel == NavigationLevel.Screens)
                {
                    navigation.Next();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                else
                {
                    // Back out to screens level
                    while (navigation.CurrentLevel != NavigationLevel.Screens && navigation.BackOut()) { }
                    navigation.Next();
                    TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
                }
                return true;
            }

            // Confirm Assignments (Numpad +, Backslash) - global action during mission phase
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Backslash))
            {
                handleConfirmAssignments?.Invoke();
                return true;
            }

            // Toggle view mode (Tab) - switch between Mine/All modes
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                handleViewModeToggle?.Invoke();
                return true;
            }

            // Faction filter ([ and ]) - cycle through factions in All mode
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                handleFactionFilter?.Invoke(true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                handleFactionFilter?.Invoke(false);
                return true;
            }

            // Sort (Ctrl+S) - open sort menu on Nations or Space Bodies screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.S))
            {
                if (handleNationSort?.Invoke() == true)
                    return true;
            }

            // Probe All (Ctrl+P) - launch probes to all eligible bodies on Space Bodies screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.P))
            {
                if (handleProbeAll?.Invoke() == true)
                    return true;
            }

            // Filter (Ctrl+F) - cycle faction filter on Nations screen
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
            {
                if (handleNationFilter?.Invoke() == true)
                    return true;
            }

            // Transfer planner (T) - enter theoretical transfer planner
            // Only trigger if Alt is NOT held (Alt+T is used for alien threat in AccessibilityCommands)
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (Input.GetKeyDown(KeyCode.T) && !altHeld)
            {
                enterTheoreticalTransferMode?.Invoke();
                return true;
            }

            // Letter navigation (A-Z) - jump to item starting with that letter
            if (navigation.CurrentLevel == NavigationLevel.Items)
            {
                char? letter = GetPressedLetter();
                if (letter.HasValue)
                {
                    HandleLetterNavigation(letter.Value);
                    return true;
                }
            }

            // Time controls (Space, 1-6) - work in all Review Mode states
            if (TimeControlHandler.HandleInput())
                return true;

            return false;
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

        private void HandleLetterNavigation(char letter)
        {
            var screen = navigation.CurrentScreen;
            if (screen == null || !screen.SupportsLetterNavigation)
            {
                TISpeechMod.Speak($"Letter navigation not supported", interrupt: true);
                return;
            }

            int currentIndex = navigation.CurrentItemIndex;
            int newIndex = screen.FindNextItemByLetter(letter, currentIndex);

            if (newIndex < 0)
            {
                TISpeechMod.Speak($"No items starting with {letter}", interrupt: true);
                return;
            }

            navigation.SetItemIndex(newIndex);
            TISpeechMod.Speak(navigation.GetCurrentAnnouncement(), interrupt: true);
        }
    }
}
