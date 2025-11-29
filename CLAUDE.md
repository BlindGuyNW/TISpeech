# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TISpeechMod is a MelonLoader mod for Terra Invicta that provides screen reader accessibility by announcing tooltips and UI elements using the Tolk library. The mod uses Harmony patches to intercept UI events and speak content through NVDA, JAWS, or SAPI.

## Build and Deployment

**Primary build command:**
```bash
./build_and_deploy.bat
```

This script:
- Cleans previous build artifacts
- Builds the project with `dotnet build TISpeechMod.csproj --configuration Release`
- Copies `TISpeechMod.dll` to `C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\Mods\`
- Deploys Tolk.dll and NVDA controller DLLs to the game directory

**Manual build only:**
```bash
dotnet build TISpeechMod.csproj --configuration Release
```

Output: `bin/Release/net472/TISpeechMod.dll`

## Architecture

### Directory Structure

- **`mod/`** - Mod source code (our code)
  - `TISpeechMod.cs` - MelonLoader entry point, Tolk initialization, static helper methods
  - `AccessibilityCommands.cs` - Global keyboard hotkeys (Alt+R for resources, etc.)
  - `Patches/` - Harmony patches for UI accessibility
    - `TooltipPatches.cs` - ModelShark tooltip system
    - `UIControlPatches.cs` - Buttons and confirmation dialogs
    - `CouncilorPatches.cs`, `ResearchPriorityPatches.cs`, etc. - Screen-specific patches
  - `ReviewMode/` - Keyboard-only navigation system
    - `ReviewModeController.cs` - Main controller (MonoBehaviour)
    - `NavigationState.cs` - Hierarchical navigation state machine
    - `ConfirmationHelper.cs` - Confirmation dialogs for actions
    - `Screens/` - Screen implementations (CouncilScreen, TechnologyScreen, etc.)
    - `Sections/` - Section interface and DataSection implementation
    - `Readers/` - Game state to text converters (CouncilorReader, NationReader, etc.)
- **`decompiled/`** - Decompiled Terra Invicta game code (read-only reference)
  - Used to understand game internals but NOT compiled into the mod
- **`Tolk.cs`** - C# wrapper for Tolk screen reader library
- **`TISpeechMod.csproj`** - Project file with critical setting `EnableDefaultCompileItems=false` to prevent compiling decompiled code

### MelonLoader Mod Structure

The mod uses MelonLoader's attribute-based registration:
```csharp
[assembly: MelonInfo(typeof(TISpeech.TISpeechMod), "TI Speech", "1.0.0", "TISpeech")]
[assembly: MelonGame("Pavonis Interactive", "TerraInvicta")]
```

These attributes are **required** in `mod/TISpeechMod.cs` for MelonLoader to recognize the DLL as a valid mod.

### Harmony Patching Pattern

Patches use HarmonyLib to intercept game methods:
```csharp
[HarmonyPatch(typeof(TooltipTrigger), "OnPointerEnter")]
[HarmonyPostfix]
public static void OnPointerEnter_Postfix(TooltipTrigger __instance, PointerEventData eventData)
```

**Critical implementation details:**
- Use `AccessTools.Property()` and `AccessTools.Field()` for reflection on game types
- Extract tooltip text from `TooltipTrigger.Tooltip.TextFields[].Text.text` (not from GameObject names)
- The `Tooltip` object contains a list of `TextField` objects, each with a `TMP_Text.text` property containing actual tooltip content
- Always use Postfix patches to avoid breaking game functionality

### ModelShark Tooltip System (Game Architecture)

The game uses the ModelShark tooltip library:
- **`TooltipTrigger`** - Component on UI GameObjects, implements Unity event interfaces (IPointerEnterHandler, etc.)
- **`TooltipManager`** - Singleton managing tooltip display, has method `Show(TooltipTrigger trigger)`
- **`Tooltip`** - Contains `TextFields` list with actual tooltip content
- **`TextField`** - Wrapper around `TMP_Text` components

Text extraction flow:
1. Get `TooltipTrigger.Tooltip` property
2. Get `Tooltip.TextFields` list
3. For each TextField, get `TextField.Text.text` (TMP_Text content)
4. Clean HTML/TextMeshPro tags with regex

### Slot-Based UI Pattern (Game Architecture)

Terra Invicta uses a **slot-based UI pattern** extensively throughout the game. This pattern consists of panels with fixed "slots" that display dynamic data via non-interactive text fields.

#### Pattern Structure

Each slot-based controller follows this structure:

```csharp
public class XYZListItemController : MonoBehaviour
{
    // Many TMP_Text fields for displaying data
    public TMP_Text field1;
    public TMP_Text field2;
    public TMP_Text field3;
    // ... etc

    // Update method that populates the text fields
    public void UpdateListItem(SomeDataType data)
    {
        field1.text = data.something;
        field2.text = data.somethingElse;
        // ... populate all fields
    }
}
```

#### Common Examples

**Controllers that follow this pattern:**
- `ResearchPanelController` - Research screen (6 slots: 3 tech + 3 project)
- `CouncilorGridItemController` - Council screen (8 councilor slots)
- `FinderListItemController` - Finder/search results list
- `ArmyListItemController` - Army management lists
- `FleetsSceenFleetListItemController` - Fleet management
- `DockedShipListItemController` - Docked ships display
- `CalendarItemListItemController` - Calendar events
- `LedgerListItemController` - Financial ledgers
- `PriorityListItemController` - National priorities grid
- `ClaimListItemController` - Space claims
- And 30+ more...

#### The Accessibility Problem

**Problem:** These text fields are purely visual displays - they have no interactive components and don't respond to pointer events. Screen readers cannot access them.

**Manifestation:** When hovering over councilor names, stats, research progress, fleet information, etc., nothing is announced because they're just `TMP_Text` components with no `EventTrigger` or interactive behavior.

#### The Solution Pattern

To make these accessible, we patch the update method and add `EventTrigger` components to text fields:

```csharp
[HarmonyPatch(typeof(XYZListItemController), "UpdateListItem")]
[HarmonyPostfix]
public static void UpdateListItem_Postfix(XYZListItemController __instance)
{
    if (!TISpeechMod.IsReady)
        return;

    AddHoverHandlers(__instance);
}

private static void AddHoverHandlers(XYZListItemController controller)
{
    // Add EventTrigger to each important text field
    AddTextHoverHandler(controller.field1, "Field 1 Label");
    AddTextHoverHandler(controller.field2, "Field 2 Label");
    // etc...
}

private static void AddTextHoverHandler(TMP_Text textField, string label)
{
    EventTrigger trigger = textField.gameObject.GetComponent<EventTrigger>();
    if (trigger == null)
        trigger = textField.gameObject.AddComponent<EventTrigger>();
    else
        trigger.triggers.Clear();

    EventTrigger.Entry entry = new EventTrigger.Entry();
    entry.eventID = EventTriggerType.PointerEnter;
    entry.callback.AddListener((data) => OnTextHover(textField, label));
    trigger.triggers.Add(entry);
}

private static void OnTextHover(TMP_Text textField, string label)
{
    string value = TISpeechMod.CleanText(textField.text);
    TISpeechMod.Speak($"{label}: {value}", interrupt: false);
}
```

#### Implementation Examples

**See these patch files for working implementations:**
- `ResearchPriorityPatches.cs` - Patches `ResearchPanelController.OnEnable` (not UpdatePanel, see pitfall #6)
- `LedgerPatches.cs` - Patches multiple `LedgerListItemController.SetListItem` overloads
- `PriorityPatches.cs` - Patches `PriorityListItemController.SetListItem`

#### Method Selection: Which Method to Patch

Different controllers use different update method names:
- `UpdateListItem` - Most common
- `SetListItem` - Used by ledgers, priorities
- `UpdatePanel` - Used by panel controllers (⚠️ see pitfall #6)
- `OnEnable` - Safe alternative to UpdatePanel

**How to find the right method:**
1. Look for methods that populate the text fields
2. Check if they're called when data changes
3. Verify they don't contain problematic static field references (like `TIUtilities`)

#### Potential Approaches

**Option A: Manual patches per controller**
- Create dedicated patch class for each controller type
- Pro: Full control over what's announced and labeling
- Con: Tedious, code duplication
- Status: Current approach

**Option B: Generic auto-discovery system**
- Create a utility that automatically finds all `TMP_Text` fields on any `MonoBehaviour`
- Patch common update patterns generically
- Pro: Less code, works across many controllers
- Con: Less control, may announce too much or need filtering
- Status: Not yet implemented, future consideration

**Option C: Prioritize high-value screens**
- Focus on frequently-used screens (Council, Research, Fleets, Armies)
- Use manual approach for these critical screens
- Pro: Good ROI, manageable scope
- Con: Leaves some screens inaccessible
- Status: Current strategy

### Confirmation Dialog Pattern (Game Architecture)

Terra Invicta has many confirmation dialogs (recruitment, operations, missions, notifications) that appear via `GameObject.SetActive(true)` instead of `CanvasControllerBase.Show()`. These dialogs contain buttons that need accessibility support.

#### The Problem

**Symptom:** Buttons in confirmation dialogs (Confirm, Decline, OK, Cancel, etc.) don't announce when hovered.

**Root Cause:**
- Most UI elements are shown via `CanvasControllerBase.Show()`, which triggers our patches
- Confirmation dialogs use `GameObject.SetActive(true)` directly, bypassing our patches
- The game has **922 SetActive calls across 126 files** - patching Unity core methods (GameObject.SetActive, Canvas.OnEnable) is risky and has severe performance implications

**Examples of affected dialogs:**
- Recruitment confirmation (Confirm/Decline)
- Operation confirmations
- Mission confirmations
- Notification alerts (OK/Go To)
- Diplomacy agreements
- Policy confirmations

#### The Solution Pattern

**Key insight:** All major screen controllers extend `CanvasControllerBase` and call `Initialize()` during setup. This is where they create confirmation dialog GameObjects.

**Pattern:** Patch the `Initialize()` method of controllers that have confirmation dialogs, and add EventTriggers to buttons at initialization time.

```csharp
[HarmonyPatch(typeof(SomeController), "Initialize")]
[HarmonyPostfix]
public static void SomeController_Initialize_Postfix(SomeController __instance)
{
    try
    {
        if (!TISpeechMod.IsReady || __instance == null)
            return;

        // Add handlers to buttons in the confirmation dialog GameObject
        if (__instance.confirmDialogBox != null)
        {
            AddGenericButtonHandlers(__instance.confirmDialogBox);
            MelonLogger.Msg("Added button handlers to SomeController confirmation dialogs");
        }
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"Error in SomeController.Initialize patch: {ex.Message}");
    }
}
```

#### Implementation Examples

**See `UIControlPatches.cs` for working implementations:**

1. **CouncilGridController** - Recruitment confirmations
   ```csharp
   [HarmonyPatch(typeof(CouncilGridController), "Initialize")]
   // Adds handlers to confirmRecruitBox (Confirm/Decline buttons)
   ```

2. **OperationCanvasController** - Operation confirmations
   ```csharp
   [HarmonyPatch(typeof(OperationCanvasController), "Initialize")]
   // Adds handlers to confirmPanel
   ```

3. **NotificationScreenController** - Alert dialogs
   ```csharp
   [HarmonyPatch(typeof(NotificationScreenController), "Initialize")]
   // Adds handlers to singleAlertBox (OK/Go To buttons)
   ```

4. **CouncilorMissionCanvasController** - Mission confirmations
   ```csharp
   [HarmonyPatch(typeof(CouncilorMissionCanvasController), "Initialize")]
   // Adds handlers to mission confirmation dialogs
   ```

#### How to Add New Confirmation Dialog Patches

**Step 1: Identify the controller**
- Find which screen/canvas has the confirmation dialog
- Look for controllers that extend `CanvasControllerBase`
- Common examples: `*ScreenController`, `*CanvasController`, `*Controller`

**Step 2: Find the confirmation dialog GameObject**
- Search the decompiled controller for `public GameObject confirm*` or `public GameObject *Box`
- Look for fields like `confirmPanel`, `confirmDialog`, `confirmationBox`, etc.
- Check the controller's `Initialize()` method to see which GameObjects are set up

**Step 3: Create the patch**
- Patch the controller's `Initialize()` method with `[HarmonyPostfix]`
- In the postfix, call `AddGenericButtonHandlers()` on the confirmation dialog GameObject
- `AddGenericButtonHandlers()` finds all buttons without `UIButtonFeedback` or `TooltipTrigger` and adds EventTriggers

**Step 4: Test**
- Build and deploy the mod
- Navigate to the screen with the confirmation dialog
- Trigger the dialog and hover over buttons
- Check MelonLoader logs for "Added button handlers" and "Announced control" messages

#### Why This Approach Works

✅ **Game-specific** - Only patches Terra Invicta code, not Unity core methods
✅ **Surgical** - Runs once per controller during initialization (zero performance impact)
✅ **Reliable** - UI elements are guaranteed to exist at initialization time
✅ **No Harmony errors** - All `Initialize()` methods exist and can be patched
✅ **Maintainable** - Easy to add new patches as needed
✅ **Safe** - Doesn't interfere with game logic or lifecycle

#### When NOT to Use This Pattern

- If the dialog is already shown via `CanvasControllerBase.Show()` (existing patch handles it)
- If buttons already have `UIButtonFeedback` or `TooltipTrigger` components (existing patches handle them)
- If the dialog is created dynamically after initialization (use a different lifecycle method)

### Project References

The .csproj references game DLLs in specific locations:
- **MelonLoader DLLs**: `$(GameDir)\MelonLoader\net35\` (note: net35, not net6)
- **Game assemblies**: `$(GameDir)\TerraInvicta_Data\Managed\`
- All references set `<Private>false</Private>` to prevent copying to output

### Review Mode Architecture (Keyboard Navigation System)

Review Mode is a keyboard-only navigation system that allows users to browse game information without using the mouse. It's activated with **Numpad 0** and provides hierarchical navigation through game data.

#### Navigation Hierarchy

Review Mode uses a 4-level hierarchy:
1. **Screens** - High-level categories (Council, Technology, Nations, Org Market)
2. **Items** - Objects within a screen (councilors, nations, etc.)
3. **Sections** - Categories within an item (Stats, Missions, Organizations, etc.)
4. **Section Items** - Individual items within a section (specific stats, available missions, etc.)

#### Key Components

**`ReviewModeController`** (`mod/ReviewMode/ReviewModeController.cs`)
- Main entry point, attached as a Unity `MonoBehaviour`
- Handles input routing (Numpad 8/2 for nav, Enter to drill, Escape to back out)
- Manages selection sub-mode for multi-step actions (mission target selection)
- Singleton pattern via `Create()` factory method

**`NavigationState`** (`mod/ReviewMode/NavigationState.cs`)
- Tracks current position at each navigation level
- Handles `Next()`, `Previous()`, `DrillDown()`, `BackOut()` navigation
- Manages section caching and refresh

**`ScreenBase`** (`mod/ReviewMode/Screens/ScreenBase.cs`)
- Abstract base class for all screens
- Defines interface: `GetItems()`, `ReadItemSummary()`, `GetSectionsForItem()`
- Concrete implementations: `CouncilScreen`, `TechnologyScreen`, `NationScreen`, `OrgMarketScreen`

**`ISection`** (`mod/ReviewMode/Sections/ISection.cs`)
- Interface for navigable sections within items
- Methods: `ReadItem()`, `CanActivate()`, `Activate()`, `CanDrillIntoItem()`
- Implemented by `DataSection` for most use cases

**`IGameStateReader<T>`** (`mod/ReviewMode/Readers/IGameStateReader.cs`)
- Interface for extracting accessible text from game state objects
- Methods: `ReadSummary()`, `ReadDetail()`, `GetSections()`
- Implementations: `CouncilorReader`, `NationReader`, `OrgReader`, `TechBrowserReader`, etc.

#### Adding a New Screen

1. Create a class extending `ScreenBase` in `mod/ReviewMode/Screens/`
2. Implement `Name`, `GetItems()`, `ReadItemSummary()`, `ReadItemDetail()`, `GetSectionsForItem()`
3. Register in `ReviewModeController.InitializeScreens()`
4. Wire up any action callbacks (e.g., `OnEnterSelectionMode`, `OnSpeak`)

#### Adding a New Reader

1. Create a class implementing `IGameStateReader<T>` in `mod/ReviewMode/Readers/`
2. Implement `ReadSummary()` for list navigation (short, one-line)
3. Implement `ReadDetail()` for verbose reading (Numpad *)
4. Implement `GetSections()` returning a list of `ISection` for item navigation

#### Selection Sub-Mode

Used for multi-step actions like mission assignment:
1. Build a `List<SelectionOption>` with labels and data
2. Call `OnEnterSelectionMode(prompt, options, callback)`
3. User navigates options with Numpad 8/2, confirms with Enter
4. Callback receives selected index to execute action

#### Review Mode Keyboard Controls

| Action | Numpad | Laptop Alternative |
|--------|--------|-------------------|
| Toggle review mode | Numpad 0 | Ctrl+R |
| Navigate prev/next | Numpad 8/2 | Up/Down arrows |
| Drill down/activate | Numpad Enter or 5 | Enter or Right arrow |
| Back out | - | Escape, Left arrow, Backspace |
| Read detailed info | Numpad * | `-` (minus/dash) |
| List all items | Numpad / | `=` (equals) |
| Screen switching | PageUp/Down | PageUp/Down |
| Confirm assignments | Numpad + | `\` (backslash) |

### Accessibility Command Hotkeys

Defined in `mod/AccessibilityCommands.cs`:
- **Alt+R** - Read current faction resources (Money, Influence, Ops, Research, Boost)
- **Alt+S** - Read screen info (under development)
- **Alt+L** - List screen items (under development)
- **Alt+D** - Read detailed selection (under development)
- **Alt+O** - Read objectives (under development)

### Intel System (Game Architecture)

Terra Invicta has a comprehensive intel system that determines what information is visible about enemy factions, councilors, and assets. **Review Mode must respect this system** when expanding to "browse all" functionality.

#### Intel Thresholds

Intel is stored as a float (0.0 to 1.0+) and compared against thresholds defined in `TIGlobalConfig`:

**Councilor Intel Levels:**
| Threshold | Constant | What You See |
|-----------|----------|--------------|
| 0.10 | `intelToSeeNeutralPawn` | Someone exists at location |
| 0.25 | `intelToSeeCouncilorBasicData` | Name, type, faction |
| 0.50 | `intelToSeeCouncilorDetails` | Stats, traits |
| 0.75 | `intelToSeeCouncilorMission` | Current mission |
| 1.00 | `intelToSeeCouncilorSecrets` | Loyalty, turned status |

**Faction Intel Levels:**
| Threshold | Constant | What You See |
|-----------|----------|--------------|
| 0.25 | `intelToSeeFactionBasicData` | Leader, basic info |
| 0.25 | `intelToSeeFactionResources` | Resource stockpiles |
| 0.25 | `intelToSeeFactionUnassignedOrgs` | Org pool |
| 0.50 | `intelToSeeFactionObjectives` | Goals |
| 0.75 | `intelToSeeFactionProjects` | Active projects |

**Space Asset Intel Levels:**
| Threshold | Constant |
|-----------|----------|
| 0.10 | `intelToSeeSpaceAssetLocationandComposition` |
| 0.50 | `intelToSeeFleetShipDetails` |
| 0.80 | `intelToSeeSpaceAssetUndercoverEnemyCouncilors` |

#### Key Intel API Methods

Methods on `TIFactionState` for checking intel:

```csharp
// Core intel queries
float GetIntel(TIGameState target)           // Current intel level
float GetHighestIntel(TIGameState target)    // Highest intel ever achieved

// Councilor convenience methods (check against thresholds)
bool HasIntelOnCouncilorLocation(TICouncilorState councilor)   // Can see location
bool HasIntelOnCouncilorBasicData(TICouncilorState councilor)  // >= 0.25
bool HasIntelOnCouncilorDetails(TICouncilorState councilor)    // >= 0.50
bool HasIntelOnCouncilorMission(TICouncilorState councilor)    // >= 0.75
bool HasIntelOnCouncilorSecrets(TICouncilorState councilor)    // >= 1.00

// Space asset methods
bool HasIntelOnSpaceAssetLocation(TISpaceAssetState asset)
bool Prospected(TISpaceBodyState spaceBody)  // Have we surveyed this body?
```

#### The CouncilorView Pattern

The game uses a "view model" pattern in `CouncilorView.cs` that gates information based on intel. This is the pattern Review Mode readers should follow:

```csharp
// From CouncilorView.cs - example of intel-gated information
if (!playerCouncil.HasIntelOnCouncilorBasicData(councilor))
    return "Unknown Agent";

if (!playerCouncil.HasIntelOnCouncilorDetails(councilor))
    return "???";  // Hide stats until sufficient intel
```

#### Implementing Intel in Review Mode Readers

When expanding Review Mode to browse non-owned objects, readers must check intel:

```csharp
public List<ISection> GetSections(TICouncilorState councilor, TIFactionState viewer)
{
    var sections = new List<ISection>();
    bool isOwn = councilor.faction == viewer;

    // Always show if we have basic data
    if (isOwn || viewer.HasIntelOnCouncilorBasicData(councilor))
    {
        sections.Add(BuildInfoSection(councilor, viewer));
    }

    // Stats require Details level intel
    if (isOwn || viewer.HasIntelOnCouncilorDetails(councilor))
    {
        sections.Add(BuildStatsSection(councilor));
        sections.Add(BuildTraitsSection(councilor));
    }

    // Current mission requires Mission level intel
    if (isOwn || viewer.HasIntelOnCouncilorMission(councilor))
    {
        sections.Add(BuildCurrentMissionSection(councilor));
    }

    // Loyalty/turned status requires Secrets level intel
    if (isOwn || viewer.HasIntelOnCouncilorSecrets(councilor))
    {
        sections.Add(BuildLoyaltySection(councilor));
    }

    // Actions ONLY for own councilors - never for enemies
    if (isOwn)
    {
        sections.Add(BuildMissionsSection(councilor));
        sections.Add(BuildActionsSection(councilor));
    }

    return sections;
}
```

#### Current Reader Audit Status

**Safe for "Browse All":**
- `RecruitCandidateReader` - Public recruitment pool, no intel needed
- `TechBrowserReader` - Read-only tech database

**Needs Intel Checks Added:**
- `CouncilorReader` - **CRITICAL** - Currently has NO ownership/intel checks on actions
- `NationReader` - Mostly safe, but should verify military data visibility
- `OrgReader` - Only checks affordability, needs ownership check for enemy orgs
- `ResearchSlotReader` - Reveals other factions' research rates

**Key Rule:** Actions (missions, automation, XP spending, etc.) must ONLY appear for objects the player owns. Information can be shown based on intel level, but actions require ownership.

### Review Mode Expansion Plans

#### Phase 1: Browse All Mode

Expand existing screens to show all objects, not just player-owned:

- Add toggle key (e.g., `Tab`) to switch between "Mine" / "All"
- Nations: All 195+ nations, grouped alphabetically
- Councilors: Your council / Known enemy councilors / All factions
- Add letter filtering: Press `A` to jump to first "A" item, etc.

**Implementation:** Modify `GetItems()` in each screen to optionally return all objects:
```csharp
public override IReadOnlyList<object> GetItems()
{
    if (showAllMode)
        return GameStateManager.AllNations().ToList();
    else
        return NationReader.GetPlayerNations(faction);
}
```

#### Phase 2: Context Links

When viewing an object that references another object, allow navigation to it:
- "Controlled by: Exodus" → Press Enter → Go to Exodus faction view
- "Mission target: China" → Press Enter → Go to China nation detail

#### Phase 3: Search

Free-text search across all objects:
- Dedicated hotkey enters search mode
- Regular keyboard for typing (not numpad)
- Results filter as you type
- Enter on result jumps to detail view

## Common Pitfalls

1. **DO NOT compile decompiled code** - The `decompiled/` directory is for reference only. Always keep `EnableDefaultCompileItems=false` in the .csproj and explicitly include only `mod/**/*.cs` files.

2. **Harmony patch parameter names matter** - Method parameters in patches with wrong names will cause runtime errors like "Parameter 'tipText' not found". Always check decompiled game code for exact method signatures.

3. **Use build script, not direct dotnet commands** - The build script handles deployment automatically. Running `dotnet build` directly won't copy the mod to the game's Mods folder.

4. **MelonLoader attributes are required** - Without `[assembly: MelonInfo(...)]` and `[assembly: MelonGame(...)]`, the mod won't load even if the DLL builds successfully.

5. **Tooltip text is in TextFields, not in TooltipTrigger directly** - Don't try to get tooltip text from the trigger object itself; navigate through `Tooltip.TextFields` to get actual content.

6. **Avoid patching methods with TIUtilities static field references** - Some game methods (like `ResearchPanelController.UpdatePanel`) contain references to static fields from `TIUtilities` (e.g., `TIUtilities.UIColorIndicatorNegative`). When Harmony tries to patch these methods during mod loading, it triggers initialization of `TIUtilities` before the game is ready, causing a `TypeInitializationException`. **Solution:** Patch simpler methods instead (like `OnEnable`) or patch methods that run after game initialization is complete.

7. **DO NOT patch Unity core methods for UI accessibility** - Do NOT attempt to patch `GameObject.SetActive`, `Canvas.OnEnable`, or `Canvas.set_enabled` to catch confirmation dialogs. These approaches fail or have severe performance implications:
   - `Canvas.OnEnable` - Method doesn't exist (Canvas doesn't override MonoBehaviour.OnEnable)
   - `Canvas.set_enabled` - Harmony cannot reliably patch property setters on Unity core types
   - `GameObject.SetActive` - Called thousands of times per second, creates massive performance hit

   **Solution:** Use the **Confirmation Dialog Pattern** documented above - patch controller `Initialize()` methods instead. This is game-specific, reliable, and has zero performance impact.

8. **DO NOT add EventTrigger to children of clickable buttons** - Unity's `EventTrigger` component implements ALL pointer interfaces (`IPointerClickHandler`, `IPointerEnterHandler`, etc.) **regardless of which triggers you've registered**. When you add an EventTrigger to a text child of a Button:
   - User clicks on the text child
   - EventSystem sees the text has `IPointerClickHandler` (via EventTrigger)
   - EventTrigger receives the click but does nothing (no click trigger registered)
   - **The event stops there** - it doesn't propagate to the parent Button
   - The button's `onClick` never fires

   **Solution:** Add the EventTrigger to the **same GameObject as the Button**, not to child elements like text labels. When both components are on the same object, they both receive pointer events. Example:

   ```csharp
   // WRONG - blocks clicks:
   AddEventTrigger(button.GetComponentInChildren<TMP_Text>().gameObject);

   // CORRECT - both Button and EventTrigger receive events:
   AddEventTrigger(button.gameObject);
   ```

   For tabs that use `TabbedPaneController`, access the button via `tabbedPane.TabButton` instead of adding handlers to tab text labels.
