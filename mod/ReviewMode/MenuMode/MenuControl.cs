using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TISpeech.ReviewMode.MenuMode
{
    /// <summary>
    /// Types of UI controls that can be navigated in menu mode.
    /// </summary>
    public enum MenuControlType
    {
        Button,
        Dropdown,
        Toggle,
        Slider,
        ScrollListItem,
        InputField
    }

    /// <summary>
    /// Represents a navigable UI control in a menu.
    /// </summary>
    public class MenuControl
    {
        /// <summary>
        /// Type of this control (button, dropdown, etc.)
        /// </summary>
        public MenuControlType Type { get; set; }

        /// <summary>
        /// Display label for this control (e.g., "New Game", "Volume")
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Current value for controls that have values (dropdowns, toggles, sliders)
        /// </summary>
        public string CurrentValue { get; set; }

        /// <summary>
        /// Reference to the Unity GameObject for this control
        /// </summary>
        public GameObject GameObject { get; set; }

        /// <summary>
        /// Whether this control can be interacted with
        /// </summary>
        public bool IsInteractable { get; set; } = true;

        /// <summary>
        /// Optional detail text for this control
        /// </summary>
        public string DetailText { get; set; }

        /// <summary>
        /// Semantic action identifier for this control (e.g., "NewGame", "LoadGame").
        /// Used for determining behavior independent of localized label text.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// For sliders: minimum value
        /// </summary>
        public float MinValue { get; set; }

        /// <summary>
        /// For sliders: maximum value
        /// </summary>
        public float MaxValue { get; set; }

        /// <summary>
        /// For sliders: step size for adjustment
        /// </summary>
        public float StepSize { get; set; } = 0.1f;

        /// <summary>
        /// Get announcement text for this control.
        /// </summary>
        public string GetAnnouncement()
        {
            string interactableText = IsInteractable ? "" : " (disabled)";

            switch (Type)
            {
                case MenuControlType.Button:
                    return $"{Label}{interactableText}";

                case MenuControlType.Dropdown:
                    return $"{Label}: {CurrentValue ?? "none"}{interactableText}";

                case MenuControlType.Toggle:
                    return $"{Label}: {CurrentValue ?? "unknown"}{interactableText}";

                case MenuControlType.Slider:
                    return $"{Label}: {CurrentValue ?? "unknown"}{interactableText}";

                case MenuControlType.ScrollListItem:
                    return Label + interactableText;

                case MenuControlType.InputField:
                    return $"{Label}: {CurrentValue ?? "empty"}{interactableText}";

                default:
                    return Label + interactableText;
            }
        }

        /// <summary>
        /// Get detail text for this control.
        /// </summary>
        public string GetDetail()
        {
            if (!string.IsNullOrEmpty(DetailText))
                return DetailText;

            return GetAnnouncement();
        }

        /// <summary>
        /// Activate this control (click button, open dropdown, toggle, etc.)
        /// </summary>
        public void Activate()
        {
            if (!IsInteractable || GameObject == null)
                return;

            switch (Type)
            {
                case MenuControlType.Button:
                    var button = GameObject.GetComponent<Button>();
                    button?.onClick?.Invoke();
                    break;

                case MenuControlType.Toggle:
                    var toggle = GameObject.GetComponent<Toggle>();
                    if (toggle != null)
                    {
                        toggle.isOn = !toggle.isOn;
                    }
                    break;

                case MenuControlType.Dropdown:
                    var dropdown = GameObject.GetComponent<TMP_Dropdown>();
                    if (dropdown != null)
                    {
                        // Cycle to next option
                        int nextIndex = (dropdown.value + 1) % dropdown.options.Count;
                        dropdown.value = nextIndex;
                    }
                    break;

                // InputField is handled specially by EscapeMenuSubMode.ActivateCurrentControl
                // which enters text input mode instead of activating directly
            }
        }

        /// <summary>
        /// Adjust control value (for sliders and dropdowns).
        /// </summary>
        /// <param name="increment">True to increase, false to decrease</param>
        public void Adjust(bool increment)
        {
            if (!IsInteractable || GameObject == null)
                return;

            switch (Type)
            {
                case MenuControlType.Slider:
                    var slider = GameObject.GetComponent<Slider>();
                    if (slider != null)
                    {
                        // Calculate step size based on slider range (default to 5% of range)
                        float range = slider.maxValue - slider.minValue;
                        float step = StepSize > 0.1f ? StepSize : range * 0.05f;
                        float delta = increment ? step : -step;
                        float newValue = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
                        slider.value = newValue;
                        // Explicitly invoke onValueChanged to ensure game reacts
                        slider.onValueChanged?.Invoke(newValue);
                    }
                    break;

                case MenuControlType.Dropdown:
                    var dropdown = GameObject.GetComponent<TMP_Dropdown>();
                    if (dropdown != null && dropdown.options.Count > 0)
                    {
                        int delta = increment ? 1 : -1;
                        int newIndex = dropdown.value + delta;
                        if (newIndex < 0) newIndex = dropdown.options.Count - 1;
                        if (newIndex >= dropdown.options.Count) newIndex = 0;
                        dropdown.value = newIndex;
                    }
                    break;

                case MenuControlType.Toggle:
                    var toggle = GameObject.GetComponent<Toggle>();
                    if (toggle != null)
                    {
                        toggle.isOn = !toggle.isOn;
                    }
                    break;
            }
        }

        /// <summary>
        /// Refresh the current value from the Unity component.
        /// </summary>
        public void RefreshValue()
        {
            if (GameObject == null)
                return;

            switch (Type)
            {
                case MenuControlType.Toggle:
                    var toggle = GameObject.GetComponent<Toggle>();
                    CurrentValue = toggle?.isOn == true ? "on" : "off";
                    IsInteractable = toggle?.interactable ?? false;
                    break;

                case MenuControlType.Slider:
                    var slider = GameObject.GetComponent<Slider>();
                    if (slider != null)
                    {
                        MinValue = slider.minValue;
                        MaxValue = slider.maxValue;
                        IsInteractable = slider.interactable;
                        // Read directly from slider value - no need for UI text
                        CurrentValue = FormatSliderValue(slider);
                    }
                    break;

                case MenuControlType.Dropdown:
                    var dropdown = GameObject.GetComponent<TMP_Dropdown>();
                    if (dropdown != null && dropdown.options.Count > 0 && dropdown.value < dropdown.options.Count)
                    {
                        CurrentValue = dropdown.options[dropdown.value].text;
                        IsInteractable = dropdown.interactable;
                    }
                    break;

                case MenuControlType.InputField:
                    var inputField = GameObject.GetComponent<TMP_InputField>();
                    CurrentValue = inputField?.text ?? "";
                    IsInteractable = inputField?.interactable ?? false;
                    break;

                case MenuControlType.Button:
                    var button = GameObject.GetComponent<Button>();
                    IsInteractable = button?.interactable ?? false;
                    break;
            }
        }

        /// <summary>
        /// Format a slider value for display based on its range.
        /// </summary>
        private static string FormatSliderValue(Slider slider)
        {
            if (slider == null)
                return "0";

            float value = slider.value;
            float range = slider.maxValue - slider.minValue;

            // If range is 0-100, show as percentage
            if (slider.minValue == 0 && slider.maxValue == 100)
            {
                return $"{(int)value}%";
            }
            // If range is 0-1, show as percentage
            else if (slider.minValue == 0 && slider.maxValue == 1)
            {
                return $"{(int)(value * 100)}%";
            }
            // Otherwise show with appropriate precision
            else if (range >= 10)
            {
                return ((int)value).ToString();
            }
            else
            {
                return value.ToString("F1");
            }
        }

        /// <summary>
        /// Create a MenuControl from a Button component.
        /// </summary>
        public static MenuControl FromButton(Button button, string label = null)
        {
            if (button == null)
                return null;

            string text = label;
            if (string.IsNullOrEmpty(text))
            {
                var tmpText = button.GetComponentInChildren<TMP_Text>();
                text = tmpText?.text ?? button.gameObject.name;
            }

            return new MenuControl
            {
                Type = MenuControlType.Button,
                Label = TISpeechMod.CleanText(text),
                GameObject = button.gameObject,
                IsInteractable = button.interactable
            };
        }

        /// <summary>
        /// Create a MenuControl from a Toggle component.
        /// </summary>
        public static MenuControl FromToggle(Toggle toggle, string label = null)
        {
            if (toggle == null)
                return null;

            string text = label;
            if (string.IsNullOrEmpty(text))
            {
                var tmpText = toggle.GetComponentInChildren<TMP_Text>();
                text = tmpText?.text ?? toggle.gameObject.name;
            }

            return new MenuControl
            {
                Type = MenuControlType.Toggle,
                Label = TISpeechMod.CleanText(text),
                CurrentValue = toggle.isOn ? "on" : "off",
                GameObject = toggle.gameObject,
                IsInteractable = toggle.interactable
            };
        }

        /// <summary>
        /// Create a MenuControl from a Slider component.
        /// </summary>
        public static MenuControl FromSlider(Slider slider, string label = null)
        {
            if (slider == null)
                return null;

            return new MenuControl
            {
                Type = MenuControlType.Slider,
                Label = label ?? slider.gameObject.name,
                CurrentValue = slider.value.ToString("F1"),
                GameObject = slider.gameObject,
                IsInteractable = slider.interactable,
                MinValue = slider.minValue,
                MaxValue = slider.maxValue
            };
        }

        /// <summary>
        /// Create a MenuControl from a TMP_Dropdown component.
        /// </summary>
        public static MenuControl FromDropdown(TMP_Dropdown dropdown, string label = null)
        {
            if (dropdown == null)
                return null;

            string currentValue = "";
            if (dropdown.options.Count > 0 && dropdown.value < dropdown.options.Count)
            {
                currentValue = dropdown.options[dropdown.value].text;
            }

            return new MenuControl
            {
                Type = MenuControlType.Dropdown,
                Label = label ?? dropdown.gameObject.name,
                CurrentValue = TISpeechMod.CleanText(currentValue),
                GameObject = dropdown.gameObject,
                IsInteractable = dropdown.interactable
            };
        }
    }
}
