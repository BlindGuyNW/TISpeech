using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for TICouncilorState objects.
    /// Extracts and formats councilor information for accessibility.
    /// </summary>
    public class CouncilorReader : IGameStateReader<TICouncilorState>
    {
        /// <summary>
        /// Callback for when a mission should be assigned.
        /// Set by CouncilScreen to handle mission assignment flow.
        /// </summary>
        public Action<TICouncilorState, TIMissionTemplate> OnAssignMission { get; set; }

        /// <summary>
        /// Callback for toggling councilor automation.
        /// </summary>
        public Action<TICouncilorState> OnToggleAutomation { get; set; }

        public string ReadSummary(TICouncilorState councilor)
        {
            if (councilor == null)
                return "Unknown councilor";

            var sb = new StringBuilder();
            sb.Append(councilor.displayName ?? "Unknown");

            // Add current mission if any
            if (councilor.activeMission != null)
            {
                sb.Append($", {councilor.activeMission.missionTemplate.displayName}");
                if (councilor.activeMission.target != null)
                {
                    sb.Append($" on {councilor.activeMission.target.displayName}");
                }
            }
            else
            {
                sb.Append(", no mission");
            }

            return sb.ToString();
        }

        public string ReadDetail(TICouncilorState councilor)
        {
            if (councilor == null)
                return "Unknown councilor";

            var sb = new StringBuilder();
            sb.AppendLine($"Councilor: {councilor.displayName}");
            sb.AppendLine($"Type: {councilor.typeTemplate?.displayName ?? "Unknown"}");
            sb.AppendLine($"Location: {GetLocationString(councilor)}");

            // Stats
            sb.AppendLine("Stats:");
            sb.AppendLine($"  Persuasion: {councilor.GetAttribute(CouncilorAttribute.Persuasion)}");
            sb.AppendLine($"  Investigation: {councilor.GetAttribute(CouncilorAttribute.Investigation)}");
            sb.AppendLine($"  Espionage: {councilor.GetAttribute(CouncilorAttribute.Espionage)}");
            sb.AppendLine($"  Command: {councilor.GetAttribute(CouncilorAttribute.Command)}");
            sb.AppendLine($"  Administration: {councilor.GetAttribute(CouncilorAttribute.Administration)}");
            sb.AppendLine($"  Science: {councilor.GetAttribute(CouncilorAttribute.Science)}");
            sb.AppendLine($"  Security: {councilor.GetAttribute(CouncilorAttribute.Security)}");
            sb.AppendLine($"  Loyalty: {councilor.GetAttribute(CouncilorAttribute.Loyalty)}");

            // Mission
            if (councilor.activeMission != null)
            {
                sb.AppendLine($"Current Mission: {councilor.activeMission.missionTemplate.displayName}");
                if (councilor.activeMission.target != null)
                {
                    sb.AppendLine($"  Target: {councilor.activeMission.target.displayName}");
                }
            }
            else
            {
                sb.AppendLine("Current Mission: None");
            }

            // Traits
            if (councilor.traits != null && councilor.traits.Count > 0)
            {
                sb.AppendLine("Traits:");
                foreach (var trait in councilor.traits)
                {
                    sb.AppendLine($"  {trait.displayName}");
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TICouncilorState councilor)
        {
            var sections = new List<ISection>();

            if (councilor == null)
                return sections;

            // Info section
            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", councilor.displayName);
            infoSection.AddItem("Type", councilor.typeTemplate?.displayName ?? "Unknown");

            if (councilor.activeMission != null)
            {
                var mission = councilor.activeMission;
                string missionInfo = mission.missionTemplate.displayName;
                if (mission.target != null)
                    missionInfo += $" on {mission.target.displayName}";
                infoSection.AddItem("Current Mission", missionInfo);
            }
            else
            {
                infoSection.AddItem("Current Mission", "None");
            }

            infoSection.AddItem("Location", GetLocationString(councilor));
            sections.Add(infoSection);

            // Stats section
            var statsSection = new DataSection("Stats");
            statsSection.AddItem("Persuasion", councilor.GetAttribute(CouncilorAttribute.Persuasion).ToString());
            statsSection.AddItem("Investigation", councilor.GetAttribute(CouncilorAttribute.Investigation).ToString());
            statsSection.AddItem("Espionage", councilor.GetAttribute(CouncilorAttribute.Espionage).ToString());
            statsSection.AddItem("Command", councilor.GetAttribute(CouncilorAttribute.Command).ToString());
            statsSection.AddItem("Administration", councilor.GetAttribute(CouncilorAttribute.Administration).ToString());
            statsSection.AddItem("Science", councilor.GetAttribute(CouncilorAttribute.Science).ToString());
            statsSection.AddItem("Security", councilor.GetAttribute(CouncilorAttribute.Security).ToString());
            statsSection.AddItem("Loyalty", councilor.GetAttribute(CouncilorAttribute.Loyalty).ToString());
            sections.Add(statsSection);

            // Traits section - with full descriptions available via detail read
            if (councilor.traits != null && councilor.traits.Count > 0)
            {
                var traitsSection = new DataSection("Traits");
                foreach (var trait in councilor.traits)
                {
                    // Use fullTraitSummary for complete trait info including effects
                    string traitDetail = TISpeechMod.CleanText(trait.fullTraitSummary);
                    traitsSection.AddItem(trait.displayName, "", traitDetail);
                }
                sections.Add(traitsSection);
            }

            // Orgs section
            if (councilor.orgs != null && councilor.orgs.Count > 0)
            {
                var orgsSection = new DataSection("Organizations");
                foreach (var org in councilor.orgs)
                {
                    orgsSection.AddItem(org.displayName);
                }
                sections.Add(orgsSection);
            }

            // Missions section (actionable)
            var missionsSection = BuildMissionsSection(councilor);
            if (missionsSection.ItemCount > 0)
            {
                sections.Add(missionsSection);
            }

            // Automation section
            var automationSection = new DataSection("Automation");
            string autoStatus = councilor.permanentDefenseMode ? "Enabled" : "Disabled";
            automationSection.AddItem("Auto-assign missions", autoStatus, onActivate: () =>
            {
                OnToggleAutomation?.Invoke(councilor);
            });
            sections.Add(automationSection);

            return sections;
        }

        private DataSection BuildMissionsSection(TICouncilorState councilor)
        {
            var missionsSection = new DataSection("Assign Mission");

            try
            {
                var possibleMissions = councilor.GetPossibleMissionList(filterForCouncilorConditions: true, sort: true);

                foreach (var mission in possibleMissions)
                {
                    try
                    {
                        // Check if mission can be afforded and has valid targets
                        bool canAfford = mission.CanAfford(councilor.faction, councilor);
                        int targetCount = mission.target?.GetValidTargets(mission, councilor)?.Count ?? 0;

                        if (canAfford && targetCount > 0)
                        {
                            // Capture for closure
                            var m = mission;
                            var c = councilor;

                            missionsSection.AddItem(mission.displayName, onActivate: () =>
                            {
                                OnAssignMission?.Invoke(c, m);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error checking mission {mission.displayName}: {ex.Message}");
                    }
                }

                if (missionsSection.ItemCount == 0)
                {
                    missionsSection.AddItem("No available missions");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building missions section: {ex.Message}");
                missionsSection.AddItem("Error loading missions");
            }

            return missionsSection;
        }

        private string GetLocationString(TICouncilorState councilor)
        {
            try
            {
                if (councilor.location != null)
                    return councilor.location.displayName ?? "Unknown location";
                return "Unknown location";
            }
            catch
            {
                return "Unknown location";
            }
        }
    }
}
