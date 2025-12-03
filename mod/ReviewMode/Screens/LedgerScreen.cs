using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Ledger screen - browse faction income and expenses organized by resource type.
    /// Navigation: Resource Type -> Income/Costs sections -> Individual sources (with drill-down for details).
    /// </summary>
    public class LedgerScreen : ScreenBase
    {
        private readonly LedgerReader ledgerReader = new LedgerReader();

        // Section caching (like NationScreen pattern)
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback when sections are invalidated (after actions).
        /// The controller should re-fetch sections from NavigationState.
        /// </summary>
        public Action OnSectionsInvalidated { get; set; }

        public override string Name => "Ledger";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return "Faction income and expenses by resource type";

                // Calculate total net income across key resources
                try
                {
                    float moneyNet = faction.GetMonthlyGrossRevenue(FactionResource.Money) - faction.GetMonthlyGrossExpenses(FactionResource.Money);
                    float influenceNet = faction.GetMonthlyGrossRevenue(FactionResource.Influence) - faction.GetMonthlyGrossExpenses(FactionResource.Influence);

                    string moneySign = moneyNet >= 0 ? "+" : "";
                    string influenceSign = influenceNet >= 0 ? "+" : "";

                    return $"Ledger: Money {moneySign}{FormatNumber(moneyNet)}, Influence {influenceSign}{FormatNumber(influenceNet)} per month";
                }
                catch
                {
                    return "Faction income and expenses by resource type";
                }
            }
        }

        public override void Refresh()
        {
            cachedItemIndex = -1;
            cachedSections.Clear();
            ledgerReader.OnSpeak = OnSpeak;
            MelonLogger.Msg("LedgerScreen refreshed");
        }

        public override IReadOnlyList<object> GetItems()
        {
            return LedgerReader.AllResourceTypes.Cast<object>().ToList();
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= LedgerReader.AllResourceTypes.Length)
                return "Invalid resource type";

            var resourceType = LedgerReader.AllResourceTypes[index];
            var faction = GameControl.control?.activePlayer;

            return ledgerReader.ReadResourceSummary(resourceType, faction);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= LedgerReader.AllResourceTypes.Length)
                return "Invalid resource type";

            var resourceType = LedgerReader.AllResourceTypes[index];
            var faction = GameControl.control?.activePlayer;

            return ledgerReader.ReadResourceDetail(resourceType, faction);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= LedgerReader.AllResourceTypes.Length)
                return new List<ISection>();

            // Use cache if available
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;
            var resourceType = LedgerReader.AllResourceTypes[index];
            var faction = GameControl.control?.activePlayer;

            if (faction == null)
            {
                cachedSections = new List<ISection>();
                return cachedSections;
            }

            try
            {
                cachedSections = ledgerReader.GetSectionsForResource(resourceType, faction);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting sections for {resourceType}: {ex.Message}");
                cachedSections = new List<ISection>();
            }

            return cachedSections;
        }

        /// <summary>
        /// Get sections for a drillable item (councilor, hab, fleet details).
        /// </summary>
        public override IReadOnlyList<ISection> GetSectionsForSectionItem(string secondaryId)
        {
            if (string.IsNullOrEmpty(secondaryId))
                return new List<ISection>();

            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return new List<ISection>();

            try
            {
                return ledgerReader.GetSectionsForDrillableItem(secondaryId, faction);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting drill-down sections for {secondaryId}: {ex.Message}");
                return new List<ISection>();
            }
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
    }
}
