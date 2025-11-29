using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for research slots (0-5).
    /// Slots 0-2 are global techs, slots 3-5 are faction projects.
    /// </summary>
    public class ResearchSlotReader
    {
        /// <summary>
        /// Callback for entering selection mode (for tech/project selection).
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback to refresh the screen after changes.
        /// </summary>
        public Action OnRefresh { get; set; }

        public string ReadSummary(int slot)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                var globalResearch = TIGlobalResearchState.globalResearch;

                if (faction == null || globalResearch == null)
                    return $"Slot {slot + 1}: Data unavailable";

                int priority = faction.researchWeights[slot];
                string priorityStr = priority > 0 ? $"Priority {priority}" : "No priority";

                if (slot < 3)
                {
                    // Global tech slot - check if selection is pending (research completed)
                    if (NeedsSelection(slot, faction, globalResearch))
                    {
                        return $"Slot {slot + 1}: Select new technology, {priorityStr}";
                    }

                    var progress = globalResearch.GetTechProgress(slot);
                    if (progress?.techTemplate != null)
                    {
                        var tech = progress.techTemplate;
                        float percent = progress.progressFrac * 100f;
                        return $"Slot {slot + 1}: {tech.displayName}, {percent:F0}%, {priorityStr}";
                    }
                    return $"Slot {slot + 1}: No tech selected, {priorityStr}";
                }
                else
                {
                    // Faction project slot - check if selection is pending
                    string slotName = GetProjectSlotName(slot);
                    if (NeedsSelection(slot, faction, globalResearch))
                    {
                        return $"{slotName}: Select new project, {priorityStr}";
                    }

                    var projectProgress = faction.GetProjectProgressInSlot(slot);
                    if (projectProgress?.projectTemplate != null)
                    {
                        var project = projectProgress.projectTemplate;
                        float percent = projectProgress.progressFrac(faction) * 100f;
                        return $"{slotName}: {project.displayName}, {percent:F0}%, {priorityStr}";
                    }
                    return $"{slotName}: No project selected, {priorityStr}";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading slot {slot} summary: {ex.Message}");
                return $"Slot {slot + 1}: Error";
            }
        }

        /// <summary>
        /// Check if a slot needs the player to select a new tech/project.
        /// This happens when research completes and the player won or needs to choose the next one.
        /// </summary>
        private bool NeedsSelection(int slot, TIFactionState faction, TIGlobalResearchState globalResearch)
        {
            try
            {
                if (slot < 3)
                {
                    // Global tech slot - check for pending PromptSelectTech
                    return TIPromptQueueState.HasPromptStatic(faction, globalResearch, null, "PromptSelectTech", slot);
                }
                else
                {
                    // Faction project slot - check for pending PromptSelectProject
                    return TIPromptQueueState.HasPromptStatic(faction, faction, null, "PromptSelectProject", slot);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking selection state for slot {slot}: {ex.Message}");
                return false;
            }
        }

        public string ReadDetail(int slot)
        {
            try
            {
                var faction = GameControl.control?.activePlayer;
                var globalResearch = TIGlobalResearchState.globalResearch;

                if (faction == null || globalResearch == null)
                    return "Research data unavailable";

                var sb = new StringBuilder();
                int priority = faction.researchWeights[slot];

                if (slot < 3)
                {
                    // Global tech slot
                    sb.AppendLine($"Global Research Slot {slot + 1}");

                    // Check if selection is pending (research completed)
                    if (NeedsSelection(slot, faction, globalResearch))
                    {
                        sb.AppendLine("Status: Research completed - select new technology");
                        sb.AppendLine($"Priority: {priority}");
                        sb.AppendLine("Use Enter to select a new technology for this slot.");
                        return sb.ToString();
                    }

                    var progress = globalResearch.GetTechProgress(slot);

                    if (progress?.techTemplate != null)
                    {
                        var tech = progress.techTemplate;
                        sb.AppendLine($"Technology: {tech.displayName}");
                        sb.AppendLine($"Category: {FormatCategory(tech.techCategory)}");
                        sb.AppendLine($"Progress: {progress.accumulatedResearch:F0} / {tech.GetResearchCost(null):F0} ({progress.progressFrac * 100:F1}%)");

                        // Flavor text (what the game shows in the panel)
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

                        // Completion estimate
                        var completionDate = globalResearch.TechCompletionDate(slot);
                        if (completionDate != null)
                        {
                            sb.AppendLine($"Estimated completion: {completionDate}");
                        }

                        // Leader
                        var leader = globalResearch.Leader(slot);
                        if (leader != null)
                        {
                            sb.AppendLine($"Leading faction: {leader.displayName}");
                        }

                        // Player's win/lose status
                        bool cantWin = progress.CantWin(faction);
                        bool cantLose = progress.CantLose(faction);
                        if (cantLose)
                            sb.AppendLine("Status: Guaranteed to win");
                        else if (cantWin)
                            sb.AppendLine("Status: Cannot win");
                        else
                            sb.AppendLine("Status: Competitive");
                    }
                    else
                    {
                        sb.AppendLine("No technology selected");
                    }

                    sb.AppendLine($"Priority: {priority}");
                }
                else
                {
                    // Faction project slot
                    string slotName = GetProjectSlotName(slot);
                    sb.AppendLine($"{slotName}");

                    // Check if selection is pending (project completed)
                    if (NeedsSelection(slot, faction, globalResearch))
                    {
                        sb.AppendLine("Status: Project completed - select new project");
                        sb.AppendLine($"Priority: {priority}");
                        sb.AppendLine("Use Enter to select a new project for this slot.");
                        return sb.ToString();
                    }

                    var projectProgress = faction.GetProjectProgressInSlot(slot);
                    if (projectProgress?.projectTemplate != null)
                    {
                        var project = projectProgress.projectTemplate;
                        sb.AppendLine($"Project: {project.displayName}");
                        sb.AppendLine($"Category: {FormatCategory(project.techCategory)}");
                        float cost = project.GetResearchCost(faction);
                        sb.AppendLine($"Progress: {projectProgress.accumulatedResearch:F0} / {cost:F0} ({projectProgress.progressFrac(faction) * 100:F1}%)");

                        // Flavor text (what the game shows in the panel)
                        try
                        {
                            string summary = project.summary;
                            if (!string.IsNullOrEmpty(summary) && summary != "<skip/>")
                            {
                                sb.AppendLine();
                                sb.AppendLine(TISpeechMod.CleanText(summary));
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        sb.AppendLine("No project selected");
                    }

                    sb.AppendLine($"Priority: {priority}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading slot {slot} detail: {ex.Message}");
                return "Error loading slot details";
            }
        }

        public List<ISection> GetSections(int slot)
        {
            var sections = new List<ISection>();

            try
            {
                var faction = GameControl.control?.activePlayer;
                var globalResearch = TIGlobalResearchState.globalResearch;

                if (faction == null || globalResearch == null)
                    return sections;

                // 1. Overview section
                sections.Add(BuildOverviewSection(slot, faction, globalResearch));

                // 2. Priority section (with actions)
                sections.Add(BuildPrioritySection(slot, faction));

                // 3. Contributions section (global slots only)
                if (slot < 3)
                {
                    sections.Add(BuildContributionsSection(slot, faction, globalResearch));
                }

                // 4. Research rate section
                sections.Add(BuildResearchRateSection(slot, faction, globalResearch));

                // 5. Tech/Project details section
                if (slot < 3)
                {
                    sections.Add(BuildTechDetailsSection(slot, globalResearch));
                }
                else
                {
                    sections.Add(BuildProjectDetailsSection(slot, faction));
                }

                // 6. Select tech/project section (if applicable)
                var selectionSection = BuildSelectionSection(slot, faction, globalResearch);
                if (selectionSection != null && selectionSection.ItemCount > 0)
                {
                    sections.Add(selectionSection);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building sections for slot {slot}: {ex.Message}");
            }

            return sections;
        }

        private DataSection BuildOverviewSection(int slot, TIFactionState faction, TIGlobalResearchState globalResearch)
        {
            var section = new DataSection("Overview");

            if (slot < 3)
            {
                // Global tech - check for pending selection first
                if (NeedsSelection(slot, faction, globalResearch))
                {
                    section.AddItem("Status", "Research completed - select new technology");
                    section.AddItem("Action", "Use Enter to select a technology", onActivate: () =>
                    {
                        StartTechSelection(slot, faction);
                    });
                    return section;
                }

                var progress = globalResearch.GetTechProgress(slot);
                if (progress?.techTemplate != null)
                {
                    var tech = progress.techTemplate;
                    section.AddItem("Technology", tech.displayName);
                    section.AddItem("Category", FormatCategory(tech.techCategory));
                    section.AddItem("Progress", $"{progress.progressFrac * 100:F1}%");
                    section.AddItem("Research", $"{progress.accumulatedResearch:F0} / {tech.GetResearchCost(null):F0}");

                    var completionDate = globalResearch.TechCompletionDate(slot);
                    section.AddItem("Est. Completion", completionDate?.ToString() ?? "Unknown");

                    if (tech.endGameTech)
                        section.AddItem("Type", "End-game technology");
                }
                else
                {
                    section.AddItem("Status", "No technology selected");
                }
            }
            else
            {
                // Faction project - check for pending selection first
                string slotName = GetProjectSlotName(slot);
                section.AddItem("Slot", slotName);

                if (NeedsSelection(slot, faction, globalResearch))
                {
                    section.AddItem("Status", "Project completed - select new project");
                    section.AddItem("Action", "Use Enter to select a project", onActivate: () =>
                    {
                        StartProjectSelection(slot, faction);
                    });
                    return section;
                }

                var projectProgress = faction.GetProjectProgressInSlot(slot);
                if (projectProgress?.projectTemplate != null)
                {
                    var project = projectProgress.projectTemplate;
                    section.AddItem("Project", project.displayName);
                    section.AddItem("Category", FormatCategory(project.techCategory));
                    float cost = project.GetResearchCost(faction);
                    section.AddItem("Progress", $"{projectProgress.progressFrac(faction) * 100:F1}%");
                    section.AddItem("Research", $"{projectProgress.accumulatedResearch:F0} / {cost:F0}");
                }
                else
                {
                    section.AddItem("Status", "No project selected");
                }
            }

            return section;
        }

        private DataSection BuildPrioritySection(int slot, TIFactionState faction)
        {
            var section = new DataSection("Priority");

            int priority = faction.researchWeights[slot];
            string priorityName = GetPriorityName(priority);
            section.AddItem("Current Priority", $"{priority} ({priorityName})");

            // Calculate allocation percentage
            float slotShare = faction.FractionWeightInSlot(slot);
            section.AddItem("Research Allocation", $"{slotShare * 100:F1}%");

            // Single cycle action
            section.AddItem("Cycle Priority", $"Currently {priorityName}", onActivate: () =>
            {
                CyclePriority(slot, faction);
            });

            return section;
        }

        private string GetPriorityName(int priority)
        {
            return priority switch
            {
                0 => "None",
                1 => "Low",
                2 => "Medium",
                3 => "High",
                _ => priority.ToString()
            };
        }

        private DataSection BuildContributionsSection(int slot, TIFactionState faction, TIGlobalResearchState globalResearch)
        {
            var section = new DataSection("Contributions");

            var progress = globalResearch.GetTechProgress(slot);
            if (progress?.techTemplate == null)
            {
                section.AddItem("No tech in progress");
                return section;
            }

            // Leader
            var leader = globalResearch.Leader(slot);
            section.AddItem("Leader", leader?.displayName ?? "None");

            // Player's status
            bool cantWin = progress.CantWin(faction);
            bool cantLose = progress.CantLose(faction);
            string status = cantLose ? "Guaranteed win" : (cantWin ? "Cannot win" : "Competitive");
            section.AddItem("Your Status", status);

            // Faction contributions
            if (progress.factionContributions != null && progress.factionContributions.Count > 0)
            {
                float totalContribution = progress.factionContributions.Values.Sum();

                var sortedContributions = progress.factionContributions
                    .OrderByDescending(kvp => kvp.Value)
                    .ToList();

                foreach (var kvp in sortedContributions)
                {
                    float percent = totalContribution > 0 ? (kvp.Value / totalContribution * 100) : 0;
                    string marker = kvp.Key == faction ? " (you)" : "";
                    string leaderMarker = kvp.Key == leader ? " [Leader]" : "";
                    section.AddItem($"{kvp.Key.displayName}{marker}{leaderMarker}", $"{percent:F1}%");
                }
            }

            return section;
        }

        private DataSection BuildResearchRateSection(int slot, TIFactionState faction, TIGlobalResearchState globalResearch)
        {
            var section = new DataSection("Research Rate");

            // Daily research income
            float dailyResearch = faction.GetDailyIncome(FactionResource.Research);
            section.AddItem("Daily Research Income", $"{dailyResearch:F1}");

            // Slot allocation
            float slotShare = faction.FractionWeightInSlot(slot);
            float dailyToSlot = dailyResearch * slotShare;
            section.AddItem("Daily to This Slot", $"{dailyToSlot:F1}");

            // Category modifiers (if there's a tech/project)
            TechCategory? category = null;
            if (slot < 3)
            {
                var progress = globalResearch.GetTechProgress(slot);
                category = progress?.techTemplate?.techCategory;
            }
            else
            {
                var projectProgress = faction.GetProjectProgressInSlot(slot);
                category = projectProgress?.projectTemplate?.techCategory;
            }

            if (category.HasValue)
            {
                float categoryMod = faction.SumCategoryModifiers(category.Value);
                section.AddItem($"{FormatCategory(category.Value)} Modifier", $"+{categoryMod * 100:F0}%");

                float effectiveRate = faction.GetEffectiveResearchPerDay(category.Value, slot >= 3);
                section.AddItem("Effective Rate", $"{effectiveRate:F1}/day");
            }

            return section;
        }

        private DataSection BuildTechDetailsSection(int slot, TIGlobalResearchState globalResearch)
        {
            var section = new DataSection("Tech Details");

            var progress = globalResearch.GetTechProgress(slot);
            if (progress?.techTemplate == null)
            {
                section.AddItem("No tech to display");
                return section;
            }

            var tech = progress.techTemplate;
            var faction = GameControl.control?.activePlayer;

            // Effects
            if (tech.Effects != null && tech.Effects.Count > 0)
            {
                var effectDescriptions = new List<string>();
                foreach (var effect in tech.Effects)
                {
                    try
                    {
                        string desc = effect.description(faction, null);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            desc = TISpeechMod.CleanText(desc);
                            if (!string.IsNullOrWhiteSpace(desc))
                                effectDescriptions.Add(desc);
                        }
                    }
                    catch { }
                }
                if (effectDescriptions.Count > 0)
                {
                    section.AddItem("Effects", string.Join("; ", effectDescriptions));
                }
            }

            // Org unlocks
            if (tech.orgTypeUnlocks != null && tech.orgTypeUnlocks.Count > 0)
            {
                var orgNames = tech.orgTypeUnlocks
                    .Select(dataName =>
                    {
                        try
                        {
                            var orgTemplate = TemplateManager.Find<TIOrgTemplate>(dataName);
                            return orgTemplate?.displayName ?? dataName;
                        }
                        catch { return dataName; }
                    });
                section.AddItem("Unlocks Orgs", string.Join(", ", orgNames));
            }

            // What this tech unlocks (other techs/projects)
            try
            {
                string unlocks = tech.UnlockableTechString(faction, TechBenefitsContext.Prospective);
                if (!string.IsNullOrEmpty(unlocks))
                {
                    section.AddItem("Unlocks", TISpeechMod.CleanText(unlocks));
                }
            }
            catch { }

            // What requires this tech
            try
            {
                string prereqFor = tech.PrereqForStr_Archive(faction, withholdDirectUnlocks: true);
                if (!string.IsNullOrEmpty(prereqFor))
                {
                    section.AddItem("Required For", TISpeechMod.CleanText(prereqFor));
                }
            }
            catch { }

            return section;
        }

        private DataSection BuildProjectDetailsSection(int slot, TIFactionState faction)
        {
            var section = new DataSection("Project Details");

            var projectProgress = faction.GetProjectProgressInSlot(slot);
            if (projectProgress?.projectTemplate == null)
            {
                section.AddItem("No project to display");
                return section;
            }

            var project = projectProgress.projectTemplate;

            // Effects
            if (project.Effects != null && project.Effects.Count > 0)
            {
                var effectDescriptions = new List<string>();
                foreach (var effect in project.Effects)
                {
                    try
                    {
                        string desc = effect.description(faction, null);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            desc = TISpeechMod.CleanText(desc);
                            if (!string.IsNullOrWhiteSpace(desc))
                                effectDescriptions.Add(desc);
                        }
                    }
                    catch { }
                }
                if (effectDescriptions.Count > 0)
                {
                    section.AddItem("Effects", string.Join("; ", effectDescriptions));
                }
            }

            // Resources granted
            if (project.resourcesGranted != null && project.resourcesGranted.Count > 0)
            {
                var resources = project.resourcesGranted
                    .Where(r => r.resource != FactionResource.None && r.value != 0)
                    .Select(r => $"{r.resource}: {r.value:F0}")
                    .ToList();
                if (resources.Count > 0)
                {
                    section.AddItem("Grants Resources", string.Join(", ", resources));
                }
            }

            // Org granted
            if (!string.IsNullOrEmpty(project.orgGranted))
            {
                try
                {
                    var orgTemplate = TemplateManager.Find<TIOrgTemplate>(project.orgGranted);
                    section.AddItem("Grants Org", orgTemplate?.displayName ?? project.orgGranted);
                }
                catch
                {
                    section.AddItem("Grants Org", project.orgGranted);
                }
            }

            // Ship parts / hab modules unlocked
            try
            {
                string unlockDetails = project.AllUnlocksDetails(includeHeader: false, truncateDescriptions: true);
                if (!string.IsNullOrEmpty(unlockDetails))
                {
                    section.AddItem("Unlocks", TISpeechMod.CleanText(unlockDetails));
                }
            }
            catch { }

            // Repeatable (always show this as it's useful info)
            section.AddItem("Repeatable", project.repeatable ? "Yes" : "No");

            return section;
        }

        private DataSection BuildSelectionSection(int slot, TIFactionState faction, TIGlobalResearchState globalResearch)
        {
            var section = new DataSection("Actions");

            if (slot < 3)
            {
                // Global tech slot
                bool needsSelection = NeedsSelection(slot, faction, globalResearch);
                var progress = globalResearch.GetTechProgress(slot);

                // If selection is pending, show prominent action
                if (needsSelection)
                {
                    section.AddItem("Select Technology (Required)", "Choose a new technology to research", onActivate: () =>
                    {
                        StartTechSelection(slot, faction);
                    });
                }
                else if (progress != null && progress.selector == faction)
                {
                    // We're the selector - can change if we want
                    section.AddItem("Select New Technology", onActivate: () =>
                    {
                        StartTechSelection(slot, faction);
                    });
                }
                else
                {
                    // Show who can select
                    if (progress?.selector != null)
                    {
                        section.AddItem("Selector", $"{progress.selector.displayName} chooses next tech");
                    }
                }
            }
            else
            {
                // Faction project slot
                bool needsSelection = NeedsSelection(slot, faction, globalResearch);

                if (needsSelection)
                {
                    section.AddItem("Select Project (Required)", "Choose a new project to develop", onActivate: () =>
                    {
                        StartProjectSelection(slot, faction);
                    });
                }
                else
                {
                    section.AddItem("Change Project", onActivate: () =>
                    {
                        StartProjectSelection(slot, faction);
                    });
                }
            }

            return section;
        }

        private void CyclePriority(int slot, TIFactionState faction)
        {
            try
            {
                var action = new CycleResearchPriorityAction(faction, slot, decrement: false);
                faction.playerControl.StartAction(action);

                // Read the actual new value from game state (not calculated)
                int newPriority = faction.researchWeights[slot];
                string priorityName = GetPriorityName(newPriority);
                OnSpeak?.Invoke($"Priority: {priorityName}", true);
                OnRefresh?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error cycling priority: {ex.Message}");
                OnSpeak?.Invoke("Error changing priority", true);
            }
        }

        private void StartTechSelection(int slot, TIFactionState faction)
        {
            try
            {
                var unlockedTechs = TIGlobalResearchState.UnlockedTechs;
                if (unlockedTechs == null || unlockedTechs.Count == 0)
                {
                    OnSpeak?.Invoke("No technologies available to research", true);
                    return;
                }

                // Filter out techs already being researched
                var globalResearch = TIGlobalResearchState.globalResearch;
                var currentTechs = new HashSet<TITechTemplate>();
                for (int i = 0; i < 3; i++)
                {
                    var progress = globalResearch.GetTechProgress(i);
                    if (progress?.techTemplate != null)
                        currentTechs.Add(progress.techTemplate);
                }

                var options = new List<SelectionOption>();
                foreach (var tech in unlockedTechs)
                {
                    if (currentTechs.Contains(tech))
                        continue;

                    options.Add(new SelectionOption
                    {
                        Label = $"{tech.displayName} ({FormatCategory(tech.techCategory)})",
                        DetailText = BuildTechDetailText(tech),
                        Data = tech
                    });
                }

                if (options.Count == 0)
                {
                    OnSpeak?.Invoke("All available technologies are already being researched", true);
                    return;
                }

                OnEnterSelectionMode?.Invoke($"Select technology for slot {slot + 1}", options, (index) =>
                {
                    ExecuteTechSelection(slot, (TITechTemplate)options[index].Data, faction);
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting tech selection: {ex.Message}");
                OnSpeak?.Invoke("Error starting tech selection", true);
            }
        }

        private void ExecuteTechSelection(int slot, TITechTemplate tech, TIFactionState faction)
        {
            try
            {
                var action = new SelectTechAction(faction, slot, tech);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Now researching {tech.displayName} in slot {slot + 1}", true);
                OnRefresh?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting tech: {ex.Message}");
                OnSpeak?.Invoke("Error selecting technology", true);
            }
        }

        private void StartProjectSelection(int slot, TIFactionState faction)
        {
            try
            {
                var selectableProjects = faction.SelectableProjects(slot);
                if (selectableProjects == null || selectableProjects.Count == 0)
                {
                    OnSpeak?.Invoke("No projects available for this slot", true);
                    return;
                }

                var options = new List<SelectionOption>();
                foreach (var project in selectableProjects)
                {
                    options.Add(new SelectionOption
                    {
                        Label = $"{project.displayName} ({FormatCategory(project.techCategory)})",
                        DetailText = BuildProjectDetailText(project, faction),
                        Data = project
                    });
                }

                string slotName = GetProjectSlotName(slot);
                OnEnterSelectionMode?.Invoke($"Select project for {slotName}", options, (index) =>
                {
                    ExecuteProjectSelection(slot, (TIProjectTemplate)options[index].Data, faction);
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error starting project selection: {ex.Message}");
                OnSpeak?.Invoke("Error starting project selection", true);
            }
        }

        private void ExecuteProjectSelection(int slot, TIProjectTemplate project, TIFactionState faction)
        {
            try
            {
                var action = new SelectProjectForDevelopmentAction(faction, slot, project);
                faction.playerControl.StartAction(action);

                string slotName = GetProjectSlotName(slot);
                OnSpeak?.Invoke($"Now developing {project.displayName} in {slotName}", true);
                OnRefresh?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error selecting project: {ex.Message}");
                OnSpeak?.Invoke("Error selecting project", true);
            }
        }

        private string BuildTechDetailText(TITechTemplate tech)
        {
            var sb = new StringBuilder();
            sb.AppendLine(tech.displayName);
            sb.AppendLine($"Category: {FormatCategory(tech.techCategory)}");
            sb.AppendLine($"Cost: {tech.researchCost:F0}");

            if (tech.endGameTech)
                sb.AppendLine("End-game technology");

            if (tech.TechPrereqs != null && tech.TechPrereqs.Count > 0)
            {
                sb.AppendLine($"Prerequisites: {string.Join(", ", tech.TechPrereqs.Select(t => t.displayName))}");
            }

            if (tech.orgTypeUnlocks != null && tech.orgTypeUnlocks.Count > 0)
            {
                var orgNames = tech.orgTypeUnlocks
                    .Select(dataName =>
                    {
                        try
                        {
                            var orgTemplate = TemplateManager.Find<TIOrgTemplate>(dataName);
                            return orgTemplate?.displayName ?? dataName;
                        }
                        catch { return dataName; }
                    });
                sb.AppendLine($"Unlocks: {string.Join(", ", orgNames)}");
            }

            return sb.ToString();
        }

        private string BuildProjectDetailText(TIProjectTemplate project, TIFactionState faction)
        {
            var sb = new StringBuilder();
            sb.AppendLine(project.displayName);
            sb.AppendLine($"Category: {FormatCategory(project.techCategory)}");
            sb.AppendLine($"Cost: {project.GetResearchCost(faction):F0}");

            if (project.repeatable)
                sb.AppendLine("Repeatable");

            if (!string.IsNullOrEmpty(project.orgGranted))
            {
                try
                {
                    var orgTemplate = TemplateManager.Find<TIOrgTemplate>(project.orgGranted);
                    sb.AppendLine($"Grants org: {orgTemplate?.displayName ?? project.orgGranted}");
                }
                catch
                {
                    sb.AppendLine($"Grants org: {project.orgGranted}");
                }
            }

            if (project.resourcesGranted != null && project.resourcesGranted.Count > 0)
            {
                var resources = project.resourcesGranted.Select(r => $"{r.resource}: {r.value}");
                sb.AppendLine($"Grants: {string.Join(", ", resources)}");
            }

            return sb.ToString();
        }

        private string GetProjectSlotName(int slot)
        {
            return slot switch
            {
                3 => "HQ Project",
                4 => "Org Project",
                5 => "Hab Project",
                _ => $"Project Slot {slot - 2}"
            };
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
