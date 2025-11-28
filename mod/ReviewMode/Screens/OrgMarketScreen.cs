using System;
using System.Collections.Generic;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Org Market screen - browse available orgs, faction pool, and assigned orgs.
    /// Provides actions for purchasing, selling, and transferring organizations.
    /// </summary>
    public class OrgMarketScreen : ScreenBase
    {
        // Item types in the list
        private enum ItemType { MarketDivider, MarketOrg, PoolDivider, PoolOrg, CouncilorDivider, AssignedOrg }
        private class OrgItem
        {
            public ItemType Type;
            public TIOrgState Org;           // For org items
            public TICouncilorState Councilor; // For assigned orgs, which councilor owns it
            public string DividerText;       // For divider items
        }

        private List<OrgItem> items = new List<OrgItem>();

        private readonly OrgReader orgReader = new OrgReader();

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public override string Name => "Organizations";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int market = faction.availableOrgs?.Count ?? 0;
                    int pool = faction.unassignedOrgs?.Count ?? 0;
                    return $"{market} on market, {pool} in pool";
                }
                return "Browse and manage organizations";
            }
        }

        public OrgMarketScreen()
        {
            // Wire up reader callbacks
            orgReader.OnPurchaseOrg = ExecutePurchaseToCouncilor;
            orgReader.OnPurchaseOrgToPool = ExecutePurchaseToPool;
            orgReader.OnAssignOrg = ExecuteAssignToCouncilor;
            orgReader.OnSellOrg = ExecuteSellOrg;
            orgReader.OnMoveToPool = ExecuteMoveToPool;
            orgReader.OnTransferOrg = ExecuteTransferOrg;
        }

        public override void Refresh()
        {
            items.Clear();
            cachedItemIndex = -1;
            cachedSections.Clear();

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return;

                // Market orgs
                if (faction.availableOrgs != null && faction.availableOrgs.Count > 0)
                {
                    items.Add(new OrgItem
                    {
                        Type = ItemType.MarketDivider,
                        DividerText = $"--- Org Market: {faction.availableOrgs.Count} available ---"
                    });

                    foreach (var org in faction.availableOrgs)
                    {
                        items.Add(new OrgItem { Type = ItemType.MarketOrg, Org = org });
                    }
                }

                // Faction pool orgs
                if (faction.unassignedOrgs != null && faction.unassignedOrgs.Count > 0)
                {
                    items.Add(new OrgItem
                    {
                        Type = ItemType.PoolDivider,
                        DividerText = $"--- Faction Pool: {faction.unassignedOrgs.Count} unassigned ---"
                    });

                    foreach (var org in faction.unassignedOrgs)
                    {
                        items.Add(new OrgItem { Type = ItemType.PoolOrg, Org = org });
                    }
                }

                // Assigned orgs by councilor
                if (faction.councilors != null)
                {
                    foreach (var councilor in faction.councilors)
                    {
                        if (councilor.orgs != null && councilor.orgs.Count > 0)
                        {
                            items.Add(new OrgItem
                            {
                                Type = ItemType.CouncilorDivider,
                                Councilor = councilor,
                                DividerText = $"--- {councilor.displayName}'s Organizations: {councilor.orgs.Count} ---"
                            });

                            foreach (var org in councilor.orgs)
                            {
                                items.Add(new OrgItem
                                {
                                    Type = ItemType.AssignedOrg,
                                    Org = org,
                                    Councilor = councilor
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing org market screen: {ex.Message}");
            }
        }

        public override IReadOnlyList<object> GetItems()
        {
            return items.ConvertAll(i => (object)i);
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.MarketDivider:
                case ItemType.PoolDivider:
                case ItemType.CouncilorDivider:
                    return item.DividerText;

                case ItemType.MarketOrg:
                case ItemType.PoolOrg:
                case ItemType.AssignedOrg:
                    return orgReader.ReadSummary(item.Org);

                default:
                    return "Unknown";
            }
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.MarketDivider:
                    return GetMarketStatus();

                case ItemType.PoolDivider:
                    return GetPoolStatus();

                case ItemType.CouncilorDivider:
                    return GetCouncilorOrgStatus(item.Councilor);

                case ItemType.MarketOrg:
                case ItemType.PoolOrg:
                case ItemType.AssignedOrg:
                    return orgReader.ReadDetail(item.Org);

                default:
                    return "Unknown";
            }
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cache if available
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;

            var item = items[index];
            switch (item.Type)
            {
                case ItemType.MarketDivider:
                case ItemType.PoolDivider:
                case ItemType.CouncilorDivider:
                    // Dividers have no sections
                    cachedSections = new List<ISection>();
                    break;

                case ItemType.MarketOrg:
                    cachedSections = orgReader.GetSections(item.Org, OrgContext.Market, null);
                    break;

                case ItemType.PoolOrg:
                    cachedSections = orgReader.GetSections(item.Org, OrgContext.Pool, null);
                    break;

                case ItemType.AssignedOrg:
                    cachedSections = orgReader.GetSections(item.Org, OrgContext.Assigned, item.Councilor);
                    break;

                default:
                    cachedSections = new List<ISection>();
                    break;
            }

            return cachedSections;
        }

        public override bool CanDrillIntoItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;

            // Dividers can't be drilled into
            var item = items[index];
            if (item.Type == ItemType.MarketDivider ||
                item.Type == ItemType.PoolDivider ||
                item.Type == ItemType.CouncilorDivider)
                return false;

            return base.CanDrillIntoItem(index);
        }

        #region Status Methods

        private string GetMarketStatus()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return "No faction data available";

            int count = faction.availableOrgs?.Count ?? 0;
            return $"Org Market: {count} organizations available for purchase.";
        }

        private string GetPoolStatus()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return "No faction data available";

            int count = faction.unassignedOrgs?.Count ?? 0;
            return $"Faction Pool: {count} unassigned organizations. These orgs are owned but not assigned to any councilor.";
        }

        private string GetCouncilorOrgStatus(TICouncilorState councilor)
        {
            if (councilor == null)
                return "Unknown councilor";

            int count = councilor.orgs?.Count ?? 0;
            int capacity = councilor.GetAttribute(CouncilorAttribute.Administration);
            return $"{councilor.displayName} has {count} organizations assigned (Administration: {capacity}).";
        }

        #endregion

        #region Purchase Actions

        private void ExecutePurchaseToPool(TIOrgState org)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || org == null)
                {
                    OnSpeak?.Invoke("Cannot purchase: invalid state", true);
                    return;
                }

                // Check affordability
                if (!faction.CanPurchaseOrg(org))
                {
                    OnSpeak?.Invoke("Cannot afford this organization", true);
                    return;
                }

                string costString = GetPurchaseCostString(org, faction);
                string actionDescription = $"Purchase {org.displayName} to faction pool";
                string details = ConfirmationHelper.FormatCostDetails(costString);

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformPurchase(org, faction, null),
                    onCancel: () => OnSpeak?.Invoke("Purchase cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating pool purchase: {ex.Message}");
                OnSpeak?.Invoke("Error initiating purchase", true);
            }
        }

        private void ExecutePurchaseToCouncilor(TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || org == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot purchase: invalid state", true);
                    return;
                }

                // Check affordability
                if (!faction.CanPurchaseOrg(org))
                {
                    OnSpeak?.Invoke("Cannot afford this organization", true);
                    return;
                }

                string costString = GetPurchaseCostString(org, faction);
                string actionDescription = $"Purchase {org.displayName} for {councilor.displayName}";
                string details = ConfirmationHelper.FormatCostDetails(costString);

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformPurchase(org, faction, councilor),
                    onCancel: () => OnSpeak?.Invoke("Purchase cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating councilor purchase: {ex.Message}");
                OnSpeak?.Invoke("Error initiating purchase", true);
            }
        }

        private void PerformPurchase(TIOrgState org, TIFactionState faction, TICouncilorState councilor)
        {
            try
            {
                // Final affordability check before executing
                if (!faction.CanPurchaseOrg(org))
                {
                    OnSpeak?.Invoke("Cannot afford this organization", true);
                    return;
                }

                var action = new PurchaseOrgAction(org, faction, councilor);
                faction.playerControl.StartAction(action);

                string target = councilor != null ? councilor.displayName : "faction pool";
                string announcement = $"Purchased {org.displayName} for {target}";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error purchasing org: {ex.Message}");
                OnSpeak?.Invoke("Error purchasing organization", true);
            }
        }

        #endregion

        #region Assign Actions

        private void ExecuteAssignToCouncilor(TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || org == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot assign: invalid state", true);
                    return;
                }

                string actionDescription = $"Assign {org.displayName} to {councilor.displayName}";

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    "This will assign the organization from the faction pool",
                    OnEnterSelectionMode,
                    onConfirm: () => PerformAssign(org, faction, councilor),
                    onCancel: () => OnSpeak?.Invoke("Assignment cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating assignment: {ex.Message}");
                OnSpeak?.Invoke("Error initiating assignment", true);
            }
        }

        private void PerformAssign(TIOrgState org, TIFactionState faction, TICouncilorState councilor)
        {
            try
            {
                // Assigning from pool is essentially a purchase with zero cost
                var action = new PurchaseOrgAction(org, faction, councilor);
                faction.playerControl.StartAction(action);

                string announcement = $"Assigned {org.displayName} to {councilor.displayName}";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error assigning org: {ex.Message}");
                OnSpeak?.Invoke("Error assigning organization", true);
            }
        }

        #endregion

        #region Sell Actions

        private void ExecuteSellOrg(TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || org == null)
                {
                    OnSpeak?.Invoke("Cannot sell: invalid state", true);
                    return;
                }

                string salePrice = GetSalePriceString(org);
                string actionDescription = $"Sell {org.displayName}";
                string details = $"Returns: {salePrice}";

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    details,
                    OnEnterSelectionMode,
                    onConfirm: () => PerformSell(org, faction, councilor),
                    onCancel: () => OnSpeak?.Invoke("Sale cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating sale: {ex.Message}");
                OnSpeak?.Invoke("Error initiating sale", true);
            }
        }

        private void PerformSell(TIOrgState org, TIFactionState faction, TICouncilorState councilor)
        {
            try
            {
                var action = new SellOrgAction(org, faction, councilor);
                faction.playerControl.StartAction(action);

                string announcement = $"Sold {org.displayName}";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selling org: {ex.Message}");
                OnSpeak?.Invoke("Error selling organization", true);
            }
        }

        #endregion

        #region Transfer Actions

        private void ExecuteMoveToPool(TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                if (org == null || councilor == null)
                {
                    OnSpeak?.Invoke("Cannot move: invalid state", true);
                    return;
                }

                string actionDescription = $"Move {org.displayName} to faction pool";

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    "This org will be unassigned and moved to the faction pool",
                    OnEnterSelectionMode,
                    onConfirm: () => PerformMoveToPool(org, councilor),
                    onCancel: () => OnSpeak?.Invoke("Move cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating move to pool: {ex.Message}");
                OnSpeak?.Invoke("Error initiating move", true);
            }
        }

        private void PerformMoveToPool(TIOrgState org, TICouncilorState councilor)
        {
            try
            {
                var action = new TransferOrgToFactionPoolAction(org, councilor);
                councilor.faction.playerControl.StartAction(action);

                string announcement = $"Moved {org.displayName} to faction pool";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error moving org to pool: {ex.Message}");
                OnSpeak?.Invoke("Error moving organization", true);
            }
        }

        private void ExecuteTransferOrg(TIOrgState org, TICouncilorState fromCouncilor, TICouncilorState toCouncilor)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null || org == null || fromCouncilor == null || toCouncilor == null)
                {
                    OnSpeak?.Invoke("Cannot transfer: invalid state", true);
                    return;
                }

                string actionDescription = $"Transfer {org.displayName} from {fromCouncilor.displayName} to {toCouncilor.displayName}";

                ConfirmationHelper.RequestConfirmation(
                    actionDescription,
                    "The org will be transferred between councilors",
                    OnEnterSelectionMode,
                    onConfirm: () => PerformTransfer(org, faction, fromCouncilor, toCouncilor),
                    onCancel: () => OnSpeak?.Invoke("Transfer cancelled", true)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initiating transfer: {ex.Message}");
                OnSpeak?.Invoke("Error initiating transfer", true);
            }
        }

        private void PerformTransfer(TIOrgState org, TIFactionState faction, TICouncilorState fromCouncilor, TICouncilorState toCouncilor)
        {
            try
            {
                var action = new TransferOrgToCouncilorAction(org, faction, toCouncilor, fromCouncilor);
                faction.playerControl.StartAction(action);

                string announcement = $"Transferred {org.displayName} from {fromCouncilor.displayName} to {toCouncilor.displayName}";
                OnSpeak?.Invoke(announcement, true);
                MelonLogger.Msg(announcement);

                Refresh();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error transferring org: {ex.Message}");
                OnSpeak?.Invoke("Error transferring organization", true);
            }
        }

        #endregion

        #region Helper Methods

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

            return "Cost unknown";
        }

        private string GetSalePriceString(TIOrgState org)
        {
            try
            {
                var salePrice = org.GetSalePrice();
                return TISpeechMod.CleanText(salePrice.ToString());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting sale price: {ex.Message}");
                return "Price unknown";
            }
        }

        #endregion
    }
}
