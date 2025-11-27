using System;
using System.Collections.Generic;
using System.Text;

namespace TISpeech.ReviewMode.Sections
{
    /// <summary>
    /// Base class for data-driven sections that display read-only information.
    /// </summary>
    public class DataSection : ISection
    {
        private string name;
        private List<DataItem> items = new List<DataItem>();

        public string Name => name;
        public int ItemCount => items.Count;

        public DataSection(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Add an item to the section
        /// </summary>
        public void AddItem(string label, string value, Action onActivate = null, Func<bool> hasTooltip = null, Action showTooltip = null)
        {
            items.Add(new DataItem
            {
                Label = label,
                Value = value,
                OnActivate = onActivate,
                HasTooltipFunc = hasTooltip,
                ShowTooltipFunc = showTooltip
            });
        }

        /// <summary>
        /// Add an item with just a value (no label)
        /// </summary>
        public void AddItem(string value, Action onActivate = null)
        {
            items.Add(new DataItem
            {
                Label = null,
                Value = value,
                OnActivate = onActivate
            });
        }

        /// <summary>
        /// Add an item with label, value, and detail text for extended description
        /// </summary>
        public void AddItem(string label, string value, string detailText, Action onActivate = null)
        {
            items.Add(new DataItem
            {
                Label = label,
                Value = value,
                DetailText = detailText,
                OnActivate = onActivate
            });
        }

        /// <summary>
        /// Clear all items
        /// </summary>
        public void Clear()
        {
            items.Clear();
        }

        public string ReadItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            if (string.IsNullOrEmpty(item.Label))
                return item.Value;
            return $"{item.Label}: {item.Value}";
        }

        public string ReadSummary()
        {
            if (items.Count == 0)
                return "No items";

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Label))
                    sb.Append($"{item.Value}, ");
                else
                    sb.Append($"{item.Label}: {item.Value}, ");
            }
            return sb.ToString().TrimEnd(',', ' ');
        }

        public bool CanActivate(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;
            return items[index].OnActivate != null;
        }

        public void Activate(int index)
        {
            if (index < 0 || index >= items.Count)
                return;
            items[index].OnActivate?.Invoke();
        }

        public bool HasTooltip(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;
            return items[index].HasTooltipFunc?.Invoke() ?? false;
        }

        public void ShowTooltip(int index)
        {
            if (index < 0 || index >= items.Count)
                return;
            items[index].ShowTooltipFunc?.Invoke();
        }

        /// <summary>
        /// Get detail text for an item (for Numpad * reading)
        /// </summary>
        public string ReadItemDetail(int index)
        {
            if (index < 0 || index >= items.Count)
                return "Invalid item";

            var item = items[index];
            // If there's detail text, return it; otherwise return the standard reading
            if (!string.IsNullOrEmpty(item.DetailText))
                return item.DetailText;
            return ReadItem(index);
        }

        private class DataItem
        {
            public string Label;
            public string Value;
            public string DetailText; // Extended description for detail reading
            public Action OnActivate;
            public Func<bool> HasTooltipFunc;
            public Action ShowTooltipFunc;
        }
    }
}
