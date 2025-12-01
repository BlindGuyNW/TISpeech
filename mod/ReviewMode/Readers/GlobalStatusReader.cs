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
    /// Reader for global status information.
    /// Handles: Public Opinion, Environmental, Economy, Wars, Atrocities.
    /// </summary>
    public class GlobalStatusReader
    {
        /// <summary>
        /// Global status category types.
        /// </summary>
        public enum GlobalCategory
        {
            PublicOpinion,
            Environmental,
            Economy,
            Wars,
            Atrocities
        }

        /// <summary>
        /// Get display name for a category.
        /// </summary>
        public static string GetCategoryName(GlobalCategory category)
        {
            switch (category)
            {
                case GlobalCategory.PublicOpinion: return "Public Opinion";
                case GlobalCategory.Environmental: return "Environmental";
                case GlobalCategory.Economy: return "Economy";
                case GlobalCategory.Wars: return "Wars";
                case GlobalCategory.Atrocities: return "Atrocities";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Read category summary.
        /// </summary>
        public string ReadCategorySummary(GlobalCategory category)
        {
            try
            {
                switch (category)
                {
                    case GlobalCategory.PublicOpinion:
                        return GetPublicOpinionSummary();
                    case GlobalCategory.Environmental:
                        return GetEnvironmentalSummary();
                    case GlobalCategory.Economy:
                        return GetEconomySummary();
                    case GlobalCategory.Wars:
                        return GetWarsSummary();
                    case GlobalCategory.Atrocities:
                        return GetAtrocitiesSummary();
                    default:
                        return "Unknown category";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading category summary for {category}: {ex.Message}");
                return $"{GetCategoryName(category)}: Error reading data";
            }
        }

        /// <summary>
        /// Read category detail.
        /// </summary>
        public string ReadCategoryDetail(GlobalCategory category)
        {
            try
            {
                switch (category)
                {
                    case GlobalCategory.PublicOpinion:
                        return GetPublicOpinionDetail();
                    case GlobalCategory.Environmental:
                        return GetEnvironmentalDetail();
                    case GlobalCategory.Economy:
                        return GetEconomyDetail();
                    case GlobalCategory.Wars:
                        return GetWarsDetail();
                    case GlobalCategory.Atrocities:
                        return GetAtrocitiesDetail();
                    default:
                        return "Unknown category";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading category detail for {category}: {ex.Message}");
                return $"{GetCategoryName(category)}\nError reading data: {ex.Message}";
            }
        }

        /// <summary>
        /// Get sections for a category.
        /// </summary>
        public List<ISection> GetSectionsForCategory(GlobalCategory category)
        {
            var sections = new List<ISection>();

            try
            {
                switch (category)
                {
                    case GlobalCategory.PublicOpinion:
                        sections.AddRange(GetPublicOpinionSections());
                        break;
                    case GlobalCategory.Environmental:
                        sections.AddRange(GetEnvironmentalSections());
                        break;
                    case GlobalCategory.Economy:
                        sections.AddRange(GetEconomySections());
                        break;
                    case GlobalCategory.Wars:
                        sections.AddRange(GetWarsSections());
                        break;
                    case GlobalCategory.Atrocities:
                        sections.AddRange(GetAtrocitiesSections());
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting sections for {category}: {ex.Message}");
                var errorSection = new DataSection("Error");
                errorSection.AddItem("Unable to read data", ex.Message);
                sections.Add(errorSection);
            }

            return sections;
        }

        #region Public Opinion

        private string GetPublicOpinionSummary()
        {
            var opinions = TIGlobalValuesState.GlobalValues?.GetGlobalPublicOpinionProportions();
            if (opinions == null) return "Public Opinion: No data";

            // Find the top 3 ideologies
            var topOpinions = opinions
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => $"{GetIdeologyName(kv.Key)} {kv.Value:P0}");

            return $"Public Opinion: {string.Join(", ", topOpinions)}";
        }

        private string GetPublicOpinionDetail()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Global Public Opinion");
            sb.AppendLine();

            var opinions = TIGlobalValuesState.GlobalValues?.GetGlobalPublicOpinionProportions();
            if (opinions == null)
            {
                sb.AppendLine("No data available");
                return sb.ToString();
            }

            foreach (var kv in opinions.OrderByDescending(x => x.Value))
            {
                string ideologyName = GetIdeologyName(kv.Key);
                sb.AppendLine($"{ideologyName}: {kv.Value:P1}");
            }

            return sb.ToString();
        }

        private List<ISection> GetPublicOpinionSections()
        {
            var sections = new List<ISection>();
            var opinionSection = new DataSection("Ideology Distribution");

            var opinions = TIGlobalValuesState.GlobalValues?.GetGlobalPublicOpinionProportions();
            if (opinions != null)
            {
                foreach (var kv in opinions.OrderByDescending(x => x.Value))
                {
                    string ideologyName = GetIdeologyName(kv.Key);
                    opinionSection.AddItem(ideologyName, $"{kv.Value:P1}");
                }
            }
            else
            {
                opinionSection.AddItem("No data available");
            }

            sections.Add(opinionSection);
            return sections;
        }

        private string GetIdeologyName(FactionIdeology ideology)
        {
            // Try to find the faction with this ideology
            var factions = GameStateManager.AllHumanFactions();
            foreach (var faction in factions)
            {
                if (faction.ideology?.ideology == ideology)
                    return faction.displayName;
            }

            if (ideology == FactionIdeology.Undecided)
                return "Undecided";

            return ideology.ToString();
        }

        #endregion

        #region Environmental

        private string GetEnvironmentalSummary()
        {
            var global = TIGlobalValuesState.GlobalValues;
            if (global == null) return "Environmental: No data";

            float tempC = global.temperatureAnomaly_C;
            float co2 = global.earthAtmosphericCO2_ppm;
            return $"Environmental: {tempC:+0.0;-0.0;0.0}C anomaly, CO2 at {co2:F0} ppm";
        }

        private string GetEnvironmentalDetail()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Environmental Status");
            sb.AppendLine();

            var global = TIGlobalValuesState.GlobalValues;
            if (global == null)
            {
                sb.AppendLine("No data available");
                return sb.ToString();
            }

            sb.AppendLine($"Temperature Anomaly: {global.temperatureAnomaly_C:+0.00;-0.00;0.00} C ({global.temperatureAnomaly_F:+0.00;-0.00;0.00} F)");
            sb.AppendLine($"Sea Level Anomaly: {global.globalSeaLevelAnomaly_cm:F1} cm");
            sb.AppendLine();
            sb.AppendLine("Greenhouse Gases:");
            sb.AppendLine($"  CO2: {global.earthAtmosphericCO2_ppm:F1} ppm (safe: 325.68 ppm)");
            sb.AppendLine($"  CH4: {global.earthAtmosphericCH4_ppm:F2} ppm (safe: 1.30 ppm)");
            sb.AppendLine($"  N2O: {global.earthAtmosphericN2O_ppm:F2} ppm (safe: 0.29 ppm)");
            sb.AppendLine($"  Stratospheric Aerosols: {global.stratosphericAerosols_ppm:F4} ppm");

            return sb.ToString();
        }

        private List<ISection> GetEnvironmentalSections()
        {
            var sections = new List<ISection>();
            var global = TIGlobalValuesState.GlobalValues;

            if (global == null)
            {
                var errorSection = new DataSection("Environmental");
                errorSection.AddItem("No data available");
                sections.Add(errorSection);
                return sections;
            }

            // Temperature section
            var tempSection = new DataSection("Temperature");
            tempSection.AddItem("Anomaly (C)", $"{global.temperatureAnomaly_C:+0.00;-0.00;0.00}");
            tempSection.AddItem("Anomaly (F)", $"{global.temperatureAnomaly_F:+0.00;-0.00;0.00}");
            tempSection.AddItem("Sea Level Change", $"{global.globalSeaLevelAnomaly_cm:F1} cm");
            sections.Add(tempSection);

            // GHG section
            var ghgSection = new DataSection("Greenhouse Gases");
            ghgSection.AddItem("CO2", $"{global.earthAtmosphericCO2_ppm:F1} ppm",
                $"Current: {global.earthAtmosphericCO2_ppm:F1} ppm\nSafe level: 325.68 ppm\nPre-industrial: 280 ppm");
            ghgSection.AddItem("CH4", $"{global.earthAtmosphericCH4_ppm:F2} ppm",
                $"Current: {global.earthAtmosphericCH4_ppm:F2} ppm\nSafe level: 1.30 ppm");
            ghgSection.AddItem("N2O", $"{global.earthAtmosphericN2O_ppm:F2} ppm",
                $"Current: {global.earthAtmosphericN2O_ppm:F2} ppm\nSafe level: 0.29 ppm");
            ghgSection.AddItem("Aerosols", $"{global.stratosphericAerosols_ppm:F4} ppm",
                "Stratospheric aerosols can cool the planet but have side effects.");
            sections.Add(ghgSection);

            return sections;
        }

        #endregion

        #region Economy

        private string GetEconomySummary()
        {
            double gdp = TIGlobalValuesState.globalGDP;
            double population = GetTotalPopulation();
            return $"Economy: Global GDP ${gdp / 1e12:F1}T, Population {population:F1}B";
        }

        private string GetEconomyDetail()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Global Economy");
            sb.AppendLine();

            double gdp = TIGlobalValuesState.globalGDP;
            double population = GetTotalPopulation();
            double perCapita = population > 0 ? gdp / (population * 1e9) : 0;

            sb.AppendLine($"Global GDP: ${gdp / 1e12:F2} trillion");
            sb.AppendLine($"Earth Population: {population:F2} billion");
            sb.AppendLine($"Per Capita GDP: ${perCapita:N0}");

            // Space population
            int spacePop = GetSpacePopulation();
            if (spacePop > 0)
            {
                sb.AppendLine($"Space Population: {spacePop:N0}");
            }

            return sb.ToString();
        }

        private List<ISection> GetEconomySections()
        {
            var sections = new List<ISection>();

            // Population section
            var popSection = new DataSection("Population");
            double earthPop = GetTotalPopulation();
            int spacePop = GetSpacePopulation();
            popSection.AddItem("Earth Population", $"{earthPop:F2} billion");
            popSection.AddItem("Space Population", spacePop.ToString("N0"));
            sections.Add(popSection);

            // GDP section
            var gdpSection = new DataSection("Economy");
            double gdp = TIGlobalValuesState.globalGDP;
            double perCapita = earthPop > 0 ? gdp / (earthPop * 1e9) : 0;
            gdpSection.AddItem("Global GDP", $"${gdp / 1e12:F2}T");
            gdpSection.AddItem("Per Capita GDP", $"${perCapita:N0}");
            sections.Add(gdpSection);

            // Space resources note section
            var resourcesSection = new DataSection("Space Resources");
            resourcesSection.AddItem("Note", "Space resources are traded directly between factions");
            sections.Add(resourcesSection);

            return sections;
        }

        private double GetTotalPopulation()
        {
            try
            {
                return GameStateManager.AllExtantNations().Sum(n => n.population_Millions) / 1000.0;
            }
            catch
            {
                return 0;
            }
        }

        private int GetSpacePopulation()
        {
            try
            {
                return GameStateManager.IterateByClass<TIHabState>()
                    .Sum(h => h.crew);
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Wars

        private string GetWarsSummary()
        {
            var wars = GetActiveWars();
            if (wars.Count == 0)
                return "Wars: No active conflicts";
            return $"Wars: {wars.Count} active conflict{(wars.Count != 1 ? "s" : "")}";
        }

        private string GetWarsDetail()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Active Wars");
            sb.AppendLine();

            var wars = GetActiveWars();
            if (wars.Count == 0)
            {
                sb.AppendLine("No active conflicts.");
                return sb.ToString();
            }

            foreach (var war in wars)
            {
                sb.AppendLine($"War: {war.displayName ?? "Unnamed conflict"}");
                if (war.attackingAlliance != null && war.attackingAlliance.Count > 0)
                {
                    var attackers = string.Join(", ", war.attackingAlliance.Select(n => n?.displayName ?? "?"));
                    sb.AppendLine($"  Attackers: {attackers}");
                }
                if (war.defendingAlliance != null && war.defendingAlliance.Count > 0)
                {
                    var defenders = string.Join(", ", war.defendingAlliance.Select(n => n?.displayName ?? "?"));
                    sb.AppendLine($"  Defenders: {defenders}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private List<ISection> GetWarsSections()
        {
            var sections = new List<ISection>();

            var wars = GetActiveWars();
            if (wars.Count == 0)
            {
                var noWarsSection = new DataSection("Wars");
                noWarsSection.AddItem("No active conflicts");
                sections.Add(noWarsSection);
                return sections;
            }

            foreach (var war in wars)
            {
                var warSection = new DataSection(war.displayName ?? "War");

                if (war.attackingAlliance != null && war.attackingAlliance.Count > 0)
                {
                    var attackers = string.Join(", ", war.attackingAlliance.Select(n => n?.displayName ?? "?"));
                    warSection.AddItem("Attackers", attackers);
                }

                if (war.defendingAlliance != null && war.defendingAlliance.Count > 0)
                {
                    var defenders = string.Join(", ", war.defendingAlliance.Select(n => n?.displayName ?? "?"));
                    warSection.AddItem("Defenders", defenders);
                }

                // Duration - calculate from startDate
                try
                {
                    if (war.startDate != null)
                    {
                        double days = TITimeState.Now().DifferenceInDays(war.startDate);
                        warSection.AddItem("Duration", $"{(int)days} days");
                    }
                }
                catch { }

                sections.Add(warSection);
            }

            return sections;
        }

        private List<TIWarState> GetActiveWars()
        {
            try
            {
                var globalValues = TIGlobalValuesState.GlobalValues;
                if (globalValues?.interstateWars == null)
                    return new List<TIWarState>();

                return globalValues.interstateWars.ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting wars: {ex.Message}");
                return new List<TIWarState>();
            }
        }

        #endregion

        #region Atrocities

        private string GetAtrocitiesSummary()
        {
            var factions = GameStateManager.AllHumanFactions();
            int totalAtrocities = factions.Sum(f => f.atrocities);
            if (totalAtrocities == 0)
                return "Atrocities: None committed";
            return $"Atrocities: {totalAtrocities} total across all factions";
        }

        private string GetAtrocitiesDetail()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Faction Atrocities");
            sb.AppendLine();

            var factions = GameStateManager.AllHumanFactions()
                .OrderByDescending(f => f.atrocities);

            foreach (var faction in factions)
            {
                sb.AppendLine($"{faction.displayName}: {faction.atrocities}");
            }

            return sb.ToString();
        }

        private List<ISection> GetAtrocitiesSections()
        {
            var sections = new List<ISection>();

            var atrocitiesSection = new DataSection("Atrocities by Faction");

            var factions = GameStateManager.AllHumanFactions()
                .OrderByDescending(f => f.atrocities);

            foreach (var faction in factions)
            {
                atrocitiesSection.AddItem(faction.displayName, faction.atrocities.ToString());
            }

            sections.Add(atrocitiesSection);
            return sections;
        }

        #endregion
    }
}
