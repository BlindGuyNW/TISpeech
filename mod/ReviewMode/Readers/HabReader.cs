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
                        string powerStr = GetModuleTemplatePowerString(moduleTemplate);
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
                var buildOptions = new List<BuildModuleData>();

                int displayNum = TISectorState.sectorDisplayNum(sector.sectorNum, hab.habType);
                string slotLabel = GetSlotLabel(hab, sector, slotIndex);

                // Option 1: Build from Earth (uses boost, has transit time)
                // Game's GetString with includeCompletionTime will format like "10 Boost 50 Money 30 days"
                string earthCostStr = FormatModuleCostWithTime(earthCost, faction);
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
                buildOptions.Add(new BuildModuleData { Cost = earthCost, CanAfford = canAffordEarth, Source = "Earth" });

                // Option 2: Build from Space (uses space resources, faster)
                string spaceCostStr = FormatModuleCostWithTime(spaceCost, faction);
                string spaceLabel;
                string spaceDetail;

                if (usingBoostSubstitution)
                {
                    spaceLabel = canAffordSpaceWithBoost
                        ? $"From Space (boost substituted): {spaceCostStr}"
                        : $"From Space: {spaceCostStr} (Cannot afford)";
                    spaceDetail = canAffordSpaceWithBoost
                        ? "Using boost to substitute for missing space resources"
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
                buildOptions.Add(new BuildModuleData { Cost = spaceCost, CanAfford = canAffordSpace, Source = "Space" });

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
                        if (data.CanAfford)
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
        /// Data class for build module confirmation.
        /// </summary>
        private class BuildModuleData
        {
            public TIResourcesCost Cost { get; set; }
            public bool CanAfford { get; set; }
            public string Source { get; set; }
        }

        /// <summary>
        /// Format module cost using the game's built-in formatting, with completion time.
        /// </summary>
        private string FormatModuleCostWithTime(TIResourcesCost cost, TIFactionState faction)
        {
            // Use the game's GetString method which properly formats all resources with sprites
            // Parameters: format, includeCostStr, includeCompletionTime, completionTimeOnly, relevantCap, costsOnly, gainsOnly, faction
            string gameFormatted = cost.GetString("Relevant", includeCostStr: false, includeCompletionTime: true,
                completionTimeOnly: false, relevantCap: 7, costsOnly: false, gainsOnly: false, faction: faction);

            // Clean the text to convert sprites to readable labels
            return TISpeechMod.CleanText(gameFormatted).Trim();
        }

        /// <summary>
        /// Format module cost using the game's built-in formatting, without completion time.
        /// </summary>
        private string FormatModuleCostOnly(TIResourcesCost cost, TIFactionState faction)
        {
            // Use the game's ToString method which formats resources with sprites
            string gameFormatted = cost.ToString("Relevant", gainsOnly: false, costsOnly: false, faction: faction);

            // Clean the text to convert sprites to readable labels
            return TISpeechMod.CleanText(gameFormatted).Trim();
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
                    costStr = FormatModuleCostOnly(spaceCost, faction);
                    source = "Space";
                }
                else if (canAffordEarth)
                {
                    costStr = FormatModuleCostOnly(earthCost, faction);
                    source = "Earth";
                }
                else
                {
                    // Can't afford either - show cheapest
                    if (spaceBoost < earthBoost || (spaceBoost == 0 && earthBoost > 0))
                    {
                        costStr = FormatModuleCostOnly(spaceCost, faction) + " (unaffordable)";
                        source = "Space";
                    }
                    else
                    {
                        costStr = FormatModuleCostOnly(earthCost, faction) + " (unaffordable)";
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
        /// </summary>
        private string GetModuleTemplatePowerString(TIHabModuleTemplate template)
        {
            try
            {
                int power = template.power;
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
                // Power
                int power = template.power;
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
                    sb.AppendLine($"Cost: {FormatModuleCostWithTime(cost, faction)}");
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
