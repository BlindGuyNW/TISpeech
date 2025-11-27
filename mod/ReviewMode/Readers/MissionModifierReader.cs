using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for mission modifier breakdowns.
    /// Extracts attacking and defending modifiers for contested missions.
    /// </summary>
    public class MissionModifierReader
    {
        /// <summary>
        /// Get the full modifier breakdown for a mission + target combination.
        /// </summary>
        public MissionModifierBreakdown GetModifiers(
            TIMissionTemplate mission,
            TICouncilorState councilor,
            TIGameState target,
            float resourcesSpent = 0f)
        {
            var breakdown = new MissionModifierBreakdown();

            try
            {
                if (mission.resolutionMethod is TIMissionResolution_Contested contested)
                {
                    // Get attacking modifiers (bonuses for the councilor)
                    var attackMods = contested.GetAttackingNonZeroModifiers(mission, councilor, target, resourcesSpent);
                    foreach (var mod in attackMods)
                    {
                        try
                        {
                            float value = mod.GetModifier(councilor, target, resourcesSpent,
                                mission.cost?.resourceType ?? FactionResource.None);
                            breakdown.Bonuses.Add(new ModifierItem
                            {
                                Name = mod.displayName,
                                Value = value
                            });
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Error getting modifier value: {ex.Message}");
                        }
                    }
                    breakdown.TotalBonus = contested.SumAttackingModifiers(mission, councilor, target, resourcesSpent);

                    // Get defending modifiers (penalties/resistance from target)
                    var defendMods = contested.GetDefendingNonZeroModifiers(mission, councilor, target, resourcesSpent);
                    foreach (var mod in defendMods)
                    {
                        try
                        {
                            float value = mod.GetModifier(councilor, target, resourcesSpent,
                                mission.cost?.resourceType ?? FactionResource.None);
                            breakdown.Penalties.Add(new ModifierItem
                            {
                                Name = mod.displayName,
                                Value = value
                            });
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Error getting defender modifier value: {ex.Message}");
                        }
                    }
                    breakdown.TotalPenalty = contested.SumDefendingModifiers(mission, councilor, target, resourcesSpent);

                    // Get success chance
                    breakdown.SuccessChance = mission.resolutionMethod.GetSuccessChanceString(mission, councilor, target, resourcesSpent);
                    breakdown.IsContested = true;
                }
                else
                {
                    // Uncontested mission - automatic success
                    breakdown.SuccessChance = "100%";
                    breakdown.IsContested = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting mission modifiers: {ex.Message}");
                breakdown.SuccessChance = "Unknown";
            }

            return breakdown;
        }

        /// <summary>
        /// Format the modifier breakdown for speech output.
        /// </summary>
        /// <param name="breakdown">The modifier breakdown to format</param>
        /// <param name="verbose">If true, include individual modifiers. If false, just success chance.</param>
        /// <param name="maxModifiers">Maximum number of individual modifiers to include when verbose</param>
        public string FormatForSpeech(MissionModifierBreakdown breakdown, bool verbose = false, int maxModifiers = 3)
        {
            var sb = new StringBuilder();

            sb.Append($"Success chance {breakdown.SuccessChance}. ");

            if (!verbose || !breakdown.IsContested)
            {
                return sb.ToString();
            }

            // Bonuses (sorted by absolute value, highest first)
            if (breakdown.Bonuses.Count > 0)
            {
                var sortedBonuses = new List<ModifierItem>(breakdown.Bonuses);
                sortedBonuses.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));

                sb.Append("Bonuses: ");
                int count = Math.Min(maxModifiers, sortedBonuses.Count);
                for (int i = 0; i < count; i++)
                {
                    var mod = sortedBonuses[i];
                    sb.Append($"{mod.Name} {FormatModifierValue(mod.Value)}");
                    if (i < count - 1)
                        sb.Append(", ");
                }

                if (sortedBonuses.Count > maxModifiers)
                {
                    sb.Append($" and {sortedBonuses.Count - maxModifiers} more");
                }

                sb.Append($". Total bonus {FormatModifierValue(breakdown.TotalBonus)}. ");
            }

            // Penalties (sorted by absolute value, highest first)
            if (breakdown.Penalties.Count > 0)
            {
                var sortedPenalties = new List<ModifierItem>(breakdown.Penalties);
                sortedPenalties.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));

                sb.Append("Target defenses: ");
                int count = Math.Min(maxModifiers, sortedPenalties.Count);
                for (int i = 0; i < count; i++)
                {
                    var mod = sortedPenalties[i];
                    sb.Append($"{mod.Name} {FormatModifierValue(mod.Value)}");
                    if (i < count - 1)
                        sb.Append(", ");
                }

                if (sortedPenalties.Count > maxModifiers)
                {
                    sb.Append($" and {sortedPenalties.Count - maxModifiers} more");
                }

                sb.Append($". Total defense {FormatModifierValue(breakdown.TotalPenalty)}. ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Format a modifier value with sign.
        /// </summary>
        private string FormatModifierValue(float value)
        {
            if (value >= 0)
                return $"+{value:N1}";
            else
                return $"{value:N1}";
        }
    }

    /// <summary>
    /// Contains the full modifier breakdown for a mission.
    /// </summary>
    public class MissionModifierBreakdown
    {
        /// <summary>
        /// Attacking/bonus modifiers that help the councilor.
        /// </summary>
        public List<ModifierItem> Bonuses { get; } = new List<ModifierItem>();

        /// <summary>
        /// Defending/penalty modifiers from the target.
        /// </summary>
        public List<ModifierItem> Penalties { get; } = new List<ModifierItem>();

        /// <summary>
        /// Sum of all attacking modifiers.
        /// </summary>
        public float TotalBonus { get; set; }

        /// <summary>
        /// Sum of all defending modifiers.
        /// </summary>
        public float TotalPenalty { get; set; }

        /// <summary>
        /// Formatted success chance string (e.g., "65%").
        /// </summary>
        public string SuccessChance { get; set; }

        /// <summary>
        /// Whether this is a contested mission (has modifiers) or uncontested.
        /// </summary>
        public bool IsContested { get; set; }

        /// <summary>
        /// Net modifier value (Bonus - Penalty).
        /// Positive favors attacker, negative favors defender.
        /// </summary>
        public float NetModifier => TotalBonus - TotalPenalty;
    }

    /// <summary>
    /// A single modifier item with name and value.
    /// </summary>
    public class ModifierItem
    {
        public string Name { get; set; }
        public float Value { get; set; }
    }
}
