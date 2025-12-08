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

        /// <summary>
        /// Callback for dismissing a councilor.
        /// Parameters: councilor, keepOrgs (true to move orgs to pool, false to sell)
        /// </summary>
        public Action<TICouncilorState, bool> OnDismissCouncilor { get; set; }

        /// <summary>
        /// Callback for aborting/canceling an active mission.
        /// </summary>
        public Action<TICouncilorState> OnAbortMission { get; set; }

        /// <summary>
        /// Callback for setting autofail value for turned councilors.
        /// Parameters: councilor, new value (0.0 to 1.0)
        /// </summary>
        public Action<TICouncilorState, float> OnSetAutofailValue { get; set; }

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
        /// Formatted for screen reader clarity - stats first, sentence-style separation.
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
                sb.Append($"Unknown Agent. ");
                sb.Append($"Faction: {councilor.faction?.displayName ?? "Unknown"}. ");
                sb.Append($"Location: {councilor.location?.displayName ?? "Unknown"}. ");
                sb.Append("Intel level: Location only. Use Investigate Councilor to learn more.");
                return sb.ToString();
            }

            // Name and type first
            sb.Append($"{councilor.displayName}");
            if (!isOwn)
            {
                sb.Append($", {councilor.faction?.displayName ?? "Unknown"} faction");
            }
            sb.Append($", {councilor.typeTemplate?.displayName ?? "Unknown"}. ");

            // STATS FIRST - the most important info for gameplay decisions
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                sb.Append($"Persuasion {councilor.GetAttribute(CouncilorAttribute.Persuasion)}. ");
                sb.Append($"Investigation {councilor.GetAttribute(CouncilorAttribute.Investigation)}. ");
                sb.Append($"Espionage {councilor.GetAttribute(CouncilorAttribute.Espionage)}. ");
                sb.Append($"Command {councilor.GetAttribute(CouncilorAttribute.Command)}. ");
                sb.Append($"Administration {councilor.GetAttribute(CouncilorAttribute.Administration)}. ");
                sb.Append($"Science {councilor.GetAttribute(CouncilorAttribute.Science)}. ");
                sb.Append($"Security {councilor.GetAttribute(CouncilorAttribute.Security)}. ");

                // Loyalty - require secrets intel (1.0) for true loyalty
                if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorSecrets(councilor)))
                {
                    sb.Append($"Loyalty {councilor.GetAttribute(CouncilorAttribute.Loyalty)}. ");
                }
                else
                {
                    sb.Append($"Loyalty {councilor.GetAttribute(CouncilorAttribute.ApparentLoyalty)} apparent. ");
                }
            }

            // Current mission
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorMission(councilor)))
            {
                if (councilor.activeMission != null)
                {
                    sb.Append($"Mission: {councilor.activeMission.missionTemplate.displayName}");
                    if (councilor.activeMission.target != null)
                    {
                        sb.Append($" on {councilor.activeMission.target.displayName}");
                    }
                    sb.Append(". ");
                }
                else
                {
                    sb.Append("No mission assigned. ");
                }
            }

            // Location
            sb.Append($"Location: {GetLocationString(councilor, viewer)}. ");

            // Traits - listed inline
            if (isOwn || (viewer != null && viewer.HasIntelOnCouncilorDetails(councilor)))
            {
                if (councilor.traits != null && councilor.traits.Count > 0)
                {
                    var traitNames = councilor.traits.Select(t => t.displayName).ToList();
                    sb.Append($"Traits: {string.Join(", ", traitNames)}. ");
                }
            }
            else if (viewer != null && viewer.HasIntelOnCouncilorBasicData(councilor))
            {
                var visibleTraits = councilor.traits?.Where(t => t.easilyVisible).ToList();
                if (visibleTraits != null && visibleTraits.Count > 0)
                {
                    var traitNames = visibleTraits.Select(t => t.displayName).ToList();
                    sb.Append($"Visible traits: {string.Join(", ", traitNames)}. ");
                }
            }

            // Biographical info last (less important for gameplay)
            sb.Append($"Age {councilor.age}, {GetGenderString(councilor.gender)}, from {GetHometownString(councilor)}.");

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
                // Status section (if detained or turned)
                var statusSection = BuildStatusSection(councilor, viewer);
                if (statusSection != null && statusSection.ItemCount > 0)
                {
                    sections.Add(statusSection);
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

                // Spend XP section
                var xpSection = BuildSpendXPSection(councilor);
                if (xpSection.ItemCount > 0)
                {
                    sections.Add(xpSection);
                }

                // Councilor Actions section (Dismiss, Abort Mission)
                var actionsSection = BuildActionsSection(councilor, viewer);
                if (actionsSection != null && actionsSection.ItemCount > 0)
                {
                    sections.Add(actionsSection);
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

        private DataSection BuildStatusSection(TICouncilorState councilor, TIFactionState viewer)
        {
            var statusSection = new DataSection("Status");
            var activePlayer = GameControl.control?.activePlayer;

            try
            {
                // Check for detained status
                if (councilor.detained)
                {
                    string detainInfo;
                    if (councilor.detainingFaction == councilor.faction)
                    {
                        // Self-detained (protective custody)
                        detainInfo = $"In protective custody until {councilor.detainedReleaseDate?.ToCustomDateString() ?? "unknown"}";
                    }
                    else
                    {
                        detainInfo = $"Detained by {councilor.detainingFaction?.displayName ?? "unknown"} until {councilor.detainedReleaseDate?.ToCustomDateString() ?? "unknown"}";
                    }
                    statusSection.AddItem("Detained", detainInfo);
                }

                // Check for turned status
                if (councilor.turned)
                {
                    if (councilor.agentForFaction == activePlayer)
                    {
                        // This is an enemy councilor we've turned - show as "Turned (ours)"
                        statusSection.AddItem("Turned", $"Working for us against {councilor.faction?.displayName ?? "their faction"}");

                        // Show and allow setting the autofail slider
                        float currentAutofail = councilor.autofailMissionsValue;
                        string autofailPercent = $"{(currentAutofail * 100):F0}%";
                        statusSection.AddItem("Mission Failure Rate", autofailPercent,
                            "Controls how often this turned councilor fails their missions. Higher values make them fail more often but may arouse suspicion.",
                            onActivate: () =>
                            {
                                // Cycle through common values: 0%, 25%, 50%, 75%, 100%
                                float[] options = { 0f, 0.25f, 0.5f, 0.75f, 1.0f };
                                int currentIndex = 0;
                                for (int i = 0; i < options.Length; i++)
                                {
                                    if (Math.Abs(options[i] - currentAutofail) < 0.01f)
                                    {
                                        currentIndex = i;
                                        break;
                                    }
                                }
                                int nextIndex = (currentIndex + 1) % options.Length;
                                OnSetAutofailValue?.Invoke(councilor, options[nextIndex]);
                            });
                    }
                    else if (councilor.faction == activePlayer)
                    {
                        // Our councilor has been turned by someone else - show as "Traitor"
                        statusSection.AddItem("Traitor", $"Working for {councilor.agentForFaction?.displayName ?? "unknown faction"}");
                    }
                }

                // Show who is tracking this councilor
                if (councilor.knowsIveBeenSeenBy != null && councilor.knowsIveBeenSeenBy.Count > 0)
                {
                    var trackers = councilor.knowsIveBeenSeenBy.Select(f => f.displayName).ToList();
                    statusSection.AddItem("Being Tracked By", string.Join(", ", trackers),
                        "These factions have used Investigate Councilor on this agent and we detected them");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building status section: {ex.Message}");
            }

            return statusSection.ItemCount > 0 ? statusSection : null;
        }

        private DataSection BuildActionsSection(TICouncilorState councilor, TIFactionState viewer)
        {
            var actionsSection = new DataSection("Councilor Actions");
            var activePlayer = GameControl.control?.activePlayer;

            try
            {
                // Abort Mission - only if councilor has a mission and we're not in mission phase
                if (councilor.HasMission && !TIMissionPhaseState.InMissionPhase())
                {
                    var councilorCopy = councilor;
                    string missionName = councilor.activeMission?.missionTemplate?.displayName ?? "mission";
                    actionsSection.AddItem($"Abort Mission: {missionName}",
                        "Cancel the currently assigned mission",
                        onActivate: () =>
                        {
                            OnAbortMission?.Invoke(councilorCopy);
                        });
                }

                // Dismiss Councilor - check if allowed
                // Can dismiss if:
                // 1. Not in mission phase and no save-blocking prompts
                // 2. Either this is our councilor OR this is a turned enemy councilor (agentForFaction == us)
                bool canDismiss = !GameStateManager.AllFactions().Any(f => f.planningMissions) &&
                                  !TIPromptQueueState.ActivePlayerHasSaveBlockingPrompt() &&
                                  !councilor.detained;

                if (canDismiss)
                {
                    // Check if this is a turned enemy councilor we control
                    bool isTurnedEnemy = councilor.agentForFaction == activePlayer && councilor.faction != activePlayer;

                    if (isTurnedEnemy)
                    {
                        // Dismiss a turned enemy councilor
                        var councilorCopy = councilor;
                        actionsSection.AddItem("Dismiss Turned Councilor",
                            $"Release {councilor.displayName} back to {councilor.faction?.displayName ?? "their faction"}",
                            onActivate: () =>
                            {
                                OnDismissCouncilor?.Invoke(councilorCopy, true); // keepOrgs doesn't matter for enemy
                            });
                    }
                    else if (councilor.faction == activePlayer)
                    {
                        // Dismiss our own councilor
                        int orgCount = councilor.orgs?.Count ?? 0;
                        var councilorCopy = councilor;

                        if (orgCount > 0)
                        {
                            // Has orgs - offer keep or sell options
                            string orgSaleValue = GetOrgsSaleValueString(councilor);

                            actionsSection.AddItem("Dismiss (Keep Orgs)",
                                $"Fire {councilor.displayName} and move {orgCount} org(s) to faction pool",
                                onActivate: () =>
                                {
                                    OnDismissCouncilor?.Invoke(councilorCopy, true);
                                });

                            actionsSection.AddItem("Dismiss (Sell Orgs)",
                                $"Fire {councilor.displayName} and sell {orgCount} org(s) for {orgSaleValue}",
                                onActivate: () =>
                                {
                                    OnDismissCouncilor?.Invoke(councilorCopy, false);
                                });
                        }
                        else
                        {
                            // No orgs - simple dismiss
                            actionsSection.AddItem("Dismiss Councilor",
                                $"Fire {councilor.displayName} from the council",
                                onActivate: () =>
                                {
                                    OnDismissCouncilor?.Invoke(councilorCopy, true);
                                });
                        }
                    }
                }
                else if (councilor.detained)
                {
                    actionsSection.AddItem("Cannot Dismiss", "Councilor is detained");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error building actions section: {ex.Message}");
            }

            return actionsSection.ItemCount > 0 ? actionsSection : null;
        }

        private string GetOrgsSaleValueString(TICouncilorState councilor)
        {
            try
            {
                var saleValue = councilor.AllOrgsSaleValue;
                return TISpeechMod.CleanText(saleValue.ToString("N0"));
            }
            catch
            {
                return "unknown value";
            }
        }
    }
}
