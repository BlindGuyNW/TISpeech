using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Readers;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Screens
{
    /// <summary>
    /// Ship Classes screen - browse ship designs.
    /// Shows player's ship designs with sections for detailed info and actions.
    /// </summary>
    public class ShipClassesScreen : ScreenBase
    {
        private List<TISpaceShipTemplate> items = new List<TISpaceShipTemplate>();
        private readonly ShipClassReader shipClassReader = new ShipClassReader();

        // View mode: false = active designs only, true = all designs including obsolete
        private bool showObsolete = false;

        // Special marker for "Design New Ship" virtual item (index 0 when can design)
        private bool canDesignShips = false;
        private bool hasRefreshed = false;

        // Cached sections
        private int cachedItemIndex = -1;
        private List<ISection> cachedSections = new List<ISection>();

        /// <summary>
        /// Callback for entering selection mode.
        /// </summary>
        public Action<string, List<SelectionOption>, Action<int>> OnEnterSelectionMode { get; set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback for entering ship designer mode.
        /// Parameter is the existing design to edit (null for new design).
        /// </summary>
        public Action<TISpaceShipTemplate> OnEnterShipDesignerMode { get; set; }

        public override string Name => "Ship Classes";

        public override string Description
        {
            get
            {
                var faction = GameControl.control?.activePlayer;
                if (faction != null)
                {
                    if (!ShipClassReader.CanDesignShips(faction))
                    {
                        return "Research hull technologies to design ships";
                    }

                    var allDesigns = ShipClassReader.GetPlayerDesigns(faction);
                    var activeDesigns = ShipClassReader.GetActiveDesigns(faction);
                    int obsoleteCount = allDesigns.Count - activeDesigns.Count;

                    if (showObsolete)
                    {
                        return $"All designs: {activeDesigns.Count} active, {obsoleteCount} obsolete";
                    }
                    return $"{activeDesigns.Count} design{(activeDesigns.Count != 1 ? "s" : "")}";
                }
                return "Browse your ship designs";
            }
        }

        public override bool SupportsViewModeToggle => true;
        public override bool SupportsLetterNavigation => true;

        public override IReadOnlyList<object> GetItems()
        {
            // Ensure we have fresh data on first access
            if (!hasRefreshed)
            {
                Refresh();
            }

            var result = new List<object>();

            // Always add virtual "Design New Ship" item - it will show status if unavailable
            result.Add("__DESIGN_NEW_SHIP__"); // Marker for virtual item

            result.AddRange(items.Cast<object>());
            return result;
        }

        public override void Refresh()
        {
            items.Clear();
            cachedItemIndex = -1;
            cachedSections.Clear();
            canDesignShips = false;
            hasRefreshed = true;

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                    return;

                // Check if player can design ships (has hull tech)
                canDesignShips = ShipClassReader.CanDesignShips(faction);

                if (showObsolete)
                {
                    items.AddRange(ShipClassReader.GetPlayerDesigns(faction));
                }
                else
                {
                    items.AddRange(ShipClassReader.GetActiveDesigns(faction));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error refreshing ship classes: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the actual design index, accounting for the virtual "Design New Ship" item.
        /// Returns -1 if index points to the "Design New Ship" virtual item (index 0).
        /// The virtual item is always present now.
        /// </summary>
        private int GetDesignIndex(int index)
        {
            // Index 0 is always "Design New Ship", real designs start at index 1
            return index - 1;
        }

        /// <summary>
        /// Get total item count including the virtual "Design New Ship" item.
        /// The virtual item is always present now.
        /// </summary>
        private int GetTotalItemCount()
        {
            return items.Count + 1; // Always includes the virtual "Design New Ship" item
        }

        public override string ToggleViewMode()
        {
            showObsolete = !showObsolete;
            Refresh();

            var faction = GameControl.control?.activePlayer;
            if (showObsolete)
            {
                var allDesigns = ShipClassReader.GetPlayerDesigns(faction);
                var activeDesigns = ShipClassReader.GetActiveDesigns(faction);
                int obsoleteCount = allDesigns.Count - activeDesigns.Count;
                return $"Showing all designs: {activeDesigns.Count} active, {obsoleteCount} obsolete";
            }
            else
            {
                return $"Showing active designs only: {items.Count} design{(items.Count != 1 ? "s" : "")}";
            }
        }

        public override string ReadItemSummary(int index)
        {
            // Handle virtual "Design New Ship" item (always at index 0)
            if (index == 0)
            {
                if (canDesignShips)
                {
                    return "Design New Ship - Create a new ship design";
                }
                else
                {
                    return "Design New Ship - [Locked] Requires hull technology";
                }
            }

            int designIndex = GetDesignIndex(index);
            if (designIndex < 0 || designIndex >= items.Count)
                return "Invalid ship class";

            var design = items[designIndex];

            // Mark obsolete designs
            var faction = GameControl.control?.activePlayer;
            try
            {
                if (faction != null && design.Obsolete(faction))
                {
                    return $"[Obsolete] {shipClassReader.ReadSummary(design)}";
                }
            }
            catch { }

            return shipClassReader.ReadSummary(design);
        }

        public override string ReadItemDetail(int index)
        {
            // Handle virtual "Design New Ship" item (always at index 0)
            if (index == 0)
            {
                var faction = GameControl.control?.activePlayer;
                if (canDesignShips)
                {
                    int hullCount = faction?.allowedShipHulls?.Count() ?? 0;
                    return $"Design New Ship\n\nCreate a new ship design from scratch.\nYou have {hullCount} hull types available.\n\nPress Enter to start designing.";
                }
                else
                {
                    return "Design New Ship\n\n[LOCKED]\n\nResearch a hull technology to unlock ship design.\n\nHull technologies are in the Space Military tech tree.";
                }
            }

            int designIndex = GetDesignIndex(index);
            if (designIndex < 0 || designIndex >= items.Count)
                return "Invalid ship class";

            var design = items[designIndex];
            var sb = new System.Text.StringBuilder();

            // Mark obsolete designs
            var faction2 = GameControl.control?.activePlayer;
            try
            {
                if (faction2 != null && design.Obsolete(faction2))
                {
                    sb.AppendLine("[OBSOLETE DESIGN]");
                    sb.AppendLine();
                }
            }
            catch { }

            sb.Append(shipClassReader.ReadDetail(design));
            return sb.ToString();
        }

        public override IReadOnlyList<ISection> GetSectionsForItem(int index)
        {
            // Handle virtual "Design New Ship" item (always at index 0)
            if (index == 0)
            {
                if (canDesignShips)
                {
                    var designSection = new DataSection("Actions");
                    designSection.AddItem("Start Designing", "Open the ship designer to create a new design manually",
                        onActivate: () => OnEnterShipDesignerMode?.Invoke(null));
                    designSection.AddItem("Quick Autodesign", "Have the AI design a ship automatically (select role first)",
                        onActivate: () => StartQuickAutodesign());
                    return new List<ISection> { designSection };
                }
                else
                {
                    // Show requirements when unavailable
                    var requirementsSection = new DataSection("Requirements");
                    requirementsSection.AddItem("Status", "Locked - Requires hull technology");
                    requirementsSection.AddItem("Location", "Space Military tech tree");
                    return new List<ISection> { requirementsSection };
                }
            }

            int designIndex = GetDesignIndex(index);
            if (designIndex < 0 || designIndex >= items.Count)
                return new List<ISection>();

            // Use cached sections if same item
            if (index == cachedItemIndex && cachedSections.Count > 0)
                return cachedSections;

            var design = items[designIndex];
            cachedItemIndex = index;

            // Wire up callbacks for the reader
            shipClassReader.OnEnterSelectionMode = OnEnterSelectionMode;
            shipClassReader.OnSpeak = OnSpeak;
            shipClassReader.OnEnterShipDesignerMode = OnEnterShipDesignerMode;
            shipClassReader.OnRefreshSections = () =>
            {
                // Invalidate cache and re-fetch sections
                cachedItemIndex = -1;
                cachedSections.Clear();
                // Also refresh the items list in case design was deleted
                Refresh();
            };

            cachedSections = shipClassReader.GetSections(design);

            return cachedSections;
        }

        public override string GetItemSortName(int index)
        {
            // Virtual "Design New Ship" item sorts first (empty string sorts before letters)
            if (index == 0)
                return "";

            int designIndex = GetDesignIndex(index);
            if (designIndex < 0 || designIndex >= items.Count)
                return "";
            return items[designIndex].className ?? "";
        }

        /// <summary>
        /// Start quick autodesign flow - select role then hull, then AI designs.
        /// </summary>
        private void StartQuickAutodesign()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
            {
                OnSpeak?.Invoke("No active faction", true);
                return;
            }

            // Build role options
            var roleOptions = new List<SelectionOption>();
            foreach (ShipRole role in Enum.GetValues(typeof(ShipRole)))
            {
                if (role == ShipRole.NoRole)
                    continue;

                roleOptions.Add(new SelectionOption
                {
                    Label = role.ToString().Replace("_", " "),
                    Data = role
                });
            }

            OnEnterSelectionMode?.Invoke("Select ship role", roleOptions, (roleIndex) =>
            {
                if (roleIndex < 0 || roleIndex >= roleOptions.Count)
                    return;

                var selectedRole = (ShipRole)roleOptions[roleIndex].Data;

                // Now select hull
                var hulls = faction.allowedShipHulls?.OrderBy(h => h.volume_m3).ToList();
                if (hulls == null || hulls.Count == 0)
                {
                    OnSpeak?.Invoke("No hulls available", true);
                    return;
                }

                var hullOptions = new List<SelectionOption>();
                foreach (var hull in hulls)
                {
                    string sizeStr = hull.smallHull ? "Small" : (hull.mediumHull ? "Medium" : (hull.largeHull ? "Large" : "Huge"));
                    hullOptions.Add(new SelectionOption
                    {
                        Label = hull.displayName,
                        DetailText = $"{sizeStr}, {hull.noseHardpoints} nose, {hull.hullHardpoints} hull hardpoints",
                        Data = hull
                    });
                }

                OnEnterSelectionMode?.Invoke("Select hull", hullOptions, (hullIndex) =>
                {
                    if (hullIndex < 0 || hullIndex >= hullOptions.Count)
                        return;

                    var selectedHull = (TIShipHullTemplate)hullOptions[hullIndex].Data;
                    ExecuteQuickAutodesign(faction, selectedRole, selectedHull);
                });
            });
        }

        private void ExecuteQuickAutodesign(TIFactionState faction, ShipRole role, TIShipHullTemplate hull)
        {
            try
            {
                bool allowExotics = faction.UnlockedExotics && faction.GetCurrentResourceAmount(FactionResource.Exotics) > 0f;
                bool allowAntimatter = faction.UnlockedAntimatter && faction.GetDailyIncome(FactionResource.Antimatter) > 0f;
                float strategicRange = faction.DesiredStrategicRange_AU();

                var outcome = faction.DesignShip(
                    playerAutodesign: true,
                    role: role,
                    design: out TISpaceShipTemplate autoDesign,
                    desiredStrategicRange_AU: strategicRange,
                    allowExotics: allowExotics,
                    allowAntimatter: allowAntimatter,
                    forceHull: hull
                );

                if (outcome == TIFactionState.ShipDesignerOutcome.Success && autoDesign != null)
                {
                    // Set a name and save
                    autoDesign.SetDisplayName($"Auto {hull.displayName} {role}");
                    faction.SaveShipDesign(autoDesign);
                    Refresh();

                    OnSpeak?.Invoke($"Created {autoDesign.className}. {autoDesign.wetMass_tons:N0} tons, {autoDesign.baseCruiseDeltaV_kps(false):F1} km/s delta-V, combat value {autoDesign.TemplateSpaceCombatValue():N0}", true);
                }
                else
                {
                    string reason = GetAutodesignFailureReason(outcome);
                    OnSpeak?.Invoke($"Autodesign failed: {reason}", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in quick autodesign: {ex.Message}");
                OnSpeak?.Invoke("Autodesign failed", true);
            }
        }

        private string GetAutodesignFailureReason(TIFactionState.ShipDesignerOutcome outcome)
        {
            switch (outcome)
            {
                case TIFactionState.ShipDesignerOutcome.NoAvailableHulls:
                    return "No hulls available";
                case TIFactionState.ShipDesignerOutcome.NoHullsForRole:
                    return "No suitable hull for this role";
                case TIFactionState.ShipDesignerOutcome.NoDrives:
                    return "No drives available";
                case TIFactionState.ShipDesignerOutcome.NoPowerPlants:
                    return "No power plants available";
                case TIFactionState.ShipDesignerOutcome.NoWeapons:
                    return "No weapons available";
                case TIFactionState.ShipDesignerOutcome.ForcedHullNotAvailable:
                    return "Selected hull not available";
                case TIFactionState.ShipDesignerOutcome.NoCandidateDesigns:
                    return "No viable designs found";
                case TIFactionState.ShipDesignerOutcome.MinimumPropulsionRequirementsNotMet:
                    return "Cannot meet propulsion requirements";
                case TIFactionState.ShipDesignerOutcome.NoScoredDesigns:
                    return "No designs scored well enough";
                case TIFactionState.ShipDesignerOutcome.AntimatterRequired:
                    return "Antimatter technology required";
                case TIFactionState.ShipDesignerOutcome.ExoticsRequired:
                    return "Exotic materials required";
                case TIFactionState.ShipDesignerOutcome.DesignNotAllowedForRole:
                    return "Design not allowed for this role";
                default:
                    return outcome.ToString();
            }
        }

        public override string GetActivationAnnouncement()
        {
            var faction = GameControl.control?.activePlayer;
            if (faction == null)
            {
                return "Ship Classes. No game loaded.";
            }

            // Always call Refresh to update canDesignShips and items
            Refresh();

            int totalItems = GetTotalItemCount();

            if (canDesignShips)
            {
                if (items.Count == 0)
                {
                    return $"Ship Classes. {totalItems} item. No ship designs yet.";
                }
                return $"Ship Classes. {totalItems} items: {items.Count} design{(items.Count != 1 ? "s" : "")} plus Design New Ship.";
            }
            else
            {
                return $"Ship Classes. {totalItems} item. Ship design locked - requires hull technology.";
            }
        }
    }
}
