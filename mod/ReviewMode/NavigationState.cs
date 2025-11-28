using System;
using System.Collections.Generic;
using MelonLoader;
using TISpeech.ReviewMode.Screens;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Navigation levels in the review mode hierarchy.
    /// </summary>
    public enum NavigationLevel
    {
        /// <summary>Top level: choosing between screens</summary>
        Screens,
        /// <summary>Within a screen: choosing between items (e.g., councilors, nations)</summary>
        Items,
        /// <summary>Within an item: choosing between sections (e.g., Stats, Traits, Missions)</summary>
        Sections,
        /// <summary>Within a section: choosing between section items</summary>
        SectionItems
    }

    /// <summary>
    /// Manages hierarchical navigation state for review mode.
    /// Tracks current position at each level and handles drill-down/back-out navigation.
    /// </summary>
    public class NavigationState
    {
        private List<ScreenBase> screens = new List<ScreenBase>();
        private int currentScreenIndex = 0;
        private int currentItemIndex = 0;
        private int currentSectionIndex = 0;
        private int currentSectionItemIndex = 0;
        private NavigationLevel currentLevel = NavigationLevel.Screens;

        // Cache for current item's sections
        private IReadOnlyList<ISection> currentSections = null;

        // State for drilling into section items (e.g., techs in tech browser)
        private bool inSectionItemDrill = false;
        private IReadOnlyList<ISection> parentSections = null;
        private int parentSectionIndex = 0;
        private int parentSectionItemIndex = 0;
        private string drilledItemSecondaryId = null;

        /// <summary>
        /// Current navigation level in the hierarchy.
        /// </summary>
        public NavigationLevel CurrentLevel => currentLevel;

        /// <summary>
        /// Currently selected screen.
        /// </summary>
        public ScreenBase CurrentScreen => screens.Count > 0 && currentScreenIndex >= 0 && currentScreenIndex < screens.Count
            ? screens[currentScreenIndex]
            : null;

        /// <summary>
        /// Currently selected section (when at Sections or SectionItems level).
        /// </summary>
        public ISection CurrentSection => currentSections != null && currentSectionIndex >= 0 && currentSectionIndex < currentSections.Count
            ? currentSections[currentSectionIndex]
            : null;

        /// <summary>
        /// Index of the current item within the current screen.
        /// </summary>
        public int CurrentItemIndex => currentItemIndex;

        /// <summary>
        /// Index of the current section item.
        /// </summary>
        public int CurrentSectionItemIndex => currentSectionItemIndex;

        /// <summary>
        /// Register available screens.
        /// </summary>
        public void RegisterScreens(IEnumerable<ScreenBase> screenList)
        {
            screens.Clear();
            screens.AddRange(screenList);
            Reset();
        }

        /// <summary>
        /// Reset navigation to initial state (first screen, Screens level).
        /// </summary>
        public void Reset()
        {
            currentScreenIndex = 0;
            currentItemIndex = 0;
            currentSectionIndex = 0;
            currentSectionItemIndex = 0;
            currentLevel = NavigationLevel.Screens;
            currentSections = null;

            if (screens.Count > 0)
            {
                screens[0].OnActivate();
            }
        }

        /// <summary>
        /// Navigate to the next item at the current level.
        /// </summary>
        public void Next()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    if (screens.Count > 0)
                    {
                        CurrentScreen?.OnDeactivate();
                        currentScreenIndex = (currentScreenIndex + 1) % screens.Count;
                        CurrentScreen?.OnActivate();
                    }
                    break;

                case NavigationLevel.Items:
                    if (CurrentScreen != null && CurrentScreen.ItemCount > 0)
                    {
                        currentItemIndex = (currentItemIndex + 1) % CurrentScreen.ItemCount;
                        currentSections = null; // Invalidate section cache
                    }
                    break;

                case NavigationLevel.Sections:
                    if (currentSections != null && currentSections.Count > 0)
                    {
                        currentSectionIndex = (currentSectionIndex + 1) % currentSections.Count;
                        currentSectionItemIndex = 0;
                    }
                    break;

                case NavigationLevel.SectionItems:
                    if (CurrentSection != null && CurrentSection.ItemCount > 0)
                    {
                        currentSectionItemIndex = (currentSectionItemIndex + 1) % CurrentSection.ItemCount;
                    }
                    break;
            }
        }

        /// <summary>
        /// Navigate to the previous item at the current level.
        /// </summary>
        public void Previous()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    if (screens.Count > 0)
                    {
                        CurrentScreen?.OnDeactivate();
                        currentScreenIndex--;
                        if (currentScreenIndex < 0) currentScreenIndex = screens.Count - 1;
                        CurrentScreen?.OnActivate();
                    }
                    break;

                case NavigationLevel.Items:
                    if (CurrentScreen != null && CurrentScreen.ItemCount > 0)
                    {
                        currentItemIndex--;
                        if (currentItemIndex < 0) currentItemIndex = CurrentScreen.ItemCount - 1;
                        currentSections = null; // Invalidate section cache
                    }
                    break;

                case NavigationLevel.Sections:
                    if (currentSections != null && currentSections.Count > 0)
                    {
                        currentSectionIndex--;
                        if (currentSectionIndex < 0) currentSectionIndex = currentSections.Count - 1;
                        currentSectionItemIndex = 0;
                    }
                    break;

                case NavigationLevel.SectionItems:
                    if (CurrentSection != null && CurrentSection.ItemCount > 0)
                    {
                        currentSectionItemIndex--;
                        if (currentSectionItemIndex < 0) currentSectionItemIndex = CurrentSection.ItemCount - 1;
                    }
                    break;
            }
        }

        /// <summary>
        /// Drill down into the current selection.
        /// Returns true if drill-down was successful.
        /// </summary>
        public bool DrillDown()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    if (CurrentScreen != null && CurrentScreen.ItemCount > 0)
                    {
                        currentLevel = NavigationLevel.Items;
                        currentItemIndex = 0;
                        currentSections = null;
                        return true;
                    }
                    break;

                case NavigationLevel.Items:
                    if (CurrentScreen != null && CurrentScreen.CanDrillIntoItem(currentItemIndex))
                    {
                        currentSections = CurrentScreen.GetSectionsForItem(currentItemIndex);
                        if (currentSections != null && currentSections.Count > 0)
                        {
                            currentLevel = NavigationLevel.Sections;
                            currentSectionIndex = 0;
                            currentSectionItemIndex = 0;
                            return true;
                        }
                    }
                    break;

                case NavigationLevel.Sections:
                    if (CurrentSection != null && CurrentSection.ItemCount > 0)
                    {
                        currentLevel = NavigationLevel.SectionItems;
                        currentSectionItemIndex = 0;
                        return true;
                    }
                    break;

                case NavigationLevel.SectionItems:
                    if (CurrentSection != null)
                    {
                        // Check if this section item can be drilled into (has sub-sections)
                        if (CurrentSection.CanDrillIntoItem(currentSectionItemIndex))
                        {
                            string secondaryId = CurrentSection.GetItemSecondaryValue(currentSectionItemIndex);
                            if (!string.IsNullOrEmpty(secondaryId) && CurrentScreen != null)
                            {
                                var subSections = CurrentScreen.GetSectionsForSectionItem(secondaryId);
                                if (subSections != null && subSections.Count > 0)
                                {
                                    // Save parent state
                                    parentSections = currentSections;
                                    parentSectionIndex = currentSectionIndex;
                                    parentSectionItemIndex = currentSectionItemIndex;
                                    drilledItemSecondaryId = secondaryId;
                                    inSectionItemDrill = true;

                                    // Navigate into sub-sections
                                    currentSections = subSections;
                                    currentSectionIndex = 0;
                                    currentSectionItemIndex = 0;
                                    currentLevel = NavigationLevel.Sections;
                                    return true;
                                }
                            }
                        }

                        // Fall back to activation if can't drill
                        if (CurrentSection.CanActivate(currentSectionItemIndex))
                        {
                            CurrentSection.Activate(currentSectionItemIndex);
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }

        /// <summary>
        /// Back out to the previous navigation level.
        /// Returns true if back-out was successful, false if at top level.
        /// </summary>
        public bool BackOut()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    // At top level, can't back out further
                    return false;

                case NavigationLevel.Items:
                    currentLevel = NavigationLevel.Screens;
                    currentSections = null;
                    return true;

                case NavigationLevel.Sections:
                    // If we drilled into a section item, restore parent state
                    if (inSectionItemDrill && parentSections != null)
                    {
                        currentSections = parentSections;
                        currentSectionIndex = parentSectionIndex;
                        currentSectionItemIndex = parentSectionItemIndex;
                        parentSections = null;
                        inSectionItemDrill = false;
                        drilledItemSecondaryId = null;
                        currentLevel = NavigationLevel.SectionItems;
                        return true;
                    }
                    currentLevel = NavigationLevel.Items;
                    currentSectionIndex = 0;
                    return true;

                case NavigationLevel.SectionItems:
                    currentLevel = NavigationLevel.Sections;
                    currentSectionItemIndex = 0;
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get announcement text for the current position.
        /// </summary>
        public string GetCurrentAnnouncement()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    if (CurrentScreen != null)
                    {
                        return $"Screen {currentScreenIndex + 1} of {screens.Count}: {CurrentScreen.Name}";
                    }
                    return "No screens available";

                case NavigationLevel.Items:
                    if (CurrentScreen != null && CurrentScreen.ItemCount > 0)
                    {
                        string summary = CurrentScreen.ReadItemSummary(currentItemIndex);
                        return $"{currentItemIndex + 1} of {CurrentScreen.ItemCount}: {summary}";
                    }
                    return "No items";

                case NavigationLevel.Sections:
                    if (CurrentSection != null && currentSections != null)
                    {
                        return $"Section {currentSectionIndex + 1} of {currentSections.Count}: {CurrentSection.Name}, {CurrentSection.ItemCount} items";
                    }
                    return "No sections";

                case NavigationLevel.SectionItems:
                    if (CurrentSection != null && CurrentSection.ItemCount > 0)
                    {
                        string itemText = CurrentSection.ReadItem(currentSectionItemIndex);
                        bool canActivate = CurrentSection.CanActivate(currentSectionItemIndex);
                        string suffix = canActivate ? " (press Enter to activate)" : "";
                        return $"{currentSectionItemIndex + 1} of {CurrentSection.ItemCount}: {itemText}{suffix}";
                    }
                    return "No items in section";
            }
            return "Unknown state";
        }

        /// <summary>
        /// Get a detailed reading of the current item.
        /// </summary>
        public string GetCurrentDetail()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    return CurrentScreen?.GetActivationAnnouncement() ?? "No screen";

                case NavigationLevel.Items:
                    return CurrentScreen?.ReadItemDetail(currentItemIndex) ?? "No item detail";

                case NavigationLevel.Sections:
                    return CurrentSection?.ReadSummary() ?? "No section detail";

                case NavigationLevel.SectionItems:
                    return CurrentSection?.ReadItemDetail(currentSectionItemIndex) ?? "No item";
            }
            return "Unknown";
        }

        /// <summary>
        /// List all items at the current level.
        /// </summary>
        public string ListCurrentLevel()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    var screenNames = new List<string>();
                    for (int i = 0; i < screens.Count; i++)
                    {
                        string marker = i == currentScreenIndex ? " (current)" : "";
                        screenNames.Add($"{screens[i].Name}{marker}");
                    }
                    return $"{screens.Count} screens: {string.Join(", ", screenNames)}";

                case NavigationLevel.Items:
                    if (CurrentScreen == null) return "No items";
                    int itemCount = CurrentScreen.ItemCount;
                    return $"{itemCount} items in {CurrentScreen.Name}";

                case NavigationLevel.Sections:
                    if (currentSections == null) return "No sections";
                    var sectionNames = new List<string>();
                    for (int i = 0; i < currentSections.Count; i++)
                    {
                        string marker = i == currentSectionIndex ? " (current)" : "";
                        sectionNames.Add($"{currentSections[i].Name}{marker}");
                    }
                    return $"{currentSections.Count} sections: {string.Join(", ", sectionNames)}";

                case NavigationLevel.SectionItems:
                    if (CurrentSection == null) return "No items";
                    return $"{CurrentSection.ItemCount} items in {CurrentSection.Name}";
            }
            return "Unknown";
        }

        /// <summary>
        /// Get the count of items at the current level.
        /// </summary>
        public int GetCurrentLevelCount()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    return screens.Count;
                case NavigationLevel.Items:
                    return CurrentScreen?.ItemCount ?? 0;
                case NavigationLevel.Sections:
                    return currentSections?.Count ?? 0;
                case NavigationLevel.SectionItems:
                    return CurrentSection?.ItemCount ?? 0;
            }
            return 0;
        }

        /// <summary>
        /// Get the current index at the current level.
        /// </summary>
        public int GetCurrentIndex()
        {
            switch (currentLevel)
            {
                case NavigationLevel.Screens:
                    return currentScreenIndex;
                case NavigationLevel.Items:
                    return currentItemIndex;
                case NavigationLevel.Sections:
                    return currentSectionIndex;
                case NavigationLevel.SectionItems:
                    return currentSectionItemIndex;
            }
            return 0;
        }

        /// <summary>
        /// Refresh sections from the current screen.
        /// Called after actions that modify data to ensure fresh sections.
        /// Backs out to Sections level if currently at SectionItems.
        /// </summary>
        public void RefreshSections()
        {
            if (CurrentScreen == null)
                return;

            // Re-fetch sections for current item
            if (currentLevel == NavigationLevel.Sections || currentLevel == NavigationLevel.SectionItems)
            {
                currentSections = CurrentScreen.GetSectionsForItem(currentItemIndex);

                // Validate section index
                if (currentSections == null || currentSections.Count == 0)
                {
                    // No sections - back out to Items level
                    currentLevel = NavigationLevel.Items;
                    currentSectionIndex = 0;
                    currentSectionItemIndex = 0;
                }
                else if (currentSectionIndex >= currentSections.Count)
                {
                    currentSectionIndex = 0;
                    currentSectionItemIndex = 0;
                }

                // If at SectionItems, validate section item index
                if (currentLevel == NavigationLevel.SectionItems && CurrentSection != null)
                {
                    if (currentSectionItemIndex >= CurrentSection.ItemCount)
                    {
                        currentSectionItemIndex = 0;
                    }
                }
            }
        }
    }
}
