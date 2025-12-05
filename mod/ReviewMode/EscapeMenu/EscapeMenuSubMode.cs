using System;
using System.Collections.Generic;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.EscapeMenu.Codex;
using TISpeech.ReviewMode.EscapeMenu.Screens;
using TISpeech.ReviewMode.MenuMode;
using TMPro;
using UnityEngine;

namespace TISpeech.ReviewMode.EscapeMenu
{
    /// <summary>
    /// Sub-mode for navigating the in-game escape menu (pause menu).
    /// Handles navigation through Save, Load, Settings, and Exit options.
    /// </summary>
    public class EscapeMenuSubMode
    {
        private List<EscapeMenuScreenBase> screens;
        private int currentScreenIndex;
        private int currentControlIndex;
        private OptionsScreenController optionsScreenController;

        // Track activation time to prevent immediate Escape key processing
        private float activationTime = 0f;
        private const float ACTIVATION_GRACE_PERIOD = 0.3f;

        // Text input mode state (for save filename entry)
        private bool isEnteringText = false;
        private string textInput = "";
        private string textInputLabel = "";
        private TMP_InputField targetInputField = null;
        private int maxTextLength = 100;

        // Codex sub-mode for encyclopedia navigation
        private CodexSubMode codexSubMode;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Whether we are currently navigating the Codex.
        /// </summary>
        public bool IsInCodexMode => codexSubMode != null && codexSubMode.IsActive;

        /// <summary>
        /// Access to the Codex sub-mode for input routing.
        /// </summary>
        public CodexSubMode CodexMode => codexSubMode;
        public bool IsEnteringText => isEnteringText;
        public string TextInput => textInput;
        public string CurrentScreenName => screens != null && currentScreenIndex >= 0 && currentScreenIndex < screens.Count
            ? screens[currentScreenIndex].Name
            : "Unknown";

        public EscapeMenuSubMode()
        {
            screens = new List<EscapeMenuScreenBase>();
            currentScreenIndex = 0;
            currentControlIndex = 0;
            IsActive = false;
            activationTime = 0f;
        }

        /// <summary>
        /// Check if we're still in the grace period after activation.
        /// Used to prevent immediate Escape key processing.
        /// </summary>
        public bool IsInActivationGracePeriod()
        {
            return (Time.unscaledTime - activationTime) < ACTIVATION_GRACE_PERIOD;
        }

        /// <summary>
        /// Check if the in-game escape menu (OptionsScreenController) is visible.
        /// </summary>
        public static bool IsEscapeMenuVisible()
        {
            try
            {
                var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsScreen == null)
                    return false;

                // Use the Visible() method from CanvasControllerBase
                return optionsScreen.Visible();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initialize and activate the escape menu sub-mode.
        /// </summary>
        public void Activate()
        {
            try
            {
                optionsScreenController = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsScreenController == null)
                {
                    MelonLogger.Error("EscapeMenuSubMode: OptionsScreenController not found");
                    return;
                }

                // Record activation time for grace period
                activationTime = Time.unscaledTime;

                InitializeScreens();

                // Determine which screen is currently active
                currentScreenIndex = GetActiveScreenIndex();
                currentControlIndex = 0;

                IsActive = true;

                // Announce activation
                var screen = GetCurrentScreen();
                if (screen != null)
                {
                    screen.OnActivate();
                    string announcement = screen.GetActivationAnnouncement();
                    TISpeechMod.Speak($"Escape menu. {announcement}", interrupt: true);

                    // Announce first control
                    if (screen.ControlCount > 0)
                    {
                        string firstControl = screen.ReadControl(0);
                        TISpeechMod.Speak($"1 of {screen.ControlCount}: {firstControl}", interrupt: false);
                    }
                }

                MelonLogger.Msg($"EscapeMenuSubMode activated on screen: {CurrentScreenName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating EscapeMenuSubMode: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivate the escape menu sub-mode.
        /// </summary>
        public void Deactivate()
        {
            // Clean up Codex mode if active
            if (codexSubMode != null)
            {
                codexSubMode.Deactivate();
                codexSubMode = null;
            }

            IsActive = false;
            var screen = GetCurrentScreen();
            screen?.OnDeactivate();
            MelonLogger.Msg("EscapeMenuSubMode deactivated");
        }

        private void InitializeScreens()
        {
            screens.Clear();
            screens.Add(new InGameEscapeMenuScreen());     // 0: Main escape menu
            screens.Add(new InGameSaveScreen());           // 1: Save game
            screens.Add(new InGameLoadScreen());           // 2: Load game
            screens.Add(new InGameSettingsScreen());       // 3: Settings
            screens.Add(new ExitConfirmationScreen());     // 4: Exit confirmation
            MelonLogger.Msg($"EscapeMenuSubMode: Initialized {screens.Count} screens");
        }

        /// <summary>
        /// Determine which sub-screen is currently active based on visible GameObjects.
        /// </summary>
        public int GetActiveScreenIndex()
        {
            if (optionsScreenController == null)
                return 0;

            try
            {
                // Check exit confirmation first (highest priority overlay)
                if (optionsScreenController.exitWithoutSaveWarningObject != null &&
                    optionsScreenController.exitWithoutSaveWarningObject.activeSelf)
                    return 4; // ExitConfirmationScreen

                // Check save menu
                if (optionsScreenController.saveMenuObject != null &&
                    optionsScreenController.saveMenuObject.activeSelf)
                {
                    // Need to check if it's actually showing (Menu.IsOpen)
                    var saveMenu = optionsScreenController.saveMenuObject.GetComponent<Menu>();
                    if (saveMenu != null && saveMenu.IsOpen)
                        return 1; // InGameSaveScreen
                }

                // Check load menu
                if (optionsScreenController.loadMenuObject != null &&
                    optionsScreenController.loadMenuObject.activeSelf)
                {
                    var loadMenu = optionsScreenController.loadMenuObject.GetComponent<Menu>();
                    if (loadMenu != null && loadMenu.IsOpen)
                        return 2; // InGameLoadScreen
                }

                // Check settings menu
                if (optionsScreenController.settingsMenuObject != null &&
                    optionsScreenController.settingsMenuObject.activeSelf)
                {
                    var settingsMenu = optionsScreenController.settingsMenuObject.GetComponent<Menu>();
                    if (settingsMenu != null && settingsMenu.IsOpen)
                        return 3; // InGameSettingsScreen
                }

                // Default to main escape menu
                return 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error determining active screen: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Check for screen context changes (e.g., sub-menu opened/closed, Codex opened/closed).
        /// </summary>
        public bool CheckContextChange()
        {
            // Check for Codex state changes first
            bool codexVisible = CodexSubMode.IsCodexVisible();

            if (codexVisible && codexSubMode == null)
            {
                // Codex just became visible - activate Codex mode
                codexSubMode = new CodexSubMode();
                codexSubMode.Activate();
                MelonLogger.Msg("EscapeMenuSubMode: Switched to Codex mode");
                return true;
            }

            if (!codexVisible && codexSubMode != null)
            {
                // Codex was closed - deactivate Codex mode
                codexSubMode.Deactivate();
                codexSubMode = null;
                MelonLogger.Msg("EscapeMenuSubMode: Returned from Codex mode");

                // Announce return to escape menu
                var screen = GetCurrentScreen();
                if (screen != null)
                {
                    screen.OnActivate();
                    string announcement = screen.GetActivationAnnouncement();
                    TISpeechMod.Speak($"Escape menu. {announcement}", interrupt: true);
                }
                return true;
            }

            // If in Codex mode, no need to check escape menu screens
            if (IsInCodexMode)
                return false;

            // Check for escape menu screen changes
            int newScreenIndex = GetActiveScreenIndex();
            if (newScreenIndex != currentScreenIndex)
            {
                // Deactivate old screen
                var oldScreen = GetCurrentScreen();
                oldScreen?.OnDeactivate();

                // Switch to new screen
                currentScreenIndex = newScreenIndex;
                currentControlIndex = 0;

                // Activate new screen
                var newScreen = GetCurrentScreen();
                if (newScreen != null)
                {
                    newScreen.OnActivate();
                    string announcement = newScreen.GetActivationAnnouncement();
                    TISpeechMod.Speak(announcement, interrupt: true);

                    // Announce first control
                    if (newScreen.ControlCount > 0)
                    {
                        string firstControl = newScreen.ReadControl(0);
                        TISpeechMod.Speak($"1 of {newScreen.ControlCount}: {firstControl}", interrupt: false);
                    }
                }

                MelonLogger.Msg($"EscapeMenuSubMode: Context changed to screen {currentScreenIndex}: {CurrentScreenName}");
                return true;
            }
            return false;
        }

        public EscapeMenuScreenBase GetCurrentScreen()
        {
            if (screens == null || currentScreenIndex < 0 || currentScreenIndex >= screens.Count)
                return null;
            return screens[currentScreenIndex];
        }

        /// <summary>
        /// Check if currently on a sub-screen (not the main escape menu).
        /// </summary>
        public bool IsInSubScreen()
        {
            return currentScreenIndex > 0;
        }

        #region Navigation

        public void NavigatePrevious()
        {
            var screen = GetCurrentScreen();
            if (screen == null || screen.ControlCount == 0)
                return;

            currentControlIndex--;
            if (currentControlIndex < 0)
                currentControlIndex = screen.ControlCount - 1;

            AnnounceCurrentControl();
        }

        public void NavigateNext()
        {
            var screen = GetCurrentScreen();
            if (screen == null || screen.ControlCount == 0)
                return;

            currentControlIndex++;
            if (currentControlIndex >= screen.ControlCount)
                currentControlIndex = 0;

            AnnounceCurrentControl();
        }

        public void ActivateCurrentControl()
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            // Check if this is an InputField - enter text input mode instead
            var controls = screen.GetControls();
            if (controls != null && currentControlIndex >= 0 && currentControlIndex < controls.Count)
            {
                var control = controls[currentControlIndex];
                if (control.Type == MenuControlType.InputField)
                {
                    string announcement = EnterTextInputMode(control);
                    TISpeechMod.Speak(announcement, interrupt: true);
                    return;
                }
            }

            screen.ActivateControl(currentControlIndex);
        }

        public void AdjustCurrentControl(bool increment)
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            screen.AdjustControl(currentControlIndex, increment);
        }

        public void ReadDetail()
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            string detail = screen.ReadControlDetail(currentControlIndex);
            TISpeechMod.Speak(detail, interrupt: true);
        }

        public void ListAllControls()
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            string list = screen.ListAllControls();
            TISpeechMod.Speak(list, interrupt: true);
        }

        /// <summary>
        /// Invoke the "Back to Game" button to close the escape menu.
        /// </summary>
        public void InvokeBackToGame()
        {
            try
            {
                if (optionsScreenController != null)
                {
                    optionsScreenController.OnReturnPressed();
                    TISpeechMod.Speak("Returned to game", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error invoking Back to Game: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate by letter (A-Z).
        /// </summary>
        public void NavigateByLetter(char letter)
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            int newIndex = screen.FindNextControlByLetter(letter, currentControlIndex);
            if (newIndex >= 0)
            {
                currentControlIndex = newIndex;
                AnnounceCurrentControl();
            }
            else
            {
                TISpeechMod.Speak($"No controls starting with {letter}", interrupt: true);
            }
        }

        #endregion

        #region Text Input Mode

        /// <summary>
        /// Enter text input mode for an input field control.
        /// </summary>
        public string EnterTextInputMode(MenuControl control)
        {
            if (control == null || control.Type != MenuControlType.InputField || control.GameObject == null)
                return "Cannot enter text input mode";

            targetInputField = control.GameObject.GetComponent<TMP_InputField>();
            if (targetInputField == null)
                return "Input field not found";

            isEnteringText = true;
            textInputLabel = control.Label;
            textInput = targetInputField.text ?? "";
            maxTextLength = targetInputField.characterLimit > 0 ? targetInputField.characterLimit : 100;

            MelonLogger.Msg($"Entered text input mode for '{textInputLabel}', current value: '{textInput}'");

            if (string.IsNullOrEmpty(textInput))
                return $"{textInputLabel}. Type to enter text. Press Enter to confirm, Escape to cancel.";
            else
                return $"{textInputLabel}: {textInput}. Type to modify. Press Enter to confirm, Escape to cancel.";
        }

        /// <summary>
        /// Handle a character input while in text input mode.
        /// </summary>
        public bool HandleCharacter(char c)
        {
            if (!isEnteringText) return false;

            // Allow alphanumeric, space, and common filename characters
            if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-' && c != '.')
                return false;

            // Check length limit
            if (textInput.Length >= maxTextLength)
            {
                TISpeechMod.Speak("Maximum length reached", interrupt: true);
                return true;
            }

            textInput += c;

            // Update the input field in real-time
            if (targetInputField != null)
                targetInputField.text = textInput;

            return true;
        }

        /// <summary>
        /// Handle backspace in text input mode.
        /// </summary>
        public bool HandleBackspace()
        {
            if (!isEnteringText) return false;

            if (textInput.Length > 0)
            {
                textInput = textInput.Substring(0, textInput.Length - 1);

                // Update the input field
                if (targetInputField != null)
                    targetInputField.text = textInput;

                return true;
            }

            return true; // Still consume the key even if nothing to delete
        }

        /// <summary>
        /// Get announcement for current text input state.
        /// </summary>
        public string GetTextInputAnnouncement()
        {
            if (string.IsNullOrEmpty(textInput))
                return $"{textInputLabel}: empty";
            return $"{textInputLabel}: {textInput}";
        }

        /// <summary>
        /// Apply the entered text and exit text input mode.
        /// </summary>
        public string ApplyTextInput()
        {
            if (!isEnteringText)
                return "";

            string result;
            if (targetInputField != null)
            {
                targetInputField.text = textInput;
                result = $"Set {textInputLabel} to: {textInput}";
                MelonLogger.Msg($"Applied text input: '{textInput}'");
            }
            else
            {
                result = "Error: input field not found";
            }

            // Clear state
            isEnteringText = false;
            textInput = "";
            textInputLabel = "";
            targetInputField = null;

            return result;
        }

        /// <summary>
        /// Cancel text input mode without applying changes.
        /// </summary>
        public string CancelTextInput()
        {
            if (!isEnteringText)
                return "";

            // Restore original value if we have the input field
            // (The original value was already in the field, we just modified our local copy)

            string result = $"Cancelled {textInputLabel} input";
            MelonLogger.Msg($"Cancelled text input for '{textInputLabel}'");

            // Clear state
            isEnteringText = false;
            textInput = "";
            textInputLabel = "";
            targetInputField = null;

            return result;
        }

        #endregion

        private void AnnounceCurrentControl()
        {
            var screen = GetCurrentScreen();
            if (screen == null)
                return;

            int count = screen.ControlCount;
            if (count == 0)
            {
                TISpeechMod.Speak("No controls", interrupt: true);
                return;
            }

            if (currentControlIndex < 0)
                currentControlIndex = 0;
            if (currentControlIndex >= count)
                currentControlIndex = count - 1;

            string controlText = screen.ReadControl(currentControlIndex);
            TISpeechMod.Speak($"{currentControlIndex + 1} of {count}: {controlText}", interrupt: true);
        }
    }
}
