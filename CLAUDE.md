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
  - `Patches/TooltipPatches.cs` - Harmony patches for ModelShark tooltip system
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
