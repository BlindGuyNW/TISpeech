using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Context indicating where an org is located (affects available actions).
    /// </summary>
    public enum OrgContext
    {
        Market,    // Available on the org market
        Pool,      // In faction pool (owned but unassigned)
        Assigned   // Assigned to a councilor
    }

    /// <summary>
    /// Reader for TIOrgState objects.
    /// Extracts and formats organization information for accessibility.
    /// </summary>
    public class OrgReader : IGameStateReader<TIOrgState>
    {
        /// <summary>
        /// Callback for purchasing an org to a councilor.
        /// </summary>
        public Action<TIOrgState, TICouncilorState> OnPurchaseOrg { get; set; }

        /// <summary>
        /// Callback for purchasing an org to faction pool.
        /// </summary>
        public Action<TIOrgState> OnPurchaseOrgToPool { get; set; }

        /// <summary>
        /// Callback for assigning a pool org to a councilor.
        /// </summary>
        public Action<TIOrgState, TICouncilorState> OnAssignOrg { get; set; }

        /// <summary>
        /// Callback for selling an org back to market.
        /// </summary>
        public Action<TIOrgState, TICouncilorState> OnSellOrg { get; set; }

        /// <summary>
        /// Callback for moving an org from councilor to faction pool.
        /// </summary>
        public Action<TIOrgState, TICouncilorState> OnMoveToPool { get; set; }

        /// <summary>
        /// Callback for transferring an org between councilors.
        /// </summary>
        public Action<TIOrgState, TICouncilorState, TICouncilorState> OnTransferOrg { get; set; }

        public string ReadSummary(TIOrgState org)
        {
            if (org == null)
                return "Unknown organization";

            var sb = new StringBuilder();
            sb.Append(org.displayName ?? "Unknown");
            sb.Append($", Tier {org.tier}");

            if (org.template != null)
            {
                sb.Append($", {org.template.orgType}");
            }

            return sb.ToString();
        }

        public string ReadDetail(TIOrgState org)
        {
            if (org == null)
                return "Unknown organization";

            var sb = new StringBuilder();
            sb.AppendLine($"Organization: {org.displayName}");
            sb.AppendLine($"Tier: {org.tier}");

            if (org.template != null)
            {
                sb.AppendLine($"Type: {org.template.orgType}");
            }

            if (org.homeRegion != null)
            {
                sb.AppendLine($"Home Region: {org.homeRegion.displayName}");
            }

            // Income
            sb.AppendLine("Monthly Income:");
            if (org.incomeMoney_month != 0) sb.AppendLine($"  Money: {org.incomeMoney_month:+#;-#;0}");
            if (org.incomeInfluence_month != 0) sb.AppendLine($"  Influence: {org.incomeInfluence_month:+#;-#;0}");
            if (org.incomeOps_month != 0) sb.AppendLine($"  Operations: {org.incomeOps_month:+#;-#;0}");
            if (org.incomeBoost_month != 0) sb.AppendLine($"  Boost: {org.incomeBoost_month:+#;-#;0}");
            if (org.incomeResearch_month != 0) sb.AppendLine($"  Research: {org.incomeResearch_month:+#;-#;0}");
            if (org.incomeMissionControl != 0) sb.AppendLine($"  Mission Control: {org.incomeMissionControl:+#;-#;0}");

            // Stat bonuses
            sb.AppendLine("Stat Bonuses:");
            if (org.persuasion != 0) sb.AppendLine($"  Persuasion: {org.persuasion:+#;-#;0}");
            if (org.command != 0) sb.AppendLine($"  Command: {org.command:+#;-#;0}");
            if (org.investigation != 0) sb.AppendLine($"  Investigation: {org.investigation:+#;-#;0}");
            if (org.espionage != 0) sb.AppendLine($"  Espionage: {org.espionage:+#;-#;0}");
            if (org.administration != 0) sb.AppendLine($"  Administration: {org.administration:+#;-#;0}");
            if (org.science != 0) sb.AppendLine($"  Science: {org.science:+#;-#;0}");
            if (org.security != 0) sb.AppendLine($"  Security: {org.security:+#;-#;0}");

            return sb.ToString();
        }

        public List<ISection> GetSections(TIOrgState org)
        {
            // Default to market context
            return GetSections(org, OrgContext.Market, null);
        }

        /// <summary>
        /// Get sections for an org with context-aware actions.
        /// </summary>
        public List<ISection> GetSections(TIOrgState org, OrgContext context, TICouncilorState assignedCouncilor = null)
        {
            var sections = new List<ISection>();

            if (org == null)
                return sections;

            // Info section
            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", org.displayName);
            infoSection.AddItem("Tier", org.tier.ToString());

            if (org.template != null)
            {
                infoSection.AddItem("Type", org.template.orgType.ToString());
            }

            if (org.homeRegion != null)
            {
                infoSection.AddItem("Home Region", org.homeRegion.displayName);
            }

            sections.Add(infoSection);

            // Bonuses section
            var bonusSection = BuildBonusSection(org);
            if (bonusSection.ItemCount > 0)
            {
                sections.Add(bonusSection);
            }

            // Actions section (context-dependent)
            var actionsSection = BuildActionsSection(org, context, assignedCouncilor);
            if (actionsSection.ItemCount > 0)
            {
                sections.Add(actionsSection);
            }

            return sections;
        }

        private DataSection BuildBonusSection(TIOrgState org)
        {
            var bonusSection = new DataSection("Bonuses");

            // Income bonuses
            if (org.incomeMoney_month != 0)
                bonusSection.AddItem("Money/month", FormatBonus(org.incomeMoney_month));
            if (org.incomeInfluence_month != 0)
                bonusSection.AddItem("Influence/month", FormatBonus(org.incomeInfluence_month));
            if (org.incomeOps_month != 0)
                bonusSection.AddItem("Operations/month", FormatBonus(org.incomeOps_month));
            if (org.incomeBoost_month != 0)
                bonusSection.AddItem("Boost/month", FormatBonus(org.incomeBoost_month));
            if (org.incomeResearch_month != 0)
                bonusSection.AddItem("Research/month", FormatBonus(org.incomeResearch_month));
            if (org.incomeMissionControl != 0)
                bonusSection.AddItem("Mission Control", FormatBonus(org.incomeMissionControl));

            // Stat bonuses
            if (org.persuasion != 0)
                bonusSection.AddItem("Persuasion", FormatBonus(org.persuasion));
            if (org.command != 0)
                bonusSection.AddItem("Command", FormatBonus(org.command));
            if (org.investigation != 0)
                bonusSection.AddItem("Investigation", FormatBonus(org.investigation));
            if (org.espionage != 0)
                bonusSection.AddItem("Espionage", FormatBonus(org.espionage));
            if (org.administration != 0)
                bonusSection.AddItem("Administration", FormatBonus(org.administration));
            if (org.science != 0)
                bonusSection.AddItem("Science", FormatBonus(org.science));
            if (org.security != 0)
                bonusSection.AddItem("Security", FormatBonus(org.security));

            // Other bonuses
            if (org.economyBonus != 0)
                bonusSection.AddItem("Economy Bonus", FormatPercentBonus(org.economyBonus));
            if (org.welfareBonus != 0)
                bonusSection.AddItem("Welfare Bonus", FormatPercentBonus(org.welfareBonus));
            if (org.militaryBonus != 0)
                bonusSection.AddItem("Military Bonus", FormatPercentBonus(org.militaryBonus));
            if (org.knowledgeBonus != 0)
                bonusSection.AddItem("Knowledge Bonus", FormatPercentBonus(org.knowledgeBonus));
            if (org.unityBonus != 0)
                bonusSection.AddItem("Unity Bonus", FormatPercentBonus(org.unityBonus));
            if (org.governmentBonus != 0)
                bonusSection.AddItem("Government Bonus", FormatPercentBonus(org.governmentBonus));
            if (org.spoilsBonus != 0)
                bonusSection.AddItem("Spoils Bonus", FormatPercentBonus(org.spoilsBonus));
            if (org.miningBonus != 0)
                bonusSection.AddItem("Mining Bonus", FormatPercentBonus(org.miningBonus));
            if (org.spaceflightBonus != 0)
                bonusSection.AddItem("Spaceflight Bonus", FormatPercentBonus(org.spaceflightBonus));
            if (org.XPModifier != 0)
                bonusSection.AddItem("XP Modifier", FormatPercentBonus(org.XPModifier));

            if (org.projectCapacityGranted > 0)
                bonusSection.AddItem("Project Slots", $"+{org.projectCapacityGranted}");

            return bonusSection;
        }

        private DataSection BuildActionsSection(TIOrgState org, OrgContext context, TICouncilorState assignedCouncilor)
        {
            var actionsSection = new DataSection("Actions");
            var faction = GameControl.control?.activePlayer;

            if (faction == null)
                return actionsSection;

            try
            {
                switch (context)
                {
                    case OrgContext.Market:
                        BuildMarketActions(actionsSection, org, faction);
                        break;

                    case OrgContext.Pool:
                        BuildPoolActions(actionsSection, org, faction);
                        break;

                    case OrgContext.Assigned:
                        BuildAssignedActions(actionsSection, org, faction, assignedCouncilor);
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building org actions: {ex.Message}");
            }

            return actionsSection;
        }

        private void BuildMarketActions(DataSection actionsSection, TIOrgState org, TIFactionState faction)
        {
            // Check if faction can afford this org - don't show purchase options if not affordable
            if (!faction.CanPurchaseOrg(org))
            {
                return; // No actions available - user can still browse org info/bonuses
            }

            string costString = GetPurchaseCostString(org, faction);

            // Purchase to Pool
            var orgCopy = org;
            actionsSection.AddItem("Purchase to Pool", costString, onActivate: () =>
            {
                OnPurchaseOrgToPool?.Invoke(orgCopy);
            });

            // Purchase to each eligible councilor
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    try
                    {
                        if (org.IsEligibleForCouncilor(councilor) && councilor.SufficientCapacityForOrg(org))
                        {
                            var councilorCopy = councilor;
                            actionsSection.AddItem($"Purchase to {councilor.displayName}", costString, onActivate: () =>
                            {
                                OnPurchaseOrg?.Invoke(orgCopy, councilorCopy);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking org eligibility for {councilor.displayName}: {ex.Message}");
                    }
                }
            }
        }

        private void BuildPoolActions(DataSection actionsSection, TIOrgState org, TIFactionState faction)
        {
            var orgCopy = org;

            // Assign to each eligible councilor
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    try
                    {
                        if (org.IsEligibleForCouncilor(councilor) && councilor.SufficientCapacityForOrg(org))
                        {
                            var councilorCopy = councilor;
                            actionsSection.AddItem($"Assign to {councilor.displayName}", onActivate: () =>
                            {
                                OnAssignOrg?.Invoke(orgCopy, councilorCopy);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking org eligibility for {councilor.displayName}: {ex.Message}");
                    }
                }
            }

            // Sell back to market
            string salePrice = GetSalePriceString(org);
            actionsSection.AddItem("Sell to Market", salePrice, onActivate: () =>
            {
                OnSellOrg?.Invoke(orgCopy, null);
            });
        }

        private void BuildAssignedActions(DataSection actionsSection, TIOrgState org, TIFactionState faction, TICouncilorState assignedCouncilor)
        {
            var orgCopy = org;
            var councilorCopy = assignedCouncilor;

            // Move to faction pool
            actionsSection.AddItem("Move to Faction Pool", onActivate: () =>
            {
                OnMoveToPool?.Invoke(orgCopy, councilorCopy);
            });

            // Transfer to other eligible councilors
            if (faction.councilors != null)
            {
                foreach (var councilor in faction.councilors)
                {
                    if (councilor == assignedCouncilor)
                        continue; // Skip current councilor

                    try
                    {
                        if (org.IsEligibleForCouncilor(councilor) && councilor.SufficientCapacityForOrg(org))
                        {
                            var targetCouncilor = councilor;
                            actionsSection.AddItem($"Transfer to {councilor.displayName}", onActivate: () =>
                            {
                                OnTransferOrg?.Invoke(orgCopy, councilorCopy, targetCouncilor);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking transfer eligibility for {councilor.displayName}: {ex.Message}");
                    }
                }
            }

            // Sell back to market
            string salePrice = GetSalePriceString(org);
            actionsSection.AddItem("Sell to Market", salePrice, onActivate: () =>
            {
                OnSellOrg?.Invoke(orgCopy, councilorCopy);
            });
        }

        private string GetPurchaseCostString(TIOrgState org, TIFactionState faction)
        {
            try
            {
                var cost = org.GetPurchaseCost(faction);
                if (cost != null)
                {
                    return TISpeechMod.CleanText(cost.ToString());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting purchase cost: {ex.Message}");
            }

            // Fallback to basic costs
            var sb = new StringBuilder();
            if (org.costMoney > 0) sb.Append($"${org.costMoney:N0} ");
            if (org.costInfluence > 0) sb.Append($"{org.costInfluence:N0} Influence ");
            if (org.costOps > 0) sb.Append($"{org.costOps:N0} Ops ");
            if (org.costBoost > 0) sb.Append($"{org.costBoost:N0} Boost ");
            return sb.ToString().Trim();
        }

        private string GetSalePriceString(TIOrgState org)
        {
            try
            {
                var salePrice = org.GetSalePrice();
                return $"Returns {TISpeechMod.CleanText(salePrice.ToString())}";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting sale price: {ex.Message}");
                return "Sale price unknown";
            }
        }

        private string FormatBonus(float value)
        {
            if (value > 0)
                return $"+{value:N0}";
            else
                return $"{value:N0}";
        }

        private string FormatBonus(int value)
        {
            if (value > 0)
                return $"+{value}";
            else
                return $"{value}";
        }

        private string FormatPercentBonus(float value)
        {
            if (value > 0)
                return $"+{value:P0}";
            else
                return $"{value:P0}";
        }
    }
}
