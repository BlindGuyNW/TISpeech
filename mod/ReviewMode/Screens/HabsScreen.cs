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
    /// Habs screen - browse your bases and stations.
    /// Supports view mode toggle to see all known habs (intel-filtered).
    /// </summary>
    public class HabsScreen : ScreenBase
    {
        private List<TIHabState> items = new List<TIHabState>();
        private readonly HabReader habReader = new HabReader();

        // View mode: false = your habs only, true = all known habs
        private bool showAllMode = false;

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

        public override string Name => "Habs";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    var myHabs = HabReader.GetPlayerHabs(faction);
                    int bases = myHabs.Count(h => h.IsBase);
                    int stations = myHabs.Count(h => h.IsStation);

                    if (showAllMode)
                    {
                        var allKnown = HabReader.GetAllKnownHabs(faction);
                        int knownOther = allKnown.Count - myHabs.Count;
                        return $"All known habs: {bases} bases, {stations} stations yours, {knownOther} other";
                    }
                    return $"{bases} base{(bases != 1 ? "s" : "")}, {stations} station{(stations != 1 ? "s" : "")}";
                }
                return "Browse your bases and stations";
            }
        }

        public override bool SupportsViewModeToggle => true;
        public override bool SupportsLetterNavigation => true;

        public override IReadOnlyList<object> GetItems()
        {
            return items.Cast<object>().ToList();
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

                if (showAllMode)
                {
                    // Add all known habs (intel-filtered)
                    items.AddRange(HabReader.GetAllKnownHabs(faction));
                }
                else
                {
                    items.AddRange(HabReader.GetPlayerHabs(faction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing habs: {ex.Message}");
            }
        }

        public override string ToggleViewMode()
        {
            showAllMode = !showAllMode;
            Refresh();

            var faction = GameControl.control?.activePlayer;
            if (showAllMode)
            {
                var myHabs = HabReader.GetPlayerHabs(faction);
                var allKnown = HabReader.GetAllKnownHabs(faction);
                int knownOther = allKnown.Count - myHabs.Count;
                return $"Showing all known habs: {myHabs.Count} yours, {knownOther} other";
            }
            else
            {
                int bases = items.Count(h => h.IsBase);
                int stations = items.Count(h => h.IsStation);
                return $"Showing your habs only: {bases} bases, {stations} stations";
            }
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid hab";

            var hab = items[index];

            // Add faction prefix for other factions' habs
            var faction = GameControl.control?.activePlayer;
            if (hab.coreFaction != faction && hab.coreFaction != null)
            {
                return $"[{hab.coreFaction.displayName}] {habReader.ReadSummary(hab)}";
            }

            return habReader.ReadSummary(hab);
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid hab";

            var hab = items[index];
            var sb = new System.Text.StringBuilder();

            // Add faction info for other factions' habs
            var faction = GameControl.control?.activePlayer;
            if (hab.coreFaction != faction && hab.coreFaction != null)
            {
                sb.AppendLine($"Faction: {hab.coreFaction.displayName}");
            }

            sb.Append(habReader.ReadDetail(hab));
            return sb.ToString();
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            var hab = items[index];
            cachedItemIndex = index;

            // Use factory method to ensure consistent callback wiring
            var configuredReader = HabReader.CreateConfigured(
                OnEnterSelectionMode,
                OnSpeak,
                InvalidateCache);

            cachedSections = configuredReader.GetSections(hab);

            return cachedSections;
        }

        /// <summary>
        /// Invalidate cached sections (called after actions modify hab state).
        /// </summary>
        private void InvalidateCache()
        {
            cachedItemIndex = -1;
            cachedSections.Clear();
        }

        public override string GetItemSortName(int index)
        {
            if (index < 0 || index >= items.Count)
                return "";
            return items[index].displayName ?? "";
        }

        public override string GetActivationAnnouncement()
        {
            Refresh();
            if (items.Count == 0)
            {
                return "Habs. No habs available.";
            }
            return $"Habs. {Description}. Press Enter to browse.";
        }
    }
}
