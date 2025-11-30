using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
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

            // Modules section
            sections.Add(CreateModulesSection(hab));

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

        private ISection CreateModulesSection(TIHabState hab)
        {
            var section = new DataSection("Modules");

            var allModules = hab.AllModules();
            if (allModules == null || allModules.Count == 0)
            {
                section.AddItem("No modules");
                return section;
            }

            // Sort modules: active first, then under construction, then others
            var sortedModules = allModules
                .OrderByDescending(m => m.active)
                .ThenByDescending(m => m.functional)
                .ThenBy(m => m.underConstruction ? 0 : 1)
                .ThenBy(m => m.sectorNum)
                .ThenBy(m => m.slot)
                .ToList();

            foreach (var module in sortedModules)
            {
                string label = module.displayName ?? "Unknown Module";
                string status = GetModuleStatus(module);
                string power = GetModulePowerString(module);
                string value = !string.IsNullOrEmpty(power) ? $"{status}, {power}" : status;

                // Create detail text for * key
                string detail = GetModuleDetailText(module);

                section.AddItem(label, value, detail);
            }

            return section;
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
