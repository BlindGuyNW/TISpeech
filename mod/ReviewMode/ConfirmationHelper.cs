using System;
using System.Collections.Generic;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// Helper for creating confirmation prompts in review mode.
    /// Uses the existing SelectionSubMode mechanism with standard Confirm/Cancel options.
    /// </summary>
    public static class ConfirmationHelper
    {
        /// <summary>
        /// Request confirmation for an action.
        /// </summary>
        /// <param name="actionDescription">What action is being performed (e.g., "Recruit John Smith")</param>
        /// <param name="details">Additional details like cost (e.g., "Cost: 50 Influence")</param>
        /// <param name="enterSelectionMode">Callback to enter selection mode (from screen)</param>
        /// <param name="onConfirm">Called if user confirms</param>
        /// <param name="onCancel">Optional callback if user cancels</param>
        public static void RequestConfirmation(
            string actionDescription,
            string details,
            Action<string, List<SelectionOption>, Action<int>> enterSelectionMode,
            Action onConfirm,
            Action onCancel = null)
        {
            var options = new List<SelectionOption>
            {
                new SelectionOption
                {
                    Label = "Confirm",
                    DetailText = $"Confirm: {actionDescription}. {details}"
                },
                new SelectionOption
                {
                    Label = "Cancel",
                    DetailText = "Cancel this action"
                }
            };

            string prompt = $"{actionDescription}. {details}";

            enterSelectionMode(prompt, options, (index) =>
            {
                if (index == 0) // Confirm
                {
                    onConfirm?.Invoke();
                }
                else // Cancel
                {
                    onCancel?.Invoke();
                }
            });
        }

        /// <summary>
        /// Request confirmation with a choice between options.
        /// Useful when there are multiple ways to execute an action.
        /// </summary>
        /// <typeparam name="T">Type of data associated with each choice</typeparam>
        /// <param name="prompt">The prompt to show</param>
        /// <param name="choices">List of choices with labels and data</param>
        /// <param name="enterSelectionMode">Callback to enter selection mode</param>
        /// <param name="onSelect">Called with the selected choice data</param>
        /// <param name="onCancel">Optional callback if user cancels</param>
        public static void RequestChoice<T>(
            string prompt,
            List<ConfirmationChoice<T>> choices,
            Action<string, List<SelectionOption>, Action<int>> enterSelectionMode,
            Action<T> onSelect,
            Action onCancel = null)
        {
            var options = new List<SelectionOption>();

            foreach (var choice in choices)
            {
                options.Add(new SelectionOption
                {
                    Label = choice.Label,
                    DetailText = choice.DetailText,
                    Data = choice.Data
                });
            }

            // Add cancel option
            options.Add(new SelectionOption
            {
                Label = "Cancel",
                DetailText = "Cancel this action",
                Data = default(T)
            });

            enterSelectionMode(prompt, options, (index) =>
            {
                if (index < choices.Count) // A real choice, not cancel
                {
                    onSelect?.Invoke(choices[index].Data);
                }
                else // Cancel
                {
                    onCancel?.Invoke();
                }
            });
        }

        /// <summary>
        /// Build a standard confirmation message for resource costs.
        /// </summary>
        public static string FormatCostDetails(string costString, string additionalInfo = null)
        {
            string details = $"Cost: {costString}";
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                details += $". {additionalInfo}";
            }
            return details;
        }
    }

    /// <summary>
    /// A choice option for confirmation with choice.
    /// </summary>
    public class ConfirmationChoice<T>
    {
        public string Label { get; set; }
        public string DetailText { get; set; }
        public T Data { get; set; }

        public ConfirmationChoice(string label, T data, string detailText = null)
        {
            Label = label;
            Data = data;
            DetailText = detailText ?? label;
        }
    }
}
