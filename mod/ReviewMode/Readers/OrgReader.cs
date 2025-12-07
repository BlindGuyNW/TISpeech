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

            try
            {
                // Use the game's built-in description method which includes:
                // - Name, tier, home region
                // - Required/prohibited traits
                // - Why you can't purchase (ideology, nation requirements, etc.)
                // - Missions granted
                // - All income bonuses
                // - All stat bonuses
                // - All priority bonuses
                // - Tech category bonuses
                // - Sale price if owned
                var faction = GameControl.control?.activePlayer;
                string description = org.description(includeDisplayName: true, faction);
                return TISpeechMod.CleanText(description);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting org description: {ex.Message}");
                // Fallback to basic info
                return $"{org.displayName}, Tier {org.tier}";
            }
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

            var faction = GameControl.control?.activePlayer;

            // Full Description section - uses the game's built-in tooltip text
            var descSection = new DataSection("Description");
            try
            {
                string fullDescription = org.description(includeDisplayName: false, faction);
                string cleanDescription = TISpeechMod.CleanText(fullDescription);

                // Split into manageable items for navigation
                var lines = cleanDescription.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        descSection.AddItem(trimmed);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building org description section: {ex.Message}");
                descSection.AddItem("Description unavailable");
            }

            if (descSection.ItemCount > 0)
            {
                sections.Add(descSection);
            }

            // Info section - basic info for quick reference
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

            // Actions section (context-dependent)
            var actionsSection = BuildActionsSection(org, context, assignedCouncilor);
            if (actionsSection.ItemCount > 0)
            {
                sections.Add(actionsSection);
            }

            return sections;
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
            string costString = GetPurchaseCostString(org, faction);

            // Check if faction can afford this org
            if (!faction.CanPurchaseOrg(org))
            {
                actionsSection.AddItem("Cannot Purchase", "Insufficient resources");
                actionsSection.AddItem("Cost", costString);
                return;
            }

            // Check if org is eligible for this faction at all
            if (!org.IsEligibleForFaction(faction))
            {
                // The description method includes the reason, but let's be explicit here too
                actionsSection.AddItem("Not Available", "This organization is not available to your faction");
                return;
            }

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
    }
}
