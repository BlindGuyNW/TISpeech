using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using TISpeech.ReviewMode.Sections;

// For CostFormatter
using TISpeech.ReviewMode;

// Alias for the nested enum to avoid ambiguity
using TrackedValue = PavonisInteractive.TerraInvicta.NationInfoController.TrackedValue;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reader for nation state data.
    /// Provides summary, detail, and sectioned views of nation information.
    /// </summary>
    public class NationReader : IGameStateReader<TINationState>
    {
        /// <summary>
        /// Callback for setting control point priorities.
        /// </summary>
        public Action<TIControlPoint, PriorityType, int> OnSetPriority { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback when sections need to be refreshed after an action.
        /// </summary>
        public Action OnRefreshNeeded { get; set; }

        public string ReadSummary(TINationState nation)
        {
            if (nation == null)
                return "Unknown nation";

            var faction = GameControl.control?.activePlayer;
            int yourCPs = 0;
            int totalCPs = nation.numControlPoints;

            if (faction != null && nation.controlPoints != null)
            {
                yourCPs = nation.controlPoints.Count(cp => cp.faction == faction);
            }

            string cpInfo = yourCPs > 0 ? $"{yourCPs}/{totalCPs} CPs" : $"{totalCPs} CPs";
            string pop = FormatPopulation(nation.population);

            return $"{nation.displayName}, {cpInfo}, {pop} pop";
        }

        public string ReadDetail(TINationState nation)
        {
            if (nation == null)
                return "Unknown nation";

            var sb = new StringBuilder();
            sb.AppendLine($"Nation: {nation.displayName}");

            if (nation.federation != null)
            {
                sb.AppendLine($"Federation: {nation.federation.displayName}");
            }

            sb.AppendLine($"Population: {FormatPopulation(nation.population)}");
            sb.AppendLine($"GDP: ${FormatLargeNumber(nation.GDP)}");
            sb.AppendLine($"GDP per Capita: ${nation.perCapitaGDP:N0}");

            sb.AppendLine();
            sb.AppendLine("Space:");
            sb.AppendLine($"  Investment Points: {nation.BaseInvestmentPoints_month():F1} IP/month");
            sb.AppendLine($"  Boost: {nation.boostIncome_month_dekatons:F1} dt/month");
            sb.AppendLine($"  Space Funding: ${FormatLargeNumber(nation.spaceFunding_month)}/month");
            sb.AppendLine($"  Research: {nation.research_month:F1}/month");
            sb.AppendLine($"  Mission Control: {nation.missionControl:F1}");
            if (nation.spaceFlightProgram)
                sb.AppendLine("  Space Program: Active");
            if (nation.canBuildSpaceDefenses)
                sb.AppendLine("  Space Defenses: Available");

            sb.AppendLine();
            sb.AppendLine("Statistics:");
            sb.AppendLine($"  Democracy: {nation.democracy:F1}");
            sb.AppendLine($"  Cohesion: {nation.cohesion:F1}");
            sb.AppendLine($"  Education: {nation.education:F1}");
            sb.AppendLine($"  Inequality: {nation.inequality:F1}");
            sb.AppendLine($"  Unrest: {nation.unrest:F1}");
            sb.AppendLine($"  Sustainability: {TINationState.SustainabilityValueForDisplay(nation.sustainability)}");

            sb.AppendLine();
            sb.AppendLine("Military:");
            sb.AppendLine($"  Miltech: {nation.militaryTechLevel:F1}");
            sb.AppendLine($"  Armies: {nation.armies?.Count ?? 0}");
            sb.AppendLine($"  Navies: {nation.numNavies}");
            sb.AppendLine($"  STO Fighters: {nation.numSTOFighters}");
            sb.AppendLine($"  Nuclear Weapons: {nation.numNuclearWeapons}");
            if (nation.nuclearProgram)
                sb.AppendLine("  Nuclear Program: Active");

            return sb.ToString();
        }

        public List<ISection> GetSections(TINationState nation)
        {
            var sections = new List<ISection>();
            if (nation == null)
                return sections;

            var faction = GameControl.control?.activePlayer;

            // Overview section
            sections.Add(CreateOverviewSection(nation));

            // Public Opinion section - shows faction popularity
            sections.Add(CreatePublicOpinionSection(nation));

            // Your Control Points section (if you have any)
            if (faction != null)
            {
                var yourCPs = nation.controlPoints?.Where(cp => cp.faction == faction).ToList();
                if (yourCPs != null && yourCPs.Count > 0)
                {
                    sections.Add(CreateControlPointsSection(nation, yourCPs, faction));

                    // Control Point Actions section - abandon, auto-abandon toggle
                    sections.Add(CreateControlPointActionsSection(nation, yourCPs, faction));
                }
            }

            // All Control Points section (who controls what)
            sections.Add(CreateAllControlPointsSection(nation));

            // Military summary section
            sections.Add(CreateMilitarySummarySection(nation));

            // Armies section - detailed army info with operations
            if (nation.armies != null && nation.armies.Count > 0)
            {
                sections.Add(CreateArmiesSection(nation, faction));
            }

            // Nuclear Weapons section - if nation has nukes or program
            if (nation.numNuclearWeapons > 0 || nation.nuclearProgram)
            {
                sections.Add(CreateNuclearSection(nation, faction));
            }

            // Direct Investment section - if player has any presence
            if (faction != null && nation.MaxDirectInvestIPsRemainingThisYear() > 0)
            {
                sections.Add(CreateDirectInvestmentSection(nation, faction));
            }

            // Relations section (read-only summary)
            sections.Add(CreateRelationsSection(nation));

            // Manage Relations section - if player controls executive
            if (faction != null && nation.executiveFaction == faction)
            {
                sections.Add(CreateManageRelationsSection(nation, faction));
            }

            // Claims section - territorial claims and unification status
            sections.Add(CreateClaimsSection(nation));

            // Adjacency section - neighboring nations
            sections.Add(CreateAdjacencySection(nation));

            // Regions section
            if (nation.regions != null && nation.regions.Count > 0)
            {
                sections.Add(CreateRegionsSection(nation));
            }

            return sections;
        }

        #region Section Builders

        private ISection CreateOverviewSection(TINationState nation)
        {
            var section = new DataSection("Overview");

            // Population with tooltip
            string populationTooltip = GetCleanTooltip(() => NationInfoController.BuildPopulationTooltip(nation));
            section.AddItem("Population", FormatPopulation(nation.population), populationTooltip);

            // GDP with tooltip
            string gdpTooltip = GetCleanTooltip(() => NationInfoController.BuildGDPTooltip(nation));
            section.AddItem("GDP", $"${FormatLargeNumber(nation.GDP)}", gdpTooltip);

            // GDP per Capita with tooltip
            string perCapitaTooltip = GetCleanTooltip(() => NationInfoController.BuildPerCapitaGDPTooltip(nation));
            section.AddItem("GDP per Capita", $"${nation.perCapitaGDP:N0}", perCapitaTooltip);

            if (nation.federation != null)
            {
                string federationTooltip = GetCleanTooltip(() => NationInfoController.BuildSpecialRelationshipTooltip(nation));
                section.AddItem("Federation", nation.federation.displayName, federationTooltip);
            }

            section.AddItem("Capital", nation.capital?.displayName ?? "None");
            section.AddItem("Regions", nation.regions?.Count.ToString() ?? "0");

            // Investment Points with tooltip
            string ipTooltip = GetCleanTooltip(() => NationInfoController.BuildInvestmentTooltip(nation));
            float ipPerMonth = nation.BaseInvestmentPoints_month();
            section.AddItem("Investment Points", $"{ipPerMonth:F1} IP/month", ipTooltip);

            // Boost income
            float boostPerMonth = nation.boostIncome_month_dekatons;
            if (boostPerMonth > 0)
            {
                string boostTooltip = GetCleanTooltip(() => NationInfoController.BuildBoostTooltip(nation));
                section.AddItem("Boost", $"{boostPerMonth:F1} dt/month", boostTooltip);
            }

            // Space funding
            float fundingPerMonth = nation.spaceFunding_month;
            string fundingTooltip = GetCleanTooltip(() => NationInfoController.BuildSpaceFundingTooltip(nation));
            section.AddItem("Space Funding", $"${FormatLargeNumber(fundingPerMonth)}/month", fundingTooltip);

            // Research
            float researchPerMonth = nation.research_month;
            string researchTooltip = GetCleanTooltip(() => NationInfoController.BuildResearchTooltip(nation));
            section.AddItem("Research", $"{researchPerMonth:F1}/month", researchTooltip);

            // Mission Control
            float missionControl = nation.missionControl;
            string mcTooltip = GetCleanTooltip(() => NationInfoController.BuildMissionControlTooltip(nation));
            section.AddItem("Mission Control", $"{missionControl:F1}", mcTooltip);

            // Key stats with tooltips
            string democracyTooltip = GetCleanTooltip(() => NationInfoController.BuildDemocracyTooltip(nation));
            section.AddItem("Democracy", $"{nation.democracy:F1}", democracyTooltip);

            string cohesionTooltip = GetCleanTooltip(() => NationInfoController.BuildCohesionTooltip(nation));
            section.AddItem("Cohesion", $"{nation.cohesion:F1}", cohesionTooltip);

            string educationTooltip = GetCleanTooltip(() => NationInfoController.BuildEducationTooltip(nation));
            section.AddItem("Education", $"{nation.education:F1}", educationTooltip);

            string inequalityTooltip = GetCleanTooltip(() => NationInfoController.BuildInequalityTooltip(nation));
            section.AddItem("Inequality", $"{nation.inequality:F1}", inequalityTooltip);

            string unrestTooltip = GetCleanTooltip(() => NationInfoController.BuildUnrestTooltip(nation));
            section.AddItem("Unrest", $"{nation.unrest:F1}", unrestTooltip);

            string sustainabilityTooltip = GetCleanTooltip(() => NationInfoController.BuildSustainabilityTooltip(nation));
            // Sustainability uses inverse display (1/value), so use game's display method
            string sustainabilityDisplay = TINationState.SustainabilityValueForDisplay(nation.sustainability);
            section.AddItem("Sustainability", sustainabilityDisplay, sustainabilityTooltip);

            return section;
        }

        /// <summary>
        /// Safely get tooltip text and clean it for screen reader output.
        /// </summary>
        private string GetCleanTooltip(Func<string> tooltipBuilder)
        {
            try
            {
                string tooltip = tooltipBuilder();
                return TISpeechMod.CleanText(tooltip);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not build tooltip: {ex.Message}");
                return "";
            }
        }

        private ISection CreatePublicOpinionSection(TINationState nation)
        {
            var section = new DataSection("Public Opinion");
            var playerFaction = GameControl.control?.activePlayer;

            try
            {
                // Get all active human ideologies (the factions in the game)
                var activeIdeologies = GameStateManager.ActiveHumanIdeologies();
                if (activeIdeologies == null)
                {
                    section.AddItem("Public opinion data unavailable");
                    return section;
                }

                // Get the overall public opinion tooltip for detail text
                string overallTooltip = GetCleanTooltip(() => NationInfoController.BuildPublicOpinionTooltip(nation));

                // Sort by popularity (highest first)
                var sortedIdeologies = activeIdeologies
                    .Select(template => new {
                        Template = template,
                        Popularity = nation.GetPublicOpinionOfFaction(template.ideology)
                    })
                    .OrderByDescending(x => x.Popularity)
                    .ToList();

                foreach (var item in sortedIdeologies)
                {
                    var ideology = item.Template.ideology;
                    float popularity = item.Popularity;
                    string percentStr = $"{popularity * 100:F1}%";

                    string factionName;
                    string detailText;
                    bool isPlayerFaction = false;

                    if (ideology == FactionIdeology.Undecided)
                    {
                        // Undecided population
                        factionName = "Undecided";
                        detailText = "Population not aligned with any faction";
                    }
                    else
                    {
                        // Get the faction for this ideology
                        var faction = TIFactionIdeologyTemplate.GetFactionByIdeology(ideology);
                        if (faction != null)
                        {
                            factionName = faction.displayName;
                            isPlayerFaction = (faction == playerFaction);

                            // Build detail text
                            var sb = new StringBuilder();
                            sb.AppendLine($"{faction.displayName}: {percentStr} of population supports this faction");

                            // Get historical change if available
                            try
                            {
                                if (nation.historyPublicOpinion != null && nation.historyPublicOpinion.Count > 31)
                                {
                                    nation.historyPublicOpinion[31].TryGetValue(ideology, out float lastMonth);
                                    float change = popularity - lastMonth;
                                    if (Math.Abs(change) > 0.001f)
                                    {
                                        string changeDir = change > 0 ? "increased" : "decreased";
                                        sb.AppendLine($"Change from last month: {changeDir} by {Math.Abs(change) * 100:F1}%");
                                    }
                                    else
                                    {
                                        sb.AppendLine("No change from last month");
                                    }
                                }
                            }
                            catch { }

                            detailText = sb.ToString();
                        }
                        else
                        {
                            factionName = ideology.ToString();
                            detailText = $"{percentStr} support";
                        }
                    }

                    // Mark player's faction
                    string label = isPlayerFaction ? $"{factionName} (You)" : factionName;
                    section.AddItem(label, percentStr, detailText);
                }

                // Add summary info about public opinion mechanics
                if (!string.IsNullOrEmpty(overallTooltip))
                {
                    section.AddItem("About Public Opinion", "How public opinion works", overallTooltip);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building public opinion section: {ex.Message}");
                section.AddItem("Error loading public opinion data");
            }

            return section;
        }

        private ISection CreateControlPointsSection(TINationState nation, List<TIControlPoint> yourCPs, TIFactionState faction)
        {
            var section = new DataSection("Your Control Points");

            foreach (var cp in yourCPs)
            {
                string cpName = GetControlPointName(cp, nation);
                string priorities = GetPrioritySummary(cp);

                // Each control point is an item that can be drilled into for priority setting
                section.AddItem(cpName, priorities, GetControlPointDetail(cp, nation));
            }

            return section;
        }

        private ISection CreateControlPointActionsSection(TINationState nation, List<TIControlPoint> yourCPs, TIFactionState faction)
        {
            var section = new DataSection("Control Point Actions");

            // Get current status
            bool canDisable = nation.CanDisableControlPoints(faction);
            bool autoAbandonEnabled = faction.permaAbandonedNations?.Contains(nation) ?? false;
            int activeCount = yourCPs.Count(cp => !cp.benefitsDisabled);
            int disabledCount = yourCPs.Count(cp => cp.benefitsDisabled);

            // Get duration from game config
            int disableDuration = TemplateManager.global?.selfDisableControlPointDuration_months ?? 6;

            // Status summary
            string statusValue = $"{activeCount} active, {disabledCount} disabled";
            section.AddItem("Status", statusValue);

            // Abandon Control Points action
            if (canDisable)
            {
                string abandonLabel = "Abandon Control Points";
                string abandonValue = $"Disable for {disableDuration} months";
                string abandonDetail = $"Voluntarily disable your control points in {nation.displayName}. " +
                    $"This will disable all {activeCount} of your active control points for {disableDuration} months. " +
                    "Use this to withdraw from a nation without losing control points to enemy factions. " +
                    "During this time, you won't receive benefits from these control points.";

                section.AddItem(abandonLabel, abandonValue, abandonDetail,
                    onActivate: () => StartAbandonControlPoints(nation, faction, disableDuration));
            }
            else
            {
                string reason = disabledCount > 0
                    ? "All control points are already disabled"
                    : "No control points to disable";
                section.AddItem("Abandon Control Points", reason);
            }

            // Auto-Abandon Toggle
            string autoLabel = "Auto-Abandon";
            string autoValue = autoAbandonEnabled ? "Enabled" : "Disabled";
            string autoDetail = autoAbandonEnabled
                ? $"Auto-abandon is ENABLED for {nation.displayName}. " +
                  "Your control points will be automatically abandoned if an enemy faction takes the executive. " +
                  "This prevents you from losing control points to hostile takeovers."
                : $"Auto-abandon is DISABLED for {nation.displayName}. " +
                  "Your control points will remain active even if an enemy faction takes the executive. " +
                  "Enable this to automatically protect your control points from hostile takeovers.";

            section.AddItem(autoLabel, autoValue, autoDetail,
                onActivate: () => ToggleAutoAbandon(nation, faction, !autoAbandonEnabled));

            return section;
        }

        private void StartAbandonControlPoints(TINationState nation, TIFactionState faction, int durationMonths)
        {
            // Double confirmation for abandon action
            var confirmOptions = new List<SelectionOption>
            {
                new SelectionOption
                {
                    Label = "Confirm Abandon",
                    DetailText = $"Disable all control points in {nation.displayName} for {durationMonths} months",
                    Data = true
                },
                new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Keep control points active",
                    Data = false
                }
            };

            OnEnterSelectionMode?.Invoke(
                $"Abandon control points in {nation.displayName}?",
                confirmOptions,
                (index) =>
                {
                    if ((bool)confirmOptions[index].Data)
                    {
                        ExecuteAbandonControlPoints(nation, faction, durationMonths);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Abandon cancelled", true);
                    }
                }
            );
        }

        private void ExecuteAbandonControlPoints(TINationState nation, TIFactionState faction, int durationMonths)
        {
            try
            {
                var action = new SelfDisableControlPoints(faction, nation);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Control points in {nation.displayName} abandoned for {durationMonths} months", true);
                MelonLogger.Msg($"Abandoned control points in {nation.displayName}");

                // Refresh sections to show updated status
                OnRefreshNeeded?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error abandoning control points: {ex.Message}");
                OnSpeak?.Invoke("Error abandoning control points", true);
            }
        }

        private void ToggleAutoAbandon(TINationState nation, TIFactionState faction, bool newValue)
        {
            try
            {
                var action = new SetNationAutoAbandon(faction, nation, newValue);
                faction.playerControl.StartAction(action);

                string status = newValue ? "enabled" : "disabled";
                OnSpeak?.Invoke($"Auto-abandon {status} for {nation.displayName}", true);
                MelonLogger.Msg($"Auto-abandon {status} for {nation.displayName}");

                // Refresh sections to show updated status
                OnRefreshNeeded?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error toggling auto-abandon: {ex.Message}");
                OnSpeak?.Invoke("Error toggling auto-abandon", true);
            }
        }

        #region Direct Investment

        private ISection CreateDirectInvestmentSection(TINationState nation, TIFactionState faction)
        {
            var section = new DataSection("Direct Investment");

            try
            {
                int remainingThisYear = nation.MaxDirectInvestIPsRemainingThisYear();
                int maxAnnual = nation.MaxAnnualDirectInvestIPs;
                float usedThisYear = nation.directInvestmentedIPsThisYear;

                // Summary of remaining capacity
                section.AddItem("Annual Limit", $"{remainingThisYear} of {maxAnnual} IPs remaining this year",
                    $"You can directly invest up to {maxAnnual} Investment Points per year in {nation.displayName}. " +
                    $"You have used {usedThisYear:F0} IPs so far this year.");

                // Check for free influence period
                if (nation.SkipDirectInvestInfluenceCost(faction))
                {
                    section.AddItem("Regime Change Bonus", "Reduced influence costs active",
                        "You recently took control of the executive. Influence costs for direct investment are reduced by 50%.");
                }

                // List available priorities for direct investment
                foreach (PriorityType priority in Enum.GetValues(typeof(PriorityType)))
                {
                    if (priority == PriorityType.None || priority == PriorityType.Spoils)
                        continue;

                    if (!TINationState.EverAllowedForDirectInvest(priority))
                        continue;

                    if (!nation.ValidPriority(priority))
                        continue;

                    if (nation.CanDirectInvest(faction, priority, out int maxAllowed))
                    {
                        // Get cost per IP
                        var costPerIP = nation.InvestmentPointDirectPurchasePrice(priority, faction);
                        string costStr = CostFormatter.FormatCostOnly(costPerIP, faction);

                        string priorityName = GetPriorityDisplayName(priority);
                        string label = priorityName;
                        string value = $"Cost: {costStr}/IP, max {maxAllowed}";

                        // Get current progress if applicable
                        float accumulated = nation.GetAccumulatedInvestmentPoints(priority);
                        float required = nation.GetRequiredInvestmentPointsForPriority(priority);
                        string progressStr = "";
                        if (required > 0)
                        {
                            float percent = (accumulated / required) * 100f;
                            progressStr = $"Progress: {percent:F0}% ({accumulated:F1}/{required:F0} IP). ";
                        }

                        string detail = $"{progressStr}Cost per Investment Point: {costStr}. " +
                            $"Maximum you can invest: {maxAllowed} IP.";

                        section.AddItem(label, value, detail,
                            onActivate: () => StartDirectInvestment(nation, faction, priority, costPerIP, maxAllowed));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building direct investment section: {ex.Message}");
                section.AddItem("Error loading direct investment data");
            }

            return section;
        }

        private void StartDirectInvestment(TINationState nation, TIFactionState faction, PriorityType priority, TIResourcesCost costPerIP, int maxAllowed)
        {
            string priorityName = GetPriorityDisplayName(priority);

            // Calculate how many IPs the player can afford
            int affordable = CalculateAffordableIPs(faction, costPerIP, maxAllowed);

            if (affordable <= 0)
            {
                OnSpeak?.Invoke("Cannot afford to invest in this priority", true);
                return;
            }

            // Create options for 1, 5, 10, max IPs
            var options = new List<SelectionOption>();

            int[] amounts = { 1, 5, 10, affordable };
            foreach (int amount in amounts.Distinct().Where(a => a <= affordable).OrderBy(a => a))
            {
                var totalCost = nation.SingleDirectInvestmentPrice(priority, amount, faction);
                string costStr = CostFormatter.FormatCostOnly(totalCost, faction);
                string label = amount == affordable ? $"{amount} IP (max)" : $"{amount} IP";

                options.Add(new SelectionOption
                {
                    Label = label,
                    DetailText = $"Invest {amount} IP for {costStr}",
                    Data = amount
                });
            }

            OnEnterSelectionMode?.Invoke(
                $"Invest in {priorityName}",
                options,
                (index) => ExecuteDirectInvestment(nation, faction, priority, (int)options[index].Data)
            );
        }

        private int CalculateAffordableIPs(TIFactionState faction, TIResourcesCost costPerIP, int maxAllowed)
        {
            // Find how many IPs we can afford
            for (int i = maxAllowed; i >= 1; i--)
            {
                var totalCost = new TIResourcesCost(costPerIP).MultiplyCost(i);
                if (totalCost.CanAfford(faction))
                    return i;
            }
            return 0;
        }

        private void ExecuteDirectInvestment(TINationState nation, TIFactionState faction, PriorityType priority, int amount)
        {
            try
            {
                var action = new DirectInvestAction(faction, nation, priority, amount);
                faction.playerControl.StartAction(action);

                string priorityName = GetPriorityDisplayName(priority);
                OnSpeak?.Invoke($"Invested {amount} IP in {priorityName}", true);
                MelonLogger.Msg($"Direct invested {amount} IP in {priorityName} for {nation.displayName}");

                // Refresh sections to show updated status
                OnRefreshNeeded?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing direct investment: {ex.Message}");
                OnSpeak?.Invoke("Error executing direct investment", true);
            }
        }

        #endregion

        #region Manage Relations

        private ISection CreateManageRelationsSection(TINationState nation, TIFactionState faction)
        {
            var section = new DataSection("Manage Relations");

            try
            {
                // Get the influence cost for relationship changes
                float influenceCost = TIFactionState.setPolicyMission?.cost?.value ?? 25f;

                section.AddItem("Relationship Cost", $"{influenceCost:F0} Influence per change",
                    "Changing a diplomatic relationship costs Influence. You can form alliances, create rivalries, or end existing relationships.");

                // Get all nations we could potentially have relationships with
                var allNations = GameStateManager.AllExtantNations()
                    ?.Where(n => n != nation)
                    .OrderBy(n => n.displayName)
                    .ToList() ?? new List<TINationState>();

                // Current allies - can end alliance
                var currentAllies = nation.allies?.Where(n => n != null).ToList() ?? new List<TINationState>();
                if (currentAllies.Count > 0)
                {
                    section.AddItem("--- Current Allies ---", $"{currentAllies.Count} allies");
                    foreach (var ally in currentAllies.OrderBy(n => n.displayName))
                    {
                        bool canEnd = nation.CanEndAlliance(ally);
                        string status = canEnd ? "can end alliance" : "cannot end alliance";
                        section.AddItem(ally.displayName, $"Ally, {status}",
                            $"Alliance with {ally.displayName}. " + (canEnd
                                ? $"Ending this alliance will cost {influenceCost:F0} Influence."
                                : "Cannot end this alliance at this time."),
                            onActivate: canEnd ? () => ConfirmRelationshipChange(nation, ally, RelationChange.AllyToNormal, faction, influenceCost) : (Action)null);
                    }
                }

                // Current rivals - can end rivalry
                var currentRivals = nation.rivals?.Where(n => n != null).ToList() ?? new List<TINationState>();
                if (currentRivals.Count > 0)
                {
                    section.AddItem("--- Current Rivals ---", $"{currentRivals.Count} rivals");
                    foreach (var rival in currentRivals.OrderBy(n => n.displayName))
                    {
                        bool canEnd = nation.CanEndRivalry(rival);
                        string status = canEnd ? "can end rivalry" : "cannot end rivalry";
                        section.AddItem(rival.displayName, $"Rival, {status}",
                            $"Rivalry with {rival.displayName}. " + (canEnd
                                ? $"Ending this rivalry will cost {influenceCost:F0} Influence."
                                : "Cannot end this rivalry at this time."),
                            onActivate: canEnd ? () => ConfirmRelationshipChange(nation, rival, RelationChange.RivalToNormal, faction, influenceCost) : (Action)null);
                    }
                }

                // Potential allies - neutral nations we can ally
                var potentialAllies = allNations
                    .Where(n => !currentAllies.Contains(n) && !currentRivals.Contains(n) && !(nation.wars?.Contains(n) ?? false))
                    .Where(n => nation.CanAlly(n))
                    .ToList();

                if (potentialAllies.Count > 0)
                {
                    section.AddItem("--- Form New Alliance ---", $"{potentialAllies.Count} options");
                    foreach (var potential in potentialAllies)
                    {
                        section.AddItem(potential.displayName, "can ally",
                            $"Form an alliance with {potential.displayName}. This will cost {influenceCost:F0} Influence.",
                            onActivate: () => ConfirmRelationshipChange(nation, potential, RelationChange.NormalToAlly, faction, influenceCost));
                    }
                }

                // Potential rivals - neutral nations we can rival
                var potentialRivals = allNations
                    .Where(n => !currentAllies.Contains(n) && !currentRivals.Contains(n) && !(nation.wars?.Contains(n) ?? false))
                    .Where(n => nation.CanRival(n))
                    .ToList();

                if (potentialRivals.Count > 0)
                {
                    section.AddItem("--- Declare New Rivalry ---", $"{potentialRivals.Count} options");
                    foreach (var potential in potentialRivals)
                    {
                        section.AddItem(potential.displayName, "can rival",
                            $"Declare {potential.displayName} as a rival. This will cost {influenceCost:F0} Influence.",
                            onActivate: () => ConfirmRelationshipChange(nation, potential, RelationChange.NormalToRival, faction, influenceCost));
                    }
                }

                // Current wars (read-only info)
                var currentWars = nation.wars?.Where(n => n != null).ToList() ?? new List<TINationState>();
                if (currentWars.Count > 0)
                {
                    section.AddItem("--- At War ---", $"{currentWars.Count} enemies (cannot change via relations)");
                    foreach (var enemy in currentWars.OrderBy(n => n.displayName))
                    {
                        section.AddItem(enemy.displayName, "at war",
                            $"Currently at war with {enemy.displayName}. Wars are resolved through military action or diplomatic missions, not the relations panel.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building manage relations section: {ex.Message}");
                section.AddItem("Error loading relations data");
            }

            return section;
        }

        private void ConfirmRelationshipChange(TINationState nation, TINationState targetNation, RelationChange change, TIFactionState faction, float influenceCost)
        {
            string actionDesc = GetRelationChangeDescription(change, targetNation);

            // Check if we can afford it using the game's cost definition
            if (!TINationState.FactionLevelRelationShipChangeCost.CanAfford(faction))
            {
                OnSpeak?.Invoke($"Cannot afford {influenceCost:F0} Influence for this change", true);
                return;
            }

            var confirmOptions = new List<SelectionOption>
            {
                new SelectionOption
                {
                    Label = "Confirm",
                    DetailText = $"{actionDesc} for {influenceCost:F0} Influence",
                    Data = true
                },
                new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Keep current relationship",
                    Data = false
                }
            };

            OnEnterSelectionMode?.Invoke(
                actionDesc + "?",
                confirmOptions,
                (index) =>
                {
                    if ((bool)confirmOptions[index].Data)
                    {
                        ExecuteRelationshipChange(nation, targetNation, change, faction, influenceCost);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Relationship change cancelled", true);
                    }
                }
            );
        }

        private string GetRelationChangeDescription(RelationChange change, TINationState target)
        {
            switch (change)
            {
                case RelationChange.NormalToAlly:
                    return $"Form alliance with {target.displayName}";
                case RelationChange.AllyToNormal:
                    return $"End alliance with {target.displayName}";
                case RelationChange.NormalToRival:
                    return $"Declare rivalry with {target.displayName}";
                case RelationChange.RivalToNormal:
                    return $"End rivalry with {target.displayName}";
                default:
                    return $"Change relationship with {target.displayName}";
            }
        }

        private void ExecuteRelationshipChange(TINationState nation, TINationState targetNation, RelationChange change, TIFactionState faction, float influenceCost)
        {
            try
            {
                // Execute the relationship change - HandleFactionLevelRelationshipChanges pays the cost internally
                nation.HandleFactionLevelRelationshipChanges(targetNation, change);

                string actionDesc = GetRelationChangeDescription(change, targetNation);
                OnSpeak?.Invoke($"{actionDesc} complete", true);
                MelonLogger.Msg($"Relationship change: {actionDesc} for {nation.displayName}");

                // Refresh sections to show updated status
                OnRefreshNeeded?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing relationship change: {ex.Message}");
                OnSpeak?.Invoke("Error changing relationship", true);
            }
        }

        #endregion

        private ISection CreateAllControlPointsSection(TINationState nation)
        {
            var section = new DataSection("Control Point Status");

            if (nation.controlPoints == null || nation.controlPoints.Count == 0)
            {
                section.AddItem("No control points");
                return section;
            }

            // Group by faction
            var byFaction = nation.controlPoints
                .GroupBy(cp => cp.faction?.displayName ?? "Uncontrolled")
                .OrderByDescending(g => g.Count());

            foreach (var group in byFaction)
            {
                string factionName = group.Key;
                int count = group.Count();
                int defendedCount = group.Count(cp => cp.defended);
                int crackdownCount = group.Count(cp => cp.benefitsDisabled);

                string details = $"{count} CP{(count > 1 ? "s" : "")}";
                if (defendedCount > 0)
                    details += $", {defendedCount} defended";
                if (crackdownCount > 0)
                    details += $", {crackdownCount} under crackdown";

                section.AddItem(factionName, details);
            }

            return section;
        }

        private ISection CreateMilitarySummarySection(TINationState nation)
        {
            var section = new DataSection("Military Summary");

            section.AddItem("Military Tech", $"{nation.militaryTechLevel:F1}");

            // Army count with status breakdown
            int totalArmies = nation.armies?.Count ?? 0;
            if (totalArmies > 0)
            {
                int inBattle = nation.armies.Count(a => a.InBattleWithArmies());
                int moving = nation.armies.Count(a => a.IsMoving && !a.InBattleWithArmies());
                string armyStatus = $"{totalArmies}";
                if (inBattle > 0) armyStatus += $" ({inBattle} in battle)";
                else if (moving > 0) armyStatus += $" ({moving} moving)";
                section.AddItem("Armies", armyStatus);
            }
            else
            {
                section.AddItem("Armies", "0");
            }

            section.AddItem("Navies", nation.numNavies.ToString());
            section.AddItem("STO Fighters", nation.numSTOFighters.ToString());

            // Nuclear summary
            if (nation.numNuclearWeapons > 0)
            {
                section.AddItem("Nuclear Weapons", nation.numNuclearWeapons.ToString());
            }
            else if (nation.nuclearProgram)
            {
                section.AddItem("Nuclear Program", "Active (building)");
            }
            else
            {
                section.AddItem("Nuclear Program", "None");
            }

            if (nation.spaceFlightProgram)
            {
                section.AddItem("Space Program", "Active");
            }

            if (nation.canBuildSpaceDefenses)
            {
                section.AddItem("Space Defenses", "Available");
            }

            return section;
        }

        private ISection CreateArmiesSection(TINationState nation, TIFactionState playerFaction)
        {
            var section = new DataSection("Armies");

            if (nation.armies == null || nation.armies.Count == 0)
            {
                section.AddItem("No armies");
                return section;
            }

            bool canControlArmies = nation.executiveFaction == playerFaction;

            foreach (var army in nation.armies)
            {
                // Build army summary
                int strengthPercent = (int)(army.strength * 100);
                string location = army.currentRegion?.displayName ?? "Unknown";
                string status = GetArmyStatusBrief(army);

                string label = army.displayName;
                string value = $"{strengthPercent}% in {location}";
                if (!string.IsNullOrEmpty(status))
                {
                    value += $", {status}";
                }

                // Build detailed info
                string detail = BuildArmyDetail(army);

                if (canControlArmies)
                {
                    // Add as activatable item that opens army operations
                    section.AddItem(label, value, detail,
                        onActivate: () => ShowArmyOperations(army, playerFaction));
                }
                else
                {
                    section.AddItem(label, value, detail);
                }
            }

            return section;
        }

        private string GetArmyStatusBrief(TIArmyState army)
        {
            if (army.InBattleWithArmies())
                return "in battle";
            if (army.IsMoving)
                return "moving";
            if (army.currentOperations?.Count > 0)
            {
                var op = army.currentOperations.FirstOrDefault();
                return op?.operation?.GetDisplayName()?.ToLower() ?? "operating";
            }
            if (army.huntingXenofauna)
                return "hunting";
            if (!army.InFriendlyRegion)
                return "hostile territory";
            if (army.CanHeal() && army.strength < 1.0f)
                return "healing";
            return "";
        }

        private string BuildArmyDetail(TIArmyState army)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Army: {army.displayName}");
            sb.AppendLine($"Strength: {army.strength * 100:F0}%");
            sb.AppendLine($"Tech Level: {army.techLevel:F1}");
            sb.AppendLine($"Deployment: {army.deploymentType}");
            sb.AppendLine($"Current Region: {army.currentRegion?.displayName ?? "Unknown"}");
            sb.AppendLine($"Home Region: {army.homeRegion?.displayName ?? "Unknown"}");

            if (army.destinationQueue != null && army.destinationQueue.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Moving to: " + string.Join(" -> ", army.destinationQueue.Select(d => d.displayName)));
            }

            if (army.currentOperations != null && army.currentOperations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Current operation:");
                foreach (var op in army.currentOperations)
                {
                    sb.AppendLine($"  {op.operation?.GetDisplayName() ?? "Unknown"} -> {op.target?.displayName ?? "Unknown"}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"In Friendly Region: {(army.InFriendlyRegion ? "Yes" : "No")}");
            sb.AppendLine($"Can Take Offensive Action: {(army.CanTakeOffensiveAction ? "Yes" : "No")}");

            return sb.ToString();
        }

        private void ShowArmyOperations(TIArmyState army, TIFactionState playerFaction)
        {
            try
            {
                var availableOps = army.AvailableOperationList();

                if (availableOps == null || availableOps.Count == 0)
                {
                    OnSpeak?.Invoke("No operations available for this army", true);
                    return;
                }

                var options = new List<SelectionOption>();
                foreach (var op in availableOps)
                {
                    string desc = GetOperationDescription(op, army);
                    options.Add(new SelectionOption
                    {
                        Label = op.GetDisplayName(),
                        DetailText = desc,
                        Data = op
                    });
                }

                OnEnterSelectionMode?.Invoke(
                    $"Operations for {army.displayName}",
                    options,
                    (index) => ExecuteArmyOperation(army, (IOperation)options[index].Data, playerFaction)
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error showing army operations: {ex.Message}");
                OnSpeak?.Invoke("Error loading operations", true);
            }
        }

        private string GetOperationDescription(IOperation op, TIArmyState army)
        {
            if (op is DeployArmyOperation)
                return "Move to a destination region";
            if (op is AnnexRegionOperation)
                return $"Annex current region (needs 80% strength, have {army.strength * 100:F0}%)";
            if (op is RazeRegionOperation)
                return $"Damage region economy (needs 50% strength, have {army.strength * 100:F0}%)";
            if (op is SetHuntXenoformingOperation)
                return "Enable hunting of alien megafauna";
            if (op is CancelHuntXenoformingOperation)
                return "Disable hunting of alien megafauna";
            if (op is CancelArmyOperation)
                return "Cancel current operation and movement";
            if (op is ArmyGoHomeOperation)
                return $"Return to home region ({army.homeRegion?.displayName ?? "Unknown"})";

            return op.GetDisplayName();
        }

        private void ExecuteArmyOperation(TIArmyState army, IOperation operation, TIFactionState faction)
        {
            try
            {
                // Check if operation needs target selection
                var possibleTargets = operation.GetPossibleTargets(army);

                if (possibleTargets != null && possibleTargets.Count > 0)
                {
                    // Need to select a target
                    var options = new List<SelectionOption>();
                    foreach (var target in possibleTargets)
                    {
                        string detail = "";
                        if (target is TIRegionState region)
                        {
                            detail = $"Region in {region.nation?.displayName ?? "Unknown"}";
                        }

                        options.Add(new SelectionOption
                        {
                            Label = target.displayName,
                            DetailText = detail,
                            Data = target
                        });
                    }

                    OnEnterSelectionMode?.Invoke(
                        $"Select target for {operation.GetDisplayName()}",
                        options,
                        (index) => ConfirmArmyOperation(army, operation, (TIGameState)options[index].Data, faction)
                    );
                }
                else
                {
                    // Instant operation (target is self)
                    ConfirmArmyOperation(army, operation, army, faction);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing army operation: {ex.Message}");
                OnSpeak?.Invoke("Error executing operation", true);
            }
        }

        private void ConfirmArmyOperation(TIArmyState army, IOperation operation, TIGameState target, TIFactionState faction)
        {
            try
            {
                var action = new ConfirmOperationAction(army, target, operation);
                faction.playerControl.StartAction(action);

                OnSpeak?.Invoke($"Started {operation.GetDisplayName()} on {target.displayName}", true);
                MelonLogger.Msg($"Executed army operation: {operation.GetDisplayName()} on {target.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error confirming army operation: {ex.Message}");
                OnSpeak?.Invoke("Error confirming operation", true);
            }
        }

        private ISection CreateNuclearSection(TINationState nation, TIFactionState playerFaction)
        {
            var section = new DataSection("Nuclear Weapons");

            // Nuclear arsenal count
            section.AddItem("Arsenal", $"{nation.numNuclearWeapons} nuclear weapons");

            // Nuclear program status
            if (nation.nuclearProgram)
            {
                section.AddItem("Nuclear Program", "Active");
            }
            else
            {
                section.AddItem("Nuclear Program", "Not established");
            }

            // Defensive nukes (allies)
            try
            {
                int defendingNukes = nation.NumNuclearWeaponsDefendingMe();
                if (defendingNukes > nation.numNuclearWeapons)
                {
                    section.AddItem("Allied Nukes", $"{defendingNukes - nation.numNuclearWeapons} from allies");
                }
            }
            catch { }

            // Threatening nukes (enemies)
            try
            {
                int threateningNukes = nation.NumNuclearWeaponsThreateningMeInWars();
                if (threateningNukes > 0)
                {
                    section.AddItem("Enemy Nukes", $"{threateningNukes} threatening");
                }
            }
            catch { }

            // Launch capability - only if player controls executive
            bool canLaunch = nation.executiveFaction == playerFaction && nation.numNuclearWeapons > 0;

            if (canLaunch)
            {
                try
                {
                    var validTargets = nation.NuclearWeaponsTargets();
                    if (validTargets != null && validTargets.Count > 0)
                    {
                        section.AddItem("Launch Nuclear Strike", $"{validTargets.Count} valid targets",
                            $"Launch a nuclear weapon against an enemy target. You have {nation.numNuclearWeapons} weapons available.",
                            onActivate: () => StartNuclearTargetSelection(nation, validTargets, playerFaction));
                    }
                    else
                    {
                        section.AddItem("Launch", "No valid targets available");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Error getting nuclear targets: {ex.Message}");
                    section.AddItem("Launch", "Error checking targets");
                }
            }
            else if (nation.numNuclearWeapons > 0)
            {
                section.AddItem("Launch", "Requires executive control");
            }

            return section;
        }

        private void StartNuclearTargetSelection(TINationState nation, List<TIRegionState> targets, TIFactionState faction)
        {
            var options = new List<SelectionOption>();

            foreach (var target in targets)
            {
                string nationName = target.nation?.displayName ?? "Unknown";
                string pop = FormatPopulation(target.population);

                options.Add(new SelectionOption
                {
                    Label = target.displayName,
                    DetailText = $"Region in {nationName}, population {pop}",
                    Data = target
                });
            }

            OnEnterSelectionMode?.Invoke(
                $"Select nuclear target ({nation.numNuclearWeapons} weapons available)",
                options,
                (index) => ConfirmNuclearStrike(nation, (TIRegionState)options[index].Data, faction)
            );
        }

        private void ConfirmNuclearStrike(TINationState nation, TIRegionState target, TIFactionState faction)
        {
            // Double confirmation for nuclear strike
            var confirmOptions = new List<SelectionOption>
            {
                new SelectionOption
                {
                    Label = "Confirm Launch",
                    DetailText = $"Launch nuclear strike on {target.displayName}. This cannot be undone.",
                    Data = true
                },
                new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Abort the nuclear strike",
                    Data = false
                }
            };

            OnEnterSelectionMode?.Invoke(
                $"Confirm nuclear strike on {target.displayName}?",
                confirmOptions,
                (index) =>
                {
                    if ((bool)confirmOptions[index].Data)
                    {
                        ExecuteNuclearStrike(nation, target, faction);
                    }
                    else
                    {
                        OnSpeak?.Invoke("Nuclear strike cancelled", true);
                    }
                }
            );
        }

        private void ExecuteNuclearStrike(TINationState nation, TIRegionState target, TIFactionState faction)
        {
            try
            {
                // Execute the nuclear strike via the region's method
                target.NuclearAttackOnRegion(faction, nation);

                OnSpeak?.Invoke($"Nuclear strike launched on {target.displayName}. Detonation in 30 minutes.", true);
                MelonLogger.Msg($"Nuclear strike launched by {nation.displayName} on {target.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error executing nuclear strike: {ex.Message}");
                OnSpeak?.Invoke("Error launching nuclear strike", true);
            }
        }

        private ISection CreateRelationsSection(TINationState nation)
        {
            var section = new DataSection("Relations");

            // Wars
            if (nation.wars != null && nation.wars.Count > 0)
            {
                foreach (var enemy in nation.wars)
                {
                    section.AddItem("At War", enemy.displayName);
                }
            }

            // Allies
            if (nation.allies != null && nation.allies.Count > 0)
            {
                foreach (var ally in nation.allies)
                {
                    section.AddItem("Ally", ally.displayName);
                }
            }

            // Rivals
            if (nation.rivals != null && nation.rivals.Count > 0)
            {
                foreach (var rival in nation.rivals)
                {
                    section.AddItem("Rival", rival.displayName);
                }
            }

            if ((nation.wars == null || nation.wars.Count == 0) &&
                (nation.allies == null || nation.allies.Count == 0) &&
                (nation.rivals == null || nation.rivals.Count == 0))
            {
                section.AddItem("No significant relations");
            }

            return section;
        }

        private ISection CreateClaimsSection(TINationState nation)
        {
            var section = new DataSection("Claims");

            try
            {
                // 1. Our outward claims - regions we claim but don't own
                var outwardClaims = nation.claims?
                    .Where(r => r.nation != nation && r.nation != null)
                    .OrderBy(r => r.nation?.displayName ?? "")
                    .ThenBy(r => r.displayName)
                    .ToList() ?? new List<TIRegionState>();

                if (outwardClaims.Count > 0)
                {
                    section.AddItem("--- Our Claims ---", $"{outwardClaims.Count} regions we claim but don't own");

                    // Group by nation for easier reading
                    var byNation = outwardClaims.GroupBy(r => r.nation).OrderBy(g => g.Key?.displayName ?? "");

                    foreach (var group in byNation)
                    {
                        var ownerNation = group.Key;
                        var regions = group.ToList();

                        foreach (var region in regions)
                        {
                            string label = region.displayName;
                            string value = $"owned by {ownerNation?.displayName ?? "Unknown"}";

                            // Flag if this is the owner's capital - critical for unification
                            bool isCapital = (region == ownerNation?.capital);
                            if (isCapital)
                            {
                                value += " (CAPITAL)";
                            }

                            string detail = BuildOutwardClaimDetail(region, ownerNation, nation, isCapital);
                            section.AddItem(label, value, detail);
                        }
                    }
                }
                else
                {
                    section.AddItem("No outward claims", "We don't claim any regions outside our borders");
                }

                // 2. Claims against us - nations that claim our regions
                var claimsAgainstUs = new List<(TINationState claimant, TIRegionState region, bool isHostile)>();

                if (nation.regions != null)
                {
                    foreach (var region in nation.regions)
                    {
                        var claimants = region.NationsWithClaim(
                            requireExtantNation: true,
                            requireExtantClaim: true,
                            includeCurrentOwner: false);

                        foreach (var claimant in claimants)
                        {
                            bool isHostile = claimant.hostileClaims?.Contains(region) ?? false;
                            claimsAgainstUs.Add((claimant, region, isHostile));
                        }
                    }
                }

                if (claimsAgainstUs.Count > 0)
                {
                    section.AddItem("--- Claims Against Us ---", $"{claimsAgainstUs.Count} claims on our territory");

                    // Group by claimant nation
                    var byClaimant = claimsAgainstUs
                        .GroupBy(c => c.claimant)
                        .OrderByDescending(g => g.Any(c => c.region == nation.capital)) // Capital claimants first
                        .ThenBy(g => g.Key?.displayName ?? "");

                    foreach (var group in byClaimant)
                    {
                        var claimant = group.Key;
                        var claims = group.ToList();
                        bool claimsOurCapital = claims.Any(c => c.region == nation.capital);

                        string label = claimant?.displayName ?? "Unknown";
                        string value = $"claims {claims.Count} region{(claims.Count > 1 ? "s" : "")}";

                        if (claimsOurCapital)
                        {
                            value += " (INCLUDING CAPITAL!)";
                        }

                        string detail = BuildClaimsAgainstUsDetail(claimant, claims, nation);
                        section.AddItem(label, value, detail);
                    }
                }
                else
                {
                    section.AddItem("No claims against us", "No other nations claim our territory");
                }

                // 3. Hostile claims we hold - regions causing cohesion/unrest penalties
                if (nation.hostileClaims != null && nation.hostileClaims.Count > 0)
                {
                    float cohesionImpact = nation.hostileClaimsImpactOnCohesion;
                    float unrestImpact = nation.hostileClaimsImpactOnUnrest;

                    string impactStr = "";
                    if (cohesionImpact != 0)
                        impactStr += $"cohesion {cohesionImpact:+0.0;-0.0}";
                    if (unrestImpact != 0)
                    {
                        if (!string.IsNullOrEmpty(impactStr)) impactStr += ", ";
                        impactStr += $"unrest {unrestImpact:+0.0;-0.0}";
                    }

                    section.AddItem("--- Hostile Claims We Hold ---",
                        $"{nation.hostileClaims.Count} regions, {impactStr}");

                    foreach (var region in nation.hostileClaims.OrderBy(r => r.displayName))
                    {
                        section.AddItem(region.displayName, "hostile claim",
                            "This region was taken by force and causes stability penalties until the population accepts our rule.");
                    }
                }

                // 4. Unification status
                AddUnificationStatus(section, nation);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building claims section: {ex.Message}");
                section.AddItem("Error loading claims data");
            }

            return section;
        }

        private string BuildOutwardClaimDetail(TIRegionState region, TINationState owner, TINationState viewer, bool isCapital)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Region: {region.displayName}");
            sb.AppendLine($"Current owner: {owner?.displayName ?? "Unknown"}");
            sb.AppendLine($"Population: {FormatPopulation(region.population)}");

            if (isCapital)
            {
                sb.AppendLine();
                sb.AppendLine("This is the owner's CAPITAL region.");
                sb.AppendLine("Claiming another nation's capital is required for unification.");

                // Check if we could unify with them
                if (owner != null && viewer.eligibleUnifications?.Contains(owner) == true)
                {
                    sb.AppendLine("UNIFICATION AVAILABLE: Same faction controls both nations!");
                }
                else if (owner != null && viewer.candidateUnifications?.Contains(owner) == true)
                {
                    sb.AppendLine("Unification possible if same faction gains control of both executives.");
                }
            }

            // Check if region is adjacent to us
            bool isAdjacent = region.AdjacentRegions(IAmAnInvadingArmy: false)?
                .Any(r => r.nation == viewer) ?? false;
            if (isAdjacent)
            {
                sb.AppendLine();
                sb.AppendLine("This region borders our territory.");
            }

            return sb.ToString();
        }

        private string BuildClaimsAgainstUsDetail(TINationState claimant, List<(TINationState claimant, TIRegionState region, bool isHostile)> claims, TINationState viewer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Nation: {claimant?.displayName ?? "Unknown"}");
            sb.AppendLine($"Claims {claims.Count} of our regions:");
            sb.AppendLine();

            foreach (var claim in claims.OrderByDescending(c => c.region == viewer.capital))
            {
                string regionInfo = claim.region.displayName;
                if (claim.region == viewer.capital)
                    regionInfo += " (OUR CAPITAL)";
                if (claim.isHostile)
                    regionInfo += " [hostile claim]";
                sb.AppendLine($"  - {regionInfo}");
            }

            // Warning about capital claims
            if (claims.Any(c => c.region == viewer.capital))
            {
                sb.AppendLine();
                sb.AppendLine("WARNING: This nation claims our capital!");
                sb.AppendLine("If they gain executive control of our nation, they could unify us into their territory.");
            }

            // Check relationship
            if (viewer.wars?.Contains(claimant) == true)
            {
                sb.AppendLine();
                sb.AppendLine("We are currently AT WAR with this nation.");
            }
            else if (viewer.rivals?.Contains(claimant) == true)
            {
                sb.AppendLine();
                sb.AppendLine("This nation is our RIVAL.");
            }
            else if (viewer.allies?.Contains(claimant) == true)
            {
                sb.AppendLine();
                sb.AppendLine("This nation is our ALLY (claims may be peaceful).");
            }

            return sb.ToString();
        }

        private void AddUnificationStatus(DataSection section, TINationState nation)
        {
            try
            {
                var eligible = nation.eligibleUnifications;
                var candidates = nation.candidateUnifications;

                if ((eligible == null || eligible.Count == 0) && (candidates == null || candidates.Count == 0))
                {
                    return; // No unification info to show
                }

                section.AddItem("--- Unification Status ---", "");

                // Eligible unifications - can do RIGHT NOW
                if (eligible != null && eligible.Count > 0)
                {
                    foreach (var target in eligible)
                    {
                        string label = $"Can unify with {target.displayName}";
                        string value = "READY NOW";

                        var sb = new StringBuilder();
                        sb.AppendLine($"Unification with {target.displayName} is available now.");
                        sb.AppendLine("Both nations have consolidated executive power under the same faction.");
                        sb.AppendLine();
                        sb.AppendLine($"Their population: {FormatPopulation(target.population)}");
                        sb.AppendLine($"Their GDP: ${FormatLargeNumber(target.GDP)}");
                        sb.AppendLine($"Their regions: {target.regions?.Count ?? 0}");
                        sb.AppendLine();
                        sb.AppendLine("Unification will merge their territory, resources, and military into this nation.");

                        section.AddItem(label, value, sb.ToString());
                    }
                }

                // Candidate unifications - possible but need same faction control
                if (candidates != null && candidates.Count > 0)
                {
                    var pendingCandidates = candidates.Where(c => eligible?.Contains(c) != true).ToList();

                    if (pendingCandidates.Count > 0)
                    {
                        foreach (var target in pendingCandidates)
                        {
                            string label = $"Potential: {target.displayName}";
                            string value = "needs same faction control";

                            var sb = new StringBuilder();
                            sb.AppendLine($"Unification with {target.displayName} is possible.");
                            sb.AppendLine("Requirements:");
                            sb.AppendLine("  - Same faction must control both executives");
                            sb.AppendLine("  - Both nations must have consolidated power");
                            sb.AppendLine();
                            sb.AppendLine($"Their population: {FormatPopulation(target.population)}");
                            sb.AppendLine($"Their executive: {target.executiveFaction?.displayName ?? "None"}");

                            section.AddItem(label, value, sb.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Could not add unification status: {ex.Message}");
            }
        }

        private ISection CreateAdjacencySection(TINationState nation)
        {
            var section = new DataSection("Neighbors");
            var faction = GameControl.control?.activePlayer;

            try
            {
                // Get all adjacent nations (including friendly crossing only)
                var adjacentNations = nation.AdjacentNations(IAmAnInvadingArmy: false);

                if (adjacentNations == null || adjacentNations.Count == 0)
                {
                    section.AddItem("No neighboring nations (island nation)");
                    return section;
                }

                // Sort by name for easier navigation
                var sortedNeighbors = adjacentNations.OrderBy(n => n.displayName).ToList();

                foreach (var neighbor in sortedNeighbors)
                {
                    string neighborName = neighbor.displayName;
                    var adjacencyType = nation.NationAdjacency(neighbor);

                    // Build detail string
                    string adjacencyDesc = GetAdjacencyDescription(adjacencyType);
                    string controlInfo = GetNationControlSummary(neighbor, faction);

                    string label = neighborName;
                    string value = adjacencyDesc;
                    if (!string.IsNullOrEmpty(controlInfo))
                    {
                        value += $", {controlInfo}";
                    }

                    // Detail includes more info about the neighbor
                    string detail = BuildNeighborDetail(neighbor, adjacencyType, faction);

                    section.AddItem(label, value, detail);
                }

                // Add summary at end
                int fullCount = adjacentNations.Count(n => nation.NationAdjacency(n) == TerrestrialAdjacencyType.FullAdjacency);
                int friendlyOnlyCount = adjacentNations.Count - fullCount;

                string summary = $"{adjacentNations.Count} neighbors total";
                if (friendlyOnlyCount > 0)
                {
                    summary += $" ({fullCount} land borders, {friendlyOnlyCount} sea/friendly only)";
                }
                section.AddItem("Total", summary);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error building adjacency section: {ex.Message}");
                section.AddItem("Error loading adjacency data");
            }

            return section;
        }

        private string GetAdjacencyDescription(TerrestrialAdjacencyType adjacencyType)
        {
            switch (adjacencyType)
            {
                case TerrestrialAdjacencyType.FullAdjacency:
                    return "land border";
                case TerrestrialAdjacencyType.FriendlyCrossingOnly:
                    return "sea/friendly crossing";
                default:
                    return "adjacent";
            }
        }

        private string GetNationControlSummary(TINationState nation, TIFactionState viewerFaction)
        {
            if (nation.controlPoints == null || nation.controlPoints.Count == 0)
                return "uncontrolled";

            // Check if viewer has any CPs
            int viewerCPs = viewerFaction != null
                ? nation.controlPoints.Count(cp => cp.faction == viewerFaction)
                : 0;

            if (viewerCPs > 0)
            {
                return $"you have {viewerCPs}/{nation.numControlPoints} CPs";
            }

            // Find top controller
            var topFaction = nation.controlPoints
                .Where(cp => cp.faction != null)
                .GroupBy(cp => cp.faction)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (topFaction == null)
                return "uncontrolled";

            int topCount = topFaction.Count();
            if (topCount == nation.numControlPoints)
                return $"controlled by {topFaction.Key.displayName}";
            else
                return $"{topFaction.Key.displayName} has {topCount}/{nation.numControlPoints}";
        }

        private string BuildNeighborDetail(TINationState neighbor, TerrestrialAdjacencyType adjacencyType, TIFactionState viewerFaction)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Nation: {neighbor.displayName}");
            sb.AppendLine($"Border type: {GetAdjacencyDescription(adjacencyType)}");

            if (adjacencyType == TerrestrialAdjacencyType.FullAdjacency)
            {
                sb.AppendLine("Armies can invade across this border");
            }
            else if (adjacencyType == TerrestrialAdjacencyType.FriendlyCrossingOnly)
            {
                sb.AppendLine("Armies can only cross if not invading (friendly passage)");
            }

            sb.AppendLine();
            sb.AppendLine($"Population: {FormatPopulation(neighbor.population)}");
            sb.AppendLine($"GDP: ${FormatLargeNumber(neighbor.GDP)}");
            sb.AppendLine($"Military Tech: {neighbor.militaryTechLevel:F1}");

            if (neighbor.armies != null && neighbor.armies.Count > 0)
            {
                sb.AppendLine($"Armies: {neighbor.armies.Count}");
            }

            if (neighbor.numNuclearWeapons > 0)
            {
                sb.AppendLine($"Nuclear Weapons: {neighbor.numNuclearWeapons}");
            }

            // Control info
            sb.AppendLine();
            string controlInfo = GetNationControlSummary(neighbor, viewerFaction);
            sb.AppendLine($"Control: {controlInfo}");

            return sb.ToString();
        }

        private ISection CreateRegionsSection(TINationState nation)
        {
            var section = new DataSection("Regions");

            foreach (var region in nation.regions)
            {
                string pop = FormatPopulation(region.population);

                // Get adjacent regions info
                var adjacentRegions = region.AdjacentRegions(IAmAnInvadingArmy: false);
                int foreignAdjacent = adjacentRegions?.Count(r => r.nation != nation) ?? 0;

                string value = pop;
                if (foreignAdjacent > 0)
                {
                    value += $", borders {foreignAdjacent} foreign region{(foreignAdjacent > 1 ? "s" : "")}";
                }

                // Build detail with adjacent region info
                string detail = BuildRegionDetail(region, nation);

                section.AddItem(region.displayName, value, detail);
            }

            return section;
        }

        private string BuildRegionDetail(TIRegionState region, TINationState nation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Region: {region.displayName}");
            sb.AppendLine($"Population: {FormatPopulation(region.population)}");

            if (region == nation.capital)
            {
                sb.AppendLine("This is the capital region");
            }

            // Adjacent regions
            var adjacentRegions = region.AdjacentRegions(IAmAnInvadingArmy: false);
            if (adjacentRegions != null && adjacentRegions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Adjacent regions:");

                // Group by nation
                var byNation = adjacentRegions
                    .GroupBy(r => r.nation)
                    .OrderBy(g => g.Key == nation ? 0 : 1) // Own nation first
                    .ThenBy(g => g.Key?.displayName ?? "");

                foreach (var group in byNation)
                {
                    string nationName = group.Key == nation ? "same nation" : group.Key?.displayName ?? "unknown";
                    var regionNames = group.Select(r => r.displayName);
                    sb.AppendLine($"  {nationName}: {string.Join(", ", regionNames)}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Control Point Helpers

        private string GetControlPointName(TIControlPoint cp, TINationState nation)
        {
            // Control points are identified by type and index
            int index = nation.controlPoints?.IndexOf(cp) ?? 0;
            string typeName = cp.controlPointType.ToString();
            string defended = cp.defended ? ", defended" : "";
            string crackdown = cp.benefitsDisabled ? ", crackdown" : "";
            return $"CP {index + 1} ({typeName}{defended}{crackdown})";
        }

        private string GetPrioritySummary(TIControlPoint cp)
        {
            var priorities = new List<string>();

            foreach (PriorityType priority in Enum.GetValues(typeof(PriorityType)))
            {
                if (priority == PriorityType.None)
                    continue;

                int value = cp.GetControlPointPriority(priority, checkValid: true);
                if (value > 0)
                {
                    priorities.Add($"{GetPriorityShortName(priority)}:{value}");
                }
            }

            return priorities.Count > 0 ? string.Join(", ", priorities) : "No priorities set";
        }

        private string GetControlPointDetail(TIControlPoint cp, TINationState nation)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Control Point in {nation.displayName}");
            sb.AppendLine($"Type: {cp.controlPointType}");

            if (cp.defended)
            {
                sb.AppendLine("Status: Defended");
            }
            if (cp.benefitsDisabled)
            {
                sb.AppendLine("Status: Benefits disabled (crackdown)");
            }

            sb.AppendLine();
            sb.AppendLine("Priorities:");

            foreach (PriorityType priority in Enum.GetValues(typeof(PriorityType)))
            {
                if (priority == PriorityType.None)
                    continue;

                int value = cp.GetControlPointPriority(priority, checkValid: false);
                bool valid = nation.ValidPriority(priority);

                if (value > 0 || valid)
                {
                    string status = valid ? "" : " (unavailable)";
                    sb.AppendLine($"  {GetPriorityDisplayName(priority)}: {value}{status}");
                }
            }

            return sb.ToString();
        }

        private string GetPriorityShortName(PriorityType priority)
        {
            switch (priority)
            {
                case PriorityType.Economy: return "Econ";
                case PriorityType.Welfare: return "Welf";
                case PriorityType.Environment: return "Env";
                case PriorityType.Knowledge: return "Know";
                case PriorityType.Government: return "Gov";
                case PriorityType.Unity: return "Unit";
                case PriorityType.Oppression: return "Oppr";
                case PriorityType.Funding: return "Fund";
                case PriorityType.Spoils: return "Spoil";
                case PriorityType.Military: return "Mil";
                case PriorityType.MissionControl: return "MC";
                case PriorityType.LaunchFacilities: return "Launch";
                default: return priority.ToString().Substring(0, Math.Min(4, priority.ToString().Length));
            }
        }

        private string GetPriorityDisplayName(PriorityType priority)
        {
            switch (priority)
            {
                case PriorityType.Economy: return "Economy";
                case PriorityType.Welfare: return "Welfare";
                case PriorityType.Environment: return "Environment";
                case PriorityType.Knowledge: return "Knowledge";
                case PriorityType.Government: return "Government";
                case PriorityType.Unity: return "Unity";
                case PriorityType.Oppression: return "Oppression";
                case PriorityType.Funding: return "Space Funding";
                case PriorityType.Spoils: return "Spoils";
                case PriorityType.Military: return "Military";
                case PriorityType.MissionControl: return "Mission Control";
                case PriorityType.LaunchFacilities: return "Launch Facilities";
                case PriorityType.Civilian_InitiateSpaceflightProgram: return "Initiate Space Program";
                case PriorityType.Military_FoundMilitary: return "Found Military";
                case PriorityType.Military_BuildArmy: return "Build Army";
                case PriorityType.Military_BuildNavy: return "Build Navy";
                case PriorityType.Military_InitiateNuclearProgram: return "Initiate Nuclear Program";
                case PriorityType.Military_BuildNuclearWeapons: return "Build Nuclear Weapons";
                case PriorityType.Military_BuildSpaceDefenses: return "Build Space Defenses";
                case PriorityType.Military_BuildSTOSquadron: return "Build STO Squadron";
                default: return priority.ToString();
            }
        }

        #endregion

        #region Formatting Helpers

        private string FormatPopulation(float population)
        {
            // population is in actual people, not millions
            if (population >= 1_000_000_000)
            {
                return $"{population / 1_000_000_000:F2}B";
            }
            if (population >= 1_000_000)
            {
                return $"{population / 1_000_000:F1}M";
            }
            if (population >= 1_000)
            {
                return $"{population / 1_000:F0}K";
            }
            return $"{population:F0}";
        }

        private string FormatLargeNumber(double value)
        {
            if (value >= 1_000_000_000_000)
            {
                return $"{value / 1_000_000_000_000:F1}T";
            }
            if (value >= 1_000_000_000)
            {
                return $"{value / 1_000_000_000:F1}B";
            }
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000:F1}M";
            }
            return $"{value:N0}";
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get nations where the player has control points.
        /// </summary>
        public static List<TINationState> GetPlayerNations(TIFactionState faction)
        {
            if (faction == null)
                return new List<TINationState>();

            try
            {
                var nations = new HashSet<TINationState>();

                // Get all control points for the faction
                if (faction.controlPoints != null)
                {
                    foreach (var cp in faction.controlPoints)
                    {
                        if (cp.nation != null)
                        {
                            nations.Add(cp.nation);
                        }
                    }
                }

                return nations.OrderByDescending(n =>
                    n.controlPoints?.Count(cp => cp.faction == faction) ?? 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player nations: {ex.Message}");
                return new List<TINationState>();
            }
        }

        /// <summary>
        /// Get all extant nations in the game (nations that currently exist with at least one region).
        /// </summary>
        public static List<TINationState> GetAllNations()
        {
            try
            {
                return GameStateManager.AllExtantNations()?.ToList() ?? new List<TINationState>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting all nations: {ex.Message}");
                return new List<TINationState>();
            }
        }

        #endregion
    }
}
