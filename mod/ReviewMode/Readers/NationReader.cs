using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

// Alias for the nested enum to avoid ambiguity
using TrackedValue = PavonisInteractive.TerraInvicta.NationInfoController.TrackedValue;

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
            sb.AppendLine("Space:");
            sb.AppendLine($"  Investment Points: {nation.BaseInvestmentPoints_month():F1} IP/month");
            sb.AppendLine($"  Boost: {nation.boostIncome_month_dekatons:F1} dt/month");
            sb.AppendLine($"  Space Funding: ${FormatLargeNumber(nation.spaceFunding_month)}/month");
            sb.AppendLine($"  Research: {nation.research_month:F1}/month");
            sb.AppendLine($"  Mission Control: {nation.missionControl:F1}");
            if (nation.spaceFlightProgram)
                sb.AppendLine("  Space Program: Active");
            if (nation.canBuildSpaceDefenses)
                sb.AppendLine("  Space Defenses: Available");

            sb.AppendLine();
            sb.AppendLine("Statistics:");
            sb.AppendLine($"  Democracy: {nation.democracy:F1}");
            sb.AppendLine($"  Cohesion: {nation.cohesion:F1}");
            sb.AppendLine($"  Education: {nation.education:F1}");
            sb.AppendLine($"  Inequality: {nation.inequality:F1}");
            sb.AppendLine($"  Unrest: {nation.unrest:F1}");
            sb.AppendLine($"  Sustainability: {TINationState.SustainabilityValueForDisplay(nation.sustainability)}");

            sb.AppendLine();
            sb.AppendLine("Military:");
            sb.AppendLine($"  Miltech: {nation.militaryTechLevel:F1}");
            sb.AppendLine($"  Armies: {nation.armies?.Count ?? 0}");
            sb.AppendLine($"  Navies: {nation.numNavies}");
            sb.AppendLine($"  STO Fighters: {nation.numSTOFighters}");
            sb.AppendLine($"  Nuclear Weapons: {nation.numNuclearWeapons}");
            if (nation.nuclearProgram)
                sb.AppendLine("  Nuclear Program: Active");

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

            // Public Opinion section - shows faction popularity
            sections.Add(CreatePublicOpinionSection(nation));

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

            // Adjacency section - neighboring nations
            sections.Add(CreateAdjacencySection(nation));

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

            // Population with tooltip
            string populationTooltip = GetCleanTooltip(() => NationInfoController.BuildPopulationTooltip(nation));
            section.AddItem("Population", FormatPopulation(nation.population), populationTooltip);

            // GDP with tooltip
            string gdpTooltip = GetCleanTooltip(() => NationInfoController.BuildGDPTooltip(nation));
            section.AddItem("GDP", $"${FormatLargeNumber(nation.GDP)}", gdpTooltip);

            // GDP per Capita with tooltip
            string perCapitaTooltip = GetCleanTooltip(() => NationInfoController.BuildPerCapitaGDPTooltip(nation));
            section.AddItem("GDP per Capita", $"${nation.perCapitaGDP:N0}", perCapitaTooltip);

            if (nation.federation != null)
            {
                string federationTooltip = GetCleanTooltip(() => NationInfoController.BuildSpecialRelationshipTooltip(nation));
                section.AddItem("Federation", nation.federation.displayName, federationTooltip);
            }

            section.AddItem("Capital", nation.capital?.displayName ?? "None");
            section.AddItem("Regions", nation.regions?.Count.ToString() ?? "0");

            // Investment Points with tooltip
            string ipTooltip = GetCleanTooltip(() => NationInfoController.BuildInvestmentTooltip(nation));
            float ipPerMonth = nation.BaseInvestmentPoints_month();
            section.AddItem("Investment Points", $"{ipPerMonth:F1} IP/month", ipTooltip);

            // Boost income
            float boostPerMonth = nation.boostIncome_month_dekatons;
            if (boostPerMonth > 0)
            {
                string boostTooltip = GetCleanTooltip(() => NationInfoController.BuildBoostTooltip(nation));
                section.AddItem("Boost", $"{boostPerMonth:F1} dt/month", boostTooltip);
            }

            // Space funding
            float fundingPerMonth = nation.spaceFunding_month;
            string fundingTooltip = GetCleanTooltip(() => NationInfoController.BuildSpaceFundingTooltip(nation));
            section.AddItem("Space Funding", $"${FormatLargeNumber(fundingPerMonth)}/month", fundingTooltip);

            // Research
            float researchPerMonth = nation.research_month;
            string researchTooltip = GetCleanTooltip(() => NationInfoController.BuildResearchTooltip(nation));
            section.AddItem("Research", $"{researchPerMonth:F1}/month", researchTooltip);

            // Mission Control
            float missionControl = nation.missionControl;
            string mcTooltip = GetCleanTooltip(() => NationInfoController.BuildMissionControlTooltip(nation));
            section.AddItem("Mission Control", $"{missionControl:F1}", mcTooltip);

            // Key stats with tooltips
            string democracyTooltip = GetCleanTooltip(() => NationInfoController.BuildDemocracyTooltip(nation));
            section.AddItem("Democracy", $"{nation.democracy:F1}", democracyTooltip);

            string cohesionTooltip = GetCleanTooltip(() => NationInfoController.BuildCohesionTooltip(nation));
            section.AddItem("Cohesion", $"{nation.cohesion:F1}", cohesionTooltip);

            string educationTooltip = GetCleanTooltip(() => NationInfoController.BuildEducationTooltip(nation));
            section.AddItem("Education", $"{nation.education:F1}", educationTooltip);

            string inequalityTooltip = GetCleanTooltip(() => NationInfoController.BuildInequalityTooltip(nation));
            section.AddItem("Inequality", $"{nation.inequality:F1}", inequalityTooltip);

            string unrestTooltip = GetCleanTooltip(() => NationInfoController.BuildUnrestTooltip(nation));
            section.AddItem("Unrest", $"{nation.unrest:F1}", unrestTooltip);

            string sustainabilityTooltip = GetCleanTooltip(() => NationInfoController.BuildSustainabilityTooltip(nation));
            // Sustainability uses inverse display (1/value), so use game's display method
            string sustainabilityDisplay = TINationState.SustainabilityValueForDisplay(nation.sustainability);
            section.AddItem("Sustainability", sustainabilityDisplay, sustainabilityTooltip);

            return section;
        }

        /// <summary>
        /// Safely get tooltip text and clean it for screen reader output.
        /// </summary>
        private string GetCleanTooltip(Func<string> tooltipBuilder)
        {
            try
            {
                string tooltip = tooltipBuilder();
                return TISpeechMod.CleanText(tooltip);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not build tooltip: {ex.Message}");
                return "";
            }
        }

        private ISection CreatePublicOpinionSection(TINationState nation)
        {
            var section = new DataSection("Public Opinion");
            var playerFaction = GameControl.control?.activePlayer;

            try
            {
                // Get all active human ideologies (the factions in the game)
                var activeIdeologies = GameStateManager.ActiveHumanIdeologies();
                if (activeIdeologies == null)
                {
                    section.AddItem("Public opinion data unavailable");
                    return section;
                }

                // Get the overall public opinion tooltip for detail text
                string overallTooltip = GetCleanTooltip(() => NationInfoController.BuildPublicOpinionTooltip(nation));

                // Sort by popularity (highest first)
                var sortedIdeologies = activeIdeologies
                    .Select(template => new {
                        Template = template,
                        Popularity = nation.GetPublicOpinionOfFaction(template.ideology)
                    })
                    .OrderByDescending(x => x.Popularity)
                    .ToList();

                foreach (var item in sortedIdeologies)
                {
                    var ideology = item.Template.ideology;
                    float popularity = item.Popularity;
                    string percentStr = $"{popularity * 100:F1}%";

                    string factionName;
                    string detailText;
                    bool isPlayerFaction = false;

                    if (ideology == FactionIdeology.Undecided)
                    {
                        // Undecided population
                        factionName = "Undecided";
                        detailText = "Population not aligned with any faction";
                    }
                    else
                    {
                        // Get the faction for this ideology
                        var faction = TIFactionIdeologyTemplate.GetFactionByIdeology(ideology);
                        if (faction != null)
                        {
                            factionName = faction.displayName;
                            isPlayerFaction = (faction == playerFaction);

                            // Build detail text
                            var sb = new StringBuilder();
                            sb.AppendLine($"{faction.displayName}: {percentStr} of population supports this faction");

                            // Get historical change if available
                            try
                            {
                                if (nation.historyPublicOpinion != null && nation.historyPublicOpinion.Count > 31)
                                {
                                    nation.historyPublicOpinion[31].TryGetValue(ideology, out float lastMonth);
                                    float change = popularity - lastMonth;
                                    if (Math.Abs(change) > 0.001f)
                                    {
                                        string changeDir = change > 0 ? "increased" : "decreased";
                                        sb.AppendLine($"Change from last month: {changeDir} by {Math.Abs(change) * 100:F1}%");
                                    }
                                    else
                                    {
                                        sb.AppendLine("No change from last month");
                                    }
                                }
                            }
                            catch { }

                            detailText = sb.ToString();
                        }
                        else
                        {
                            factionName = ideology.ToString();
                            detailText = $"{percentStr} support";
                        }
                    }

                    // Mark player's faction
                    string label = isPlayerFaction ? $"{factionName} (You)" : factionName;
                    section.AddItem(label, percentStr, detailText);
                }

                // Add summary info about public opinion mechanics
                if (!string.IsNullOrEmpty(overallTooltip))
                {
                    section.AddItem("About Public Opinion", "How public opinion works", overallTooltip);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building public opinion section: {ex.Message}");
                section.AddItem("Error loading public opinion data");
            }

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
                int defendedCount = group.Count(cp => cp.defended);
                int crackdownCount = group.Count(cp => cp.benefitsDisabled);

                string details = $"{count} CP{(count > 1 ? "s" : "")}";
                if (defendedCount > 0)
                    details += $", {defendedCount} defended";
                if (crackdownCount > 0)
                    details += $", {crackdownCount} under crackdown";

                section.AddItem(factionName, details);
            }

            return section;
        }

        private ISection CreateMilitarySection(TINationState nation)
        {
            var section = new DataSection("Military");

            section.AddItem("Military Tech", $"{nation.militaryTechLevel:F1}");
            section.AddItem("Armies", (nation.armies?.Count ?? 0).ToString());
            section.AddItem("Navies", nation.numNavies.ToString());
            section.AddItem("STO Fighters", nation.numSTOFighters.ToString());
            section.AddItem("Nuclear Weapons", nation.numNuclearWeapons.ToString());

            if (nation.nuclearProgram)
            {
                section.AddItem("Nuclear Program", "Active");
            }
            else if (nation.numNuclearWeapons == 0)
            {
                section.AddItem("Nuclear Program", "None");
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

        private ISection CreateAdjacencySection(TINationState nation)
        {
            var section = new DataSection("Neighbors");
            var faction = GameControl.control?.activePlayer;

            try
            {
                // Get all adjacent nations (including friendly crossing only)
                var adjacentNations = nation.AdjacentNations(IAmAnInvadingArmy: false);

                if (adjacentNations == null || adjacentNations.Count == 0)
                {
                    section.AddItem("No neighboring nations (island nation)");
                    return section;
                }

                // Sort by name for easier navigation
                var sortedNeighbors = adjacentNations.OrderBy(n => n.displayName).ToList();

                foreach (var neighbor in sortedNeighbors)
                {
                    string neighborName = neighbor.displayName;
                    var adjacencyType = nation.NationAdjacency(neighbor);

                    // Build detail string
                    string adjacencyDesc = GetAdjacencyDescription(adjacencyType);
                    string controlInfo = GetNationControlSummary(neighbor, faction);

                    string label = neighborName;
                    string value = adjacencyDesc;
                    if (!string.IsNullOrEmpty(controlInfo))
                    {
                        value += $", {controlInfo}";
                    }

                    // Detail includes more info about the neighbor
                    string detail = BuildNeighborDetail(neighbor, adjacencyType, faction);

                    section.AddItem(label, value, detail);
                }

                // Add summary at end
                int fullCount = adjacentNations.Count(n => nation.NationAdjacency(n) == TerrestrialAdjacencyType.FullAdjacency);
                int friendlyOnlyCount = adjacentNations.Count - fullCount;

                string summary = $"{adjacentNations.Count} neighbors total";
                if (friendlyOnlyCount > 0)
                {
                    summary += $" ({fullCount} land borders, {friendlyOnlyCount} sea/friendly only)";
                }
                section.AddItem("Total", summary);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building adjacency section: {ex.Message}");
                section.AddItem("Error loading adjacency data");
            }

            return section;
        }

        private string GetAdjacencyDescription(TerrestrialAdjacencyType adjacencyType)
        {
            switch (adjacencyType)
            {
                case TerrestrialAdjacencyType.FullAdjacency:
                    return "land border";
                case TerrestrialAdjacencyType.FriendlyCrossingOnly:
                    return "sea/friendly crossing";
                default:
                    return "adjacent";
            }
        }

        private string GetNationControlSummary(TINationState nation, TIFactionState viewerFaction)
        {
            if (nation.controlPoints == null || nation.controlPoints.Count == 0)
                return "uncontrolled";

            // Check if viewer has any CPs
            int viewerCPs = viewerFaction != null
                ? nation.controlPoints.Count(cp => cp.faction == viewerFaction)
                : 0;

            if (viewerCPs > 0)
            {
                return $"you have {viewerCPs}/{nation.numControlPoints} CPs";
            }

            // Find top controller
            var topFaction = nation.controlPoints
                .Where(cp => cp.faction != null)
                .GroupBy(cp => cp.faction)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (topFaction == null)
                return "uncontrolled";

            int topCount = topFaction.Count();
            if (topCount == nation.numControlPoints)
                return $"controlled by {topFaction.Key.displayName}";
            else
                return $"{topFaction.Key.displayName} has {topCount}/{nation.numControlPoints}";
        }

        private string BuildNeighborDetail(TINationState neighbor, TerrestrialAdjacencyType adjacencyType, TIFactionState viewerFaction)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Nation: {neighbor.displayName}");
            sb.AppendLine($"Border type: {GetAdjacencyDescription(adjacencyType)}");

            if (adjacencyType == TerrestrialAdjacencyType.FullAdjacency)
            {
                sb.AppendLine("Armies can invade across this border");
            }
            else if (adjacencyType == TerrestrialAdjacencyType.FriendlyCrossingOnly)
            {
                sb.AppendLine("Armies can only cross if not invading (friendly passage)");
            }

            sb.AppendLine();
            sb.AppendLine($"Population: {FormatPopulation(neighbor.population)}");
            sb.AppendLine($"GDP: ${FormatLargeNumber(neighbor.GDP)}");
            sb.AppendLine($"Military Tech: {neighbor.militaryTechLevel:F1}");

            if (neighbor.armies != null && neighbor.armies.Count > 0)
            {
                sb.AppendLine($"Armies: {neighbor.armies.Count}");
            }

            if (neighbor.numNuclearWeapons > 0)
            {
                sb.AppendLine($"Nuclear Weapons: {neighbor.numNuclearWeapons}");
            }

            // Control info
            sb.AppendLine();
            string controlInfo = GetNationControlSummary(neighbor, viewerFaction);
            sb.AppendLine($"Control: {controlInfo}");

            return sb.ToString();
        }

        private ISection CreateRegionsSection(TINationState nation)
        {
            var section = new DataSection("Regions");

            foreach (var region in nation.regions.Take(10))
            {
                string pop = FormatPopulation(region.population);

                // Get adjacent regions info
                var adjacentRegions = region.AdjacentRegions(IAmAnInvadingArmy: false);
                int foreignAdjacent = adjacentRegions?.Count(r => r.nation != nation) ?? 0;

                string value = pop;
                if (foreignAdjacent > 0)
                {
                    value += $", borders {foreignAdjacent} foreign region{(foreignAdjacent > 1 ? "s" : "")}";
                }

                // Build detail with adjacent region info
                string detail = BuildRegionDetail(region, nation);

                section.AddItem(region.displayName, value, detail);
            }

            if (nation.regions.Count > 10)
            {
                section.AddItem("", $"...and {nation.regions.Count - 10} more regions");
            }

            return section;
        }

        private string BuildRegionDetail(TIRegionState region, TINationState nation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Region: {region.displayName}");
            sb.AppendLine($"Population: {FormatPopulation(region.population)}");

            if (region == nation.capital)
            {
                sb.AppendLine("This is the capital region");
            }

            // Adjacent regions
            var adjacentRegions = region.AdjacentRegions(IAmAnInvadingArmy: false);
            if (adjacentRegions != null && adjacentRegions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Adjacent regions:");

                // Group by nation
                var byNation = adjacentRegions
                    .GroupBy(r => r.nation)
                    .OrderBy(g => g.Key == nation ? 0 : 1) // Own nation first
                    .ThenBy(g => g.Key?.displayName ?? "");

                foreach (var group in byNation)
                {
                    string nationName = group.Key == nation ? "same nation" : group.Key?.displayName ?? "unknown";
                    var regionNames = group.Select(r => r.displayName).Take(5);
                    sb.AppendLine($"  {nationName}: {string.Join(", ", regionNames)}");
                    if (group.Count() > 5)
                    {
                        sb.AppendLine($"    ...and {group.Count() - 5} more");
                    }
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Control Point Helpers

        private string GetControlPointName(TIControlPoint cp, TINationState nation)
        {
            // Control points are identified by type and index
            int index = nation.controlPoints?.IndexOf(cp) ?? 0;
            string typeName = cp.controlPointType.ToString();
            string defended = cp.defended ? ", defended" : "";
            string crackdown = cp.benefitsDisabled ? ", crackdown" : "";
            return $"CP {index + 1} ({typeName}{defended}{crackdown})";
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
