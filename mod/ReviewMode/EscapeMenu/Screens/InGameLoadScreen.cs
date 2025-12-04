using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.MenuMode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TISpeech.ReviewMode.EscapeMenu.Screens
{
    /// <summary>
    /// Escape menu screen for loading a saved game.
    /// Provides navigation through save files and action buttons.
    /// </summary>
    public class InGameLoadScreen : EscapeMenuScreenBase
    {
        public override string Name => isInDeleteConfirmation ? "Delete Confirmation" : "Load Game";

        private List<MenuControl> controls = new List<MenuControl>();
        private LoadMenuController loadController;
        private CreateSaveFileScrollList saveList;
        private List<LoadSaveButton> saveButtons = new List<LoadSaveButton>();
        private bool isInDeleteConfirmation = false;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Load Game menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
            if (optionsScreen == null)
                return false;

            if (optionsScreen.loadMenuObject == null)
                return false;

            var menu = optionsScreen.loadMenuObject.GetComponent<Menu>();
            return menu != null && menu.IsOpen;
        }

        public override void Refresh()
        {
            controls.Clear();
            saveButtons.Clear();

            try
            {
                // Get LoadMenuController from OptionsScreenController
                var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsScreen?.optionsMenuController == null)
                {
                    MelonLogger.Msg("InGameLoadScreen: optionsMenuController not found");
                    return;
                }

                loadController = optionsScreen.optionsMenuController.loadMenuController;
                if (loadController == null)
                {
                    MelonLogger.Msg("InGameLoadScreen: LoadMenuController not found");
                    return;
                }

                // Check if delete confirmation dialog is visible
                isInDeleteConfirmation = loadController.deletePanelObject != null &&
                                         loadController.deletePanelObject.activeSelf;

                if (isInDeleteConfirmation)
                {
                    RefreshDeleteConfirmation();
                    return;
                }

                RefreshSaveList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing InGameLoadScreen: {ex.Message}");
            }
        }

        private void RefreshDeleteConfirmation()
        {
            string saveName = saveList?.selectedButton?.saveInfo.name ?? "selected save";

            // Find Confirm and Cancel buttons
            if (loadController.confirmDeleteButtonText != null)
            {
                var confirmButton = loadController.confirmDeleteButtonText.GetComponentInParent<Button>();
                if (confirmButton != null && confirmButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(loadController.confirmDeleteButtonText.text);
                    if (string.IsNullOrWhiteSpace(label)) label = "Confirm";

                    var control = MenuControl.FromButton(confirmButton, label);
                    if (control != null)
                    {
                        control.Action = "ConfirmDelete";
                        control.DetailText = $"Confirm deletion of {saveName}";
                        controls.Add(control);
                    }
                }
            }

            if (loadController.cancelDeleteButtonText != null)
            {
                var cancelButton = loadController.cancelDeleteButtonText.GetComponentInParent<Button>();
                if (cancelButton != null && cancelButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(loadController.cancelDeleteButtonText.text);
                    if (string.IsNullOrWhiteSpace(label)) label = "Cancel";

                    var control = MenuControl.FromButton(cancelButton, label);
                    if (control != null)
                    {
                        control.Action = "CancelDelete";
                        control.DetailText = "Cancel and return to save list";
                        controls.Add(control);
                    }
                }
            }

            MelonLogger.Msg($"InGameLoadScreen: Delete confirmation with {controls.Count} buttons");
        }

        private void RefreshSaveList()
        {
            saveList = loadController.saveList;
            if (saveList == null)
            {
                MelonLogger.Msg("InGameLoadScreen: saveList not found");
                return;
            }

            // Iterate through save files
            if (saveList.contentPanel != null)
            {
                var buttonsByPath = new Dictionary<string, LoadSaveButton>();

                foreach (Transform child in saveList.contentPanel)
                {
                    if (!child.gameObject.activeInHierarchy)
                        continue;

                    var button = child.GetComponent<LoadSaveButton>();
                    if (button == null || button.saveInfo.name == null)
                        continue;

                    // Skip if file no longer exists
                    if (!File.Exists(button.saveInfo.path))
                        continue;

                    buttonsByPath[button.saveInfo.path] = button;
                }

                // Add deduplicated buttons, sorted by date (newest first)
                foreach (var button in buttonsByPath.Values.OrderByDescending(b => b.saveInfo.dateTime))
                {
                    saveButtons.Add(button);

                    string label = button.saveInfo.name;
                    string detail = $"{button.saveInfo.name}, saved {button.saveInfo.dateTime.ToShortDateString()} at {button.saveInfo.dateTime.ToShortTimeString()}";

                    var control = new MenuControl
                    {
                        Type = MenuControlType.ScrollListItem,
                        Label = label,
                        DetailText = detail,
                        GameObject = button.gameObject,
                        IsInteractable = button.button?.interactable ?? true
                    };
                    controls.Add(control);
                }
            }

            // Add divider if there are save files
            if (saveButtons.Count > 0)
            {
                controls.Add(new MenuControl
                {
                    Type = MenuControlType.Button,
                    Label = "--- Actions ---",
                    IsInteractable = false
                });
            }

            // Add action buttons
            if (loadController.loadButton != null)
            {
                var loadControl = MenuControl.FromButton(loadController.loadButton, "Load Selected");
                if (loadControl != null)
                {
                    loadControl.Action = "Load";
                    controls.Add(loadControl);
                }
            }

            if (loadController.deleteButton != null)
            {
                var deleteControl = MenuControl.FromButton(loadController.deleteButton, "Delete Selected");
                if (deleteControl != null)
                {
                    deleteControl.Action = "Delete";
                    controls.Add(deleteControl);
                }
            }

            if (loadController.openSaveFolderButton != null && loadController.openSaveFolderButton.gameObject.activeInHierarchy)
            {
                var openFolderControl = MenuControl.FromButton(loadController.openSaveFolderButton, "Open Save Folder");
                if (openFolderControl != null)
                {
                    openFolderControl.Action = "OpenFolder";
                    controls.Add(openFolderControl);
                }
            }

            MelonLogger.Msg($"InGameLoadScreen: Found {saveButtons.Count} save files, {controls.Count} total controls");
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            // Check if this is a save file (scroll list item)
            if (control.Type == MenuControlType.ScrollListItem && index < saveButtons.Count)
            {
                var saveButton = saveButtons[index];
                if (saveButton != null && saveList != null)
                {
                    saveList.SelectSaveFile(saveButton);
                    TISpeechMod.Speak($"Selected {control.Label}. Press Enter on Load Selected to load.", interrupt: true);

                    // Update button states
                    RefreshButtonStates();
                    MelonLogger.Msg($"InGameLoadScreen: Selected save file '{control.Label}'");
                }
                return;
            }

            // Handle action buttons
            if (!control.IsInteractable)
            {
                TISpeechMod.Speak($"{control.Label} is not available", interrupt: true);
                return;
            }

            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"InGameLoadScreen: Activated '{control.Label}'");

            // Check if state changed (delete dialog appeared/disappeared)
            CheckForStateChange();
        }

        private void RefreshButtonStates()
        {
            foreach (var control in controls)
            {
                if (control.Type == MenuControlType.Button && control.GameObject != null)
                {
                    var button = control.GameObject.GetComponent<Button>();
                    if (button != null)
                    {
                        control.IsInteractable = button.interactable;
                    }
                }
            }
        }

        private void CheckForStateChange()
        {
            bool currentDeleteState = loadController.deletePanelObject != null &&
                                      loadController.deletePanelObject.activeSelf;

            if (currentDeleteState != isInDeleteConfirmation)
            {
                Refresh();

                string announcement = GetActivationAnnouncement();
                TISpeechMod.Speak(announcement, interrupt: true);

                if (controls.Count > 0)
                {
                    TISpeechMod.Speak($"1 of {controls.Count}: {controls[0].GetAnnouncement()}", interrupt: false);
                }
            }
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            if (control.Type == MenuControlType.ScrollListItem && index < saveButtons.Count)
            {
                var saveButton = saveButtons[index];
                var info = saveButton.saveInfo;

                bool isSelected = saveList?.selectedButton == saveButton;
                string selectedText = isSelected ? " (currently selected)" : "";

                return $"{info.name}{selectedText}. Saved on {info.dateTime.ToLongDateString()} at {info.dateTime.ToLongTimeString()}.";
            }

            return control.GetDetail();
        }

        public override string GetActivationAnnouncement()
        {
            if (isInDeleteConfirmation)
            {
                string saveName = saveList?.selectedButton?.saveInfo.name ?? "selected save";
                return $"Delete confirmation. Delete {saveName}? {controls.Count} options.";
            }

            int saveCount = saveButtons.Count;
            if (saveCount == 0)
            {
                return $"{Name}. No save files found.";
            }
            return $"{Name}. {saveCount} save files.";
        }
    }
}
