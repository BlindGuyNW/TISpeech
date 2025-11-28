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

        /// <summary>
        /// Callback for applying an augmentation (spending XP).
        /// </summary>
        public Action<TICouncilorState, CouncilorAugmentationOption> OnApplyAugmentation { get; set; }

        /// <summary>
        /// Callback for managing an org (show actions menu).
        /// </summary>
        public Action<TICouncilorState, TIOrgState> OnManageOrg { get; set; }

        /// <summary>
        /// Callback for acquiring a new org for this councilor.
        /// </summary>
        public Action<TICouncilorState> OnAcquireOrg { get; set; }

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

            // Stats section - with detailed tooltips from game
            var statsSection = new DataSection("Stats");
            statsSection.AddItem("Persuasion", councilor.GetAttribute(CouncilorAttribute.Persuasion).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Persuasion));
            statsSection.AddItem("Investigation", councilor.GetAttribute(CouncilorAttribute.Investigation).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Investigation));
            statsSection.AddItem("Espionage", councilor.GetAttribute(CouncilorAttribute.Espionage).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Espionage));
            statsSection.AddItem("Command", councilor.GetAttribute(CouncilorAttribute.Command).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Command));
            statsSection.AddItem("Administration", councilor.GetAttribute(CouncilorAttribute.Administration).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Administration));
            statsSection.AddItem("Science", councilor.GetAttribute(CouncilorAttribute.Science).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Science));
            statsSection.AddItem("Security", councilor.GetAttribute(CouncilorAttribute.Security).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Security));
            statsSection.AddItem("Loyalty", councilor.GetAttribute(CouncilorAttribute.Loyalty).ToString(),
                GetStatDetailText(councilor, CouncilorAttribute.Loyalty));
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

            // Orgs section - now actionable
            var orgsSection = BuildOrganizationsSection(councilor);
            sections.Add(orgsSection);

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

            // Spend XP section
            var xpSection = BuildSpendXPSection(councilor);
            if (xpSection.ItemCount > 0)
            {
                sections.Add(xpSection);
            }

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

        private string GetStatDetailText(TICouncilorState councilor, CouncilorAttribute attribute)
        {
            try
            {
                // Use the game's built-in stat detail method
                string detail = CouncilGridController.StatDetail(councilor, attribute);
                return TISpeechMod.CleanText(detail);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting stat detail for {attribute}: {ex.Message}");
                return "";
            }
        }

        private DataSection BuildOrganizationsSection(TICouncilorState councilor)
        {
            var orgsSection = new DataSection("Organizations");

            try
            {
                // Show org capacity
                int currentOrgs = councilor.orgs?.Count ?? 0;
                int capacity = councilor.GetAttribute(CouncilorAttribute.Administration);
                orgsSection.AddItem("Org Capacity", $"{currentOrgs}/{capacity}");

                // Add each existing org (activatable to manage)
                if (councilor.orgs != null)
                {
                    foreach (var org in councilor.orgs)
                    {
                        var orgCopy = org;
                        var councilorCopy = councilor;

                        // Build detail text with org bonuses
                        string detail = GetOrgDetailText(org);

                        orgsSection.AddItem(org.displayName, $"Tier {org.tier}", detail, onActivate: () =>
                        {
                            OnManageOrg?.Invoke(councilorCopy, orgCopy);
                        });
                    }
                }

                // Add "Acquire Organization" option if there's capacity
                if (currentOrgs < capacity)
                {
                    var councilorCopy = councilor;
                    orgsSection.AddItem("Acquire Organization", "Browse available orgs", onActivate: () =>
                    {
                        OnAcquireOrg?.Invoke(councilorCopy);
                    });
                }
                else
                {
                    orgsSection.AddItem("Acquire Organization", "At capacity - cannot acquire more orgs");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building organizations section: {ex.Message}");
                orgsSection.AddItem("Error loading organizations");
            }

            return orgsSection;
        }

        private string GetOrgDetailText(TIOrgState org)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Organization: {org.displayName}");
            sb.AppendLine($"Tier: {org.tier}");

            if (org.template != null)
            {
                sb.AppendLine($"Type: {org.template.orgType}");
            }

            // Key bonuses
            if (org.incomeMoney_month != 0) sb.AppendLine($"Money/month: {org.incomeMoney_month:+#;-#;0}");
            if (org.incomeInfluence_month != 0) sb.AppendLine($"Influence/month: {org.incomeInfluence_month:+#;-#;0}");
            if (org.incomeOps_month != 0) sb.AppendLine($"Operations/month: {org.incomeOps_month:+#;-#;0}");
            if (org.incomeResearch_month != 0) sb.AppendLine($"Research/month: {org.incomeResearch_month:+#;-#;0}");

            // Stat bonuses
            if (org.persuasion != 0) sb.AppendLine($"Persuasion: {org.persuasion:+#;-#;0}");
            if (org.command != 0) sb.AppendLine($"Command: {org.command:+#;-#;0}");
            if (org.investigation != 0) sb.AppendLine($"Investigation: {org.investigation:+#;-#;0}");
            if (org.espionage != 0) sb.AppendLine($"Espionage: {org.espionage:+#;-#;0}");
            if (org.administration != 0) sb.AppendLine($"Administration: {org.administration:+#;-#;0}");
            if (org.science != 0) sb.AppendLine($"Science: {org.science:+#;-#;0}");
            if (org.security != 0) sb.AppendLine($"Security: {org.security:+#;-#;0}");

            return sb.ToString();
        }

        private DataSection BuildSpendXPSection(TICouncilorState councilor)
        {
            var xpSection = new DataSection("Spend XP");

            try
            {
                // Show current XP
                int currentXP = councilor.XP;
                xpSection.AddItem("Available XP", currentXP.ToString());

                // Get all candidate augmentations
                var augmentations = councilor.GetCandidateAugmentations();

                foreach (var aug in augmentations)
                {
                    try
                    {
                        // Get display strings from the game API
                        aug.SetAugmentationStrings(out string description1, out string description2, out string tooltipDescription, out string costString);

                        // Clean text for screen reader
                        string label = TISpeechMod.CleanText(description1);
                        string value = TISpeechMod.CleanText(description2);
                        string detail = TISpeechMod.CleanText(tooltipDescription);
                        string cost = TISpeechMod.CleanText(costString);

                        // Build the item label
                        string itemLabel = !string.IsNullOrEmpty(value) ? $"{label}: {value}" : label;

                        // Check affordability
                        bool canAfford = aug.CouncilorCanAfford(councilor);

                        if (canAfford)
                        {
                            // Affordable - make it activatable
                            var augCopy = aug; // Capture for closure
                            var councilorCopy = councilor;

                            xpSection.AddItem(itemLabel, cost, detail, onActivate: () =>
                            {
                                OnApplyAugmentation?.Invoke(councilorCopy, augCopy);
                            });
                        }
                        else
                        {
                            // Not affordable - show as read-only with reason
                            string notAffordableValue = $"Cannot afford: {cost} (have {currentXP} XP)";
                            xpSection.AddItem(itemLabel, notAffordableValue, detail);
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"Error processing augmentation option: {ex.Message}");
                    }
                }

                if (xpSection.ItemCount <= 1) // Only the "Available XP" item
                {
                    xpSection.AddItem("No augmentations available");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building Spend XP section: {ex.Message}");
                xpSection.AddItem("Error loading augmentations");
            }

            return xpSection;
        }
    }
}
