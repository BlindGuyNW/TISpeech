using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace TISpeech
{
    /// <summary>
    /// Keyboard-driven slot cursor navigation system for accessibility
    /// Allows navigating UI elements using the numeric keypad without mouse movement
    /// </summary>
    public class SlotCursor
    {
        // Cursor state
        private bool isActive = false;
        private int focusIndex = -1;
        private List<GameObject> currentElements = new List<GameObject>();
        private Canvas activeCanvas = null;

        // Key state tracking to prevent repeat firing
        private bool numpad0Pressed = false;
        private bool numpad2Pressed = false;
        private bool numpad8Pressed = false;
        private bool numpad5Pressed = false;
        private bool numpadEnterPressed = false;
        private bool numpadPeriodPressed = false;

        // Debounce for vocalization
        private string lastAnnouncement = "";
        private float lastAnnouncementTime = 0f;
        private const float ANNOUNCEMENT_DEBOUNCE_TIME = 0.2f;

        /// <summary>
        /// Update cursor state and handle input each frame
        /// Call this from MelonMod.OnUpdate()
        /// </summary>
        public void Update()
        {
            try
            {
                if (!TISpeechMod.IsReady)
                    return;

                HandleInput();

                // Refresh elements if cursor is active and canvas changed
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

            // Only process other inputs if cursor is active
            if (!isActive)
                return;

            // Numpad 2 - Move to next element (down)
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

            // Numpad 8 - Move to previous element (up)
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

        /// <summary>
        /// Toggle the slot cursor on or off
        /// </summary>
        private void ToggleCursor()
        {
            isActive = !isActive;

            if (isActive)
            {
                // Cursor activated - discover elements on active canvas
                RefreshElements();

                if (currentElements.Count > 0)
                {
                    focusIndex = 0;
                    TISpeechMod.Speak($"Slot cursor activated. {currentElements.Count} elements found.", interrupt: true);
                    AnnounceCurrent();
                }
                else
                {
                    TISpeechMod.Speak("Slot cursor activated, but no navigable elements found on this screen.", interrupt: true);
                    isActive = false; // Deactivate if nothing to navigate
                }
            }
            else
            {
                // Cursor deactivated
                TISpeechMod.Speak("Slot cursor deactivated", interrupt: true);
                focusIndex = -1;
                currentElements.Clear();
                activeCanvas = null;
            }
        }

        /// <summary>
        /// Refresh elements if the active canvas has changed
        /// </summary>
        private void RefreshIfNeeded()
        {
            Canvas current = GetActiveCanvas();

            if (current != activeCanvas)
            {
                MelonLogger.Msg($"Canvas changed, refreshing elements. Old: {activeCanvas?.name ?? "null"}, New: {current?.name ?? "null"}");
                RefreshElements();
            }
        }

        /// <summary>
        /// Refresh the list of navigable elements
        /// </summary>
        private void RefreshElements()
        {
            currentElements.Clear();
            activeCanvas = GetActiveCanvas();

            if (activeCanvas == null)
            {
                MelonLogger.Warning("No active canvas found");
                return;
            }

            MelonLogger.Msg($"Discovering elements on canvas: {activeCanvas.name}");

            // Discover navigable elements
            currentElements = DiscoverNavigableElements(activeCanvas);

            // Sort elements by position (top-to-bottom, left-to-right)
            currentElements = SortElementsByPosition(currentElements);

            MelonLogger.Msg($"Found {currentElements.Count} navigable elements");

            // Clamp focus index to valid range
            if (focusIndex >= currentElements.Count)
                focusIndex = currentElements.Count - 1;
            if (focusIndex < 0 && currentElements.Count > 0)
                focusIndex = 0;
        }

        /// <summary>
        /// Get the currently active canvas using the game's CanvasManager
        /// The game tracks which info screen (Research, Council, etc.) is active
        /// </summary>
        private Canvas GetActiveCanvas()
        {
            try
            {
                // Use the game's canvas management system to get the active info screen
                // GameControl is in the global namespace (not under PavonisInteractive.TerraInvicta)
                var canvasManager = GameControl.canvasStack;
                if (canvasManager == null)
                {
                    MelonLogger.Warning("CanvasManager not available");
                    return null;
                }

                var activeScreen = canvasManager.ActiveInfoScreen;
                if (activeScreen == null)
                {
                    // No logging - this is normal when in game world or main menu
                    return null;
                }

                // Get the canvas from the active screen
                Canvas canvas = activeScreen.Canvas;
                if (canvas == null)
                {
                    MelonLogger.Warning($"Active info screen has no Canvas component: {activeScreen.GetType().Name}");
                }

                return canvas;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting active canvas: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Discover all navigable elements on the given canvas, including sub-canvases
        /// </summary>
        private List<GameObject> DiscoverNavigableElements(Canvas rootCanvas)
        {
            var elements = new List<GameObject>();
            var seen = new HashSet<GameObject>();

            // Terra Invicta uses nested sub-canvases (e.g., primaryResearchPanel, councilGridCanvas)
            // We need to search the root canvas AND all child canvas components
            List<Canvas> canvasesToSearch = new List<Canvas>();
            canvasesToSearch.Add(rootCanvas);

            // Find all child Canvas components (sub-canvases for different panels)
            Canvas[] childCanvases = rootCanvas.GetComponentsInChildren<Canvas>(false);
            foreach (var childCanvas in childCanvases)
            {
                if (childCanvas != rootCanvas && childCanvas.enabled && childCanvas.gameObject.activeInHierarchy)
                {
                    canvasesToSearch.Add(childCanvas);
                    MelonLogger.Msg($"  Found sub-canvas: {childCanvas.name}, enabled: {childCanvas.enabled}");
                }
            }

            MelonLogger.Msg($"Searching {canvasesToSearch.Count} canvases total (1 root + {canvasesToSearch.Count - 1} sub-canvases)");

            // Search each canvas for navigable elements
            foreach (var canvas in canvasesToSearch)
            {
                // Find all EventTriggers we've added for accessibility
                foreach (var trigger in canvas.GetComponentsInChildren<EventTrigger>(false))
                {
                    if (IsElementVisible(trigger.gameObject) && !seen.Contains(trigger.gameObject))
                    {
                        elements.Add(trigger.gameObject);
                        seen.Add(trigger.gameObject);
                    }
                }

                // Find all native Unity UI buttons
                foreach (var button in canvas.GetComponentsInChildren<Button>(false))
                {
                    if (button.interactable && IsElementVisible(button.gameObject) && !seen.Contains(button.gameObject))
                    {
                        elements.Add(button.gameObject);
                        seen.Add(button.gameObject);
                    }
                }

                // Find all toggles
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

        /// <summary>
        /// Check if a UI element is actually visible using Unity's visibility system
        /// Checks: activeInHierarchy, parent Canvas enabled, and CanvasGroup alpha/interactable
        /// </summary>
        private bool IsElementVisible(GameObject element)
        {
            // Must be active in the hierarchy (element and all parents active)
            if (!element.activeInHierarchy)
                return false;

            // Check if any parent Canvas is disabled
            // Terra Invicta uses Canvas.enabled to hide entire panels
            Canvas parentCanvas = element.GetComponentInParent<Canvas>();
            while (parentCanvas != null)
            {
                if (!parentCanvas.enabled)
                    return false;

                // Move up to check parent canvases
                Transform parent = parentCanvas.transform.parent;
                parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            }

            // Check CanvasGroup (if present) - used for alpha-based hiding and interactability
            CanvasGroup canvasGroup = element.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null)
            {
                // Alpha of 0 means invisible
                if (canvasGroup.alpha <= 0f)
                    return false;

                // If not interactable, skip it (likely a disabled section)
                if (!canvasGroup.interactable)
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
                    // Use negative Y for top-to-bottom, then X for left-to-right
                    // Multiply Y by large factor to prioritize vertical position
                    return -worldPos.y * 10000 + worldPos.x;
                }
                return 0f;
            }).ToList();
        }

        /// <summary>
        /// Move focus to the next element
        /// </summary>
        private void MoveNext()
        {
            if (currentElements.Count == 0)
            {
                TISpeechMod.Speak("No elements to navigate", interrupt: true);
                return;
            }

            focusIndex = (focusIndex + 1) % currentElements.Count;
            AnnounceCurrent();
        }

        /// <summary>
        /// Move focus to the previous element
        /// </summary>
        private void MovePrevious()
        {
            if (currentElements.Count == 0)
            {
                TISpeechMod.Speak("No elements to navigate", interrupt: true);
                return;
            }

            focusIndex--;
            if (focusIndex < 0)
                focusIndex = currentElements.Count - 1;

            AnnounceCurrent();
        }

        /// <summary>
        /// Activate the currently focused element (simulate click)
        /// </summary>
        private void ActivateCurrent()
        {
            if (focusIndex < 0 || focusIndex >= currentElements.Count)
            {
                TISpeechMod.Speak("No element focused", interrupt: true);
                return;
            }

            GameObject current = currentElements[focusIndex];

            // Try to activate as a Button
            Button button = current.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                MelonLogger.Msg($"Activating button: {current.name}");
                TISpeechMod.Speak("Activated", interrupt: false);
                button.onClick.Invoke();
                return;
            }

            // Try to activate as a Toggle
            Toggle toggle = current.GetComponent<Toggle>();
            if (toggle != null && toggle.interactable)
            {
                MelonLogger.Msg($"Toggling: {current.name}");
                toggle.isOn = !toggle.isOn;
                TISpeechMod.Speak($"Toggled {(toggle.isOn ? "on" : "off")}", interrupt: false);
                return;
            }

            // For EventTrigger elements (like our text fields), simulate pointer click
            EventTrigger eventTrigger = current.GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                MelonLogger.Msg($"Simulating click on EventTrigger: {current.name}");
                SimulatePointerClick(current);
                TISpeechMod.Speak("Selected", interrupt: false);
                return;
            }

            TISpeechMod.Speak("Element cannot be activated", interrupt: false);
        }

        /// <summary>
        /// Simulate a pointer click event on an element
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

        /// <summary>
        /// Simulate a pointer enter (hover) event on an element
        /// This triggers EventTrigger handlers and tooltip display
        /// </summary>
        private void SimulatePointerEnter(GameObject target)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Vector2.zero
            };

            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerEnterHandler);
        }

        /// <summary>
        /// Announce the currently focused element by triggering hover events
        /// This triggers our EventTrigger handlers and game tooltips
        /// </summary>
        private void AnnounceCurrent()
        {
            if (focusIndex < 0 || focusIndex >= currentElements.Count)
                return;

            GameObject current = currentElements[focusIndex];

            // Debounce based on element identity, not text
            float currentTime = Time.unscaledTime;
            string elementId = current.GetInstanceID().ToString();
            if (elementId == lastAnnouncement && (currentTime - lastAnnouncementTime) < ANNOUNCEMENT_DEBOUNCE_TIME)
                return;

            lastAnnouncement = elementId;
            lastAnnouncementTime = currentTime;

            // Announce position first
            string position = $"{focusIndex + 1} of {currentElements.Count}";
            TISpeechMod.Speak(position, interrupt: true);

            // Trigger hover event - this will fire our EventTrigger handlers and show tooltips
            // Our existing patches (TooltipPatches, ResearchPriorityPatches, etc.) will handle announcements
            SimulatePointerEnter(current);

            MelonLogger.Msg($"Focused element {focusIndex + 1} of {currentElements.Count}: {current.name}");
        }

        /// <summary>
        /// Read detailed information about the current element
        /// </summary>
        private void ReadCurrentDetails()
        {
            if (focusIndex < 0 || focusIndex >= currentElements.Count)
            {
                TISpeechMod.Speak("No element focused", interrupt: true);
                return;
            }

            GameObject current = currentElements[focusIndex];
            string details = GetElementDetailsDescription(current);

            TISpeechMod.Speak(details, interrupt: true);
            MelonLogger.Msg($"Details: {details}");
        }

        /// <summary>
        /// Get a brief description of an element
        /// </summary>
        private string GetElementDescription(GameObject element)
        {
            // Try to get text from TMP_Text component on the element itself
            TMP_Text tmpText = element.GetComponent<TMP_Text>();
            if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text))
            {
                string text = TISpeechMod.CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            // Try to get text from Text component on the element itself
            Text uiText = element.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrWhiteSpace(uiText.text))
            {
                return TISpeechMod.CleanText(uiText.text);
            }

            // Check component type and get child text
            Button button = element.GetComponent<Button>();
            Toggle toggle = element.GetComponent<Toggle>();
            EventTrigger eventTrigger = element.GetComponent<EventTrigger>();

            // Try to get text from children (works for Buttons, Toggles, and EventTriggers)
            TMP_Text childTmpText = element.GetComponentInChildren<TMP_Text>();
            if (childTmpText != null && !string.IsNullOrWhiteSpace(childTmpText.text))
            {
                string text = TISpeechMod.CleanText(childTmpText.text);

                // Add type prefix if it's a button or toggle
                if (button != null)
                    return $"Button: {text}";
                else if (toggle != null)
                    return $"Toggle: {text}, {(toggle.isOn ? "on" : "off")}";
                else
                    return text;
            }

            // Try standard UI Text in children as fallback
            Text childUiText = element.GetComponentInChildren<Text>();
            if (childUiText != null && !string.IsNullOrWhiteSpace(childUiText.text))
            {
                string text = TISpeechMod.CleanText(childUiText.text);

                if (button != null)
                    return $"Button: {text}";
                else if (toggle != null)
                    return $"Toggle: {text}, {(toggle.isOn ? "on" : "off")}";
                else
                    return text;
            }

            // Type-specific fallbacks when no text found
            if (button != null)
                return "Button";
            if (toggle != null)
                return $"Toggle, {(toggle.isOn ? "on" : "off")}";

            // Final fallback to object name
            return element.name;
        }

        /// <summary>
        /// Get detailed description of an element (for Numpad Period)
        /// </summary>
        private string GetElementDetailsDescription(GameObject element)
        {
            var details = new System.Text.StringBuilder();

            // Element name
            details.Append($"Element: {element.name}. ");

            // Component types
            var components = new List<string>();
            if (element.GetComponent<Button>() != null) components.Add("Button");
            if (element.GetComponent<Toggle>() != null) components.Add("Toggle");
            if (element.GetComponent<EventTrigger>() != null) components.Add("EventTrigger");
            if (element.GetComponent<TMP_Text>() != null) components.Add("Text");

            if (components.Count > 0)
                details.Append($"Type: {string.Join(", ", components)}. ");

            // Position info
            RectTransform rectTransform = element.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
                details.Append($"Position: X {screenPos.x:F0}, Y {screenPos.y:F0}. ");
            }

            // Parent hierarchy (for context)
            if (element.transform.parent != null)
            {
                details.Append($"Parent: {element.transform.parent.name}. ");
            }

            return details.ToString();
        }
    }
}
