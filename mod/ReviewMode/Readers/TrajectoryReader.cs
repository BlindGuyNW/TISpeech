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
    /// Reader for Trajectory objects.
    /// Extracts and formats transfer trajectory information for accessibility.
    /// Used when browsing trajectory options in the transfer planner.
    /// </summary>
    public class TrajectoryReader
    {
        /// <summary>
        /// Sort mode for trajectory lists.
        /// </summary>
        public enum TrajectorySortMode
        {
            LaunchDate,
            ArrivalDate,
            DeltaV
        }

        /// <summary>
        /// Read a one-line summary of a trajectory suitable for list navigation.
        /// Example: "3.2 km/s, Launch: Jan 5 2025, Arrive: Mar 12 (66 days)"
        /// </summary>
        /// <param name="trajectory">The trajectory to describe</param>
        /// <param name="availableDV_kps">Available delta-V in km/s (for warning color)</param>
        /// <returns>Summary string</returns>
        public string ReadSummary(Trajectory trajectory, float availableDV_kps = float.MaxValue)
        {
            if (trajectory == null)
                return "Unknown trajectory";

            var sb = new StringBuilder();

            // Delta-V with warning indicator
            double dv = trajectory.DV_kps;
            string dvStr = $"{dv:F1} km/s";

            if (dv >= availableDV_kps * 0.9f)
            {
                dvStr += " (CRITICAL)";
            }
            else if (dv >= availableDV_kps * 0.5f)
            {
                dvStr += " (high)";
            }

            sb.Append(dvStr);

            // Launch date
            var launchDate = trajectory.launchTime;
            if (launchDate != null)
            {
                sb.Append($", Launch: {FormatDate(launchDate)}");
            }

            // Arrival date
            var arrivalDate = trajectory.arrivalTime;
            if (arrivalDate != null)
            {
                sb.Append($", Arrive: {FormatDate(arrivalDate)}");
            }

            // Duration
            string durationStr = FormatDuration(trajectory.duration);
            sb.Append($" ({durationStr})");

            return sb.ToString();
        }

        /// <summary>
        /// Read detailed information about a trajectory.
        /// </summary>
        /// <param name="trajectory">The trajectory to describe</param>
        /// <param name="availableDV_kps">Available delta-V in km/s</param>
        /// <returns>Detailed description</returns>
        public string ReadDetail(Trajectory trajectory, float availableDV_kps = float.MaxValue)
        {
            if (trajectory == null)
                return "Unknown trajectory";

            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Transfer Trajectory");
            sb.AppendLine();

            // Origin and destination
            sb.AppendLine("Route:");
            if (trajectory.originOrbit != null)
            {
                sb.AppendLine($"  From: {trajectory.originOrbit.displayName} ({trajectory.originOrbit.barycenter?.displayName ?? "Unknown"})");
            }

            if (trajectory.destinationOrbit != null)
            {
                sb.AppendLine($"  To: {trajectory.destinationOrbit.displayName} ({trajectory.destinationOrbit.barycenter?.displayName ?? "Unknown"})");
            }
            else if (trajectory.destination != null)
            {
                sb.AppendLine($"  To: {trajectory.destination.displayName}");
            }

            if (trajectory.destinationFleet != null)
            {
                sb.AppendLine($"  Target Fleet: {trajectory.destinationFleet.displayName}");
            }

            if (trajectory.destinationStation != null)
            {
                sb.AppendLine($"  Target Station: {trajectory.destinationStation.displayName}");
            }

            // Delta-V
            sb.AppendLine();
            sb.AppendLine("Delta-V Requirements:");
            double totalDV = trajectory.DV_kps;
            sb.AppendLine($"  Total: {totalDV:F2} km/s");

            if (availableDV_kps < float.MaxValue)
            {
                sb.AppendLine($"  Available: {availableDV_kps:F2} km/s");
                double remaining = availableDV_kps - totalDV;
                sb.AppendLine($"  Remaining: {remaining:F2} km/s");

                double percentUsed = (totalDV / availableDV_kps) * 100;
                sb.AppendLine($"  Usage: {percentUsed:F0}% of available");

                if (totalDV > availableDV_kps)
                {
                    sb.AppendLine("  WARNING: Insufficient delta-V for this transfer!");
                }
                else if (percentUsed >= 90)
                {
                    sb.AppendLine("  CAUTION: Very little delta-V will remain after transfer");
                }
            }

            // Timing
            sb.AppendLine();
            sb.AppendLine("Schedule:");
            sb.AppendLine($"  Launch: {FormatDateFull(trajectory.launchTime)}");
            sb.AppendLine($"  Arrival: {FormatDateFull(trajectory.arrivalTime)}");
            sb.AppendLine($"  Total Duration: {FormatDuration(trajectory.duration)}");

            // Loiter time (waiting before launch)
            if (trajectory.assignedTime != null && trajectory.launchTime != null)
            {
                int loiterSeconds = (int)trajectory.launchTime.DifferenceInSeconds(trajectory.assignedTime);
                if (loiterSeconds > 60)
                {
                    sb.AppendLine($"  Wait Before Launch: {FormatDuration(new TimeSpan(0, 0, loiterSeconds))}");
                }
            }

            // Trajectory type
            sb.AppendLine();
            sb.AppendLine("Transfer Type:");
            sb.AppendLine($"  Type: {GetTrajectoryTypeDescription(trajectory)}");

            if (trajectory.nextTrajectory != null)
            {
                sb.AppendLine("  Multi-leg transfer (has subsequent trajectory)");
            }

            // Warnings
            if (trajectory.exitsSolarSystem)
            {
                sb.AppendLine();
                sb.AppendLine("WARNING: This trajectory exits the solar system!");
            }

            if (trajectory.collisionTarget != null)
            {
                sb.AppendLine();
                sb.AppendLine($"WARNING: Collision course with {trajectory.collisionTarget.displayName}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Read a comparison summary between two trajectories.
        /// </summary>
        public string ReadComparison(Trajectory traj1, Trajectory traj2)
        {
            if (traj1 == null || traj2 == null)
                return "Cannot compare trajectories";

            var sb = new StringBuilder();
            sb.AppendLine("Trajectory Comparison:");

            // Delta-V
            double dv1 = traj1.DV_kps;
            double dv2 = traj2.DV_kps;
            double dvDiff = dv2 - dv1;
            sb.AppendLine($"  Delta-V: {dv1:F2} vs {dv2:F2} km/s ({(dvDiff >= 0 ? "+" : "")}{dvDiff:F2})");

            // Duration
            var dur1 = traj1.duration;
            var dur2 = traj2.duration;
            sb.AppendLine($"  Duration: {FormatDuration(dur1)} vs {FormatDuration(dur2)}");

            // Arrival
            if (traj1.arrivalTime != null && traj2.arrivalTime != null)
            {
                double arrivalDiff = traj2.arrivalTime.DifferenceInSeconds(traj1.arrivalTime);
                string arrivalDiffStr = FormatDuration(TimeSpan.FromSeconds(Math.Abs(arrivalDiff)));
                string earlier = arrivalDiff < 0 ? "second arrives earlier" : "first arrives earlier";
                sb.AppendLine($"  Arrival: {earlier} by {arrivalDiffStr}");
            }

            return sb.ToString();
        }

        #region Formatting Helpers

        /// <summary>
        /// Format a date for display (short form).
        /// </summary>
        private string FormatDate(TIDateTime dateTime)
        {
            if (dateTime == null)
                return "Unknown";

            try
            {
                return dateTime.ToCustomTimeDateString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Format a date for display (full form with time).
        /// </summary>
        private string FormatDateFull(TIDateTime dateTime)
        {
            if (dateTime == null)
                return "Unknown";

            try
            {
                return dateTime.ToCustomTimeDateString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Format a TimeSpan duration for readable output.
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours < 1)
            {
                return $"{duration.TotalMinutes:F0} minutes";
            }
            else if (duration.TotalDays < 1)
            {
                return $"{duration.TotalHours:F1} hours";
            }
            else if (duration.TotalDays < 7)
            {
                return $"{duration.TotalDays:F1} days";
            }
            else if (duration.TotalDays < 60)
            {
                double weeks = duration.TotalDays / 7;
                return $"{weeks:F1} weeks";
            }
            else if (duration.TotalDays < 365)
            {
                double months = duration.TotalDays / 30;
                return $"{months:F1} months";
            }
            else
            {
                double years = duration.TotalDays / 365;
                return $"{years:F1} years";
            }
        }

        /// <summary>
        /// Format duration in seconds.
        /// </summary>
        public static string FormatDuration(double seconds)
        {
            return FormatDuration(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Append phase info if duration is significant.
        /// </summary>
        private void AppendPhaseInfo(StringBuilder sb, string phaseName, double duration_s)
        {
            if (duration_s > 60) // More than a minute
            {
                sb.AppendLine($"  {phaseName}: {FormatDuration(duration_s)}");
            }
        }

        /// <summary>
        /// Get a description of the trajectory type based on its class.
        /// </summary>
        private string GetTrajectoryTypeDescription(Trajectory trajectory)
        {
            if (trajectory == null)
                return "Unknown";

            string typeName = trajectory.GetType().Name;

            if (typeName.Contains("Microthrust"))
                return "Microthrust (low continuous acceleration)";
            else if (typeName.Contains("Impulse"))
                return "Impulse (high thrust burns)";
            else if (typeName.Contains("Patched"))
                return "Patched conic (gravity assist)";
            else if (typeName.Contains("Phasing"))
                return "Orbit phasing (rendezvous)";
            else
                return trajectory.GetDisplayName();
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Sort trajectories by the specified mode.
        /// </summary>
        public static Trajectory[] SortTrajectories(Trajectory[] trajectories, TrajectorySortMode sortMode)
        {
            if (trajectories == null || trajectories.Length == 0)
                return trajectories ?? Array.Empty<Trajectory>();

            var sorted = trajectories.ToArray();

            switch (sortMode)
            {
                case TrajectorySortMode.LaunchDate:
                    Array.Sort(sorted, (a, b) =>
                    {
                        int cmp = a.launchTime.CompareTo(b.launchTime);
                        if (cmp == 0)
                            cmp = a.arrivalTime.CompareTo(b.arrivalTime);
                        return cmp;
                    });
                    break;

                case TrajectorySortMode.ArrivalDate:
                    Array.Sort(sorted, (a, b) => a.arrivalTime.CompareTo(b.arrivalTime));
                    break;

                case TrajectorySortMode.DeltaV:
                    Array.Sort(sorted, (a, b) =>
                    {
                        int cmp = a.DV_mps.CompareTo(b.DV_mps);
                        if (cmp == 0)
                            cmp = a.duration_s.CompareTo(b.duration_s);
                        return cmp;
                    });
                    break;
            }

            return sorted;
        }

        /// <summary>
        /// Get the sort mode display name.
        /// </summary>
        public static string GetSortModeDisplayName(TrajectorySortMode mode)
        {
            return mode switch
            {
                TrajectorySortMode.LaunchDate => "Launch Date",
                TrajectorySortMode.ArrivalDate => "Arrival Date",
                TrajectorySortMode.DeltaV => "Delta-V Cost",
                _ => mode.ToString()
            };
        }

        /// <summary>
        /// Get the next sort mode in cycle.
        /// </summary>
        public static TrajectorySortMode CycleSortMode(TrajectorySortMode current)
        {
            return current switch
            {
                TrajectorySortMode.LaunchDate => TrajectorySortMode.ArrivalDate,
                TrajectorySortMode.ArrivalDate => TrajectorySortMode.DeltaV,
                TrajectorySortMode.DeltaV => TrajectorySortMode.LaunchDate,
                _ => TrajectorySortMode.LaunchDate
            };
        }

        /// <summary>
        /// Check if a trajectory is feasible with available delta-V.
        /// </summary>
        public static bool IsFeasible(Trajectory trajectory, float availableDV_kps)
        {
            if (trajectory == null)
                return false;

            return trajectory.DV_kps <= availableDV_kps;
        }

        /// <summary>
        /// Get a quick feasibility assessment.
        /// </summary>
        public static string GetFeasibilityAssessment(Trajectory trajectory, float availableDV_kps)
        {
            if (trajectory == null)
                return "Unknown";

            double dvRequired = trajectory.DV_kps;
            double percentUsed = (dvRequired / availableDV_kps) * 100;

            if (dvRequired > availableDV_kps)
            {
                double shortfall = dvRequired - availableDV_kps;
                return $"IMPOSSIBLE - need {shortfall:F1} km/s more";
            }
            else if (percentUsed >= 90)
            {
                return $"RISKY - uses {percentUsed:F0}% of fuel";
            }
            else if (percentUsed >= 50)
            {
                return $"MODERATE - uses {percentUsed:F0}% of fuel";
            }
            else
            {
                return $"COMFORTABLE - uses {percentUsed:F0}% of fuel";
            }
        }

        #endregion
    }
}
