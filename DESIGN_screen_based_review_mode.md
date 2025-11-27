# Screen-Based Review Mode Architecture

## Overview

This document describes a refactored approach to Review Mode that:
1. **Organizes by game screens** rather than being councilor-centric
2. **Introduces "Readers"** - typed classes that know how to extract and format data from game state classes
3. **Provides full mission modifier access** when selecting action targets
4. **Mirrors the game's mental model** - users think "I want to check research" not "I want to check councilor X's research section"

## Current Problems

### 1. Councilor-Centric Limitations

The current `ReviewModeController` is built around:
```
Councilor → Sections → Items
```

This works for mission assignment but doesn't help with:
- Reviewing nation data without a mission context
- Checking research progress and adjusting priorities
- Reviewing faction resources and income breakdown
- Managing national priorities for controlled nations

### 2. Missing Modifiers

When selecting mission targets (e.g., "Control Nation"), we only show:
```csharp
label += $", {successChance}";  // Just "65%"
```

But the game has rich modifier data via `TIMissionResolution_Contested`:
- `GetAttackingNonZeroModifiers()` → Bonus modifiers (councilor stat, control points in neighbors, ideology alignment, etc.)
- `GetDefendingNonZeroModifiers()` → Penalty modifiers (target security, bureaucracy, enemy control points, etc.)

Users need this information to make informed decisions.

### 3. No Way to Browse Game State

The current mode only activates in the context of "what can this councilor do?" There's no way to:
- Browse all controlled nations and their stats
- Review all research in progress across factions
- Check fleet status without being on the fleet screen

## Proposed Architecture

### Core Concept: Readers

Inspired by TISaveViewer's service pattern, we introduce **Readers** - classes that know how to extract and format information from specific game state types.

```
IGameStateReader<T> where T : TIGameState
├── ReadSummary(T state) → string           // One-line summary
├── ReadDetail(T state) → string            // Full detail for current item
├── GetSections(T state) → List<ISection>   // Navigable sections
└── GetActions(T state) → List<ActionItem>  // Available actions
```

**Key insight**: Readers are independent of UI. They read directly from game state classes (`TICouncilorState`, `TINationState`, `TIFactionState`, etc.) regardless of which screen is showing.

### Screen Organization

```
Review Mode (R key to toggle)
│
├── [1] Council Screen
│   ├── Browse councilors (yours + known enemies)
│   ├── Councilor detail (stats, traits, orgs, mission)
│   └── Assign mission (with full modifier breakdown)
│
├── [2] Nations Screen
│   ├── Browse controlled nations
│   ├── Nation detail (GDP, population, unrest, priorities)
│   ├── Control point breakdown
│   └── Set national priority (action)
│
├── [3] Research Screen
│   ├── Active tech projects (3 slots)
│   ├── Active engineering projects (3 slots)
│   ├── Tech progress/completion estimates
│   └── Adjust research allocation (action)
│
├── [4] Resources Screen
│   ├── Current resources (Money, Influence, Ops, Boost, MC)
│   ├── Income breakdown per resource
│   └── Expense breakdown
│
├── [5] Intel Screen
│   ├── Known factions
│   ├── Faction relationships
│   └── Known enemy councilors
│
└── [6+] Future: Fleets, Habs, Space Assets
```

### Navigation Model (Hierarchical)

Screens are simply the top level of a unified hierarchy. The existing numpad navigation pattern extends naturally - no new keys needed:

```
Screens (top level)
├── Numpad 8/2: Navigate between screens
├── Enter/5: Drill into selected screen
│
└── Sections (within a screen)
    ├── Numpad 8/2: Navigate between sections
    ├── Enter/5: Drill into section (if has sub-items)
    │
    └── Items (within a section)
        ├── Numpad 4/6: Navigate between items
        ├── Enter/5: Activate item (if actionable)
        └── Numpad *: Read detail/modifiers

Escape/Numpad 0: Back up one level (item→section→screen→exit)
Numpad /: List items at current level
```

**Initial state**: Review mode always starts on Council screen (predictable).

This means the user can navigate: Screens → Council → Councilors section → Councilor 1 → Missions section → Control Nation → (target selection with modifiers)

## Readers Design

### Interface

```csharp
namespace TISpeech.ReviewMode.Readers
{
    /// <summary>
    /// Reads and formats data from a specific game state type.
    /// </summary>
    public interface IGameStateReader<T> where T : TIGameState
    {
        /// <summary>
        /// One-line summary for list navigation.
        /// Example: "United States, 6 control points, GDP $21T"
        /// </summary>
        string ReadSummary(T state);

        /// <summary>
        /// Detailed reading for current item focus.
        /// Example: Full nation stats, priorities, control breakdown
        /// </summary>
        string ReadDetail(T state);

        /// <summary>
        /// Get navigable sections for this state object.
        /// </summary>
        List<ISection> GetSections(T state);

        /// <summary>
        /// Get available actions for this state object.
        /// </summary>
        List<ActionItem> GetActions(T state);
    }
}
```

### Planned Readers

| Reader | Game State Type | Used In Screens |
|--------|-----------------|-----------------|
| `CouncilorReader` | `TICouncilorState` | Council, Intel |
| `NationReader` | `TINationState` | Nations |
| `RegionReader` | `TIRegionState` | Nations (drill-down) |
| `FactionReader` | `TIFactionState` | Resources, Intel |
| `TechReader` | `TITechTemplate` | Research |
| `ProjectReader` | `TIProjectTemplate` | Research |
| `MissionReader` | `TIMissionTemplate` | Council (mission assignment) |
| `MissionModifierReader` | `TIMissionModifier` | Council (target selection) |
| `FleetReader` | `TIFleetState` | Fleets (future) |
| `HabReader` | `TIHabState` | Habs (future) |

### Example: NationReader

```csharp
public class NationReader : IGameStateReader<TINationState>
{
    public string ReadSummary(TINationState nation)
    {
        int controlPoints = nation.regionsOwned?.Sum(r => r.controlPoints) ?? 0;
        return $"{nation.displayName}, {controlPoints} control points";
    }

    public string ReadDetail(TINationState nation)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Nation: {nation.displayName}");
        sb.AppendLine($"GDP: ${nation.GDP:N0}");
        sb.AppendLine($"Population: {nation.population:N0}");
        sb.AppendLine($"Unrest: {nation.unrest:P0}");
        sb.AppendLine($"Democracy: {nation.democracy:N1}");
        sb.AppendLine($"Current Priority: {nation.currentPriority?.displayName ?? "None"}");
        return sb.ToString();
    }

    public List<ISection> GetSections(TINationState nation)
    {
        var sections = new List<ISection>();

        // Overview section
        var overview = new DataSection("Overview");
        overview.AddItem("GDP", $"${nation.GDP:N0}");
        overview.AddItem("Population", $"{nation.population:N0}");
        overview.AddItem("Unrest", $"{nation.unrest:P0}");
        overview.AddItem("Democracy", $"{nation.democracy:N1}");
        sections.Add(overview);

        // Regions section
        var regions = new DataSection("Regions");
        foreach (var region in nation.regionsOwned ?? new List<TIRegionState>())
        {
            regions.AddItem(region.displayName, $"{region.controlPoints} CP");
        }
        sections.Add(regions);

        // Control points section
        var control = new DataSection("Control Points");
        // Group by controlling faction
        // ... implementation
        sections.Add(control);

        // Priorities section (actionable)
        var priorities = new DataSection("Set Priority");
        foreach (var priority in GetAvailablePriorities(nation))
        {
            priorities.AddItem(priority.displayName, onActivate: () => SetPriority(nation, priority));
        }
        sections.Add(priorities);

        return sections;
    }

    public List<ActionItem> GetActions(TINationState nation)
    {
        return new List<ActionItem>
        {
            new ActionItem("Set Priority", () => EnterPrioritySelection(nation)),
            new ActionItem("View Regions", () => DrillDownToRegions(nation))
        };
    }
}
```

### Example: MissionModifierReader

This is the key reader for solving the modifiers problem:

```csharp
public class MissionModifierReader
{
    /// <summary>
    /// Get full modifier breakdown for a mission + target combination.
    /// </summary>
    public MissionModifierBreakdown GetModifiers(
        TIMissionTemplate mission,
        TICouncilorState councilor,
        TIGameState target,
        float resourcesSpent = 0f)
    {
        var breakdown = new MissionModifierBreakdown();

        if (mission.resolutionMethod is TIMissionResolution_Contested contested)
        {
            // Attacking modifiers (bonuses)
            var attackMods = contested.GetAttackingNonZeroModifiers(
                mission, councilor, target, resourcesSpent);

            foreach (var mod in attackMods)
            {
                float value = mod.GetModifier(councilor, target, resourcesSpent,
                    mission.cost?.resourceType ?? FactionResource.None);
                breakdown.Bonuses.Add(new ModifierItem
                {
                    Name = mod.displayName,
                    Value = value
                });
            }
            breakdown.TotalBonus = contested.SumAttackingModifiers(
                mission, councilor, target, resourcesSpent);

            // Defending modifiers (penalties)
            var defendMods = contested.GetDefendingNonZeroModifiers(
                mission, councilor, target, resourcesSpent);

            foreach (var mod in defendMods)
            {
                float value = mod.GetModifier(councilor, target, resourcesSpent,
                    mission.cost?.resourceType ?? FactionResource.None);
                breakdown.Penalties.Add(new ModifierItem
                {
                    Name = mod.displayName,
                    Value = value
                });
            }
            breakdown.TotalPenalty = contested.SumDefendingModifiers(
                mission, councilor, target, resourcesSpent);

            // Final success chance
            breakdown.SuccessChance = mission.resolutionMethod.GetSuccessChanceString(
                mission, councilor, target, resourcesSpent);
        }
        else
        {
            breakdown.SuccessChance = "100%"; // Uncontested
        }

        return breakdown;
    }

    /// <summary>
    /// Format modifiers for speech output.
    /// </summary>
    public string FormatForSpeech(MissionModifierBreakdown breakdown, bool verbose = false)
    {
        var sb = new StringBuilder();
        sb.Append($"Success chance {breakdown.SuccessChance}. ");

        if (verbose && breakdown.Bonuses.Any())
        {
            sb.Append($"Bonuses: ");
            foreach (var bonus in breakdown.Bonuses.Take(3)) // Top 3
            {
                sb.Append($"{bonus.Name} {bonus.Value:+0.#;-0.#}, ");
            }
            sb.Append($"Total {breakdown.TotalBonus:+0.#;-0.#}. ");
        }

        if (verbose && breakdown.Penalties.Any())
        {
            sb.Append($"Penalties: ");
            foreach (var penalty in breakdown.Penalties.Take(3)) // Top 3
            {
                sb.Append($"{penalty.Name} {penalty.Value:+0.#;-0.#}, ");
            }
            sb.Append($"Total {breakdown.TotalPenalty:+0.#;-0.#}. ");
        }

        return sb.ToString();
    }
}

public class MissionModifierBreakdown
{
    public List<ModifierItem> Bonuses { get; } = new();
    public List<ModifierItem> Penalties { get; } = new();
    public float TotalBonus { get; set; }
    public float TotalPenalty { get; set; }
    public string SuccessChance { get; set; }
}

public class ModifierItem
{
    public string Name { get; set; }
    public float Value { get; set; }
}
```

## Screen Implementations

### ScreenBase

```csharp
public abstract class ScreenBase
{
    public abstract string Name { get; }
    public abstract string KeyHint { get; } // e.g., "[1]"

    /// <summary>
    /// Get all items available on this screen.
    /// </summary>
    public abstract List<object> GetItems();

    /// <summary>
    /// Get sections for a specific item.
    /// </summary>
    public abstract List<ISection> GetSectionsForItem(object item);

    /// <summary>
    /// Read summary for an item (for list navigation).
    /// </summary>
    public abstract string ReadItemSummary(object item);

    /// <summary>
    /// Read detail for an item (when focused).
    /// </summary>
    public abstract string ReadItemDetail(object item);

    /// <summary>
    /// Called when screen becomes active.
    /// </summary>
    public virtual void OnActivate() { }

    /// <summary>
    /// Called when screen becomes inactive.
    /// </summary>
    public virtual void OnDeactivate() { }
}
```

### CouncilScreen

```csharp
public class CouncilScreen : ScreenBase
{
    private readonly CouncilorReader councilorReader = new();
    private readonly MissionModifierReader modifierReader = new();

    public override string Name => "Council";
    public override string KeyHint => "[1]";

    public override List<object> GetItems()
    {
        var items = new List<object>();

        // Player's councilors
        if (GameControl.control?.activePlayer?.councilors != null)
        {
            items.AddRange(GameControl.control.activePlayer.councilors);
        }

        return items;
    }

    public override string ReadItemSummary(object item)
    {
        if (item is TICouncilorState councilor)
            return councilorReader.ReadSummary(councilor);
        return "Unknown";
    }

    public override List<ISection> GetSectionsForItem(object item)
    {
        if (item is TICouncilorState councilor)
            return councilorReader.GetSections(councilor);
        return new List<ISection>();
    }

    // Mission assignment with full modifiers
    public void StartMissionAssignment(TICouncilorState councilor, TIMissionTemplate mission)
    {
        var targets = mission.GetValidTargets(councilor);
        var options = new List<SelectionOption>();

        foreach (var target in targets)
        {
            var breakdown = modifierReader.GetModifiers(mission, councilor, target);
            string label = $"{target.displayName}, {breakdown.SuccessChance}";

            options.Add(new SelectionOption
            {
                Label = label,
                DetailReader = () => modifierReader.FormatForSpeech(breakdown, verbose: true),
                Data = target
            });
        }

        ReviewModeController.Instance.EnterSelectionMode(
            $"Select target for {mission.displayName}",
            options,
            detailKey: KeyCode.KeypadMultiply, // Press * for full modifier breakdown
            onSelect: (index) => ExecuteMission(councilor, mission, options[index].Data)
        );
    }
}
```

### NationsScreen

```csharp
public class NationsScreen : ScreenBase
{
    private readonly NationReader nationReader = new();

    public override string Name => "Nations";
    public override string KeyHint => "[2]";

    public override List<object> GetItems()
    {
        var items = new List<object>();
        var player = GameControl.control?.activePlayer;

        if (player != null)
        {
            // Get nations where player has control points
            foreach (var nation in TINationState.GetNationStates())
            {
                if (nation.HasControlPointFrom(player))
                    items.Add(nation);
            }
        }

        return items;
    }

    public override string ReadItemSummary(object item)
    {
        if (item is TINationState nation)
            return nationReader.ReadSummary(nation);
        return "Unknown";
    }

    public override List<ISection> GetSectionsForItem(object item)
    {
        if (item is TINationState nation)
            return nationReader.GetSections(nation);
        return new List<ISection>();
    }
}
```

### ResearchScreen

```csharp
public class ResearchScreen : ScreenBase
{
    private readonly TechReader techReader = new();
    private readonly ProjectReader projectReader = new();

    public override string Name => "Research";
    public override string KeyHint => "[3]";

    public override List<object> GetItems()
    {
        var items = new List<object>();
        var player = GameControl.control?.activePlayer;

        if (player != null)
        {
            // Active tech research (3 slots)
            for (int i = 0; i < 3; i++)
            {
                var tech = player.GetResearchInSlot(i);
                if (tech != null)
                    items.Add(new ResearchSlot { Index = i, Tech = tech, IsProject = false });
            }

            // Active project research (3 slots)
            for (int i = 0; i < 3; i++)
            {
                var project = player.GetProjectInSlot(i);
                if (project != null)
                    items.Add(new ResearchSlot { Index = i, Project = project, IsProject = true });
            }
        }

        return items;
    }

    public override string ReadItemSummary(object item)
    {
        if (item is ResearchSlot slot)
        {
            if (slot.IsProject)
                return $"Project {slot.Index + 1}: {slot.Project?.displayName ?? "Empty"}";
            else
                return $"Tech {slot.Index + 1}: {slot.Tech?.displayName ?? "Empty"}";
        }
        return "Unknown";
    }
}
```

## File Structure

```
mod/ReviewMode/
├── ReviewModeController.cs         # Main controller (refactored)
├── ScreenManager.cs                # Manages screen switching
├── Screens/
│   ├── ScreenBase.cs              # Abstract base
│   ├── CouncilScreen.cs
│   ├── NationsScreen.cs
│   ├── ResearchScreen.cs
│   ├── ResourcesScreen.cs
│   └── IntelScreen.cs
├── Readers/
│   ├── IGameStateReader.cs        # Interface
│   ├── CouncilorReader.cs
│   ├── NationReader.cs
│   ├── RegionReader.cs
│   ├── FactionReader.cs
│   ├── TechReader.cs
│   ├── ProjectReader.cs
│   ├── MissionReader.cs
│   └── MissionModifierReader.cs
└── Sections/
    ├── ISection.cs                 # Keep existing
    └── DataSection.cs              # Keep existing
```

## Implementation Phases

### Phase 1: Reader Foundation
- [ ] Define `IGameStateReader<T>` interface
- [ ] Implement `CouncilorReader` (extract from current ReviewModeController)
- [ ] Implement `MissionModifierReader` (solve the modifiers problem)
- [ ] Test with existing councilor-centric flow

### Phase 2: Screen Architecture
- [ ] Implement `ScreenBase` abstract class
- [ ] Implement `ScreenManager` for screen switching
- [ ] Refactor `ReviewModeController` to use screens
- [ ] Implement `CouncilScreen` (migrate existing functionality)

### Phase 3: Nations Screen
- [ ] Implement `NationReader`
- [ ] Implement `RegionReader`
- [ ] Implement `NationsScreen`
- [ ] Add national priority adjustment action

### Phase 4: Research Screen
- [ ] Implement `TechReader`
- [ ] Implement `ProjectReader`
- [ ] Implement `ResearchScreen`
- [ ] Add research slot adjustment action

### Phase 5: Resources & Intel
- [ ] Implement `FactionReader`
- [ ] Implement `ResourcesScreen`
- [ ] Implement `IntelScreen`

### Phase 6: Polish
- [ ] Add screen switching hotkeys (1-9)
- [ ] Add Tab cycling between screens
- [ ] Optimize performance (caching)
- [ ] User testing and feedback

## Key Design Decisions

### 1. Readers vs Direct Access

**Why Readers?**
- **Testability**: Readers can be unit tested with mock game state
- **Reusability**: Same reader works across multiple screens
- **Separation**: Formatting logic separate from navigation logic
- **Maintenance**: Changes to game state access isolated to readers

### 2. Screen-Based vs UI-Based

**Why not mirror the game UI exactly?**
- Game UI requires mouse interaction we can't replicate
- Some game screens are overly complex (mission screen has ~20 panels)
- Our screens should optimize for keyboard navigation and speech
- We can combine related functionality (e.g., Resources screen combines what's spread across multiple game panels)

### 3. Modifier Verbosity

**Solution**: Two levels of detail
- **Summary** (default): Target name + success percentage
- **Detail** (press *): Full modifier breakdown

This balances speed (quick navigation) with depth (informed decision-making).

## Integration with Existing Code

The existing `ReviewModeController` will be refactored:
1. Extract councilor logic into `CouncilorReader` and `CouncilScreen`
2. Keep navigation logic in `ReviewModeController`
3. Add `ScreenManager` for screen switching
4. Selection sub-mode remains, but enhanced with detail key

Existing `ISection` and `DataSection` remain unchanged - readers produce sections.

## Design Decisions (Resolved)

1. **Initial screen**: Review mode always starts on Council screen for predictability.

2. **Navigation model**: Screens are the top level of a unified hierarchy. Existing numpad keys work at all levels - no new key bindings needed.

## Open Questions

1. **How to handle actions that open game dialogs?**
   - Some actions (like detailed research adjustment) may need to fall back to game UI
   - Could announce "Opening research panel" and deactivate review mode

2. **Caching strategy?**
   - Readers currently compute fresh each call
   - May need caching for large lists (all nations, all regions)
   - Cache invalidation on game state changes

3. **Keyboard layout alternatives?**
   - Current: Numpad-centric
   - Alternative: Arrow keys + Enter
   - Could support both with configurable bindings (future enhancement)
