using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for TIHabState objects (bases and stations).
    /// Extracts and formats hab information for accessibility.
    /// </summary>
    public class HabReader : IGameStateReader<TIHabState>
    {
        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback to refresh sections after an action.
        /// </summary>
        public Action OnRefreshSections { get; set; }

        /// <summary>
        /// Create a HabReader with all callbacks properly wired up.
        /// Use this factory method to ensure consistent behavior across all screens.
        /// </summary>
        /// <param name="onEnterSelectionMode">Callback for selection dialogs</param>
        /// <param name="onSpeak">Callback for speech output</param>
        /// <param name="onRefreshSections">Callback when sections need refresh (e.g., after an action)</param>
        /// <returns>A fully configured HabReader instance</returns>
        public static HabReader CreateConfigured(
            Action<string, List<SelectionOption>, Action<int>> onEnterSelectionMode,
            Action<string, bool> onSpeak,
            Action onRefreshSections)
        {
            return new HabReader
            {
                OnEnterSelectionMode = onEnterSelectionMode,
                OnSpeak = onSpeak,
                OnRefreshSections = onRefreshSections
            };
        }
        public string ReadSummary(TIHabState hab)
        {
            if (hab == null)
                return "Unknown hab";

            var sb = new StringBuilder();
            sb.Append(hab.displayName ?? "Unknown");

            // Type and tier
            string habTypeStr = hab.IsBase ? "Base" : "Station";
            sb.Append($", Tier {hab.tier} {habTypeStr}");

            // Location
            string location = GetLocationDescription(hab);
            if (!string.IsNullOrEmpty(location))
            {
                sb.Append($" at {location}");
            }

            // Owner (if not player)
            var faction = hab.coreFaction;
            if (faction != null)
            {
                sb.Append($", {faction.displayName}");
            }

            // Module count
            int moduleCount = hab.AllModules()?.Count ?? 0;
            sb.Append($", {moduleCount} module{(moduleCount != 1 ? "s" : "")}");

            // Power status
            try
            {
                int netPower = hab.NetPower(includeUnderConstruction: false, includeDeactivated: false);
                if (netPower >= 0)
                    sb.Append($", +{netPower} MW");
                else
                    sb.Append($", {netPower} MW");
            }
            catch { }

            return sb.ToString();
        }

        public string ReadDetail(TIHabState hab)
        {
            if (hab == null)
                return "Unknown hab";

            var sb = new StringBuilder();
            sb.AppendLine($"Hab: {hab.displayName}");
            sb.AppendLine();

            // Basic info
            string habTypeStr = hab.IsBase ? "Surface Base" : "Orbital Station";
            sb.AppendLine($"Type: {habTypeStr}, Tier {hab.tier}");

            // Location
            sb.AppendLine($"Location: {GetLocationDescription(hab)}");

            // Owner
            var faction = hab.coreFaction;
            if (faction != null)
            {
                sb.AppendLine($"Owner: {faction.displayName}");
            }

            // Status
            sb.AppendLine();
            sb.AppendLine("Status:");
            if (!hab.anyCoreCompleted)
                sb.AppendLine("  Core under construction");
            else
                sb.AppendLine("  Operational");

            if (hab.underBombardment)
                sb.AppendLine("  UNDER BOMBARDMENT");

            // Power
            try
            {
                int netPower = hab.NetPower(includeUnderConstruction: false, includeDeactivated: false);
                sb.AppendLine($"  Power: {(netPower >= 0 ? "+" : "")}{netPower} MW");
            }
            catch { }

            // Modules
            sb.AppendLine();
            var allModules = hab.AllModules();
            int moduleCount = allModules?.Count ?? 0;
            int activeCount = hab.ActiveModules()?.Count ?? 0;
            sb.AppendLine($"Modules: {moduleCount} total, {activeCount} active");

            // Sectors
            int activeSectors = hab.numActiveSectors;
            sb.AppendLine($"Sectors: {activeSectors} active");

            // Crew
            try
            {
                int crew = allModules?.Where(m => m.functional).Sum(m => m.crew) ?? 0;
                if (crew > 0)
                    sb.AppendLine($"Crew: {crew}");
            }
            catch { }

            // Councilors
            if (hab.councilorsOnBoard != null && hab.councilorsOnBoard.Count > 0)
            {
                sb.AppendLine($"Councilors: {hab.councilorsOnBoard.Count}");
            }

            // Docked fleets
            if (hab.dockedFleets != null && hab.dockedFleets.Count > 0)
            {
                sb.AppendLine($"Docked Fleets: {hab.dockedFleets.Count}");
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TIHabState hab)
        {
            var sections = new List<ISection>();
            if (hab == null)
                return sections;

            // Overview section
            sections.Add(CreateOverviewSection(hab));

            // Sectors section (organized by sector)
            sections.Add(CreateSectorsSection(hab));

            // Add Module action (if player owns this hab)
            // Note: Don't check anyCoreCompleted - game allows building in non-core slots before core is done
            var faction = GameControl.control?.activePlayer;
            if (faction != null && hab.coreFaction == faction)
            {
                var addModuleSection = CreateAddModuleSection(hab);
                if (addModuleSection != null)
                    sections.Add(addModuleSection);

                // Module Actions section (power toggle, decommission, upgrade, etc.)
                var moduleActionsSection = CreateModuleActionsSection(hab);
                if (moduleActionsSection != null)
                    sections.Add(moduleActionsSection);
            }

            // Resources section
            sections.Add(CreateResourcesSection(hab));

            // Crew section (if has councilors or officers)
            if ((hab.councilorsOnBoard != null && hab.councilorsOnBoard.Count > 0) ||
                (hab.officersOnBoard != null && hab.officersOnBoard.Count > 0))
            {
                sections.Add(CreateCrewSection(hab));
            }

            // Docked fleets section
            if (hab.dockedFleets != null && hab.dockedFleets.Count > 0)
            {
                sections.Add(CreateDockedFleetsSection(hab));
            }

            // Construction section (modules under construction)
            var underConstruction = hab.AllModules()?.Where(m => m.underConstruction).ToList();
            if (underConstruction != null && underConstruction.Count > 0)
            {
                sections.Add(CreateConstructionSection(hab, underConstruction));
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateOverviewSection(TIHabState hab)
        {
            var section = new DataSection("Overview");

            // Type
            string habTypeStr = hab.IsBase ? "Surface Base" : "Orbital Station";
            section.AddItem("Type", habTypeStr);

            // Tier
            section.AddItem("Tier", hab.tier.ToString());

            // Location
            section.AddItem("Location", GetLocationDescription(hab));

            // Owner
            var faction = hab.coreFaction;
            if (faction != null)
            {
                section.AddItem("Owner", faction.displayName);
            }

            // Status
            if (!hab.anyCoreCompleted)
                section.AddItem("Status", "Core under construction");
            else if (hab.underBombardment)
                section.AddItem("Status", "UNDER BOMBARDMENT");
            else
                section.AddItem("Status", "Operational");

            // Power
            try
            {
                int netPower = hab.NetPower(includeUnderConstruction: false, includeDeactivated: false);
                string powerStr = netPower >= 0 ? $"+{netPower} MW (surplus)" : $"{netPower} MW (deficit)";
                section.AddItem("Power", powerStr);
            }
            catch
            {
                section.AddItem("Power", "Unknown");
            }

            // Module counts
            var allModules = hab.AllModules();
            int moduleCount = allModules?.Count ?? 0;
            int activeCount = hab.ActiveModules()?.Count ?? 0;
            section.AddItem("Modules", $"{activeCount} active of {moduleCount} total");

            // Sectors
            section.AddItem("Sectors", $"{hab.numActiveSectors} active");

            return section;
        }

        private ISection CreateSectorsSection(TIHabState hab)
        {
            var section = new DataSection("Sectors");

            if (hab.sectors == null || hab.sectors.Count == 0)
            {
                section.AddItem("No sectors");
                return section;
            }

            // Iterate through active sectors
            foreach (var sector in hab.sectors.Where(s => s.active))
            {
                int displayNum = TISectorState.sectorDisplayNum(sector.sectorNum, hab.habType);
                string sectorHeader = sector.coreSector ? $"Sector {displayNum} (Core)" : $"Sector {displayNum}";

                // Sector summary
                int moduleCount = sector.AllModules().Count;
                int functionalCount = sector.FunctionalModules().Count;
                string sectorSummary = $"{functionalCount}/{moduleCount} modules functional";

                // Power info
                int sectorPower = sector.SectorNetPowerValue(includeUnderConstruction: false, includeDeactivated: false);
                if (sectorPower != 0)
                {
                    sectorSummary += $", {(sectorPower > 0 ? "+" : "")}{sectorPower} MW";
                }

                section.AddItem(sectorHeader, sectorSummary);

                // List modules in this sector
                for (int slot = 0; slot < sector.habModules.Count; slot++)
                {
                    var module = sector.habModules[slot];
                    string slotLabel = GetSlotLabel(hab, sector, slot);

                    if (module.empty)
                    {
                        // Empty slot
                        if (IsSlotBuildable(hab, sector, slot))
                        {
                            section.AddItem($"  {slotLabel}", "Empty (buildable)");
                        }
                        // Don't show non-buildable empty slots to reduce clutter
                    }
                    else if (module.destroyed)
                    {
                        // Destroyed module
                        string priorName = module.priorModuleTemplate?.displayName ?? "Unknown";
                        section.AddItem($"  {slotLabel}", $"{priorName} - DESTROYED");
                    }
                    else
                    {
                        // Active module
                        string moduleName = module.displayName ?? "Unknown Module";
                        string status = GetModuleStatus(module);
                        string power = GetModulePowerString(module);
                        string value = !string.IsNullOrEmpty(power) ? $"{status}, {power}" : status;
                        string detail = GetModuleDetailText(module);

                        section.AddItem($"  {slotLabel}: {moduleName}", value, detail);
                    }
                }
            }

            // Show inactive sectors summary
            var inactiveSectors = hab.sectors.Where(s => !s.active).ToList();
            if (inactiveSectors.Count > 0)
            {
                section.AddItem("Inactive Sectors", $"{inactiveSectors.Count} (unlock by upgrading hab tier)");
            }

            return section;
        }

        /// <summary>
        /// Get a label for a module slot based on hab type and position.
        /// </summary>
        private string GetSlotLabel(TIHabState hab, TISectorState sector, int slot)
        {
            // Special slots
            if (sector.coreSector && slot == 0)
                return "Core";

            if (hab.IsBase && sector.coreSector && slot == 1)
                return "Mine";

            return $"Slot {slot + 1}";
        }

        /// <summary>
        /// Check if a slot can have modules built in it.
        /// </summary>
        private bool IsSlotBuildable(TIHabState hab, TISectorState sector, int slot)
        {
            // Core slot is only for core modules
            if (sector.coreSector && slot == 0)
                return false;

            // Mine slot on bases requires special handling
            if (hab.IsBase && sector.coreSector && slot == 1)
                return true; // Mine slot is buildable

            // Non-core sectors: slot 0 is typically not buildable, outer slots are
            if (!sector.coreSector)
            {
                // Outer sectors have different slot rules
                return slot > 0;
            }

            return true;
        }

        private ISection CreateAddModuleSection(TIHabState hab)
        {
            var section = new DataSection("Build Module");
            var faction = hab.coreFaction;

            if (faction == null)
                return null;

            try
            {
                // Get available modules
                var allowedModules = hab.AllowedModules(faction);
                if (allowedModules == null || allowedModules.Count == 0)
                {
                    section.AddItem("No modules available", "Research more tech to unlock modules");
                    return section;
                }

                // Find empty buildable slots
                var emptySlots = GetEmptyBuildableSlots(hab);
                if (emptySlots.Count == 0)
                {
                    section.AddItem("No empty slots", "All slots are occupied or hab needs upgrade");
                    return section;
                }

                // Group modules by category for easier navigation
                var modulesByCategory = allowedModules
                    .Where(m => !m.coreModule) // Exclude core modules
                    .GroupBy(m => GetModuleCategory(m))
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var category in modulesByCategory)
                {
                    foreach (var moduleTemplate in category.OrderBy(m => m.tier).ThenBy(m => m.displayName))
                    {
                        // Check if this module can be placed anywhere
                        bool canPlace = emptySlots.Any(slot =>
                            slot.sector.ValidModuleForSlot(moduleTemplate, slot.slotIndex));

                        if (!canPlace)
                            continue;

                        var template = moduleTemplate; // Capture for closure
                        var habCopy = hab;

                        string tierStr = moduleTemplate.tier > 0 ? $"T{moduleTemplate.tier} " : "";
                        string powerStr = GetModuleTemplatePowerString(moduleTemplate, hab);
                        string label = $"{tierStr}{moduleTemplate.displayName}";

                        // Show brief cost summary (cheapest option)
                        string costSummary = GetModuleCostSummary(moduleTemplate, hab, faction);
                        string value = !string.IsNullOrEmpty(powerStr)
                            ? $"{powerStr}, {costSummary}"
                            : costSummary;

                        string detail = GetModuleTemplateDetailText(moduleTemplate, hab);

                        section.AddItem(label, value, detail, onActivate: () => StartBuildModule(habCopy, template));
                    }
                }

                if (section.ItemCount == 0)
                {
                    section.AddItem("No compatible modules", "Available modules don't fit empty slots");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating add module section: {ex.Message}");
                section.AddItem("Error loading modules");
            }

            return section;
        }

        /// <summary>
        /// Get empty slots that can have modules built in them.
        /// </summary>
        private List<(TISectorState sector, int slotIndex)> GetEmptyBuildableSlots(TIHabState hab)
        {
            var result = new List<(TISectorState, int)>();

            foreach (var sector in hab.sectors.Where(s => s.active && s.faction == hab.coreFaction))
            {
                for (int slot = 0; slot < sector.habModules.Count; slot++)
                {
                    var module = sector.habModules[slot];
                    if (module.empty || module.destroyed)
                    {
                        // Check if this is a valid slot for building
                        // Core slot (0,0) is only for core modules
                        if (sector.coreSector && slot == 0)
                            continue;

                        result.Add((sector, slot));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get a category name for a module template.
        /// </summary>
        private string GetModuleCategory(TIHabModuleTemplate template)
        {
            if (template.coreModule) return "Core";
            if (template.mine) return "Mining";
            if (template.allowsShipConstruction) return "Shipyard";
            if (template.allowsResupply) return "Resupply";
            if (template.spaceCombatModule) return "Defense";
            if (template.power > 0) return "Power";

            // Check for resource production
            if (HasResourceIncome(template))
                return "Production";

            return "Utility";
        }

        /// <summary>
        /// Check if a module template produces any resources.
        /// </summary>
        private bool HasResourceIncome(TIHabModuleTemplate template)
        {
            return template.incomeMoney_month != 0 ||
                   template.incomeResearch_month != 0 ||
                   template.incomeWater_month != 0 ||
                   template.incomeVolatiles_month != 0 ||
                   template.incomeMetals_month != 0 ||
                   template.incomeNobles_month != 0 ||
                   template.incomeFissiles_month != 0 ||
                   template.missionControl != 0 ||
                   template.incomeProjects != 0;
        }

        /// <summary>
        /// Start the build module flow - select slot, then confirm.
        /// </summary>
        private void StartBuildModule(TIHabState hab, TIHabModuleTemplate moduleTemplate)
        {
            if (OnEnterSelectionMode == null)
            {
                OnSpeak?.Invoke("Cannot build module - selection mode unavailable", true);
                return;
            }

            var faction = hab.coreFaction;
            if (faction == null)
                return;

            try
            {
                // Find valid slots for this module
                var validSlots = new List<(TISectorState sector, int slotIndex)>();

                foreach (var sector in hab.sectors.Where(s => s.active && s.faction == faction))
                {
                    for (int slot = 0; slot < sector.habModules.Count; slot++)
                    {
                        var module = sector.habModules[slot];
                        if ((module.empty || module.destroyed) && sector.ValidModuleForSlot(moduleTemplate, slot))
                        {
                            validSlots.Add((sector, slot));
                        }
                    }
                }

                if (validSlots.Count == 0)
                {
                    OnSpeak?.Invoke("No valid slots for this module", true);
                    return;
                }

                // If only one valid slot, go straight to confirmation
                if (validSlots.Count == 1)
                {
                    ConfirmBuildModule(hab, moduleTemplate, validSlots[0].sector, validSlots[0].slotIndex);
                    return;
                }

                // Build slot selection options
                var options = new List<SelectionOption>();

                foreach (var (sector, slotIndex) in validSlots)
                {
                    int displayNum = TISectorState.sectorDisplayNum(sector.sectorNum, hab.habType);
                    string slotLabel = GetSlotLabel(hab, sector, slotIndex);
                    string label = $"Sector {displayNum}, {slotLabel}";

                    options.Add(new SelectionOption
                    {
                        Label = label,
                        DetailText = sector.coreSector ? "Core sector" : $"Outer sector {displayNum}",
                        Data = (sector, slotIndex)
                    });
                }

                options.Add(new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Cancel module construction",
                    Data = null
                });

                OnEnterSelectionMode($"Select slot for {moduleTemplate.displayName}", options, (index) =>
                {
                    if (index >= 0 && index < validSlots.Count)
                    {
                        var selected = validSlots[index];
                        ConfirmBuildModule(hab, moduleTemplate, selected.sector, selected.slotIndex);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Module construction cancelled", true);
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting module build: {ex.Message}");
                OnSpeak?.Invoke("Error starting module construction", true);
            }
        }

        /// <summary>
        /// Confirm and execute module construction - shows Earth and Space cost options.
        /// </summary>
        private void ConfirmBuildModule(TIHabState hab, TIHabModuleTemplate moduleTemplate, TISectorState sector, int slotIndex)
        {
            if (OnEnterSelectionMode == null)
                return;

            var faction = hab.coreFaction;
            if (faction == null)
                return;

            try
            {
                // Calculate both Earth and Space costs
                var earthCost = moduleTemplate.CostFromEarth(faction, hab, isUpgrade: false);
                var spaceCostPure = moduleTemplate.CostFromSpace(faction, hab, isUpgrade: false, substituteBoost: false);
                var spaceCostWithBoost = moduleTemplate.CostFromSpace(faction, hab, isUpgrade: false, substituteBoost: true);

                bool canAffordEarth = earthCost.CanAfford(faction);
                bool canAffordSpacePure = spaceCostPure.CanAfford(faction);
                bool canAffordSpaceWithBoost = spaceCostWithBoost.CanAfford(faction);

                // Determine which space cost to show - pure if affordable, otherwise with boost substitution
                var spaceCost = canAffordSpacePure ? spaceCostPure : spaceCostWithBoost;
                bool canAffordSpace = canAffordSpacePure || canAffordSpaceWithBoost;
                bool usingBoostSubstitution = !canAffordSpacePure && canAffordSpaceWithBoost;

                var options = new List<SelectionOption>();
                var buildOptions = new List<CostOptionData>();

                int displayNum = TISectorState.sectorDisplayNum(sector.sectorNum, hab.habType);
                string slotLabel = GetSlotLabel(hab, sector, slotIndex);

                // First, show the base space resource cost (like the game's tooltip does)
                // This shows what space resources would be needed without boost substitution
                string baseSpaceCostStr = CostFormatter.FormatCostOnly(spaceCostPure, faction);
                options.Add(new SelectionOption
                {
                    Label = $"Base space cost: {baseSpaceCostStr}",
                    DetailText = canAffordSpacePure
                        ? "You have these resources - can build from space"
                        : "You don't have these resources - will need boost substitution or build from Earth",
                    Data = "info"
                });
                // This is informational only, not a build option
                buildOptions.Add(new CostOptionData { Cost = null, CanAfford = false, Source = "Info" });

                // Option 1: Build from Earth (uses boost, has transit time)
                // Game's GetString with includeCompletionTime will format like "10 Boost 50 Money 30 days"
                string earthCostStr = CostFormatter.FormatWithTime(earthCost, faction);
                string earthLabel = canAffordEarth
                    ? $"From Earth: {earthCostStr}"
                    : $"From Earth: {earthCostStr} (Cannot afford)";

                options.Add(new SelectionOption
                {
                    Label = earthLabel,
                    DetailText = canAffordEarth
                        ? "Ship module from Earth using boost"
                        : "Insufficient resources",
                    Data = "earth"
                });
                buildOptions.Add(new CostOptionData { Cost = earthCost, CanAfford = canAffordEarth, Source = "Earth" });

                // Option 2: Build from Space
                string spaceCostStr = CostFormatter.FormatWithTime(spaceCost, faction);
                string spaceLabel;
                string spaceDetail;

                if (usingBoostSubstitution)
                {
                    // Show what the boost-substituted cost is
                    spaceLabel = canAffordSpaceWithBoost
                        ? $"From Space (boost substituted): {spaceCostStr}"
                        : $"From Space: {spaceCostStr} (Cannot afford)";
                    spaceDetail = canAffordSpaceWithBoost
                        ? $"Using boost to substitute for missing space resources"
                        : "Insufficient space resources and boost";
                }
                else
                {
                    spaceLabel = canAffordSpacePure
                        ? $"From Space: {spaceCostStr}"
                        : $"From Space: {spaceCostStr} (Cannot afford)";
                    spaceDetail = canAffordSpacePure
                        ? "Build using local space resources"
                        : "Insufficient space resources";
                }

                options.Add(new SelectionOption
                {
                    Label = spaceLabel,
                    DetailText = spaceDetail,
                    Data = "space"
                });
                buildOptions.Add(new CostOptionData { Cost = spaceCost, CanAfford = canAffordSpace, Source = "Space" });

                // Option 3: Cancel
                options.Add(new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Cancel module construction",
                    Data = "cancel"
                });

                OnEnterSelectionMode($"Build {moduleTemplate.displayName} at Sector {displayNum}, {slotLabel}?", options, (index) =>
                {
                    if (index >= 0 && index < buildOptions.Count)
                    {
                        var data = buildOptions[index];
                        if (data.Source == "Info")
                        {
                            // Informational item - just speak it, don't build
                            OnSpeak?.Invoke($"Base space resources needed: {baseSpaceCostStr}. Select From Earth or From Space to build.", true);
                        }
                        else if (data.CanAfford)
                        {
                            ExecuteBuildModule(hab, moduleTemplate, sector, slotIndex, data.Cost);
                        }
                        else
                        {
                            OnSpeak?.Invoke($"Cannot afford to build from {data.Source}", true);
                        }
                    }
                    else
                    {
                        OnSpeak?.Invoke("Module construction cancelled", true);
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error confirming module build: {ex.Message}");
                OnSpeak?.Invoke("Error confirming construction", true);
            }
        }

        /// <summary>
        /// Execute the module construction.
        /// </summary>
        private void ExecuteBuildModule(TIHabState hab, TIHabModuleTemplate moduleTemplate, TISectorState sector, int slotIndex, TIResourcesCost cost)
        {
            var faction = hab.coreFaction;
            if (faction == null)
                return;

            try
            {
                // Execute construction via game action
                // Constructor: (TIHabModuleTemplate moduleTemplate, TISectorState sector, int slot, TIResourcesCost cost, Action callback)
                var action = new BuildHabModuleAction(moduleTemplate, sector, slotIndex, cost, null);
                faction.playerControl.StartAction(action);

                int days = (int)cost.completionTime_days;
                OnSpeak?.Invoke($"Construction started: {moduleTemplate.displayName}. Completion in {days} days.", true);

                // Play sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ConfirmAlt");
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing module build: {ex.Message}");
                OnSpeak?.Invoke("Error building module", true);
            }
        }

        /// <summary>
        /// Get a brief cost summary for module listing (shows cheapest affordable option).
        /// </summary>
        private string GetModuleCostSummary(TIHabModuleTemplate template, TIHabState hab, TIFactionState faction)
        {
            try
            {
                var earthCost = template.CostFromEarth(faction, hab, isUpgrade: false);
                var spaceCost = template.CostFromSpace(faction, hab, isUpgrade: false, substituteBoost: true);

                bool canAffordEarth = earthCost.CanAfford(faction);
                bool canAffordSpace = spaceCost.CanAfford(faction);

                // Show the cheapest affordable option, or cheapest overall if can't afford either
                float earthBoost = earthCost.GetSingleCostValue(FactionResource.Boost);
                float spaceBoost = spaceCost.GetSingleCostValue(FactionResource.Boost);

                string costStr;
                string source;

                if (canAffordSpace && (!canAffordEarth || spaceBoost <= earthBoost))
                {
                    costStr = CostFormatter.FormatCostOnly(spaceCost, faction);
                    source = "Space";
                }
                else if (canAffordEarth)
                {
                    costStr = CostFormatter.FormatCostOnly(earthCost, faction);
                    source = "Earth";
                }
                else
                {
                    // Can't afford either - show cheapest
                    if (spaceBoost < earthBoost || (spaceBoost == 0 && earthBoost > 0))
                    {
                        costStr = CostFormatter.FormatCostOnly(spaceCost, faction) + " (unaffordable)";
                        source = "Space";
                    }
                    else
                    {
                        costStr = CostFormatter.FormatCostOnly(earthCost, faction) + " (unaffordable)";
                        source = "Earth";
                    }
                }

                return $"{costStr} ({source})";
            }
            catch
            {
                return "Cost unknown";
            }
        }

        private ISection CreateResourcesSection(TIHabState hab)
        {
            var section = new DataSection("Resources");

            var faction = hab.coreFaction;
            if (faction == null)
            {
                section.AddItem("No resource data");
                return section;
            }

            // Space resources
            AddResourceItem(section, hab, faction, FactionResource.Water, "Water");
            AddResourceItem(section, hab, faction, FactionResource.Volatiles, "Volatiles");
            AddResourceItem(section, hab, faction, FactionResource.Metals, "Metals");
            AddResourceItem(section, hab, faction, FactionResource.NobleMetals, "Noble Metals");
            AddResourceItem(section, hab, faction, FactionResource.Fissiles, "Fissiles");

            // Other resources
            AddResourceItem(section, hab, faction, FactionResource.Money, "Money");
            AddResourceItem(section, hab, faction, FactionResource.Research, "Research");
            AddResourceItem(section, hab, faction, FactionResource.Projects, "Projects");
            AddResourceItem(section, hab, faction, FactionResource.MissionControl, "Mission Control");

            return section;
        }

        private void AddResourceItem(DataSection section, TIHabState hab, TIFactionState faction, FactionResource resource, string label)
        {
            try
            {
                float income = hab.GetNetCurrentMonthlyIncome(faction, resource, includeInactivesIncomeAndSupport: false);
                if (Math.Abs(income) > 0.01f)
                {
                    string sign = income >= 0 ? "+" : "";
                    section.AddItem(label, $"{sign}{income:F1}/month");
                }
            }
            catch { }
        }

        private ISection CreateCrewSection(TIHabState hab)
        {
            var section = new DataSection("Crew");

            // Total crew from modules
            try
            {
                var allModules = hab.AllModules();
                int crew = allModules?.Where(m => m.functional).Sum(m => m.crew) ?? 0;
                section.AddItem("Total Crew", crew.ToString());
            }
            catch
            {
                section.AddItem("Total Crew", "Unknown");
            }

            // Councilors
            if (hab.councilorsOnBoard != null && hab.councilorsOnBoard.Count > 0)
            {
                foreach (var councilor in hab.councilorsOnBoard)
                {
                    section.AddItem("Councilor", councilor.displayName ?? "Unknown");
                }
            }

            // Officers
            if (hab.officersOnBoard != null && hab.officersOnBoard.Count > 0)
            {
                foreach (var officer in hab.officersOnBoard)
                {
                    section.AddItem("Officer", officer.displayName ?? "Unknown");
                }
            }

            return section;
        }

        private ISection CreateDockedFleetsSection(TIHabState hab)
        {
            var section = new DataSection("Docked Fleets");

            if (hab.dockedFleets == null || hab.dockedFleets.Count == 0)
            {
                section.AddItem("No docked fleets");
                return section;
            }

            foreach (var fleet in hab.dockedFleets)
            {
                string name = fleet.displayName ?? "Unknown Fleet";
                int shipCount = fleet.ships?.Count ?? 0;
                section.AddItem(name, $"{shipCount} ship{(shipCount != 1 ? "s" : "")}");
            }

            return section;
        }

        private ISection CreateConstructionSection(TIHabState hab, List<TIHabModuleState> underConstruction)
        {
            var section = new DataSection("Under Construction");

            foreach (var module in underConstruction.OrderBy(m => m.completionDate))
            {
                string name = module.displayName ?? "Unknown Module";
                string eta = GetConstructionETA(module);
                string detail = GetModuleDetailText(module);
                section.AddItem(name, eta, detail);
            }

            return section;
        }

        /// <summary>
        /// Create section for module actions (power toggle, decommission, upgrade, etc.)
        /// </summary>
        private ISection CreateModuleActionsSection(TIHabState hab)
        {
            var section = new DataSection("Module Actions");
            var faction = hab.coreFaction;
            if (faction == null)
                return null;

            // Power All action (if any modules are unpowered and could be powered)
            var unpoweredModules = hab.AllModules()?.Where(m => m.functional && !m.powered && m.CanPower()).ToList();
            if (unpoweredModules != null && unpoweredModules.Count > 0)
            {
                var habCopy = hab;
                section.AddItem(
                    "Power All Modules",
                    $"{unpoweredModules.Count} module{(unpoweredModules.Count != 1 ? "s" : "")} can be powered on",
                    "Powers on all modules that can receive power",
                    onActivate: () => ExecutePowerAll(habCopy));
            }

            // List modules with available actions
            foreach (var sector in hab.sectors.Where(s => s.active && s.faction == faction))
            {
                for (int slot = 0; slot < sector.habModules.Count; slot++)
                {
                    var module = sector.habModules[slot];
                    if (module.empty)
                        continue;

                    int displayNum = TISectorState.sectorDisplayNum(sector.sectorNum, hab.habType);
                    string slotLabel = GetSlotLabel(hab, sector, slot);
                    string moduleLocation = $"Sector {displayNum}, {slotLabel}";

                    // Add actions based on module state
                    AddModuleActions(section, hab, module, moduleLocation);
                }
            }

            // Only return section if it has items
            return section.ItemCount > 0 ? section : null;
        }

        /// <summary>
        /// Add available actions for a specific module.
        /// </summary>
        private void AddModuleActions(DataSection section, TIHabState hab, TIHabModuleState module, string location)
        {
            var faction = hab.coreFaction;
            if (faction == null)
                return;

            string moduleName = module.displayName ?? "Unknown";

            // Handle destroyed modules - can rebuild
            if (module.destroyed && module.priorModuleTemplate != null)
            {
                var priorTemplate = module.priorModuleTemplate;
                if (hab.AllowedModules(faction).Contains(priorTemplate))
                {
                    var moduleCopy = module;
                    var habCopy = hab;
                    var templateCopy = priorTemplate;

                    string rebuildCost = GetModuleCostSummary(priorTemplate, hab, faction);
                    section.AddItem(
                        $"Rebuild {priorTemplate.displayName}",
                        $"{location}, {rebuildCost}",
                        $"Rebuild the destroyed {priorTemplate.displayName} module",
                        onActivate: () => StartRebuildModule(habCopy, moduleCopy, templateCopy));
                }
                return; // Destroyed modules have no other actions
            }

            // Handle modules being decommissioned - can cancel
            if (module.decommissioning)
            {
                var moduleCopy = module;
                section.AddItem(
                    $"Cancel Decommission: {moduleName}",
                    $"{location}",
                    "Cancel the decommissioning process and keep the module",
                    onActivate: () => ExecuteCancelDecommission(moduleCopy));
                return; // Decommissioning modules have no other actions
            }

            // Handle modules under construction - can cancel
            if (module.underConstruction)
            {
                bool canImmediateCancel = module.DecommissionDuration_days() <= 0f;
                if (module.CanDecommissionModule(canImmediateCancel))
                {
                    var moduleCopy = module;
                    string cancelLabel = canImmediateCancel ? "Cancel Construction" : "Decommission (Under Construction)";
                    string cancelDesc = canImmediateCancel
                        ? "Cancel construction and get partial refund"
                        : $"Decommission module, takes {(int)module.DecommissionDuration_days()} days";
                    section.AddItem(
                        $"{cancelLabel}: {moduleName}",
                        $"{location}",
                        cancelDesc,
                        onActivate: () => ExecuteDecommissionModule(moduleCopy));
                }
                return; // Under construction modules have no other actions (besides cancel)
            }

            // For functional modules, we can have multiple actions
            var moduleTemplate = module.moduleTemplate;
            if (moduleTemplate == null)
                return;

            // Power Toggle - if module can be turned off/on
            if (moduleTemplate.CanTurnOff && module.functional)
            {
                var moduleCopy = module;
                if (module.powered && module.CanDepower())
                {
                    int powerValue = module.ModulePower();
                    string powerEffect = module.PowerProvider()
                        ? $"Will remove {powerValue} MW generation"
                        : $"Will free up {-powerValue} MW";
                    section.AddItem(
                        $"Power Off: {moduleName}",
                        $"{location}, {powerEffect}",
                        "Turn off this module to save power or disable its effects",
                        onActivate: () => ExecuteTogglePower(moduleCopy, false));
                }
                else if (!module.powered && module.CanPower())
                {
                    int powerValue = module.ModulePower();
                    string powerEffect = module.PowerProvider()
                        ? $"Will add {powerValue} MW generation"
                        : $"Will consume {-powerValue} MW";
                    section.AddItem(
                        $"Power On: {moduleName}",
                        $"{location}, {powerEffect}",
                        "Turn on this module to enable its effects",
                        onActivate: () => ExecuteTogglePower(moduleCopy, true));
                }
            }

            // Upgrade - if module has an upgrade path
            if (module.CanUpgrade(faction))
            {
                var upgradeTemplate = moduleTemplate.UpgradeModuleTemplate(faction, checkUnlocked: true);
                if (upgradeTemplate != null && hab.IsModuleAllowedForThisHab(faction, upgradeTemplate))
                {
                    var moduleCopy = module;
                    var habCopy = hab;
                    var upgradeCopy = upgradeTemplate;

                    string upgradeCost = GetUpgradeCostSummary(upgradeTemplate, hab, faction);
                    section.AddItem(
                        $"Upgrade to {upgradeTemplate.displayName}",
                        $"{location}: {moduleName}, {upgradeCost}",
                        $"Upgrade {moduleName} to {upgradeTemplate.displayName}",
                        onActivate: () => StartUpgradeModule(habCopy, moduleCopy, upgradeCopy));
                }
            }

            // Decommission - if module can be decommissioned
            if (module.CanDecommissionModule(immediateCancel: false))
            {
                var moduleCopy = module;
                var cost = module.DecommissionModuleCost();
                float days = module.DecommissionDuration_days();
                string costStr = days > 0 ? $"{(int)days} days" : "Instant";
                if (cost.anyDebit)
                {
                    costStr += $", {CostFormatter.FormatCostOnly(cost, faction)}";
                }
                section.AddItem(
                    $"Decommission: {moduleName}",
                    $"{location}, {costStr}",
                    "Remove this module from the hab",
                    onActivate: () => ConfirmDecommissionModule(moduleCopy));
            }
        }

        #region Module Action Executors

        /// <summary>
        /// Execute the Power All action for a hab.
        /// </summary>
        private void ExecutePowerAll(TIHabState hab)
        {
            try
            {
                var faction = hab.coreFaction;
                if (faction == null)
                    return;

                // Use the game's action
                var action = new UpdateHabPowerAllAction(hab);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke("Powering on all modules", true);

                // Play sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_PowerModule");
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing power all: {ex.Message}");
                OnSpeak?.Invoke("Error powering modules", true);
            }
        }

        /// <summary>
        /// Execute toggle power for a single module.
        /// </summary>
        private void ExecuteTogglePower(TIHabModuleState module, bool powerOn)
        {
            try
            {
                var faction = module.sector?.faction;
                if (faction == null)
                    return;

                var action = new UpdateHabModulePowerStatus(module, powerOn, null);
                faction.playerControl.StartAction(action);

                string actionText = powerOn ? "Powered on" : "Powered off";
                OnSpeak?.Invoke($"{actionText}: {module.displayName}", true);

                // Play sound
                try
                {
                    string sound = powerOn
                        ? "event:/SFX/UI_SFX/trig_SFX_PowerModule"
                        : "event:/SFX/UI_SFX/trig_SFX_DepowerModule";
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot(sound);
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling power: {ex.Message}");
                OnSpeak?.Invoke("Error toggling power", true);
            }
        }

        /// <summary>
        /// Cancel decommissioning of a module.
        /// </summary>
        private void ExecuteCancelDecommission(TIHabModuleState module)
        {
            try
            {
                module.CancelDecommissionModule();
                OnSpeak?.Invoke($"Cancelled decommission: {module.displayName}", true);

                // Play sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_GenericConfirm");
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cancelling decommission: {ex.Message}");
                OnSpeak?.Invoke("Error cancelling decommission", true);
            }
        }

        /// <summary>
        /// Execute decommission (used for both completed modules and canceling construction).
        /// </summary>
        private void ExecuteDecommissionModule(TIHabModuleState module)
        {
            try
            {
                var faction = module.sector?.faction;
                if (faction == null)
                    return;

                var cost = module.DecommissionModuleCost();
                if (!cost.CanAfford(faction))
                {
                    OnSpeak?.Invoke("Cannot afford to decommission", true);
                    return;
                }

                var action = new DecommissionHabModuleAction(module);
                faction.playerControl.StartAction(action);

                float days = module.DecommissionDuration_days();
                string message = days > 0
                    ? $"Decommissioning {module.displayName}, {(int)days} days"
                    : $"Cancelled construction of {module.displayName}";
                OnSpeak?.Invoke(message, true);

                // Play sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_GenericConfirm");
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error decommissioning module: {ex.Message}");
                OnSpeak?.Invoke("Error decommissioning module", true);
            }
        }

        /// <summary>
        /// Confirm decommission with cost dialog.
        /// </summary>
        private void ConfirmDecommissionModule(TIHabModuleState module)
        {
            if (OnEnterSelectionMode == null)
            {
                // No selection mode available, just execute directly
                ExecuteDecommissionModule(module);
                return;
            }

            var faction = module.sector?.faction;
            if (faction == null)
                return;

            var cost = module.DecommissionModuleCost();
            float days = module.DecommissionDuration_days();

            var options = new List<SelectionOption>();

            string costStr = days > 0
                ? $"Cost: {CostFormatter.FormatCostOnly(cost, faction)}, Time: {(int)days} days"
                : "Instant - will get partial refund";

            bool canAfford = cost.CanAfford(faction);

            options.Add(new SelectionOption
            {
                Label = canAfford ? "Confirm Decommission" : "Cannot Afford",
                DetailText = costStr,
                Data = canAfford ? "confirm" : "cannot"
            });

            options.Add(new SelectionOption
            {
                Label = "Cancel",
                DetailText = "Keep the module",
                Data = "cancel"
            });

            OnEnterSelectionMode($"Decommission {module.displayName}?", options, (index) =>
            {
                if (index == 0 && canAfford)
                {
                    ExecuteDecommissionModule(module);
                }
                else
                {
                    OnSpeak?.Invoke("Decommission cancelled", true);
                }
            });
        }

        /// <summary>
        /// Start the rebuild flow for a destroyed module.
        /// </summary>
        private void StartRebuildModule(TIHabState hab, TIHabModuleState module, TIHabModuleTemplate priorTemplate)
        {
            // Use the same flow as building a new module
            ConfirmBuildModule(hab, priorTemplate, module.sector, module.slot);
        }

        /// <summary>
        /// Start the upgrade flow for a module.
        /// </summary>
        private void StartUpgradeModule(TIHabState hab, TIHabModuleState module, TIHabModuleTemplate upgradeTemplate)
        {
            if (OnEnterSelectionMode == null)
            {
                OnSpeak?.Invoke("Cannot upgrade - selection mode unavailable", true);
                return;
            }

            var faction = hab.coreFaction;
            if (faction == null)
                return;

            try
            {
                // Calculate upgrade costs (from space is used for upgrades since module is already there)
                var spaceCostPure = upgradeTemplate.CostFromSpace(faction, hab, isUpgrade: true, substituteBoost: false);
                var spaceCostWithBoost = upgradeTemplate.CostFromSpace(faction, hab, isUpgrade: true, substituteBoost: true);

                bool canAffordSpacePure = spaceCostPure.CanAfford(faction);
                bool canAffordSpaceWithBoost = spaceCostWithBoost.CanAfford(faction);

                var spaceCost = canAffordSpacePure ? spaceCostPure : spaceCostWithBoost;
                bool canAffordSpace = canAffordSpacePure || canAffordSpaceWithBoost;
                bool usingBoostSubstitution = !canAffordSpacePure && canAffordSpaceWithBoost;

                var options = new List<SelectionOption>();
                var buildOptions = new List<CostOptionData>();

                int displayNum = TISectorState.sectorDisplayNum(module.sector.sectorNum, hab.habType);
                string slotLabel = GetSlotLabel(hab, module.sector, module.slot);

                // Show upgrade cost
                string spaceCostStr = CostFormatter.FormatWithTime(spaceCost, faction);
                string spaceLabel;
                string spaceDetail;

                if (usingBoostSubstitution)
                {
                    spaceLabel = canAffordSpaceWithBoost
                        ? $"Upgrade (boost substituted): {spaceCostStr}"
                        : $"Upgrade: {spaceCostStr} (Cannot afford)";
                    spaceDetail = canAffordSpaceWithBoost
                        ? "Using boost to substitute for missing space resources"
                        : "Insufficient space resources and boost";
                }
                else
                {
                    spaceLabel = canAffordSpacePure
                        ? $"Upgrade: {spaceCostStr}"
                        : $"Upgrade: {spaceCostStr} (Cannot afford)";
                    spaceDetail = canAffordSpacePure
                        ? "Upgrade using local space resources"
                        : "Insufficient space resources";
                }

                options.Add(new SelectionOption
                {
                    Label = spaceLabel,
                    DetailText = spaceDetail,
                    Data = "upgrade"
                });
                buildOptions.Add(new CostOptionData { Cost = spaceCost, CanAfford = canAffordSpace, Source = "Space" });

                options.Add(new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Cancel module upgrade",
                    Data = "cancel"
                });

                OnEnterSelectionMode($"Upgrade {module.displayName} to {upgradeTemplate.displayName}?", options, (index) =>
                {
                    if (index == 0 && canAffordSpace)
                    {
                        ExecuteUpgradeModule(hab, module, upgradeTemplate, spaceCost);
                    }
                    else if (index == 0 && !canAffordSpace)
                    {
                        OnSpeak?.Invoke("Cannot afford upgrade", true);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Upgrade cancelled", true);
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting upgrade: {ex.Message}");
                OnSpeak?.Invoke("Error starting upgrade", true);
            }
        }

        /// <summary>
        /// Execute the module upgrade.
        /// </summary>
        private void ExecuteUpgradeModule(TIHabState hab, TIHabModuleState module, TIHabModuleTemplate upgradeTemplate, TIResourcesCost cost)
        {
            var faction = hab.coreFaction;
            if (faction == null)
                return;

            try
            {
                // Use BuildHabModuleAction - the game uses this for upgrades too
                var action = new BuildHabModuleAction(upgradeTemplate, module.sector, module.slot, cost, null);
                faction.playerControl.StartAction(action);

                int days = (int)cost.completionTime_days;
                OnSpeak?.Invoke($"Upgrade started: {module.displayName} to {upgradeTemplate.displayName}. Completion in {days} days.", true);

                // Play sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ConfirmAlt");
                }
                catch { }

                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing upgrade: {ex.Message}");
                OnSpeak?.Invoke("Error upgrading module", true);
            }
        }

        /// <summary>
        /// Get a brief cost summary for module upgrade.
        /// </summary>
        private string GetUpgradeCostSummary(TIHabModuleTemplate upgradeTemplate, TIHabState hab, TIFactionState faction)
        {
            try
            {
                var spaceCost = upgradeTemplate.CostFromSpace(faction, hab, isUpgrade: true, substituteBoost: true);
                bool canAfford = spaceCost.CanAfford(faction);
                string costStr = CostFormatter.FormatCostOnly(spaceCost, faction);
                return canAfford ? costStr : $"{costStr} (unaffordable)";
            }
            catch
            {
                return "Cost unknown";
            }
        }

        #endregion

        #endregion

        #region Helper Methods

        private string GetLocationDescription(TIHabState hab)
        {
            try
            {
                if (hab.IsBase)
                {
                    // Surface base
                    var site = hab.habSite;
                    if (site != null)
                    {
                        var body = site.parentBody;
                        if (body != null)
                            return $"{site.displayName}, {body.displayName}";
                        return site.displayName;
                    }
                }
                else
                {
                    // Orbital station
                    var orbit = hab.orbitState;
                    if (orbit != null)
                    {
                        return orbit.displayName ?? "Unknown orbit";
                    }
                }
            }
            catch { }

            return "Unknown location";
        }

        private string GetModuleStatus(TIHabModuleState module)
        {
            if (module.destroyed)
                return "Destroyed";
            if (module.decommissioning)
                return "Decommissioning";
            if (module.underConstruction)
                return "Building";
            if (module.active)
                return "Active";
            if (module.functional)
                return "Unpowered";
            return "Inactive";
        }

        private string GetModulePowerString(TIHabModuleState module)
        {
            try
            {
                if (!module.functional && !module.underConstruction)
                    return "";

                int power = module.ModulePower();
                if (power > 0)
                    return $"+{power} MW";
                else if (power < 0)
                    return $"{power} MW";
            }
            catch { }
            return "";
        }

        private string GetModuleDetailText(TIHabModuleState module)
        {
            try
            {
                // Use the game's built-in summary method if available
                string summary = TIHabModuleState.FullSummary(module, includeExtended: true);
                if (!string.IsNullOrEmpty(summary))
                {
                    // Clean the text of any rich text tags
                    return TISpeechMod.CleanText(summary);
                }
            }
            catch { }

            // Fallback: build our own description
            var sb = new StringBuilder();
            sb.AppendLine(module.displayName ?? "Unknown Module");

            string status = GetModuleStatus(module);
            sb.AppendLine($"Status: {status}");

            try
            {
                int power = module.ModulePower();
                if (power != 0)
                    sb.AppendLine($"Power: {(power > 0 ? "+" : "")}{power} MW");

                if (module.crew > 0)
                    sb.AppendLine($"Crew: {module.crew}");

                if (module.underConstruction)
                {
                    sb.AppendLine($"Completion: {GetConstructionETA(module)}");
                }
            }
            catch { }

            return sb.ToString();
        }

        private string GetConstructionETA(TIHabModuleState module)
        {
            try
            {
                var now = GameStateManager.Time().Time_SystemNow();
                var completion = module.completionDate;
                var remaining = completion - now;

                if (remaining.TotalDays <= 0)
                    return "Complete";
                else if (remaining.TotalDays < 1)
                    return "Less than 1 day";
                else if (remaining.TotalDays < 30)
                    return $"{(int)remaining.TotalDays} days";
                else
                    return $"{(int)(remaining.TotalDays / 30)} months";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get power string for a module template.
        /// Uses ProspectivePower when hab is provided to account for solar multiplier.
        /// </summary>
        private string GetModuleTemplatePowerString(TIHabModuleTemplate template, TIHabState hab = null)
        {
            try
            {
                // Use ProspectivePower when we have hab context - this accounts for solar multiplier
                int power = hab != null ? template.ProspectivePower(hab) : template.power;
                if (power > 0)
                    return $"+{power} MW";
                else if (power < 0)
                    return $"{power} MW";
            }
            catch { }
            return "";
        }

        /// <summary>
        /// Get detailed text for a module template.
        /// </summary>
        private string GetModuleTemplateDetailText(TIHabModuleTemplate template, TIHabState hab)
        {
            var sb = new StringBuilder();
            sb.AppendLine(template.displayName);
            sb.AppendLine($"Tier: {template.tier}");

            try
            {
                // Power - use ProspectivePower to account for solar multiplier at this location
                int power = hab != null ? template.ProspectivePower(hab) : template.power;
                if (power != 0)
                    sb.AppendLine($"Power: {(power > 0 ? "+" : "")}{power} MW");

                // Crew
                if (template.crew > 0)
                    sb.AppendLine($"Crew: {template.crew}");

                // Special capabilities
                if (template.allowsShipConstruction)
                    sb.AppendLine("Allows ship construction (shipyard)");
                if (template.allowsResupply)
                    sb.AppendLine("Allows fleet resupply");
                if (template.mine)
                    sb.AppendLine("Mining module");
                if (template.spaceCombatModule)
                    sb.AppendLine("Combat/defense module");

                // Resource production - use individual income fields
                AppendIncomeIfNonZero(sb, "Money", template.incomeMoney_month);
                AppendIncomeIfNonZero(sb, "Research", template.incomeResearch_month);
                AppendIncomeIfNonZero(sb, "Water", template.incomeWater_month);
                AppendIncomeIfNonZero(sb, "Volatiles", template.incomeVolatiles_month);
                AppendIncomeIfNonZero(sb, "Metals", template.incomeMetals_month);
                AppendIncomeIfNonZero(sb, "Noble Metals", template.incomeNobles_month);
                AppendIncomeIfNonZero(sb, "Fissiles", template.incomeFissiles_month);
                if (template.missionControl > 0)
                    sb.AppendLine($"Mission Control: +{template.missionControl}");
                if (template.incomeProjects > 0)
                    sb.AppendLine($"Projects: +{template.incomeProjects}");

                // Cost - use game's built-in formatting with completion time
                var faction = hab?.coreFaction ?? GameControl.control?.activePlayer;
                if (faction != null && hab != null)
                {
                    var cost = template.MinimumBoostCostToday(faction, hab);
                    sb.AppendLine($"Cost: {CostFormatter.FormatWithTime(cost, faction)}");
                }
            }
            catch { }

            return sb.ToString();
        }

        /// <summary>
        /// Helper to append income line if value is non-zero.
        /// </summary>
        private void AppendIncomeIfNonZero(StringBuilder sb, string label, float value)
        {
            if (Math.Abs(value) > 0.001f)
            {
                string sign = value > 0 ? "+" : "";
                sb.AppendLine($"{label}: {sign}{value:F1}/month");
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all habs belonging to the player's faction.
        /// Matches the game's HabitatsScreenController.SetHabModelData() logic.
        /// </summary>
        public static List<TIHabState> GetPlayerHabs(TIFactionState faction)
        {
            if (faction == null)
                return new List<TIHabState>();

            try
            {
                return GameStateManager.IterateByClass<TIHabState>()
                    .Where(h => !h.deleted && h.coreFaction == faction)
                    .OrderBy(h => h.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player habs: {ex.Message}");
                return new List<TIHabState>();
            }
        }

        /// <summary>
        /// Get all known habs (based on intel).
        /// Uses HasIntelOnSpaceAssetLocation like the game's UI.
        /// </summary>
        public static List<TIHabState> GetAllKnownHabs(TIFactionState viewer)
        {
            if (viewer == null)
                return new List<TIHabState>();

            try
            {
                return GameStateManager.IterateByClass<TIHabState>()
                    .Where(h => !h.deleted && viewer.HasIntelOnSpaceAssetLocation(h))
                    .OrderBy(h => h.coreFaction?.displayName ?? "")
                    .ThenBy(h => h.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting known habs: {ex.Message}");
                return new List<TIHabState>();
            }
        }

        /// <summary>
        /// Get a hab by its ID.
        /// </summary>
        public static TIHabState GetHabById(int id)
        {
            try
            {
                return GameStateManager.FindGameState<TIHabState>(id, allowChild: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting hab by ID: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
