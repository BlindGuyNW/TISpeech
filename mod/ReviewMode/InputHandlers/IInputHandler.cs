using System;

namespace TISpeech.ReviewMode.InputHandlers
{
    /// <summary>
    /// Interface for input handlers that can process keyboard input in Review Mode.
    /// </summary>
    public interface IInputHandler
    {
        /// <summary>
        /// Handle input for this mode/context.
        /// </summary>
        /// <returns>True if input was handled, false otherwise.</returns>
        bool HandleInput();
    }
}
