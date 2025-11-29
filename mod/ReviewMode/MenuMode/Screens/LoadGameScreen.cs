using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the Load Game menu.
    /// Provides navigation through save files and action buttons.
    /// Also handles the delete confirmation dialog.
    /// </summary>
    public class LoadGameScreen : MenuScreenBase
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
            var controller = UnityEngine.Object.FindObjectOfType<LoadMenuController>();
            if (controller == null)
                return false;

            // Use the Menu.IsOpen property - this is the definitive check
            if (controller.menu != null && controller.menu.IsOpen)
                return true;

            return false;
        }

        public override void Refresh()
        {
            controls.Clear();
            saveButtons.Clear();

            try
            {
                loadController = UnityEngine.Object.FindObjectOfType<LoadMenuController>();
                if (loadController == null)
                {
                    MelonLogger.Msg("LoadGameScreen: LoadMenuController not found");
                    return;
                }

                // Check if delete confirmation dialog is visible
                isInDeleteConfirmation = loadController.deletePanelObject != null &&
                                         loadController.deletePanelObject.activeInHierarchy;

                if (isInDeleteConfirmation)
                {
                    RefreshDeleteConfirmation();
                    return;
                }

                RefreshSaveList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing LoadGameScreen: {ex.Message}");
            }
        }

        private void RefreshDeleteConfirmation()
        {
            // Get the save name being deleted
            string saveName = saveList?.selectedButton?.saveInfo.name ?? "selected save";

            // Find the Confirm and Cancel buttons in the delete panel
            var deletePanel = loadController.deletePanelObject;
            if (deletePanel == null)
                return;

            var buttons = deletePanel.GetComponentsInChildren<Button>(includeInactive: false);
            foreach (var button in buttons)
            {
                var tmpText = button.GetComponentInChildren<TMP_Text>();
                string label = tmpText?.text ?? button.gameObject.name;
                label = TISpeechMod.CleanText(label);

                // Try to identify Confirm vs Cancel based on text or object name
                string buttonName = button.gameObject.name.ToLower();
                bool isConfirm = buttonName.Contains("confirm") || buttonName.Contains("delete") ||
                                 label.ToLower().Contains("delete");

                var control = MenuControl.FromButton(button, label);
                if (control != null)
                {
                    control.DetailText = isConfirm
                        ? $"Confirm deletion of {saveName}"
                        : "Cancel and return to save list";
                    controls.Add(control);
                }
            }

            MelonLogger.Msg($"LoadGameScreen: Delete confirmation with {controls.Count} buttons");
        }

        private void RefreshSaveList()
        {
            saveList = loadController.saveList;
            if (saveList == null)
            {
                MelonLogger.Msg("LoadGameScreen: saveList not found");
                return;
            }

            // Iterate through direct children of contentPanel (each is a save button)
            // Note: Unity's Destroy() is deferred, so after PopulateList() is called twice
            // (from Start and OnEnable), we may see both old and new buttons until frame end.
            // Collect all buttons first, then deduplicate keeping the LAST occurrence
            // (the newly created one, not the one marked for destruction).
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

                    // Skip if file no longer exists (was just deleted)
                    if (!File.Exists(button.saveInfo.path))
                        continue;

                    // Keep the LAST occurrence (overwrites previous, which is the one pending Destroy)
                    buttonsByPath[button.saveInfo.path] = button;
                }

                // Now add the deduplicated buttons, sorted by date (newest first)
                foreach (var button in buttonsByPath.Values.OrderByDescending(b => b.saveInfo.dateTime))
                {
                    saveButtons.Add(button);

                    // Create a control for each save file
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
                    controls.Add(loadControl);
            }

            if (loadController.deleteButton != null)
            {
                var deleteControl = MenuControl.FromButton(loadController.deleteButton, "Delete Selected");
                if (deleteControl != null)
                    controls.Add(deleteControl);
            }

            if (loadController.openSaveFolderButton != null && loadController.openSaveFolderButton.gameObject.activeInHierarchy)
            {
                var openFolderControl = MenuControl.FromButton(loadController.openSaveFolderButton, "Open Save Folder");
                if (openFolderControl != null)
                    controls.Add(openFolderControl);
            }

            MelonLogger.Msg($"LoadGameScreen: Found {saveButtons.Count} save files, {controls.Count} total controls");
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            // Check if this is a save file (scroll list item)
            if (control.Type == MenuControlType.ScrollListItem && index < saveButtons.Count)
            {
                // Select this save file by calling the scroll list's SelectSaveFile method
                var saveButton = saveButtons[index];
                if (saveButton != null && saveList != null)
                {
                    saveList.SelectSaveFile(saveButton);
                    TISpeechMod.Speak($"Selected {control.Label}. Press Enter on Load Selected to load.", interrupt: true);

                    // Update the Load/Delete button states
                    RefreshButtonStates();
                    MelonLogger.Msg($"LoadGameScreen: Selected save file '{control.Label}'");
                }
                return;
            }

            // Handle action buttons
            if (!control.IsInteractable)
            {
                TISpeechMod.Speak($"{control.Label} is not available", interrupt: true);
                return;
            }

            // Announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"LoadGameScreen: Activated '{control.Label}'");

            // Check if state changed (delete dialog appeared or disappeared)
            if (HasStateChanged())
            {
                // Refresh to show the new controls
                Refresh();

                // Reset the controller's index to 0
                ReviewModeController.Instance?.ResetMenuControlIndex();

                // Announce the new state
                string announcement = GetActivationAnnouncement();
                TISpeechMod.Speak(announcement, interrupt: true);

                // Announce first control
                if (controls.Count > 0)
                {
                    TISpeechMod.Speak($"1 of {controls.Count}: {controls[0].GetAnnouncement()}", interrupt: false);
                }
            }
        }

        private void RefreshButtonStates()
        {
            // Update the interactable state of action buttons
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

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For save files, provide more detail
            if (control.Type == MenuControlType.ScrollListItem && index < saveButtons.Count)
            {
                var saveButton = saveButtons[index];
                var info = saveButton.saveInfo;

                // Check if this is the currently selected save
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

        /// <summary>
        /// Check if the internal state has changed (e.g., delete dialog opened/closed).
        /// Used by the context change detection to know when to refresh.
        /// </summary>
        public bool HasStateChanged()
        {
            if (loadController == null)
                return false;

            bool currentDeleteState = loadController.deletePanelObject != null &&
                                      loadController.deletePanelObject.activeInHierarchy;

            return currentDeleteState != isInDeleteConfirmation;
        }
    }
}
