using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Current step in the ship design process.
    /// </summary>
    public enum DesignerStep
    {
        /// <summary>Select hull for new design</summary>
        SelectHull,
        /// <summary>Navigate between design zones</summary>
        NavigateZones,
        /// <summary>Navigate items within a zone</summary>
        NavigateZoneItems,
        /// <summary>Select a component for a slot</summary>
        SelectComponent,
        /// <summary>Enter class name for the design</summary>
        EnterClassName,
        /// <summary>Confirm save</summary>
        ConfirmSave
    }

    /// <summary>
    /// Ship design zones corresponding to slot types.
    /// </summary>
    public enum DesignZone
    {
        Overview,
        Nose,
        Hull,
        Lateral,
        Tail,
        Propulsion,
        Save
    }

    /// <summary>
    /// Sub-mode for ship design creation and editing.
    /// Provides keyboard navigation through ship components.
    /// </summary>
    public class ShipDesignerSubMode
    {
        #region Properties

        /// <summary>
        /// The design being edited (a working copy).
        /// </summary>
        public TISpaceShipTemplate WorkingDesign { get; private set; }

        /// <summary>
        /// The original design being edited (null for new designs).
        /// </summary>
        public TISpaceShipTemplate OriginalDesign { get; }

        /// <summary>
        /// Whether this is a new design (vs editing existing).
        /// </summary>
        public bool IsNewDesign => OriginalDesign == null;

        /// <summary>
        /// The player faction.
        /// </summary>
        public TIFactionState Faction { get; }

        /// <summary>
        /// Current step in the design process.
        /// </summary>
        public DesignerStep CurrentStep { get; private set; }

        /// <summary>
        /// Current zone being viewed/edited.
        /// </summary>
        public DesignZone CurrentZone { get; private set; }

        /// <summary>
        /// Current index within the current zone's items.
        /// </summary>
        public int CurrentZoneItemIndex { get; private set; }

        /// <summary>
        /// Current index when selecting from a list (hulls, components, etc).
        /// </summary>
        public int CurrentSelectionIndex { get; private set; }

        /// <summary>
        /// Available hulls for selection.
        /// </summary>
        public List<TIShipHullTemplate> AvailableHulls { get; private set; }

        /// <summary>
        /// Current component options for selection.
        /// </summary>
        public List<TIShipPartTemplate> CurrentComponentOptions { get; private set; }

        /// <summary>
        /// Current slot type being edited.
        /// </summary>
        public ShipModuleSlotType CurrentSlotType { get; private set; }

        /// <summary>
        /// Current slot index being edited.
        /// </summary>
        public int CurrentSlotIndex { get; private set; }

        /// <summary>
        /// Whether design has been modified.
        /// </summary>
        public bool HasChanges { get; private set; }

        /// <summary>
        /// Whether we're waiting for user to confirm discarding changes.
        /// </summary>
        public bool PendingDiscardConfirmation { get; private set; }

        /// <summary>
        /// Custom class name entered by user.
        /// </summary>
        public string CustomClassName { get; private set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback when design is saved.
        /// </summary>
        public Action<TISpaceShipTemplate> OnDesignSaved { get; set; }

        /// <summary>
        /// Callback when designer is cancelled/exited.
        /// </summary>
        public Action OnCancelled { get; set; }

        /// <summary>
        /// Callback for entering text input mode.
        /// </summary>
        public Action<string, Action<string>> OnEnterTextInput { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new ship designer sub-mode.
        /// </summary>
        /// <param name="faction">The player faction</param>
        /// <param name="existingDesign">Existing design to edit, or null for new</param>
        public ShipDesignerSubMode(TIFactionState faction, TISpaceShipTemplate existingDesign = null)
        {
            Faction = faction;
            OriginalDesign = existingDesign;

            // Get available hulls
            AvailableHulls = GetAvailableHulls();

            if (existingDesign != null)
            {
                // Clone the existing design for editing
                WorkingDesign = CloneDesignForEditing(existingDesign);
                CustomClassName = existingDesign.className;
                CurrentStep = DesignerStep.NavigateZones;
                CurrentZone = DesignZone.Overview;
            }
            else
            {
                // New design - start with hull selection
                CurrentStep = DesignerStep.SelectHull;
                CurrentSelectionIndex = 0;
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Move to previous item in current context.
        /// </summary>
        public void Previous()
        {
            // Clear any pending confirmation if user navigates
            PendingDiscardConfirmation = false;

            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    CurrentSelectionIndex--;
                    if (CurrentSelectionIndex < 0)
                        CurrentSelectionIndex = Math.Max(0, AvailableHulls.Count - 1);
                    break;

                case DesignerStep.NavigateZones:
                    PreviousZone();
                    break;

                case DesignerStep.NavigateZoneItems:
                    CurrentZoneItemIndex--;
                    if (CurrentZoneItemIndex < 0)
                        CurrentZoneItemIndex = Math.Max(0, GetCurrentZoneItemCount() - 1);
                    break;

                case DesignerStep.SelectComponent:
                    CurrentSelectionIndex--;
                    if (CurrentSelectionIndex < 0)
                        CurrentSelectionIndex = Math.Max(0, (CurrentComponentOptions?.Count ?? 1) - 1);
                    break;

                case DesignerStep.ConfirmSave:
                    CurrentSelectionIndex = CurrentSelectionIndex == 0 ? 1 : 0;
                    break;
            }
        }

        /// <summary>
        /// Move to next item in current context.
        /// </summary>
        public void Next()
        {
            // Clear any pending confirmation if user navigates
            PendingDiscardConfirmation = false;

            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    CurrentSelectionIndex++;
                    if (CurrentSelectionIndex >= AvailableHulls.Count)
                        CurrentSelectionIndex = 0;
                    break;

                case DesignerStep.NavigateZones:
                    NextZone();
                    break;

                case DesignerStep.NavigateZoneItems:
                    CurrentZoneItemIndex++;
                    if (CurrentZoneItemIndex >= GetCurrentZoneItemCount())
                        CurrentZoneItemIndex = 0;
                    break;

                case DesignerStep.SelectComponent:
                    CurrentSelectionIndex++;
                    if (CurrentSelectionIndex >= (CurrentComponentOptions?.Count ?? 1))
                        CurrentSelectionIndex = 0;
                    break;

                case DesignerStep.ConfirmSave:
                    CurrentSelectionIndex = CurrentSelectionIndex == 0 ? 1 : 0;
                    break;
            }
        }

        /// <summary>
        /// Select/drill into current item.
        /// </summary>
        public void Select()
        {
            // If we're waiting for discard confirmation, confirm it
            if (PendingDiscardConfirmation)
            {
                PendingDiscardConfirmation = false;
                OnSpeak?.Invoke("Design discarded.", true);
                OnCancelled?.Invoke();
                return;
            }

            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    SelectHull();
                    break;

                case DesignerStep.NavigateZones:
                    DrillIntoZone();
                    break;

                case DesignerStep.NavigateZoneItems:
                    SelectZoneItem();
                    break;

                case DesignerStep.SelectComponent:
                    ApplyComponentSelection();
                    break;

                case DesignerStep.ConfirmSave:
                    if (CurrentSelectionIndex == 0) // Yes
                        SaveDesign();
                    else
                        OnCancelled?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Go back to previous step/level.
        /// </summary>
        public void Back()
        {
            // If we're waiting for discard confirmation, cancel it
            if (PendingDiscardConfirmation)
            {
                PendingDiscardConfirmation = false;
                OnSpeak?.Invoke("Discard cancelled. Returning to design.", true);
                return;
            }

            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    OnCancelled?.Invoke();
                    break;

                case DesignerStep.NavigateZones:
                    // Ask for confirmation if changes were made
                    if (HasChanges)
                    {
                        PendingDiscardConfirmation = true;
                        OnSpeak?.Invoke("Design has unsaved changes. Press Enter to discard, or Escape to cancel.", true);
                    }
                    else
                    {
                        OnCancelled?.Invoke();
                    }
                    break;

                case DesignerStep.NavigateZoneItems:
                    CurrentStep = DesignerStep.NavigateZones;
                    CurrentZoneItemIndex = 0;
                    AnnounceCurrentState();
                    break;

                case DesignerStep.SelectComponent:
                    CurrentStep = DesignerStep.NavigateZoneItems;
                    CurrentComponentOptions = null;
                    AnnounceCurrentState();
                    break;

                case DesignerStep.EnterClassName:
                    CurrentStep = DesignerStep.NavigateZones;
                    CurrentZone = DesignZone.Save;
                    AnnounceCurrentState();
                    break;

                case DesignerStep.ConfirmSave:
                    CurrentStep = DesignerStep.NavigateZones;
                    CurrentZone = DesignZone.Save;
                    AnnounceCurrentState();
                    break;
            }
        }

        private void PreviousZone()
        {
            var zones = GetAvailableZones();
            int idx = zones.IndexOf(CurrentZone);
            idx--;
            if (idx < 0) idx = zones.Count - 1;
            CurrentZone = zones[idx];
        }

        private void NextZone()
        {
            var zones = GetAvailableZones();
            int idx = zones.IndexOf(CurrentZone);
            idx++;
            if (idx >= zones.Count) idx = 0;
            CurrentZone = zones[idx];
        }

        private List<DesignZone> GetAvailableZones()
        {
            return new List<DesignZone>
            {
                DesignZone.Overview,
                DesignZone.Nose,
                DesignZone.Hull,
                DesignZone.Lateral,
                DesignZone.Tail,
                DesignZone.Propulsion,
                DesignZone.Save
            };
        }

        #endregion

        #region Hull Selection

        private List<TIShipHullTemplate> GetAvailableHulls()
        {
            if (Faction?.allowedShipHulls == null)
                return new List<TIShipHullTemplate>();

            return Faction.allowedShipHulls
                .Where(h => h != null)
                .OrderBy(h => h.volume_m3)
                .ToList();
        }

        private void SelectHull()
        {
            if (CurrentSelectionIndex < 0 || CurrentSelectionIndex >= AvailableHulls.Count)
                return;

            var hull = AvailableHulls[CurrentSelectionIndex];
            WorkingDesign = CreateNewDesign(hull);
            CustomClassName = $"New {hull.displayName}";
            HasChanges = true;

            CurrentStep = DesignerStep.NavigateZones;
            CurrentZone = DesignZone.Overview;
            AnnounceCurrentState();
        }

        private TISpaceShipTemplate CreateNewDesign(TIShipHullTemplate hull)
        {
            // Create a new design with the selected hull
            string dataName = $"PlayerDesign_{DateTime.Now.Ticks}";
            var design = new TISpaceShipTemplate(dataName)
            {
                hullName = hull.dataName,
                factionName = Faction.templateName
            };

            // Set default role based on hull size
            if (hull.smallHull)
                design.role = ShipRole.SS_Interceptor;
            else if (hull.mediumHull)
                design.role = ShipRole.MM_SpaceSuperiority;
            else
                design.role = ShipRole.LL_Bomber;

            // Initialize with defaults
            design.propellantTanks = 1;

            return design;
        }

        private TISpaceShipTemplate CloneDesignForEditing(TISpaceShipTemplate original)
        {
            // Clone for editing - will create a new design on save
            return original.Clone($"Edit_{original.dataName}_{DateTime.Now.Ticks}", Faction.templateName);
        }

        #endregion

        #region Zone Navigation

        private void DrillIntoZone()
        {
            if (CurrentZone == DesignZone.Overview)
            {
                // Overview is read-only, just announce details
                AnnounceOverviewDetails();
                return;
            }

            if (CurrentZone == DesignZone.Save)
            {
                // Start save process
                StartSaveProcess();
                return;
            }

            // Drill into zone items
            CurrentStep = DesignerStep.NavigateZoneItems;
            CurrentZoneItemIndex = 0;
            AnnounceCurrentState();
        }

        private int GetCurrentZoneItemCount()
        {
            if (WorkingDesign == null)
                return 0;

            var hull = WorkingDesign.hullTemplate;
            if (hull == null)
                return 0;

            switch (CurrentZone)
            {
                case DesignZone.Nose:
                    return hull.noseHardpoints + 1; // +1 for armor

                case DesignZone.Hull:
                    return hull.hullHardpoints + hull.internalModules;

                case DesignZone.Lateral:
                    return 1; // Just armor

                case DesignZone.Tail:
                    return 1; // Just armor

                case DesignZone.Propulsion:
                    return 4; // Drive, Power Plant, Radiator, Propellant

                default:
                    return 0;
            }
        }

        private void SelectZoneItem()
        {
            // Determine what component type we're editing
            switch (CurrentZone)
            {
                case DesignZone.Nose:
                    if (CurrentZoneItemIndex == 0)
                        StartArmorSelection(ShipModuleSlotType.NoseArmor);
                    else
                        StartWeaponSelection(ShipModuleSlotType.NoseHardPoint, CurrentZoneItemIndex - 1);
                    break;

                case DesignZone.Hull:
                    var hull = WorkingDesign?.hullTemplate;
                    if (hull != null && CurrentZoneItemIndex < hull.hullHardpoints)
                        StartWeaponSelection(ShipModuleSlotType.HullHardPoint, CurrentZoneItemIndex);
                    else
                        StartUtilitySelection(CurrentZoneItemIndex - (hull?.hullHardpoints ?? 0));
                    break;

                case DesignZone.Lateral:
                    StartArmorSelection(ShipModuleSlotType.LateralArmor);
                    break;

                case DesignZone.Tail:
                    StartArmorSelection(ShipModuleSlotType.TailArmor);
                    break;

                case DesignZone.Propulsion:
                    switch (CurrentZoneItemIndex)
                    {
                        case 0:
                            StartDriveSelection();
                            break;
                        case 1:
                            StartPowerPlantSelection();
                            break;
                        case 2:
                            StartRadiatorSelection();
                            break;
                        case 3:
                            StartPropellantAdjustment();
                            break;
                    }
                    break;
            }
        }

        #endregion

        #region Component Selection

        private void StartWeaponSelection(ShipModuleSlotType slotType, int slotIndex)
        {
            CurrentSlotType = slotType;
            CurrentSlotIndex = slotIndex;

            // Get available weapons for this slot
            CurrentComponentOptions = GetAvailableWeapons(slotType).Cast<TIShipPartTemplate>().ToList();

            // Add "Empty" option at start
            CurrentComponentOptions.Insert(0, null);

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartUtilitySelection(int slotIndex)
        {
            CurrentSlotType = ShipModuleSlotType.Utility;
            CurrentSlotIndex = slotIndex;

            CurrentComponentOptions = GetAvailableUtilityModules().Cast<TIShipPartTemplate>().ToList();
            CurrentComponentOptions.Insert(0, null); // Empty option

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartArmorSelection(ShipModuleSlotType armorSlot)
        {
            CurrentSlotType = armorSlot;
            CurrentSlotIndex = 0;

            CurrentComponentOptions = GetAvailableArmor().Cast<TIShipPartTemplate>().ToList();

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartDriveSelection()
        {
            CurrentSlotType = ShipModuleSlotType.Drive;
            CurrentSlotIndex = 0;

            CurrentComponentOptions = GetAvailableDrives().Cast<TIShipPartTemplate>().ToList();

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartPowerPlantSelection()
        {
            CurrentSlotType = ShipModuleSlotType.PowerPlant;
            CurrentSlotIndex = 0;

            CurrentComponentOptions = GetAvailablePowerPlants().Cast<TIShipPartTemplate>().ToList();

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartRadiatorSelection()
        {
            CurrentSlotType = ShipModuleSlotType.Radiator;
            CurrentSlotIndex = 0;

            CurrentComponentOptions = GetAvailableRadiators().Cast<TIShipPartTemplate>().ToList();

            CurrentStep = DesignerStep.SelectComponent;
            CurrentSelectionIndex = 0;
            AnnounceCurrentState();
        }

        private void StartPropellantAdjustment()
        {
            // For propellant, we use a simple increment/decrement
            OnSpeak?.Invoke($"Propellant tanks: {WorkingDesign?.propellantTanks ?? 0}. Use + and - to adjust, Enter to confirm.", true);
        }

        /// <summary>
        /// Adjust propellant tanks (called from input handler).
        /// </summary>
        public void AdjustPropellant(int delta)
        {
            if (WorkingDesign == null)
                return;

            int newValue = WorkingDesign.propellantTanks + delta;
            if (newValue < 0) newValue = 0;
            if (newValue > 20) newValue = 20; // Reasonable max

            WorkingDesign.propellantTanks = newValue;
            HasChanges = true;

            OnSpeak?.Invoke($"Propellant tanks: {newValue}", false);
        }

        /// <summary>
        /// Adjust armor points (called from input handler).
        /// </summary>
        public void AdjustArmor(int delta)
        {
            if (WorkingDesign == null || CurrentStep != DesignerStep.SelectComponent)
                return;

            if (CurrentSlotType == ShipModuleSlotType.NoseArmor ||
                CurrentSlotType == ShipModuleSlotType.LateralArmor ||
                CurrentSlotType == ShipModuleSlotType.TailArmor)
            {
                WorkingDesign.TryAddArmorPoints(CurrentSlotType, delta);
                HasChanges = true;

                int currentPoints = GetCurrentArmorPoints(CurrentSlotType);
                OnSpeak?.Invoke($"Armor points: {currentPoints}", false);
            }
        }

        private int GetCurrentArmorPoints(ShipModuleSlotType armorSlot)
        {
            if (WorkingDesign == null)
                return 0;

            switch (armorSlot)
            {
                case ShipModuleSlotType.NoseArmor:
                    return WorkingDesign.noseArmorValue;
                case ShipModuleSlotType.LateralArmor:
                    return WorkingDesign.lateralArmorValue;
                case ShipModuleSlotType.TailArmor:
                    return WorkingDesign.tailArmorValue;
                default:
                    return 0;
            }
        }

        private void ApplyComponentSelection()
        {
            if (WorkingDesign == null)
                return;

            var selected = CurrentSelectionIndex >= 0 && CurrentSelectionIndex < (CurrentComponentOptions?.Count ?? 0)
                ? CurrentComponentOptions[CurrentSelectionIndex]
                : null;

            try
            {
                switch (CurrentSlotType)
                {
                    case ShipModuleSlotType.NoseHardPoint:
                    case ShipModuleSlotType.HullHardPoint:
                        ApplyWeaponToSlot(selected as TIShipWeaponTemplate);
                        break;

                    case ShipModuleSlotType.Utility:
                        ApplyUtilityToSlot(selected as TIUtilityModuleTemplate);
                        break;

                    case ShipModuleSlotType.NoseArmor:
                    case ShipModuleSlotType.LateralArmor:
                    case ShipModuleSlotType.TailArmor:
                        // For armor, apply type then prompt for points
                        ApplyArmorTypeAndPromptForPoints(selected as TIShipArmorTemplate);
                        return; // Don't fall through - the callback handles navigation

                    case ShipModuleSlotType.Drive:
                        ApplyDrive(selected as TIDriveTemplate);
                        break;

                    case ShipModuleSlotType.PowerPlant:
                        ApplyPowerPlant(selected as TIPowerPlantTemplate);
                        break;

                    case ShipModuleSlotType.Radiator:
                        ApplyRadiator(selected as TIRadiatorTemplate);
                        break;
                }

                HasChanges = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying component: {ex.Message}");
                OnSpeak?.Invoke("Failed to apply component", true);
            }

            // Return to zone items
            CurrentStep = DesignerStep.NavigateZoneItems;
            CurrentComponentOptions = null;
            AnnounceCurrentState();
        }

        private void ApplyWeaponToSlot(TIShipWeaponTemplate weapon)
        {
            // This is simplified - actual implementation needs to handle the slot system properly
            // For now, just track that a weapon should be assigned
            OnSpeak?.Invoke(weapon != null ? $"Set {weapon.displayName}" : "Cleared slot", false);
        }

        private void ApplyUtilityToSlot(TIUtilityModuleTemplate module)
        {
            OnSpeak?.Invoke(module != null ? $"Set {module.displayName}" : "Cleared slot", false);
        }

        private void ApplyArmorTypeAndPromptForPoints(TIShipArmorTemplate armor)
        {
            if (armor == null)
            {
                // No armor selected, just return to zone items
                CurrentStep = DesignerStep.NavigateZoneItems;
                CurrentComponentOptions = null;
                AnnounceCurrentState();
                return;
            }

            // Apply the armor type first
            switch (CurrentSlotType)
            {
                case ShipModuleSlotType.NoseArmor:
                    WorkingDesign.SetNoseArmorTemplate(armor.dataName);
                    break;
                case ShipModuleSlotType.LateralArmor:
                    WorkingDesign.SetLateralArmorTemplate(armor.dataName);
                    break;
                case ShipModuleSlotType.TailArmor:
                    WorkingDesign.SetTailArmorTemplate(armor.dataName);
                    break;
            }

            HasChanges = true;
            int currentPoints = GetCurrentArmorPoints(CurrentSlotType);
            var slotType = CurrentSlotType; // Capture for closure

            // Prompt for armor points
            OnEnterTextInput?.Invoke($"Enter armor points (current: {currentPoints})", (input) =>
            {
                if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int newPoints))
                {
                    if (newPoints >= 0)
                    {
                        // Set the armor points by calculating delta from current
                        int currentPts = GetCurrentArmorPoints(slotType);
                        int delta = newPoints - currentPts;
                        if (delta != 0)
                        {
                            WorkingDesign.TryAddArmorPoints(slotType, delta);
                            HasChanges = true;
                        }
                        OnSpeak?.Invoke($"Set {armor.displayName} with {newPoints} points", false);
                    }
                }
                else
                {
                    // No change to points, just confirm the type
                    OnSpeak?.Invoke($"Set armor type: {armor.displayName}, {currentPoints} points", false);
                }

                // Return to zone items
                CurrentStep = DesignerStep.NavigateZoneItems;
                CurrentComponentOptions = null;
                AnnounceCurrentState();
            });
        }

        private void ApplyDrive(TIDriveTemplate drive)
        {
            if (drive == null)
                return;

            WorkingDesign.SetDriveTemplate(drive.dataName);
            OnSpeak?.Invoke($"Set drive: {drive.displayName}", false);
        }

        private void ApplyPowerPlant(TIPowerPlantTemplate powerPlant)
        {
            if (powerPlant == null)
                return;

            WorkingDesign.SetPowerPlantTemplate(powerPlant.dataName);
            OnSpeak?.Invoke($"Set power plant: {powerPlant.displayName}", false);
        }

        private void ApplyRadiator(TIRadiatorTemplate radiator)
        {
            if (radiator == null)
                return;

            WorkingDesign.SetRadiatorTemplate(radiator.dataName);
            OnSpeak?.Invoke($"Set radiator: {radiator.displayName}", false);
        }

        #endregion

        #region Available Components

        private List<TIShipWeaponTemplate> GetAvailableWeapons(ShipModuleSlotType slotType)
        {
            var result = new List<TIShipWeaponTemplate>();

            try
            {
                // Use appropriate weapon list based on slot type
                IEnumerable<TIShipWeaponTemplate> weapons = null;
                if (slotType == ShipModuleSlotType.NoseHardPoint)
                    weapons = Faction?.allowedNoseWeapons;
                else if (slotType == ShipModuleSlotType.HullHardPoint)
                    weapons = Faction?.allowedHullWeapons;

                if (weapons != null)
                {
                    result = weapons
                        .Where(w => w != null)
                        .OrderBy(w => w.displayName)
                        .ToList();
                }
            }
            catch { }

            return result;
        }

        private List<TIUtilityModuleTemplate> GetAvailableUtilityModules()
        {
            if (Faction?.allowedUtilityModules == null)
                return new List<TIUtilityModuleTemplate>();

            return Faction.allowedUtilityModules
                .Where(m => m != null)
                .OrderBy(m => m.displayName)
                .ToList();
        }

        private List<TIShipArmorTemplate> GetAvailableArmor()
        {
            var result = new List<TIShipArmorTemplate>();

            try
            {
                var armors = Faction?.allowedArmors;
                if (armors != null)
                {
                    result = armors
                        .Where(a => a != null)
                        .OrderBy(a => a.displayName)
                        .ToList();
                }
            }
            catch { }

            return result;
        }

        private List<TIDriveTemplate> GetAvailableDrives()
        {
            if (Faction?.allowedDrives == null)
                return new List<TIDriveTemplate>();

            return Faction.allowedDrives
                .Where(d => d != null)
                .OrderBy(d => d.displayName)
                .ToList();
        }

        private List<TIPowerPlantTemplate> GetAvailablePowerPlants()
        {
            if (Faction?.allowedPowerPlants == null)
                return new List<TIPowerPlantTemplate>();

            return Faction.allowedPowerPlants
                .Where(p => p != null)
                .OrderBy(p => p.displayName)
                .ToList();
        }

        private List<TIRadiatorTemplate> GetAvailableRadiators()
        {
            if (Faction?.allowedRadiators == null)
                return new List<TIRadiatorTemplate>();

            return Faction.allowedRadiators
                .Where(r => r != null)
                .OrderBy(r => r.displayName)
                .ToList();
        }

        #endregion

        #region Save Process

        private void StartSaveProcess()
        {
            if (WorkingDesign == null)
            {
                OnSpeak?.Invoke("No design to save", true);
                return;
            }

            // Check if design is valid
            if (!WorkingDesign.ValidTemplate)
            {
                OnSpeak?.Invoke("Design is incomplete. Add a drive and power plant to save.", true);
                return;
            }

            // Ask for class name
            OnEnterTextInput?.Invoke($"Enter class name (current: {CustomClassName})", (name) =>
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    CustomClassName = name;
                }
                CurrentStep = DesignerStep.ConfirmSave;
                CurrentSelectionIndex = 0;
                AnnounceCurrentState();
            });
        }

        private void SaveDesign()
        {
            if (WorkingDesign == null)
                return;

            try
            {
                // Set the class name
                WorkingDesign.SetDisplayName(CustomClassName ?? "Unnamed Class");

                // Save to faction
                Faction.SaveShipDesign(WorkingDesign);

                OnSpeak?.Invoke($"Saved ship class: {CustomClassName}", true);
                OnDesignSaved?.Invoke(WorkingDesign);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error saving design: {ex.Message}");
                OnSpeak?.Invoke("Failed to save design", true);
            }
        }

        /// <summary>
        /// Apply autodesign to the current working design.
        /// Uses the game's AI to fill in components based on hull and role.
        /// </summary>
        public void ApplyAutodesign()
        {
            if (WorkingDesign == null)
            {
                OnSpeak?.Invoke("No design to autodesign", true);
                return;
            }

            if (WorkingDesign.role == ShipRole.NoRole)
            {
                OnSpeak?.Invoke("Select a role first before autodesigning", true);
                return;
            }

            try
            {
                var hull = WorkingDesign.hullTemplate;
                var role = WorkingDesign.role;

                // Use the game's autodesign
                bool allowExotics = Faction.UnlockedExotics && Faction.GetCurrentResourceAmount(FactionResource.Exotics) > 0f;
                bool allowAntimatter = Faction.UnlockedAntimatter && Faction.GetDailyIncome(FactionResource.Antimatter) > 0f;
                float strategicRange = Faction.DesiredStrategicRange_AU();

                var outcome = Faction.DesignShip(
                    playerAutodesign: true,
                    role: role,
                    design: out TISpaceShipTemplate autoDesign,
                    desiredStrategicRange_AU: strategicRange,
                    allowExotics: allowExotics,
                    allowAntimatter: allowAntimatter,
                    forceHull: hull
                );

                if (outcome == TIFactionState.ShipDesignerOutcome.Success && autoDesign != null)
                {
                    // Replace working design with the autodesigned one
                    WorkingDesign = autoDesign;
                    CustomClassName = $"Auto {hull?.displayName ?? "Ship"} {role}";
                    HasChanges = true;

                    OnSpeak?.Invoke($"Autodesign complete. {autoDesign.wetMass_tons:N0} tons, {autoDesign.baseCruiseDeltaV_kps(false):F1} km/s delta-V", true);
                }
                else
                {
                    string reason = GetAutodesignFailureReason(outcome);
                    OnSpeak?.Invoke($"Autodesign failed: {reason}", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error during autodesign: {ex.Message}");
                OnSpeak?.Invoke("Autodesign failed", true);
            }
        }

        private string GetAutodesignFailureReason(TIFactionState.ShipDesignerOutcome outcome)
        {
            switch (outcome)
            {
                case TIFactionState.ShipDesignerOutcome.NoAvailableHulls:
                    return "No hulls available";
                case TIFactionState.ShipDesignerOutcome.NoHullsForRole:
                    return "No suitable hull for this role";
                case TIFactionState.ShipDesignerOutcome.NoDrives:
                    return "No drives available";
                case TIFactionState.ShipDesignerOutcome.NoPowerPlants:
                    return "No power plants available";
                case TIFactionState.ShipDesignerOutcome.NoWeapons:
                    return "No weapons available";
                case TIFactionState.ShipDesignerOutcome.ForcedHullNotAvailable:
                    return "Selected hull not available";
                case TIFactionState.ShipDesignerOutcome.NoCandidateDesigns:
                    return "No viable designs found";
                case TIFactionState.ShipDesignerOutcome.MinimumPropulsionRequirementsNotMet:
                    return "Cannot meet propulsion requirements";
                case TIFactionState.ShipDesignerOutcome.NoScoredDesigns:
                    return "No designs scored well enough";
                case TIFactionState.ShipDesignerOutcome.AntimatterRequired:
                    return "Antimatter technology required";
                case TIFactionState.ShipDesignerOutcome.ExoticsRequired:
                    return "Exotic materials required";
                case TIFactionState.ShipDesignerOutcome.DesignNotAllowedForRole:
                    return "Design not allowed for this role";
                default:
                    return outcome.ToString();
            }
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announce the current state for accessibility.
        /// </summary>
        public void AnnounceCurrentState()
        {
            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    AnnounceHullSelection();
                    break;

                case DesignerStep.NavigateZones:
                    AnnounceZone();
                    break;

                case DesignerStep.NavigateZoneItems:
                    AnnounceZoneItem();
                    break;

                case DesignerStep.SelectComponent:
                    AnnounceComponentSelection();
                    break;

                case DesignerStep.ConfirmSave:
                    OnSpeak?.Invoke($"Save design '{CustomClassName}'? Yes / No", true);
                    break;
            }
        }

        private void AnnounceHullSelection()
        {
            if (AvailableHulls.Count == 0)
            {
                OnSpeak?.Invoke("No hull technologies researched. Research hull technology first.", true);
                return;
            }

            var hull = AvailableHulls[CurrentSelectionIndex];
            string sizeStr = hull.smallHull ? "Small" : (hull.mediumHull ? "Medium" : (hull.largeHull ? "Large" : "Huge"));
            string info = $"{hull.displayName}, {sizeStr}, {hull.noseHardpoints} nose, {hull.hullHardpoints} hull, {hull.internalModules} utility";
            OnSpeak?.Invoke($"Select hull: {info}", true);
        }

        private void AnnounceZone()
        {
            string zoneInfo = GetZoneSummary(CurrentZone);
            OnSpeak?.Invoke($"{CurrentZone}: {zoneInfo}", true);
        }

        private string GetZoneSummary(DesignZone zone)
        {
            if (WorkingDesign == null)
                return "No design";

            switch (zone)
            {
                case DesignZone.Overview:
                    return GetOverviewSummary();

                case DesignZone.Nose:
                    return $"Armor: {WorkingDesign.noseArmorValue} points, {WorkingDesign.hullTemplate?.noseHardpoints ?? 0} hardpoints";

                case DesignZone.Hull:
                    return $"{WorkingDesign.hullTemplate?.hullHardpoints ?? 0} hardpoints, {WorkingDesign.hullTemplate?.internalModules ?? 0} utility slots";

                case DesignZone.Lateral:
                    return $"Armor: {WorkingDesign.lateralArmorValue} points";

                case DesignZone.Tail:
                    return $"Armor: {WorkingDesign.tailArmorValue} points";

                case DesignZone.Propulsion:
                    return GetPropulsionSummary();

                case DesignZone.Save:
                    return HasChanges ? "Save design" : "No changes to save";

                default:
                    return "";
            }
        }

        private string GetOverviewSummary()
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append($"{WorkingDesign.wetMass_tons:N0} tons");
                sb.Append($", {WorkingDesign.baseCruiseDeltaV_kps(false):F1} km/s");
                sb.Append($", Combat: {WorkingDesign.TemplateSpaceCombatValue():N0}");
            }
            catch
            {
                sb.Append("Incomplete design");
            }
            return sb.ToString();
        }

        private string GetPropulsionSummary()
        {
            var parts = new List<string>();
            if (WorkingDesign.driveTemplate != null)
                parts.Add(WorkingDesign.driveTemplate.displayName);
            else
                parts.Add("No drive");

            if (WorkingDesign.powerPlantTemplate != null)
                parts.Add(WorkingDesign.powerPlantTemplate.displayName);
            else
                parts.Add("No power plant");

            parts.Add($"{WorkingDesign.propellantTanks} tanks");

            return string.Join(", ", parts);
        }

        private void AnnounceOverviewDetails()
        {
            if (WorkingDesign == null)
            {
                OnSpeak?.Invoke("No design loaded", true);
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"Design: {CustomClassName}");

            try
            {
                sb.Append($", Hull: {WorkingDesign.hullTemplate?.displayName ?? "Unknown"}");
                sb.Append($", Mass: {WorkingDesign.wetMass_tons:N0} tons");
                sb.Append($", Delta-V: {WorkingDesign.baseCruiseDeltaV_kps(false):F1} km/s");
                sb.Append($", Acceleration: {WorkingDesign.baseCruiseAcceleration_gs(false) * 1000:F1} mg");
                sb.Append($", Combat Value: {WorkingDesign.TemplateSpaceCombatValue():N0}");

                if (!WorkingDesign.ValidTemplate)
                    sb.Append(". INCOMPLETE - needs drive and power plant");
            }
            catch
            {
                sb.Append(". Error reading design stats");
            }

            OnSpeak?.Invoke(sb.ToString(), true);
        }

        private void AnnounceZoneItem()
        {
            string itemInfo = GetZoneItemInfo(CurrentZone, CurrentZoneItemIndex);
            OnSpeak?.Invoke(itemInfo, true);
        }

        private string GetZoneItemInfo(DesignZone zone, int index)
        {
            if (WorkingDesign == null)
                return "No design";

            var hull = WorkingDesign.hullTemplate;

            switch (zone)
            {
                case DesignZone.Nose:
                    if (index == 0)
                        return $"Nose Armor: {WorkingDesign.noseArmorTemplate?.displayName ?? "None"}, {WorkingDesign.noseArmorValue} points";
                    else
                        return $"Nose Hardpoint {index}: {GetWeaponInSlot(ShipModuleSlotType.NoseHardPoint, index - 1)}";

                case DesignZone.Hull:
                    if (index < (hull?.hullHardpoints ?? 0))
                        return $"Hull Hardpoint {index + 1}: {GetWeaponInSlot(ShipModuleSlotType.HullHardPoint, index)}";
                    else
                        return $"Utility Slot {index - (hull?.hullHardpoints ?? 0) + 1}: {GetUtilityInSlot(index - (hull?.hullHardpoints ?? 0))}";

                case DesignZone.Lateral:
                    return $"Lateral Armor: {WorkingDesign.lateralArmorTemplate?.displayName ?? "None"}, {WorkingDesign.lateralArmorValue} points";

                case DesignZone.Tail:
                    return $"Tail Armor: {WorkingDesign.tailArmorTemplate?.displayName ?? "None"}, {WorkingDesign.tailArmorValue} points";

                case DesignZone.Propulsion:
                    switch (index)
                    {
                        case 0:
                            return $"Drive: {WorkingDesign.driveTemplate?.displayName ?? "None"}";
                        case 1:
                            return $"Power Plant: {WorkingDesign.powerPlantTemplate?.displayName ?? "None"}";
                        case 2:
                            return $"Radiator: {WorkingDesign.radiatorTemplate?.displayName ?? "None"}";
                        case 3:
                            return $"Propellant Tanks: {WorkingDesign.propellantTanks}";
                        default:
                            return "Unknown";
                    }

                default:
                    return "Unknown";
            }
        }

        private string GetWeaponInSlot(ShipModuleSlotType slotType, int index)
        {
            // Simplified - would need to actually check the weapon entries
            return "Empty";
        }

        private string GetUtilityInSlot(int index)
        {
            // Simplified - would need to actually check the module entries
            return "Empty";
        }

        private void AnnounceComponentSelection()
        {
            if (CurrentComponentOptions == null || CurrentComponentOptions.Count == 0)
            {
                OnSpeak?.Invoke("No components available", true);
                return;
            }

            var component = CurrentSelectionIndex >= 0 && CurrentSelectionIndex < CurrentComponentOptions.Count
                ? CurrentComponentOptions[CurrentSelectionIndex]
                : null;

            if (component == null)
            {
                OnSpeak?.Invoke("Empty (clear slot)", true);
            }
            else
            {
                string info = GetComponentInfo(component);
                OnSpeak?.Invoke(info, true);
            }
        }

        private string GetComponentInfo(TIShipPartTemplate component)
        {
            if (component == null)
                return "Empty";

            var sb = new StringBuilder();
            sb.Append(component.displayName);

            if (component is TIShipWeaponTemplate weapon)
            {
                sb.Append($", Range: {weapon.targetingRange_km:N0} km");
                sb.Append($", Cooldown: {weapon.cooldown_s:F1}s");
                if (weapon.defenseMode && weapon.attackMode)
                    sb.Append(", Attack+Defense");
                else if (weapon.defenseMode)
                    sb.Append(", Defense only");
            }
            else if (component is TIDriveTemplate drive)
            {
                sb.Append($", Thrust: {drive.thrust_N / 1000000f:F1} MN");
                sb.Append($", EV: {drive.EV_kps:F1} km/s");
                if (drive.selfPowered)
                    sb.Append(", Self-powered");
            }
            else if (component is TIPowerPlantTemplate pp)
            {
                sb.Append($", Efficiency: {pp.efficiency * 100:F0}%");
                sb.Append($", {pp.specificPower_tGW:F1} t/GW");
            }
            else if (component is TIRadiatorTemplate rad)
            {
                sb.Append($", Vulnerability: {rad.vulnerability * 100:F0}%");
            }
            else if (component is TIShipArmorTemplate armor)
            {
                sb.Append($", {armor.single_armor_point_mass_tons:F2}t per point");
            }
            else if (component is TIUtilityModuleTemplate utility)
            {
                // Show the module's primary function
                if (utility.specialModuleRules.Count > 0)
                {
                    var rule = utility.specialModuleRules[0];
                    sb.Append($", {rule}");
                    if (utility.specialModuleValue != 0 && utility.specialModuleValue != 1)
                        sb.Append($" ({utility.specialModuleValue:F1})");
                }
                if (utility.crew > 0)
                    sb.Append($", Crew: {utility.crew}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Read current item in the context (for Numpad * detail).
        /// </summary>
        public string ReadCurrentDetail()
        {
            switch (CurrentStep)
            {
                case DesignerStep.SelectHull:
                    if (CurrentSelectionIndex >= 0 && CurrentSelectionIndex < AvailableHulls.Count)
                    {
                        var hull = AvailableHulls[CurrentSelectionIndex];
                        return $"Hull: {hull.displayName}. " +
                               $"Size: {(hull.smallHull ? "Small" : hull.mediumHull ? "Medium" : hull.largeHull ? "Large" : "Huge")}. " +
                               $"Nose hardpoints: {hull.noseHardpoints}. Hull hardpoints: {hull.hullHardpoints}. " +
                               $"Utility slots: {hull.internalModules}. Length: {hull.length_m:N0}m. Width: {hull.width_m:N0}m.";
                    }
                    return "No hull selected";

                case DesignerStep.NavigateZones:
                case DesignerStep.NavigateZoneItems:
                    return GetDetailedZoneInfo();

                case DesignerStep.SelectComponent:
                    var component = CurrentSelectionIndex >= 0 && CurrentSelectionIndex < (CurrentComponentOptions?.Count ?? 0)
                        ? CurrentComponentOptions[CurrentSelectionIndex]
                        : null;
                    if (component == null)
                        return "Empty slot option - removes any installed component.";
                    return GetDetailedComponentInfo(component);

                default:
                    return "No details available";
            }
        }

        private string GetDetailedZoneInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Zone: {CurrentZone}");

            if (WorkingDesign == null)
            {
                sb.AppendLine("No design loaded");
                return sb.ToString();
            }

            switch (CurrentZone)
            {
                case DesignZone.Overview:
                    sb.AppendLine($"Class: {CustomClassName}");
                    sb.AppendLine($"Hull: {WorkingDesign.hullTemplate?.displayName ?? "Unknown"}");
                    try
                    {
                        sb.AppendLine($"Wet Mass: {WorkingDesign.wetMass_tons:N0} tons");
                        sb.AppendLine($"Dry Mass: {WorkingDesign.dryMass_tons():N0} tons");
                        sb.AppendLine($"Delta-V: {WorkingDesign.baseCruiseDeltaV_kps(false):F1} km/s");
                        sb.AppendLine($"Acceleration: {WorkingDesign.baseCruiseAcceleration_gs(false) * 1000:F1} mg");
                        sb.AppendLine($"Combat Value: {WorkingDesign.TemplateSpaceCombatValue():N0}");
                    }
                    catch { sb.AppendLine("Stats unavailable - incomplete design"); }
                    break;

                case DesignZone.Propulsion:
                    sb.AppendLine($"Drive: {WorkingDesign.driveTemplate?.displayName ?? "None"}");
                    sb.AppendLine($"Power Plant: {WorkingDesign.powerPlantTemplate?.displayName ?? "None"}");
                    sb.AppendLine($"Radiator: {WorkingDesign.radiatorTemplate?.displayName ?? "None"}");
                    sb.AppendLine($"Propellant Tanks: {WorkingDesign.propellantTanks}");
                    break;
            }

            return sb.ToString();
        }

        private string GetDetailedComponentInfo(TIShipPartTemplate component)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Component: {component.displayName}");

            if (component is TIShipWeaponTemplate weapon)
            {
                sb.AppendLine($"Type: {weapon.weaponClass} Weapon");
                sb.AppendLine($"Mount: {(weapon.noseWeapon ? "Nose" : "Hull")}, {weapon.internalSize} slot(s)");
                sb.AppendLine($"Targeting Range: {weapon.targetingRange_km:N0} km");
                sb.AppendLine($"Cooldown: {weapon.cooldown_s:F1} seconds");
                if (weapon.salvo_shots > 1)
                    sb.AppendLine($"Salvo: {weapon.salvo_shots} shots, {weapon.intraSalvoCooldown_s:F1}s between");
                sb.AppendLine($"Mass: {weapon.baseWeaponMass_tons:N0} tons");
                // Fire modes
                var modes = new List<string>();
                if (weapon.attackMode) modes.Add("Attack");
                if (weapon.defenseMode) modes.Add("Defense");
                if (weapon.guardianMode) modes.Add("Guardian");
                sb.AppendLine($"Modes: {string.Join(", ", modes)}");
                if (weapon.bombardmentValue > 0)
                    sb.AppendLine($"Bombardment: Yes{(weapon.canBombardThroughAtmosphere ? "" : " (no atmosphere)")}");
            }
            else if (component is TIDriveTemplate drive)
            {
                sb.AppendLine($"Type: {drive.driveClassification} Drive");
                sb.AppendLine($"Thrusters: {drive.thrusters}");
                sb.AppendLine($"Thrust: {drive.thrust_N / 1000000f:F2} MN");
                sb.AppendLine($"Exhaust Velocity: {drive.EV_kps:F1} km/s");
                sb.AppendLine($"Efficiency: {drive.efficiency * 100:F0}%");
                if (drive.selfPowered)
                    sb.AppendLine("Self-powered (no power plant needed)");
                else
                    sb.AppendLine($"Power Requirement: {drive.powerRequirement_GW:F2} GW");
                sb.AppendLine($"Propellant: {drive.propellant}");
                if (drive.freeISRU)
                    sb.AppendLine("ISRU capable (free refueling)");
            }
            else if (component is TIPowerPlantTemplate pp)
            {
                sb.AppendLine($"Type: Power Plant");
                sb.AppendLine($"Efficiency: {pp.efficiency * 100:F0}%");
                sb.AppendLine($"Specific Power: {pp.specificPower_tGW:F2} t/GW");
            }
            else if (component is TIRadiatorTemplate rad)
            {
                sb.AppendLine($"Type: Radiator");
                sb.AppendLine($"Vulnerability: {rad.vulnerability * 100:F0}%");
            }
            else if (component is TIShipArmorTemplate armor)
            {
                sb.AppendLine($"Type: Armor");
                sb.AppendLine($"Mass per point: {armor.single_armor_point_mass_tons:F2} tons");
                sb.AppendLine($"Thickness per point: {armor.plate_thickness_m * 100:F2} cm");
                sb.AppendLine($"Density: {armor.density_kgm3:N0} kg/m³");
                // Show specialties (resistances/vulnerabilities)
                foreach (var specialty in armor.specialties)
                {
                    if (specialty.armorSpecialty != ArmorSpecialty.None && specialty.value != 1f)
                    {
                        string effectType = specialty.value < 1f ? "Resistance" : "Vulnerability";
                        float effectValue = Math.Abs(1f - specialty.value) * 100f;
                        sb.AppendLine($"{specialty.armorSpecialty}: {effectValue:F0}% {effectType}");
                    }
                }
            }
            else if (component is TIUtilityModuleTemplate utility)
            {
                sb.AppendLine($"Type: Utility Module");
                if (utility.specialModuleRules.Count > 0)
                {
                    sb.AppendLine($"Function: {string.Join(", ", utility.specialModuleRules)}");
                    if (utility.specialModuleValue != 0 && utility.specialModuleValue != 1)
                        sb.AppendLine($"Value: {utility.specialModuleValue:F1}");
                }
                if (utility.crew > 0)
                    sb.AppendLine($"Crew: {utility.crew}");
                if (utility.powerRequirement_MW > 0)
                    sb.AppendLine($"Power: {utility.powerRequirement_MW:F1} MW");
            }

            return sb.ToString();
        }

        #endregion
    }
}
