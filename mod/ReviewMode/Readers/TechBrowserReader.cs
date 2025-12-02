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
    /// Reader for the Tech Browser item.
    /// Provides sections organized by tech category, with techs as section items.
    /// </summary>
    public class TechBrowserReader
    {
        public string ReadSummary()
        {
            try
            {
                int completed = TIGlobalResearchState.FinishedTechs()?.Count ?? 0;
                int available = TIGlobalResearchState.UnlockedTechs?.Count ?? 0;
                int total = TIGlobalResearchState.GetAllTechs()?.Count ?? 0;

                return $"Tech Browser: {completed} completed, {available} available, {total} total";
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading tech browser summary: {ex.Message}");
                return "Tech Browser";
            }
        }

        public string ReadDetail()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Tech Browser");
                sb.AppendLine();

                var finishedTechs = TIGlobalResearchState.FinishedTechs() ?? new List<TITechTemplate>();
                var allTechs = TIGlobalResearchState.GetAllTechs() ?? new List<TITechTemplate>();

                int completed = finishedTechs.Count;
                int available = TIGlobalResearchState.UnlockedTechs?.Count ?? 0;
                int total = allTechs.Count;
                int locked = total - completed - available;

                sb.AppendLine($"Completed: {completed}");
                sb.AppendLine($"Available: {available}");
                sb.AppendLine($"Locked: {locked}");
                sb.AppendLine($"Total: {total}");
                sb.AppendLine();
                sb.AppendLine("Completed by category:");

                // Count by category
                var categoryStats = new Dictionary<TechCategory, int>();
                foreach (TechCategory cat in Enum.GetValues(typeof(TechCategory)))
                {
                    categoryStats[cat] = 0;
                }

                foreach (var tech in finishedTechs)
                {
                    categoryStats[tech.techCategory]++;
                }

                foreach (var kvp in categoryStats)
                {
                    int categoryTotal = allTechs.Count(t => t.techCategory == kvp.Key);
                    sb.AppendLine($"  {FormatCategory(kvp.Key)}: {kvp.Value}/{categoryTotal}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading tech browser detail: {ex.Message}");
                return "Error loading tech browser details";
            }
        }

        public List<ISection> GetSections()
        {
            // Tech Browser shows category sections as a way to select a tech
            // Each category section lists techs - selecting one shows its details
            var sections = new List<ISection>();

            try
            {
                var allTechs = TIGlobalResearchState.GetAllTechs();
                if (allTechs == null || allTechs.Count == 0)
                    return sections;

                var finishedTechs = TIGlobalResearchState.FinishedTechs() ?? new List<TITechTemplate>();
                var globalResearch = TIGlobalResearchState.globalResearch;

                // Group techs by category
                var techsByCategory = allTechs
                    .GroupBy(t => t.techCategory)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var group in techsByCategory)
                {
                    var category = group.Key;
                    var techsInCategory = group.OrderBy(t => t.displayName).ToList();

                    int completed = techsInCategory.Count(t => finishedTechs.Contains(t));
                    int total = techsInCategory.Count;

                    var section = new DataSection($"{FormatCategory(category)} ({completed}/{total})");

                    foreach (var tech in techsInCategory)
                    {
                        string status = GetTechStatus(tech, finishedTechs, globalResearch);
                        string label = $"{tech.displayName} [{status}]";

                        // Build a brief summary for the section item
                        string brief = BuildTechBrief(tech, finishedTechs);

                        // Add as drillable item - user can drill in to see detailed sections
                        section.AddDrillableItem(label, tech.dataName, brief);
                    }

                    sections.Add(section);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building tech browser sections: {ex.Message}");
            }

            return sections;
        }

        /// <summary>
        /// Get detailed sections for a specific tech (when drilling into it from the browser).
        /// </summary>
        public List<ISection> GetSectionsForTech(string techDataName)
        {
            var sections = new List<ISection>();

            try
            {
                var tech = TemplateManager.Find<TITechTemplate>(techDataName);
                if (tech == null)
                    return sections;

                var faction = GameControl.control?.activePlayer;
                var finishedTechs = TIGlobalResearchState.FinishedTechs() ?? new List<TITechTemplate>();

                // Overview section with full game description
                var overview = new DataSection("Overview");
                overview.AddItem("Name", tech.displayName);
                overview.AddItem("Category", FormatCategory(tech.techCategory));
                overview.AddItem("Cost", $"{tech.researchCost:F0}");
                if (tech.endGameTech)
                    overview.AddItem("Type", "End-game technology");

                // Use the game's full description which includes description + warnings + effects
                // Archive context includes the full description text
                try
                {
                    string fullDesc = tech.GetFullDescription(faction, TechBenefitsContext.Archive);
                    if (!string.IsNullOrEmpty(fullDesc))
                    {
                        overview.AddItem("Description", TISpeechMod.CleanText(fullDesc));
                    }
                }
                catch { }
                sections.Add(overview);

                // Prerequisites section
                if (tech.TechPrereqs != null && tech.TechPrereqs.Count > 0)
                {
                    var prereqSection = new DataSection("Prerequisites");
                    foreach (var prereq in tech.TechPrereqs)
                    {
                        bool completed = finishedTechs.Contains(prereq as TITechTemplate);
                        string status = completed ? "Completed" : "Needed";
                        prereqSection.AddItem(prereq.displayName, status);
                    }
                    sections.Add(prereqSection);
                }

                // Unlocks orgs - match game behavior (just indicate orgs are unlocked, don't list names)
                if (tech.orgTypeUnlocks != null && tech.orgTypeUnlocks.Count > 0)
                {
                    var orgsSection = new DataSection("Unlocks");
                    orgsSection.AddItem("Unlocks new organizations for purchase on the Org Market");
                    sections.Add(orgsSection);
                }

                // What this tech unlocks (other techs/projects)
                try
                {
                    string unlocks = tech.UnlockableTechString(faction, TechBenefitsContext.Prospective);
                    if (!string.IsNullOrEmpty(unlocks))
                    {
                        var unlocksSection = new DataSection("Unlocks Research");
                        unlocksSection.AddItem(TISpeechMod.CleanText(unlocks));
                        sections.Add(unlocksSection);
                    }
                }
                catch { }

                // What requires this tech
                try
                {
                    string prereqFor = tech.PrereqForStr_Archive(faction, withholdDirectUnlocks: true);
                    if (!string.IsNullOrEmpty(prereqFor))
                    {
                        var reqForSection = new DataSection("Required For");
                        reqForSection.AddItem(TISpeechMod.CleanText(prereqFor));
                        sections.Add(reqForSection);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building tech sections: {ex.Message}");
            }

            return sections;
        }

        private string BuildTechBrief(TITechTemplate tech, List<TITechTemplate> finishedTechs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{tech.displayName}");
            sb.AppendLine($"Category: {FormatCategory(tech.techCategory)}, Cost: {tech.researchCost:F0}");

            // Brief summary (drill into sections for full details)
            try
            {
                string summary = tech.summary;
                if (!string.IsNullOrEmpty(summary) && summary != "<skip/>")
                {
                    sb.AppendLine();
                    sb.AppendLine(TISpeechMod.CleanText(summary));
                }
            }
            catch { }

            return sb.ToString();
        }

        private string GetTechStatus(TITechTemplate tech, List<TITechTemplate> finishedTechs, TIGlobalResearchState globalResearch)
        {
            // Check if completed
            if (finishedTechs.Contains(tech))
                return "Completed";

            // Check if currently researching
            if (globalResearch != null)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    var progress = globalResearch.GetTechProgress(slot);
                    if (progress?.techTemplate == tech)
                    {
                        float percent = progress.progressFrac * 100f;
                        return $"In Progress {percent:F0}%";
                    }
                }
            }

            // Check if available (prerequisites met)
            if (tech.TechPrereqsSatisfied(finishedTechs))
                return "Available";

            return "Locked";
        }

        private string FormatCategory(TechCategory category)
        {
            return category switch
            {
                TechCategory.Materials => "Materials",
                TechCategory.SpaceScience => "Space Science",
                TechCategory.Energy => "Energy",
                TechCategory.LifeScience => "Life Science",
                TechCategory.MilitaryScience => "Military Science",
                TechCategory.InformationScience => "Information Science",
                TechCategory.SocialScience => "Social Science",
                TechCategory.Xenology => "Xenology",
                _ => category.ToString()
            };
        }
    }
}
