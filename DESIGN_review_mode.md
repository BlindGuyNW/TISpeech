# Review Mode Design

## Problem Statement

The current hover-based accessibility approach has several issues:

1. **Discoverability** - Users don't know what elements exist on a screen unless they mouse over them
2. **Unity EventTrigger issues** - Raycast targets, click blocking (pitfall #8), initialization order
3. **TIUtilities initialization** - Many methods can't be patched due to static field references (pitfall #6)
4. **Mouse dependency** - Requires hunting with mouse to find and read content

## Solution: Review Mode

A keyboard-driven, data-centric approach that:
- Reads from game data (e.g., `TICouncilorState`) not UI text fields
- Uses section-based navigation
- Integrates with game's input blocking system
- Preserves tooltip functionality for detailed info

## Input Handling

### Game Integration

The game provides `TIInputManager.BlockKeybindings()` and `RestoreKeybindings()` for tutorials.
We use the same mechanism:

```csharp
// Enter review mode
TIInputManager.BlockKeybindings();  // Game stops processing keybindings
reviewModeActive = true;

// Exit review mode
TIInputManager.RestoreKeybindings();  // Game resumes normal input
reviewModeActive = false;
```

### Key Bindings

Using numpad keys (mostly unused by game - only +/- are bound):

| Key | Function |
|-----|----------|
| Numpad 0 | Toggle review mode on/off |
| Numpad 8 | Previous section |
| Numpad 2 | Next section |
| Numpad 4 | Previous item in section |
| Numpad 6 | Next item in section |
| Numpad 5 | Activate/click current item |
| Numpad / | List all sections on current screen |
| Numpad * | Read current section summary (all items) |
| Numpad . | Read details / trigger tooltip |
| Numpad Enter | Confirm/select |

## Section-Based Navigation

### Concept

Each screen type has defined sections. Users navigate between sections, then within sections.

```
Screen
├── Section 1 (e.g., "Info")
│   ├── Item: Name
│   ├── Item: Profession
│   └── Item: Location
├── Section 2 (e.g., "Stats")
│   ├── Item: Persuasion
│   ├── Item: Investigation
│   └── ...
└── Section 3 (e.g., "Actions")
    ├── Item: Go To button
    └── Item: Spend XP button
```

### Screen Detection

Use `GameControl.canvasStack.ActiveInfoScreen` to detect current screen:

```csharp
var activeScreen = GameControl.canvasStack.ActiveInfoScreen;
// Returns the active CanvasControllerBase (ResearchCanvasController, CouncilGridController, etc.)
```

## Council Screen Sections

### Grid View (main view with all councilors)

| Section | Key | Data Source | Content |
|---------|-----|-------------|---------|
| **Tabs** | T | Tab buttons | Council, Recruit, Ledger, Orgs, Calendar |
| **Councilors** | C | `CouncilorGridItemController[]` | List of councilor slots (1-8) |
| **Actions** | A | Buttons | Recruit button |

### Detail View (after selecting a councilor)

Data source: `CouncilGridController.currentCouncilor` (type: `TICouncilorState`)

| Section | Key | Data Source | Content |
|---------|-----|-------------|---------|
| **Info** | I | `currentCouncilor` | Name, profession, mission, location, home region, age |
| **Stats** | S | `currentCouncilor.GetAttribute()` | PER, INV, ESP, CMD, ADM, SCI, SEC, LOY |
| **Income** | N | `currentCouncilor.GetMonthlyIncome()` | Money, Influence, Ops, Research, Boost, MC, Projects |
| **XP** | X | `currentCouncilor.XP` | Experience points |
| **Status** | T | `currentCouncilor.detained`, `.IsAgent` | Detained/turned status |
| **Missions** | M | Mission button list | Available missions |
| **Traits** | R | `currentCouncilor.traits` | Trait list |
| **Orgs** | O | `currentCouncilor.orgs` | Assigned organizations |
| **Actions** | A | Buttons | Go To, Spend XP, Dismiss, Customize |

### Reading Data Example

```csharp
// Instead of reading from TMP_Text UI fields:
string value = controller.persuasion.text;  // BAD - UI dependent

// Read directly from game state:
var councilor = controller.currentCouncilor;
int persuasion = councilor.GetAttribute(CouncilorAttribute.Persuasion);  // GOOD - data direct
```

## Implementation Structure

### Core Classes

```
mod/
├── ReviewMode/
│   ├── ReviewModeController.cs    # Main controller, input handling, mode toggle
│   ├── ScreenReader.cs            # Screen detection, section management
│   ├── Sections/
│   │   ├── ISection.cs            # Interface for sections
│   │   ├── CouncilGridSections.cs # Sections for council grid view
│   │   ├── CouncilDetailSections.cs # Sections for council detail view
│   │   └── ...                    # Other screen sections
│   └── DataReaders/
│       ├── CouncilorDataReader.cs # Read TICouncilorState
│       └── ...                    # Other data readers
```

### ISection Interface

```csharp
public interface ISection
{
    string Name { get; }
    string ShortcutKey { get; }  // For direct access
    int ItemCount { get; }

    string ReadSummary();  // Read all items in section
    string ReadItem(int index);  // Read specific item
    bool CanActivate(int index);  // Is item interactive?
    void Activate(int index);  // Click/activate item
    bool HasTooltip(int index);  // Can show tooltip?
    void ShowTooltip(int index);  // Trigger tooltip display
}
```

### ReviewModeController

```csharp
public class ReviewModeController
{
    private bool isActive = false;
    private ScreenReader screenReader;
    private int currentSectionIndex = 0;
    private int currentItemIndex = 0;

    public void Update()
    {
        // Handle Numpad 0 toggle regardless of mode
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            ToggleReviewMode();
            return;
        }

        if (!isActive) return;

        // Handle navigation keys
        if (Input.GetKeyDown(KeyCode.Keypad8)) PreviousSection();
        if (Input.GetKeyDown(KeyCode.Keypad2)) NextSection();
        if (Input.GetKeyDown(KeyCode.Keypad4)) PreviousItem();
        if (Input.GetKeyDown(KeyCode.Keypad6)) NextItem();
        if (Input.GetKeyDown(KeyCode.Keypad5)) ActivateCurrent();
        // etc.
    }

    private void ToggleReviewMode()
    {
        if (isActive)
        {
            TIInputManager.RestoreKeybindings();
            isActive = false;
            TISpeechMod.Speak("Review mode off", interrupt: true);
        }
        else
        {
            TIInputManager.BlockKeybindings();
            isActive = true;
            screenReader.DetectCurrentScreen();
            TISpeechMod.Speak($"Review mode on. {screenReader.GetScreenSummary()}", interrupt: true);
        }
    }
}
```

## Tooltip Integration

Tooltips remain valuable for detailed info. When user presses Numpad Period:

1. Find the UI element associated with current item
2. If it has a `TooltipTrigger`, simulate pointer enter to show tooltip
3. Our existing `TooltipPatches` will vocalize the tooltip content

```csharp
void ShowTooltipForCurrentItem()
{
    var tooltipTrigger = currentSection.GetTooltipTrigger(currentItemIndex);
    if (tooltipTrigger != null)
    {
        // Simulate hover to trigger tooltip
        var eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(tooltipTrigger.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
    }
}
```

## Automatic Announcements

When screens change, automatically announce:
- Screen name
- Number of sections
- Key hints

```csharp
// When council detail view opens:
"Council detail view. John Smith, Spy.
 Sections: Info, Stats, Income, XP, Missions, Traits, Orgs, Actions.
 Press Numpad 8/2 to navigate sections, Numpad * to read current section."
```

## Migration Path

1. **Phase 1**: Implement core ReviewModeController with council screen
2. **Phase 2**: Add more screens (Research, Fleets, etc.)
3. **Phase 3**: Deprecate problematic EventTrigger hover patches
4. **Phase 4**: Keep only tooltip patches and review mode

## Comparison: Old vs New Approach

| Aspect | Old (Hover-based) | New (Review Mode) |
|--------|-------------------|-------------------|
| Discoverability | Must explore with mouse | Sections announced, can list |
| Data source | UI text fields | Game state objects |
| Click blocking | EventTrigger issues | No EventTriggers added |
| TIUtilities | Can't patch many methods | Read data post-initialization |
| Keyboard support | None | Full numpad navigation |
| Tooltips | Works | Still works (preserved) |

## Open Questions

1. Should review mode auto-activate when certain screens open?
2. How to handle screens with dynamic content (lists that scroll)?
3. Should there be a "quick read" that reads everything on screen?
4. How to indicate visually that review mode is active (for sighted helpers)?
