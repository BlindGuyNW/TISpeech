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
    /// Reader for alien threats and entities.
    /// Handles multiple entity types: councilors, fleets, habs, facilities, xenoforming, activities.
    /// </summary>
    public class AlienThreatReader
    {
        /// <summary>
        /// Alien threat category types.
        /// </summary>
        public enum AlienCategory
        {
            Councilors,
            Fleets,
            Habs,
            EarthAssets,
            Xenoforming,
            Events
        }

        /// <summary>
        /// Get display name for a category.
        /// </summary>
        public static string GetCategoryName(AlienCategory category)
        {
            switch (category)
            {
                case AlienCategory.Councilors: return "Alien Councilors";
                case AlienCategory.Fleets: return "Alien Fleets";
                case AlienCategory.Habs: return "Alien Habs";
                case AlienCategory.EarthAssets: return "Earth Assets";
                case AlienCategory.Xenoforming: return "Xenoforming";
                case AlienCategory.Events: return "Recent Events";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Read category summary with count.
        /// </summary>
        public string ReadCategorySummary(AlienCategory category, TIFactionState viewer)
        {
            int count = GetCategoryCount(category, viewer);
            string name = GetCategoryName(category);
            return $"{name}: {count} known";
        }

        /// <summary>
        /// Get count of items in a category.
        /// </summary>
        public int GetCategoryCount(AlienCategory category, TIFactionState viewer)
        {
            if (viewer == null) return 0;

            try
            {
                switch (category)
                {
                    case AlienCategory.Councilors:
                        return GetKnownAlienCouncilors(viewer).Count;
                    case AlienCategory.Fleets:
                        return GetKnownAlienFleets(viewer).Count;
                    case AlienCategory.Habs:
                        return GetKnownAlienHabs(viewer).Count;
                    case AlienCategory.EarthAssets:
                        return GetKnownEarthAssets(viewer).Count;
                    case AlienCategory.Xenoforming:
                        return GetKnownXenoforming(viewer).Count;
                    case AlienCategory.Events:
                        return 0; // Events are time-limited, show as drillable
                    default:
                        return 0;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting category count for {category}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get items for a category.
        /// </summary>
        public List<object> GetCategoryItems(AlienCategory category, TIFactionState viewer)
        {
            if (viewer == null) return new List<object>();

            try
            {
                switch (category)
                {
                    case AlienCategory.Councilors:
                        return GetKnownAlienCouncilors(viewer).Cast<object>().ToList();
                    case AlienCategory.Fleets:
                        return GetKnownAlienFleets(viewer).Cast<object>().ToList();
                    case AlienCategory.Habs:
                        return GetKnownAlienHabs(viewer).Cast<object>().ToList();
                    case AlienCategory.EarthAssets:
                        return GetKnownEarthAssets(viewer).Cast<object>().ToList();
                    case AlienCategory.Xenoforming:
                        return GetKnownXenoforming(viewer).Cast<object>().ToList();
                    case AlienCategory.Events:
                        return new List<object>(); // Events handled separately
                    default:
                        return new List<object>();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting category items for {category}: {ex.Message}");
                return new List<object>();
            }
        }

        #region Category Data Sources

        private List<TICouncilorState> GetKnownAlienCouncilors(TIFactionState viewer)
        {
            var result = new List<TICouncilorState>();
            try
            {
                var alienFaction = GameStateManager.AlienFaction();
                if (alienFaction?.councilors != null)
                {
                    foreach (var councilor in alienFaction.councilors)
                    {
                        if (viewer.HasIntelOnCouncilorBasicData(councilor))
                        {
                            result.Add(councilor);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting alien councilors: {ex.Message}");
            }
            return result;
        }

        private List<TISpaceFleetState> GetKnownAlienFleets(TIFactionState viewer)
        {
            var result = new List<TISpaceFleetState>();
            try
            {
                var alienFaction = GameStateManager.AlienFaction();
                if (alienFaction == null) return result;

                var knownFleets = viewer.KnownFleets;
                if (knownFleets != null)
                {
                    result.AddRange(knownFleets.Where(f => f.faction == alienFaction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting alien fleets: {ex.Message}");
            }
            return result;
        }

        private List<TIHabState> GetKnownAlienHabs(TIFactionState viewer)
        {
            var result = new List<TIHabState>();
            try
            {
                var alienFaction = GameStateManager.AlienFaction();
                if (alienFaction == null) return result;

                var knownHabs = viewer.KnownHabs;
                if (knownHabs != null)
                {
                    result.AddRange(knownHabs.Where(h => h.faction == alienFaction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting alien habs: {ex.Message}");
            }
            return result;
        }

        private List<TIRegionAlienEntityState> GetKnownEarthAssets(TIFactionState viewer)
        {
            var result = new List<TIRegionAlienEntityState>();
            try
            {
                // Get facilities
                var facilities = viewer.KnownAlienFacilities;
                if (facilities != null)
                {
                    result.AddRange(facilities.Cast<TIRegionAlienEntityState>());
                }

                // Get activities
                var activities = viewer.KnownAlienActivities;
                if (activities != null)
                {
                    result.AddRange(activities.Cast<TIRegionAlienEntityState>());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting earth assets: {ex.Message}");
            }
            return result;
        }

        private List<TIRegionXenoformingState> GetKnownXenoforming(TIFactionState viewer)
        {
            var result = new List<TIRegionXenoformingState>();
            try
            {
                var entities = viewer.KnownAlienEntities;
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        if (entity is TIRegionXenoformingState xeno && xeno.VisibleToFaction(viewer))
                        {
                            result.Add(xeno);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting xenoforming: {ex.Message}");
            }
            return result.OrderByDescending(x => x.xenoformingLevel).ToList();
        }

        #endregion

        #region Item Reading

        /// <summary>
        /// Read summary for any alien entity item.
        /// </summary>
        public string ReadItemSummary(object item, TIFactionState viewer)
        {
            if (item == null) return "Unknown";

            try
            {
                if (item is TICouncilorState councilor)
                    return ReadCouncilorSummary(councilor, viewer);
                if (item is TISpaceFleetState fleet)
                    return ReadFleetSummary(fleet, viewer);
                if (item is TIHabState hab)
                    return ReadHabSummary(hab, viewer);
                if (item is TIRegionXenoformingState xeno)
                    return ReadXenoformingSummary(xeno, viewer);
                if (item is TIRegionAlienFacilityState facility)
                    return ReadFacilitySummary(facility, viewer);
                if (item is TIRegionAlienActivityState activity)
                    return ReadActivitySummary(activity, viewer);
                if (item is TIRegionAlienEntityState entity)
                    return $"{entity.displayName} in {entity.region?.displayName ?? "Unknown"}";

                return item.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading item summary: {ex.Message}");
                return "Error reading item";
            }
        }

        /// <summary>
        /// Read detail for any alien entity item.
        /// </summary>
        public string ReadItemDetail(object item, TIFactionState viewer)
        {
            if (item == null) return "Unknown";

            try
            {
                if (item is TICouncilorState councilor)
                    return ReadCouncilorDetail(councilor, viewer);
                if (item is TISpaceFleetState fleet)
                    return ReadFleetDetail(fleet, viewer);
                if (item is TIHabState hab)
                    return ReadHabDetail(hab, viewer);
                if (item is TIRegionXenoformingState xeno)
                    return ReadXenoformingDetail(xeno, viewer);
                if (item is TIRegionAlienFacilityState facility)
                    return ReadFacilityDetail(facility, viewer);
                if (item is TIRegionAlienActivityState activity)
                    return ReadActivityDetail(activity, viewer);
                if (item is TIRegionAlienEntityState entity)
                    return $"{entity.displayName}\nLocation: {entity.region?.displayName ?? "Unknown"}";

                return item.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading item detail: {ex.Message}");
                return "Error reading item";
            }
        }

        private string ReadCouncilorSummary(TICouncilorState councilor, TIFactionState viewer)
        {
            var councilorView = new CouncilorView(councilor, viewer);
            string name = councilorView.displayNameCurrent;
            string location = councilorView.locationString(false);
            if (string.IsNullOrEmpty(location)) location = "Unknown location";
            return $"{name} at {location}";
        }

        private string ReadCouncilorDetail(TICouncilorState councilor, TIFactionState viewer)
        {
            var councilorView = new CouncilorView(councilor, viewer);
            var sb = new StringBuilder();
            sb.AppendLine($"Alien Councilor: {councilorView.displayNameCurrent}");
            sb.AppendLine($"Type: {councilorView.councilorJobStringCurrent}");

            string location = councilorView.locationString(true);
            if (!string.IsNullOrEmpty(location))
                sb.AppendLine($"Location: {location}");

            // Stats (intel-gated via CouncilorView)
            string persuasion = councilorView.GetAttributeString(CouncilorAttribute.Persuasion);
            if (persuasion != Loc.T("UI.CouncilorView.UnknownSymbol"))
            {
                sb.AppendLine();
                sb.AppendLine("Stats:");
                sb.AppendLine($"  Persuasion: {councilorView.GetAttributeString(CouncilorAttribute.Persuasion)}");
                sb.AppendLine($"  Investigation: {councilorView.GetAttributeString(CouncilorAttribute.Investigation)}");
                sb.AppendLine($"  Espionage: {councilorView.GetAttributeString(CouncilorAttribute.Espionage)}");
                sb.AppendLine($"  Command: {councilorView.GetAttributeString(CouncilorAttribute.Command)}");
                sb.AppendLine($"  Security: {councilorView.GetAttributeString(CouncilorAttribute.Security)}");
            }

            // Mission (intel-gated via CouncilorView)
            string mission = councilorView.GetCurrentMissionString(includeTarget: true);
            if (!string.IsNullOrEmpty(mission) && mission != Loc.T("UI.CouncilorView.UnknownMission"))
            {
                sb.AppendLine();
                sb.AppendLine($"Current Mission: {mission}");
            }

            return sb.ToString();
        }

        private string ReadFleetSummary(TISpaceFleetState fleet, TIFactionState viewer)
        {
            string location = fleet.GetLocationDescription(viewer, false, true) ?? "Unknown";
            int shipCount = fleet.ships?.Count ?? 0;
            return $"{fleet.displayName}, {shipCount} ship{(shipCount != 1 ? "s" : "")}, {location}";
        }

        private string ReadFleetDetail(TISpaceFleetState fleet, TIFactionState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Alien Fleet: {fleet.displayName}");
            sb.AppendLine($"Location: {fleet.GetLocationDescription(viewer, false, true) ?? "Unknown"}");
            sb.AppendLine($"Ships: {fleet.ships?.Count ?? 0}");

            if (viewer.HasIntelOnFleetShipDetails(fleet) && fleet.ships != null)
            {
                sb.AppendLine();
                sb.AppendLine("Ship Classes:");
                var shipGroups = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown");
                foreach (var group in shipGroups)
                {
                    sb.AppendLine($"  {group.Key}: {group.Count()}");
                }
            }

            return sb.ToString();
        }

        private string ReadHabSummary(TIHabState hab, TIFactionState viewer)
        {
            string location = hab.location?.displayName ?? "Unknown";
            return $"{hab.displayName} at {location}";
        }

        private string ReadHabDetail(TIHabState hab, TIFactionState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Alien Hab: {hab.displayName}");
            sb.AppendLine($"Location: {hab.location?.displayName ?? "Unknown"}");
            sb.AppendLine($"Type: {hab.template?.displayName ?? "Unknown"}");
            return sb.ToString();
        }

        private string ReadXenoformingSummary(TIRegionXenoformingState xeno, TIFactionState viewer)
        {
            string region = xeno.region?.displayName ?? "Unknown";
            string nation = xeno.region?.nation?.displayName ?? "";
            int level = (int)xeno.xenoformingLevel;
            string severity = xeno.severityDescription ?? "Unknown";
            return $"{region}{(string.IsNullOrEmpty(nation) ? "" : $", {nation}")}: {severity} ({level}%)";
        }

        private string ReadXenoformingDetail(TIRegionXenoformingState xeno, TIFactionState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Xenoforming in {xeno.region?.displayName ?? "Unknown"}");
            sb.AppendLine($"Nation: {xeno.region?.nation?.displayName ?? "Unknown"}");
            sb.AppendLine($"Level: {xeno.xenoformingLevel:F1}%");
            sb.AppendLine($"Severity: {xeno.severityDescription ?? "Unknown"}");

            // Add severity explanation
            if (xeno.xenoformingLevel < 30f)
            {
                sb.AppendLine("Status: Light contamination, minimal threat");
            }
            else if (xeno.xenoformingLevel < 75f)
            {
                sb.AppendLine("Status: Heavy contamination, growing threat");
            }
            else
            {
                sb.AppendLine("Status: Severe contamination, megafauna may spawn");
            }

            return sb.ToString();
        }

        private string ReadFacilitySummary(TIRegionAlienFacilityState facility, TIFactionState viewer)
        {
            string region = facility.region?.displayName ?? "Unknown";
            string nation = facility.region?.nation?.displayName ?? "";
            int hp = (int)facility.currentHP;
            return $"Alien Facility in {region}{(string.IsNullOrEmpty(nation) ? "" : $", {nation}")}, HP: {hp}/80";
        }

        private string ReadFacilityDetail(TIRegionAlienFacilityState facility, TIFactionState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Alien Facility");
            sb.AppendLine($"Region: {facility.region?.displayName ?? "Unknown"}");
            sb.AppendLine($"Nation: {facility.region?.nation?.displayName ?? "Unknown"}");
            sb.AppendLine($"HP: {facility.currentHP:F0}/80");
            sb.AppendLine($"Status: {(facility.built ? "Active" : "Destroyed")}");
            sb.AppendLine();
            sb.AppendLine("This underground base supports alien operations in the region.");
            sb.AppendLine("Can be destroyed by councilor Assault mission or orbital bombardment.");
            return sb.ToString();
        }

        private string ReadActivitySummary(TIRegionAlienActivityState activity, TIFactionState viewer)
        {
            string region = activity.region?.displayName ?? "Unknown";
            string nation = activity.region?.nation?.displayName ?? "";

            // Get detected missions
            var missions = activity.GetMissionList(viewer);
            string missionText = missions?.Count > 0 ? string.Join(", ", missions) : "Unknown activity";

            return $"{missionText} in {region}{(string.IsNullOrEmpty(nation) ? "" : $", {nation}")}";
        }

        private string ReadActivityDetail(TIRegionAlienActivityState activity, TIFactionState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Alien Activity");
            sb.AppendLine($"Region: {activity.region?.displayName ?? "Unknown"}");
            sb.AppendLine($"Nation: {activity.region?.nation?.displayName ?? "Unknown"}");

            var missions = activity.GetMissionList(viewer);
            if (missions != null && missions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Detected Activities:");
                foreach (var mission in missions)
                {
                    sb.AppendLine($"  - {mission}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Sections for Items

        /// <summary>
        /// Get sections for an item.
        /// </summary>
        public List<ISection> GetSectionsForItem(object item, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            if (item == null || viewer == null)
                return sections;

            try
            {
                if (item is TICouncilorState councilor)
                    return GetCouncilorSections(councilor, viewer);
                if (item is TISpaceFleetState fleet)
                    return GetFleetSections(fleet, viewer);
                if (item is TIHabState hab)
                    return GetHabSections(hab, viewer);
                if (item is TIRegionXenoformingState xeno)
                    return GetXenoformingSections(xeno, viewer);
                if (item is TIRegionAlienFacilityState facility)
                    return GetFacilitySections(facility, viewer);
                if (item is TIRegionAlienActivityState activity)
                    return GetActivitySections(activity, viewer);

                // Default section for unknown types
                var infoSection = new DataSection("Info");
                infoSection.AddItem("Type", item.GetType().Name);
                sections.Add(infoSection);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting sections for item: {ex.Message}");
            }

            return sections;
        }

        private List<ISection> GetCouncilorSections(TICouncilorState councilor, TIFactionState viewer)
        {
            var sections = new List<ISection>();
            var councilorView = new CouncilorView(councilor, viewer);

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", councilorView.displayNameCurrent);
            infoSection.AddItem("Type", councilorView.councilorJobStringCurrent);
            string location = councilorView.locationString(false);
            if (!string.IsNullOrEmpty(location))
                infoSection.AddItem("Location", location);
            sections.Add(infoSection);

            // Stats - use CouncilorView for intel gating
            string persuasion = councilorView.GetAttributeString(CouncilorAttribute.Persuasion);
            if (persuasion != Loc.T("UI.CouncilorView.UnknownSymbol"))
            {
                var statsSection = new DataSection("Stats");
                statsSection.AddItem("Persuasion", councilorView.GetAttributeString(CouncilorAttribute.Persuasion));
                statsSection.AddItem("Investigation", councilorView.GetAttributeString(CouncilorAttribute.Investigation));
                statsSection.AddItem("Espionage", councilorView.GetAttributeString(CouncilorAttribute.Espionage));
                statsSection.AddItem("Command", councilorView.GetAttributeString(CouncilorAttribute.Command));
                statsSection.AddItem("Administration", councilorView.GetAttributeString(CouncilorAttribute.Administration));
                statsSection.AddItem("Science", councilorView.GetAttributeString(CouncilorAttribute.Science));
                statsSection.AddItem("Security", councilorView.GetAttributeString(CouncilorAttribute.Security));
                sections.Add(statsSection);
            }

            return sections;
        }

        private List<ISection> GetFleetSections(TISpaceFleetState fleet, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", fleet.displayName);
            infoSection.AddItem("Location", fleet.GetLocationDescription(viewer, false, true) ?? "Unknown");
            infoSection.AddItem("Ships", (fleet.ships?.Count ?? 0).ToString());
            sections.Add(infoSection);

            if (viewer.HasIntelOnFleetShipDetails(fleet) && fleet.ships != null && fleet.ships.Count > 0)
            {
                var shipsSection = new DataSection("Ships");
                var shipGroups = fleet.ships.GroupBy(s => s.template?.displayName ?? "Unknown");
                foreach (var group in shipGroups)
                {
                    shipsSection.AddItem(group.Key, group.Count().ToString());
                }
                sections.Add(shipsSection);
            }

            return sections;
        }

        private List<ISection> GetHabSections(TIHabState hab, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Name", hab.displayName);
            infoSection.AddItem("Location", hab.location?.displayName ?? "Unknown");
            infoSection.AddItem("Type", hab.template?.displayName ?? "Unknown");
            sections.Add(infoSection);

            return sections;
        }

        private List<ISection> GetXenoformingSections(TIRegionXenoformingState xeno, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Region", xeno.region?.displayName ?? "Unknown");
            infoSection.AddItem("Nation", xeno.region?.nation?.displayName ?? "Unknown");
            infoSection.AddItem("Level", $"{xeno.xenoformingLevel:F1}%");
            infoSection.AddItem("Severity", xeno.severityDescription ?? "Unknown");
            sections.Add(infoSection);

            var threatSection = new DataSection("Threat Assessment");
            if (xeno.xenoformingLevel >= 100f)
            {
                threatSection.AddItem("Status", "Critical - Megafauna spawning imminent");
            }
            else if (xeno.xenoformingLevel >= 75f)
            {
                threatSection.AddItem("Status", "Severe - Rapid expansion, megafauna likely");
            }
            else if (xeno.xenoformingLevel >= 30f)
            {
                threatSection.AddItem("Status", "Heavy - Growing contamination");
            }
            else
            {
                threatSection.AddItem("Status", "Light - Early stage contamination");
            }
            sections.Add(threatSection);

            return sections;
        }

        private List<ISection> GetFacilitySections(TIRegionAlienFacilityState facility, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Region", facility.region?.displayName ?? "Unknown");
            infoSection.AddItem("Nation", facility.region?.nation?.displayName ?? "Unknown");
            sections.Add(infoSection);

            var statusSection = new DataSection("Status");
            statusSection.AddItem("HP", $"{facility.currentHP:F0}/80");
            statusSection.AddItem("Active", facility.built ? "Yes" : "No");
            sections.Add(statusSection);

            return sections;
        }

        private List<ISection> GetActivitySections(TIRegionAlienActivityState activity, TIFactionState viewer)
        {
            var sections = new List<ISection>();

            var infoSection = new DataSection("Info");
            infoSection.AddItem("Region", activity.region?.displayName ?? "Unknown");
            infoSection.AddItem("Nation", activity.region?.nation?.displayName ?? "Unknown");
            sections.Add(infoSection);

            var missionSection = new DataSection("Detected Activities");
            var missions = activity.GetMissionList(viewer);
            if (missions != null && missions.Count > 0)
            {
                foreach (var mission in missions)
                {
                    missionSection.AddItem(mission);
                }
            }
            else
            {
                missionSection.AddItem("No activities detected");
            }
            sections.Add(missionSection);

            return sections;
        }

        #endregion
    }
}
