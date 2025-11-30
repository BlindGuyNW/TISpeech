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
    /// Reader for TICouncilorState objects.
    /// Extracts and formats councilor information for accessibility.
    /// Supports intel-gated viewing for enemy councilors.
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
            return ReadSummary(councilor, null);
        }

        /// <summary>
        /// Read summary with optional viewer for intel-gated enemy councilor viewing.
        /// </summary>
        public string ReadSummary(TICouncilorState councilor, TIFactionState viewer)
        {
            if (councilor == null)
                return "Unknown councilor";

            bool isOwn = viewer == null || councilor.faction == viewer;
            var sb = new StringBuilder();

            // Check intel levels for enemies
            bool hasBasicIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor));
            bool hasLocationIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorLocation(councilor));

            // For enemies with basic intel, show faction
            if (!isOwn && hasBasicIntel)
            {
                sb.Append($"{councilor.faction?.displayName ?? "Unknown"}: ");
            }

            // Name - show "Unknown Agent" if we only have location intel
            if (hasBasicIntel)
            {
                sb.Append(councilor.displayName ?? "Unknown");
            }
            else if (hasLocationIntel)
            {
                // At 0.10 intel: only know there's an agent at location, not name or faction
                sb.Append($"Unknown Agent at {councilor.location?.displayName ?? "unknown location"}");
                // Don't try to add mission info - just return here
                return sb.ToString();
            }
            else
            {
                sb.Append("Unknown");
            }

            // Add current mission if we can see it
            if (isOwn)
            {
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
            }
            else if (viewer != null && viewer.HasIntelOnCouncilorMission(councilor))
            {
                // Can see enemy mission at 0.75 intel
                if (councilor.activeMission != null)
                {
                    sb.Append($", {councilor.activeMission.missionTemplate.displayName}");
                }
                else
                {
                    sb.Append(", no mission");
                }
            }

            return sb.ToString();
        }

        public string ReadDetail(TICouncilorState councilor)
        {
            return ReadDetail(councilor, null);
        }

        /// <summary>
        /// Read detail with optional viewer for intel-gated enemy councilor viewing.
        /// </summary>
        public string ReadDetail(TICouncilorState councilor, TIFactionState viewer)
        {
            if (councilor == null)
                return "Unknown councilor";

            bool isOwn = viewer == null || councilor.faction == viewer;
            bool hasBasicIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor));
            bool hasLocationIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorLocation(councilor));

            var sb = new StringBuilder();

            // For location-only intel, show minimal info
            if (!hasBasicIntel && hasLocationIntel)
            {
                sb.AppendLine($"Unknown Agent");
                sb.AppendLine($"Faction: {councilor.faction?.displayName ?? "Unknown"}");
                sb.AppendLine($"Location: {councilor.location?.displayName ?? "Unknown"}");
                sb.AppendLine();
                sb.AppendLine("Intel level: Location only (0.10)");
                sb.AppendLine("Use Investigate Councilor to learn more about this agent.");
                return sb.ToString();
            }

            // Has basic intel (0.25+) - show full details based on intel level
            sb.AppendLine($"Councilor: {councilor.displayName}");

            // For enemies, show faction
            if (!isOwn)
            {
                sb.AppendLine($"Faction: {councilor.faction?.displayName ?? "Unknown"}");
            }

            sb.AppendLine($"Type: {councilor.typeTemplate?.displayName ?? "Unknown"}");

            // Biographical info (available at basic intel level 0.25)
            sb.AppendLine($"Age: {councilor.age}");
            sb.AppendLine($"Hometown: {GetHometownString(councilor)}");
            sb.AppendLine($"Gender: {GetGenderString(councilor.gender)}");

            sb.AppendLine($"Location: {GetLocationString(councilor, viewer)}");

            // Stats - require details intel (0.50) for enemies
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                sb.AppendLine("Stats:");
                sb.AppendLine($"  Persuasion: {councilor.GetAttribute(CouncilorAttribute.Persuasion)}");
                sb.AppendLine($"  Investigation: {councilor.GetAttribute(CouncilorAttribute.Investigation)}");
                sb.AppendLine($"  Espionage: {councilor.GetAttribute(CouncilorAttribute.Espionage)}");
                sb.AppendLine($"  Command: {councilor.GetAttribute(CouncilorAttribute.Command)}");
                sb.AppendLine($"  Administration: {councilor.GetAttribute(CouncilorAttribute.Administration)}");
                sb.AppendLine($"  Science: {councilor.GetAttribute(CouncilorAttribute.Science)}");
                sb.AppendLine($"  Security: {councilor.GetAttribute(CouncilorAttribute.Security)}");

                // Loyalty - require secrets intel (1.0) for true loyalty
                if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorSecrets(councilor)))
                {
                    sb.AppendLine($"  Loyalty: {councilor.GetAttribute(CouncilorAttribute.Loyalty)}");
                }
                else
                {
                    sb.AppendLine($"  Loyalty: {councilor.GetAttribute(CouncilorAttribute.ApparentLoyalty)} (apparent)");
                }
            }

            // Mission - require mission intel (0.75) for enemies
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorMission(councilor)))
            {
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
            }

            // Traits - require details intel (0.50) for enemies
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                if (councilor.traits != null && councilor.traits.Count > 0)
                {
                    sb.AppendLine("Traits:");
                    foreach (var trait in councilor.traits)
                    {
                        sb.AppendLine($"  {trait.displayName}");
                    }
                }
            }
            else if (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor))
            {
                // At basic intel, can only see "easily visible" traits
                var visibleTraits = councilor.traits?.Where(t => t.easilyVisible).ToList();
                if (visibleTraits != null && visibleTraits.Count > 0)
                {
                    sb.AppendLine("Visible Traits:");
                    foreach (var trait in visibleTraits)
                    {
                        sb.AppendLine($"  {trait.displayName}");
                    }
                }
            }

            return sb.ToString();
        }

        public List<ISection> GetSections(TICouncilorState councilor)
        {
            return GetSections(councilor, null);
        }

        /// <summary>
        /// Get sections with optional viewer for intel-gated enemy councilor viewing.
        /// </summary>
        public List<ISection> GetSections(TICouncilorState councilor, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            if (councilor == null)
                return sections;

            bool isOwn = viewer == null || councilor.faction == viewer;
            bool hasBasicIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor));
            bool hasLocationIntel = isOwn || (viewer != null && viewer.HasIntelOnCouncilorLocation(councilor));

            // For location-only intel, show minimal info
            if (!hasBasicIntel && hasLocationIntel)
            {
                var unknownSection = new DataSection("Unknown Agent");
                unknownSection.AddItem("Faction", councilor.faction?.displayName ?? "Unknown");
                unknownSection.AddItem("Location", councilor.location?.displayName ?? "Unknown");
                unknownSection.AddItem("Intel Level", "Location only (0.10)");
                unknownSection.AddItem("Tip", "Use Investigate Councilor to learn more");
                sections.Add(unknownSection);
                return sections;
            }

            // Info section - available at basic intel level (0.25)
            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", councilor.displayName);

            // For enemies, show faction
            if (!isOwn)
            {
                infoSection.AddItem("Faction", councilor.faction?.displayName ?? "Unknown");
            }

            infoSection.AddItem("Type", councilor.typeTemplate?.displayName ?? "Unknown");

            // Biographical info
            infoSection.AddItem("Age", councilor.age.ToString());
            infoSection.AddItem("Hometown", GetHometownString(councilor));
            infoSection.AddItem("Gender", GetGenderString(councilor.gender));

            // Mission info - gated by intel level
            if (isOwn)
            {
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
            }
            else if (viewer != null && viewer.HasIntelOnCouncilorMission(councilor))
            {
                // Can see enemy mission at 0.75 intel
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
            }
            else
            {
                infoSection.AddItem("Current Mission", "Unknown");
            }

            infoSection.AddItem("Location", GetLocationString(councilor, viewer));
            sections.Add(infoSection);

            // Stats section - requires details intel (0.50) for enemies
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
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

                // Loyalty - require secrets intel (1.0) for true loyalty
                if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorSecrets(councilor)))
                {
                    statsSection.AddItem("Loyalty", councilor.GetAttribute(CouncilorAttribute.Loyalty).ToString(),
                        GetStatDetailText(councilor, CouncilorAttribute.Loyalty));
                }
                else
                {
                    statsSection.AddItem("Loyalty", $"{councilor.GetAttribute(CouncilorAttribute.ApparentLoyalty)} (apparent)",
                        "True loyalty unknown - requires full intel");
                }
                sections.Add(statsSection);
            }

            // Traits section - requires details intel (0.50) for full list, basic intel shows easily visible only
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                if (councilor.traits != null && councilor.traits.Count > 0)
                {
                    var traitsSection = new DataSection("Traits");
                    foreach (var trait in councilor.traits)
                    {
                        string traitDetail = TISpeechMod.CleanText(trait.fullTraitSummary);
                        traitsSection.AddItem(trait.displayName, "", traitDetail);
                    }
                    sections.Add(traitsSection);
                }
            }
            else if (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor))
            {
                // At basic intel, can only see "easily visible" traits
                var visibleTraits = councilor.traits?.Where(t => t.easilyVisible).ToList();
                if (visibleTraits != null && visibleTraits.Count > 0)
                {
                    var traitsSection = new DataSection("Visible Traits");
                    foreach (var trait in visibleTraits)
                    {
                        string traitDetail = TISpeechMod.CleanText(trait.fullTraitSummary);
                        traitsSection.AddItem(trait.displayName, "", traitDetail);
                    }
                    sections.Add(traitsSection);
                }
            }

            // Orgs section - requires details intel (0.50) for enemies
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                var orgsSection = BuildOrganizationsSection(councilor, isOwn);
                sections.Add(orgsSection);
            }

            // ACTION SECTIONS - Only for own councilors!
            if (isOwn)
            {
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

        private string GetLocationString(TICouncilorState councilor, TIFactionState viewer = null)
        {
            try
            {
                bool isOwn = viewer == null || councilor.faction == viewer;

                // For enemies, check if we have location intel
                if (!isOwn && viewer != null && !viewer.HasIntelOnCouncilorLocation(councilor))
                {
                    return "Unknown location";
                }

                if (councilor.location != null)
                    return councilor.location.displayName ?? "Unknown location";
                return "Unknown location";
            }
            catch
            {
                return "Unknown location";
            }
        }

        private string GetHometownString(TICouncilorState councilor)
        {
            try
            {
                if (councilor.homeRegion != null)
                {
                    // Use the game's built-in method for formatted hometown string
                    return councilor.GetHomeLocationString();
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetGenderString(CouncilorGender gender)
        {
            switch (gender)
            {
                case CouncilorGender.Male:
                    return "Male";
                case CouncilorGender.Female:
                    return "Female";
                case CouncilorGender.Nonbinary:
                    return "Nonbinary";
                case CouncilorGender.None:
                default:
                    return "Unknown";
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

        private DataSection BuildOrganizationsSection(TICouncilorState councilor, bool isOwn)
        {
            var orgsSection = new DataSection("Organizations");

            try
            {
                // Show org capacity
                int currentOrgs = councilor.orgs?.Count ?? 0;
                int capacity = councilor.GetAttribute(CouncilorAttribute.Administration);
                orgsSection.AddItem("Org Capacity", $"{currentOrgs}/{capacity}");

                // Add each existing org
                if (councilor.orgs != null)
                {
                    foreach (var org in councilor.orgs)
                    {
                        // Build detail text with org bonuses
                        string detail = GetOrgDetailText(org);

                        if (isOwn)
                        {
                            // For own councilors, make orgs activatable to manage
                            var orgCopy = org;
                            var councilorCopy = councilor;
                            orgsSection.AddItem(org.displayName, $"Tier {org.tier}", detail, onActivate: () =>
                            {
                                OnManageOrg?.Invoke(councilorCopy, orgCopy);
                            });
                        }
                        else
                        {
                            // For enemies, orgs are read-only
                            orgsSection.AddItem(org.displayName, $"Tier {org.tier}", detail);
                        }
                    }
                }

                // Add "Acquire Organization" option only for own councilors
                if (isOwn)
                {
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
