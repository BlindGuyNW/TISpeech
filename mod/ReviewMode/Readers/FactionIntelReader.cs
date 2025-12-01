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
    /// Reader for enemy faction intel.
    /// Uses FactionView pattern for intel-gated access to faction information.
    /// </summary>
    public class FactionIntelReader : IGameStateReader<TIFactionState>
    {
        public string ReadSummary(TIFactionState faction)
        {
            return ReadSummary(faction, null);
        }

        /// <summary>
        /// Read summary with viewer for intel-gated display.
        /// Uses FactionView for proper intel gating.
        /// </summary>
        public string ReadSummary(TIFactionState faction, TIFactionState viewer)
        {
            if (faction == null)
                return "Unknown faction";

            var sb = new StringBuilder();
            sb.Append(faction.displayName ?? "Unknown");

            if (viewer != null)
            {
                // Use FactionView for intel-gated access
                var factionView = new FactionView(faction, viewer);

                // Show leader if we have intel (FactionView handles gating)
                if (factionView.showLeader)
                {
                    sb.Append($", Leader: {factionView.leader}");
                }

                // Show hate level toward player
                float hate = faction.GetFactionHate(viewer);
                string stance = GetHateDescription(hate);
                sb.Append($", {stance}");

                // Show intel level
                float intel = viewer.GetIntel(faction);
                int intelPercent = (int)(intel * 100);
                sb.Append($", Intel: {intelPercent}%");
            }

            return sb.ToString();
        }

        public string ReadDetail(TIFactionState faction)
        {
            return ReadDetail(faction, null);
        }

        /// <summary>
        /// Read detailed faction info with intel gating via FactionView.
        /// </summary>
        public string ReadDetail(TIFactionState faction, TIFactionState viewer)
        {
            if (faction == null)
                return "Unknown faction";

            var sb = new StringBuilder();
            sb.AppendLine($"Faction: {faction.displayName}");

            // Ideology stance (always visible)
            string alienStance = faction.proAlien ? "Pro-Alien" : (faction.antiAlien ? "Anti-Alien" : "Neutral");
            if (faction.veryProAlien) alienStance = "Strongly Pro-Alien";
            if (faction.veryAntiAlien) alienStance = "Strongly Anti-Alien";
            sb.AppendLine($"Ideology: {alienStance}");

            if (viewer != null)
            {
                var factionView = new FactionView(faction, viewer);
                float intel = viewer.GetIntel(faction);

                // Leader info (intel-gated via FactionView)
                if (factionView.showLeader)
                {
                    sb.AppendLine($"Leader: {factionView.fullLeader}");
                }

                // Relationship with viewer
                float hate = faction.GetFactionHate(viewer);
                string stance = GetHateDescription(hate);
                sb.AppendLine($"Stance toward you: {stance} ({hate:F1})");

                // Intel level
                int intelPercent = (int)(intel * 100);
                sb.AppendLine($"Intel Level: {intelPercent}%");

                // Councilor count (needs basic intel)
                if (factionView.showLeader)
                {
                    int councilorCount = faction.councilors?.Count ?? 0;
                    sb.AppendLine($"Councilors: {councilorCount}");
                }

                // Control points
                if (factionView.showLeader)
                {
                    int controlPoints = faction.controlPoints?.Count ?? 0;
                    sb.AppendLine($"Control Points: {controlPoints}");
                }

                // Resources (intel-gated via FactionView.GetResourceString)
                string moneyStr = factionView.GetResourceString(FactionResource.Money);
                if (moneyStr != Loc.T("UI.CouncilorView.UnknownSymbol"))
                {
                    sb.AppendLine();
                    sb.AppendLine("Resources:");
                    sb.AppendLine($"  Money: {factionView.GetResourceString(FactionResource.Money)}");
                    sb.AppendLine($"  Influence: {factionView.GetResourceString(FactionResource.Influence)}");
                    sb.AppendLine($"  Operations: {factionView.GetResourceString(FactionResource.Operations)}");
                    sb.AppendLine($"  Boost: {factionView.GetResourceString(FactionResource.Boost)}");
                    sb.AppendLine($"  Mission Control: {factionView.GetResourceString(FactionResource.MissionControl)}");
                    sb.AppendLine($"  Research: {factionView.GetResourceString(FactionResource.Research)}");
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TIFactionState faction)
        {
            return GetSections(faction, null);
        }

        /// <summary>
        /// Get sections with intel gating via FactionView.
        /// </summary>
        public List<ISection> GetSections(TIFactionState faction, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            if (faction == null)
                return sections;

            FactionView factionView = viewer != null
                ? new FactionView(faction, viewer)
                : new FactionView(faction, faction);
            float intel = viewer?.GetIntel(faction) ?? 1f;

            // Basic Info - always visible
            var infoSection = new DataSection("Info");
            infoSection.AddItem("Faction", faction.displayName);

            string alienStance = faction.proAlien ? "Pro-Alien" : (faction.antiAlien ? "Anti-Alien" : "Neutral");
            if (faction.veryProAlien) alienStance = "Strongly Pro-Alien";
            if (faction.veryAntiAlien) alienStance = "Strongly Anti-Alien";
            infoSection.AddItem("Ideology", alienStance);

            // Leader (intel-gated via FactionView)
            if (factionView.showLeader)
            {
                infoSection.AddItem("Leader", factionView.fullLeader);
            }

            if (viewer != null)
            {
                float hate = faction.GetFactionHate(viewer);
                string stance = GetHateDescription(hate);
                infoSection.AddItem("Stance toward you", $"{stance} ({hate:F1})");

                int intelPercent = (int)(intel * 100);
                infoSection.AddItem("Intel Level", $"{intelPercent}%");
            }

            if (factionView.showLeader)
            {
                infoSection.AddItem("Councilors", (faction.councilors?.Count ?? 0).ToString());
                infoSection.AddItem("Control Points", (faction.controlPoints?.Count ?? 0).ToString());
            }
            sections.Add(infoSection);

            // Councilors section - use CouncilorView for each councilor
            if (factionView.showLeader && faction.councilors != null && faction.councilors.Count > 0)
            {
                var councilorsSection = new DataSection("Councilors");
                foreach (var councilor in faction.councilors)
                {
                    if (viewer != null)
                    {
                        var councilorView = new CouncilorView(councilor, viewer);
                        string name = councilorView.displayNameCurrent;
                        string location = councilorView.locationString(false);
                        if (string.IsNullOrEmpty(location)) location = "Unknown";
                        string mission = councilorView.GetCurrentMissionString(includeTarget: false);

                        councilorsSection.AddItem(name, $"{location}, {mission}");
                    }
                    else
                    {
                        councilorsSection.AddItem(councilor.displayName, councilor.location?.displayName ?? "Unknown");
                    }
                }
                sections.Add(councilorsSection);
            }

            // Resources section - use FactionView.GetResourceString for intel gating
            string testResource = factionView.GetResourceString(FactionResource.Money);
            if (testResource != Loc.T("UI.CouncilorView.UnknownSymbol"))
            {
                var resourcesSection = new DataSection("Resources");
                resourcesSection.AddItem("Money", factionView.GetResourceString(FactionResource.Money));
                resourcesSection.AddItem("Influence", factionView.GetResourceString(FactionResource.Influence));
                resourcesSection.AddItem("Operations", factionView.GetResourceString(FactionResource.Operations));
                resourcesSection.AddItem("Boost", factionView.GetResourceString(FactionResource.Boost));
                resourcesSection.AddItem("Mission Control", factionView.GetResourceString(FactionResource.MissionControl));
                resourcesSection.AddItem("Research", factionView.GetResourceString(FactionResource.Research));
                resourcesSection.AddItem("Projects", factionView.GetResourceString(FactionResource.Projects));

                // Space commodities
                resourcesSection.AddItem("Water", factionView.GetResourceString(FactionResource.Water));
                resourcesSection.AddItem("Volatiles", factionView.GetResourceString(FactionResource.Volatiles));
                resourcesSection.AddItem("Metals", factionView.GetResourceString(FactionResource.Metals));
                resourcesSection.AddItem("Noble Metals", factionView.GetResourceString(FactionResource.NobleMetals));
                resourcesSection.AddItem("Fissiles", factionView.GetResourceString(FactionResource.Fissiles));

                sections.Add(resourcesSection);
            }

            // Objectives section - use FactionView.GetObjectives for intel gating
            var objectives = factionView.GetObjectives(ObjectiveType.Campaign, ObjectiveStatus.Unlocked);
            if (objectives != null && objectives.Count > 0)
            {
                var objectivesSection = new DataSection("Objectives");
                try
                {
                    foreach (var obj in objectives)
                    {
                        string progress = GetObjectiveProgress(faction, obj);
                        string description = obj.description(faction);
                        objectivesSection.AddItem(obj.displayName(faction), progress, TISpeechMod.CleanText(description));
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error getting objectives: {ex.Message}");
                    objectivesSection.AddItem("Unable to read objectives");
                }
                sections.Add(objectivesSection);
            }

            // Relations section - always visible (diplomacy is public)
            var relationsSection = new DataSection("Relations");
            try
            {
                var allFactions = GameStateManager.AllHumanFactions();
                foreach (var otherFaction in allFactions)
                {
                    if (otherFaction == faction) continue;

                    float hate = faction.GetFactionHate(otherFaction);
                    string stance = GetHateDescription(hate);
                    relationsSection.AddItem(otherFaction.displayName, $"{stance} ({hate:F1})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting relations: {ex.Message}");
                relationsSection.AddItem("Unable to read relations");
            }
            sections.Add(relationsSection);

            // Projects section - use FactionView.currentProjectProgress for intel gating
            var projectProgress = factionView.currentProjectProgress;
            if (projectProgress != null && projectProgress.Count > 0)
            {
                var projectsSection = new DataSection("Active Projects");
                try
                {
                    foreach (var proj in projectProgress)
                    {
                        if (proj.projectTemplate != null)
                        {
                            int progressPercent = (int)(proj.progressFrac(faction) * 100);
                            projectsSection.AddItem(proj.projectTemplate.displayName, $"{progressPercent}% complete");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error getting projects: {ex.Message}");
                    projectsSection.AddItem("Unable to read projects");
                }
                sections.Add(projectsSection);
            }

            return sections;
        }

        /// <summary>
        /// Get all non-player human factions for browsing.
        /// </summary>
        public static List<TIFactionState> GetAllEnemyFactions(TIFactionState playerFaction)
        {
            var factions = new List<TIFactionState>();
            try
            {
                var allFactions = GameStateManager.AllHumanFactions();
                foreach (var faction in allFactions)
                {
                    if (faction != playerFaction)
                    {
                        factions.Add(faction);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting factions: {ex.Message}");
            }
            return factions;
        }

        private string FormatResource(TIFactionState faction, FactionResource resource)
        {
            try
            {
                float value = faction.GetCurrentResourceAmount(resource);
                if (resource == FactionResource.Money)
                {
                    return $"${value:N0}";
                }
                return value.ToString("N1");
            }
            catch
            {
                return "?";
            }
        }

        private string GetHateDescription(float hate)
        {
            if (hate >= 10) return "At War";
            if (hate >= 5) return "Hostile";
            if (hate >= 2) return "Unfriendly";
            if (hate >= -2) return "Neutral";
            if (hate >= -5) return "Friendly";
            return "Allied";
        }

        private string GetObjectiveProgress(TIFactionState faction, TIObjectiveTemplate objective)
        {
            try
            {
                var status = faction.GetObjectiveStatus(objective);
                if (status == ObjectiveStatus.Completed)
                    return "Completed";
                if (status == ObjectiveStatus.Locked)
                    return "Locked";
                return "In Progress";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
