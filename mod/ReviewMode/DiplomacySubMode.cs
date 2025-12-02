using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Simple mode for the diplomacy greeting screen (before trade negotiations).
    /// Has navigable content (title, headline, body) and a Continue button.
    /// </summary>
    public class DiplomacyGreetingMode
    {
        public string Title { get; private set; }
        public string Headline { get; private set; }
        public string Body { get; private set; }
        public Button ContinueButton { get; private set; }

        private int currentIndex = 0;
        private List<string> items = new List<string>();

        public int CurrentIndex => currentIndex;
        public int Count => items.Count;
        public string CurrentItem => items.Count > 0 && currentIndex >= 0 && currentIndex < items.Count
            ? items[currentIndex]
            : "";

        public DiplomacyGreetingMode(NotificationScreenController controller)
        {
            try
            {
                // Extract text fields
                Title = TISpeechMod.CleanText(controller.factionDiplomacyGreetingTitleText?.text ?? "");
                Headline = TISpeechMod.CleanText(controller.factionDiplomacyGreetingHeadlineText?.text ?? "");
                Body = TISpeechMod.CleanText(controller.factionDiplomacyGreetingBodyText?.text ?? "");
                ContinueButton = controller.factionDiplomacyGreetingContinueButton;

                // Build navigable items
                if (!string.IsNullOrEmpty(Title))
                    items.Add($"Title: {Title}");
                if (!string.IsNullOrEmpty(Headline))
                    items.Add($"Message: {Headline}");
                if (!string.IsNullOrEmpty(Body))
                    items.Add($"Details: {Body}");
                if (ContinueButton != null)
                    items.Add("Continue to Trade");

                MelonLogger.Msg($"DiplomacyGreetingMode created with {items.Count} items");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating DiplomacyGreetingMode: {ex.Message}");
            }
        }

        public void Next()
        {
            if (items.Count == 0) return;
            currentIndex = (currentIndex + 1) % items.Count;
        }

        public void Previous()
        {
            if (items.Count == 0) return;
            currentIndex--;
            if (currentIndex < 0) currentIndex = items.Count - 1;
        }

        public bool Activate()
        {
            // If on the Continue button, click it
            if (currentIndex == items.Count - 1 && ContinueButton != null)
            {
                ContinueButton.onClick.Invoke();
                return true; // Signal to exit greeting mode
            }
            return false;
        }

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append("Diplomacy greeting. ");

            if (!string.IsNullOrEmpty(Title))
            {
                sb.Append(Title);
                sb.Append(". ");
            }

            if (!string.IsNullOrEmpty(Headline))
            {
                sb.Append(Headline);
                sb.Append(". ");
            }

            sb.Append(items.Count);
            sb.Append(" items. Navigate with Up/Down, Enter to continue.");

            return sb.ToString();
        }

        public string GetCurrentAnnouncement()
        {
            if (items.Count == 0) return "No items";
            return $"{currentIndex + 1} of {items.Count}: {CurrentItem}";
        }

        public string GetFullContent()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Title))
            {
                sb.Append("Title: ");
                sb.Append(Title);
                sb.Append(". ");
            }
            if (!string.IsNullOrEmpty(Headline))
            {
                sb.Append("Message: ");
                sb.Append(Headline);
                sb.Append(". ");
            }
            if (!string.IsNullOrEmpty(Body))
            {
                sb.Append("Details: ");
                sb.Append(Body);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Types of items in the diplomacy UI.
    /// </summary>
    public enum DiplomacyItemType
    {
        Information,    // Read-only status text
        Tab,            // Expand/collapse toggle (+/-)
        Resource,       // Faction resources (Money, Influence, etc.)
        Org,            // Organization
        Hab,            // Habitat
        Project,        // Completed project
        Treaty,         // Truce, NAP, Intel Sharing
        IntelExchange,  // Intel exchange option
        HateReduction,  // Improve relations (AI side only)
        Action          // Execute Trade, Clear, Cancel
    }

    /// <summary>
    /// Represents a single navigable option in diplomacy mode.
    /// </summary>
    public class DiplomacyOption
    {
        public string Label { get; set; }
        public string DetailText { get; set; }
        public DiplomacyItemType ItemType { get; set; }
        public bool IsInformational { get; set; }        // Can't be activated
        public bool IsInTable { get; set; }              // In deal table vs bank
        public bool IsPlayerSide { get; set; }           // Player's side vs AI's side
        public bool IsExpanded { get; set; }             // For Tab items - current expand state

        // References for actions
        public DiplomacyBankListItem BankItem { get; set; }
        public DiplomacyTableListItem TableItem { get; set; }
        public Button ActionButton { get; set; }         // For action items
        public FactionResource Resource { get; set; }    // For resource items
        public int CurrentQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public TradeItemType TradeType { get; set; }     // For toggling the right tab
    }

    /// <summary>
    /// Represents a section in the diplomacy navigation.
    /// </summary>
    public class DiplomacySection
    {
        public string Name { get; set; }
        public List<DiplomacyOption> Options { get; set; } = new List<DiplomacyOption>();
    }

    /// <summary>
    /// Sub-mode for navigating the diplomacy/trade screen.
    /// Provides keyboard-only access to trade negotiations.
    /// Uses drill-down pattern: Navigate sections with 8/2, Enter to drill in, Escape to back out.
    /// </summary>
    public class DiplomacySubMode
    {
        // State
        private DiplomacyController controller;
        private List<DiplomacySection> sections = new List<DiplomacySection>();
        private int currentSectionIndex = 0;
        private int currentItemIndex = 0;
        private bool isDrilledIntoSection = false;  // True when navigating items, false when navigating sections

        // Quantity input state - explicit mode like delta-V planner
        private bool isEnteringQuantity = false;
        private string quantityInput = "";

        // Track the target for quantity input (bank item for adding, table item for modifying)
        private DiplomacyBankListItem pendingBankItem = null;
        private DiplomacyTableListItem pendingTableItem = null;
        private FactionResource pendingResource;
        private int pendingMaxQuantity = 0;

        public int SectionCount => sections.Count;
        public int CurrentSectionItemCount => CurrentSection?.Options.Count ?? 0;
        public bool IsDrilledIn => isDrilledIntoSection;
        public bool IsEnteringQuantity => isEnteringQuantity;
        public string QuantityInput => quantityInput;

        public DiplomacySection CurrentSection => sections.Count > 0 && currentSectionIndex >= 0 && currentSectionIndex < sections.Count
            ? sections[currentSectionIndex]
            : null;
        public DiplomacyOption CurrentOption
        {
            get
            {
                if (!isDrilledIntoSection) return null;
                var section = CurrentSection;
                if (section == null || section.Options.Count == 0) return null;
                if (currentItemIndex < 0 || currentItemIndex >= section.Options.Count) return null;
                return section.Options[currentItemIndex];
            }
        }

        // Kept for backwards compatibility but now checks isEnteringQuantity
        public bool IsTypingQuantity => isEnteringQuantity && !string.IsNullOrEmpty(quantityInput);
        public string TypedQuantity => quantityInput;

        /// <summary>
        /// Create diplomacy mode from a DiplomacyController.
        /// </summary>
        public DiplomacySubMode(DiplomacyController controller)
        {
            this.controller = controller;
            BuildSections();
            MelonLogger.Msg($"DiplomacySubMode created with {sections.Count} sections");
        }

        #region Section Building

        private void BuildSections()
        {
            sections.Clear();

            try
            {
                // Section 1: Trade Status (read-only)
                BuildTradeStatusSection();

                // Section 2: Your Offer (items in your table)
                BuildYourOfferSection();

                // Section 3: Add to Your Offer (your bank)
                BuildYourBankSection();

                // Section 4: Their Offer (items in their table)
                BuildTheirOfferSection();

                // Section 5: Request from Them (their bank)
                BuildTheirBankSection();

                // Section 6: Actions
                BuildActionsSection();

                MelonLogger.Msg($"Built {sections.Count} diplomacy sections");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building diplomacy sections: {ex.Message}");
            }
        }

        private void BuildTradeStatusSection()
        {
            var section = new DiplomacySection { Name = "Trade Status" };

            // Trading faction name
            if (controller.aiFactionText != null && !string.IsNullOrEmpty(controller.aiFactionText.text))
            {
                section.Options.Add(new DiplomacyOption
                {
                    Label = $"Trading with: {TISpeechMod.CleanText(controller.aiFactionText.text)}",
                    DetailText = "The faction you are negotiating with",
                    ItemType = DiplomacyItemType.Information,
                    IsInformational = true
                });
            }

            // Attitude
            if (controller.aiFactionAttitudeText != null && !string.IsNullOrEmpty(controller.aiFactionAttitudeText.text))
            {
                section.Options.Add(new DiplomacyOption
                {
                    Label = $"Attitude: {TISpeechMod.CleanText(controller.aiFactionAttitudeText.text)}",
                    DetailText = "Their current stance towards you",
                    ItemType = DiplomacyItemType.Information,
                    IsInformational = true
                });
            }

            // AI feedback (trade evaluation)
            if (controller.aiFeedbackDialogText != null && !string.IsNullOrEmpty(controller.aiFeedbackDialogText.text))
            {
                section.Options.Add(new DiplomacyOption
                {
                    Label = TISpeechMod.CleanText(controller.aiFeedbackDialogText.text),
                    DetailText = "Current trade evaluation",
                    ItemType = DiplomacyItemType.Information,
                    IsInformational = true
                });
            }

            if (section.Options.Count > 0)
                sections.Add(section);
        }

        private void BuildYourOfferSection()
        {
            var section = new DiplomacySection { Name = "Your Offer" };

            // Add resource table items that are active
            AddResourceTableItem(section, controller.playerTableCashItem, "Money", FactionResource.Money, true);
            AddResourceTableItem(section, controller.playerTableInfluenceItem, "Influence", FactionResource.Influence, true);
            AddResourceTableItem(section, controller.playerTableOpsItem, "Operations", FactionResource.Operations, true);
            AddResourceTableItem(section, controller.playerTableBoostItem, "Boost", FactionResource.Boost, true);
            AddResourceTableItem(section, controller.playerTableWaterItem, "Water", FactionResource.Water, true);
            AddResourceTableItem(section, controller.playerTableVolatilesItem, "Volatiles", FactionResource.Volatiles, true);
            AddResourceTableItem(section, controller.playerTableBaseMetalsItem, "Metals", FactionResource.Metals, true);
            AddResourceTableItem(section, controller.playerTableNobleMetalsItem, "Noble Metals", FactionResource.NobleMetals, true);
            AddResourceTableItem(section, controller.playerTableFissilesItem, "Fissiles", FactionResource.Fissiles, true);
            AddResourceTableItem(section, controller.playerTableAntimatterItem, "Antimatter", FactionResource.Antimatter, true);
            AddResourceTableItem(section, controller.playerTableExoticsItem, "Exotics", FactionResource.Exotics, true);

            // Add treaty if active
            if (controller.playerTableTreatyItem != null && controller.playerTableTreatyItem.gameObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.playerTableTreatyItem.itemDescription?.text ?? "Treaty");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(controller.playerTableTreatyItem.tooltipTrigger),
                    ItemType = DiplomacyItemType.Treaty,
                    IsInTable = true,
                    IsPlayerSide = true,
                    TableItem = controller.playerTableTreatyItem
                });
            }

            // Add intel exchange if active
            if (controller.playerTableExchangeIntelItem != null && controller.playerTableExchangeIntelItem.gameObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.playerTableExchangeIntelItem.itemDescription?.text ?? "Intel Exchange");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(controller.playerTableExchangeIntelItem.tooltipTrigger),
                    ItemType = DiplomacyItemType.IntelExchange,
                    IsInTable = true,
                    IsPlayerSide = true,
                    TableItem = controller.playerTableExchangeIntelItem
                });
            }

            // Add orgs, habs, projects from player table
            AddDynamicTableItems(section, controller.playerTableItemsContent, true);

            if (section.Options.Count > 0)
                sections.Add(section);
            else
            {
                // Add empty placeholder
                section.Options.Add(new DiplomacyOption
                {
                    Label = "No items in your offer",
                    DetailText = "Navigate to 'Add to Your Offer' section to add items",
                    ItemType = DiplomacyItemType.Information,
                    IsInformational = true
                });
                sections.Add(section);
            }
        }

        private void BuildYourBankSection()
        {
            var section = new DiplomacySection { Name = "Add to Your Offer" };

            // Resources tab toggle
            AddTabToggle(section, controller.playerResourcesTab, TradeItemType.Resource, true);

            // Add resource bank items if visible (right after Resources tab)
            AddResourceBankItem(section, controller.playerBankCashItem, "Money", FactionResource.Money, true);
            AddResourceBankItem(section, controller.playerBankInfluenceItem, "Influence", FactionResource.Influence, true);
            AddResourceBankItem(section, controller.playerBankOpsItem, "Operations", FactionResource.Operations, true);
            AddResourceBankItem(section, controller.playerBankBoostItem, "Boost", FactionResource.Boost, true);
            AddResourceBankItem(section, controller.playerBankWaterItem, "Water", FactionResource.Water, true);
            AddResourceBankItem(section, controller.playerBankVolatilesItem, "Volatiles", FactionResource.Volatiles, true);
            AddResourceBankItem(section, controller.playerBankBaseMetalsItem, "Metals", FactionResource.Metals, true);
            AddResourceBankItem(section, controller.playerBankNobleMetalsItem, "Noble Metals", FactionResource.NobleMetals, true);
            AddResourceBankItem(section, controller.playerBankFissilesItem, "Fissiles", FactionResource.Fissiles, true);
            AddResourceBankItem(section, controller.playerBankAntimatterItem, "Antimatter", FactionResource.Antimatter, true);
            AddResourceBankItem(section, controller.playerBankExoticsItem, "Exotics", FactionResource.Exotics, true);

            // Treaty bank item if available
            AddTreatyBankItem(section, controller.playerBankTreatyItem, true);

            // Intel exchange bank item if available
            AddIntelExchangeBankItem(section, controller.playerBankExchangeIntelItem, true);

            // Orgs tab + items (items appear right after their tab)
            AddTabToggle(section, controller.playerOrgTab, TradeItemType.Org, true);
            AddDynamicBankItemsByType(section, controller.playerBankItemsContent, TradeItemType.Org, true);

            // Habs tab + items (if visible)
            if (controller.playerHabsTab != null && controller.playerHabsTab.gameObject.activeSelf)
            {
                AddTabToggle(section, controller.playerHabsTab, TradeItemType.Hab, true);
                AddDynamicBankItemsByType(section, controller.playerBankItemsContent, TradeItemType.Hab, true);
            }

            // Projects tab + items (if visible)
            if (controller.playerProjectsTab != null && controller.playerProjectsTab.gameObject.activeSelf)
            {
                AddTabToggle(section, controller.playerProjectsTab, TradeItemType.Project, true);
                AddDynamicBankItemsByType(section, controller.playerBankItemsContent, TradeItemType.Project, true);
            }

            if (section.Options.Count > 0)
                sections.Add(section);
        }

        private void BuildTheirOfferSection()
        {
            var section = new DiplomacySection { Name = "Their Offer" };

            // Add resource table items that are active
            AddResourceTableItem(section, controller.aiTableCashItem, "Money", FactionResource.Money, false);
            AddResourceTableItem(section, controller.aiTableInfluenceItem, "Influence", FactionResource.Influence, false);
            AddResourceTableItem(section, controller.aiTableOpsItem, "Operations", FactionResource.Operations, false);
            AddResourceTableItem(section, controller.aiTableBoostItem, "Boost", FactionResource.Boost, false);
            AddResourceTableItem(section, controller.aiTableWaterItem, "Water", FactionResource.Water, false);
            AddResourceTableItem(section, controller.aiTableVolatilesItem, "Volatiles", FactionResource.Volatiles, false);
            AddResourceTableItem(section, controller.aiTableBaseMetalsItem, "Metals", FactionResource.Metals, false);
            AddResourceTableItem(section, controller.aiTableNobleMetalsItem, "Noble Metals", FactionResource.NobleMetals, false);
            AddResourceTableItem(section, controller.aiTableFissilesItem, "Fissiles", FactionResource.Fissiles, false);
            AddResourceTableItem(section, controller.aiTableAntimatterItem, "Antimatter", FactionResource.Antimatter, false);
            AddResourceTableItem(section, controller.aiTableExoticsItem, "Exotics", FactionResource.Exotics, false);

            // Add treaty if active
            if (controller.aiTableTreatyItem != null && controller.aiTableTreatyItem.gameObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.aiTableTreatyItem.itemDescription?.text ?? "Treaty");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(controller.aiTableTreatyItem.tooltipTrigger),
                    ItemType = DiplomacyItemType.Treaty,
                    IsInTable = true,
                    IsPlayerSide = false,
                    TableItem = controller.aiTableTreatyItem
                });
            }

            // Add intel exchange if active
            if (controller.aiTableExchangeIntelItem != null && controller.aiTableExchangeIntelItem.gameObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.aiTableExchangeIntelItem.itemDescription?.text ?? "Intel Exchange");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(controller.aiTableExchangeIntelItem.tooltipTrigger),
                    ItemType = DiplomacyItemType.IntelExchange,
                    IsInTable = true,
                    IsPlayerSide = false,
                    TableItem = controller.aiTableExchangeIntelItem
                });
            }

            // Add improve relations if active
            if (controller.aiTableHateReductionItem != null && controller.aiTableHateReductionItem.gameObject.activeSelf)
            {
                string label = TISpeechMod.CleanText(controller.aiTableHateReductionItem.itemDescription?.text ?? "Improve Relations");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(controller.aiTableHateReductionItem.tooltipTrigger),
                    ItemType = DiplomacyItemType.HateReduction,
                    IsInTable = true,
                    IsPlayerSide = false,
                    TableItem = controller.aiTableHateReductionItem,
                    IsInformational = true  // Can't be removed
                });
            }

            // Add orgs, habs, projects from AI table
            AddDynamicTableItems(section, controller.aiTableItemsContent, false);

            if (section.Options.Count > 0)
                sections.Add(section);
            else
            {
                section.Options.Add(new DiplomacyOption
                {
                    Label = "No items in their offer",
                    DetailText = "Navigate to 'Request from Them' section to request items",
                    ItemType = DiplomacyItemType.Information,
                    IsInformational = true
                });
                sections.Add(section);
            }
        }

        private void BuildTheirBankSection()
        {
            var section = new DiplomacySection { Name = "Request from Them" };

            // Resources tab toggle
            AddTabToggle(section, controller.aiResourcesTab, TradeItemType.Resource, false);

            // Add resource bank items if visible (right after Resources tab)
            AddResourceBankItem(section, controller.aiBankCashItem, "Money", FactionResource.Money, false);
            AddResourceBankItem(section, controller.aiBankInfluenceItem, "Influence", FactionResource.Influence, false);
            AddResourceBankItem(section, controller.aiBankOpsItem, "Operations", FactionResource.Operations, false);
            AddResourceBankItem(section, controller.aiBankBoostItem, "Boost", FactionResource.Boost, false);
            AddResourceBankItem(section, controller.aiBankWaterItem, "Water", FactionResource.Water, false);
            AddResourceBankItem(section, controller.aiBankVolatilesItem, "Volatiles", FactionResource.Volatiles, false);
            AddResourceBankItem(section, controller.aiBankBaseMetalsItem, "Metals", FactionResource.Metals, false);
            AddResourceBankItem(section, controller.aiBankNobleMetalsItem, "Noble Metals", FactionResource.NobleMetals, false);
            AddResourceBankItem(section, controller.aiBankFissilesItem, "Fissiles", FactionResource.Fissiles, false);
            AddResourceBankItem(section, controller.aiBankAntimatterItem, "Antimatter", FactionResource.Antimatter, false);
            AddResourceBankItem(section, controller.aiBankExoticsItem, "Exotics", FactionResource.Exotics, false);

            // Treaty bank item if available
            AddTreatyBankItem(section, controller.aiBankTreatyItem, false);

            // Intel exchange bank item if available
            AddIntelExchangeBankItem(section, controller.aiBankExchangeIntelItem, false);

            // Orgs tab + items (items appear right after their tab)
            AddTabToggle(section, controller.aiOrgTab, TradeItemType.Org, false);
            AddDynamicBankItemsByType(section, controller.aiBankItemsContent, TradeItemType.Org, false);

            // Habs tab + items (if visible)
            if (controller.aiHabsTab != null && controller.aiHabsTab.gameObject.activeSelf)
            {
                AddTabToggle(section, controller.aiHabsTab, TradeItemType.Hab, false);
                AddDynamicBankItemsByType(section, controller.aiBankItemsContent, TradeItemType.Hab, false);
            }

            // Projects tab + items (if visible)
            if (controller.aiProjectsTab != null && controller.aiProjectsTab.gameObject.activeSelf)
            {
                AddTabToggle(section, controller.aiProjectsTab, TradeItemType.Project, false);
                AddDynamicBankItemsByType(section, controller.aiBankItemsContent, TradeItemType.Project, false);
            }

            if (section.Options.Count > 0)
                sections.Add(section);
        }

        private void BuildActionsSection()
        {
            var section = new DiplomacySection { Name = "Actions" };

            // Execute Trade button
            if (controller.executeTradeButton != null)
            {
                bool canExecute = controller.executeTradeButton.interactable;
                section.Options.Add(new DiplomacyOption
                {
                    Label = canExecute ? "Execute Trade" : "Execute Trade (unavailable)",
                    DetailText = canExecute ? "Finalize and execute this trade" : "Trade is not balanced or empty",
                    ItemType = DiplomacyItemType.Action,
                    IsInformational = !canExecute,
                    ActionButton = controller.executeTradeButton
                });
            }

            // Clear button
            section.Options.Add(new DiplomacyOption
            {
                Label = "Clear Trade",
                DetailText = "Remove all items from the trade table",
                ItemType = DiplomacyItemType.Action,
                IsInformational = false
            });

            // Cancel/Exit
            section.Options.Add(new DiplomacyOption
            {
                Label = "Cancel",
                DetailText = "Exit diplomacy without trading",
                ItemType = DiplomacyItemType.Action,
                IsInformational = false
            });

            sections.Add(section);
        }

        #endregion

        #region Helper Methods for Section Building

        private void AddResourceTableItem(DiplomacySection section, DiplomacyTableListItem item, string resourceName, FactionResource resource, bool isPlayerSide)
        {
            if (item == null || !item.gameObject.activeSelf) return;

            int quantity = 0;
            int maxQuantity = item.originalValue;
            if (item.quantitySendInput != null && !string.IsNullOrEmpty(item.quantitySendInput.text))
            {
                int.TryParse(item.quantitySendInput.text, out quantity);
            }

            if (quantity <= 0) return;  // Don't show if quantity is 0

            section.Options.Add(new DiplomacyOption
            {
                Label = $"{resourceName}: {quantity}",
                DetailText = GetTooltipText(item.tooltipTrigger),
                ItemType = DiplomacyItemType.Resource,
                IsInTable = true,
                IsPlayerSide = isPlayerSide,
                TableItem = item,
                Resource = resource,
                CurrentQuantity = quantity,
                MaxQuantity = maxQuantity
            });
        }

        private void AddResourceBankItem(DiplomacySection section, DiplomacyBankListItem item, string resourceName, FactionResource resource, bool isPlayerSide)
        {
            if (item == null || !item.gameObject.activeSelf) return;

            int quantity = 0;
            if (item.quantityText != null && !string.IsNullOrEmpty(item.quantityText.text))
            {
                int.TryParse(item.quantityText.text, out quantity);
            }

            // Check if button is interactable
            var button = item.GetComponent<Button>();
            bool isInteractable = button != null && button.interactable;

            section.Options.Add(new DiplomacyOption
            {
                Label = $"{resourceName}: {quantity} available",
                DetailText = GetTooltipText(item.tooltipTrigger),
                ItemType = DiplomacyItemType.Resource,
                IsInTable = false,
                IsPlayerSide = isPlayerSide,
                BankItem = item,
                Resource = resource,
                CurrentQuantity = 0,
                MaxQuantity = quantity,
                IsInformational = !isInteractable
            });
        }

        private void AddTabToggle(DiplomacySection section, DiplomacyBankListItem tab, TradeItemType tradeType, bool isPlayerSide)
        {
            if (tab == null || !tab.gameObject.activeSelf) return;

            bool isExpanded = tab.tabText?.text == "-";
            string typeName = tradeType.ToString();
            string expandState = isExpanded ? "expanded" : "collapsed";

            section.Options.Add(new DiplomacyOption
            {
                Label = $"{typeName} ({expandState})",
                DetailText = $"Toggle to {(isExpanded ? "collapse" : "expand")} {typeName.ToLower()} list",
                ItemType = DiplomacyItemType.Tab,
                IsPlayerSide = isPlayerSide,
                BankItem = tab,
                TradeType = tradeType,
                IsExpanded = isExpanded
            });
        }

        private void AddTreatyBankItem(DiplomacySection section, DiplomacyBankListItem item, bool isPlayerSide)
        {
            if (item == null || !item.gameObject.activeSelf) return;

            string label = TISpeechMod.CleanText(item.quantityText?.text ?? "Treaty");
            var button = item.GetComponent<Button>();
            bool isInteractable = button != null && button.interactable;

            section.Options.Add(new DiplomacyOption
            {
                Label = label,
                DetailText = GetTooltipText(item.tooltipTrigger),
                ItemType = DiplomacyItemType.Treaty,
                IsInTable = false,
                IsPlayerSide = isPlayerSide,
                BankItem = item,
                IsInformational = !isInteractable
            });
        }

        private void AddIntelExchangeBankItem(DiplomacySection section, DiplomacyBankListItem item, bool isPlayerSide)
        {
            if (item == null || !item.gameObject.activeSelf) return;

            string label = TISpeechMod.CleanText(item.quantityText?.text ?? "Intel Exchange");
            var button = item.GetComponent<Button>();
            bool isInteractable = button != null && button.interactable;

            section.Options.Add(new DiplomacyOption
            {
                Label = label,
                DetailText = GetTooltipText(item.tooltipTrigger),
                ItemType = DiplomacyItemType.IntelExchange,
                IsInTable = false,
                IsPlayerSide = isPlayerSide,
                BankItem = item,
                IsInformational = !isInteractable
            });
        }

        private void AddDynamicTableItems(DiplomacySection section, GameObject contentContainer, bool isPlayerSide)
        {
            if (contentContainer == null) return;

            for (int i = 0; i < contentContainer.transform.childCount; i++)
            {
                var child = contentContainer.transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;

                var tableItem = child.GetComponent<DiplomacyTableListItem>();
                if (tableItem == null) continue;

                // Skip resource items (already handled)
                if (tableItem.itemType == TradeItemType.Resource) continue;
                // Skip treaty (already handled)
                if (tableItem.itemType == TradeItemType.Treaty) continue;
                // Skip intel exchange (already handled)
                if (tableItem.itemType == TradeItemType.ExchangeIntel) continue;

                DiplomacyItemType itemType = DiplomacyItemType.Org;
                if (tableItem.itemType == TradeItemType.Hab) itemType = DiplomacyItemType.Hab;
                else if (tableItem.itemType == TradeItemType.Project) itemType = DiplomacyItemType.Project;

                string label = TISpeechMod.CleanText(tableItem.itemDescription?.text ?? "Unknown");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(tableItem.tooltipTrigger),
                    ItemType = itemType,
                    IsInTable = true,
                    IsPlayerSide = isPlayerSide,
                    TableItem = tableItem
                });
            }
        }

        /// <summary>
        /// Add dynamic bank items of a specific type (Org, Hab, or Project).
        /// This ensures items appear right after their respective tab toggle.
        /// </summary>
        private void AddDynamicBankItemsByType(DiplomacySection section, GameObject contentContainer, TradeItemType targetType, bool isPlayerSide)
        {
            if (contentContainer == null) return;

            for (int i = 0; i < contentContainer.transform.childCount; i++)
            {
                var child = contentContainer.transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;

                var bankItem = child.GetComponent<DiplomacyBankListItem>();
                if (bankItem == null || !bankItem.isValid) continue;

                // Only add items matching the target type
                if (bankItem.itemType != targetType) continue;

                DiplomacyItemType itemType = DiplomacyItemType.Org;
                if (bankItem.itemType == TradeItemType.Hab) itemType = DiplomacyItemType.Hab;
                else if (bankItem.itemType == TradeItemType.Project) itemType = DiplomacyItemType.Project;

                string label = TISpeechMod.CleanText(bankItem.quantityText?.text ?? "Unknown");
                section.Options.Add(new DiplomacyOption
                {
                    Label = label,
                    DetailText = GetTooltipText(bankItem.tooltipTrigger),
                    ItemType = itemType,
                    IsInTable = false,
                    IsPlayerSide = isPlayerSide,
                    BankItem = bankItem
                });
            }
        }

        private string GetTooltipText(ModelShark.TooltipTrigger tooltip)
        {
            try
            {
                if (tooltip == null || tooltip.Tooltip == null) return "";

                var textFields = tooltip.Tooltip.TextFields;
                if (textFields == null || textFields.Count == 0) return "";

                var sb = new StringBuilder();
                foreach (var field in textFields)
                {
                    if (field?.Text != null && !string.IsNullOrEmpty(field.Text.text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(TISpeechMod.CleanText(field.Text.text));
                    }
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Move to the next item. At section level, moves to next section. At item level, moves to next item.
        /// </summary>
        public void Next()
        {
            // Apply any pending quantity first
            if (IsTypingQuantity)
            {
                ApplyTypedQuantity();
            }

            if (isDrilledIntoSection)
            {
                // Navigate items within section
                var section = CurrentSection;
                if (section == null || section.Options.Count == 0) return;
                currentItemIndex = (currentItemIndex + 1) % section.Options.Count;
            }
            else
            {
                // Navigate sections
                if (sections.Count == 0) return;
                currentSectionIndex = (currentSectionIndex + 1) % sections.Count;
            }
        }

        /// <summary>
        /// Move to the previous item. At section level, moves to previous section. At item level, moves to previous item.
        /// </summary>
        public void Previous()
        {
            // Apply any pending quantity first
            if (IsTypingQuantity)
            {
                ApplyTypedQuantity();
            }

            if (isDrilledIntoSection)
            {
                // Navigate items within section
                var section = CurrentSection;
                if (section == null || section.Options.Count == 0) return;
                currentItemIndex--;
                if (currentItemIndex < 0) currentItemIndex = section.Options.Count - 1;
            }
            else
            {
                // Navigate sections
                if (sections.Count == 0) return;
                currentSectionIndex--;
                if (currentSectionIndex < 0) currentSectionIndex = sections.Count - 1;
            }
        }

        /// <summary>
        /// Drill down into the current section, or activate the current item if already drilled in.
        /// </summary>
        /// <returns>Result message to announce</returns>
        public string DrillDown()
        {
            // If typing quantity, apply it first
            if (IsTypingQuantity)
            {
                return ApplyTypedQuantity();
            }

            if (!isDrilledIntoSection)
            {
                // Drill into section
                var section = CurrentSection;
                if (section == null || section.Options.Count == 0)
                {
                    return "Section is empty";
                }
                isDrilledIntoSection = true;
                currentItemIndex = 0;
                return GetCurrentAnnouncement();
            }
            else
            {
                // Activate current item
                return Activate();
            }
        }

        /// <summary>
        /// Back out from item level to section level, or signal exit if at section level.
        /// </summary>
        /// <returns>True if backed out to section level, false if should exit diplomacy mode</returns>
        public bool BackOut()
        {
            // Clear any pending quantity
            ClearTypedQuantity();

            if (isDrilledIntoSection)
            {
                isDrilledIntoSection = false;
                return true;
            }
            return false;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Activate the current option (add to table, remove from table, toggle tab, etc.)
        /// </summary>
        private string Activate()
        {
            var option = CurrentOption;
            if (option == null) return "No option selected";
            if (option.IsInformational) return $"{option.Label}. This is informational only.";

            try
            {
                switch (option.ItemType)
                {
                    case DiplomacyItemType.Tab:
                        return ActivateTab(option);

                    case DiplomacyItemType.Resource:
                        return ActivateResource(option);

                    case DiplomacyItemType.Org:
                    case DiplomacyItemType.Hab:
                    case DiplomacyItemType.Project:
                    case DiplomacyItemType.Treaty:
                    case DiplomacyItemType.IntelExchange:
                        return ActivateItem(option);

                    case DiplomacyItemType.Action:
                        return ActivateAction(option);

                    default:
                        return option.Label;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating diplomacy option: {ex.Message}");
                return "Error activating option";
            }
        }

        private string ActivateTab(DiplomacyOption option)
        {
            if (option.BankItem == null) return "Invalid tab";

            // Use the controller's direct toggle methods instead of button invocation
            bool isPlayerSide = option.IsPlayerSide;

            switch (option.TradeType)
            {
                case TradeItemType.Resource:
                    controller.ToggleResources(isPlayerSide);
                    break;
                case TradeItemType.Org:
                    controller.ToggleOrgs(isPlayerSide);
                    break;
                case TradeItemType.Hab:
                    controller.ToggleHabs(isPlayerSide);
                    break;
                case TradeItemType.Project:
                    controller.ToggleProjects(isPlayerSide);
                    break;
                default:
                    return "Unknown tab type";
            }

            Refresh();
            bool nowExpanded = option.BankItem.tabText?.text == "-";
            return $"{option.TradeType} {(nowExpanded ? "expanded" : "collapsed")}";
        }

        private string ActivateResource(DiplomacyOption option)
        {
            if (option.IsInTable)
            {
                // In table - Enter activates quantity input mode to modify existing
                if (option.TableItem == null) return "Invalid table item";

                pendingBankItem = null;
                pendingTableItem = option.TableItem;
                pendingResource = option.Resource;
                pendingMaxQuantity = option.MaxQuantity;

                isEnteringQuantity = true;
                quantityInput = "";

                return $"Enter quantity for {option.Resource}. Current: {option.CurrentQuantity}. Maximum: {pendingMaxQuantity}. Type digits then press Enter.";
            }
            else
            {
                // In bank - Enter activates quantity input mode to add new
                if (option.BankItem == null) return "Invalid bank item";
                if (option.MaxQuantity <= 0) return $"No {option.Resource} available to trade";

                pendingBankItem = option.BankItem;
                pendingTableItem = null;
                pendingResource = option.Resource;
                pendingMaxQuantity = option.MaxQuantity;

                isEnteringQuantity = true;
                quantityInput = "";

                return $"Enter quantity for {option.Resource}. Available: {pendingMaxQuantity}. Type digits then press Enter.";
            }
        }

        /// <summary>
        /// Remove the current item from the trade (used by Delete key).
        /// </summary>
        public string RemoveCurrentItem()
        {
            var option = CurrentOption;
            if (option == null) return "No item selected";
            if (!option.IsInTable) return "Item is not in the trade";
            if (option.IsInformational) return "Cannot remove this item";

            if (option.TableItem != null)
            {
                string label = option.ItemType == DiplomacyItemType.Resource
                    ? option.Resource.ToString()
                    : option.Label;
                option.TableItem.OnRightClick(audio: true);
                Refresh();
                return $"Removed {label} from trade";
            }

            return "Could not remove item";
        }

        private string ActivateItem(DiplomacyOption option)
        {
            if (option.IsInTable)
            {
                // In table - remove it
                if (option.TableItem != null)
                {
                    option.TableItem.OnRightClick(audio: true);
                    Refresh();
                    return $"Removed {option.Label} from trade";
                }
            }
            else
            {
                // In bank - add to table
                if (option.BankItem != null)
                {
                    option.BankItem.OnLeftClick();
                    Refresh();
                    return $"Added {option.Label} to trade";
                }
            }

            return "Could not activate item";
        }

        private string ActivateAction(DiplomacyOption option)
        {
            if (option.Label.StartsWith("Execute Trade"))
            {
                if (option.ActionButton != null && option.ActionButton.interactable)
                {
                    controller.OnClickTradeButton();
                    return "Trade executed";
                }
                else
                {
                    return "Cannot execute trade. Trade is not balanced.";
                }
            }
            else if (option.Label == "Clear Trade")
            {
                controller.OnClickClear();
                Refresh();
                return "Trade cleared";
            }
            else if (option.Label == "Cancel")
            {
                // Actually close the diplomacy window using the game's proper method
                CloseDiplomacyWindow();
                return "CANCEL_DIPLOMACY";
            }

            return option.Label;
        }

        /// <summary>
        /// Close the diplomacy window by calling the NotificationScreenController's OnDiplomacyCloseButton method.
        /// Uses reflection to access the private notificationController field.
        /// </summary>
        public void CloseDiplomacyWindow()
        {
            try
            {
                // Get the private notificationController field from DiplomacyController
                var fieldInfo = typeof(DiplomacyController).GetField("notificationController",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldInfo != null)
                {
                    var notificationController = fieldInfo.GetValue(controller) as NotificationScreenController;
                    if (notificationController != null)
                    {
                        notificationController.OnDiplomacyCloseButton();
                        MelonLogger.Msg("Closed diplomacy window via OnDiplomacyCloseButton");
                        return;
                    }
                }

                // Fallback: try to find NotificationScreenController and call OnDiplomacyCloseButton
                var nsc = UnityEngine.Object.FindObjectOfType<NotificationScreenController>();
                if (nsc != null)
                {
                    nsc.OnDiplomacyCloseButton();
                    MelonLogger.Msg("Closed diplomacy window via FindObjectOfType");
                }
                else
                {
                    MelonLogger.Warning("Could not find NotificationScreenController to close diplomacy window");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error closing diplomacy window: {ex.Message}");
            }
        }

        #endregion

        #region Quantity Input Mode

        /// <summary>
        /// Enter quantity input mode for a table resource item.
        /// Returns the announcement to speak.
        /// </summary>
        public string EnterQuantityMode()
        {
            var option = CurrentOption;
            if (option == null || option.ItemType != DiplomacyItemType.Resource || !option.IsInTable)
            {
                return "Cannot enter quantity mode - not on a resource in the trade";
            }

            isEnteringQuantity = true;
            quantityInput = "";

            return $"Enter quantity for {option.Resource}. Current: {option.CurrentQuantity}. Maximum: {option.MaxQuantity}. Type digits then press Enter.";
        }

        /// <summary>
        /// Handle a digit key press while in quantity input mode.
        /// </summary>
        public bool HandleDigit(char digit)
        {
            if (!isEnteringQuantity) return false;
            if (!char.IsDigit(digit)) return false;

            // Limit input length to prevent overflow
            if (quantityInput.Length >= 10) return true;

            quantityInput += digit;

            // Clamp to max quantity using the stored max
            if (int.TryParse(quantityInput, out int value))
            {
                if (value > pendingMaxQuantity)
                {
                    quantityInput = pendingMaxQuantity.ToString();
                }
            }

            return true;
        }

        /// <summary>
        /// Handle backspace in quantity input mode.
        /// </summary>
        public bool HandleBackspace()
        {
            if (!isEnteringQuantity) return false;

            if (quantityInput.Length > 0)
            {
                quantityInput = quantityInput.Substring(0, quantityInput.Length - 1);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Apply the entered quantity and exit quantity mode.
        /// Returns the announcement to speak.
        /// </summary>
        public string ApplyQuantity()
        {
            if (!isEnteringQuantity)
            {
                return "";
            }

            // Parse the entered value
            int value = 0;
            if (!string.IsNullOrEmpty(quantityInput))
            {
                int.TryParse(quantityInput, out value);
            }

            // Clamp to max
            value = Math.Min(value, pendingMaxQuantity);
            value = Math.Max(value, 0);

            string result;

            if (pendingBankItem != null)
            {
                // Adding from bank - use AddToTable to bypass UI completely
                if (value <= 0)
                {
                    result = "Cancelled - no quantity entered";
                }
                else
                {
                    pendingBankItem.AddToTable(value, playAudio: false);
                    // Play the trade sound manually (AddToTable's playAudio param is unused in game code)
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_CycleForward");
                    result = $"Added {value} {pendingResource} to trade";
                    MelonLogger.Msg($"Added {value} {pendingResource} via AddToTable");
                }
            }
            else if (pendingTableItem != null)
            {
                // Modifying existing table item - set quantity directly
                if (pendingTableItem.quantitySendInput != null)
                {
                    pendingTableItem.quantitySendInput.text = value.ToString();
                    pendingTableItem.OnValueChanged();
                    result = $"Set {pendingResource} to {value}";
                    MelonLogger.Msg($"Set {pendingResource} quantity to {value} directly");
                }
                else
                {
                    result = "Error: could not modify quantity";
                }
            }
            else
            {
                result = "Error: no target for quantity";
            }

            // Clear state
            isEnteringQuantity = false;
            quantityInput = "";
            pendingBankItem = null;
            pendingTableItem = null;
            pendingMaxQuantity = 0;

            Refresh();
            return result;
        }

        /// <summary>
        /// Cancel quantity input mode without applying.
        /// </summary>
        public string CancelQuantityMode()
        {
            if (!isEnteringQuantity)
            {
                return "";
            }

            isEnteringQuantity = false;
            quantityInput = "";
            pendingBankItem = null;
            pendingTableItem = null;
            pendingMaxQuantity = 0;
            return "Quantity entry cancelled";
        }

        /// <summary>
        /// Get the current quantity input announcement.
        /// </summary>
        public string GetQuantityInputAnnouncement()
        {
            if (string.IsNullOrEmpty(quantityInput))
            {
                if (pendingBankItem != null)
                {
                    return $"{pendingResource}: type digits to add. Max: {pendingMaxQuantity}";
                }
                else
                {
                    return $"{pendingResource}: type digits. Max: {pendingMaxQuantity}";
                }
            }
            else
            {
                return $"{pendingResource}: {quantityInput}";
            }
        }

        // Legacy methods for compatibility
        public void TypeDigit(char digit) => HandleDigit(digit);
        public void ClearTypedQuantity() => CancelQuantityMode();
        public string ApplyTypedQuantity() => ApplyQuantity();

        /// <summary>
        /// Check if current item is a resource in the trade table.
        /// </summary>
        public bool IsCurrentItemResourceInTable()
        {
            if (!isDrilledIntoSection) return false;
            var option = CurrentOption;
            return option != null && option.ItemType == DiplomacyItemType.Resource && option.IsInTable;
        }

        /// <summary>
        /// Check if current item is a tab toggle.
        /// </summary>
        public bool IsCurrentItemTab()
        {
            var option = CurrentOption;
            return option != null && option.ItemType == DiplomacyItemType.Tab;
        }

        #endregion

        #region Announcements

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append("Diplomacy Mode. ");

            // Announce trading faction
            if (controller.aiFactionText != null && !string.IsNullOrEmpty(controller.aiFactionText.text))
            {
                sb.Append("Trading with ");
                sb.Append(TISpeechMod.CleanText(controller.aiFactionText.text));
                sb.Append(". ");
            }

            sb.Append(sections.Count);
            sb.Append(" sections. Use Up and Down to navigate sections, Enter to drill in, Escape to back out. ");

            if (sections.Count > 0 && CurrentSection != null)
            {
                sb.Append("Section 1 of ");
                sb.Append(sections.Count);
                sb.Append(": ");
                sb.Append(CurrentSection.Name);
                sb.Append(", ");
                sb.Append(CurrentSection.Options.Count);
                sb.Append(" items.");
            }

            return sb.ToString();
        }

        public string GetCurrentAnnouncement()
        {
            if (!isDrilledIntoSection)
            {
                // At section level
                return GetSectionAnnouncement();
            }
            else
            {
                // At item level
                return GetItemAnnouncement();
            }
        }

        private string GetSectionAnnouncement()
        {
            var section = CurrentSection;
            if (section == null) return "No section selected";

            var sb = new StringBuilder();
            sb.Append("Section ");
            sb.Append(currentSectionIndex + 1);
            sb.Append(" of ");
            sb.Append(sections.Count);
            sb.Append(": ");
            sb.Append(section.Name);
            sb.Append(", ");
            sb.Append(section.Options.Count);
            sb.Append(section.Options.Count == 1 ? " item" : " items");

            return sb.ToString();
        }

        private string GetItemAnnouncement()
        {
            var section = CurrentSection;
            var option = CurrentOption;

            if (section == null || option == null)
                return "No item selected";

            var sb = new StringBuilder();
            sb.Append(currentItemIndex + 1);
            sb.Append(" of ");
            sb.Append(section.Options.Count);
            sb.Append(": ");
            sb.Append(option.Label);

            // If in quantity input mode, show input status
            if (isEnteringQuantity)
            {
                sb.Append(". Entering quantity: ");
                sb.Append(string.IsNullOrEmpty(quantityInput) ? "empty" : quantityInput);
            }
            else if (option.ItemType == DiplomacyItemType.Resource && option.IsInTable)
            {
                sb.Append(". Press Enter to set quantity, Delete to remove.");
            }

            return sb.ToString();
        }

        public string GetCurrentDetail()
        {
            if (!isDrilledIntoSection)
            {
                // At section level - describe section
                var section = CurrentSection;
                if (section == null) return "No section selected";

                var sb = new StringBuilder();
                sb.Append(section.Name);
                sb.Append(": ");
                sb.Append(section.Options.Count);
                sb.Append(section.Options.Count == 1 ? " item. " : " items. ");

                // List first few items
                int count = Math.Min(5, section.Options.Count);
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(section.Options[i].Label);
                }
                if (section.Options.Count > 5)
                {
                    sb.Append($", and {section.Options.Count - 5} more");
                }

                return sb.ToString();
            }

            var option = CurrentOption;
            if (option == null) return "No item selected";

            var sb2 = new StringBuilder();
            sb2.Append(option.Label);

            if (!string.IsNullOrEmpty(option.DetailText))
            {
                sb2.Append(". ");
                sb2.Append(option.DetailText);
            }

            // Add context about what actions are available
            if (!option.IsInformational)
            {
                sb2.Append(". Press Enter to ");
                if (option.ItemType == DiplomacyItemType.Tab)
                {
                    sb2.Append(option.IsExpanded ? "collapse" : "expand");
                }
                else if (option.IsInTable)
                {
                    sb2.Append("remove from trade");
                }
                else
                {
                    sb2.Append("add to trade");
                }
            }

            return sb2.ToString();
        }

        public string ListCurrentSection()
        {
            var section = CurrentSection;
            if (section == null) return "No section selected";

            var sb = new StringBuilder();
            sb.Append(section.Name);
            sb.Append(": ");
            sb.Append(section.Options.Count);
            sb.Append(section.Options.Count == 1 ? " item. " : " items. ");

            for (int i = 0; i < section.Options.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(section.Options[i].Label);
            }

            return sb.ToString();
        }

        #endregion

        #region Refresh

        /// <summary>
        /// Rebuild sections after a change (item added/removed, quantity changed, etc.)
        /// </summary>
        public void Refresh()
        {
            int oldSectionIndex = currentSectionIndex;
            int oldItemIndex = currentItemIndex;
            string oldSectionName = CurrentSection?.Name ?? "";
            bool wasDrilledIn = isDrilledIntoSection;

            BuildSections();

            // Try to restore position
            currentSectionIndex = Math.Min(oldSectionIndex, sections.Count - 1);
            if (currentSectionIndex < 0) currentSectionIndex = 0;

            // Try to find the same section by name
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Name == oldSectionName)
                {
                    currentSectionIndex = i;
                    break;
                }
            }

            // Restore drilled-in state and item index within bounds
            isDrilledIntoSection = wasDrilledIn;
            var section = CurrentSection;
            if (section != null && isDrilledIntoSection)
            {
                currentItemIndex = Math.Min(oldItemIndex, section.Options.Count - 1);
                if (currentItemIndex < 0) currentItemIndex = 0;
            }
            else
            {
                currentItemIndex = 0;
            }
        }

        #endregion
    }
}
