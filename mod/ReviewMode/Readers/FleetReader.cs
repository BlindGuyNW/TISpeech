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
    /// Reader for TISpaceFleetState objects.
    /// Extracts and formats fleet information for accessibility.
    /// </summary>
    public class FleetReader : IGameStateReader<TISpaceFleetState>
    {
        public string ReadSummary(TISpaceFleetState fleet)
        {
            if (fleet == null)
                return "Unknown fleet";

            var sb = new StringBuilder();
            sb.Append(fleet.displayName ?? "Unknown Fleet");

            // Ship count
            int shipCount = fleet.ships?.Count ?? 0;
            sb.Append($", {shipCount} ship{(shipCount != 1 ? "s" : "")}");

            // Location/status
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                if (dockedAt != null)
                {
                    string locationName = dockedAt.ref_hab?.displayName ?? dockedAt.ref_habSite?.displayName ?? "unknown";
                    sb.Append(fleet.landed ? $", landed at {locationName}" : $", docked at {locationName}");
                }
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                if (destination != null)
                {
                    sb.Append($", in transit to {destination.displayName}");
                }
                else
                {
                    sb.Append(", in transit");
                }
            }
            else if (fleet.inCombat)
            {
                sb.Append(", in combat");
            }
            else
            {
                // In orbit
                var body = fleet.ref_spaceBody;
                if (body != null)
                {
                    sb.Append($", orbiting {body.displayName}");
                }
            }

            return sb.ToString();
        }

        public string ReadDetail(TISpaceFleetState fleet)
        {
            if (fleet == null)
                return "Unknown fleet";

            var sb = new StringBuilder();
            sb.AppendLine($"Fleet: {fleet.displayName}");

            // Location details
            sb.AppendLine();
            sb.AppendLine("Location:");
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                if (dockedAt != null)
                {
                    string locationName = dockedAt.ref_hab?.displayName ?? dockedAt.ref_habSite?.displayName ?? "unknown location";
                    sb.AppendLine(fleet.landed ? $"  Landed at {locationName}" : $"  Docked at {locationName}");
                }
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                sb.AppendLine($"  In transit to: {destination?.displayName ?? "unknown"}");
            }
            else
            {
                var body = fleet.ref_spaceBody;
                sb.AppendLine($"  Orbiting: {body?.displayName ?? "unknown"}");
                sb.AppendLine($"  Altitude: {fleet.altitude_km:N0} km");
            }

            // Combat status
            if (fleet.inCombat)
            {
                sb.AppendLine("  STATUS: IN COMBAT");
            }
            else if (fleet.waitingForCombat)
            {
                sb.AppendLine("  STATUS: Combat pending");
            }

            // Ships
            sb.AppendLine();
            int shipCount = fleet.ships?.Count ?? 0;
            sb.AppendLine($"Ships: {shipCount}");
            if (fleet.ships != null && fleet.ships.Count > 0)
            {
                // Group by class
                var shipsByClass = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown Class");
                foreach (var group in shipsByClass)
                {
                    sb.AppendLine($"  {group.Count()}x {group.Key}");
                }
            }

            // Delta-V and propellant
            sb.AppendLine();
            sb.AppendLine("Performance:");
            try
            {
                float deltaV = fleet.currentDeltaV_kps;
                sb.AppendLine($"  Delta-V: {deltaV:F1} km/s");
            }
            catch { }

            try
            {
                float cruiseAccel = fleet.cruiseAcceleration_gs * 1000f; // Convert to milligees
                sb.AppendLine($"  Cruise Acceleration: {cruiseAccel:F1} mg");
            }
            catch { }

            // Homeport
            if (fleet.homeport != null)
            {
                sb.AppendLine($"  Homeport: {fleet.homeport.displayName}");
            }

            // Current operations
            if (fleet.currentOperations != null && fleet.currentOperations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Active Operations:");
                foreach (var op in fleet.currentOperations)
                {
                    if (op.operation != null)
                    {
                        sb.AppendLine($"  {op.operation.GetDisplayName()}");
                    }
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TISpaceFleetState fleet)
        {
            var sections = new List<ISection>();
            if (fleet == null)
                return sections;

            // Status section
            sections.Add(CreateStatusSection(fleet));

            // Ships section
            if (fleet.ships != null && fleet.ships.Count > 0)
            {
                sections.Add(CreateShipsSection(fleet));
            }

            // Performance section
            sections.Add(CreatePerformanceSection(fleet));

            // Operations section
            sections.Add(CreateOperationsSection(fleet));

            return sections;
        }

        #region Section Builders

        private ISection CreateStatusSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Status");

            // Location
            if (fleet.dockedOrLanded)
            {
                var dockedAt = fleet.dockedLocation;
                string locationName = dockedAt?.ref_hab?.displayName ?? dockedAt?.ref_habSite?.displayName ?? "unknown";
                section.AddItem("Location", fleet.landed ? $"Landed at {locationName}" : $"Docked at {locationName}");
            }
            else if (fleet.inTransfer)
            {
                var destination = fleet.trajectory?.destination;
                section.AddItem("Location", $"In transit to {destination?.displayName ?? "unknown"}");
            }
            else
            {
                var body = fleet.ref_spaceBody;
                section.AddItem("Location", $"Orbiting {body?.displayName ?? "unknown"}");
                section.AddItem("Altitude", $"{fleet.altitude_km:N0} km");
            }

            // Combat status
            if (fleet.inCombat)
            {
                section.AddItem("Combat", "IN COMBAT");
            }
            else if (fleet.waitingForCombat)
            {
                section.AddItem("Combat", "Pending");
            }
            else if (fleet.bombarding)
            {
                section.AddItem("Status", "Bombarding");
            }
            else
            {
                section.AddItem("Combat", "None");
            }

            // Homeport
            if (fleet.homeport != null)
            {
                section.AddItem("Homeport", fleet.homeport.displayName);
            }

            return section;
        }

        private ISection CreateShipsSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Ships");

            if (fleet.ships == null || fleet.ships.Count == 0)
            {
                section.AddItem("Ships", "None");
                return section;
            }

            // Group by class and show count
            var shipsByClass = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown");
            foreach (var group in shipsByClass.OrderByDescending(g => g.Count()))
            {
                string className = group.Key;
                int count = group.Count();

                // Get first ship for details
                var sample = group.First();
                string hullType = sample.hull?.displayName ?? "";
                string detail = !string.IsNullOrEmpty(hullType) ? $"{hullType}" : "";

                section.AddItem($"{count}x {className}", detail);
            }

            // Summary
            int small = fleet.smallShips?.Count ?? 0;
            int medium = fleet.mediumShips?.Count ?? 0;
            int large = fleet.largeShips?.Count ?? 0;

            if (small > 0 || medium > 0 || large > 0)
            {
                var sizes = new List<string>();
                if (large > 0) sizes.Add($"{large} large");
                if (medium > 0) sizes.Add($"{medium} medium");
                if (small > 0) sizes.Add($"{small} small");
                section.AddItem("By Size", string.Join(", ", sizes));
            }

            return section;
        }

        private ISection CreatePerformanceSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Performance");

            try
            {
                float deltaV = fleet.currentDeltaV_kps;
                section.AddItem("Delta-V", $"{deltaV:F1} km/s");
            }
            catch
            {
                section.AddItem("Delta-V", "Unknown");
            }

            try
            {
                float cruiseAccel = fleet.cruiseAcceleration_gs * 1000f;
                section.AddItem("Cruise Accel", $"{cruiseAccel:F1} milligees");
            }
            catch { }

            try
            {
                float maxAccel = fleet.maxAcceleration_gs * 1000f;
                section.AddItem("Max Accel", $"{maxAccel:F1} milligees");
            }
            catch { }

            try
            {
                double mass = fleet.mass_kg / 1000.0; // Convert to tons
                section.AddItem("Mass", $"{mass:N0} tons");
            }
            catch { }

            return section;
        }

        private ISection CreateOperationsSection(TISpaceFleetState fleet)
        {
            var section = new DataSection("Operations");

            // Current operations
            if (fleet.currentOperations != null && fleet.currentOperations.Count > 0)
            {
                foreach (var op in fleet.currentOperations)
                {
                    if (op.operation != null)
                    {
                        string target = op.target?.displayName ?? "";
                        section.AddItem(op.operation.GetDisplayName(), target);
                    }
                }
            }
            else
            {
                section.AddItem("Current", "None");
            }

            // Availability
            if (fleet.unavailableForOperations)
            {
                section.AddItem("Availability", "Unavailable");
            }
            else if (fleet.dockedOrLanded)
            {
                section.AddItem("Availability", "Ready (docked)");
            }
            else if (fleet.inTransfer)
            {
                section.AddItem("Availability", "In transit");
            }
            else
            {
                section.AddItem("Availability", "Ready");
            }

            return section;
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get all fleets belonging to the player's faction.
        /// </summary>
        public static List<TISpaceFleetState> GetPlayerFleets(TIFactionState faction)
        {
            if (faction == null)
                return new List<TISpaceFleetState>();

            try
            {
                return GameStateManager.IterateByClass<TISpaceFleetState>()
                    .Where(f => f.faction == faction && !f.archived && !f.dummyFleet)
                    .OrderBy(f => f.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player fleets: {ex.Message}");
                return new List<TISpaceFleetState>();
            }
        }

        /// <summary>
        /// Get all known enemy fleets (based on intel).
        /// </summary>
        public static List<TISpaceFleetState> GetKnownEnemyFleets(TIFactionState viewer)
        {
            if (viewer == null)
                return new List<TISpaceFleetState>();

            try
            {
                return GameStateManager.IterateByClass<TISpaceFleetState>()
                    .Where(f => f.faction != viewer &&
                                !f.archived &&
                                !f.dummyFleet &&
                                viewer.GetIntel(f) > 0)
                    .OrderBy(f => f.faction?.displayName ?? "")
                    .ThenBy(f => f.displayName)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting enemy fleets: {ex.Message}");
                return new List<TISpaceFleetState>();
            }
        }

        #endregion
    }
}
