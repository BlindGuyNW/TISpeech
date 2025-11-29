using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode.MenuMode.Screens
{
    /// <summary>
    /// Menu screen for the Mods management menu.
    /// Provides navigation through mod list and enable/disable controls.
    /// </summary>
    public class ModsScreen : MenuScreenBase
    {
        public override string Name => "Mods";

        private List<MenuControl> controls = new List<MenuControl>();
        private ModMenuController modController;
        private List<ModItemListItemController> modItems = new List<ModItemListItemController>();

        public override List<MenuControl> GetControls()
        {
            return controls;
        }

        /// <summary>
        /// Check if the Mods menu is currently visible.
        /// </summary>
        public static bool IsVisible()
        {
            var controller = UnityEngine.Object.FindObjectOfType<ModMenuController>();
            if (controller == null)
                return false;

            // ModMenuController doesn't extend MenuController, so check mainModPanel
            if (controller.mainModPanel != null && controller.mainModPanel.activeInHierarchy)
                return true;

            return false;
        }

        public override void Refresh()
        {
            controls.Clear();
            modItems.Clear();

            try
            {
                modController = UnityEngine.Object.FindObjectOfType<ModMenuController>();
                if (modController == null)
                {
                    MelonLogger.Msg("ModsScreen: ModMenuController not found");
                    return;
                }

                // Use Mods toggle
                if (modController.useModsToggle != null &&
                    modController.useModsToggle.gameObject.activeInHierarchy)
                {
                    var useModsControl = MenuControl.FromToggle(
                        modController.useModsToggle,
                        "Enable Mods");
                    if (useModsControl != null)
                    {
                        useModsControl.DetailText = "Enable or disable mod loading for the game";
                        controls.Add(useModsControl);
                    }
                }

                // Get mod list items
                if (modController.modListManager != null)
                {
                    var items = modController.modListManager.GetComponentsInChildren<ModItemListItemController>(includeInactive: false);
                    if (items != null && items.Length > 0)
                    {
                        // Add divider before mod list
                        controls.Add(new MenuControl
                        {
                            Type = MenuControlType.Button,
                            Label = "--- Installed Mods ---",
                            IsInteractable = false
                        });

                        foreach (var item in items)
                        {
                            modItems.Add(item);

                            string modName = item.modName != null ? TISpeechMod.CleanText(item.modName.text) : "Unknown Mod";
                            string status = item.modStatus == ModItemListItemController.ModStatus.Enabled ? "Enabled" : "Disabled";

                            var modControl = new MenuControl
                            {
                                Type = MenuControlType.ScrollListItem,
                                Label = $"{modName} ({status})",
                                DetailText = $"{modName}. Status: {status}. Press Enter to toggle.",
                                GameObject = item.gameObject,
                                IsInteractable = true
                            };
                            controls.Add(modControl);
                        }
                    }
                }

                // If no mods found but Use Mods toggle is on, show a message
                if (modItems.Count == 0)
                {
                    controls.Add(new MenuControl
                    {
                        Type = MenuControlType.Button,
                        Label = "No mods installed",
                        IsInteractable = false
                    });
                }

                // Workshop buttons (if available)
                if (modController.steamWorkshopBrowsePanel != null)
                {
                    controls.Add(new MenuControl
                    {
                        Type = MenuControlType.Button,
                        Label = "--- Workshop ---",
                        IsInteractable = false
                    });

                    // Find browse button
                    var browsePanel = modController.tabWorkshopBrowseText?.transform.parent;
                    if (browsePanel != null && browsePanel.gameObject.activeInHierarchy)
                    {
                        var browseButton = browsePanel.GetComponent<Button>();
                        if (browseButton != null)
                        {
                            var browseControl = MenuControl.FromButton(browseButton, "Browse Workshop");
                            if (browseControl != null)
                            {
                                browseControl.DetailText = "Browse Steam Workshop for mods";
                                controls.Add(browseControl);
                            }
                        }
                    }

                    // Find upload button
                    var uploadPanel = modController.tabWorkshopUploadText?.transform.parent;
                    if (uploadPanel != null && uploadPanel.gameObject.activeInHierarchy)
                    {
                        var uploadButton = uploadPanel.GetComponent<Button>();
                        if (uploadButton != null)
                        {
                            var uploadControl = MenuControl.FromButton(uploadButton, "Upload to Workshop");
                            if (uploadControl != null)
                            {
                                uploadControl.DetailText = "Upload a mod to Steam Workshop";
                                controls.Add(uploadControl);
                            }
                        }
                    }
                }

                MelonLogger.Msg($"ModsScreen: Found {modItems.Count} mods, {controls.Count} total controls");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing ModsScreen: {ex.Message}");
            }
        }

        public override void ActivateControl(int index)
        {
            if (index < 0 || index >= controls.Count)
                return;

            var control = controls[index];

            if (!control.IsInteractable)
            {
                if (control.Label.StartsWith("---") || control.Label == "No mods installed")
                    return; // Divider, do nothing
                TISpeechMod.Speak($"{control.Label} is not available", interrupt: true);
                return;
            }

            // For toggles, activate cycles the value
            if (control.Type == MenuControlType.Toggle)
            {
                control.Activate();
                control.RefreshValue();
                TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
                return;
            }

            // For mod items, toggle their enabled state
            if (control.Type == MenuControlType.ScrollListItem)
            {
                // Find the corresponding mod item
                // Index needs to account for non-mod controls before the mod list
                int modIndex = index - GetModListStartIndex();
                if (modIndex >= 0 && modIndex < modItems.Count)
                {
                    var modItem = modItems[modIndex];
                    if (modItem.modStatus == ModItemListItemController.ModStatus.Enabled)
                    {
                        modItem.OnClickDisable();
                        TISpeechMod.Speak($"Disabled {TISpeechMod.CleanText(modItem.modName.text)}", interrupt: true);
                    }
                    else
                    {
                        modItem.OnClickEnable();
                        TISpeechMod.Speak($"Enabled {TISpeechMod.CleanText(modItem.modName.text)}", interrupt: true);
                    }

                    // Refresh to update status
                    Refresh();
                }
                return;
            }

            // For buttons, announce and activate
            TISpeechMod.Speak($"Activating {control.Label}", interrupt: true);
            control.Activate();

            MelonLogger.Msg($"ModsScreen: Activated '{control.Label}'");
        }

        private int GetModListStartIndex()
        {
            // Find the index where the mod list starts (after "--- Installed Mods ---" divider)
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i].Label == "--- Installed Mods ---")
                    return i + 1;
            }
            return controls.Count; // No mod list found
        }

        public override string ReadControlDetail(int index)
        {
            if (index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];

            // For mod items, provide more detail
            if (control.Type == MenuControlType.ScrollListItem)
            {
                int modIndex = index - GetModListStartIndex();
                if (modIndex >= 0 && modIndex < modItems.Count)
                {
                    var modItem = modItems[modIndex];
                    string modName = TISpeechMod.CleanText(modItem.modName.text);
                    string status = modItem.modStatus == ModItemListItemController.ModStatus.Enabled
                        ? "Currently enabled"
                        : "Currently disabled";

                    // Check if from workshop
                    string workshopInfo = "";
                    if (modItem.modWorkshopText != null && !string.IsNullOrEmpty(modItem.modWorkshopText.text))
                    {
                        workshopInfo = " (Steam Workshop subscription)";
                    }

                    return $"{modName}. {status}{workshopInfo}. Press Enter to toggle.";
                }
            }

            return control.GetDetail();
        }

        public override string GetActivationAnnouncement()
        {
            int enabledCount = 0;
            int disabledCount = 0;

            foreach (var item in modItems)
            {
                if (item.modStatus == ModItemListItemController.ModStatus.Enabled)
                    enabledCount++;
                else
                    disabledCount++;
            }

            string modInfo = modItems.Count > 0
                ? $"{modItems.Count} mods installed ({enabledCount} enabled, {disabledCount} disabled)"
                : "No mods installed";

            return $"{Name}. {modInfo}.";
        }
    }
}
