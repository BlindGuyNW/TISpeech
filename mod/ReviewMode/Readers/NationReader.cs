using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for nation state data.
    /// Provides summary, detail, and sectioned views of nation information.
    /// </summary>
    public class NationReader : IGameStateReader<TINationState>
    {
        /// <summary>
        /// Callback for setting control point priorities.
        /// </summary>
        public Action<TIControlPoint, PriorityType, int> OnSetPriority { get; set; }

        public string ReadSummary(TINationState nation)
        {
            if (nation == null)
                return "Unknown nation";

            var faction = GameControl.control?.activePlayer;
            int yourCPs = 0;
            int totalCPs = nation.numControlPoints;

            if (faction != null && nation.controlPoints != null)
            {
                yourCPs = nation.controlPoints.Count(cp => cp.faction == faction);
            }

            string cpInfo = yourCPs > 0 ? $"{yourCPs}/{totalCPs} CPs" : $"{totalCPs} CPs";
            string pop = FormatPopulation(nation.population);

            return $"{nation.displayName}, {cpInfo}, {pop} pop";
        }

        public string ReadDetail(TINationState nation)
        {
            if (nation == null)
                return "Unknown nation";

            var sb = new StringBuilder();
            sb.AppendLine($"Nation: {nation.displayName}");

            if (nation.federation != null)
            {
                sb.AppendLine($"Federation: {nation.federation.displayName}");
            }

            sb.AppendLine($"Population: {FormatPopulation(nation.population)}");
            sb.AppendLine($"GDP: ${FormatLargeNumber(nation.GDP)}");
            sb.AppendLine($"GDP per Capita: ${nation.perCapitaGDP:N0}");

            sb.AppendLine();
            sb.AppendLine("Statistics:");
            sb.AppendLine($"  Democracy: {nation.democracy:F1}");
            sb.AppendLine($"  Cohesion: {nation.cohesion:F1}");
            sb.AppendLine($"  Education: {nation.education:F1}");
            sb.AppendLine($"  Inequality: {nation.inequality:F1}");
            sb.AppendLine($"  Unrest: {nation.unrest:F1}");

            sb.AppendLine();
            sb.AppendLine("Military:");
            sb.AppendLine($"  Miltech: {nation.militaryTechLevel:F1}");
            sb.AppendLine($"  Armies: {nation.armies?.Count ?? 0}");
            sb.AppendLine($"  Nuclear Weapons: {nation.numNuclearWeapons}");

            return sb.ToString();
        }

        public List<ISection> GetSections(TINationState nation)
        {
            var sections = new List<ISection>();
            if (nation == null)
                return sections;

            var faction = GameControl.control?.activePlayer;

            // Overview section
            sections.Add(CreateOverviewSection(nation));

            // Your Control Points section (if you have any)
            if (faction != null)
            {
                var yourCPs = nation.controlPoints?.Where(cp => cp.faction == faction).ToList();
                if (yourCPs != null && yourCPs.Count > 0)
                {
                    sections.Add(CreateControlPointsSection(nation, yourCPs, faction));
                }
            }

            // All Control Points section (who controls what)
            sections.Add(CreateAllControlPointsSection(nation));

            // Military section
            sections.Add(CreateMilitarySection(nation));

            // Relations section
            sections.Add(CreateRelationsSection(nation));

            // Regions section
            if (nation.regions != null && nation.regions.Count > 0)
            {
                sections.Add(CreateRegionsSection(nation));
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateOverviewSection(TINationState nation)
        {
            var section = new DataSection("Overview");

            section.AddItem("Population", FormatPopulation(nation.population));
            section.AddItem("GDP", $"${FormatLargeNumber(nation.GDP)}");
            section.AddItem("GDP per Capita", $"${nation.perCapitaGDP:N0}");

            if (nation.federation != null)
            {
                section.AddItem("Federation", nation.federation.displayName);
            }

            section.AddItem("Capital", nation.capital?.displayName ?? "None");
            section.AddItem("Regions", nation.regions?.Count.ToString() ?? "0");

            // Key stats
            section.AddItem("Democracy", $"{nation.democracy:F1}");
            section.AddItem("Cohesion", $"{nation.cohesion:F1}");
            section.AddItem("Education", $"{nation.education:F1}");
            section.AddItem("Inequality", $"{nation.inequality:F1}");
            section.AddItem("Unrest", $"{nation.unrest:F1}");
            section.AddItem("Sustainability", $"{nation.sustainability:F1}");

            return section;
        }

        private ISection CreateControlPointsSection(TINationState nation, List<TIControlPoint> yourCPs, TIFactionState faction)
        {
            var section = new DataSection("Your Control Points");

            foreach (var cp in yourCPs)
            {
                string cpName = GetControlPointName(cp, nation);
                string priorities = GetPrioritySummary(cp);

                // Each control point is an item that can be drilled into for priority setting
                section.AddItem(cpName, priorities, GetControlPointDetail(cp, nation));
            }

            return section;
        }

        private ISection CreateAllControlPointsSection(TINationState nation)
        {
            var section = new DataSection("Control Point Status");

            if (nation.controlPoints == null || nation.controlPoints.Count == 0)
            {
                section.AddItem("No control points");
                return section;
            }

            // Group by faction
            var byFaction = nation.controlPoints
                .GroupBy(cp => cp.faction?.displayName ?? "Uncontrolled")
                .OrderByDescending(g => g.Count());

            foreach (var group in byFaction)
            {
                string factionName = group.Key;
                int count = group.Count();
                section.AddItem(factionName, $"{count} CP{(count > 1 ? "s" : "")}");
            }

            return section;
        }

        private ISection CreateMilitarySection(TINationState nation)
        {
            var section = new DataSection("Military");

            section.AddItem("Military Tech", $"{nation.militaryTechLevel:F1}");
            section.AddItem("Armies", (nation.armies?.Count ?? 0).ToString());
            section.AddItem("Nuclear Weapons", nation.numNuclearWeapons.ToString());

            if (nation.nuclearProgram)
            {
                section.AddItem("Nuclear Program", "Active");
            }

            if (nation.spaceFlightProgram)
            {
                section.AddItem("Space Program", "Active");
            }

            if (nation.canBuildSpaceDefenses)
            {
                section.AddItem("Space Defenses", "Available");
            }

            // List armies if any
            if (nation.armies != null && nation.armies.Count > 0)
            {
                foreach (var army in nation.armies.Take(5))
                {
                    string armyInfo = $"{army.displayName}";
                    if (army.ref_region != null)
                    {
                        armyInfo += $" in {army.ref_region.displayName}";
                    }
                    section.AddItem("Army", armyInfo);
                }

                if (nation.armies.Count > 5)
                {
                    section.AddItem("", $"...and {nation.armies.Count - 5} more armies");
                }
            }

            return section;
        }

        private ISection CreateRelationsSection(TINationState nation)
        {
            var section = new DataSection("Relations");

            // Wars
            if (nation.wars != null && nation.wars.Count > 0)
            {
                foreach (var enemy in nation.wars)
                {
                    section.AddItem("At War", enemy.displayName);
                }
            }

            // Allies
            if (nation.allies != null && nation.allies.Count > 0)
            {
                foreach (var ally in nation.allies.Take(5))
                {
                    section.AddItem("Ally", ally.displayName);
                }
                if (nation.allies.Count > 5)
                {
                    section.AddItem("", $"...and {nation.allies.Count - 5} more allies");
                }
            }

            // Rivals
            if (nation.rivals != null && nation.rivals.Count > 0)
            {
                foreach (var rival in nation.rivals.Take(3))
                {
                    section.AddItem("Rival", rival.displayName);
                }
                if (nation.rivals.Count > 3)
                {
                    section.AddItem("", $"...and {nation.rivals.Count - 3} more rivals");
                }
            }

            if ((nation.wars == null || nation.wars.Count == 0) &&
                (nation.allies == null || nation.allies.Count == 0) &&
                (nation.rivals == null || nation.rivals.Count == 0))
            {
                section.AddItem("No significant relations");
            }

            return section;
        }

        private ISection CreateRegionsSection(TINationState nation)
        {
            var section = new DataSection("Regions");

            foreach (var region in nation.regions.Take(10))
            {
                string pop = FormatPopulation(region.population);
                section.AddItem(region.displayName, pop);
            }

            if (nation.regions.Count > 10)
            {
                section.AddItem("", $"...and {nation.regions.Count - 10} more regions");
            }

            return section;
        }

        #endregion

        #region Control Point Helpers

        private string GetControlPointName(TIControlPoint cp, TINationState nation)
        {
            // Control points are identified by type and index
            int index = nation.controlPoints?.IndexOf(cp) ?? 0;
            string typeName = cp.controlPointType.ToString();
            return $"CP {index + 1} ({typeName})";
        }

        private string GetPrioritySummary(TIControlPoint cp)
        {
            var priorities = new List<string>();

            foreach (PriorityType priority in Enum.GetValues(typeof(PriorityType)))
            {
                if (priority == PriorityType.None)
                    continue;

                int value = cp.GetControlPointPriority(priority, checkValid: true);
                if (value > 0)
                {
                    priorities.Add($"{GetPriorityShortName(priority)}:{value}");
                }
            }

            return priorities.Count > 0 ? string.Join(", ", priorities) : "No priorities set";
        }

        private string GetControlPointDetail(TIControlPoint cp, TINationState nation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Control Point in {nation.displayName}");
            sb.AppendLine($"Type: {cp.controlPointType}");

            if (cp.defended)
            {
                sb.AppendLine("Status: Defended");
            }
            if (cp.benefitsDisabled)
            {
                sb.AppendLine("Status: Benefits disabled (crackdown)");
            }

            sb.AppendLine();
            sb.AppendLine("Priorities:");

            foreach (PriorityType priority in Enum.GetValues(typeof(PriorityType)))
            {
                if (priority == PriorityType.None)
                    continue;

                int value = cp.GetControlPointPriority(priority, checkValid: false);
                bool valid = nation.ValidPriority(priority);

                if (value > 0 || valid)
                {
                    string status = valid ? "" : " (unavailable)";
                    sb.AppendLine($"  {GetPriorityDisplayName(priority)}: {value}{status}");
                }
            }

            return sb.ToString();
        }

        private string GetPriorityShortName(PriorityType priority)
        {
            switch (priority)
            {
                case PriorityType.Economy: return "Econ";
                case PriorityType.Welfare: return "Welf";
                case PriorityType.Environment: return "Env";
                case PriorityType.Knowledge: return "Know";
                case PriorityType.Government: return "Gov";
                case PriorityType.Unity: return "Unit";
                case PriorityType.Oppression: return "Oppr";
                case PriorityType.Funding: return "Fund";
                case PriorityType.Spoils: return "Spoil";
                case PriorityType.Military: return "Mil";
                case PriorityType.MissionControl: return "MC";
                case PriorityType.LaunchFacilities: return "Launch";
                default: return priority.ToString().Substring(0, Math.Min(4, priority.ToString().Length));
            }
        }

        private string GetPriorityDisplayName(PriorityType priority)
        {
            switch (priority)
            {
                case PriorityType.Economy: return "Economy";
                case PriorityType.Welfare: return "Welfare";
                case PriorityType.Environment: return "Environment";
                case PriorityType.Knowledge: return "Knowledge";
                case PriorityType.Government: return "Government";
                case PriorityType.Unity: return "Unity";
                case PriorityType.Oppression: return "Oppression";
                case PriorityType.Funding: return "Space Funding";
                case PriorityType.Spoils: return "Spoils";
                case PriorityType.Military: return "Military";
                case PriorityType.MissionControl: return "Mission Control";
                case PriorityType.LaunchFacilities: return "Launch Facilities";
                case PriorityType.Civilian_InitiateSpaceflightProgram: return "Initiate Space Program";
                case PriorityType.Military_FoundMilitary: return "Found Military";
                case PriorityType.Military_BuildArmy: return "Build Army";
                case PriorityType.Military_BuildNavy: return "Build Navy";
                case PriorityType.Military_InitiateNuclearProgram: return "Initiate Nuclear Program";
                case PriorityType.Military_BuildNuclearWeapons: return "Build Nuclear Weapons";
                case PriorityType.Military_BuildSpaceDefenses: return "Build Space Defenses";
                case PriorityType.Military_BuildSTOSquadron: return "Build STO Squadron";
                default: return priority.ToString();
            }
        }

        #endregion

        #region Formatting Helpers

        private string FormatPopulation(float population)
        {
            // population is in actual people, not millions
            if (population >= 1_000_000_000)
            {
                return $"{population / 1_000_000_000:F2}B";
            }
            if (population >= 1_000_000)
            {
                return $"{population / 1_000_000:F1}M";
            }
            if (population >= 1_000)
            {
                return $"{population / 1_000:F0}K";
            }
            return $"{population:F0}";
        }

        private string FormatLargeNumber(double value)
        {
            if (value >= 1_000_000_000_000)
            {
                return $"{value / 1_000_000_000_000:F1}T";
            }
            if (value >= 1_000_000_000)
            {
                return $"{value / 1_000_000_000:F1}B";
            }
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000:F1}M";
            }
            return $"{value:N0}";
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get nations where the player has control points.
        /// </summary>
        public static List<TINationState> GetPlayerNations(TIFactionState faction)
        {
            if (faction == null)
                return new List<TINationState>();

            try
            {
                var nations = new HashSet<TINationState>();

                // Get all control points for the faction
                if (faction.controlPoints != null)
                {
                    foreach (var cp in faction.controlPoints)
                    {
                        if (cp.nation != null)
                        {
                            nations.Add(cp.nation);
                        }
                    }
                }

                return nations.OrderByDescending(n =>
                    n.controlPoints?.Count(cp => cp.faction == faction) ?? 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player nations: {ex.Message}");
                return new List<TINationState>();
            }
        }

        /// <summary>
        /// Get all nations in the game.
        /// </summary>
        public static List<TINationState> GetAllNations()
        {
            try
            {
                return GameStateManager.AllNations()?.ToList() ?? new List<TINationState>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting all nations: {ex.Message}");
                return new List<TINationState>();
            }
        }

        #endregion
    }
}
