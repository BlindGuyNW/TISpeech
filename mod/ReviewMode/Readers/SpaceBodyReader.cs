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
    /// Reader for TISpaceBodyState objects (planets, moons, asteroids, etc.)
    /// Extracts and formats space body information for accessibility.
    /// </summary>
    public class SpaceBodyReader : IGameStateReader<TISpaceBodyState>
    {
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

            // Hab Sites section
            if (body.habSites != null && body.habSites.Length > 0)
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

            return section;
        }

        private ISection CreateHabSitesSection(TISpaceBodyState body)
        {
            var section = new DataSection("Hab Sites");

            var occupied = body.occupiedHabSites;
            var vacant = body.vacantHabSites;

            section.AddItem("Total Sites", body.habSites.Length.ToString());
            section.AddItem("Occupied", (occupied?.Count ?? 0).ToString());
            section.AddItem("Vacant", (vacant?.Count ?? 0).ToString());

            // List occupied sites with their bases
            if (occupied != null)
            {
                foreach (var site in occupied)
                {
                    if (site.hab != null)
                    {
                        string owner = site.hab.faction?.displayName ?? "Unknown";
                        section.AddItem(site.hab.displayName, $"({owner})");
                    }
                }
            }

            // Stations in orbit
            var stations = body.stationsInOrbit;
            if (stations != null && stations.Count > 0)
            {
                section.AddItem("Stations in Orbit", stations.Count.ToString());
                foreach (var station in stations.Take(5)) // Limit to first 5
                {
                    string owner = station.faction?.displayName ?? "Unknown";
                    section.AddItem($"  {station.displayName}", $"({owner})");
                }
                if (stations.Count > 5)
                {
                    section.AddItem($"  ... and {stations.Count - 5} more", "");
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
