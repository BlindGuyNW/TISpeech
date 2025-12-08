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
    /// Escape menu screen for saving the game.
    /// Provides navigation through save files, filename input, and action buttons.
    /// </summary>
    public class InGameSaveScreen : EscapeMenuScreenBase
    {
        public override string Name => isInDeleteConfirmation ? "Delete Confirmation" : "Save Game";

        private List<MenuControl> controls = new List<MenuControl>();
        private SaveMenuController saveController;
        private CreateSaveFileScrollList saveList;
        private List<LoadSaveButton> saveButtons = new List<LoadSaveButton>();
        private bool isInDeleteConfirmation = false;

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Save Game menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
            if (optionsScreen == null)
                return false;

            if (optionsScreen.saveMenuObject == null)
                return false;

            var menu = optionsScreen.saveMenuObject.GetComponent<Menu>();
            return menu != null && menu.IsOpen;
        }

        public override void Refresh()
        {
            controls.Clear();
            saveButtons.Clear();

            try
            {
                // Get SaveMenuController from OptionsScreenController
                var optionsScreen = UnityEngine.Object.FindObjectOfType<OptionsScreenController>();
                if (optionsScreen?.optionsMenuController == null)
                {
                    MelonLogger.Msg("InGameSaveScreen: optionsMenuController not found");
                    return;
                }

                saveController = optionsScreen.optionsMenuController.saveMenuController;
                if (saveController == null)
                {
                    MelonLogger.Msg("InGameSaveScreen: SaveMenuController not found");
                    return;
                }

                // Check if delete confirmation dialog is visible
                isInDeleteConfirmation = saveController.deletePanelObject != null &&
                                         saveController.deletePanelObject.activeSelf;

                if (isInDeleteConfirmation)
                {
                    RefreshDeleteConfirmation();
                    return;
                }

                RefreshSaveList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing InGameSaveScreen: {ex.Message}");
            }
        }

        private void RefreshDeleteConfirmation()
        {
            // Get the save name being deleted
            string saveName = saveList?.selectedButton?.saveInfo.name ?? "selected save";

            // Find Confirm and Cancel buttons
            if (saveController.confirmDeleteButtonText != null)
            {
                var confirmButton = saveController.confirmDeleteButtonText.GetComponentInParent<Button>();
                if (confirmButton != null && confirmButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(saveController.confirmDeleteButtonText.text);
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

            if (saveController.cancelDeleteButtonText != null)
            {
                var cancelButton = saveController.cancelDeleteButtonText.GetComponentInParent<Button>();
                if (cancelButton != null && cancelButton.gameObject.activeInHierarchy)
                {
                    string label = TISpeechMod.CleanText(saveController.cancelDeleteButtonText.text);
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

            MelonLogger.Msg($"InGameSaveScreen: Delete confirmation with {controls.Count} buttons");
        }

        private void RefreshSaveList()
        {
            saveList = saveController.saveList;
            if (saveList == null)
            {
                MelonLogger.Msg("InGameSaveScreen: saveList not found");
                return;
            }

            // Add filename input field first
            if (saveController.saveFileName != null && saveController.saveFileName.gameObject.activeInHierarchy)
            {
                var inputControl = new MenuControl
                {
                    Type = MenuControlType.InputField,
                    Label = "Save File Name",
                    CurrentValue = saveController.saveFileName.text ?? "",
                    GameObject = saveController.saveFileName.gameObject,
                    IsInteractable = saveController.saveFileName.interactable,
                    DetailText = "Type a name for the save file, or select an existing save to overwrite"
                };
                controls.Add(inputControl);
            }

            // Add divider
            controls.Add(new MenuControl
            {
                Type = MenuControlType.Button,
                Label = "--- Existing Saves ---",
                IsInteractable = false
            });

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
            controls.Add(new MenuControl
            {
                Type = MenuControlType.Button,
                Label = "--- Actions ---",
                IsInteractable = false
            });

            // Add action buttons
            if (saveController.saveButton != null)
            {
                var saveControl = MenuControl.FromButton(saveController.saveButton, "Save");
                if (saveControl != null)
                {
                    saveControl.Action = "Save";
                    controls.Add(saveControl);
                }
            }

            if (saveController.deleteButton != null)
            {
                var deleteControl = MenuControl.FromButton(saveController.deleteButton, "Delete Selected");
                if (deleteControl != null)
                {
                    deleteControl.Action = "Delete";
                    controls.Add(deleteControl);
                }
            }

            if (saveController.returnButton != null)
            {
                var returnControl = MenuControl.FromButton(saveController.returnButton, "Return");
                if (returnControl != null)
                {
                    returnControl.Action = "Return";
                    controls.Add(returnControl);
                }
            }

            MelonLogger.Msg($"InGameSaveScreen: Found {saveButtons.Count} save files, {controls.Count} total controls");
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            // Check if this is a save file (scroll list item)
            if (control.Type == MenuControlType.ScrollListItem)
            {
                // Find the corresponding save button
                int saveButtonIndex = GetSaveButtonIndexForControl(index);
                if (saveButtonIndex >= 0 && saveButtonIndex < saveButtons.Count)
                {
                    var saveButton = saveButtons[saveButtonIndex];
                    if (saveButton != null && saveList != null)
                    {
                        saveList.SelectSaveFile(saveButton);
                        // Also update the filename input
                        if (saveController.saveFileName != null)
                        {
                            saveController.saveFileName.text = saveButton.saveInfo.name;
                        }
                        TISpeechMod.Speak($"Selected {control.Label}. Press Enter on Save to overwrite.", interrupt: true);
                        MelonLogger.Msg($"InGameSaveScreen: Selected save file '{control.Label}'");
                    }
                }
                return;
            }

            // Handle input field - can't really "activate" it in the same way
            if (control.Type == MenuControlType.InputField)
            {
                TISpeechMod.Speak($"Save file name: {control.CurrentValue}. Type to change the name.", interrupt: true);
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

            MelonLogger.Msg($"InGameSaveScreen: Activated '{control.Label}'");

            // Check if state changed (delete dialog appeared)
            CheckForStateChange();
        }

        private int GetSaveButtonIndexForControl(int controlIndex)
        {
            // Count how many scroll list items come before this control
            int saveIndex = 0;
            for (int i = 0; i < controlIndex; i++)
            {
                if (controls[i].Type == MenuControlType.ScrollListItem)
                    saveIndex++;
            }
            // The current control is a scroll list item, so saveIndex is its index
            // But we need to find which save button it corresponds to
            int foundIndex = -1;
            int currentSaveIndex = 0;
            for (int i = 0; i < controls.Count && i <= controlIndex; i++)
            {
                if (controls[i].Type == MenuControlType.ScrollListItem)
                {
                    if (i == controlIndex)
                    {
                        foundIndex = currentSaveIndex;
                        break;
                    }
                    currentSaveIndex++;
                }
            }
            return foundIndex;
        }

        private void CheckForStateChange()
        {
            bool currentDeleteState = saveController.deletePanelObject != null &&
                                      saveController.deletePanelObject.activeSelf;

            if (currentDeleteState != isInDeleteConfirmation)
            {
                // State changed - refresh
                Refresh();

                // Announce the new state
                string announcement = GetActivationAnnouncement();
                TISpeechMod.Speak(announcement, interrupt: true);

                if (controls.Count > 0)
                {
                    TISpeechMod.Speak($"{controls[0].GetAnnouncement()}, 1 of {controls.Count}", interrupt: false);
                }
            }
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For save files, provide more detail
            if (control.Type == MenuControlType.ScrollListItem)
            {
                int saveButtonIndex = GetSaveButtonIndexForControl(index);
                if (saveButtonIndex >= 0 && saveButtonIndex < saveButtons.Count)
                {
                    var saveButton = saveButtons[saveButtonIndex];
                    var info = saveButton.saveInfo;

                    bool isSelected = saveList?.selectedButton == saveButton;
                    string selectedText = isSelected ? " (currently selected)" : "";

                    return $"{info.name}{selectedText}. Saved on {info.dateTime.ToLongDateString()} at {info.dateTime.ToLongTimeString()}.";
                }
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
                return $"{Name}. No existing saves. Enter a filename and press Save.";
            }
            return $"{Name}. {saveCount} existing saves.";
        }
    }
}
