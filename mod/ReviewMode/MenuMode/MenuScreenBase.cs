using System;
using System.Collections.Generic;
using MelonLoader;

namespace TISpeech.ReviewMode.MenuMode
{
    /// <summary>
    /// Base class for menu screens in menu navigation mode.
    /// Each screen represents a menu (Main Menu, Load Game, Options, etc.)
    /// and provides navigation through its controls.
    /// </summary>
    public abstract class MenuScreenBase
    {
        /// <summary>
        /// Display name of the menu (e.g., "Main Menu", "Load Game")
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Get the list of controls on this menu screen.
        /// </summary>
        public abstract List<MenuControl> GetControls();

        /// <summary>
        /// Refresh the control list and their values.
        /// Called when the menu becomes active or needs updating.
        /// </summary>
        public abstract void Refresh();

        /// <summary>
        /// Called when this menu screen becomes active.
        /// </summary>
        public virtual void OnActivate()
        {
            Refresh();
        }

        /// <summary>
        /// Called when this menu screen becomes inactive.
        /// </summary>
        public virtual void OnDeactivate() { }

        /// <summary>
        /// Get the number of controls on this screen.
        /// </summary>
        public int ControlCount => GetControls()?.Count ?? 0;

        /// <summary>
        /// Whether this screen supports letter navigation.
        /// </summary>
        public virtual bool SupportsLetterNavigation => true;

        /// <summary>
        /// Get announcement text when this screen becomes active.
        /// </summary>
        public virtual string GetActivationAnnouncement()
        {
            int count = ControlCount;
            return $"{Name}. {count} items.";
        }

        /// <summary>
        /// Read the control at the given index.
        /// </summary>
        public virtual string ReadControl(int index)
        {
            var controls = GetControls();
            if (controls == null || index < 0 || index >= controls.Count)
                return "No control";

            var control = controls[index];
            // Refresh value before announcing (ensures we have current state)
            control.RefreshValue();
            return control.GetAnnouncement();
        }

        /// <summary>
        /// Read detail for the control at the given index.
        /// </summary>
        public virtual string ReadControlDetail(int index)
        {
            var controls = GetControls();
            if (controls == null || index < 0 || index >= controls.Count)
                return "No control";

            return controls[index].GetDetail();
        }

        /// <summary>
        /// Activate the control at the given index.
        /// </summary>
        public virtual void ActivateControl(int index)
        {
            var controls = GetControls();
            if (controls == null || index < 0 || index >= controls.Count)
                return;

            var control = controls[index];
            if (!control.IsInteractable)
            {
                TISpeechMod.Speak($"{control.Label} is disabled", interrupt: true);
                return;
            }

            control.Activate();
            MelonLogger.Msg($"Activated menu control: {control.Label}");
        }

        /// <summary>
        /// Adjust the control at the given index (for sliders, dropdowns).
        /// </summary>
        /// <param name="index">Control index</param>
        /// <param name="increment">True to increase, false to decrease</param>
        public virtual void AdjustControl(int index, bool increment)
        {
            var controls = GetControls();
            if (controls == null || index < 0 || index >= controls.Count)
                return;

            var control = controls[index];
            if (!control.IsInteractable)
            {
                TISpeechMod.Speak($"{control.Label} is disabled", interrupt: true);
                return;
            }

            if (control.Type == MenuControlType.Button)
            {
                // Buttons don't have adjustable values
                return;
            }

            control.Adjust(increment);
            control.RefreshValue();
            TISpeechMod.Speak(control.GetAnnouncement(), interrupt: true);
        }

        /// <summary>
        /// Check if the control at the given index can be adjusted with left/right.
        /// </summary>
        public virtual bool CanAdjustControl(int index)
        {
            var controls = GetControls();
            if (controls == null || index < 0 || index >= controls.Count)
                return false;

            var control = controls[index];
            return control.Type == MenuControlType.Slider ||
                   control.Type == MenuControlType.Dropdown ||
                   control.Type == MenuControlType.Toggle;
        }

        /// <summary>
        /// Find the next control starting with the given letter after the current index.
        /// </summary>
        public virtual int FindNextControlByLetter(char letter, int currentIndex)
        {
            if (!SupportsLetterNavigation)
                return -1;

            letter = char.ToUpperInvariant(letter);
            var controls = GetControls();
            if (controls == null || controls.Count == 0)
                return -1;

            // Search from current index + 1 to end
            for (int i = currentIndex + 1; i < controls.Count; i++)
            {
                string label = controls[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            // Wrap around: search from 0 to current index
            for (int i = 0; i <= currentIndex; i++)
            {
                string label = controls[i].Label;
                if (!string.IsNullOrEmpty(label) && char.ToUpperInvariant(label[0]) == letter)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// List all controls on this screen.
        /// </summary>
        public virtual string ListAllControls()
        {
            var controls = GetControls();
            if (controls == null || controls.Count == 0)
                return "No controls";

            var labels = new List<string>();
            foreach (var control in controls)
            {
                labels.Add(control.Label);
            }

            return $"{controls.Count} items: {string.Join(", ", labels)}";
        }
    }
}
