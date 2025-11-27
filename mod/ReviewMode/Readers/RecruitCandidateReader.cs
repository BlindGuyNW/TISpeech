using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for councilor recruitment candidates.
    /// Shows base stats (without orgs) and recruitment cost.
    /// </summary>
    public class RecruitCandidateReader : IGameStateReader<TICouncilorState>
    {
        /// <summary>
        /// Callback for when a candidate should be recruited.
        /// </summary>
        public Action<TICouncilorState> OnRecruit { get; set; }

        public string ReadSummary(TICouncilorState candidate)
        {
            if (candidate == null)
                return "Unknown candidate";

            var faction = GameControl.control?.activePlayer;
            if (faction == null)
                return candidate.displayName ?? "Unknown";

            string cost = TISpeechMod.CleanText(candidate.GetRecruitCostString(faction));
            string profession = candidate.typeTemplate?.displayName ?? "Unknown";

            return $"{candidate.displayName}, {profession}, {cost}";
        }

        public string ReadDetail(TICouncilorState candidate)
        {
            if (candidate == null)
                return "Unknown candidate";

            var faction = GameControl.control?.activePlayer;
            var sb = new StringBuilder();

            sb.AppendLine($"Recruit Candidate: {candidate.displayName}");
            sb.AppendLine($"Profession: {candidate.typeTemplate?.displayName ?? "Unknown"}");

            if (faction != null)
            {
                sb.AppendLine($"Cost: {TISpeechMod.CleanText(candidate.GetRecruitCostString(faction))}");
            }

            // Base stats (without orgs)
            sb.AppendLine("Base Stats:");
            sb.AppendLine($"  Persuasion: {candidate.GetAttribute(CouncilorAttribute.Persuasion, includeOrgs: false)}");
            sb.AppendLine($"  Investigation: {candidate.GetAttribute(CouncilorAttribute.Investigation, includeOrgs: false)}");
            sb.AppendLine($"  Espionage: {candidate.GetAttribute(CouncilorAttribute.Espionage, includeOrgs: false)}");
            sb.AppendLine($"  Command: {candidate.GetAttribute(CouncilorAttribute.Command, includeOrgs: false)}");
            sb.AppendLine($"  Administration: {candidate.GetAttribute(CouncilorAttribute.Administration, includeOrgs: false)}");
            sb.AppendLine($"  Science: {candidate.GetAttribute(CouncilorAttribute.Science, includeOrgs: false)}");
            sb.AppendLine($"  Security: {candidate.GetAttribute(CouncilorAttribute.Security, includeOrgs: false)}");
            sb.AppendLine($"  Apparent Loyalty: {candidate.GetAttribute(CouncilorAttribute.ApparentLoyalty, includeOrgs: false)}");

            // Location/Origin
            try
            {
                if (candidate.homeNation != null)
                {
                    sb.AppendLine($"Nationality: {candidate.homeNation.displayName}");
                }
            }
            catch { }

            // Traits
            if (candidate.traits != null && candidate.traits.Count > 0)
            {
                sb.AppendLine("Traits:");
                foreach (var trait in candidate.traits)
                {
                    sb.AppendLine($"  {trait.displayName}");
                }
            }

            // Missions this profession can perform
            if (candidate.typeTemplate?.missions != null && candidate.typeTemplate.missions.Count > 0)
            {
                sb.AppendLine("Available Missions:");
                int count = 0;
                foreach (var mission in candidate.typeTemplate.missions)
                {
                    sb.Append($"  {mission.displayName}");
                    count++;
                    if (count >= 5) // Limit to first 5
                    {
                        sb.Append($" and {candidate.typeTemplate.missions.Count - 5} more");
                        break;
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TICouncilorState candidate)
        {
            var sections = new List<ISection>();

            if (candidate == null)
                return sections;

            var faction = GameControl.control?.activePlayer;

            // Overview section
            var overview = new DataSection("Overview");
            overview.AddItem("Name", candidate.displayName);
            overview.AddItem("Profession", candidate.typeTemplate?.displayName ?? "Unknown");
            if (faction != null)
            {
                overview.AddItem("Cost", TISpeechMod.CleanText(candidate.GetRecruitCostString(faction)));
            }
            try
            {
                if (candidate.homeNation != null)
                {
                    overview.AddItem("Nationality", candidate.homeNation.displayName);
                }
            }
            catch { }
            sections.Add(overview);

            // Base Stats section (without orgs - important for recruitment decision)
            var stats = new DataSection("Base Stats");
            stats.AddItem("Persuasion", candidate.GetAttribute(CouncilorAttribute.Persuasion, includeOrgs: false).ToString());
            stats.AddItem("Investigation", candidate.GetAttribute(CouncilorAttribute.Investigation, includeOrgs: false).ToString());
            stats.AddItem("Espionage", candidate.GetAttribute(CouncilorAttribute.Espionage, includeOrgs: false).ToString());
            stats.AddItem("Command", candidate.GetAttribute(CouncilorAttribute.Command, includeOrgs: false).ToString());
            stats.AddItem("Administration", candidate.GetAttribute(CouncilorAttribute.Administration, includeOrgs: false).ToString());
            stats.AddItem("Science", candidate.GetAttribute(CouncilorAttribute.Science, includeOrgs: false).ToString());
            stats.AddItem("Security", candidate.GetAttribute(CouncilorAttribute.Security, includeOrgs: false).ToString());
            stats.AddItem("Apparent Loyalty", candidate.GetAttribute(CouncilorAttribute.ApparentLoyalty, includeOrgs: false).ToString());
            sections.Add(stats);

            // Traits section - with full descriptions available via detail read
            if (candidate.traits != null && candidate.traits.Count > 0)
            {
                var traits = new DataSection("Traits");
                foreach (var trait in candidate.traits)
                {
                    // Use fullTraitSummary for complete trait info including effects
                    string traitDetail = TISpeechMod.CleanText(trait.fullTraitSummary);
                    traits.AddItem(trait.displayName, "", traitDetail);
                }
                sections.Add(traits);
            }

            // Missions section (what this profession can do)
            if (candidate.typeTemplate?.missions != null && candidate.typeTemplate.missions.Count > 0)
            {
                var missions = new DataSection("Available Missions");
                foreach (var mission in candidate.typeTemplate.missions)
                {
                    missions.AddItem(mission.displayName);
                }
                sections.Add(missions);
            }

            // Recruit action
            var actions = new DataSection("Actions");
            bool canRecruit = CanRecruitCandidate(candidate, faction);
            string recruitLabel = canRecruit ? "Recruit this candidate" : "Cannot recruit (check cost or slots)";

            actions.AddItem(recruitLabel, onActivate: canRecruit ? () =>
            {
                OnRecruit?.Invoke(candidate);
            } : (Action)null);
            sections.Add(actions);

            return sections;
        }

        /// <summary>
        /// Check if a candidate can be recruited.
        /// </summary>
        public bool CanRecruitCandidate(TICouncilorState candidate, TIFactionState faction)
        {
            if (candidate == null || faction == null)
                return false;

            try
            {
                // Check if we have council slots
                if (faction.councilors.Count >= faction.maxCouncilSize)
                    return false;

                // Check if we can afford the cost
                var cost = candidate.HireRecruitCost(faction);
                if (!cost.CanAfford(faction))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error checking recruit eligibility: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get recruitment status string.
        /// </summary>
        public string GetRecruitmentStatus(TIFactionState faction)
        {
            if (faction == null)
                return "No faction";

            int current = faction.councilors.Count;
            int max = faction.maxCouncilSize;
            int available = faction.availableCouncilors?.Count ?? 0;

            return $"Council: {current}/{max} councilors. {available} candidates available.";
        }
    }
}
