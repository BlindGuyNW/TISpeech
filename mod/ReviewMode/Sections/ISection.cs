using System;

namespace TISpeech.ReviewMode.Sections
{
    /// <summary>
    /// Interface for a navigable section within a screen.
    /// Each section contains a list of items that can be navigated and activated.
    /// </summary>
    public interface ISection
    {
        /// <summary>
        /// Display name of the section (e.g., "Tabs", "Councilors", "Stats")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Number of items in this section
        /// </summary>
        int ItemCount { get; }

        /// <summary>
        /// Read a specific item at the given index
        /// </summary>
        /// <param name="index">Zero-based index of the item</param>
        /// <returns>Text to announce for this item</returns>
        string ReadItem(int index);

        /// <summary>
        /// Read a summary of all items in the section
        /// </summary>
        /// <returns>Text summarizing all items</returns>
        string ReadSummary();

        /// <summary>
        /// Check if the item at the given index can be activated (clicked)
        /// </summary>
        /// <param name="index">Zero-based index of the item</param>
        /// <returns>True if the item is interactive</returns>
        bool CanActivate(int index);

        /// <summary>
        /// Activate (click) the item at the given index
        /// </summary>
        /// <param name="index">Zero-based index of the item</param>
        void Activate(int index);

        /// <summary>
        /// Check if the item at the given index has a tooltip
        /// </summary>
        /// <param name="index">Zero-based index of the item</param>
        /// <returns>True if tooltip is available</returns>
        bool HasTooltip(int index);

        /// <summary>
        /// Show the tooltip for the item at the given index
        /// </summary>
        /// <param name="index">Zero-based index of the item</param>
        void ShowTooltip(int index);
    }
}
