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
    /// Reader for TIOrbitState objects.
    /// Extracts and formats orbital information for accessibility.
    /// Used for browsing orbits around space bodies and for transfer planning.
    /// </summary>
    public class OrbitReader : IGameStateReader<TIOrbitState>
    {
        /// <summary>
        /// Read a one-line summary of an orbit suitable for list navigation.
        /// Example: "LEO 1 (200 km, interface orbit, 2/3 stations)"
        /// </summary>
        public string ReadSummary(TIOrbitState orbit)
        {
            if (orbit == null)
                return "Unknown orbit";

            var sb = new StringBuilder();
            sb.Append(orbit.displayName ?? "Unknown Orbit");

            // Altitude
            sb.Append($" ({orbit.altitude_km:N0} km");

            // Key properties
            var properties = new List<string>();

            if (orbit.interfaceOrbit)
            {
                properties.Add("interface");
            }

            if (orbit.irradiated)
            {
                properties.Add("irradiated");
            }

            if (orbit.isEarthLEO)
            {
                properties.Add("LEO");
            }

            if (properties.Count > 0)
            {
                sb.Append($", {string.Join(", ", properties)}");
            }

            sb.Append(")");

            // Station capacity
            int stationCount = orbit.stationsInOrbit?.Count ?? 0;
            int capacity = orbit.stationCapacity;
            if (capacity > 0)
            {
                sb.Append($", {stationCount}/{capacity} stations");
            }

            // Fleet count
            int fleetCount = orbit.fleetsInOrbit?.Count ?? 0;
            if (fleetCount > 0)
            {
                sb.Append($", {fleetCount} fleet{(fleetCount != 1 ? "s" : "")}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Read detailed information about an orbit.
        /// </summary>
        public string ReadDetail(TIOrbitState orbit)
        {
            if (orbit == null)
                return "Unknown orbit";

            var sb = new StringBuilder();
            sb.AppendLine($"Orbit: {orbit.displayName}");
            sb.AppendLine($"Around: {orbit.barycenter?.displayName ?? "Unknown"}");

            // Orbital parameters
            sb.AppendLine();
            sb.AppendLine("Orbital Parameters:");
            sb.AppendLine($"  Altitude: {orbit.altitude_km:N0} km");
            sb.AppendLine($"  Semi-major Axis: {orbit.semiMajorAxis_km:N0} km");
            sb.AppendLine($"  Eccentricity: {orbit.eccentricity:F4}");
            sb.AppendLine($"  Inclination: {orbit.inclination_Rad * 180 / Math.PI:F1} degrees");

            // Orbital period
            double periodHours = orbit.period_s / 3600.0;
            if (periodHours < 24)
            {
                sb.AppendLine($"  Period: {periodHours:F1} hours");
            }
            else
            {
                double periodDays = periodHours / 24.0;
                sb.AppendLine($"  Period: {periodDays:F1} days");
            }

            // Orbital velocity
            sb.AppendLine($"  Orbital Velocity: {orbit.averageOrbitalVelocity_kps:F2} km/s");

            // Local conditions
            sb.AppendLine();
            sb.AppendLine("Local Conditions:");
            sb.AppendLine($"  Local Gravity: {FormatGravity(orbit.localGravity_gs)}");
            sb.AppendLine($"  Solar Power: {orbit.solarMultiplier:P0}");

            if (orbit.irradiated)
            {
                sb.AppendLine($"  Radiation Hazard: {orbit.irradiatedValue:F1}x normal");
            }

            // Special properties
            sb.AppendLine();
            sb.AppendLine("Properties:");
            if (orbit.interfaceOrbit)
            {
                sb.AppendLine("  Interface Orbit - can support ground operations");
            }
            if (orbit.isEarthLEO)
            {
                sb.AppendLine("  Low Earth Orbit - LEO hab modules effective here");
            }
            if (orbit.isAdHocOrbit)
            {
                sb.AppendLine("  Ad-hoc Orbit - temporary position, not a standard orbit");
            }

            // Antimatter collection
            if (orbit.amat_ugpy > 0)
            {
                sb.AppendLine($"  Antimatter Collection: {orbit.antimatterPerMonth_dekatonnes:F3} per month");
            }

            // Capacity
            sb.AppendLine();
            sb.AppendLine("Capacity:");
            int stationCount = orbit.stationsInOrbit?.Count ?? 0;
            int pendingHabs = orbit.pendingHabs;
            sb.AppendLine($"  Station Capacity: {stationCount}/{orbit.stationCapacity}");
            if (pendingHabs > 0)
            {
                sb.AppendLine($"  Stations Under Construction: {pendingHabs}");
            }

            // Debris
            if (orbit.destroyedAssets > 0)
            {
                sb.AppendLine($"  Debris Field: {orbit.destroyedAssets} destroyed assets");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get navigable sections for an orbit.
        /// </summary>
        public List<ISection> GetSections(TIOrbitState orbit)
        {
            var sections = new List<ISection>();
            if (orbit == null)
                return sections;

            // Properties section
            sections.Add(CreatePropertiesSection(orbit));

            // Stations section (if any)
            var stations = orbit.stationsInOrbit;
            if (stations != null && stations.Count > 0)
            {
                sections.Add(CreateStationsSection(orbit));
            }

            // Fleets section (if any)
            var fleets = orbit.fleetsInOrbit;
            if (fleets != null && fleets.Count > 0)
            {
                sections.Add(CreateFleetsSection(orbit));
            }

            return sections;
        }

        #region Section Builders

        private ISection CreatePropertiesSection(TIOrbitState orbit)
        {
            var section = new DataSection("Properties");

            section.AddItem("Altitude", $"{orbit.altitude_km:N0} km");
            section.AddItem("Semi-major Axis", $"{orbit.semiMajorAxis_km:N0} km");

            // Orbital period
            double periodHours = orbit.period_s / 3600.0;
            if (periodHours < 24)
            {
                section.AddItem("Period", $"{periodHours:F1} hours");
            }
            else
            {
                section.AddItem("Period", $"{periodHours / 24.0:F1} days");
            }

            section.AddItem("Orbital Velocity", $"{orbit.averageOrbitalVelocity_kps:F2} km/s");
            section.AddItem("Local Gravity", FormatGravity(orbit.localGravity_gs));
            section.AddItem("Solar Power", $"{orbit.solarMultiplier:P0}");

            // Special properties
            if (orbit.interfaceOrbit)
            {
                section.AddItem("Interface Orbit", "Yes - supports ground operations");
            }

            if (orbit.isEarthLEO)
            {
                section.AddItem("Earth LEO", "Yes - LEO modules effective");
            }

            if (orbit.irradiated)
            {
                section.AddItem("Radiation", $"{orbit.irradiatedValue:F1}x hazard");
            }

            if (orbit.amat_ugpy > 0)
            {
                section.AddItem("Antimatter", $"{orbit.antimatterPerMonth_dekatonnes:F3}/month");
            }

            // Capacity
            int stationCount = orbit.stationsInOrbit?.Count ?? 0;
            section.AddItem("Station Capacity", $"{stationCount}/{orbit.stationCapacity}");

            if (orbit.destroyedAssets > 0)
            {
                section.AddItem("Debris", $"{orbit.destroyedAssets} wrecks");
            }

            return section;
        }

        private ISection CreateStationsSection(TIOrbitState orbit)
        {
            var section = new DataSection("Stations");

            var stations = orbit.stationsInOrbit;
            if (stations == null || stations.Count == 0)
            {
                section.AddItem("Stations", "None");
                return section;
            }

            foreach (var station in stations.OrderBy(s => s.displayName))
            {
                string owner = station.faction?.displayName ?? "Unknown";
                int modules = 0;
                int crew = 0;

                try
                {
                    modules = station.AllModules()?.Count ?? 0;
                    crew = station.crew;
                }
                catch { }

                string detail = $"{owner}, {modules} modules";
                if (crew > 0)
                {
                    detail += $", {crew:N0} crew";
                }

                section.AddItem(station.displayName, detail);
            }

            return section;
        }

        private ISection CreateFleetsSection(TIOrbitState orbit)
        {
            var section = new DataSection("Fleets");

            var fleets = orbit.fleetsInOrbit;
            if (fleets == null || fleets.Count == 0)
            {
                section.AddItem("Fleets", "None");
                return section;
            }

            foreach (var fleet in fleets.OrderBy(f => f.faction?.displayName).ThenBy(f => f.displayName))
            {
                string owner = fleet.faction?.displayName ?? "Unknown";
                int ships = fleet.ships?.Count ?? 0;

                string detail = $"{owner}, {ships} ship{(ships != 1 ? "s" : "")}";

                if (fleet.inTransfer)
                {
                    detail += " (in transit)";
                }
                else if (fleet.dockedOrLanded)
                {
                    detail += " (docked)";
                }

                section.AddItem(fleet.displayName, detail);
            }

            return section;
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all orbits around a space body, ordered by altitude.
        /// </summary>
        public static List<TIOrbitState> GetOrbitsAroundBody(TINaturalSpaceObjectState body)
        {
            if (body?.orbits == null)
                return new List<TIOrbitState>();

            return body.orbits
                .Where(o => o != null && !o.isAdHocOrbit)
                .OrderBy(o => o.altitude_km)
                .ToList();
        }

        /// <summary>
        /// Get all orbits around a space body including ad-hoc orbits.
        /// </summary>
        public static List<TIOrbitState> GetAllOrbitsAroundBody(TINaturalSpaceObjectState body)
        {
            if (body?.orbits == null)
                return new List<TIOrbitState>();

            return body.orbits
                .Where(o => o != null)
                .OrderBy(o => o.altitude_km)
                .ToList();
        }

        /// <summary>
        /// Get all Earth LEO orbits.
        /// </summary>
        public static List<TIOrbitState> GetEarthLEOOrbits()
        {
            try
            {
                return GameStateManager.LEOStates()
                    .Where(o => o != null)
                    .OrderBy(o => o.altitude_km)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting LEO orbits: {ex.Message}");
                return new List<TIOrbitState>();
            }
        }

        /// <summary>
        /// Get orbits with player presence (stations or fleets).
        /// </summary>
        public static List<TIOrbitState> GetOrbitsWithPlayerPresence(TIFactionState faction)
        {
            if (faction == null)
                return new List<TIOrbitState>();

            try
            {
                return GameStateManager.IterateByClass<TIOrbitState>()
                    .Where(o => o != null && !o.isAdHocOrbit &&
                           (o.stationsInOrbit.Any(s => s.faction == faction) ||
                            o.fleetsInOrbit.Any(f => f.faction == faction)))
                    .OrderBy(o => o.barycenter?.displayName ?? "")
                    .ThenBy(o => o.altitude_km)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player orbits: {ex.Message}");
                return new List<TIOrbitState>();
            }
        }

        /// <summary>
        /// Get interface orbits (suitable for ground operations) around a body.
        /// </summary>
        public static List<TIOrbitState> GetInterfaceOrbits(TINaturalSpaceObjectState body)
        {
            if (body?.orbits == null)
                return new List<TIOrbitState>();

            return body.orbits
                .Where(o => o != null && o.interfaceOrbit && !o.isAdHocOrbit)
                .OrderBy(o => o.altitude_km)
                .ToList();
        }

        /// <summary>
        /// Check if an orbit has capacity for a new station.
        /// </summary>
        public static bool HasStationCapacity(TIOrbitState orbit)
        {
            if (orbit == null)
                return false;

            int currentCount = orbit.stationsInOrbit?.Count ?? 0;
            int pending = orbit.pendingHabs;
            return (currentCount + pending) < orbit.stationCapacity;
        }

        /// <summary>
        /// Format gravity value for display.
        /// </summary>
        private static string FormatGravity(double gravity_gs)
        {
            if (gravity_gs < 0.001)
            {
                return "Microgravity";
            }
            else if (gravity_gs < 0.01)
            {
                return $"{gravity_gs * 1000:F2} milli-g";
            }
            else if (gravity_gs < 1)
            {
                return $"{gravity_gs:F3} g";
            }
            else
            {
                return $"{gravity_gs:F2} g";
            }
        }

        /// <summary>
        /// Get a short orbit type description.
        /// </summary>
        public static string GetOrbitTypeDescription(TIOrbitState orbit)
        {
            if (orbit == null)
                return "Unknown";

            var types = new List<string>();

            if (orbit.isEarthLEO)
                types.Add("LEO");
            else if (orbit.interfaceOrbit)
                types.Add("Interface");

            if (orbit.irradiated)
                types.Add("Irradiated");

            if (orbit.amat_ugpy > 0)
                types.Add("Antimatter");

            if (types.Count == 0)
                return "Standard orbit";

            return string.Join(", ", types);
        }

        #endregion
    }
}
