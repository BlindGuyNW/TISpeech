using PavonisInteractive.TerraInvicta;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Data class for cost option selection (building modules, founding habs, etc.)
    /// Used by both HabReader and SpaceBodyReader for consistent cost confirmation flows.
    /// </summary>
    public class CostOptionData
    {
        public TIResourcesCost Cost { get; set; }
        public bool CanAfford { get; set; }
        public string Source { get; set; }  // "Info", "Earth", "Space", etc.
    }

    /// <summary>
    /// Shared utility for formatting TIResourcesCost objects consistently across all readers.
    /// Uses the game's built-in formatting with sprite-to-text conversion.
    /// </summary>
    public static class CostFormatter
    {
        /// <summary>
        /// Format cost with completion time using the game's built-in formatting.
        /// Output example: "Boost 10 Money 50 30 days"
        /// </summary>
        public static string FormatWithTime(TIResourcesCost cost, TIFactionState faction = null)
        {
            if (cost == null)
                return "Unknown cost";

            // Use the game's GetString method which properly formats all resources with sprites
            // Parameters: format, includeCostStr, includeCompletionTime, completionTimeOnly, relevantCap, costsOnly, gainsOnly, faction
            string gameFormatted = cost.GetString("Relevant", includeCostStr: false, includeCompletionTime: true,
                completionTimeOnly: false, relevantCap: 7, costsOnly: false, gainsOnly: false, faction: faction);

            // Clean the text to convert sprites to readable labels
            return TISpeechMod.CleanText(gameFormatted).Trim();
        }

        /// <summary>
        /// Format cost without completion time using the game's built-in formatting.
        /// Output example: "Boost 10 Money 50"
        /// </summary>
        public static string FormatCostOnly(TIResourcesCost cost, TIFactionState faction = null)
        {
            if (cost == null)
                return "Unknown cost";

            // Use the game's ToString method which formats resources with sprites
            string gameFormatted = cost.ToString("Relevant", gainsOnly: false, costsOnly: false, faction: faction);

            // Clean the text to convert sprites to readable labels
            return TISpeechMod.CleanText(gameFormatted).Trim();
        }

        /// <summary>
        /// Format cost showing just the completion time in days.
        /// </summary>
        public static string FormatTimeOnly(TIResourcesCost cost)
        {
            if (cost == null)
                return "Unknown";

            int days = (int)cost.completionTime_days;
            if (days <= 0)
                return "Immediate";
            if (days == 1)
                return "1 day";
            return $"{days} days";
        }

        /// <summary>
        /// Check if the faction can afford this cost.
        /// </summary>
        public static bool CanAfford(TIResourcesCost cost, TIFactionState faction)
        {
            if (cost == null || faction == null)
                return false;
            return cost.CanAfford(faction);
        }
    }
}
