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
    /// Resource type for ledger navigation.
    /// </summary>
    public enum LedgerResourceType
    {
        Money,
        Influence,
        Operations,
        Boost,
        MissionControl,
        Research,
        Projects,
        ControlPoints,
        Water,
        Volatiles,
        Metals,
        NobleMetals,
        Fissiles,
        Antimatter,
        Exotics,
        TechBonuses
    }

    /// <summary>
    /// A single contribution to income or cost for a resource.
    /// </summary>
    public class LedgerContribution
    {
        public string SourceName { get; set; }
        public string SourceType { get; set; } // "HQ", "Councilor", "Nation", "Hab", "Fleet", "Org", "Trait", "Module", "Ship", "Transfer"
        public float Value { get; set; }
        public object SourceObject { get; set; }
        public List<LedgerContribution> Children { get; set; } // For expandable sub-items
    }

    /// <summary>
    /// Reader for extracting and formatting ledger data organized by resource type.
    /// </summary>
    public class LedgerReader
    {
        public Action<string, bool> OnSpeak { get; set; }

        #region Resource Type Helpers

        public static readonly LedgerResourceType[] AllResourceTypes = new[]
        {
            LedgerResourceType.Money,
            LedgerResourceType.Influence,
            LedgerResourceType.Operations,
            LedgerResourceType.Boost,
            LedgerResourceType.MissionControl,
            LedgerResourceType.Research,
            LedgerResourceType.Projects,
            LedgerResourceType.ControlPoints,
            LedgerResourceType.Water,
            LedgerResourceType.Volatiles,
            LedgerResourceType.Metals,
            LedgerResourceType.NobleMetals,
            LedgerResourceType.Fissiles,
            LedgerResourceType.Antimatter,
            LedgerResourceType.Exotics,
            LedgerResourceType.TechBonuses
        };

        public static string GetResourceDisplayName(LedgerResourceType type)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return "Money";
                case LedgerResourceType.Influence: return "Influence";
                case LedgerResourceType.Operations: return "Operations";
                case LedgerResourceType.Boost: return "Boost";
                case LedgerResourceType.MissionControl: return "Mission Control";
                case LedgerResourceType.Research: return "Research";
                case LedgerResourceType.Projects: return "Projects";
                case LedgerResourceType.ControlPoints: return "Control Points";
                case LedgerResourceType.Water: return "Water";
                case LedgerResourceType.Volatiles: return "Volatiles";
                case LedgerResourceType.Metals: return "Metals";
                case LedgerResourceType.NobleMetals: return "Noble Metals";
                case LedgerResourceType.Fissiles: return "Fissiles";
                case LedgerResourceType.Antimatter: return "Antimatter";
                case LedgerResourceType.Exotics: return "Exotics";
                case LedgerResourceType.TechBonuses: return "Tech Bonuses";
                default: return type.ToString();
            }
        }

        public static FactionResource? GetFactionResource(LedgerResourceType type)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return FactionResource.Money;
                case LedgerResourceType.Influence: return FactionResource.Influence;
                case LedgerResourceType.Operations: return FactionResource.Operations;
                case LedgerResourceType.Boost: return FactionResource.Boost;
                case LedgerResourceType.MissionControl: return FactionResource.MissionControl;
                case LedgerResourceType.Research: return FactionResource.Research;
                case LedgerResourceType.Projects: return FactionResource.Projects;
                case LedgerResourceType.Water: return FactionResource.Water;
                case LedgerResourceType.Volatiles: return FactionResource.Volatiles;
                case LedgerResourceType.Metals: return FactionResource.Metals;
                case LedgerResourceType.NobleMetals: return FactionResource.NobleMetals;
                case LedgerResourceType.Fissiles: return FactionResource.Fissiles;
                case LedgerResourceType.Antimatter: return FactionResource.Antimatter;
                case LedgerResourceType.Exotics: return FactionResource.Exotics;
                default: return null;
            }
        }

        /// <summary>
        /// Check if this resource type has both income and costs (vs just income or just bonuses).
        /// </summary>
        public static bool HasCosts(LedgerResourceType type)
        {
            switch (type)
            {
                case LedgerResourceType.Operations:
                case LedgerResourceType.Research:
                case LedgerResourceType.Projects:
                case LedgerResourceType.TechBonuses:
                    return false;
                default:
                    return true;
            }
        }

        #endregion

        #region Resource Summary

        public string ReadResourceSummary(LedgerResourceType type, TIFactionState faction)
        {
            if (faction == null)
                return $"{GetResourceDisplayName(type)}: No faction";

            try
            {
                var factionRes = GetFactionResource(type);
                if (type == LedgerResourceType.TechBonuses)
                {
                    return $"Tech Bonuses: Research speed modifiers by category";
                }
                else if (type == LedgerResourceType.ControlPoints)
                {
                    float capacity = faction.GetControlPointMaintenanceFreebieCap();
                    float used = faction.GetBaselineControlPointMaintenanceCost();
                    return $"Control Points: {capacity:F0} free capacity, {used:F0} maintenance cost";
                }
                else if (factionRes.HasValue)
                {
                    float income = faction.GetMonthlyGrossRevenue(factionRes.Value);
                    float cost = faction.GetMonthlyGrossExpenses(factionRes.Value);

                    // Special case: Boost includes substitution for missing space resources
                    if (type == LedgerResourceType.Boost)
                    {
                        float dailyShortage = faction.DailySpaceResourceShortage();
                        float monthlySubstitution = dailyShortage * 30.44f;
                        cost += monthlySubstitution;
                    }

                    float net = income - cost;

                    if (!HasCosts(type))
                    {
                        return $"{GetResourceDisplayName(type)}: +{FormatNumber(income)}/month";
                    }
                    else
                    {
                        string netSign = net >= 0 ? "+" : "";
                        return $"{GetResourceDisplayName(type)}: +{FormatNumber(income)} income, -{FormatNumber(cost)} costs, {netSign}{FormatNumber(net)} net";
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading resource summary for {type}: {ex.Message}");
            }

            return $"{GetResourceDisplayName(type)}";
        }

        public string ReadResourceDetail(LedgerResourceType type, TIFactionState faction)
        {
            if (faction == null)
                return "No faction";

            var sb = new StringBuilder();
            sb.AppendLine($"{GetResourceDisplayName(type)} Breakdown:");

            try
            {
                if (type == LedgerResourceType.TechBonuses)
                {
                    sb.AppendLine($"  Energy: +{faction.SumCategoryModifiers(TechCategory.Energy) * 100:F0}%");
                    sb.AppendLine($"  Materials: +{faction.SumCategoryModifiers(TechCategory.Materials) * 100:F0}%");
                    sb.AppendLine($"  Space Science: +{faction.SumCategoryModifiers(TechCategory.SpaceScience) * 100:F0}%");
                    sb.AppendLine($"  Life Science: +{faction.SumCategoryModifiers(TechCategory.LifeScience) * 100:F0}%");
                    sb.AppendLine($"  Information Science: +{faction.SumCategoryModifiers(TechCategory.InformationScience) * 100:F0}%");
                    sb.AppendLine($"  Military Science: +{faction.SumCategoryModifiers(TechCategory.MilitaryScience) * 100:F0}%");
                    sb.AppendLine($"  Social Science: +{faction.SumCategoryModifiers(TechCategory.SocialScience) * 100:F0}%");
                    sb.AppendLine($"  Xenology: +{faction.SumCategoryModifiers(TechCategory.Xenology) * 100:F0}%");
                }
                else
                {
                    var factionRes = GetFactionResource(type);
                    if (factionRes.HasValue)
                    {
                        float income = faction.GetMonthlyGrossRevenue(factionRes.Value);
                        float cost = faction.GetMonthlyGrossExpenses(factionRes.Value);

                        // Special case: Boost includes substitution for missing space resources
                        float substitution = 0;
                        if (type == LedgerResourceType.Boost)
                        {
                            float dailyShortage = faction.DailySpaceResourceShortage();
                            substitution = dailyShortage * 30.44f;
                            cost += substitution;
                        }

                        float net = income - cost;

                        sb.AppendLine($"  Total Income: +{FormatNumber(income)}/month");
                        if (HasCosts(type))
                        {
                            sb.AppendLine($"  Total Costs: -{FormatNumber(cost)}/month");
                            if (substitution > 0)
                            {
                                sb.AppendLine($"    (includes -{FormatNumber(substitution)} for space resource deficits)");
                            }
                            string netSign = net >= 0 ? "+" : "";
                            sb.AppendLine($"  Net: {netSign}{FormatNumber(net)}/month");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading resource detail for {type}: {ex.Message}");
            }

            return sb.ToString();
        }

        #endregion

        #region Section Building

        public List<ISection> GetSectionsForResource(LedgerResourceType type, TIFactionState faction)
        {
            var sections = new List<ISection>();
            if (faction == null) return sections;

            try
            {
                if (type == LedgerResourceType.TechBonuses)
                {
                    sections.Add(CreateTechBonusesSection(faction));
                }
                else if (type == LedgerResourceType.ControlPoints)
                {
                    sections.Add(CreateControlPointsIncomeSection(faction));
                    sections.Add(CreateControlPointsCostsSection(faction));
                }
                else
                {
                    var incomeSection = CreateIncomeSection(type, faction);
                    if (incomeSection != null)
                        sections.Add(incomeSection);

                    if (HasCosts(type))
                    {
                        var costsSection = CreateCostsSection(type, faction);
                        if (costsSection != null)
                            sections.Add(costsSection);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting sections for {type}: {ex.Message}");
            }

            return sections;
        }

        private ISection CreateIncomeSection(LedgerResourceType type, TIFactionState faction)
        {
            var section = new DataSection("Income");
            var contributions = GetIncomeContributions(type, faction);

            if (contributions.Count == 0)
            {
                section.AddItem("No income sources", "");
                return section;
            }

            float total = contributions.Sum(c => c.Value);
            section.AddItem("Total", $"+{FormatNumber(total)}/month");

            foreach (var contrib in contributions.OrderByDescending(c => c.Value))
            {
                if (contrib.Value == 0) continue;

                string label = $"{contrib.SourceName} ({contrib.SourceType})";
                string value = $"+{FormatNumber(contrib.Value)}";

                if (contrib.Children != null && contrib.Children.Count > 0)
                {
                    // Has expandable children
                    int childCount = contrib.Children.Count(c => c.Value != 0);
                    string childInfo = childCount > 0 ? $", {childCount} sub-items" : "";
                    section.AddDrillableItem(label, $"income:{type}:{contrib.SourceType}:{GetSourceId(contrib.SourceObject)}", $"{value}{childInfo}");
                }
                else
                {
                    section.AddItem(label, value);
                }
            }

            return section;
        }

        private ISection CreateCostsSection(LedgerResourceType type, TIFactionState faction)
        {
            var section = new DataSection("Costs");
            var contributions = GetCostContributions(type, faction);

            if (contributions.Count == 0)
            {
                section.AddItem("No costs", "");
                return section;
            }

            float total = contributions.Sum(c => c.Value);
            section.AddItem("Total", $"-{FormatNumber(total)}/month");

            foreach (var contrib in contributions.OrderByDescending(c => c.Value))
            {
                if (contrib.Value == 0) continue;

                string label = $"{contrib.SourceName} ({contrib.SourceType})";
                string value = $"-{FormatNumber(contrib.Value)}";

                if (contrib.Children != null && contrib.Children.Count > 0)
                {
                    int childCount = contrib.Children.Count(c => c.Value != 0);
                    string childInfo = childCount > 0 ? $", {childCount} sub-items" : "";
                    section.AddDrillableItem(label, $"cost:{type}:{contrib.SourceType}:{GetSourceId(contrib.SourceObject)}", $"{value}{childInfo}");
                }
                else
                {
                    section.AddItem(label, value);
                }
            }

            return section;
        }

        private ISection CreateControlPointsIncomeSection(TIFactionState faction)
        {
            var section = new DataSection("Capacity");
            var contributions = new List<LedgerContribution>();

            // HQ base capacity
            float hqCapacity = (float)TIGlobalValuesState.GlobalValues.controlPointMaintenanceFreebies -
                TIEffectsState.SumEffectsModifiers(Context.ControlPointMaintenance, faction, TIGlobalValuesState.GlobalValues.controlPointMaintenanceFreebies);
            if (hqCapacity > 0)
            {
                contributions.Add(new LedgerContribution { SourceName = "Faction HQ", SourceType = "HQ", Value = hqCapacity });
            }

            // Councilor CP capacity
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    if (councilor.controlPointCapacity > 0)
                    {
                        contributions.Add(new LedgerContribution
                        {
                            SourceName = councilor.displayName,
                            SourceType = "Councilor",
                            Value = councilor.controlPointCapacity,
                            SourceObject = councilor
                        });
                    }
                }
            }

            // Hab CP capacity
            if (faction.habs != null)
            {
                foreach (var hab in faction.habs)
                {
                    if (hab.controlPointCapacityValue > 0)
                    {
                        contributions.Add(new LedgerContribution
                        {
                            SourceName = hab.displayName,
                            SourceType = "Hab",
                            Value = hab.controlPointCapacityValue,
                            SourceObject = hab
                        });
                    }
                }
            }

            float total = contributions.Sum(c => c.Value);
            section.AddItem("Total Free Capacity", $"{total:F0} CPs");

            foreach (var contrib in contributions.OrderByDescending(c => c.Value))
            {
                section.AddItem($"{contrib.SourceName} ({contrib.SourceType})", $"+{contrib.Value:F0}");
            }

            return section;
        }

        private ISection CreateControlPointsCostsSection(TIFactionState faction)
        {
            var section = new DataSection("Maintenance Costs");
            var contributions = new List<LedgerContribution>();

            // Get nations with CPs
            if (faction.controlPoints != null)
            {
                var nationCPs = faction.controlPoints
                    .Where(cp => !cp.benefitsDisabled)
                    .GroupBy(cp => cp.nation);

                foreach (var group in nationCPs)
                {
                    float cost = group.Sum(cp => cp.CurrentMaintenanceCost);
                    if (cost > 0)
                    {
                        contributions.Add(new LedgerContribution
                        {
                            SourceName = group.Key.displayName,
                            SourceType = "Nation",
                            Value = cost,
                            SourceObject = group.Key
                        });
                    }
                }
            }

            float total = contributions.Sum(c => c.Value);
            section.AddItem("Total Maintenance", $"{total:F1} Influence/month");

            foreach (var contrib in contributions.OrderByDescending(c => c.Value))
            {
                section.AddItem($"{contrib.SourceName}", $"-{contrib.Value:F1}");
            }

            return section;
        }

        private ISection CreateTechBonusesSection(TIFactionState faction)
        {
            var section = new DataSection("Research Speed Bonuses");

            var categories = new[]
            {
                (TechCategory.Energy, "Energy"),
                (TechCategory.Materials, "Materials"),
                (TechCategory.SpaceScience, "Space Science"),
                (TechCategory.LifeScience, "Life Science"),
                (TechCategory.InformationScience, "Information Science"),
                (TechCategory.MilitaryScience, "Military Science"),
                (TechCategory.SocialScience, "Social Science"),
                (TechCategory.Xenology, "Xenology")
            };

            foreach (var (cat, name) in categories)
            {
                float bonus = faction.SumCategoryModifiers(cat);
                if (bonus != 0)
                {
                    section.AddDrillableItem(name, $"techbonus:{cat}", $"+{bonus * 100:F0}%");
                }
                else
                {
                    section.AddItem(name, "+0%");
                }
            }

            return section;
        }

        #endregion

        #region Income Contributions

        private List<LedgerContribution> GetIncomeContributions(LedgerResourceType type, TIFactionState faction)
        {
            var contributions = new List<LedgerContribution>();

            try
            {
                // HQ income
                var hqIncome = GetHQIncome(type, faction);
                if (hqIncome > 0)
                {
                    contributions.Add(new LedgerContribution { SourceName = "Faction HQ", SourceType = "HQ", Value = hqIncome });
                }

                // Councilor income (with traits/orgs as children)
                if (faction.councilors != null)
                {
                    foreach (var councilor in faction.councilors)
                    {
                        var councilorContrib = GetCouncilorIncomeContribution(type, councilor);
                        if (councilorContrib != null && councilorContrib.Value > 0)
                        {
                            contributions.Add(councilorContrib);
                        }
                    }
                }

                // Nation income
                if (faction.controlPoints != null)
                {
                    var nations = faction.controlPoints.Select(cp => cp.nation).Distinct();
                    foreach (var nation in nations)
                    {
                        float income = GetNationIncome(type, nation, faction);
                        if (income > 0)
                        {
                            contributions.Add(new LedgerContribution
                            {
                                SourceName = nation.displayName,
                                SourceType = "Nation",
                                Value = income,
                                SourceObject = nation
                            });
                        }
                    }
                }

                // Hab income (with modules as children)
                if (faction.habs != null)
                {
                    foreach (var hab in faction.habs)
                    {
                        var habContrib = GetHabIncomeContribution(type, hab);
                        if (habContrib != null && habContrib.Value > 0)
                        {
                            contributions.Add(habContrib);
                        }
                    }
                }

                // Transfer income
                var transferIncome = GetTransferIncomeContributions(type, faction);
                contributions.AddRange(transferIncome.Where(c => c.Value > 0));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting income contributions for {type}: {ex.Message}");
            }

            return contributions;
        }

        private float GetHQIncome(LedgerResourceType type, TIFactionState faction)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return 0;

            try
            {
                float income = faction.GetMonthlyIncomeFromHQ(factionRes.Value);

                // Add excess MC conversion for money/research
                if (type == LedgerResourceType.Money || type == LedgerResourceType.Research)
                {
                    income += faction.GetMonthlyIncomeFromExcessMissionControl(FactionResource.Money);
                }

                // Add nation influence income to HQ
                if (type == LedgerResourceType.Influence)
                {
                    income += faction.GetMonthlyIncomeFromNations(FactionResource.Influence, includeDeficit: false);
                }

                return income;
            }
            catch
            {
                return 0;
            }
        }

        private LedgerContribution GetCouncilorIncomeContribution(LedgerResourceType type, TICouncilorState councilor)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return null;

            try
            {
                float total = councilor.GetMonthlyIncome_PositiveOnly(factionRes.Value);
                if (total <= 0) return null;

                var children = new List<LedgerContribution>();

                // Trait contributions
                if (councilor.traits != null)
                {
                    foreach (var trait in councilor.traits)
                    {
                        float traitIncome = GetTraitIncome(type, trait);
                        if (traitIncome > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = trait.displayName,
                                SourceType = "Trait",
                                Value = traitIncome,
                                SourceObject = trait
                            });
                        }
                    }
                }

                // Org contributions
                if (councilor.orgs != null)
                {
                    foreach (var org in councilor.orgs)
                    {
                        float orgIncome = GetOrgIncome(type, org);
                        if (orgIncome > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = org.displayName,
                                SourceType = "Org",
                                Value = orgIncome,
                                SourceObject = org
                            });
                        }
                    }
                }

                return new LedgerContribution
                {
                    SourceName = councilor.displayName,
                    SourceType = "Councilor",
                    Value = total,
                    SourceObject = councilor,
                    Children = children.Count > 0 ? children : null
                };
            }
            catch
            {
                return null;
            }
        }

        private float GetTraitIncome(LedgerResourceType type, TITraitTemplate trait)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return trait.incomeMoney > 0 ? trait.incomeMoney : 0;
                case LedgerResourceType.Influence: return trait.incomeInfluence > 0 ? trait.incomeInfluence : 0;
                case LedgerResourceType.Operations: return trait.incomeOps > 0 ? trait.incomeOps : 0;
                case LedgerResourceType.Boost: return trait.incomeBoost > 0 ? trait.incomeBoost : 0;
                case LedgerResourceType.Research: return trait.incomeResearch > 0 ? trait.incomeResearch : 0;
                case LedgerResourceType.Projects: return trait.incomeProjects > 0 ? trait.incomeProjects : 0;
                default: return 0;
            }
        }

        private float GetOrgIncome(LedgerResourceType type, TIOrgState org)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return org.incomeMoney_month > 0 ? org.incomeMoney_month : 0;
                case LedgerResourceType.Influence: return org.incomeInfluence_month > 0 ? org.incomeInfluence_month : 0;
                case LedgerResourceType.Operations: return org.incomeOps_month > 0 ? org.incomeOps_month : 0;
                case LedgerResourceType.Boost: return org.incomeBoost_month > 0 ? org.incomeBoost_month : 0;
                case LedgerResourceType.MissionControl: return org.incomeMissionControl > 0 ? org.incomeMissionControl : 0;
                case LedgerResourceType.Research: return org.incomeResearch_month > 0 ? org.incomeResearch_month : 0;
                default: return 0;
            }
        }

        private float GetNationIncome(LedgerResourceType type, TINationState nation, TIFactionState faction)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return 0;

            try
            {
                var cps = nation.FactionControlPoints(faction, includeDisabled: true, includePermanentAllies: false, includeDefended: true);
                bool allDisabled = cps.All(cp => cp.benefitsDisabled);

                if (type == LedgerResourceType.Research)
                {
                    int cpCount = nation.CountFactionControlPoints(faction, allDisabled, includePermanentAllies: false, includeDefended: true);
                    return nation.GetMonthlyResearchFromControlPoint(faction) * cpCount;
                }
                else
                {
                    return nation.GetMonthlyCouncilResourceShare(faction, factionRes.Value, allDisabled);
                }
            }
            catch
            {
                return 0;
            }
        }

        private LedgerContribution GetHabIncomeContribution(LedgerResourceType type, TIHabState hab)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return null;

            try
            {
                float total = hab.GetMonthlyRevenue_WithAdviser(factionRes.Value);

                // Special handling for Mission Control
                if (type == LedgerResourceType.MissionControl)
                {
                    total = hab.ActiveModules().Where(m => m.moduleTemplate.missionControl > 0).Sum(m => m.moduleTemplate.missionControl);
                }

                if (total <= 0) return null;

                var children = new List<LedgerContribution>();
                var allModules = hab.AllModules();

                if (allModules != null)
                {
                    foreach (var module in allModules.Where(m => m != null))
                    {
                        float moduleIncome = GetModuleIncome(type, module);
                        if (moduleIncome > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = module.displayName,
                                SourceType = "Module",
                                Value = moduleIncome,
                                SourceObject = module
                            });
                        }
                    }
                }

                return new LedgerContribution
                {
                    SourceName = hab.displayName,
                    SourceType = "Hab",
                    Value = total,
                    SourceObject = hab,
                    Children = children.Count > 0 ? children : null
                };
            }
            catch
            {
                return null;
            }
        }

        private float GetModuleIncome(LedgerResourceType type, TIHabModuleState module)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return 0;

            try
            {
                if (type == LedgerResourceType.MissionControl)
                {
                    return module.moduleTemplate.missionControl > 0 ? module.moduleTemplate.missionControl : 0;
                }
                else
                {
                    return module.moduleTemplate.MonthlyResourceRevenue(factionRes.Value, module.hab.location, module.ref_faction);
                }
            }
            catch
            {
                return 0;
            }
        }

        private List<LedgerContribution> GetTransferIncomeContributions(LedgerResourceType type, TIFactionState faction)
        {
            var contributions = new List<LedgerContribution>();
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return contributions;

            try
            {
                var allFactions = GameStateManager.AllHumanFactions();
                foreach (var other in allFactions.Where(f => f != faction))
                {
                    float income = faction.GetMonthlyTransferInFromResourceTransfers(factionRes.Value, other, includeInactives: true);
                    if (income > 0)
                    {
                        contributions.Add(new LedgerContribution
                        {
                            SourceName = $"From {other.displayName}",
                            SourceType = "Transfer",
                            Value = income,
                            SourceObject = other
                        });
                    }
                }
            }
            catch { }

            return contributions;
        }

        #endregion

        #region Cost Contributions

        private List<LedgerContribution> GetCostContributions(LedgerResourceType type, TIFactionState faction)
        {
            var contributions = new List<LedgerContribution>();

            try
            {
                // Special case: Boost substitution for missing space resources
                // The game deducts space resource deficits from boost income, but we show it as a cost for clarity
                if (type == LedgerResourceType.Boost)
                {
                    var substitutionContribs = GetBoostSubstitutionCosts(faction);
                    contributions.AddRange(substitutionContribs.Where(c => c.Value > 0));
                }

                // HQ costs
                var hqCost = GetHQCost(type, faction);
                if (hqCost > 0)
                {
                    contributions.Add(new LedgerContribution { SourceName = "Faction HQ", SourceType = "HQ", Value = hqCost });
                }

                // Unassigned org costs
                var unassignedCost = GetUnassignedOrgsCost(type, faction);
                if (unassignedCost > 0)
                {
                    contributions.Add(new LedgerContribution { SourceName = "Unassigned Orgs", SourceType = "Pool", Value = unassignedCost });
                }

                // Councilor costs (with traits/orgs as children)
                if (faction.councilors != null)
                {
                    foreach (var councilor in faction.councilors)
                    {
                        var councilorContrib = GetCouncilorCostContribution(type, councilor);
                        if (councilorContrib != null && councilorContrib.Value > 0)
                        {
                            contributions.Add(councilorContrib);
                        }
                    }
                }

                // Hab costs (with modules as children)
                if (faction.habs != null)
                {
                    foreach (var hab in faction.habs)
                    {
                        var habContrib = GetHabCostContribution(type, hab);
                        if (habContrib != null && habContrib.Value > 0)
                        {
                            contributions.Add(habContrib);
                        }
                    }
                }

                // Fleet costs (with ships as children)
                var fleets = GameStateManager.IterateByClass<TISpaceFleetState>()
                    .Where(f => f.faction == faction && !f.archived && !f.dummyFleet);

                foreach (var fleet in fleets)
                {
                    var fleetContrib = GetFleetCostContribution(type, fleet);
                    if (fleetContrib != null && fleetContrib.Value > 0)
                    {
                        contributions.Add(fleetContrib);
                    }
                }

                // Transfer costs
                var transferCosts = GetTransferCostContributions(type, faction);
                contributions.AddRange(transferCosts.Where(c => c.Value > 0));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting cost contributions for {type}: {ex.Message}");
            }

            return contributions;
        }

        private float GetHQCost(LedgerResourceType type, TIFactionState faction)
        {
            try
            {
                if (type == LedgerResourceType.Influence)
                {
                    return faction.GetAnnualControlPointMaintenanceCost() / 12f;
                }
                else if (type == LedgerResourceType.MissionControl)
                {
                    return faction.GetMissionControlRequirementFromMineNetwork();
                }
            }
            catch { }

            return 0;
        }

        private float GetUnassignedOrgsCost(LedgerResourceType type, TIFactionState faction)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return 0;

            try
            {
                return -faction.GetNegativeMonthlyIncomeFromUnassignedOrgs(factionRes.Value);
            }
            catch
            {
                return 0;
            }
        }

        private LedgerContribution GetCouncilorCostContribution(LedgerResourceType type, TICouncilorState councilor)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return null;

            try
            {
                float total = councilor.GetMonthlyIncome_NegativeOnly(factionRes.Value, returnPositiveNumber: true);
                if (total <= 0) return null;

                var children = new List<LedgerContribution>();

                // Trait contributions
                if (councilor.traits != null)
                {
                    foreach (var trait in councilor.traits)
                    {
                        float traitCost = GetTraitCost(type, trait);
                        if (traitCost > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = trait.displayName,
                                SourceType = "Trait",
                                Value = traitCost,
                                SourceObject = trait
                            });
                        }
                    }
                }

                // Org contributions
                if (councilor.orgs != null)
                {
                    foreach (var org in councilor.orgs)
                    {
                        float orgCost = GetOrgCost(type, org);
                        if (orgCost > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = org.displayName,
                                SourceType = "Org",
                                Value = orgCost,
                                SourceObject = org
                            });
                        }
                    }
                }

                return new LedgerContribution
                {
                    SourceName = councilor.displayName,
                    SourceType = "Councilor",
                    Value = total,
                    SourceObject = councilor,
                    Children = children.Count > 0 ? children : null
                };
            }
            catch
            {
                return null;
            }
        }

        private float GetTraitCost(LedgerResourceType type, TITraitTemplate trait)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return trait.incomeMoney < 0 ? -trait.incomeMoney : 0;
                case LedgerResourceType.Influence: return trait.incomeInfluence < 0 ? -trait.incomeInfluence : 0;
                case LedgerResourceType.Boost: return trait.incomeBoost < 0 ? -trait.incomeBoost : 0;
                default: return 0;
            }
        }

        private float GetOrgCost(LedgerResourceType type, TIOrgState org)
        {
            switch (type)
            {
                case LedgerResourceType.Money: return org.incomeMoney_month < 0 ? -org.incomeMoney_month : 0;
                case LedgerResourceType.Influence: return org.incomeInfluence_month < 0 ? -org.incomeInfluence_month : 0;
                case LedgerResourceType.Boost: return org.incomeBoost_month < 0 ? -org.incomeBoost_month : 0;
                default: return 0;
            }
        }

        private LedgerContribution GetHabCostContribution(LedgerResourceType type, TIHabState hab)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return null;

            try
            {
                float total = hab.GetMonthlySupportCost(factionRes.Value);

                // Special handling for Mission Control
                if (type == LedgerResourceType.MissionControl)
                {
                    total = Math.Abs(hab.ActiveModules().Where(m => m.moduleTemplate.missionControl < 0).Sum(m => m.moduleTemplate.missionControl));
                }

                if (total <= 0) return null;

                var children = new List<LedgerContribution>();
                var allModules = hab.AllModules();

                if (allModules != null)
                {
                    foreach (var module in allModules.Where(m => m != null))
                    {
                        float moduleCost = GetModuleCost(type, module);
                        if (moduleCost > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = module.displayName,
                                SourceType = "Module",
                                Value = moduleCost,
                                SourceObject = module
                            });
                        }
                    }
                }

                return new LedgerContribution
                {
                    SourceName = hab.displayName,
                    SourceType = "Hab",
                    Value = total,
                    SourceObject = hab,
                    Children = children.Count > 0 ? children : null
                };
            }
            catch
            {
                return null;
            }
        }

        private float GetModuleCost(LedgerResourceType type, TIHabModuleState module)
        {
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return 0;

            try
            {
                if (type == LedgerResourceType.MissionControl)
                {
                    return module.moduleTemplate.missionControl < 0 ? Math.Abs(module.moduleTemplate.missionControl) : 0;
                }
                else
                {
                    return module.moduleTemplate.MonthlySupportCost(factionRes.Value, includeCrewSupportCost: true, module.ref_faction, module.ref_hab);
                }
            }
            catch
            {
                return 0;
            }
        }

        private LedgerContribution GetFleetCostContribution(LedgerResourceType type, TISpaceFleetState fleet)
        {
            try
            {
                float total = 0;

                if (type == LedgerResourceType.Money)
                {
                    total = fleet.ships?.Sum(s => s.GetMonthlyExpenses(FactionResource.Money)) ?? 0;
                }
                else if (type == LedgerResourceType.MissionControl)
                {
                    total = fleet.MissionControlConsumption();
                }

                if (total <= 0) return null;

                var children = new List<LedgerContribution>();

                if (fleet.ships != null)
                {
                    foreach (var ship in fleet.ships)
                    {
                        float shipCost = 0;
                        if (type == LedgerResourceType.Money)
                            shipCost = ship.GetMonthlyExpenses(FactionResource.Money);
                        else if (type == LedgerResourceType.MissionControl)
                            shipCost = ship.missionControlConsumption;

                        if (shipCost > 0)
                        {
                            children.Add(new LedgerContribution
                            {
                                SourceName = ship.displayName,
                                SourceType = "Ship",
                                Value = shipCost,
                                SourceObject = ship
                            });
                        }
                    }
                }

                return new LedgerContribution
                {
                    SourceName = fleet.displayName ?? "Unknown Fleet",
                    SourceType = "Fleet",
                    Value = total,
                    SourceObject = fleet,
                    Children = children.Count > 0 ? children : null
                };
            }
            catch
            {
                return null;
            }
        }

        private List<LedgerContribution> GetTransferCostContributions(LedgerResourceType type, TIFactionState faction)
        {
            var contributions = new List<LedgerContribution>();
            var factionRes = GetFactionResource(type);
            if (!factionRes.HasValue) return contributions;

            try
            {
                var allFactions = GameStateManager.AllHumanFactions();
                foreach (var other in allFactions.Where(f => f != faction))
                {
                    float cost = faction.GetMonthlyTransferOutFromResourceTransfers(factionRes.Value, other, includeInactives: true);
                    if (cost > 0)
                    {
                        contributions.Add(new LedgerContribution
                        {
                            SourceName = $"To {other.displayName}",
                            SourceType = "Transfer",
                            Value = cost,
                            SourceObject = other
                        });
                    }
                }
            }
            catch { }

            return contributions;
        }

        /// <summary>
        /// Calculate boost costs from substituting for missing space resources.
        /// When habs need water/volatiles/metals but don't have enough, boost covers the deficit.
        /// The game treats this as an income reduction, but we show it as a cost for clarity.
        /// </summary>
        private List<LedgerContribution> GetBoostSubstitutionCosts(TIFactionState faction)
        {
            var contributions = new List<LedgerContribution>();

            try
            {
                // Check each space resource for deficits
                var spaceResources = new[]
                {
                    (FactionResource.Water, "Water"),
                    (FactionResource.Volatiles, "Volatiles"),
                    (FactionResource.Metals, "Metals"),
                    (FactionResource.NobleMetals, "Noble Metals"),
                    (FactionResource.Fissiles, "Fissiles"),
                    (FactionResource.Antimatter, "Antimatter"),
                    (FactionResource.Exotics, "Exotics")
                };

                foreach (var (resource, name) in spaceResources)
                {
                    // Calculate daily deficit for this resource
                    // Deficit = consumption - (stockpile + production)
                    float stockpile = faction.GetCurrentResourceAmount(resource);
                    float dailyIncome = faction.GetDailyIncome(resource);
                    float netDaily = stockpile + dailyIncome;

                    if (netDaily < 0)
                    {
                        // This resource has a deficit that boost must cover
                        float dailyDeficit = Math.Abs(netDaily);
                        float monthlyDeficit = dailyDeficit * 30.44f; // Average days per month

                        contributions.Add(new LedgerContribution
                        {
                            SourceName = $"{name} Deficit",
                            SourceType = "Substitution",
                            Value = monthlyDeficit
                        });
                    }
                }

                // If we have any substitution costs, also show the total from the game's calculation
                float totalDailyShortage = faction.DailySpaceResourceShortage();
                if (totalDailyShortage > 0 && contributions.Count == 0)
                {
                    // Fallback: if our per-resource calculation missed something, show the total
                    contributions.Add(new LedgerContribution
                    {
                        SourceName = "Space Resource Deficit",
                        SourceType = "Substitution",
                        Value = totalDailyShortage * 30.44f
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error calculating boost substitution costs: {ex.Message}");
            }

            return contributions;
        }

        #endregion

        #region Drill-Down Support

        public List<ISection> GetSectionsForDrillableItem(string secondaryId, TIFactionState faction)
        {
            var sections = new List<ISection>();
            if (string.IsNullOrEmpty(secondaryId)) return sections;

            try
            {
                var parts = secondaryId.Split(':');
                if (parts.Length < 4)
                {
                    // Handle tech bonus drill-down
                    if (parts.Length >= 2 && parts[0] == "techbonus")
                    {
                        if (Enum.TryParse<TechCategory>(parts[1], out var techCat))
                        {
                            sections.Add(CreateTechBonusDetailSection(techCat, faction));
                        }
                    }
                    return sections;
                }

                string incomeOrCost = parts[0]; // "income" or "cost"
                var resourceType = (LedgerResourceType)Enum.Parse(typeof(LedgerResourceType), parts[1]);
                string sourceType = parts[2];
                string idPart = parts[3];

                if (!int.TryParse(idPart, out int sourceId)) return sections;

                switch (sourceType)
                {
                    case "Councilor":
                        var councilor = GameStateManager.FindGameState<TICouncilorState>(sourceId, allowChild: true);
                        if (councilor != null)
                        {
                            sections.Add(CreateCouncilorBreakdownSection(resourceType, councilor, incomeOrCost == "income"));
                        }
                        break;

                    case "Hab":
                        var hab = GameStateManager.FindGameState<TIHabState>(sourceId, allowChild: true);
                        if (hab != null)
                        {
                            sections.Add(CreateHabBreakdownSection(resourceType, hab, incomeOrCost == "income"));
                        }
                        break;

                    case "Fleet":
                        var fleet = GameStateManager.FindGameState<TISpaceFleetState>(sourceId, allowChild: true);
                        if (fleet != null)
                        {
                            sections.Add(CreateFleetBreakdownSection(resourceType, fleet));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting drill-down sections for {secondaryId}: {ex.Message}");
            }

            return sections;
        }

        private ISection CreateCouncilorBreakdownSection(LedgerResourceType type, TICouncilorState councilor, bool isIncome)
        {
            var section = new DataSection($"{councilor.displayName} - {(isIncome ? "Income" : "Costs")}");

            // Traits
            if (councilor.traits != null)
            {
                foreach (var trait in councilor.traits)
                {
                    float value = isIncome ? GetTraitIncome(type, trait) : GetTraitCost(type, trait);
                    if (value > 0)
                    {
                        string sign = isIncome ? "+" : "-";
                        section.AddItem($"{trait.displayName} (Trait)", $"{sign}{FormatNumber(value)}");
                    }
                }
            }

            // Orgs
            if (councilor.orgs != null)
            {
                foreach (var org in councilor.orgs)
                {
                    float value = isIncome ? GetOrgIncome(type, org) : GetOrgCost(type, org);
                    if (value > 0)
                    {
                        string sign = isIncome ? "+" : "-";
                        section.AddItem($"{org.displayName} (Org)", $"{sign}{FormatNumber(value)}");
                    }
                }
            }

            return section;
        }

        private ISection CreateHabBreakdownSection(LedgerResourceType type, TIHabState hab, bool isIncome)
        {
            var section = new DataSection($"{hab.displayName} - {(isIncome ? "Income" : "Costs")}");

            var allModules = hab.AllModules();
            if (allModules != null)
            {
                foreach (var module in allModules.Where(m => m != null))
                {
                    float value = isIncome ? GetModuleIncome(type, module) : GetModuleCost(type, module);
                    if (value > 0)
                    {
                        string sign = isIncome ? "+" : "-";
                        section.AddItem(module.displayName, $"{sign}{FormatNumber(value)}");
                    }
                }
            }

            return section;
        }

        private ISection CreateFleetBreakdownSection(LedgerResourceType type, TISpaceFleetState fleet)
        {
            var section = new DataSection($"{fleet.displayName} - Costs");

            if (fleet.ships != null)
            {
                foreach (var ship in fleet.ships)
                {
                    float cost = 0;
                    if (type == LedgerResourceType.Money)
                        cost = ship.GetMonthlyExpenses(FactionResource.Money);
                    else if (type == LedgerResourceType.MissionControl)
                        cost = ship.missionControlConsumption;

                    if (cost > 0)
                    {
                        section.AddItem(ship.displayName, $"-{FormatNumber(cost)}");
                    }
                }
            }

            return section;
        }

        private ISection CreateTechBonusDetailSection(TechCategory category, TIFactionState faction)
        {
            string catName = category.ToString();
            var section = new DataSection($"{catName} Research Bonus Sources");

            // Councilors
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    float bonus = councilor.TotalTechBonus(category, activeOrgsOnly: false);
                    if (bonus > 0)
                    {
                        section.AddItem($"{councilor.displayName} (Councilor)", $"+{bonus * 100:F0}%");
                    }
                }
            }

            // Habs
            if (faction.habs != null)
            {
                foreach (var hab in faction.habs)
                {
                    float bonus = hab.GetNetTechBonusByFaction(category, faction, includeInactives: false);
                    if (bonus > 0)
                    {
                        section.AddItem($"{hab.displayName} (Hab)", $"+{bonus * 100:F0}%");
                    }
                }
            }

            // Xenology investigations bonus
            if (category == TechCategory.Xenology)
            {
                float invBonus = faction.InvestigationsModifier(TechCategory.Xenology);
                if (invBonus > 0)
                {
                    section.AddItem("Alien Investigations", $"+{invBonus * 100:F0}%");
                }
            }

            return section;
        }

        #endregion

        #region Helpers

        private string GetSourceId(object source)
        {
            if (source == null) return "0";

            if (source is TICouncilorState c) return c.ID.ToString();
            if (source is TINationState n) return n.ID.ToString();
            if (source is TIHabState h) return h.ID.ToString();
            if (source is TISpaceFleetState f) return f.ID.ToString();
            if (source is TIFactionState fa) return fa.ID.ToString();

            return "0";
        }

        private string FormatNumber(float value)
        {
            float abs = Math.Abs(value);
            if (abs >= 1_000_000_000)
                return $"{value / 1_000_000_000:F1}B";
            if (abs >= 1_000_000)
                return $"{value / 1_000_000:F1}M";
            if (abs >= 1_000)
                return $"{value / 1_000:F1}K";
            if (abs >= 1)
                return $"{value:F0}";
            return $"{value:F1}";
        }

        #endregion
    }
}
