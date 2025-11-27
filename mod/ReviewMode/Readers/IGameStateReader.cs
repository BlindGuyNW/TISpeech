using System.Collections.Generic;
using TISpeech.ReviewMode.Sections;

namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Interface for readers that extract and format data from game state objects.
    /// Readers are responsible for translating game state into accessible text.
    /// </summary>
    /// <typeparam name="T">The game state type this reader handles</typeparam>
    public interface IGameStateReader<T>
    {
        /// <summary>
        /// Read a one-line summary suitable for list navigation.
        /// Example: "Chen Wei, Spy, Investigating Mumbai"
        /// </summary>
        string ReadSummary(T state);

        /// <summary>
        /// Read detailed information about the state object.
        /// Used when user requests full detail (Numpad *).
        /// </summary>
        string ReadDetail(T state);

        /// <summary>
        /// Get navigable sections for this state object.
        /// Sections contain the items the user can navigate through.
        /// </summary>
        List<ISection> GetSections(T state);
    }

    /// <summary>
    /// Represents an action that can be performed on a game state object.
    /// </summary>
    public class ActionItem
    {
        /// <summary>
        /// Display label for the action
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Description of what the action does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Action to execute when activated
        /// </summary>
        public System.Action Execute { get; set; }

        /// <summary>
        /// Optional check for whether the action is currently available
        /// </summary>
        public System.Func<bool> IsAvailable { get; set; }
    }
}
