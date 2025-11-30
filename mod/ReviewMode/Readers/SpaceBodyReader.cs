using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Systems.GameTime;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for TISpaceBodyState objects (planets, moons, asteroids, etc.)
    /// Extracts and formats space body information for accessibility.
    /// </summary>
    public class SpaceBodyReader : IGameStateReader<TISpaceBodyState>
    {
        /// <summary>
        /// Callback for entering selection mode (for probe launch confirmation).
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        public string ReadSummary(TISpaceBodyState body)
        {
            if (body == null)
                return "Unknown body";

            var sb = new StringBuilder();
            sb.Append(body.displayName ?? "Unknown");

            // Type
            string typeStr = GetBodyTypeString(body.objectType);
            if (!string.IsNullOrEmpty(typeStr))
            {
                sb.Append($", {typeStr}");
            }

            // Parent body (for moons)
            if (body.barycenter != null && body.barycenter.objectType != SpaceObjectType.Star)
            {
                sb.Append($" of {body.barycenter.displayName}");
            }

            // Key info: bases/sites
            int bases = body.surfaceBases?.Count ?? 0;
            int stations = body.stationsInOrbit?.Count ?? 0;
            int vacantSites = body.vacantHabSites?.Count ?? 0;

            if (bases > 0 || stations > 0)
            {
                var parts = new List<string>();
                if (bases > 0) parts.Add($"{bases} base{(bases != 1 ? "s" : "")}");
                if (stations > 0) parts.Add($"{stations} station{(stations != 1 ? "s" : "")}");
                sb.Append($", {string.Join(", ", parts)}");
            }
            else if (vacantSites > 0)
            {
                sb.Append($", {vacantSites} vacant site{(vacantSites != 1 ? "s" : "")}");
            }

            return sb.ToString();
        }

        public string ReadDetail(TISpaceBodyState body)
        {
            if (body == null)
                return "Unknown body";

            var sb = new StringBuilder();
            sb.AppendLine($"{body.displayName}");
            sb.AppendLine($"Type: {GetBodyTypeString(body.objectType)}");

            // Parent
            if (body.barycenter != null)
            {
                if (body.barycenter.objectType == SpaceObjectType.Star)
                {
                    sb.AppendLine("Orbits: Sun");
                }
                else
                {
                    sb.AppendLine($"Orbits: {body.barycenter.displayName}");
                }
            }

            // Physical properties
            sb.AppendLine();
            sb.AppendLine("Physical Properties:");
            sb.AppendLine($"  Radius: {body.meanRadius_km:N0} km");
            sb.AppendLine($"  Surface Gravity: {body.surfaceGravity_g:F2} g");

            if (body.atmosphere != Atmosphere.None)
            {
                sb.AppendLine($"  Atmosphere: {body.atmosphere}");
            }

            // Orbital info
            sb.AppendLine();
            sb.AppendLine("Orbit:");
            sb.AppendLine($"  Semi-major Axis: {body.semiMajorAxis_AU:F3} AU");
            sb.AppendLine($"  Eccentricity: {body.ecc:F3}");

            // Hab sites
            var habSites = body.habSites;
            if (habSites != null && habSites.Length > 0)
            {
                sb.AppendLine();
                int occupied = body.occupiedHabSites?.Count ?? 0;
                int vacant = body.vacantHabSites?.Count ?? 0;
                sb.AppendLine($"Hab Sites: {habSites.Length} total ({occupied} occupied, {vacant} vacant)");

                // Mining potential summary (from first hab site)
                var site = habSites[0];
                if (site.miningProfile != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("Mining Potential:");
                    AppendMiningInfo(sb, "  Water", site, FactionResource.Water);
                    AppendMiningInfo(sb, "  Volatiles", site, FactionResource.Volatiles);
                    AppendMiningInfo(sb, "  Metals", site, FactionResource.Metals);
                    AppendMiningInfo(sb, "  Noble Metals", site, FactionResource.NobleMetals);
                    AppendMiningInfo(sb, "  Fissiles", site, FactionResource.Fissiles);
                }
            }

            // Fleets in orbit
            var fleets = body.fleetsInOrbit;
            if (fleets != null && fleets.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Fleets in Orbit: {fleets.Count}");
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TISpaceBodyState body)
        {
            var sections = new List<ISection>();
            if (body == null)
                return sections;

            // Overview section
            sections.Add(CreateOverviewSection(body));

            // Hab Sites section (includes stations in orbit)
            bool hasHabSites = body.habSites != null && body.habSites.Length > 0;
            bool hasStations = body.stationsInOrbit != null && body.stationsInOrbit.Count > 0;
            bool hasBases = body.surfaceBases != null && body.surfaceBases.Count > 0;
            if (hasHabSites || hasStations || hasBases)
            {
                sections.Add(CreateHabSitesSection(body));
            }

            // Mining section (if relevant)
            if (body.habSites != null && body.habSites.Length > 0)
            {
                sections.Add(CreateMiningSection(body));
            }

            // Moons section (if any)
            if (body.naturalSatellites != null && body.naturalSatellites.Count > 0)
            {
                sections.Add(CreateMoonsSection(body));
            }

            // Fleets section
            var fleets = body.fleetsInOrbit;
            if (fleets != null && fleets.Count > 0)
            {
                sections.Add(CreateFleetsSection(body));
            }

            // Prospecting section (for bodies with hab sites)
            if (body.habSites != null && body.habSites.Length > 0)
            {
                var prospectingSection = CreateProspectingSection(body);
                if (prospectingSection != null)
                {
                    sections.Add(prospectingSection);
                }
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateOverviewSection(TISpaceBodyState body)
        {
            var section = new DataSection("Overview");

            section.AddItem("Type", GetBodyTypeString(body.objectType));

            if (body.barycenter != null)
            {
                if (body.barycenter.objectType == SpaceObjectType.Star)
                {
                    section.AddItem("Orbits", "Sun");
                }
                else
                {
                    section.AddItem("Orbits", body.barycenter.displayName);
                }
            }

            section.AddItem("Radius", $"{body.meanRadius_km:N0} km");
            section.AddItem("Gravity", $"{body.surfaceGravity_g:F2} g");

            if (body.atmosphere != Atmosphere.None)
            {
                section.AddItem("Atmosphere", body.atmosphere.ToString());
            }

            section.AddItem("Semi-major Axis", $"{body.semiMajorAxis_AU:F3} AU");

            // Moons count
            if (body.naturalSatellites != null && body.naturalSatellites.Count > 0)
            {
                section.AddItem("Moons", body.naturalSatellites.Count.ToString());
            }

            // Launch window info (for non-Earth bodies)
            var launchWindowInfo = GetLaunchWindowInfo(body);
            if (launchWindowInfo != null)
            {
                section.AddItem("Launch Window", launchWindowInfo);
            }

            return section;
        }

        private ISection CreateHabSitesSection(TISpaceBodyState body)
        {
            var section = new DataSection("Habs and Stations");
            var habReader = new HabReader();

            // Surface hab sites (if any - not present on Earth)
            var habSites = body.habSites;
            if (habSites != null && habSites.Length > 0)
            {
                var occupied = body.occupiedHabSites;
                var vacant = body.vacantHabSites;

                section.AddItem("Surface Sites", habSites.Length.ToString());
                section.AddItem("Occupied", (occupied?.Count ?? 0).ToString());
                section.AddItem("Vacant", (vacant?.Count ?? 0).ToString());

                // List occupied sites with their bases (drillable)
                if (occupied != null)
                {
                    foreach (var site in occupied)
                    {
                        if (site.hab != null)
                        {
                            string owner = site.hab.faction?.displayName ?? "Unknown";
                            string label = $"{site.hab.displayName} ({owner})";
                            string detail = habReader.ReadDetail(site.hab);
                            section.AddDrillableItem(label, site.hab.ID.ToString(), detail);
                        }
                    }
                }
            }

            // Stations in orbit (drillable)
            var stations = body.stationsInOrbit;
            if (stations != null && stations.Count > 0)
            {
                section.AddItem("Stations in Orbit", stations.Count.ToString());
                foreach (var station in stations)
                {
                    string owner = station.faction?.displayName ?? "Unknown";
                    string label = $"{station.displayName} ({owner})";
                    string detail = habReader.ReadDetail(station);
                    section.AddDrillableItem(label, station.ID.ToString(), detail);
                }
            }

            return section;
        }

        private ISection CreateMiningSection(TISpaceBodyState body)
        {
            var section = new DataSection("Mining Potential");

            if (body.habSites == null || body.habSites.Length == 0)
            {
                section.AddItem("Mining", "No hab sites");
                return section;
            }

            var site = body.habSites[0];
            if (site.miningProfile == null)
            {
                section.AddItem("Mining", "Unknown profile");
                return section;
            }

            // Show expected productivity for each resource
            AddMiningItem(section, "Water", site, FactionResource.Water);
            AddMiningItem(section, "Volatiles", site, FactionResource.Volatiles);
            AddMiningItem(section, "Metals", site, FactionResource.Metals);
            AddMiningItem(section, "Noble Metals", site, FactionResource.NobleMetals);
            AddMiningItem(section, "Fissiles", site, FactionResource.Fissiles);

            return section;
        }

        private ISection CreateMoonsSection(TISpaceBodyState body)
        {
            var section = new DataSection("Moons");

            if (body.naturalSatellites == null || body.naturalSatellites.Count == 0)
            {
                section.AddItem("Moons", "None");
                return section;
            }

            foreach (var moon in body.naturalSatellites.OrderBy(m => m.semiMajorAxis_m))
            {
                string typeStr = GetBodyTypeString(moon.objectType);
                int sites = moon.habSites?.Length ?? 0;
                string info = sites > 0 ? $"{typeStr}, {sites} site{(sites != 1 ? "s" : "")}" : typeStr;
                section.AddItem(moon.displayName, info);
            }

            return section;
        }

        private ISection CreateFleetsSection(TISpaceBodyState body)
        {
            var section = new DataSection("Fleets in Orbit");

            var fleets = body.fleetsInOrbit;
            if (fleets == null || fleets.Count == 0)
            {
                section.AddItem("Fleets", "None");
                return section;
            }

            // Group by faction
            var byFaction = fleets.GroupBy(f => f.faction?.displayName ?? "Unknown");
            foreach (var group in byFaction)
            {
                section.AddItem(group.Key, $"{group.Count()} fleet{(group.Count() != 1 ? "s" : "")}");
            }

            return section;
        }

        /// <summary>
        /// Create the Prospecting section showing probe status and launch action.
        /// </summary>
        private ISection CreateProspectingSection(TISpaceBodyState body)
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null || faction.IsAlienFaction)
                return null;

            var section = new DataSection("Prospecting");

            try
            {
                // Check prospecting status
                bool isProspected = faction.Prospected(body);
                bool probeEnRoute = faction.ProspectorEnRoute(body);

                if (isProspected)
                {
                    section.AddItem("Status", "Fully prospected");
                    return section;
                }

                if (probeEnRoute)
                {
                    // Get ETA
                    var arrival = faction.ProspectorArrival(body);
                    if (arrival != null)
                    {
                        var currentDate = GameTimeManager.Singleton?.currentTime;
                        int daysRemaining = currentDate != null ? (int)(arrival - currentDate).TotalDays : 0;
                        section.AddItem("Status", $"Probe en route, {daysRemaining} days remaining");
                        section.AddItem("Arrival", arrival.ToShortDateString());
                    }
                    else
                    {
                        section.AddItem("Status", "Probe en route");
                    }

                    // Check if we can launch an overtaking probe
                    var overtakeCosts = faction.CanOvertakeProbeWithProbe(body);
                    if (overtakeCosts != null && overtakeCosts.Count > 0)
                    {
                        var bodyCopy = body;
                        section.AddItem("Launch Faster Probe", "Send a probe that will arrive sooner",
                            onActivate: () => StartProbeLaunch(bodyCopy, isOvertake: true));
                    }

                    return section;
                }

                // Not prospected and no probe en route - can we launch?
                bool canProspect = faction.CanProspectWithProbe(body, checkIfCanOvertake: false);

                if (canProspect)
                {
                    section.AddItem("Status", "Not prospected");

                    // Get cost info for display
                    var probeOp = new LaunchProbeOperation();
                    var costs = probeOp.ResourceCostOptions(faction, body, faction, checkCanAfford: false);

                    if (costs.Count > 0)
                    {
                        // Show cheapest/fastest option summary
                        var bestCost = costs[0];
                        int days = (int)bestCost.completionTime_days;
                        section.AddItem("Probe Time", $"~{days} days to prospect");
                    }

                    // Add launch action
                    var bodyCopy = body;
                    section.AddItem("Launch Probe", "Send a probe to prospect this body",
                        onActivate: () => StartProbeLaunch(bodyCopy, isOvertake: false));
                }
                else
                {
                    // Check why we can't prospect
                    if (body.habSites == null || body.habSites.Length == 0)
                    {
                        section.AddItem("Status", "No hab sites - cannot prospect");
                    }
                    else if (!faction.CanExplore(body))
                    {
                        // Find what tech is needed
                        string requiredTech = GetRequiredExplorationTech(body);
                        if (!string.IsNullOrEmpty(requiredTech))
                        {
                            section.AddItem("Status", $"Requires research: {requiredTech}");
                        }
                        else
                        {
                            section.AddItem("Status", "Cannot explore - need tech");
                        }
                    }
                    else if (faction.FleetSurveyingPlanet(body))
                    {
                        section.AddItem("Status", "Fleet survey in progress");
                    }
                    else
                    {
                        section.AddItem("Status", "Cannot launch probe");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error creating prospecting section: {ex.Message}");
                section.AddItem("Status", "Error checking probe status");
            }

            return section;
        }

        /// <summary>
        /// Start the probe launch process - shows cost options and confirms.
        /// </summary>
        private void StartProbeLaunch(TISpaceBodyState body, bool isOvertake)
        {
            if (OnEnterSelectionMode == null)
            {
                OnSpeak?.Invoke("Cannot launch probe - selection mode unavailable", true);
                return;
            }

            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return;

            try
            {
                // Get cost options
                LaunchProbeOperation probeOp;
                List<TIResourcesCost> costs;

                if (isOvertake)
                {
                    costs = faction.CanOvertakeProbeWithProbe(body);
                    probeOp = new LaunchOverrideProbeOperation();
                }
                else
                {
                    probeOp = new LaunchProbeOperation();
                    costs = probeOp.ResourceCostOptions(faction, body, faction, checkCanAfford: false);
                }

                if (costs == null || costs.Count == 0)
                {
                    OnSpeak?.Invoke("No probe launch options available", true);
                    return;
                }

                // Build selection options
                var options = new List<SelectionOption>();

                foreach (var cost in costs)
                {
                    bool canAfford = cost.CanAfford(faction);
                    string costStr = FormatProbeCost(cost);
                    int days = (int)cost.completionTime_days;

                    string label = canAfford
                        ? $"{days} days - {costStr}"
                        : $"{days} days - {costStr} (Cannot afford)";

                    options.Add(new SelectionOption
                    {
                        Label = label,
                        DetailText = $"Launch probe to {body.displayName}. {costStr}. Arrival in {days} days.",
                        Data = new ProbeLaunchData { Body = body, Cost = cost, CanAfford = canAfford, IsOvertake = isOvertake }
                    });
                }

                // Add cancel option
                options.Add(new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Cancel probe launch",
                    Data = null
                });

                string prompt = isOvertake
                    ? $"Launch faster probe to {body.displayName}?"
                    : $"Launch probe to {body.displayName}?";

                OnEnterSelectionMode(prompt, options, (index) =>
                {
                    if (index >= 0 && index < costs.Count)
                    {
                        var selectedData = options[index].Data as ProbeLaunchData;
                        if (selectedData != null && selectedData.CanAfford)
                        {
                            ExecuteProbeLaunch(selectedData.Body, selectedData.Cost, selectedData.IsOvertake);
                        }
                        else
                        {
                            OnSpeak?.Invoke("Cannot afford this probe launch option", true);
                        }
                    }
                    else
                    {
                        OnSpeak?.Invoke("Probe launch cancelled", true);
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting probe launch: {ex.Message}");
                OnSpeak?.Invoke("Error preparing probe launch", true);
            }
        }

        /// <summary>
        /// Execute the probe launch.
        /// </summary>
        private void ExecuteProbeLaunch(TISpaceBodyState body, TIResourcesCost cost, bool isOvertake)
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return;

            try
            {
                LaunchProbeOperation probeOp = isOvertake
                    ? new LaunchOverrideProbeOperation()
                    : new LaunchProbeOperation();

                bool success = probeOp.OnOperationConfirm(faction, body, cost, null);

                if (success)
                {
                    int days = (int)cost.completionTime_days;
                    OnSpeak?.Invoke($"Probe launched to {body.displayName}. Arrival in {days} days.", true);

                    // Play launch sound
                    try
                    {
                        PavonisInteractive.TerraInvicta.Audio.AudioManager.PlayOneShot("event:/SFX/Game_SFX/Guns/trig_SFX_Missile_Launch");
                    }
                    catch { }
                }
                else
                {
                    OnSpeak?.Invoke("Failed to launch probe", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing probe launch: {ex.Message}");
                OnSpeak?.Invoke("Error launching probe", true);
            }
        }

        /// <summary>
        /// Format probe cost for display.
        /// </summary>
        private string FormatProbeCost(TIResourcesCost cost)
        {
            var parts = new List<string>();

            // Check for boost (Earth launch)
            float boost = cost.GetSingleCostValue(FactionResource.Boost);
            if (boost > 0)
            {
                parts.Add($"{boost:F1} Boost");
            }

            // Check for money
            float money = cost.GetSingleCostValue(FactionResource.Money);
            if (money > 0)
            {
                parts.Add($"${money:F0}M");
            }

            // Check for space resources
            float metals = cost.GetSingleCostValue(FactionResource.Metals);
            if (metals > 0) parts.Add($"{metals:F1} Metals");

            float volatiles = cost.GetSingleCostValue(FactionResource.Volatiles);
            if (volatiles > 0) parts.Add($"{volatiles:F1} Volatiles");

            float water = cost.GetSingleCostValue(FactionResource.Water);
            if (water > 0) parts.Add($"{water:F1} Water");

            float nobles = cost.GetSingleCostValue(FactionResource.NobleMetals);
            if (nobles > 0) parts.Add($"{nobles:F1} Nobles");

            float fissiles = cost.GetSingleCostValue(FactionResource.Fissiles);
            if (fissiles > 0) parts.Add($"{fissiles:F1} Fissiles");

            if (parts.Count == 0)
                return "Free";

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Data class for probe launch selection.
        /// </summary>
        private class ProbeLaunchData
        {
            public TISpaceBodyState Body { get; set; }
            public TIResourcesCost Cost { get; set; }
            public bool CanAfford { get; set; }
            public bool IsOvertake { get; set; }
        }

        #endregion

        #region Helpers

        private static string GetBodyTypeString(SpaceObjectType type)
        {
            return type switch
            {
                SpaceObjectType.Star => "Star",
                SpaceObjectType.Planet => "Planet",
                SpaceObjectType.DwarfPlanet => "Dwarf Planet",
                SpaceObjectType.PlanetaryMoon => "Moon",
                SpaceObjectType.Asteroid => "Asteroid",
                SpaceObjectType.AsteroidalMoon => "Asteroidal Moon",
                SpaceObjectType.Comet => "Comet",
                SpaceObjectType.LagrangePoint => "Lagrange Point",
                _ => type.ToString()
            };
        }

        private void AppendMiningInfo(StringBuilder sb, string label, TIHabSiteState site, FactionResource resource)
        {
            try
            {
                float expected = site.GetHabSiteExpectedProductivity_month(resource);
                if (expected > 0.1f)
                {
                    sb.AppendLine($"{label}: {expected:F1}/month");
                }
            }
            catch { }
        }

        private void AddMiningItem(DataSection section, string label, TIHabSiteState site, FactionResource resource)
        {
            try
            {
                float expected = site.GetHabSiteExpectedProductivity_month(resource);
                float min = site.GetHabSiteMinProductivity_month(resource);
                float max = site.GetHabSiteMaxProductivity_month(resource);

                if (expected > 0.1f)
                {
                    section.AddItem(label, $"{expected:F1}/mo (range: {min:F1}-{max:F1})");
                }
                else
                {
                    section.AddItem(label, "Negligible");
                }
            }
            catch
            {
                section.AddItem(label, "Unknown");
            }
        }

        /// <summary>
        /// Get the name of the tech required to explore a space body.
        /// </summary>
        private string GetRequiredExplorationTech(TISpaceBodyState body)
        {
            try
            {
                // Get the effect needed to explore this body
                var effectToExplore = body.GetEffectToExplore();
                if (effectToExplore == null)
                    return null;

                // Find the tech that provides this effect
                var requiredTech = TemplateManager.IterateByClass<TITechTemplate>()
                    .FirstOrDefault(t => t.effects != null && t.effects.Contains(effectToExplore.dataName));

                return requiredTech?.displayName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get launch window info for a space body (from Earth).
        /// </summary>
        private string GetLaunchWindowInfo(TISpaceBodyState body)
        {
            try
            {
                // No launch window for Earth or Earth's moons
                if (body.isEarth || (body.barycenter != null && body.barycenter.isEarth))
                    return null;

                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return null;

                // Get the next Hohmann launch window from Earth
                double synodicPeriod_s;
                var nextWindow = TINaturalSpaceObjectState.GetNextHohmannLaunchWindowDate(
                    faction,
                    GameStateManager.Earth(),
                    body,
                    TITimeState.Now(),
                    out synodicPeriod_s);

                bool penaltyFromPrior;
                double penaltyFraction = TISpaceObjectState.GetHohmannTimePenaltyFraction(
                    faction,
                    nextWindow,
                    synodicPeriod_s,
                    out penaltyFromPrior);

                // Format the result
                if (penaltyFraction < 0.03)
                {
                    return "Now (optimal)";
                }

                int penaltyPercent = (int)(penaltyFraction * 100);
                string trend = penaltyFromPrior ? "closing" : "opening";

                // Calculate days to next window
                var currentDate = GameTimeManager.Singleton?.currentTime;
                if (currentDate != null)
                {
                    int daysToWindow = (int)(nextWindow - currentDate).TotalDays;
                    if (daysToWindow > 0)
                    {
                        return $"+{penaltyPercent}% penalty, {trend}, {daysToWindow} days to optimal";
                    }
                }

                return $"+{penaltyPercent}% penalty, {trend}";
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all major space bodies (planets, dwarf planets).
        /// </summary>
        public static List<TISpaceBodyState> GetMajorBodies()
        {
            try
            {
                return GameStateManager.AllSpaceBodies()
                    .Where(b => b != null &&
                               (b.objectType == SpaceObjectType.Planet ||
                                b.objectType == SpaceObjectType.DwarfPlanet))
                    .OrderBy(b => b.semiMajorAxis_m)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting major bodies: {ex.Message}");
                return new List<TISpaceBodyState>();
            }
        }

        /// <summary>
        /// Get all space bodies in the solar system, organized hierarchically.
        /// </summary>
        public static List<TISpaceBodyState> GetAllBodiesHierarchical()
        {
            var result = new List<TISpaceBodyState>();
            try
            {
                // Get all non-star bodies (same approach as Intel screen)
                var allBodies = GameStateManager.AllSpaceBodies()
                    .Where(b => b != null && b.objectType != SpaceObjectType.Star)
                    .ToList();

                if (allBodies.Count == 0)
                {
                    MelonLogger.Warning("No space bodies found");
                    return result;
                }

                // Separate into planets and their moons for hierarchical display
                var planets = allBodies
                    .Where(b => b.objectType == SpaceObjectType.Planet ||
                               b.objectType == SpaceObjectType.DwarfPlanet)
                    .OrderBy(b => b.semiMajorAxis_m)
                    .ToList();

                foreach (var planet in planets)
                {
                    result.Add(planet);

                    // Add moons immediately after parent
                    if (planet.naturalSatellites != null)
                    {
                        foreach (var moon in planet.naturalSatellites.OrderBy(m => m.semiMajorAxis_m))
                        {
                            result.Add(moon);
                        }
                    }
                }

                // Add asteroids and comets at the end
                var asteroids = allBodies
                    .Where(b => b.objectType == SpaceObjectType.Asteroid ||
                               b.objectType == SpaceObjectType.Comet)
                    .OrderBy(b => b.displayName)
                    .ToList();

                result.AddRange(asteroids);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting all bodies: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get bodies where the player has presence (bases, stations).
        /// </summary>
        public static List<TISpaceBodyState> GetBodiesWithPlayerPresence(TIFactionState faction)
        {
            if (faction == null)
                return new List<TISpaceBodyState>();

            try
            {
                return GameStateManager.AllSpaceBodies()
                    .Where(b => b != null && HasPlayerPresence(b, faction))
                    .OrderBy(b => b.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player bodies: {ex.Message}");
                return new List<TISpaceBodyState>();
            }
        }

        private static bool HasPlayerPresence(TISpaceBodyState body, TIFactionState faction)
        {
            // Check surface bases
            if (body.surfaceBases?.Any(h => h.faction == faction) == true)
                return true;

            // Check stations in orbit
            if (body.stationsInOrbit?.Any(h => h.faction == faction) == true)
                return true;

            // Check fleets
            if (body.fleetsInOrbit?.Any(f => f.faction == faction) == true)
                return true;

            return false;
        }

        #endregion
    }
}
