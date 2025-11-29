using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Nations screen - browse nations where you have control points or all nations.
    /// Each nation can be drilled into for detailed info and priority management.
    /// Supports view mode toggle (Tab), letter navigation (A-Z keys), sorting (S key), and faction filtering (F key).
    /// </summary>
    public class NationScreen : ScreenBase
    {
        private List<TINationState> nations = new List<TINationState>();
        private readonly NationReader nationReader = new NationReader();
        private ViewMode viewMode = ViewMode.Mine;

        // Sorting
        private SortNationDataBy currentSort = SortNationDataBy.Alfa;
        private bool sortDescending = false;

        // Faction filtering
        private TIFactionState filterFaction = null; // null = no filter (show all)
        private int filterIndex = 0; // 0=All, 1=Mine, 2=Uncontrolled, 3+=specific factions

        // Pending sort options for callback
        private List<SelectionOption> pendingSortOptions = null;

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

        public override string Name => "Nations";

        /// <summary>
        /// Nations screen supports toggling between your nations and all nations.
        /// </summary>
        public override bool SupportsViewModeToggle => true;

        /// <summary>
        /// Current view mode - your nations vs all nations.
        /// </summary>
        public override ViewMode CurrentViewMode
        {
            get => viewMode;
            set => viewMode = value;
        }

        /// <summary>
        /// Nations screen supports letter navigation for quick jumping.
        /// </summary>
        public override bool SupportsLetterNavigation => true;

        /// <summary>
        /// Get the nation display name for letter navigation.
        /// </summary>
        public override string GetItemSortName(int index)
        {
            if (index < 0 || index >= nations.Count)
                return "";
            return nations[index].displayName ?? "";
        }

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                string baseDesc;

                if (viewMode == ViewMode.Mine && faction != null)
                {
                    int yourNations = nations.Count;
                    int totalCPs = faction.controlPoints?.Count ?? 0;
                    baseDesc = $"{yourNations} nations with {totalCPs} control points";
                }
                else
                {
                    baseDesc = $"All {nations.Count} nations";
                }

                // Add sort info
                string sortInfo = GetCurrentSortDescription();

                // Add filter info
                string filterInfo = "";
                if (filterIndex == 1)
                    filterInfo = ", filtered to mine";
                else if (filterIndex == 2)
                    filterInfo = ", filtered to uncontrolled";
                else if (filterFaction != null)
                    filterInfo = $", filtered to {filterFaction.displayName}";

                return $"{baseDesc}, sorted by {sortInfo}{filterInfo}. Ctrl+S to sort, Ctrl+F to filter.";
            }
        }

        public override void Refresh()
        {
            nations.Clear();
            cachedItemIndex = -1;
            cachedSections.Clear();

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return;

                List<TINationState> allNations;
                if (viewMode == ViewMode.Mine)
                {
                    // Get nations where player has control points
                    allNations = NationReader.GetPlayerNations(faction);
                }
                else
                {
                    // Get all nations
                    allNations = NationReader.GetAllNations();
                }

                // Apply faction filter
                allNations = ApplyFactionFilter(allNations, faction);

                // Apply sorting
                nations = ApplySorting(allNations, faction);

                MelonLogger.Msg($"NationScreen refreshed: {nations.Count} nations in {viewMode} mode, sorted by {currentSort}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing nations screen: {ex.Message}");
            }
        }

        public override IReadOnlyList<object> GetItems()
        {
            return nations.Cast<object>().ToList();
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= nations.Count)
                return "Invalid item";

            var nation = nations[index];
            string summary = nationReader.ReadSummary(nation);

            // In "All" mode, add control status for nations you don't control
            if (viewMode == ViewMode.All)
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    int yourCPs = nation.controlPoints?.Count(cp => cp.faction == faction) ?? 0;
                    if (yourCPs == 0)
                    {
                        // Show who has the most control
                        var topController = GetTopController(nation);
                        if (!string.IsNullOrEmpty(topController))
                        {
                            summary += $", {topController}";
                        }
                    }
                }
            }

            // Add sort-relevant data when not sorting alphabetically
            summary += GetSortRelevantInfo(nation);

            return summary;
        }

        /// <summary>
        /// Get additional info relevant to the current sort field.
        /// </summary>
        private string GetSortRelevantInfo(TINationState nation)
        {
            var faction = GameControl.control?.activePlayer;

            switch (currentSort)
            {
                case SortNationDataBy.Alfa:
                    // No additional info needed for alphabetical
                    return "";

                case SortNationDataBy.MyControlPoints:
                    // Already in base summary
                    return "";

                case SortNationDataBy.HighestPopularity:
                    float highPop = GetHighestPopularity(nation) * 100f;
                    return $", highest {highPop:F0}%";

                case SortNationDataBy.MyPopularity:
                    float myPop = GetMyPopularity(nation, faction) * 100f;
                    return $", my pop {myPop:F0}%";

                case SortNationDataBy.Population:
                    // Already in base summary
                    return "";

                case SortNationDataBy.GDP:
                case SortNationDataBy.Difficulty:
                    return $", ${FormatLargeNumber(nation.GDP)} GDP";

                case SortNationDataBy.PerCapitaGDP:
                    return $", ${nation.perCapitaGDP:N0} per capita";

                case SortNationDataBy.Government:
                    return $", democracy {nation.democracy:F1}";

                case SortNationDataBy.Education:
                    return $", education {nation.education:F1}";

                case SortNationDataBy.Inequality:
                    return $", inequality {nation.inequality:F1}";

                case SortNationDataBy.Cohesion:
                    return $", cohesion {nation.cohesion:F1}";

                case SortNationDataBy.Unrest:
                    return $", unrest {nation.unrest:F1}";

                case SortNationDataBy.Funding:
                    return $", ${FormatLargeNumber(nation.spaceFunding_month)}/mo funding";

                case SortNationDataBy.Research:
                    return $", {nation.research_month:F1}/mo research";

                case SortNationDataBy.Boost:
                    return $", {nation.boostIncome_month_dekatons:F1} dt/mo boost";

                case SortNationDataBy.MissionControl:
                    return $", MC {nation.missionControl:F1}";

                case SortNationDataBy.Miltech:
                    return $", miltech {nation.militaryTechLevel:F1}";

                case SortNationDataBy.Armies:
                    return $", {nation.armies?.Count ?? 0} armies";

                case SortNationDataBy.Navies:
                    return $", {nation.numNavies} navies";

                case SortNationDataBy.STOFighters:
                    return $", {nation.numSTOFighters} STO";

                case SortNationDataBy.NuclearWeapons:
                    return $", {nation.numNuclearWeapons} nukes";

                case SortNationDataBy.InvestmentPoints:
                    return $", {nation.BaseInvestmentPoints_month():F1} IP/mo";

                case SortNationDataBy.Sustainability:
                    return $", sust {TINationState.SustainabilityValueForDisplay(nation.sustainability)}";

                case SortNationDataBy.Wars:
                    int warCount = nation.wars?.Count ?? 0;
                    return warCount > 0 ? $", {warCount} wars" : "";

                default:
                    return "";
            }
        }

        private string FormatLargeNumber(double value)
        {
            if (value >= 1_000_000_000_000)
                return $"{value / 1_000_000_000_000:F1}T";
            if (value >= 1_000_000_000)
                return $"{value / 1_000_000_000:F1}B";
            if (value >= 1_000_000)
                return $"{value / 1_000_000:F1}M";
            return $"{value:N0}";
        }

        /// <summary>
        /// Get the faction with the most control points in a nation.
        /// </summary>
        private string GetTopController(TINationState nation)
        {
            if (nation.controlPoints == null || nation.controlPoints.Count == 0)
                return "uncontrolled";

            var byFaction = nation.controlPoints
                .Where(cp => cp.faction != null)
                .GroupBy(cp => cp.faction)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (byFaction == null)
                return "uncontrolled";

            int count = byFaction.Count();
            int total = nation.numControlPoints;
            string factionName = byFaction.Key.displayName;

            if (count == total)
                return $"controlled by {factionName}";
            else
                return $"{factionName} has {count}/{total}";
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= nations.Count)
                return "Invalid item";

            return nationReader.ReadDetail(nations[index]);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= nations.Count)
                return new List<ISection>();

            // Use cache if available
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            cachedItemIndex = index;
            var nation = nations[index];

            // Get base sections from reader
            cachedSections = nationReader.GetSections(nation);

            // Always add priority grid section - read-only if player has no CPs
            var faction = GameControl.control?.activePlayer;
            if (faction != null && nation.controlPoints != null && nation.controlPoints.Count > 0)
            {
                var yourCPs = nation.controlPoints.Where(cp => cp.faction == faction).ToList();
                bool hasControl = yourCPs.Count > 0;
                cachedSections.Add(CreatePriorityGridSection(nation, faction, readOnly: !hasControl));
            }

            return cachedSections;
        }

        private ISection CreatePriorityGridSection(TINationState nation, TIFactionState faction, bool readOnly = false)
        {
            return new PriorityGridSection(
                nation,
                faction,
                (text) => OnSpeak?.Invoke(text, true),
                OnEnterSelectionMode,
                readOnly
            );
        }

        #region Sorting and Filtering

        /// <summary>
        /// Apply faction filter to nation list.
        /// </summary>
        private List<TINationState> ApplyFactionFilter(List<TINationState> allNations, TIFactionState playerFaction)
        {
            // filterIndex: 0=All, 1=Mine, 2=Uncontrolled, 3+=specific factions
            if (filterIndex == 0)
            {
                // No filter - show all
                return allNations;
            }
            else if (filterIndex == 1)
            {
                // Mine - nations where player has any CP
                return allNations.Where(n =>
                    n.controlPoints?.Any(cp => cp.faction == playerFaction) ?? false
                ).ToList();
            }
            else if (filterIndex == 2)
            {
                // Uncontrolled - nations with no faction controlling executive
                return allNations.Where(n =>
                    n.executiveFaction == null
                ).ToList();
            }
            else if (filterFaction != null)
            {
                // Specific faction - nations where that faction has any CP
                return allNations.Where(n =>
                    n.controlPoints?.Any(cp => cp.faction == filterFaction) ?? false
                ).ToList();
            }

            return allNations;
        }

        /// <summary>
        /// Apply current sort to nation list.
        /// </summary>
        private List<TINationState> ApplySorting(List<TINationState> nations, TIFactionState playerFaction)
        {
            IOrderedEnumerable<TINationState> sorted;

            Func<TINationState, object> keySelector = GetSortKeySelector(playerFaction);

            if (sortDescending)
            {
                sorted = nations.OrderByDescending(keySelector);
            }
            else
            {
                sorted = nations.OrderBy(keySelector);
            }

            return sorted.ToList();
        }

        /// <summary>
        /// Get the key selector function for the current sort type.
        /// </summary>
        private Func<TINationState, object> GetSortKeySelector(TIFactionState playerFaction)
        {
            switch (currentSort)
            {
                case SortNationDataBy.Alfa:
                    return n => n.displayName ?? "";

                case SortNationDataBy.MyControlPoints:
                    return n => n.controlPoints?.Count(cp => cp.faction == playerFaction) ?? 0;

                case SortNationDataBy.HighestPopularity:
                    return n => GetHighestPopularity(n);

                case SortNationDataBy.MyPopularity:
                    return n => GetMyPopularity(n, playerFaction);

                case SortNationDataBy.Population:
                    return n => n.population;

                case SortNationDataBy.GDP:
                case SortNationDataBy.Difficulty:
                    return n => n.GDP;

                case SortNationDataBy.PerCapitaGDP:
                    return n => n.perCapitaGDP;

                case SortNationDataBy.Government:
                    return n => n.democracy;

                case SortNationDataBy.Education:
                    return n => n.education;

                case SortNationDataBy.Inequality:
                    return n => n.inequality;

                case SortNationDataBy.Cohesion:
                    return n => n.cohesion;

                case SortNationDataBy.Unrest:
                    return n => n.unrest;

                case SortNationDataBy.Funding:
                    return n => n.spaceFunding_month;

                case SortNationDataBy.Research:
                    return n => n.research_month;

                case SortNationDataBy.Boost:
                    return n => n.boostIncome_month_dekatons;

                case SortNationDataBy.MissionControl:
                    return n => n.missionControl;

                case SortNationDataBy.Miltech:
                    return n => n.militaryTechLevel;

                case SortNationDataBy.Armies:
                    return n => n.armies?.Count ?? 0;

                case SortNationDataBy.Navies:
                    return n => n.numNavies;

                case SortNationDataBy.STOFighters:
                    return n => n.numSTOFighters;

                case SortNationDataBy.NuclearWeapons:
                    return n => n.numNuclearWeapons;

                case SortNationDataBy.InvestmentPoints:
                    return n => n.BaseInvestmentPoints_month();

                case SortNationDataBy.Sustainability:
                    return n => n.sustainability;

                case SortNationDataBy.Wars:
                    return n => n.wars?.Count ?? 0;

                default:
                    return n => n.displayName ?? "";
            }
        }

        private float GetHighestPopularity(TINationState nation)
        {
            try
            {
                var activeIdeologies = GameStateManager.ActiveHumanIdeologies();
                if (activeIdeologies == null)
                    return 0;

                return activeIdeologies
                    .Where(t => t.ideology != FactionIdeology.Undecided)
                    .Max(t => nation.GetPublicOpinionOfFaction(t.ideology));
            }
            catch
            {
                return 0;
            }
        }

        private float GetMyPopularity(TINationState nation, TIFactionState faction)
        {
            try
            {
                if (faction?.ideology == null)
                    return 0;
                return nation.GetPublicOpinionOfFaction(faction.ideology.ideology);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Start the sort selection process with category menu.
        /// </summary>
        public void StartSortSelection()
        {
            var categories = new List<SelectionOption>
            {
                new SelectionOption { Label = "Basic", DetailText = "Alphabetical, Control Points, Population, Wars", Data = "Basic" },
                new SelectionOption { Label = "Economic", DetailText = "GDP, Per Capita, Investment Points, Sustainability", Data = "Economic" },
                new SelectionOption { Label = "Social", DetailText = "Government, Education, Inequality, Cohesion, Unrest", Data = "Social" },
                new SelectionOption { Label = "Space", DetailText = "Funding, Research, Boost, Mission Control", Data = "Space" },
                new SelectionOption { Label = "Military", DetailText = "Miltech, Armies, Navies, STO Fighters, Nuclear", Data = "Military" },
                new SelectionOption { Label = "Faction Control", DetailText = "Highest Popularity, My Popularity", Data = "Faction" }
            };

            OnEnterSelectionMode?.Invoke("Select sort category", categories, SelectSortCategory);
        }

        private void SelectSortCategory(int index)
        {
            var categories = new[] { "Basic", "Economic", "Social", "Space", "Military", "Faction" };
            if (index < 0 || index >= categories.Length)
                return;

            pendingSortOptions = GetSortOptionsForCategory(categories[index]);
            OnEnterSelectionMode?.Invoke($"Select {categories[index]} sort", pendingSortOptions, ApplySortSelection);
        }

        private List<SelectionOption> GetSortOptionsForCategory(string category)
        {
            var options = new List<SelectionOption>();

            switch (category)
            {
                case "Basic":
                    options.Add(CreateSortOption("Alphabetical", SortNationDataBy.Alfa));
                    options.Add(CreateSortOption("My Control Points", SortNationDataBy.MyControlPoints));
                    options.Add(CreateSortOption("Population", SortNationDataBy.Population));
                    options.Add(CreateSortOption("Wars", SortNationDataBy.Wars));
                    break;

                case "Economic":
                    options.Add(CreateSortOption("GDP", SortNationDataBy.GDP));
                    options.Add(CreateSortOption("Per Capita GDP", SortNationDataBy.PerCapitaGDP));
                    options.Add(CreateSortOption("Investment Points", SortNationDataBy.InvestmentPoints));
                    options.Add(CreateSortOption("Sustainability", SortNationDataBy.Sustainability));
                    break;

                case "Social":
                    options.Add(CreateSortOption("Government (Democracy)", SortNationDataBy.Government));
                    options.Add(CreateSortOption("Education", SortNationDataBy.Education));
                    options.Add(CreateSortOption("Inequality", SortNationDataBy.Inequality));
                    options.Add(CreateSortOption("Cohesion", SortNationDataBy.Cohesion));
                    options.Add(CreateSortOption("Unrest", SortNationDataBy.Unrest));
                    break;

                case "Space":
                    options.Add(CreateSortOption("Space Funding", SortNationDataBy.Funding));
                    options.Add(CreateSortOption("Research", SortNationDataBy.Research));
                    options.Add(CreateSortOption("Boost", SortNationDataBy.Boost));
                    options.Add(CreateSortOption("Mission Control", SortNationDataBy.MissionControl));
                    break;

                case "Military":
                    options.Add(CreateSortOption("Military Tech", SortNationDataBy.Miltech));
                    options.Add(CreateSortOption("Armies", SortNationDataBy.Armies));
                    options.Add(CreateSortOption("Navies", SortNationDataBy.Navies));
                    options.Add(CreateSortOption("STO Fighters", SortNationDataBy.STOFighters));
                    options.Add(CreateSortOption("Nuclear Weapons", SortNationDataBy.NuclearWeapons));
                    break;

                case "Faction":
                    options.Add(CreateSortOption("Highest Popularity", SortNationDataBy.HighestPopularity));
                    options.Add(CreateSortOption("My Popularity", SortNationDataBy.MyPopularity));
                    break;
            }

            return options;
        }

        private SelectionOption CreateSortOption(string label, SortNationDataBy sortBy)
        {
            string currentMarker = (currentSort == sortBy) ? " (current)" : "";
            return new SelectionOption
            {
                Label = $"{label}{currentMarker}",
                DetailText = $"Sort nations by {label.ToLower()}",
                Data = sortBy
            };
        }

        private void ApplySortSelection(int index)
        {
            if (pendingSortOptions == null || index < 0 || index >= pendingSortOptions.Count)
                return;

            var selectedOption = pendingSortOptions[index];
            if (selectedOption.Data is SortNationDataBy sortBy)
            {
                ApplySort(sortBy);
            }

            pendingSortOptions = null;
        }

        /// <summary>
        /// Apply a sort option selected from the menu.
        /// </summary>
        public void ApplySort(SortNationDataBy sortBy)
        {
            // If selecting the same sort, toggle direction
            if (currentSort == sortBy)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                currentSort = sortBy;
                // Default to descending for numeric fields, ascending for alphabetical
                sortDescending = (sortBy != SortNationDataBy.Alfa);
            }

            Refresh();
            string direction = sortDescending ? "descending" : "ascending";
            OnSpeak?.Invoke($"Sorted by {GetSortDisplayName(sortBy)}, {direction}", true);
        }

        /// <summary>
        /// Cycle to the next faction filter.
        /// </summary>
        public void CycleFactionFilter()
        {
            var factions = GetAllFactions();
            int maxIndex = 2 + factions.Count; // All, Mine, Uncontrolled, + each faction

            filterIndex = (filterIndex + 1) % (maxIndex + 1);

            if (filterIndex == 0)
            {
                filterFaction = null;
                OnSpeak?.Invoke("Filter: All nations", true);
            }
            else if (filterIndex == 1)
            {
                filterFaction = null;
                OnSpeak?.Invoke("Filter: Nations I control", true);
            }
            else if (filterIndex == 2)
            {
                filterFaction = null;
                OnSpeak?.Invoke("Filter: Uncontrolled nations", true);
            }
            else
            {
                int factionIdx = filterIndex - 3;
                if (factionIdx >= 0 && factionIdx < factions.Count)
                {
                    filterFaction = factions[factionIdx];
                    OnSpeak?.Invoke($"Filter: {filterFaction.displayName} nations", true);
                }
            }

            Refresh();
        }

        private List<TIFactionState> GetAllFactions()
        {
            try
            {
                return GameStateManager.AllHumanFactions()?.ToList() ?? new List<TIFactionState>();
            }
            catch
            {
                return new List<TIFactionState>();
            }
        }

        /// <summary>
        /// Get display name for a sort type.
        /// </summary>
        public string GetSortDisplayName(SortNationDataBy sortBy)
        {
            switch (sortBy)
            {
                case SortNationDataBy.Alfa: return "Alphabetical";
                case SortNationDataBy.MyControlPoints: return "My Control Points";
                case SortNationDataBy.HighestPopularity: return "Highest Popularity";
                case SortNationDataBy.MyPopularity: return "My Popularity";
                case SortNationDataBy.Population: return "Population";
                case SortNationDataBy.GDP: return "GDP";
                case SortNationDataBy.Difficulty: return "Difficulty";
                case SortNationDataBy.PerCapitaGDP: return "Per Capita GDP";
                case SortNationDataBy.Government: return "Government";
                case SortNationDataBy.Education: return "Education";
                case SortNationDataBy.Inequality: return "Inequality";
                case SortNationDataBy.Cohesion: return "Cohesion";
                case SortNationDataBy.Unrest: return "Unrest";
                case SortNationDataBy.Funding: return "Space Funding";
                case SortNationDataBy.Research: return "Research";
                case SortNationDataBy.Boost: return "Boost";
                case SortNationDataBy.MissionControl: return "Mission Control";
                case SortNationDataBy.Miltech: return "Military Tech";
                case SortNationDataBy.Armies: return "Armies";
                case SortNationDataBy.Navies: return "Navies";
                case SortNationDataBy.STOFighters: return "STO Fighters";
                case SortNationDataBy.NuclearWeapons: return "Nuclear Weapons";
                case SortNationDataBy.InvestmentPoints: return "Investment Points";
                case SortNationDataBy.Sustainability: return "Sustainability";
                case SortNationDataBy.Wars: return "Wars";
                default: return sortBy.ToString();
            }
        }

        /// <summary>
        /// Get the current sort description for announcements.
        /// </summary>
        public string GetCurrentSortDescription()
        {
            string direction = sortDescending ? "descending" : "ascending";
            return $"{GetSortDisplayName(currentSort)}, {direction}";
        }

        #endregion

        #region Priority Management (Legacy - kept for reference)

        private ISection CreatePriorityActionsSection(TINationState nation, List<TIControlPoint> yourCPs, TIFactionState faction)
        {
            var section = new DataSection("Priorities");

            // Add investment summary at the top
            AddInvestmentSummary(section, nation, yourCPs, faction);

            // Show each valid priority with current values and progress
            var validPriorities = nation.ValidPriorities;

            foreach (var priority in validPriorities)
            {
                string displayName = GetPriorityDisplayName(priority);
                string yourValues = GetPriorityValuesForCPs(priority, yourCPs);

                // Build concise label: "Unity: Your CPs at 0"
                string cpLabel = yourCPs.Count == 1 ? "Your CP at" : "Your CPs at";
                string label = $"{displayName}: {cpLabel} {yourValues}";

                // Build concise value showing nation allocation and effect
                string valueInfo = GetPriorityValueInfo(nation, priority);

                // Full tooltip only on detail read
                string detail = GetPriorityDetailText(nation, priority, yourCPs);

                // Activating a priority lets you set its value
                section.AddItem(label, valueInfo, detail,
                    onActivate: () => AdjustPriority(priority, yourCPs, nation, faction));
            }

            // Add preset option at the end
            section.AddItem("Apply Preset", "Apply a priority preset to all control points",
                onActivate: () => StartPresetSelection(yourCPs, nation, faction));

            return section;
        }

        private void AddInvestmentSummary(DataSection section, TINationState nation, List<TIControlPoint> yourCPs, TIFactionState faction)
        {
            try
            {
                float nationIP = nation.BaseInvestmentPoints_month();
                float yourIP = nation.BaseInvestmentPoints_month(faction);
                int yourCPCount = yourCPs.Count;
                int totalCPs = nation.numControlPoints;

                string summaryLabel = "Investment Points";
                string summaryValue = $"Nation: {nationIP:F1} IP/month, You: {yourIP:F1} IP ({yourCPCount}/{totalCPs} CPs)";

                // Get full investment tooltip for detail
                string investmentTooltip = TISpeechMod.CleanText(NationInfoController.BuildInvestmentTooltip(nation));

                section.AddItem(summaryLabel, summaryValue, investmentTooltip);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not build investment summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Get concise value info for a priority: nation allocation + brief effect.
        /// </summary>
        private string GetPriorityValueInfo(TINationState nation, PriorityType priority)
        {
            var sb = new System.Text.StringBuilder();

            try
            {
                // Nation allocation percentage
                float weightPercent = nation.percentWeighttoPriority(priority) * 100f;
                sb.Append($"Nation allocates {weightPercent:F0}%");

                // For accumulating priorities, show progress
                if (IsAccumulatingPriority(priority))
                {
                    float accumulated = nation.GetAccumulatedInvestmentPoints(priority);
                    float required = nation.GetRequiredInvestmentPointsForPriority(priority);
                    if (required > 0)
                    {
                        float progressPercent = (accumulated / required) * 100f;
                        sb.Append($", {progressPercent:F0}% complete");
                    }
                }

                // Brief effect summary (cleaned)
                string effectSummary = TISpeechMod.CleanText(
                    NationInfoController.PrioritySummaryString(priority, nation, includeIPSymbol: false));
                if (!string.IsNullOrWhiteSpace(effectSummary))
                {
                    sb.Append($". Effect: {effectSummary}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not build priority value info: {ex.Message}");
            }

            return sb.ToString();
        }

        private string GetPriorityValuesForCPs(PriorityType priority, List<TIControlPoint> cps)
        {
            if (cps.Count == 1)
            {
                return cps[0].GetControlPointPriority(priority, checkValid: true).ToString();
            }

            // Multiple CPs - show all values
            var values = cps.Select(cp => cp.GetControlPointPriority(priority, checkValid: true));
            return string.Join(", ", values);
        }

        private string GetPriorityProgressInfo(TINationState nation, PriorityType priority)
        {
            try
            {
                float accumulated = nation.GetAccumulatedInvestmentPoints(priority);
                float required = nation.GetRequiredInvestmentPointsForPriority(priority);

                // Only show progress for priorities that accumulate toward something
                if (required > 0 && IsAccumulatingPriority(priority))
                {
                    float percent = (accumulated / required) * 100f;
                    return $"{percent:F0}%";
                }

                // For ongoing priorities, show the weight percentage
                float weightPercent = nation.percentWeighttoPriority(priority) * 100f;
                if (weightPercent > 0)
                {
                    return $"{weightPercent:F0}% of investment";
                }
            }
            catch { }

            return "";
        }

        private string GetPriorityDetailText(TINationState nation, PriorityType priority, List<TIControlPoint> cps)
        {
            var sb = new System.Text.StringBuilder();

            try
            {
                // Get the game's actual tooltip text - this has all the detailed effects
                var faction = GameControl.control?.activePlayer;
                string priorityLine = GetPriorityDisplayName(priority);
                string gameTooltip = PriorityListItemController.priorityTipStr(faction, nation, priority, priorityLine);

                // Clean up the tooltip (remove sprite tags, etc.)
                gameTooltip = TISpeechMod.CleanText(gameTooltip);
                sb.AppendLine(gameTooltip);

                // Add per-CP values if multiple CPs
                if (cps.Count > 1)
                {
                    sb.AppendLine();
                    sb.AppendLine("Values per CP:");
                    for (int i = 0; i < cps.Count; i++)
                    {
                        int value = cps[i].GetControlPointPriority(priority, checkValid: true);
                        string cpName = GetControlPointDisplayName(cps[i], nation);
                        sb.AppendLine($"  {cpName}: {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to basic info if tooltip fails
                MelonLogger.Warning($"Could not get priority tooltip: {ex.Message}");
                string displayName = GetPriorityDisplayName(priority);
                sb.AppendLine(displayName);

                float weightPercent = nation.percentWeighttoPriority(priority) * 100f;
                sb.AppendLine($"Investment share: {weightPercent:F1}%");
            }

            return sb.ToString();
        }

        private bool IsAccumulatingPriority(PriorityType priority)
        {
            // These priorities accumulate toward completing a project
            switch (priority)
            {
                case PriorityType.Civilian_InitiateSpaceflightProgram:
                case PriorityType.Military_FoundMilitary:
                case PriorityType.Military_BuildArmy:
                case PriorityType.Military_BuildNavy:
                case PriorityType.Military_InitiateNuclearProgram:
                case PriorityType.Military_BuildNuclearWeapons:
                case PriorityType.Military_BuildSpaceDefenses:
                case PriorityType.Military_BuildSTOSquadron:
                    return true;
                default:
                    return false;
            }
        }

        private void AdjustPriority(PriorityType priority, List<TIControlPoint> cps, TINationState nation, TIFactionState faction)
        {
            string priorityName = GetPriorityDisplayName(priority);

            if (cps.Count == 1)
            {
                // Single CP - go straight to value selection
                SelectValueForCP(priority, cps[0], nation, faction);
                return;
            }

            // Multiple CPs - offer per-CP selection or bulk
            var options = new List<SelectionOption>();

            // Add option for each CP
            for (int i = 0; i < cps.Count; i++)
            {
                var cp = cps[i];
                int currentValue = cp.GetControlPointPriority(priority, checkValid: true);
                string cpName = GetControlPointDisplayName(cp, nation);

                options.Add(new SelectionOption
                {
                    Label = $"{cpName}: {currentValue}",
                    DetailText = $"Adjust {priorityName} for {cpName}, currently {currentValue}",
                    Data = cp
                });
            }

            // Add bulk option
            options.Add(new SelectionOption
            {
                Label = "Set all CPs to same value",
                DetailText = $"Set {priorityName} to the same value for all {cps.Count} control points",
                Data = null // null indicates bulk mode
            });

            OnEnterSelectionMode?.Invoke(
                $"Adjust {priorityName}",
                options,
                (index) =>
                {
                    if (options[index].Data == null)
                    {
                        // Bulk mode - select value for all
                        SelectValueForAllCPs(priority, cps, nation, faction);
                    }
                    else
                    {
                        // Single CP
                        var selectedCP = (TIControlPoint)options[index].Data;
                        SelectValueForCP(priority, selectedCP, nation, faction);
                    }
                }
            );
        }

        private void SelectValueForCP(PriorityType priority, TIControlPoint cp, TINationState nation, TIFactionState faction)
        {
            string priorityName = GetPriorityDisplayName(priority);
            int currentValue = cp.GetControlPointPriority(priority, checkValid: true);

            var options = new List<SelectionOption>();
            for (int i = 0; i <= 3; i++)
            {
                string label = i == currentValue ? $"{i} (current)" : i.ToString();
                options.Add(new SelectionOption
                {
                    Label = label,
                    DetailText = $"Set {priorityName} to {i}",
                    Data = i
                });
            }

            string cpName = GetControlPointDisplayName(cp, nation);

            OnEnterSelectionMode?.Invoke(
                $"Set {priorityName} for {cpName}",
                options,
                (index) =>
                {
                    int newValue = (int)options[index].Data;
                    ExecuteSetPriority(cp, priority, newValue, faction);
                }
            );
        }

        private void SelectValueForAllCPs(PriorityType priority, List<TIControlPoint> cps, TINationState nation, TIFactionState faction)
        {
            string priorityName = GetPriorityDisplayName(priority);

            // Check if all CPs have the same value
            var currentValues = cps.Select(cp => cp.GetControlPointPriority(priority, checkValid: true)).ToList();
            bool allSame = currentValues.Distinct().Count() == 1;
            int commonValue = allSame ? currentValues[0] : -1;

            var options = new List<SelectionOption>();
            for (int i = 0; i <= 3; i++)
            {
                string label = (allSame && i == commonValue) ? $"{i} (current)" : i.ToString();
                options.Add(new SelectionOption
                {
                    Label = label,
                    DetailText = $"Set {priorityName} to {i} for all {cps.Count} control points",
                    Data = i
                });
            }

            OnEnterSelectionMode?.Invoke(
                $"Set {priorityName} for all CPs",
                options,
                (index) =>
                {
                    int newValue = (int)options[index].Data;
                    ExecuteSetPriorityForCPs(priority, newValue, cps, faction);
                }
            );
        }

        private void ExecuteSetPriority(TIControlPoint cp, PriorityType priority, int value, TIFactionState faction)
        {
            try
            {
                var action = new SetPriorityAction(cp, faction, priority, value, onlyIfHigher: false, skipUpdates: false);
                faction.playerControl.StartAction(action);

                string priorityName = GetPriorityDisplayName(priority);
                OnSpeak?.Invoke($"{priorityName} set to {value}", true);
                MelonLogger.Msg($"Set priority {priority} to {value} on control point");

                // Invalidate cache
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting priority: {ex.Message}");
                OnSpeak?.Invoke("Error setting priority", true);
            }
        }

        private void ExecuteSetPriorityForCPs(PriorityType priority, int value, List<TIControlPoint> cps, TIFactionState faction)
        {
            try
            {
                foreach (var cp in cps)
                {
                    var action = new SetPriorityAction(cp, faction, priority, value, onlyIfHigher: false, skipUpdates: false);
                    faction.playerControl.StartAction(action);
                }

                string priorityName = GetPriorityDisplayName(priority);
                OnSpeak?.Invoke($"{priorityName} set to {value} for {cps.Count} CPs", true);
                MelonLogger.Msg($"Set priority {priority} to {value} on {cps.Count} control point(s)");

                // Invalidate cache
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting priority: {ex.Message}");
                OnSpeak?.Invoke("Error setting priority", true);
            }
        }

        private void StartPresetSelection(List<TIControlPoint> cps, TINationState nation, TIFactionState faction)
        {
            // Get valid presets from the game's template system
            var validPresets = TemplateManager.IterateByClass<TIPriorityPresetTemplate>()
                .Where(p => p.ValidPreset(nation, faction))
                .OrderByDescending(p => p.customDesign)
                .ToList();

            if (validPresets.Count == 0)
            {
                OnSpeak?.Invoke("No valid presets available for this nation", true);
                return;
            }

            var options = new List<SelectionOption>();

            foreach (var preset in validPresets)
            {
                // Build description from preset settings
                var settings = preset.GetAllSettings();
                var activeSettings = settings.Where(s => s.Value > 0)
                    .Select(s => $"{GetPriorityShortName(s.Key)}:{s.Value}");
                string description = activeSettings.Any()
                    ? string.Join(", ", activeSettings)
                    : "All priorities at 0";

                options.Add(new SelectionOption
                {
                    Label = preset.displayName,
                    DetailText = description,
                    Data = preset
                });
            }

            OnEnterSelectionMode?.Invoke(
                $"Apply preset to {cps.Count} control points",
                options,
                (index) =>
                {
                    var selectedPreset = (TIPriorityPresetTemplate)options[index].Data;
                    ApplyPresetToAllCPs(cps, selectedPreset, faction);
                }
            );
        }

        private void ApplyPresetToAllCPs(List<TIControlPoint> cps, TIPriorityPresetTemplate preset, TIFactionState faction)
        {
            try
            {
                foreach (var cp in cps)
                {
                    // Use the game's action to apply the preset
                    var action = new ApplyPriorityPresetToControlPoint(cp, faction, preset.dataName);
                    faction.playerControl.StartAction(action);
                }

                OnSpeak?.Invoke($"Applied {preset.displayName} to {cps.Count} control points", true);
                MelonLogger.Msg($"Applied preset '{preset.displayName}' to {cps.Count} control points");

                // Invalidate cache
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying preset: {ex.Message}");
                OnSpeak?.Invoke("Error applying preset", true);
            }
        }

        #endregion

        #region Helpers

        private string GetControlPointDisplayName(TIControlPoint cp, TINationState nation)
        {
            int index = nation.controlPoints?.IndexOf(cp) ?? 0;
            string typeName = cp.controlPointType.ToString();
            return $"CP {index + 1} ({typeName})";
        }

        private string GetCurrentPrioritiesSummary(TIControlPoint cp)
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

            return priorities.Count > 0 ? string.Join(", ", priorities) : "None";
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
    }
}
