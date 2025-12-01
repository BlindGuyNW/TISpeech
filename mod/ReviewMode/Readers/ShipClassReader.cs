using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for TISpaceShipTemplate objects (ship classes/designs).
    /// Extracts and formats ship design information for accessibility.
    /// </summary>
    public class ShipClassReader : IGameStateReader<TISpaceShipTemplate>
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
        /// Callback for entering ship designer mode.
        /// Parameter is the design to edit (null for new design).
        /// </summary>
        public Action<TISpaceShipTemplate> OnEnterShipDesignerMode { get; set; }

        public string ReadSummary(TISpaceShipTemplate design)
        {
            if (design == null)
                return "Unknown ship class";

            var sb = new StringBuilder();
            sb.Append(design.className ?? "Unknown");

            // Hull type
            var hull = design.hullTemplate;
            if (hull != null)
            {
                string sizeStr = hull.smallHull ? "Small" : (hull.mediumHull ? "Medium" : (hull.largeHull ? "Large" : "Huge"));
                sb.Append($", {sizeStr} {hull.displayName}");
            }

            // Role
            try
            {
                sb.Append($", {design.roleStr}");
            }
            catch { }

            // Mass
            try
            {
                float mass = design.wetMass_tons;
                sb.Append($", {mass:N0} tons");
            }
            catch { }

            // Ships in service
            var faction = GameControl.control?.activePlayer;
            if (faction != null)
            {
                try
                {
                    int inService = faction.ships?.Count(s => s.templateName == design.dataName) ?? 0;
                    if (inService > 0)
                        sb.Append($", {inService} in service");
                }
                catch { }
            }

            return sb.ToString();
        }

        public string ReadDetail(TISpaceShipTemplate design)
        {
            if (design == null)
                return "Unknown ship class";

            var sb = new StringBuilder();
            sb.AppendLine($"Ship Class: {design.className}");
            sb.AppendLine();

            // Hull info
            var hull = design.hullTemplate;
            if (hull != null)
            {
                string sizeStr = hull.smallHull ? "Small" : (hull.mediumHull ? "Medium" : (hull.largeHull ? "Large" : "Huge"));
                sb.AppendLine($"Hull: {hull.displayName} ({sizeStr})");
                sb.AppendLine($"Length: {hull.length_m:N0}m, Width: {hull.width_m:N0}m");
            }

            // Role
            try
            {
                sb.AppendLine($"Role: {design.roleStr}");
            }
            catch { }

            // Mass
            sb.AppendLine();
            sb.AppendLine("Mass:");
            try
            {
                float wetMass = design.wetMass_tons;
                float dryMass = design.dryMass_tons();
                float propellant = design.propellantMass_tons;
                sb.AppendLine($"  Wet Mass: {wetMass:N0} tons");
                sb.AppendLine($"  Dry Mass: {dryMass:N0} tons");
                sb.AppendLine($"  Propellant: {propellant:N0} tons ({design.propellantTanks} tanks)");
            }
            catch { }

            // Performance
            sb.AppendLine();
            sb.AppendLine("Performance:");
            try
            {
                float deltaV = design.baseCruiseDeltaV_kps(forceUpdate: false);
                float accel = design.baseCruiseAcceleration_gs(forceUpdate: false) * 1000f; // Convert to milligees
                sb.AppendLine($"  Delta-V: {deltaV:F1} km/s");
                sb.AppendLine($"  Cruise Acceleration: {accel:F1} mg");
            }
            catch { }

            // Combat value
            try
            {
                float combatValue = design.TemplateSpaceCombatValue();
                sb.AppendLine($"  Combat Value: {combatValue:N0}");
            }
            catch { }

            // Armor
            sb.AppendLine();
            sb.AppendLine("Armor:");
            try
            {
                var noseArmor = design.noseArmorTemplate;
                var lateralArmor = design.lateralArmorTemplate;
                var tailArmor = design.tailArmorTemplate;

                if (noseArmor != null)
                    sb.AppendLine($"  Nose: {noseArmor.displayName}, {design.noseArmorValue} points, {design.noseArmorThickness:F2}m thick");
                if (lateralArmor != null)
                    sb.AppendLine($"  Lateral: {lateralArmor.displayName}, {design.lateralArmorValue} points, {design.lateralArmorThickness_m:F2}m thick");
                if (tailArmor != null)
                    sb.AppendLine($"  Tail: {tailArmor.displayName}, {design.tailArmorValue} points, {design.tailArmorThickness:F2}m thick");
            }
            catch { }

            // Weapons
            sb.AppendLine();
            sb.AppendLine("Weapons:");
            try
            {
                var noseWeapons = design.noseWeapons?.ToList();
                var hullWeapons = design.hullWeapons?.ToList();

                if (noseWeapons != null && noseWeapons.Count > 0)
                {
                    sb.AppendLine("  Nose Hardpoints:");
                    foreach (var weapon in noseWeapons)
                    {
                        var template = weapon.weaponTemplate;
                        if (template != null)
                            sb.AppendLine($"    {template.displayName}");
                    }
                }

                if (hullWeapons != null && hullWeapons.Count > 0)
                {
                    sb.AppendLine("  Hull Hardpoints:");
                    foreach (var weapon in hullWeapons)
                    {
                        var template = weapon.weaponTemplate;
                        if (template != null)
                            sb.AppendLine($"    {template.displayName}");
                    }
                }

                if ((noseWeapons == null || noseWeapons.Count == 0) &&
                    (hullWeapons == null || hullWeapons.Count == 0))
                {
                    sb.AppendLine("  No weapons");
                }
            }
            catch { }

            // Propulsion
            sb.AppendLine();
            sb.AppendLine("Propulsion:");
            try
            {
                var drive = design.driveTemplate;
                var powerPlant = design.powerPlantTemplate;
                var radiator = design.radiatorTemplate;

                if (drive != null)
                    sb.AppendLine($"  Drive: {drive.displayName}");
                if (powerPlant != null)
                    sb.AppendLine($"  Power Plant: {powerPlant.displayName}");
                if (radiator != null)
                    sb.AppendLine($"  Radiator: {radiator.displayName}");
            }
            catch { }

            // Utility modules
            try
            {
                var modules = design.utilityModules?.ToList();
                if (modules != null && modules.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Utility Modules:");
                    foreach (var module in modules)
                    {
                        var template = module.moduleTemplate;
                        if (template != null)
                            sb.AppendLine($"  {template.displayName}");
                    }
                }
            }
            catch { }

            // Build cost
            sb.AppendLine();
            sb.AppendLine("Build Cost:");
            try
            {
                var cost = design.spaceResourceConstructionCost(forceUpdateToCache: false, shipyard: null);
                sb.AppendLine($"  {FormatCost(cost)}");
            }
            catch { }

            // Ships in service
            var faction = GameControl.control?.activePlayer;
            if (faction != null)
            {
                try
                {
                    int inService = faction.ships?.Count(s => s.templateName == design.dataName) ?? 0;
                    sb.AppendLine();
                    sb.AppendLine($"Ships in Service: {inService}");
                }
                catch { }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TISpaceShipTemplate design)
        {
            var sections = new List<ISection>();
            if (design == null)
                return sections;

            // Overview section
            sections.Add(CreateOverviewSection(design));

            // Propulsion section
            sections.Add(CreatePropulsionSection(design));

            // Armor section
            sections.Add(CreateArmorSection(design));

            // Weapons section
            sections.Add(CreateWeaponsSection(design));

            // Utility modules section
            var modulesSection = CreateModulesSection(design);
            if (modulesSection != null)
                sections.Add(modulesSection);

            // Actions section (player's designs only)
            var faction = GameControl.control?.activePlayer;
            if (faction != null && design.designingFaction == faction)
            {
                var actionsSection = CreateActionsSection(design, faction);
                if (actionsSection != null)
                    sections.Add(actionsSection);
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateOverviewSection(TISpaceShipTemplate design)
        {
            var section = new DataSection("Overview");

            // Class name
            section.AddItem("Class Name", design.className ?? "Unknown");

            // Hull
            var hull = design.hullTemplate;
            if (hull != null)
            {
                string sizeStr = hull.smallHull ? "Small" : (hull.mediumHull ? "Medium" : (hull.largeHull ? "Large" : "Huge"));
                section.AddItem("Hull", $"{hull.displayName} ({sizeStr})");
            }

            // Role
            try
            {
                section.AddItem("Role", design.roleStr);
            }
            catch
            {
                section.AddItem("Role", design.role.ToString());
            }

            // Mass
            try
            {
                float wetMass = design.wetMass_tons;
                float dryMass = design.dryMass_tons();
                section.AddItem("Mass", $"{wetMass:N0} tons wet, {dryMass:N0} tons dry");
            }
            catch { }

            // Performance
            try
            {
                float deltaV = design.baseCruiseDeltaV_kps(forceUpdate: false);
                section.AddItem("Delta-V", $"{deltaV:F1} km/s");
            }
            catch { }

            try
            {
                float accel = design.baseCruiseAcceleration_gs(forceUpdate: false) * 1000f;
                section.AddItem("Acceleration", $"{accel:F1} mg");
            }
            catch { }

            // Combat values
            try
            {
                float combatValue = design.TemplateSpaceCombatValue();
                section.AddItem("Combat Value", $"{combatValue:N0}");
            }
            catch { }

            try
            {
                float assaultValue = design.AssaultCombatValue(defense: false);
                if (assaultValue > 0)
                    section.AddItem("Assault Value", $"{assaultValue:N0}");
            }
            catch { }

            // Ships in service
            var faction = GameControl.control?.activePlayer;
            if (faction != null)
            {
                try
                {
                    int inService = faction.ships?.Count(s => s.templateName == design.dataName) ?? 0;
                    section.AddItem("In Service", inService.ToString());
                }
                catch { }
            }

            // Build cost
            try
            {
                var cost = design.spaceResourceConstructionCost(forceUpdateToCache: false, shipyard: null);
                section.AddItem("Build Cost", FormatCost(cost));
            }
            catch { }

            return section;
        }

        private ISection CreatePropulsionSection(TISpaceShipTemplate design)
        {
            var section = new DataSection("Propulsion");

            // Drive
            var drive = design.driveTemplate;
            if (drive != null)
            {
                try
                {
                    float thrust = drive.thrust_N / 1000000f; // Convert to MN
                    float ev = drive.EV_kps;
                    string driveDetail = $"Thrust: {thrust:F1} MN, EV: {ev:F1} km/s";
                    section.AddItem("Drive", drive.displayName, driveDetail);
                }
                catch
                {
                    section.AddItem("Drive", drive.displayName);
                }
            }
            else
            {
                section.AddItem("Drive", "None");
            }

            // Power plant
            var powerPlant = design.powerPlantTemplate;
            if (powerPlant != null)
            {
                try
                {
                    float efficiency = powerPlant.efficiency * 100f;
                    float specificPower = powerPlant.specificPower_tGW;
                    string ppDetail = $"Efficiency: {efficiency:F0}%, {specificPower:F2} t/GW";
                    section.AddItem("Power Plant", powerPlant.displayName, ppDetail);
                }
                catch
                {
                    section.AddItem("Power Plant", powerPlant.displayName);
                }
            }
            else
            {
                section.AddItem("Power Plant", "None");
            }

            // Radiator
            var radiator = design.radiatorTemplate;
            if (radiator != null)
            {
                try
                {
                    float vulnerability = radiator.vulnerability * 100f;
                    string radDetail = $"Vulnerability: {vulnerability:F0}%";
                    section.AddItem("Radiator", radiator.displayName, radDetail);
                }
                catch
                {
                    section.AddItem("Radiator", radiator.displayName);
                }
            }
            else
            {
                section.AddItem("Radiator", "None");
            }

            // Propellant
            section.AddItem("Propellant Tanks", $"{design.propellantTanks} ({design.propellantMass_tons:N0} tons)");

            return section;
        }

        private ISection CreateArmorSection(TISpaceShipTemplate design)
        {
            var section = new DataSection("Armor");

            // Nose armor
            try
            {
                var noseArmor = design.noseArmorTemplate;
                if (noseArmor != null && design.noseArmorValue > 0)
                {
                    float thickness = design.noseArmorThickness;
                    string detail = $"Points: {design.noseArmorValue}, Thickness: {thickness:F2}m, Mass: {design.noseArmorMass_tons:N0} tons";
                    section.AddItem("Nose", noseArmor.displayName, detail);
                }
                else
                {
                    section.AddItem("Nose", "None");
                }
            }
            catch
            {
                section.AddItem("Nose", "Unknown");
            }

            // Lateral armor
            try
            {
                var lateralArmor = design.lateralArmorTemplate;
                if (lateralArmor != null && design.lateralArmorValue > 0)
                {
                    float thickness = design.lateralArmorThickness_m;
                    string detail = $"Points: {design.lateralArmorValue}, Thickness: {thickness:F2}m, Mass: {design.lateralArmorMass_tons:N0} tons";
                    section.AddItem("Lateral", lateralArmor.displayName, detail);
                }
                else
                {
                    section.AddItem("Lateral", "None");
                }
            }
            catch
            {
                section.AddItem("Lateral", "Unknown");
            }

            // Tail armor
            try
            {
                var tailArmor = design.tailArmorTemplate;
                if (tailArmor != null && design.tailArmorValue > 0)
                {
                    float thickness = design.tailArmorThickness;
                    string detail = $"Points: {design.tailArmorValue}, Thickness: {thickness:F2}m, Mass: {design.tailArmorMass_tons:N0} tons";
                    section.AddItem("Tail", tailArmor.displayName, detail);
                }
                else
                {
                    section.AddItem("Tail", "None");
                }
            }
            catch
            {
                section.AddItem("Tail", "Unknown");
            }

            // Total armor mass
            try
            {
                float totalMass = design.totalArmorMass_tons;
                section.AddItem("Total Armor Mass", $"{totalMass:N0} tons");
            }
            catch { }

            return section;
        }

        private ISection CreateWeaponsSection(TISpaceShipTemplate design)
        {
            var section = new DataSection("Weapons");

            // Nose weapons
            try
            {
                var noseWeapons = design.noseWeapons?.ToList();
                if (noseWeapons != null && noseWeapons.Count > 0)
                {
                    int slot = 1;
                    foreach (var weapon in noseWeapons)
                    {
                        var template = weapon.weaponTemplate;
                        if (template != null)
                        {
                            string weaponInfo = GetWeaponInfo(template);
                            section.AddItem($"Nose {slot}", template.displayName, weaponInfo);
                        }
                        slot++;
                    }
                }
                else
                {
                    var hull = design.hullTemplate;
                    if (hull != null && hull.noseHardpoints > 0)
                        section.AddItem("Nose Hardpoints", $"{hull.noseHardpoints} empty");
                }
            }
            catch { }

            // Hull weapons
            try
            {
                var hullWeapons = design.hullWeapons?.ToList();
                if (hullWeapons != null && hullWeapons.Count > 0)
                {
                    int slot = 1;
                    foreach (var weapon in hullWeapons)
                    {
                        var template = weapon.weaponTemplate;
                        if (template != null)
                        {
                            string weaponInfo = GetWeaponInfo(template);
                            section.AddItem($"Hull {slot}", template.displayName, weaponInfo);
                        }
                        slot++;
                    }
                }
                else
                {
                    var hull = design.hullTemplate;
                    if (hull != null && hull.hullHardpoints > 0)
                        section.AddItem("Hull Hardpoints", $"{hull.hullHardpoints} empty");
                }
            }
            catch { }

            // If no weapons at all
            var allWeapons = design.allWeapons?.ToList();
            if (allWeapons == null || allWeapons.Count == 0)
            {
                section.AddItem("No weapons installed");
            }

            return section;
        }

        private ISection CreateModulesSection(TISpaceShipTemplate design)
        {
            try
            {
                var modules = design.utilityModules?.ToList();
                if (modules == null || modules.Count == 0)
                    return null;

                var section = new DataSection("Utility Modules");

                int slot = 1;
                foreach (var module in modules)
                {
                    var template = module.moduleTemplate;
                    if (template != null)
                    {
                        string moduleInfo = GetModuleInfo(template);
                        section.AddItem($"Slot {slot}", template.displayName, moduleInfo);
                    }
                    slot++;
                }

                return section;
            }
            catch
            {
                return null;
            }
        }

        private ISection CreateActionsSection(TISpaceShipTemplate design, TIFactionState faction)
        {
            var section = new DataSection("Actions");

            // Design New Ship (always available)
            if (CanDesignShips(faction))
            {
                section.AddItem("Design New Ship", "Create a new ship design from scratch",
                    onActivate: () => OnEnterShipDesignerMode?.Invoke(null));
            }

            // Edit Design
            section.AddItem("Edit Design", "Modify this ship design in the designer",
                onActivate: () => OnEnterShipDesignerMode?.Invoke(design));

            // Build Ship at Shipyard
            try
            {
                var shipyards = GetAvailableShipyards(faction, design);
                if (shipyards.Count > 0)
                {
                    string buildDesc = shipyards.Count == 1
                        ? $"Build at {shipyards[0].hab.displayName}"
                        : $"Build at shipyard ({shipyards.Count} available)";
                    section.AddItem("Build Ship", buildDesc,
                        onActivate: () => StartBuildShip(design, faction, shipyards));
                }
                else if (faction.nShipyardQueues.Keys.Count == 0)
                {
                    section.AddItem("Build Ship", "No shipyards available - build a shipyard first");
                }
                else
                {
                    section.AddItem("Build Ship", "No shipyard can build this design");
                }
            }
            catch { }

            // Toggle Obsolete
            try
            {
                bool isObsolete = design.Obsolete(faction);
                string obsoleteLabel = isObsolete ? "Mark as Active" : "Mark as Obsolete";
                string obsoleteDesc = isObsolete
                    ? "Remove obsolete flag from this design"
                    : "Mark this design as obsolete (hides from build list)";
                section.AddItem(obsoleteLabel, obsoleteDesc,
                    onActivate: () => ToggleObsolete(design, faction));
            }
            catch { }

            // Delete Design (only if no ships in service)
            try
            {
                int inService = faction.ships?.Count(s => s.templateName == design.dataName) ?? 0;
                if (inService == 0 && design.CanDeleteDesign)
                {
                    section.AddItem("Delete Design", "Remove this ship class permanently",
                        onActivate: () => DeleteDesign(design, faction));
                }
                else if (inService > 0)
                {
                    section.AddItem("Delete Design", $"Cannot delete: {inService} ships in service");
                }
            }
            catch { }

            return section;
        }

        #endregion

        #region Helper Methods

        private string GetWeaponInfo(TIShipWeaponTemplate weapon)
        {
            var sb = new StringBuilder();

            try
            {
                // Range
                float range = weapon.targetingRange_km;
                sb.Append($"Range: {range:N0} km");

                // Cooldown
                float cooldown = weapon.cooldown_s;
                sb.Append($", Cooldown: {cooldown:F1}s");

                // Salvo
                int salvo = weapon.salvo_shots;
                if (salvo > 1)
                    sb.Append($", Salvo: {salvo}");

                // Mass
                float mass = weapon.baseWeaponMass_tons;
                sb.Append($", Mass: {mass:N0}t");
            }
            catch { }

            return sb.ToString();
        }

        private string GetModuleInfo(TIShipPartTemplate module)
        {
            var sb = new StringBuilder();

            try
            {
                // Crew
                int crew = module.crew;
                if (crew > 0)
                    sb.Append($"Crew: {crew}");

                // Mass
                float mass = module.buildMass_tons();
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"Mass: {mass:N0}t");
            }
            catch { }

            return sb.ToString();
        }

        private string FormatCost(TIResourcesCost cost)
        {
            if (cost == null)
                return "Unknown";

            var parts = new List<string>();

            float money = cost.GetSingleCostValue(FactionResource.Money);
            if (money > 0)
                parts.Add($"{money:N0} Money");

            float influence = cost.GetSingleCostValue(FactionResource.Influence);
            if (influence > 0)
                parts.Add($"{influence:N0} Influence");

            float ops = cost.GetSingleCostValue(FactionResource.Operations);
            if (ops > 0)
                parts.Add($"{ops:N0} Ops");

            float boost = cost.GetSingleCostValue(FactionResource.Boost);
            if (boost > 0)
                parts.Add($"{boost:N0} Boost");

            float water = cost.GetSingleCostValue(FactionResource.Water);
            if (water > 0)
                parts.Add($"{water:N0} Water");

            float volatiles = cost.GetSingleCostValue(FactionResource.Volatiles);
            if (volatiles > 0)
                parts.Add($"{volatiles:N0} Volatiles");

            float metals = cost.GetSingleCostValue(FactionResource.Metals);
            if (metals > 0)
                parts.Add($"{metals:N0} Metals");

            float nobles = cost.GetSingleCostValue(FactionResource.NobleMetals);
            if (nobles > 0)
                parts.Add($"{nobles:N0} Noble Metals");

            float fissiles = cost.GetSingleCostValue(FactionResource.Fissiles);
            if (fissiles > 0)
                parts.Add($"{fissiles:N0} Fissiles");

            float exotics = cost.GetSingleCostValue(FactionResource.Exotics);
            if (exotics > 0)
                parts.Add($"{exotics:N0} Exotics");

            float antimatter = cost.GetSingleCostValue(FactionResource.Antimatter);
            if (antimatter > 0)
                parts.Add($"{antimatter:N0} Antimatter");

            return parts.Count > 0 ? string.Join(", ", parts) : "Free";
        }

        private List<TIHabModuleState> GetAvailableShipyards(TIFactionState faction, TISpaceShipTemplate design)
        {
            var result = new List<TIHabModuleState>();
            try
            {
                foreach (var shipyard in faction.nShipyardQueues.Keys)
                {
                    if (shipyard != null && shipyard.active && design.CanBuildAtShipyard(shipyard))
                    {
                        result.Add(shipyard);
                    }
                }
            }
            catch { }
            return result;
        }

        #endregion

        #region Actions

        private void StartBuildShip(TISpaceShipTemplate design, TIFactionState faction, List<TIHabModuleState> shipyards)
        {
            if (shipyards == null || shipyards.Count == 0)
            {
                OnSpeak?.Invoke("No available shipyards", true);
                return;
            }

            if (shipyards.Count == 1)
            {
                // Only one shipyard, queue directly
                QueueShipAtShipyard(design, faction, shipyards[0]);
            }
            else
            {
                // Multiple shipyards, let user choose
                var options = new List<SelectionOption>();
                foreach (var shipyard in shipyards)
                {
                    var queueSize = faction.nShipyardQueues[shipyard]?.Count ?? 0;
                    string location = shipyard.hab?.ref_naturalSpaceObject?.displayName ?? "Unknown";
                    string queueInfo = queueSize > 0 ? $", {queueSize} in queue" : "";
                    options.Add(new SelectionOption
                    {
                        Label = $"{shipyard.hab.displayName} (Tier {shipyard.tier})",
                        DetailText = $"At {location}{queueInfo}",
                        Data = shipyard
                    });
                }

                OnEnterSelectionMode?.Invoke("Select shipyard", options, (index) =>
                {
                    if (index >= 0 && index < shipyards.Count)
                    {
                        QueueShipAtShipyard(design, faction, shipyards[index]);
                    }
                });
            }
        }

        private void QueueShipAtShipyard(TISpaceShipTemplate design, TIFactionState faction, TIHabModuleState shipyard)
        {
            try
            {
                // Check if we can afford it
                var cost = design.spaceResourceConstructionCost(forceUpdateToCache: false, shipyard: shipyard);
                if (!cost.CanAfford(faction))
                {
                    OnSpeak?.Invoke($"Cannot afford to build {design.className}. Cost: {FormatCost(cost)}", true);
                    return;
                }

                // Queue the ship
                bool success = faction.AddShipToShipyardQueue(shipyard, design, allowPayFromEarth: true);
                if (success)
                {
                    var queueSize = faction.nShipyardQueues[shipyard]?.Count ?? 1;
                    string message = $"Queued {design.className} at {shipyard.hab.displayName}, position {queueSize}";
                    OnSpeak?.Invoke(message, true);
                }
                else
                {
                    OnSpeak?.Invoke($"Failed to queue {design.className}", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error queuing ship: {ex.Message}");
                OnSpeak?.Invoke("Failed to queue ship for construction", true);
            }
        }

        private void ToggleObsolete(TISpaceShipTemplate design, TIFactionState faction)
        {
            try
            {
                bool wasObsolete = design.Obsolete(faction);

                // Toggle obsolete by adding/removing from the list
                if (wasObsolete)
                {
                    faction.obsoleteShipDesigns.Remove(design.dataName);
                }
                else
                {
                    faction.obsoleteShipDesigns.Add(design.dataName);
                }

                string message = wasObsolete
                    ? $"{design.className} marked as active"
                    : $"{design.className} marked as obsolete";
                OnSpeak?.Invoke(message, true);
                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling obsolete: {ex.Message}");
                OnSpeak?.Invoke("Failed to toggle obsolete status", true);
            }
        }

        private void DeleteDesign(TISpaceShipTemplate design, TIFactionState faction)
        {
            try
            {
                // Use the faction's DeleteShipDesign method
                faction.DeleteShipDesign(design);

                string message = $"Deleted ship class: {design.className}";
                OnSpeak?.Invoke(message, true);
                OnRefreshSections?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error deleting design: {ex.Message}");
                OnSpeak?.Invoke("Failed to delete design", true);
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all ship designs for the player faction.
        /// </summary>
        public static List<TISpaceShipTemplate> GetPlayerDesigns(TIFactionState faction)
        {
            if (faction == null || faction.shipDesigns == null)
                return new List<TISpaceShipTemplate>();

            return faction.shipDesigns
                .Where(d => d != null)
                .OrderBy(d => d.className)
                .ToList();
        }

        /// <summary>
        /// Get non-obsolete ship designs for the player faction.
        /// </summary>
        public static List<TISpaceShipTemplate> GetActiveDesigns(TIFactionState faction)
        {
            if (faction == null || faction.shipDesigns == null)
                return new List<TISpaceShipTemplate>();

            return faction.shipDesigns
                .Where(d => d != null && !d.Obsolete(faction))
                .OrderBy(d => d.className)
                .ToList();
        }

        /// <summary>
        /// Check if the faction can design ships (has researched hull technologies).
        /// Uses the game's FleetsScreenController.CanDesignShips method.
        /// </summary>
        public static bool CanDesignShips(TIFactionState faction)
        {
            if (faction == null)
                return false;

            try
            {
                return FleetsScreenController.CanDesignShips(faction);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the faction has any shipyards for building ships.
        /// </summary>
        public static bool HasShipyards(TIFactionState faction)
        {
            if (faction == null)
                return false;

            try
            {
                return faction.nShipyardQueues?.Keys.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
