using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace TISpeech
{
    /// <summary>
    /// Keyboard-driven slot cursor navigation system for accessibility
    /// Supports hierarchical navigation with drill-down into containers
    ///
    /// Controls:
    /// - Numpad 0: Toggle cursor on/off
    /// - Numpad 8: Move to previous element/container
    /// - Numpad 2: Move to next element/container
    /// - Numpad 6 (or +): Drill down into container
    /// - Numpad 4 (or -): Go back up to container level
    /// - Numpad 5 or Numpad Enter: Activate current element
    /// - Numpad Period: Read current element details
    /// </summary>
    public class SlotCursor
    {
        // Navigation levels
        private const int LEVEL_CONTAINERS = 1;
        private const int LEVEL_CHILDREN = 2;

        // Cursor state
        private bool isActive = false;
        private int currentLevel = LEVEL_CONTAINERS;
        private Canvas activeCanvas = null;

        // Container-level navigation
        private List<GameObject> containers = new List<GameObject>();
        private int containerIndex = 0;

        // Child-level navigation (when drilled into a container)
        private List<GameObject> currentChildren = new List<GameObject>();
        private int childIndex = 0;

        // Flat element list (fallback when no containers found)
        private List<GameObject> flatElements = new List<GameObject>();
        private int flatIndex = 0;
        private bool usingFlatMode = false;

        // Key state tracking to prevent repeat firing
        private bool numpad0Pressed = false;
        private bool numpad2Pressed = false;
        private bool numpad8Pressed = false;
        private bool numpad4Pressed = false;
        private bool numpad6Pressed = false;
        private bool numpad5Pressed = false;
        private bool numpadEnterPressed = false;
        private bool numpadPeriodPressed = false;
        private bool numpadPlusPressed = false;
        private bool numpadMinusPressed = false;

        // Debounce for vocalization
        private string lastAnnouncement = "";
        private float lastAnnouncementTime = 0f;
        private const float ANNOUNCEMENT_DEBOUNCE_TIME = 0.2f;

        /// <summary>
        /// Update cursor state and handle input each frame
        /// </summary>
        public void Update()
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                HandleInput();

                if (isActive)
                {
                    RefreshIfNeeded();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SlotCursor.Update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle keyboard input for cursor navigation
        /// </summary>
        private void HandleInput()
        {
            // Numpad 0 - Toggle cursor on/off
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                if (!numpad0Pressed)
                {
                    numpad0Pressed = true;
                    ToggleCursor();
                }
            }
            else if (!Input.GetKey(KeyCode.Keypad0))
            {
                numpad0Pressed = false;
            }

            if (!isActive)
                return;

            // Numpad 2 - Move to next element
            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                if (!numpad2Pressed)
                {
                    numpad2Pressed = true;
                    MoveNext();
                }
            }
            else if (!Input.GetKey(KeyCode.Keypad2))
            {
                numpad2Pressed = false;
            }

            // Numpad 8 - Move to previous element
            if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                if (!numpad8Pressed)
                {
                    numpad8Pressed = true;
                    MovePrevious();
                }
            }
            else if (!Input.GetKey(KeyCode.Keypad8))
            {
                numpad8Pressed = false;
            }

            // Numpad 6 or Numpad Plus - Drill down into container
            bool drillDownPressed = Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.KeypadPlus);
            if (drillDownPressed)
            {
                if (!numpad6Pressed && !numpadPlusPressed)
                {
                    numpad6Pressed = true;
                    numpadPlusPressed = true;
                    DrillDown();
                }
            }
            else
            {
                if (!Input.GetKey(KeyCode.Keypad6)) numpad6Pressed = false;
                if (!Input.GetKey(KeyCode.KeypadPlus)) numpadPlusPressed = false;
            }

            // Numpad 4 or Numpad Minus - Go back up
            bool drillUpPressed = Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.KeypadMinus);
            if (drillUpPressed)
            {
                if (!numpad4Pressed && !numpadMinusPressed)
                {
                    numpad4Pressed = true;
                    numpadMinusPressed = true;
                    DrillUp();
                }
            }
            else
            {
                if (!Input.GetKey(KeyCode.Keypad4)) numpad4Pressed = false;
                if (!Input.GetKey(KeyCode.KeypadMinus)) numpadMinusPressed = false;
            }

            // Numpad 5 - Activate current element
            if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                if (!numpad5Pressed)
                {
                    numpad5Pressed = true;
                    ActivateCurrent();
                }
            }
            else if (!Input.GetKey(KeyCode.Keypad5))
            {
                numpad5Pressed = false;
            }

            // Numpad Enter - Activate current element (alternative)
            if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!numpadEnterPressed)
                {
                    numpadEnterPressed = true;
                    ActivateCurrent();
                }
            }
            else if (!Input.GetKey(KeyCode.KeypadEnter))
            {
                numpadEnterPressed = false;
            }

            // Numpad Period - Read current element details
            if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                if (!numpadPeriodPressed)
                {
                    numpadPeriodPressed = true;
                    ReadCurrentDetails();
                }
            }
            else if (!Input.GetKey(KeyCode.KeypadPeriod))
            {
                numpadPeriodPressed = false;
            }
        }

        #region Cursor Toggle and Refresh

        /// <summary>
        /// Toggle the slot cursor on or off
        /// </summary>
        private void ToggleCursor()
        {
            isActive = !isActive;

            if (isActive)
            {
                RefreshElements();

                if (usingFlatMode)
                {
                    if (flatElements.Count > 0)
                    {
                        flatIndex = 0;
                        TISpeechMod.Speak($"Slot cursor activated. {flatElements.Count} elements found.", interrupt: true);
                        AnnounceCurrent();
                    }
                    else
                    {
                        TISpeechMod.Speak("Slot cursor activated, but no navigable elements found.", interrupt: true);
                        isActive = false;
                    }
                }
                else
                {
                    if (containers.Count > 0)
                    {
                        containerIndex = 0;
                        currentLevel = LEVEL_CONTAINERS;
                        TISpeechMod.Speak($"Slot cursor activated. {containers.Count} containers found. Use numpad 6 to drill down.", interrupt: true);
                        AnnounceCurrent();
                    }
                    else
                    {
                        TISpeechMod.Speak("Slot cursor activated, but no containers found.", interrupt: true);
                        isActive = false;
                    }
                }
            }
            else
            {
                TISpeechMod.Speak("Slot cursor deactivated", interrupt: true);
                ResetState();
            }
        }

        /// <summary>
        /// Reset all navigation state
        /// </summary>
        private void ResetState()
        {
            containerIndex = 0;
            childIndex = 0;
            flatIndex = 0;
            currentLevel = LEVEL_CONTAINERS;
            containers.Clear();
            currentChildren.Clear();
            flatElements.Clear();
            activeCanvas = null;
            usingFlatMode = false;
        }

        /// <summary>
        /// Refresh elements if the active canvas has changed
        /// </summary>
        private void RefreshIfNeeded()
        {
            Canvas current = GetActiveCanvas();
            if (current != activeCanvas)
            {
                MelonLogger.Msg($"Canvas changed, refreshing. Old: {activeCanvas?.name ?? "null"}, New: {current?.name ?? "null"}");
                RefreshElements();
            }
        }

        /// <summary>
        /// Refresh containers and elements for the current screen
        /// </summary>
        private void RefreshElements()
        {
            ResetState();
            activeCanvas = GetActiveCanvas();

            if (activeCanvas == null)
            {
                MelonLogger.Warning("No active canvas found");
                return;
            }

            MelonLogger.Msg($"Discovering elements on canvas: {activeCanvas.name}");

            // Try to discover containers first
            containers = DiscoverContainers(activeCanvas);
            containers = SortElementsByPosition(containers);

            if (containers.Count > 0)
            {
                usingFlatMode = false;
                MelonLogger.Msg($"Found {containers.Count} containers (hierarchical mode)");
            }
            else
            {
                // Fallback to flat mode
                usingFlatMode = true;
                flatElements = DiscoverAllElements(activeCanvas);
                flatElements = SortElementsByPosition(flatElements);
                MelonLogger.Msg($"No containers found, using flat mode with {flatElements.Count} elements");
            }
        }

        #endregion

        #region Container Discovery

        /// <summary>
        /// Discover containers on the current screen
        /// </summary>
        private List<GameObject> DiscoverContainers(Canvas rootCanvas)
        {
            var result = new List<GameObject>();

            // Check for Council screen containers (CouncilorGridItemController)
            var councilorItems = rootCanvas.GetComponentsInChildren<CouncilorGridItemController>(false);
            foreach (var item in councilorItems)
            {
                if (item != null && item.gameObject.activeInHierarchy && IsElementVisible(item.gameObject))
                {
                    result.Add(item.gameObject);
                    MelonLogger.Msg($"  Found councilor container: {item.councilorName?.text ?? item.gameObject.name}");
                }
            }

            // TODO: Add other container types here
            // - ResearchSlotController for Research screen
            // - IntelFactionGridItemController for Intel screen
            // - etc.

            return result;
        }

        /// <summary>
        /// Discover children within a container
        /// </summary>
        private List<GameObject> DiscoverContainerChildren(GameObject container)
        {
            var children = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            // Find EventTriggers within this container
            foreach (var trigger in container.GetComponentsInChildren<EventTrigger>(false))
            {
                if (IsElementVisible(trigger.gameObject) && !seen.Contains(trigger.gameObject))
                {
                    children.Add(trigger.gameObject);
                    seen.Add(trigger.gameObject);
                }
            }

            // Find Buttons within this container
            foreach (var button in container.GetComponentsInChildren<Button>(false))
            {
                if (button.interactable && IsElementVisible(button.gameObject) && !seen.Contains(button.gameObject))
                {
                    children.Add(button.gameObject);
                    seen.Add(button.gameObject);
                }
            }

            // Find Toggles within this container
            foreach (var toggle in container.GetComponentsInChildren<Toggle>(false))
            {
                if (toggle.interactable && IsElementVisible(toggle.gameObject) && !seen.Contains(toggle.gameObject))
                {
                    children.Add(toggle.gameObject);
                    seen.Add(toggle.gameObject);
                }
            }

            return SortElementsByPosition(children);
        }

        /// <summary>
        /// Discover all navigable elements (flat mode fallback)
        /// </summary>
        private List<GameObject> DiscoverAllElements(Canvas rootCanvas)
        {
            var elements = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            List<Canvas> canvasesToSearch = new List<Canvas> { rootCanvas };
            Canvas[] childCanvases = rootCanvas.GetComponentsInChildren<Canvas>(false);
            foreach (var childCanvas in childCanvases)
            {
                if (childCanvas != rootCanvas && childCanvas.enabled && childCanvas.gameObject.activeInHierarchy)
                {
                    canvasesToSearch.Add(childCanvas);
                }
            }

            foreach (var canvas in canvasesToSearch)
            {
                foreach (var trigger in canvas.GetComponentsInChildren<EventTrigger>(false))
                {
                    if (IsElementVisible(trigger.gameObject) && !seen.Contains(trigger.gameObject))
                    {
                        elements.Add(trigger.gameObject);
                        seen.Add(trigger.gameObject);
                    }
                }

                foreach (var button in canvas.GetComponentsInChildren<Button>(false))
                {
                    if (button.interactable && IsElementVisible(button.gameObject) && !seen.Contains(button.gameObject))
                    {
                        elements.Add(button.gameObject);
                        seen.Add(button.gameObject);
                    }
                }

                foreach (var toggle in canvas.GetComponentsInChildren<Toggle>(false))
                {
                    if (toggle.interactable && IsElementVisible(toggle.gameObject) && !seen.Contains(toggle.gameObject))
                    {
                        elements.Add(toggle.gameObject);
                        seen.Add(toggle.gameObject);
                    }
                }
            }

            return elements;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Move to next element (behavior depends on current level)
        /// </summary>
        private void MoveNext()
        {
            if (usingFlatMode)
            {
                MoveFlatNext();
                return;
            }

            if (currentLevel == LEVEL_CONTAINERS)
            {
                MoveNextContainer();
            }
            else // LEVEL_CHILDREN
            {
                MoveNextChild();
            }
        }

        /// <summary>
        /// Move to previous element (behavior depends on current level)
        /// </summary>
        private void MovePrevious()
        {
            if (usingFlatMode)
            {
                MoveFlatPrevious();
                return;
            }

            if (currentLevel == LEVEL_CONTAINERS)
            {
                MovePreviousContainer();
            }
            else // LEVEL_CHILDREN
            {
                MovePreviousChild();
            }
        }

        /// <summary>
        /// Move to next container
        /// </summary>
        private void MoveNextContainer()
        {
            if (containers.Count == 0) return;
            containerIndex = (containerIndex + 1) % containers.Count;
            AnnounceCurrent();
        }

        /// <summary>
        /// Move to previous container
        /// </summary>
        private void MovePreviousContainer()
        {
            if (containers.Count == 0) return;
            containerIndex--;
            if (containerIndex < 0) containerIndex = containers.Count - 1;
            AnnounceCurrent();
        }

        /// <summary>
        /// Move to next child, auto-advancing to next container at boundary
        /// </summary>
        private void MoveNextChild()
        {
            if (currentChildren.Count == 0) return;

            childIndex++;
            if (childIndex >= currentChildren.Count)
            {
                // Auto-advance to next container
                containerIndex++;
                if (containerIndex >= containers.Count)
                {
                    containerIndex = 0;
                }

                // Drill into the new container
                currentChildren = DiscoverContainerChildren(containers[containerIndex]);
                childIndex = 0;

                if (currentChildren.Count == 0)
                {
                    // No children in this container, announce container instead
                    currentLevel = LEVEL_CONTAINERS;
                    TISpeechMod.Speak("No elements in this container", interrupt: true);
                    AnnounceCurrent();
                    return;
                }

                // Announce we moved to a new container
                string containerName = GetContainerName(containers[containerIndex]);
                TISpeechMod.Speak($"Moved to {containerName}", interrupt: true);
            }

            AnnounceCurrent();
        }

        /// <summary>
        /// Move to previous child, auto-advancing to previous container at boundary
        /// </summary>
        private void MovePreviousChild()
        {
            if (currentChildren.Count == 0) return;

            childIndex--;
            if (childIndex < 0)
            {
                // Auto-advance to previous container
                containerIndex--;
                if (containerIndex < 0)
                {
                    containerIndex = containers.Count - 1;
                }

                // Drill into the new container
                currentChildren = DiscoverContainerChildren(containers[containerIndex]);
                childIndex = currentChildren.Count - 1;

                if (currentChildren.Count == 0 || childIndex < 0)
                {
                    childIndex = 0;
                    currentLevel = LEVEL_CONTAINERS;
                    TISpeechMod.Speak("No elements in this container", interrupt: true);
                    AnnounceCurrent();
                    return;
                }

                // Announce we moved to a new container
                string containerName = GetContainerName(containers[containerIndex]);
                TISpeechMod.Speak($"Moved to {containerName}", interrupt: true);
            }

            AnnounceCurrent();
        }

        /// <summary>
        /// Flat mode: move to next element
        /// </summary>
        private void MoveFlatNext()
        {
            if (flatElements.Count == 0) return;
            flatIndex = (flatIndex + 1) % flatElements.Count;
            AnnounceCurrent();
        }

        /// <summary>
        /// Flat mode: move to previous element
        /// </summary>
        private void MoveFlatPrevious()
        {
            if (flatElements.Count == 0) return;
            flatIndex--;
            if (flatIndex < 0) flatIndex = flatElements.Count - 1;
            AnnounceCurrent();
        }

        /// <summary>
        /// Drill down into the current container
        /// </summary>
        private void DrillDown()
        {
            if (usingFlatMode)
            {
                TISpeechMod.Speak("Flat mode, cannot drill down", interrupt: true);
                return;
            }

            if (currentLevel == LEVEL_CHILDREN)
            {
                TISpeechMod.Speak("Already at detail level", interrupt: true);
                return;
            }

            if (containers.Count == 0 || containerIndex >= containers.Count)
            {
                TISpeechMod.Speak("No container to drill into", interrupt: true);
                return;
            }

            // Discover children in current container
            currentChildren = DiscoverContainerChildren(containers[containerIndex]);

            if (currentChildren.Count == 0)
            {
                TISpeechMod.Speak("No elements in this container", interrupt: true);
                return;
            }

            currentLevel = LEVEL_CHILDREN;
            childIndex = 0;

            string containerName = GetContainerName(containers[containerIndex]);
            TISpeechMod.Speak($"Entering {containerName}, {currentChildren.Count} elements", interrupt: true);
            AnnounceCurrent();
        }

        /// <summary>
        /// Go back up to container level
        /// </summary>
        private void DrillUp()
        {
            if (usingFlatMode)
            {
                TISpeechMod.Speak("Flat mode, cannot go up", interrupt: true);
                return;
            }

            if (currentLevel == LEVEL_CONTAINERS)
            {
                TISpeechMod.Speak("Already at container level", interrupt: true);
                return;
            }

            currentLevel = LEVEL_CONTAINERS;
            currentChildren.Clear();
            childIndex = 0;

            TISpeechMod.Speak("Back to container level", interrupt: true);
            AnnounceCurrent();
        }

        #endregion

        #region Activation

        /// <summary>
        /// Activate the currently focused element
        /// </summary>
        private void ActivateCurrent()
        {
            GameObject current = GetCurrentElement();
            if (current == null)
            {
                TISpeechMod.Speak("No element focused", interrupt: true);
                return;
            }

            // Try Button
            Button button = current.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                MelonLogger.Msg($"Activating button: {current.name}");
                TISpeechMod.Speak("Activated", interrupt: false);
                button.onClick.Invoke();
                return;
            }

            // Try Toggle
            Toggle toggle = current.GetComponent<Toggle>();
            if (toggle != null && toggle.interactable)
            {
                MelonLogger.Msg($"Toggling: {current.name}");
                toggle.isOn = !toggle.isOn;
                TISpeechMod.Speak($"Toggled {(toggle.isOn ? "on" : "off")}", interrupt: false);
                return;
            }

            // Try EventTrigger (simulate click)
            EventTrigger eventTrigger = current.GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                MelonLogger.Msg($"Simulating click on: {current.name}");
                SimulatePointerClick(current);
                TISpeechMod.Speak("Selected", interrupt: false);
                return;
            }

            TISpeechMod.Speak("Element cannot be activated", interrupt: false);
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announce the currently focused element
        /// </summary>
        private void AnnounceCurrent()
        {
            GameObject current = GetCurrentElement();
            if (current == null) return;

            // Debounce
            float currentTime = Time.unscaledTime;
            string elementId = current.GetInstanceID().ToString();
            if (elementId == lastAnnouncement && (currentTime - lastAnnouncementTime) < ANNOUNCEMENT_DEBOUNCE_TIME)
                return;

            lastAnnouncement = elementId;
            lastAnnouncementTime = currentTime;

            // Build position announcement
            string position;
            if (usingFlatMode)
            {
                position = $"{flatIndex + 1} of {flatElements.Count}";
            }
            else if (currentLevel == LEVEL_CONTAINERS)
            {
                string containerName = GetContainerName(current);
                position = $"{containerIndex + 1} of {containers.Count}: {containerName}";
            }
            else
            {
                position = $"{childIndex + 1} of {currentChildren.Count}";
            }

            TISpeechMod.Speak(position, interrupt: true);

            // Trigger hover event to fire our existing accessibility patches
            SimulatePointerEnter(current);

            MelonLogger.Msg($"Focused: {current.name} ({position})");
        }

        /// <summary>
        /// Read detailed information about current element
        /// </summary>
        private void ReadCurrentDetails()
        {
            GameObject current = GetCurrentElement();
            if (current == null)
            {
                TISpeechMod.Speak("No element focused", interrupt: true);
                return;
            }

            string details = GetElementDetails(current);
            TISpeechMod.Speak(details, interrupt: true);
        }

        /// <summary>
        /// Get the currently focused element based on mode and level
        /// </summary>
        private GameObject GetCurrentElement()
        {
            if (usingFlatMode)
            {
                if (flatIndex >= 0 && flatIndex < flatElements.Count)
                    return flatElements[flatIndex];
            }
            else if (currentLevel == LEVEL_CONTAINERS)
            {
                if (containerIndex >= 0 && containerIndex < containers.Count)
                    return containers[containerIndex];
            }
            else // LEVEL_CHILDREN
            {
                if (childIndex >= 0 && childIndex < currentChildren.Count)
                    return currentChildren[childIndex];
            }

            return null;
        }

        /// <summary>
        /// Get a descriptive name for a container
        /// </summary>
        private string GetContainerName(GameObject container)
        {
            // Check for CouncilorGridItemController
            var councilor = container.GetComponent<CouncilorGridItemController>();
            if (councilor != null && councilor.councilorName != null)
            {
                string name = TISpeechMod.CleanText(councilor.councilorName.text);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            // TODO: Add other container types here

            // Fallback to GameObject name
            return container.name;
        }

        /// <summary>
        /// Get detailed description of an element
        /// </summary>
        private string GetElementDetails(GameObject element)
        {
            var details = new System.Text.StringBuilder();
            details.Append($"Element: {element.name}. ");

            var components = new List<string>();
            if (element.GetComponent<Button>() != null) components.Add("Button");
            if (element.GetComponent<Toggle>() != null) components.Add("Toggle");
            if (element.GetComponent<EventTrigger>() != null) components.Add("EventTrigger");
            if (element.GetComponent<TMP_Text>() != null) components.Add("Text");

            if (components.Count > 0)
                details.Append($"Type: {string.Join(", ", components)}. ");

            if (element.transform.parent != null)
                details.Append($"Parent: {element.transform.parent.name}. ");

            return details.ToString();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get the active canvas using game's canvas management
        /// </summary>
        private Canvas GetActiveCanvas()
        {
            try
            {
                var canvasManager = GameControl.canvasStack;
                if (canvasManager == null) return null;

                var activeScreen = canvasManager.ActiveInfoScreen;
                if (activeScreen == null) return null;

                return activeScreen.Canvas;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting active canvas: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a UI element is visible
        /// </summary>
        private bool IsElementVisible(GameObject element)
        {
            if (!element.activeInHierarchy)
                return false;

            Canvas parentCanvas = element.GetComponentInParent<Canvas>();
            while (parentCanvas != null)
            {
                if (!parentCanvas.enabled)
                    return false;
                Transform parent = parentCanvas.transform.parent;
                parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            }

            CanvasGroup canvasGroup = element.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (canvasGroup.alpha <= 0f || !canvasGroup.interactable)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Sort elements by screen position (top-to-bottom, left-to-right)
        /// </summary>
        private List<GameObject> SortElementsByPosition(List<GameObject> elements)
        {
            return elements.OrderBy(e =>
            {
                var rectTransform = e.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    Vector3 worldPos = rectTransform.position;
                    return -worldPos.y * 10000 + worldPos.x;
                }
                return 0f;
            }).ToList();
        }

        /// <summary>
        /// Simulate pointer enter event
        /// </summary>
        private void SimulatePointerEnter(GameObject target)
        {
            var eventData = new PointerEventData(EventSystem.current) { position = Vector2.zero };
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerEnterHandler);
        }

        /// <summary>
        /// Simulate pointer click event
        /// </summary>
        private void SimulatePointerClick(GameObject target)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Vector2.zero,
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
        }

        #endregion
    }
}
