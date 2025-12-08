using System;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using TISpeech.ReviewMode.Screens;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Helper for navigating to specific game states within Review Mode.
    /// </summary>
    public class NavigationHelper
    {
        private readonly NavigationState navigation;

        public NavigationHelper(NavigationState navigation)
        {
            this.navigation = navigation;
        }

        /// <summary>
        /// Navigate to a game state within Review Mode.
        /// Tries to switch to the appropriate screen and item.
        /// Falls back to announcing the target if navigation isn't supported.
        /// </summary>
        public void NavigateToGameState(TIGameState target)
        {
            if (target == null)
            {
                TISpeechMod.Speak("No navigation target", true);
                return;
            }

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null)
                {
                    AnnounceTargetFallback(target);
                    return;
                }

                // Try to find the appropriate screen and navigate to it
                if (target.isNationState)
                {
                    NavigateToNation(target.ref_nation, faction);
                }
                else if (target.isCouncilorState)
                {
                    NavigateToCouncilor(target.ref_councilor, faction);
                }
                else if (target.isSpaceFleetState)
                {
                    NavigateToFleet(target.ref_fleet, faction);
                }
                else if (target.isHabState)
                {
                    NavigateToHab(target.ref_hab, faction);
                }
                else if (target.isSpaceBodyState)
                {
                    NavigateToSpaceBody(target.ref_spaceBody, faction);
                }
                else if (target.isRegionState)
                {
                    // Regions: navigate to the nation that owns them
                    var region = target.ref_region;
                    if (region?.nation != null)
                    {
                        NavigateToNation(region.nation, faction);
                    }
                    else
                    {
                        AnnounceTargetFallback(target);
                    }
                }
                else
                {
                    // Unsupported type - announce the target
                    AnnounceTargetFallback(target);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error navigating to game state: {ex.Message}");
                AnnounceTargetFallback(target);
            }
        }

        private void NavigateToNation(TINationState nation, TIFactionState faction)
        {
            if (nation == null)
            {
                TISpeechMod.Speak("Invalid nation", true);
                return;
            }

            // Switch to Nations screen
            int screenIndex = navigation.FindScreenByName("Nations");
            if (screenIndex < 0)
            {
                // Try alternate name
                screenIndex = navigation.FindScreenByName("Nation");
            }

            if (screenIndex >= 0)
            {
                navigation.SwitchToScreen(screenIndex);

                // Try to find and select this nation
                var nationScreen = navigation.CurrentScreen as NationScreen;
                if (nationScreen != null)
                {
                    int itemIndex = FindItemIndex(nationScreen, n => n is TINationState ns && ns == nation);
                    if (itemIndex >= 0)
                    {
                        navigation.SetItemIndex(itemIndex);
                        TISpeechMod.Speak($"Navigated to {nation.displayName}. {navigation.GetCurrentAnnouncement()}", true);
                        return;
                    }
                }

                TISpeechMod.Speak($"Switched to Nations screen. {nation.displayName} may not be in your controlled nations.", true);
            }
            else
            {
                TISpeechMod.Speak($"Target: {nation.displayName}, Nation", true);
            }
        }

        private void NavigateToCouncilor(TICouncilorState councilor, TIFactionState faction)
        {
            if (councilor == null)
            {
                TISpeechMod.Speak("Invalid councilor", true);
                return;
            }

            // Switch to Council screen
            int screenIndex = navigation.FindScreenByName("Council");
            if (screenIndex < 0)
            {
                screenIndex = navigation.FindScreenByName("Enemy Councilors");
            }

            if (screenIndex >= 0)
            {
                navigation.SwitchToScreen(screenIndex);

                // If councilor is ours, they should be in the list
                // If enemy, may need to toggle to enemy view
                var council = navigation.CurrentScreen as CouncilScreen;
                if (council != null)
                {
                    // Check if councilor is ours
                    bool isOwn = councilor.faction == faction;
                    if (!isOwn && council.CurrentMode == CouncilScreen.ViewMode.MyCouncil)
                    {
                        // Switch to enemy councilors view
                        council.ToggleMode();
                    }

                    int itemIndex = FindItemIndex(council, c =>
                        c is TICouncilorState cs && cs == councilor);
                    if (itemIndex >= 0)
                    {
                        navigation.SetItemIndex(itemIndex);
                        TISpeechMod.Speak($"Navigated to {councilor.displayName}. {navigation.GetCurrentAnnouncement()}", true);
                        return;
                    }
                }

                TISpeechMod.Speak($"Switched to Council screen. {councilor.displayName}", true);
            }
            else
            {
                TISpeechMod.Speak($"Target: {councilor.displayName}, Councilor", true);
            }
        }

        private void NavigateToFleet(TISpaceFleetState fleet, TIFactionState faction)
        {
            if (fleet == null)
            {
                TISpeechMod.Speak("Invalid fleet", true);
                return;
            }

            // Switch to Fleets screen
            int screenIndex = navigation.FindScreenByName("Fleets");
            if (screenIndex >= 0)
            {
                navigation.SwitchToScreen(screenIndex);

                var fleets = navigation.CurrentScreen as FleetsScreen;
                if (fleets != null)
                {
                    int itemIndex = FindItemIndex(fleets, f =>
                        f is TISpaceFleetState fs && fs == fleet);
                    if (itemIndex >= 0)
                    {
                        navigation.SetItemIndex(itemIndex);
                        TISpeechMod.Speak($"Navigated to {fleet.displayName}. {navigation.GetCurrentAnnouncement()}", true);
                        return;
                    }
                }

                TISpeechMod.Speak($"Switched to Fleets screen. {fleet.displayName}", true);
            }
            else
            {
                TISpeechMod.Speak($"Target: {fleet.displayName}, Fleet", true);
            }
        }

        private void NavigateToHab(TIHabState hab, TIFactionState faction)
        {
            if (hab == null)
            {
                TISpeechMod.Speak("Invalid hab", true);
                return;
            }

            // Switch to Habs screen
            int screenIndex = navigation.FindScreenByName("Habs");
            if (screenIndex >= 0)
            {
                navigation.SwitchToScreen(screenIndex);

                var habs = navigation.CurrentScreen as HabsScreen;
                if (habs != null)
                {
                    int itemIndex = FindItemIndex(habs, h =>
                        h is TIHabState hs && hs == hab);
                    if (itemIndex >= 0)
                    {
                        navigation.SetItemIndex(itemIndex);
                        TISpeechMod.Speak($"Navigated to {hab.displayName}. {navigation.GetCurrentAnnouncement()}", true);
                        return;
                    }
                }

                TISpeechMod.Speak($"Switched to Habs screen. {hab.displayName}", true);
            }
            else
            {
                TISpeechMod.Speak($"Target: {hab.displayName}, Hab", true);
            }
        }

        private void NavigateToSpaceBody(TISpaceBodyState spaceBody, TIFactionState faction)
        {
            if (spaceBody == null)
            {
                TISpeechMod.Speak("Invalid space body", true);
                return;
            }

            // Switch to Space Bodies screen
            int screenIndex = navigation.FindScreenByName("Space Bodies");
            if (screenIndex < 0)
            {
                screenIndex = navigation.FindScreenByName("Orbits");
            }

            if (screenIndex >= 0)
            {
                navigation.SwitchToScreen(screenIndex);

                var bodies = navigation.CurrentScreen as SpaceBodiesScreen;
                if (bodies != null)
                {
                    int itemIndex = FindItemIndex(bodies, b =>
                        b is TISpaceBodyState sbs && sbs == spaceBody);
                    if (itemIndex >= 0)
                    {
                        navigation.SetItemIndex(itemIndex);
                        TISpeechMod.Speak($"Navigated to {spaceBody.displayName}. {navigation.GetCurrentAnnouncement()}", true);
                        return;
                    }
                }

                TISpeechMod.Speak($"Switched to Space Bodies screen. {spaceBody.displayName}", true);
            }
            else
            {
                TISpeechMod.Speak($"Target: {spaceBody.displayName}, Space Body", true);
            }
        }

        private int FindItemIndex(ScreenBase screen, Func<object, bool> predicate)
        {
            var items = screen.GetItems();
            if (items == null)
                return -1;

            for (int i = 0; i < items.Count; i++)
            {
                if (predicate(items[i]))
                    return i;
            }
            return -1;
        }

        private void AnnounceTargetFallback(TIGameState target)
        {
            string name = target?.displayName ?? "Unknown";
            string typeName = GetGameStateTypeName(target);
            TISpeechMod.Speak($"Target: {name}, {typeName}", true);
        }

        private string GetGameStateTypeName(TIGameState state)
        {
            if (state == null) return "Unknown";
            if (state.isNationState) return "Nation";
            if (state.isRegionState) return "Region";
            if (state.isCouncilorState) return "Councilor";
            if (state.isSpaceFleetState) return "Fleet";
            if (state.isHabState) return "Hab";
            if (state.isSpaceBodyState) return "Space Body";
            if (state.isArmyState) return "Army";
            if (state.isFactionState) return "Faction";
            if (state.isOrgState) return "Organization";
            return "Object";
        }
    }
}
