using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Sort options for space bodies (matching game's SortSpaceDataBy enum).
    /// </summary>
    public enum SortSpaceBodyBy
    {
        Orbit,          // By distance from Sun (semi-major axis)
        Alfa,           // Alphabetical by name
        Size,           // By radius/dimension
        Habs,           // By number of hab sites
        Water,          // By water mining potential
        Volatiles,      // By volatiles mining potential
        Metals,         // By metals mining potential
        Nobles,         // By noble metals mining potential
        Fissiles,       // By fissiles mining potential
        Solar           // By solar collection potential
    }

    /// <summary>
    /// Space Bodies screen - browse planets, moons, asteroids, and other celestial bodies.
    /// Default view shows bodies where you have presence; toggle to see all bodies.
    /// Supports sorting (Ctrl+S) similar to the Intel solar system view.
    /// </summary>
    public class SpaceBodiesScreen : ScreenBase
    {
        private List<TISpaceBodyState> items = new List<TISpaceBodyState>();
        private readonly SpaceBodyReader bodyReader = new SpaceBodyReader();

        // View mode: false = your presence only, true = all bodies
        private bool showAllMode = false;

        // Sorting
        private SortSpaceBodyBy currentSort = SortSpaceBodyBy.Orbit;
        private bool sortDescending = false;

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

        /// <summary>
        /// Callback for entering transfer planner from a specific orbit.
        /// </summary>
        public Action<TIOrbitState> OnEnterTransferFromOrbit { get; set; }

        public override string Name => "Space Bodies";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                string baseDesc;

                if (faction != null)
                {
                    if (showAllMode)
                    {
                        int planets = items.Count(b => b.objectType == SpaceObjectType.Planet);
                        int moons = items.Count(b => b.objectType == SpaceObjectType.PlanetaryMoon || b.objectType == SpaceObjectType.AsteroidalMoon);
                        int asteroids = items.Count(b => b.objectType == SpaceObjectType.Asteroid || b.objectType == SpaceObjectType.DwarfPlanet || b.objectType == SpaceObjectType.Comet);
                        baseDesc = $"All bodies: {planets} planets, {moons} moons, {asteroids} asteroids";
                    }
                    else
                    {
                        int count = SpaceBodyReader.GetBodiesWithPlayerPresence(faction).Count;
                        baseDesc = $"{count} bod{(count != 1 ? "ies" : "y")} with your presence";
                    }
                }
                else
                {
                    baseDesc = "Browse celestial bodies in the solar system";
                }

                // Add sort info
                string sortInfo = GetCurrentSortDescription();
                return $"{baseDesc}, sorted by {sortInfo}. Ctrl+S to sort.";
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

                List<TISpaceBodyState> allBodies;
                if (showAllMode)
                {
                    allBodies = SpaceBodyReader.GetAllBodiesHierarchical();
                }
                else
                {
                    allBodies = SpaceBodyReader.GetBodiesWithPlayerPresence(faction);
                }

                // Apply sorting
                items = ApplySorting(allBodies, faction);

                MelonLogger.Msg($"SpaceBodiesScreen refreshed: {items.Count} bodies in {(showAllMode ? "all" : "presence")} mode, sorted by {currentSort}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing space bodies: {ex.Message}");
            }
        }

        public override string ToggleViewMode()
        {
            showAllMode = !showAllMode;
            Refresh();

            if (showAllMode)
            {
                int planets = items.Count(b => b.objectType == SpaceObjectType.Planet);
                int moons = items.Count(b => b.objectType == SpaceObjectType.PlanetaryMoon || b.objectType == SpaceObjectType.AsteroidalMoon);
                int asteroids = items.Count(b => b.objectType == SpaceObjectType.Asteroid || b.objectType == SpaceObjectType.DwarfPlanet || b.objectType == SpaceObjectType.Comet);
                return $"Showing all bodies: {planets} planets, {moons} moons, {asteroids} asteroids";
            }
            else
            {
                return $"Showing your presence only: {items.Count} bod{(items.Count != 1 ? "ies" : "y")}";
            }
        }

        public override string ReadItemSummary(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid body";

            var body = items[index];
            string summary = bodyReader.ReadSummary(body);

            // Add sort-relevant info
            summary += GetSortRelevantInfo(body);

            // Add indentation for moons (bodies orbiting non-star parents)
            if (body.barycenter != null && body.barycenter.objectType != SpaceObjectType.Star)
            {
                return $"  {summary}";
            }

            return summary;
        }

        public override string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid body";

            return bodyReader.ReadDetail(items[index]);
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            // Wire up callbacks for probe actions and transfer planner
            bodyReader.OnEnterSelectionMode = OnEnterSelectionMode;
            bodyReader.OnSpeak = OnSpeak;
            bodyReader.OnEnterTransferFromOrbit = OnEnterTransferFromOrbit;

            var body = items[index];
            cachedItemIndex = index;
            cachedSections = bodyReader.GetSections(body);

            return cachedSections;
        }

        /// <summary>
        /// Get sections for a drilled-into section item (e.g., a hab/station in the Hab Sites section).
        /// </summary>
        public override IReadOnlyList<ISection> GetSectionsForSectionItem(string secondaryId)
        {
            if (string.IsNullOrEmpty(secondaryId))
                return new List<ISection>();

            // The secondaryId is a hab ID - get the hab and return its sections
            if (int.TryParse(secondaryId, out int habId))
            {
                var hab = HabReader.GetHabById(habId);
                if (hab != null)
                {
                    var habReader = new HabReader();
                    return habReader.GetSections(hab);
                }
            }

            return base.GetSectionsForSectionItem(secondaryId);
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
                if (showAllMode)
                {
                    return "Space Bodies. No bodies found.";
                }
                else
                {
                    return "Space Bodies. No bodies with your presence. Press Tab to show all bodies.";
                }
            }
            return $"Space Bodies. {Description}. Press Enter to browse, Tab to toggle view.";
        }

        #region Sorting

        /// <summary>
        /// Apply current sort to bodies list.
        /// </summary>
        private List<TISpaceBodyState> ApplySorting(List<TISpaceBodyState> bodies, TIFactionState playerFaction)
        {
            IOrderedEnumerable<TISpaceBodyState> sorted;
            Func<TISpaceBodyState, object> keySelector = GetSortKeySelector(playerFaction);

            if (sortDescending)
            {
                sorted = bodies.OrderByDescending(keySelector);
            }
            else
            {
                sorted = bodies.OrderBy(keySelector);
            }

            return sorted.ToList();
        }

        /// <summary>
        /// Get the key selector function for the current sort type.
        /// </summary>
        private Func<TISpaceBodyState, object> GetSortKeySelector(TIFactionState playerFaction)
        {
            switch (currentSort)
            {
                case SortSpaceBodyBy.Orbit:
                    // Sort by semi-major axis, with moons sorted after their parent
                    return b =>
                    {
                        double baseAxis = b.semiMajorAxis_AU;
                        if (b.barycenter != null && b.barycenter.objectType != SpaceObjectType.Star)
                        {
                            // Moon - add parent's axis to sort after parent
                            baseAxis = b.barycenter.semiMajorAxis_AU + (b.semiMajorAxis_AU / 1000.0);
                        }
                        return baseAxis;
                    };

                case SortSpaceBodyBy.Alfa:
                    return b => b.displayName ?? "";

                case SortSpaceBodyBy.Size:
                    return b => b.meanRadius_km;

                case SortSpaceBodyBy.Habs:
                    return b => b.habSites?.Length ?? 0;

                case SortSpaceBodyBy.Water:
                    return b => GetMiningRating(b, FactionResource.Water, playerFaction);

                case SortSpaceBodyBy.Volatiles:
                    return b => GetMiningRating(b, FactionResource.Volatiles, playerFaction);

                case SortSpaceBodyBy.Metals:
                    return b => GetMiningRating(b, FactionResource.Metals, playerFaction);

                case SortSpaceBodyBy.Nobles:
                    return b => GetMiningRating(b, FactionResource.NobleMetals, playerFaction);

                case SortSpaceBodyBy.Fissiles:
                    return b => GetMiningRating(b, FactionResource.Fissiles, playerFaction);

                case SortSpaceBodyBy.Solar:
                    return b => GetSolarPotential(b);

                default:
                    return b => b.displayName ?? "";
            }
        }

        /// <summary>
        /// Get mining rating for a resource on a body.
        /// </summary>
        private int GetMiningRating(TISpaceBodyState body, FactionResource resource, TIFactionState faction)
        {
            try
            {
                if (body.habSites == null || body.habSites.Length == 0)
                    return 0;

                bool prospected = faction?.Prospected(body) ?? false;
                return (int)body.GetSiteProfileRating(resource, prospected);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get solar power potential for a body.
        /// </summary>
        private float GetSolarPotential(TISpaceBodyState body)
        {
            try
            {
                if (body.orbits == null || body.orbits.Count == 0)
                    return 0;

                return TIHabModuleState.NaturalSolarPowerMultiplier(body.orbits.MaxBy(x => x.solarMultiplier));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get additional info relevant to the current sort field.
        /// </summary>
        private string GetSortRelevantInfo(TISpaceBodyState body)
        {
            var faction = GameControl.control?.activePlayer;

            switch (currentSort)
            {
                case SortSpaceBodyBy.Orbit:
                case SortSpaceBodyBy.Alfa:
                    // No additional info for these
                    return "";

                case SortSpaceBodyBy.Size:
                    return $", radius {body.meanRadius_km:N0} km";

                case SortSpaceBodyBy.Habs:
                    int sites = body.habSites?.Length ?? 0;
                    return sites > 0 ? $", {sites} site{(sites != 1 ? "s" : "")}" : "";

                case SortSpaceBodyBy.Water:
                    return GetMiningRatingString(body, FactionResource.Water, "water", faction);

                case SortSpaceBodyBy.Volatiles:
                    return GetMiningRatingString(body, FactionResource.Volatiles, "volatiles", faction);

                case SortSpaceBodyBy.Metals:
                    return GetMiningRatingString(body, FactionResource.Metals, "metals", faction);

                case SortSpaceBodyBy.Nobles:
                    return GetMiningRatingString(body, FactionResource.NobleMetals, "noble metals", faction);

                case SortSpaceBodyBy.Fissiles:
                    return GetMiningRatingString(body, FactionResource.Fissiles, "fissiles", faction);

                case SortSpaceBodyBy.Solar:
                    float solar = GetSolarPotential(body);
                    return solar > 0 ? $", solar {solar:P0}" : "";

                default:
                    return "";
            }
        }

        /// <summary>
        /// Get a formatted string for mining rating.
        /// </summary>
        private string GetMiningRatingString(TISpaceBodyState body, FactionResource resource, string label, TIFactionState faction)
        {
            try
            {
                if (body.habSites == null || body.habSites.Length == 0)
                    return "";

                bool prospected = faction?.Prospected(body) ?? false;
                var rating = body.GetSiteProfileRating(resource, prospected);

                if (rating == SiteProfileRating.empty)
                    return "";

                string ratingStr = rating switch
                {
                    SiteProfileRating.possible => "possible",
                    SiteProfileRating.low => "low",
                    SiteProfileRating.medium => "medium",
                    SiteProfileRating.high => "high",
                    SiteProfileRating.max => "max",
                    _ => ""
                };

                return string.IsNullOrEmpty(ratingStr) ? "" : $", {label} {ratingStr}";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Start the sort selection process.
        /// </summary>
        public void StartSortSelection()
        {
            var options = new List<SelectionOption>
            {
                CreateSortOption("Orbit", SortSpaceBodyBy.Orbit, "Distance from Sun"),
                CreateSortOption("Alphabetical", SortSpaceBodyBy.Alfa, "Sort by name"),
                CreateSortOption("Size", SortSpaceBodyBy.Size, "By radius"),
                CreateSortOption("Hab Sites", SortSpaceBodyBy.Habs, "Number of buildable sites"),
                CreateSortOption("Water", SortSpaceBodyBy.Water, "Water mining potential"),
                CreateSortOption("Volatiles", SortSpaceBodyBy.Volatiles, "Volatiles mining potential"),
                CreateSortOption("Metals", SortSpaceBodyBy.Metals, "Metals mining potential"),
                CreateSortOption("Noble Metals", SortSpaceBodyBy.Nobles, "Noble metals mining potential"),
                CreateSortOption("Fissiles", SortSpaceBodyBy.Fissiles, "Fissiles mining potential"),
                CreateSortOption("Solar", SortSpaceBodyBy.Solar, "Solar power collection potential")
            };

            pendingSortOptions = options;
            OnEnterSelectionMode?.Invoke("Sort space bodies by", options, ApplySortSelection);
        }

        private SelectionOption CreateSortOption(string label, SortSpaceBodyBy sortBy, string description)
        {
            string currentMarker = (currentSort == sortBy) ? " (current)" : "";
            return new SelectionOption
            {
                Label = $"{label}{currentMarker}",
                DetailText = description,
                Data = sortBy
            };
        }

        private void ApplySortSelection(int index)
        {
            if (pendingSortOptions == null || index < 0 || index >= pendingSortOptions.Count)
                return;

            var selectedOption = pendingSortOptions[index];
            if (selectedOption.Data is SortSpaceBodyBy sortBy)
            {
                ApplySort(sortBy);
            }

            pendingSortOptions = null;
        }

        /// <summary>
        /// Apply a sort option.
        /// </summary>
        public void ApplySort(SortSpaceBodyBy sortBy)
        {
            // If selecting the same sort, toggle direction
            if (currentSort == sortBy)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                currentSort = sortBy;
                // Default to ascending for orbit/alphabetical, descending for numeric/resource values
                sortDescending = (sortBy != SortSpaceBodyBy.Orbit && sortBy != SortSpaceBodyBy.Alfa);
            }

            Refresh();
            string direction = sortDescending ? "descending" : "ascending";
            OnSpeak?.Invoke($"Sorted by {GetSortDisplayName(sortBy)}, {direction}", true);
        }

        /// <summary>
        /// Get display name for a sort type.
        /// </summary>
        public string GetSortDisplayName(SortSpaceBodyBy sortBy)
        {
            switch (sortBy)
            {
                case SortSpaceBodyBy.Orbit: return "Orbit";
                case SortSpaceBodyBy.Alfa: return "Alphabetical";
                case SortSpaceBodyBy.Size: return "Size";
                case SortSpaceBodyBy.Habs: return "Hab Sites";
                case SortSpaceBodyBy.Water: return "Water";
                case SortSpaceBodyBy.Volatiles: return "Volatiles";
                case SortSpaceBodyBy.Metals: return "Metals";
                case SortSpaceBodyBy.Nobles: return "Noble Metals";
                case SortSpaceBodyBy.Fissiles: return "Fissiles";
                case SortSpaceBodyBy.Solar: return "Solar";
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

        #region Probe All

        /// <summary>
        /// Start the "Probe All" process - launches probes to all eligible bodies.
        /// </summary>
        public void StartProbeAll()
        {
            if (OnEnterSelectionMode == null)
            {
                OnSpeak?.Invoke("Cannot probe all - selection mode unavailable", true);
                return;
            }

            var faction = GameControl.control?.activePlayer;
            if (faction == null || faction.IsAlienFaction)
            {
                OnSpeak?.Invoke("Cannot probe all", true);
                return;
            }

            try
            {
                var probeAllOp = new LaunchAllProbeOperation();
                var targets = probeAllOp.GetPossibleTargets(faction);

                if (targets == null || targets.Count == 0)
                {
                    OnSpeak?.Invoke("No bodies available to probe", true);
                    return;
                }

                // Get total cost
                var costs = probeAllOp.ResourceCostOptions(faction, null, faction, checkCanAfford: false);
                if (costs == null || costs.Count == 0)
                {
                    OnSpeak?.Invoke("Cannot calculate probe costs", true);
                    return;
                }

                var totalCost = costs[0];
                bool canAfford = totalCost.CanAfford(faction);
                string costStr = FormatProbeCost(totalCost);

                // Build confirmation options
                var options = new List<SelectionOption>
                {
                    new SelectionOption
                    {
                        Label = canAfford ? $"Launch {targets.Count} probes - {costStr}" : $"Launch {targets.Count} probes - {costStr} (Cannot afford)",
                        DetailText = $"Send probes to all {targets.Count} eligible bodies. Total cost: {costStr}.",
                        Data = canAfford
                    },
                    new SelectionOption
                    {
                        Label = "Cancel",
                        DetailText = "Cancel probe all",
                        Data = false
                    }
                };

                string prompt = $"Launch probes to {targets.Count} bodies?";

                OnEnterSelectionMode(prompt, options, (index) =>
                {
                    if (index == 0 && canAfford)
                    {
                        ExecuteProbeAll(probeAllOp, targets, totalCost);
                    }
                    else if (index == 0 && !canAfford)
                    {
                        OnSpeak?.Invoke("Cannot afford to launch all probes", true);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Probe all cancelled", true);
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in StartProbeAll: {ex.Message}");
                OnSpeak?.Invoke("Error preparing probe all", true);
            }
        }

        /// <summary>
        /// Execute probe all operation.
        /// </summary>
        private void ExecuteProbeAll(LaunchAllProbeOperation probeAllOp, List<TIGameState> targets, TIResourcesCost totalCost)
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return;

            try
            {
                // Execute the operation
                probeAllOp.OnOperationConfirm(faction, targets[0], totalCost, null);

                OnSpeak?.Invoke($"Launched {targets.Count} probes", true);

                // Play launch sound
                try
                {
                    PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/Game_SFX/Guns/trig_SFX_Missile_Launch");
                }
                catch { }

                // Refresh the screen to update probe status
                Refresh();
                cachedSections.Clear();
                cachedItemIndex = -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing probe all: {ex.Message}");
                OnSpeak?.Invoke("Error launching probes", true);
            }
        }

        /// <summary>
        /// Get the number of bodies that can be probed.
        /// </summary>
        public int GetProbeableBodyCount()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null || faction.IsAlienFaction)
                return 0;

            try
            {
                var probeAllOp = new LaunchAllProbeOperation();
                var targets = probeAllOp.GetPossibleTargets(faction);
                return targets?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Format probe cost for display.
        /// </summary>
        private string FormatProbeCost(TIResourcesCost cost)
        {
            return CostFormatter.FormatCostOnly(cost, GameControl.control?.activePlayer);
        }

        #endregion
    }
}
