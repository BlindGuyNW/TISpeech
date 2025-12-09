using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Navigation level within the org target sub-mode.
    /// </summary>
    public enum OrgTargetLevel
    {
        OrgList,      // Browsing list of targetable orgs
        Sections,     // Viewing sections of a specific org
        SectionItems  // Viewing items within a section
    }

    /// <summary>
    /// Sub-mode for navigating org targets for missions like Hostile Takeover.
    /// Provides hierarchical navigation: Orgs -> Sections -> Section Items
    /// </summary>
    public class OrgTargetSubMode
    {
        private readonly TICouncilorState councilor;
        private readonly TIMissionTemplate mission;
        private readonly List<TIOrgState> orgs;
        private readonly MissionModifierReader modifierReader;
        private readonly OrgReader orgReader;

        // Navigation state
        private OrgTargetLevel currentLevel = OrgTargetLevel.OrgList;
        private int orgIndex = 0;
        private int sectionIndex = 0;
        private int sectionItemIndex = 0;

        // Cached sections for current org
        private List<ISection> cachedSections = new List<ISection>();
        private int cachedOrgIndex = -1;

        // Callbacks
        public Action<string, bool> OnSpeak { get; set; }
        public Action OnTargetSelected { get; set; }

        public int OrgCount => orgs.Count;
        public int CurrentOrgIndex => orgIndex;
        public TIOrgState CurrentOrg => orgs.Count > 0 && orgIndex >= 0 && orgIndex < orgs.Count
            ? orgs[orgIndex]
            : null;
        public OrgTargetLevel CurrentLevel => currentLevel;

        public OrgTargetSubMode(
            TICouncilorState councilor,
            TIMissionTemplate mission,
            IList<TIGameState> targets)
        {
            this.councilor = councilor;
            this.mission = mission;
            this.modifierReader = new MissionModifierReader();
            this.orgReader = new OrgReader();

            // Extract orgs from targets
            orgs = new List<TIOrgState>();
            foreach (var target in targets)
            {
                if (target.isOrgState)
                {
                    orgs.Add(target.ref_org);
                }
            }

            MelonLogger.Msg($"OrgTargetSubMode: Created with {orgs.Count} org targets for {mission.displayName}");
        }

        #region Entry Announcement

        public string GetEntryAnnouncement()
        {
            var sb = new StringBuilder();
            sb.Append($"{mission.displayName} target selection. ");
            sb.Append($"{orgs.Count} {(orgs.Count == 1 ? "organization" : "organizations")} available. ");

            if (orgs.Count > 0)
            {
                sb.Append(GetOrgSummary(0));
            }

            sb.Append(". Navigate with arrows, Enter to browse details, plus to assign target, Escape to cancel.");
            return sb.ToString();
        }

        #endregion

        #region Navigation

        public void Next()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    orgIndex = (orgIndex + 1) % orgs.Count;
                    break;

                case OrgTargetLevel.Sections:
                    var sections = GetCachedSections();
                    sectionIndex = (sectionIndex + 1) % sections.Count;
                    sectionItemIndex = 0;
                    break;

                case OrgTargetLevel.SectionItems:
                    var section = GetCurrentSection();
                    if (section != null)
                    {
                        sectionItemIndex = (sectionItemIndex + 1) % section.ItemCount;
                    }
                    break;
            }
        }

        public void Previous()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    orgIndex--;
                    if (orgIndex < 0) orgIndex = orgs.Count - 1;
                    break;

                case OrgTargetLevel.Sections:
                    var sections = GetCachedSections();
                    sectionIndex--;
                    if (sectionIndex < 0) sectionIndex = sections.Count - 1;
                    sectionItemIndex = 0;
                    break;

                case OrgTargetLevel.SectionItems:
                    var section = GetCurrentSection();
                    if (section != null)
                    {
                        sectionItemIndex--;
                        if (sectionItemIndex < 0) sectionItemIndex = section.ItemCount - 1;
                    }
                    break;
            }
        }

        public bool DrillDown()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    var sections = GetCachedSections();
                    if (sections.Count > 0)
                    {
                        currentLevel = OrgTargetLevel.Sections;
                        sectionIndex = 0;
                        sectionItemIndex = 0;
                        return true;
                    }
                    break;

                case OrgTargetLevel.Sections:
                    var section = GetCurrentSection();
                    if (section != null && section.ItemCount > 0)
                    {
                        // Check if item is activatable
                        if (section.CanActivate(sectionItemIndex))
                        {
                            section.Activate(sectionItemIndex);
                            return true;
                        }
                        else if (section.CanDrillIntoItem(sectionItemIndex))
                        {
                            currentLevel = OrgTargetLevel.SectionItems;
                            sectionItemIndex = 0;
                            return true;
                        }
                    }
                    break;

                case OrgTargetLevel.SectionItems:
                    // Try to activate current item
                    var currentSection = GetCurrentSection();
                    if (currentSection != null && currentSection.CanActivate(sectionItemIndex))
                    {
                        currentSection.Activate(sectionItemIndex);
                        return true;
                    }
                    break;
            }

            return false;
        }

        public bool BackOut()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.SectionItems:
                    currentLevel = OrgTargetLevel.Sections;
                    sectionItemIndex = 0;
                    return true;

                case OrgTargetLevel.Sections:
                    currentLevel = OrgTargetLevel.OrgList;
                    sectionIndex = 0;
                    sectionItemIndex = 0;
                    return true;

                case OrgTargetLevel.OrgList:
                    // At top level - return false to indicate cancel
                    return false;
            }

            return false;
        }

        #endregion

        #region Announcements

        public string GetCurrentAnnouncement()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    return GetOrgSummary(orgIndex);

                case OrgTargetLevel.Sections:
                    var section = GetCurrentSection();
                    if (section != null)
                    {
                        return $"{section.Name}, {sectionIndex + 1} of {GetCachedSections().Count}";
                    }
                    return "No sections";

                case OrgTargetLevel.SectionItems:
                    var sec = GetCurrentSection();
                    if (sec != null && sectionItemIndex < sec.ItemCount)
                    {
                        string item = sec.ReadItem(sectionItemIndex);
                        return $"{item}, {sectionItemIndex + 1} of {sec.ItemCount}";
                    }
                    return "No items";

                default:
                    return "Unknown state";
            }
        }

        public string GetDetailAnnouncement()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    return GetOrgDetail(orgIndex);

                case OrgTargetLevel.Sections:
                    // Read all items in section
                    var section = GetCurrentSection();
                    if (section != null)
                    {
                        var sb = new StringBuilder();
                        sb.Append($"{section.Name}: ");
                        for (int i = 0; i < section.ItemCount; i++)
                        {
                            if (i > 0) sb.Append(". ");
                            sb.Append(section.ReadItem(i));
                        }
                        return sb.ToString();
                    }
                    return "No section selected";

                case OrgTargetLevel.SectionItems:
                    var sec = GetCurrentSection();
                    if (sec != null && sectionItemIndex < sec.ItemCount)
                    {
                        return sec.ReadItem(sectionItemIndex);
                    }
                    return "No item selected";

                default:
                    return "Unknown state";
            }
        }

        public string GetListAnnouncement()
        {
            switch (currentLevel)
            {
                case OrgTargetLevel.OrgList:
                    var sb = new StringBuilder();
                    sb.Append($"{orgs.Count} organizations: ");
                    for (int i = 0; i < Math.Min(10, orgs.Count); i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(orgs[i].displayName);
                    }
                    if (orgs.Count > 10)
                    {
                        sb.Append($" and {orgs.Count - 10} more");
                    }
                    return sb.ToString();

                case OrgTargetLevel.Sections:
                    var sections = GetCachedSections();
                    return $"{sections.Count} sections: {string.Join(", ", sections.ConvertAll(s => s.Name))}";

                case OrgTargetLevel.SectionItems:
                    var section = GetCurrentSection();
                    if (section != null)
                    {
                        var itemSb = new StringBuilder();
                        itemSb.Append($"{section.ItemCount} items: ");
                        for (int i = 0; i < Math.Min(10, section.ItemCount); i++)
                        {
                            if (i > 0) itemSb.Append(", ");
                            itemSb.Append(section.ReadItem(i));
                        }
                        if (section.ItemCount > 10)
                        {
                            itemSb.Append($" and {section.ItemCount - 10} more");
                        }
                        return itemSb.ToString();
                    }
                    return "No section";

                default:
                    return "Unknown state";
            }
        }

        private string GetOrgSummary(int index)
        {
            if (index < 0 || index >= orgs.Count)
                return "Invalid org";

            var org = orgs[index];
            var breakdown = modifierReader.GetModifiers(mission, councilor, org, 0f);

            return $"{org.displayName}, Tier {org.tier}, {breakdown.SuccessChance}, {index + 1} of {orgs.Count}";
        }

        private string GetOrgDetail(int index)
        {
            if (index < 0 || index >= orgs.Count)
                return "Invalid org";

            var org = orgs[index];
            var sb = new StringBuilder();

            // Org description (bonuses, income, missions granted, eligibility)
            try
            {
                var faction = GameControl.control?.activePlayer;
                string description = org.description(includeDisplayName: true, faction, includeOwnership: true);
                sb.AppendLine(TISpeechMod.CleanText(description));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting org description: {ex.Message}");
                sb.AppendLine($"{org.displayName}, Tier {org.tier}");
            }

            // Mission success info
            sb.AppendLine();
            var breakdown = modifierReader.GetModifiers(mission, councilor, org, 0f);
            sb.Append(modifierReader.FormatForSpeech(breakdown, verbose: true));

            return sb.ToString();
        }

        #endregion

        #region Sections

        private List<ISection> GetCachedSections()
        {
            if (cachedOrgIndex != orgIndex)
            {
                cachedSections = BuildSectionsForOrg(CurrentOrg);
                cachedOrgIndex = orgIndex;
            }
            return cachedSections;
        }

        private List<ISection> BuildSectionsForOrg(TIOrgState org)
        {
            var sections = new List<ISection>();

            if (org == null)
                return sections;

            try
            {
                var faction = GameControl.control?.activePlayer;

                // Mission Info section - success chance and modifiers
                var missionSection = new DataSection("Mission Info");
                var breakdown = modifierReader.GetModifiers(mission, councilor, org, 0f);
                missionSection.AddItem("Success Chance", breakdown.SuccessChance);
                missionSection.AddItem("Total Bonus", $"+{breakdown.TotalBonus:N1}");
                missionSection.AddItem("Total Defense", $"+{breakdown.TotalPenalty:N1}");

                // Add individual bonuses
                foreach (var bonus in breakdown.Bonuses)
                {
                    missionSection.AddItem(bonus.Name, $"+{bonus.Value:N1}");
                }

                // Add individual penalties
                foreach (var penalty in breakdown.Penalties)
                {
                    missionSection.AddItem(penalty.Name, $"+{penalty.Value:N1}");
                }

                sections.Add(missionSection);

                // Description section - uses the game's built-in tooltip text
                var descSection = new DataSection("Description");
                try
                {
                    string fullDescription = org.description(includeDisplayName: false, faction, includeOwnership: true);
                    string cleanDescription = TISpeechMod.CleanText(fullDescription);

                    var lines = cleanDescription.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            descSection.AddItem(trimmed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error building org description: {ex.Message}");
                    descSection.AddItem("Description unavailable");
                }

                if (descSection.ItemCount > 0)
                {
                    sections.Add(descSection);
                }

                // Info section
                var infoSection = new DataSection("Info");
                infoSection.AddItem("Name", org.displayName);
                infoSection.AddItem("Tier", org.tier.ToString());

                if (org.template != null)
                {
                    infoSection.AddItem("Type", org.template.orgType.ToString());
                }

                if (org.homeRegion != null)
                {
                    infoSection.AddItem("Home Region", org.homeRegion.displayName);
                }

                if (org.assignedCouncilor != null)
                {
                    infoSection.AddItem("Assigned To", org.assignedCouncilor.displayName);
                    infoSection.AddItem("Faction", org.factionOrbit?.displayName ?? "Unknown");
                }
                else if (org.factionOrbit != null)
                {
                    infoSection.AddItem("In Pool Of", org.factionOrbit.displayName);
                }

                sections.Add(infoSection);

                // Eligibility section - can we actually use this org?
                var eligibilitySection = new DataSection("Eligibility");
                bool canUse = org.IsEligibleForFaction(faction);
                eligibilitySection.AddItem("Usable by Your Faction", canUse ? "Yes" : "No");

                if (!canUse)
                {
                    // The org description already includes the reason why it can't be used
                    // (ideology mismatch, can't transfer, etc.) - just note it's ineligible
                    eligibilitySection.AddItem("Reason", "See description for details");
                }

                // Check councilor eligibility
                if (canUse && faction?.councilors != null)
                {
                    int eligibleCount = 0;
                    foreach (var c in faction.councilors)
                    {
                        if (org.IsEligibleForCouncilor(c) && c.SufficientCapacityForOrg(org))
                        {
                            eligibleCount++;
                        }
                    }
                    eligibilitySection.AddItem("Eligible Councilors", $"{eligibleCount} of {faction.councilors.Count}");
                }

                sections.Add(eligibilitySection);

                // Actions section - assign as target
                var actionsSection = new DataSection("Actions");
                var orgCopy = org;
                actionsSection.AddItem("Assign as Target", $"Execute {mission.displayName}", onActivate: () =>
                {
                    AssignTarget(orgCopy);
                });
                sections.Add(actionsSection);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building sections for org: {ex.Message}");
            }

            return sections;
        }

        private ISection GetCurrentSection()
        {
            var sections = GetCachedSections();
            if (sectionIndex >= 0 && sectionIndex < sections.Count)
            {
                return sections[sectionIndex];
            }
            return null;
        }

        #endregion

        #region Actions

        public void AssignCurrentTarget()
        {
            if (CurrentOrg != null)
            {
                AssignTarget(CurrentOrg);
            }
        }

        private void AssignTarget(TIOrgState org)
        {
            try
            {
                MelonLogger.Msg($"Assigning {mission.displayName} target: {org.displayName}");

                // Execute the mission assignment using the game's action system
                var faction = councilor.faction;
                var action = new AssignCouncilorToMission(councilor, mission, org, 0f, false);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Assigned {councilor.displayName} to {mission.displayName} targeting {org.displayName}", true);
                OnTargetSelected?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error assigning target: {ex.Message}");
                OnSpeak?.Invoke($"Error assigning target: {ex.Message}", true);
            }
        }

        #endregion

        #region Letter Navigation

        public int FindNextOrgByLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);

            // Search from current + 1 to end
            for (int i = orgIndex + 1; i < orgs.Count; i++)
            {
                string name = orgs[i].displayName;
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            // Wrap around
            for (int i = 0; i <= orgIndex; i++)
            {
                string name = orgs[i].displayName;
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            return -1;
        }

        public void SetOrgIndex(int index)
        {
            if (index >= 0 && index < orgs.Count)
            {
                orgIndex = index;
                currentLevel = OrgTargetLevel.OrgList;
                cachedOrgIndex = -1; // Invalidate cache
            }
        }

        #endregion
    }
}
