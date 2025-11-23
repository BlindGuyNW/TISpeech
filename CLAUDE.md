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
