using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace TISpeech.ReviewMode.EscapeMenu.Codex
{
    /// <summary>
    /// Sub-mode for navigating the in-game Codex encyclopedia.
    /// Provides keyboard navigation through topics (left panel) and content sections (right panel).
    /// Reads directly from the existing UI components, respecting the game's content gating.
    /// </summary>
    public class CodexSubMode
    {
        public enum NavigationLevel
        {
            Topics,  // Browsing topic list (left panel)
            Content  // Reading content sections (right panel)
        }

        private CodexController controller;
        private NavigationLevel currentLevel = NavigationLevel.Topics;
        private int currentTopicIndex = 0;
        private int currentContentIndex = 0;

        // Cached lists to avoid frequent traversal
        private List<CodexTopicListItemController> cachedTopics;
        private List<CodexInfoListItemController> cachedContent;

        public bool IsActive { get; private set; }
        public NavigationLevel CurrentLevel => currentLevel;

        /// <summary>
        /// Check if the Codex UI is currently visible.
        /// </summary>
        public static bool IsCodexVisible()
        {
            try
            {
                var codexController = UnityEngine.Object.FindObjectOfType<CodexController>();
                if (codexController == null)
                    return false;
                return codexController.Visible();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Activate Codex navigation mode.
        /// </summary>
        public void Activate()
        {
            try
            {
                controller = UnityEngine.Object.FindObjectOfType<CodexController>();
                if (controller == null)
                {
                    MelonLogger.Error("CodexSubMode: CodexController not found");
                    return;
                }

                currentLevel = NavigationLevel.Topics;
                currentTopicIndex = 0;
                currentContentIndex = 0;
                IsActive = true;

                // Refresh caches
                RefreshTopicCache();

                // Announce activation
                int topicCount = cachedTopics?.Count ?? 0;
                TISpeechMod.Speak($"Codex. {topicCount} topics. Use up and down to navigate, Enter to read topic.", interrupt: true);

                // Announce first topic if available
                if (topicCount > 0)
                {
                    string firstTopic = ReadTopic(0);
                    TISpeechMod.Speak(firstTopic, interrupt: false);
                }

                MelonLogger.Msg($"CodexSubMode activated with {topicCount} topics");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error activating CodexSubMode: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivate Codex navigation mode.
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
            controller = null;
            cachedTopics = null;
            cachedContent = null;
            MelonLogger.Msg("CodexSubMode deactivated");
        }

        #region Topic Navigation

        /// <summary>
        /// Get visible topics from the UI (only active GameObjects).
        /// </summary>
        private List<CodexTopicListItemController> GetVisibleTopics()
        {
            var topics = new List<CodexTopicListItemController>();

            if (controller == null || controller.codexTopicListManager == null)
                return topics;

            foreach (Transform child in controller.codexTopicListManager.transform)
            {
                if (child.gameObject.activeSelf)
                {
                    var item = child.GetComponent<CodexTopicListItemController>();
                    if (item != null)
                        topics.Add(item);
                }
            }

            return topics;
        }

        /// <summary>
        /// Refresh the cached topic list.
        /// </summary>
        private void RefreshTopicCache()
        {
            cachedTopics = GetVisibleTopics();
        }

        /// <summary>
        /// Read a topic at the given index.
        /// </summary>
        private string ReadTopic(int index)
        {
            if (cachedTopics == null || index < 0 || index >= cachedTopics.Count)
                return "No topic";

            var topic = cachedTopics[index];
            string title = TISpeechMod.CleanText(topic.topicTitle.text);
            string suffix = topic.template.mainTopic ? ", main topic" : "";
            return $"{index + 1} of {cachedTopics.Count}: {title}{suffix}";
        }

        #endregion

        #region Content Navigation

        /// <summary>
        /// Get content items from the UI (only active GameObjects).
        /// </summary>
        private List<CodexInfoListItemController> GetVisibleContent()
        {
            var items = new List<CodexInfoListItemController>();

            if (controller == null || controller.codexInfoListManager == null)
                return items;

            foreach (Transform child in controller.codexInfoListManager.transform)
            {
                if (child.gameObject.activeSelf)
                {
                    var item = child.GetComponent<CodexInfoListItemController>();
                    if (item != null)
                        items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        /// Refresh the cached content list.
        /// </summary>
        private void RefreshContentCache()
        {
            cachedContent = GetVisibleContent();
        }

        /// <summary>
        /// Read content at the given index.
        /// </summary>
        private string ReadContent(int index)
        {
            if (cachedContent == null || index < 0 || index >= cachedContent.Count)
                return "No content";

            var item = cachedContent[index];
            var sb = new StringBuilder();
            sb.Append($"{index + 1} of {cachedContent.Count}: ");

            // Add title if present (first section of a topic)
            if (item.title != null && item.title.gameObject != null && item.title.gameObject.activeSelf)
            {
                string titleText = TISpeechMod.CleanText(item.title.text);
                if (!string.IsNullOrEmpty(titleText))
                    sb.Append(titleText + ". ");
            }

            // Add main content
            if (item.infoText != null)
            {
                string content = TISpeechMod.CleanText(item.infoText.text);
                sb.Append(content);
            }

            return sb.ToString();
        }

        #endregion

        #region Navigation Commands

        /// <summary>
        /// Navigate to the previous item at the current level.
        /// </summary>
        public void NavigatePrevious()
        {
            if (currentLevel == NavigationLevel.Topics)
            {
                if (cachedTopics == null || cachedTopics.Count == 0)
                    return;

                currentTopicIndex--;
                if (currentTopicIndex < 0)
                    currentTopicIndex = cachedTopics.Count - 1;

                TISpeechMod.Speak(ReadTopic(currentTopicIndex), interrupt: true);
            }
            else if (currentLevel == NavigationLevel.Content)
            {
                if (cachedContent == null || cachedContent.Count == 0)
                    return;

                currentContentIndex--;
                if (currentContentIndex < 0)
                    currentContentIndex = cachedContent.Count - 1;

                TISpeechMod.Speak(ReadContent(currentContentIndex), interrupt: true);
            }
        }

        /// <summary>
        /// Navigate to the next item at the current level.
        /// </summary>
        public void NavigateNext()
        {
            if (currentLevel == NavigationLevel.Topics)
            {
                if (cachedTopics == null || cachedTopics.Count == 0)
                    return;

                currentTopicIndex++;
                if (currentTopicIndex >= cachedTopics.Count)
                    currentTopicIndex = 0;

                TISpeechMod.Speak(ReadTopic(currentTopicIndex), interrupt: true);
            }
            else if (currentLevel == NavigationLevel.Content)
            {
                if (cachedContent == null || cachedContent.Count == 0)
                    return;

                currentContentIndex++;
                if (currentContentIndex >= cachedContent.Count)
                    currentContentIndex = 0;

                TISpeechMod.Speak(ReadContent(currentContentIndex), interrupt: true);
            }
        }

        /// <summary>
        /// Drill down from topics to content.
        /// </summary>
        public void DrillDown()
        {
            if (currentLevel == NavigationLevel.Topics)
            {
                if (cachedTopics == null || currentTopicIndex < 0 || currentTopicIndex >= cachedTopics.Count)
                    return;

                var topic = cachedTopics[currentTopicIndex];

                // Use game's selection method to populate content
                controller.SelectTopic(topic.template.dataName);

                // Switch to content level
                currentLevel = NavigationLevel.Content;
                currentContentIndex = 0;

                // Need to wait a frame for content to populate - use a coroutine-like approach
                // For now, refresh immediately and announce
                RefreshContentCache();

                int contentCount = cachedContent?.Count ?? 0;
                string topicTitle = TISpeechMod.CleanText(topic.topicTitle.text);

                if (contentCount > 0)
                {
                    TISpeechMod.Speak($"{topicTitle}. {contentCount} sections.", interrupt: true);
                    TISpeechMod.Speak(ReadContent(0), interrupt: false);
                }
                else
                {
                    TISpeechMod.Speak($"{topicTitle}. No content available.", interrupt: true);
                }

                MelonLogger.Msg($"CodexSubMode: Drilled into topic '{topic.template.dataName}', {contentCount} content items");
            }
        }

        /// <summary>
        /// Back out from content to topics.
        /// </summary>
        public void BackOut()
        {
            if (currentLevel == NavigationLevel.Content)
            {
                currentLevel = NavigationLevel.Topics;
                cachedContent = null;

                // Announce return to topics
                int topicCount = cachedTopics?.Count ?? 0;
                TISpeechMod.Speak($"Back to topics. {topicCount} topics.", interrupt: true);

                // Re-announce current topic
                if (topicCount > 0 && currentTopicIndex >= 0 && currentTopicIndex < topicCount)
                {
                    TISpeechMod.Speak(ReadTopic(currentTopicIndex), interrupt: false);
                }

                MelonLogger.Msg("CodexSubMode: Backed out to topics level");
            }
        }

        /// <summary>
        /// Re-read the current item in full.
        /// </summary>
        public void ReadDetail()
        {
            if (currentLevel == NavigationLevel.Topics)
            {
                TISpeechMod.Speak(ReadTopic(currentTopicIndex), interrupt: true);
            }
            else if (currentLevel == NavigationLevel.Content)
            {
                TISpeechMod.Speak(ReadContent(currentContentIndex), interrupt: true);
            }
        }

        /// <summary>
        /// List all items at the current level.
        /// </summary>
        public void ListAllItems()
        {
            var sb = new StringBuilder();

            if (currentLevel == NavigationLevel.Topics)
            {
                if (cachedTopics == null || cachedTopics.Count == 0)
                {
                    TISpeechMod.Speak("No topics", interrupt: true);
                    return;
                }

                sb.AppendLine($"{cachedTopics.Count} topics:");
                foreach (var topic in cachedTopics)
                {
                    string title = TISpeechMod.CleanText(topic.topicTitle.text);
                    string marker = topic.template.mainTopic ? " (main)" : "";
                    sb.AppendLine($"  {title}{marker}");
                }
            }
            else if (currentLevel == NavigationLevel.Content)
            {
                if (cachedContent == null || cachedContent.Count == 0)
                {
                    TISpeechMod.Speak("No content sections", interrupt: true);
                    return;
                }

                sb.AppendLine($"{cachedContent.Count} content sections");
            }

            TISpeechMod.Speak(sb.ToString(), interrupt: true);
        }

        /// <summary>
        /// Navigate to the first item starting with the given letter.
        /// </summary>
        public void NavigateByLetter(char letter)
        {
            letter = char.ToUpper(letter);

            if (currentLevel == NavigationLevel.Topics)
            {
                if (cachedTopics == null || cachedTopics.Count == 0)
                    return;

                // Search from current position + 1, wrapping around
                for (int i = 1; i <= cachedTopics.Count; i++)
                {
                    int index = (currentTopicIndex + i) % cachedTopics.Count;
                    var topic = cachedTopics[index];
                    string title = TISpeechMod.CleanText(topic.topicTitle.text).Trim();

                    if (!string.IsNullOrEmpty(title) && char.ToUpper(title[0]) == letter)
                    {
                        currentTopicIndex = index;
                        TISpeechMod.Speak(ReadTopic(currentTopicIndex), interrupt: true);
                        return;
                    }
                }

                TISpeechMod.Speak($"No topic starting with {letter}", interrupt: true);
            }
            else if (currentLevel == NavigationLevel.Content)
            {
                // Content doesn't have meaningful letter navigation
                TISpeechMod.Speak("Letter navigation not available for content", interrupt: true);
            }
        }

        /// <summary>
        /// Close the Codex and return to escape menu.
        /// </summary>
        public void CloseCodex()
        {
            try
            {
                CodexController.HideCodexPanel();
                TISpeechMod.Speak("Codex closed", interrupt: true);
                MelonLogger.Msg("CodexSubMode: Closed Codex");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error closing Codex: {ex.Message}");
            }
        }

        #endregion
    }
}
