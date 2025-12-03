# Fleet Operations Implementation Guide

This document tracks the implementation status of fleet operations in Review Mode and provides guidance for future development.

## Implementation Status

### Phase 1: Simple Operations (COMPLETE)

| Operation | Section | Status | Notes |
|-----------|---------|--------|-------|
| Transfer | Transfer Planning | Done | Uses TransferPlanningSubMode |
| Undock From Station | Fleet Management | Done | Instant operation |
| Set Homeport | Homeport | Done | Uses SelectionSubMode |
| Clear Homeport | Homeport | Done | Instant operation |
| Merge Fleet | Fleet Management | Done | Uses SelectionSubMode |
| Merge All Fleets | Fleet Management | Done | Instant operation |
| Cancel Operation | Fleet Management | Done | Uses fleet.CancelOperation() |

### Phase 2: Maintenance Operations (COMPLETE)

| Operation | Section | Status | Notes |
|-----------|---------|--------|-------|
| Resupply | Maintenance | Done | Shows full cost breakdown |
| Repair | Maintenance | Done | Shows full cost breakdown |
| Resupply & Repair | Maintenance | Done | Combined operation |

### Phase 3: Ship Selection Operations (NOT STARTED)

| Operation | Type | Complexity | Notes |
|-----------|------|------------|-------|
| Split Fleet | SplitFleetOperation | High | Needs ShipSelectionSubMode |
| Scuttle Ships | ScuttleShipsOperation | High | Needs ShipSelectionSubMode |

**Required:** Create `ShipSelectionSubMode.cs` similar to `SelectionSubMode.cs` but for multi-select ship picking.

### Phase 4: Location Operations (NOT STARTED)

| Operation | Type | Complexity | Notes |
|-----------|------|------------|-------|
| Land On Surface | LandOnSurfaceOperation | Medium | Target = TISpaceBodyState |
| Launch From Surface | LaunchFromSurfaceOperation | Low | No target selection |
| Survey Planet | SurveyPlanetOperation | Low | Fleet must be in orbit |

### Phase 5: Combat & Construction (NOT STARTED)

| Operation | Type | Complexity | Notes |
|-----------|------|------------|-------|
| Bombard | BombardOperation | Medium | Target selection needed |
| Assault Hab | AssaultHabOperation | Medium | Marines required |
| Destroy Hab | DestroyHabOperation | Medium | Confirmation needed |
| Found Platform | FoundPlatformOperation | Medium | Location selection |
| Found Outpost | FoundOutpostOperation | Medium | Location selection |

---

## Critical Implementation Patterns

### Pattern 1: Always Use OnOperationConfirm()

**CRITICAL:** Never call game methods directly. Always use the operation's `OnOperationConfirm()` method.

```csharp
// WRONG - Operation won't register properly
fleet.AssignTrajectory(trajectory);

// CORRECT - Proper operation registration
var transferOp = OperationsManager.operationsLookup[typeof(TransferOperation)] as TISpaceFleetOperationTemplate;
transferOp.OnOperationConfirm(fleet, target, null, trajectory);
```

**Why:** `OnOperationConfirm()` handles:
- Creating time events for operation completion
- Registering the operation in `fleet.currentOperations`
- Triggering UI updates and notifications
- Proper game state management

### Pattern 2: Cancel Operations Properly

```csharp
// WRONG - Leaves orphan time events
operation.OnOperationCancel(fleet, false);
fleet.currentOperations.Remove(operation);

// CORRECT - Handles all cleanup
fleet.CancelOperation(operationData);
```

### Pattern 3: Get Operation Templates

```csharp
using static OperationsManager;

// Get any operation template by type
var op = operationsLookup[typeof(SomeOperation)] as TISpaceFleetOperationTemplate;

// Check if visible/available
bool visible = op.OpVisibleToActor(fleet);
bool canExecute = op.ActorCanPerformOperation(fleet, target);
```

### Pattern 4: Format Operation Costs

```csharp
private string GetOperationCostString(TISpaceFleetState fleet, TISpaceFleetOperationTemplate operation, TIGameState target = null)
{
    var costs = operation.ResourceCostOptions(fleet.faction, target ?? fleet, fleet, checkCanAfford: false);
    if (costs == null || costs.Count == 0 || !costs[0].anyDebit)
        return "No cost";
    return costs[0].ToString("Relevant", gainsOnly: false, costsOnly: false, fleet.faction);
}
```

### Pattern 5: Callback Chain

Operations flow through this chain:
```
FleetReader (creates section items with callbacks)
    ↓
FleetsScreen (wires FleetReader callbacks to controller callbacks)
    ↓
ReviewModeController (handles execution, speaks results)
```

Each callback property must be defined at all three levels.

---

## Operation Reference

### Checking Operation Availability

```csharp
// Docking
bool canUndock = fleet.dockedAtStation;
bool canDock = !fleet.dockedAtStation && nearbyStation != null;

// Homeport
bool hasHomeport = fleet.homeport != null;
var possibleHomeports = setHomeportOp.GetPossibleTargets(fleet); // Returns faction's habs

// Merge
bool canMerge = fleet.CanMerge(otherFleet);
var nearbyFleets = fleet.ref_deployedBody.FleetsAtLocation(fleet.location)
    .Where(f => f != fleet && f.faction == fleet.faction && fleet.CanMerge(f));

// Maintenance
bool needsRefuel = fleet.NeedsRefuel;
bool needsRearm = fleet.NeedsRearm;
bool needsRepair = fleet.NeedsRepair;

// Location
bool onSurface = fleet.onSurface;
bool inOrbit = !fleet.onSurface && fleet.ref_deployedBody != null;
```

### Operation Types in OperationsManager.fleetOperations

Full list of fleet operations (from decompiled code):

```
TransferOperation
UndockFromStationOperation
DockAtStationOperation
SetHomeportOperation
ClearHomeportOperation
MergeFleetOperation
MergeAllFleetsOperation
SplitFleetOperation
RenameFleetOperation
ScuttleShipsOperation
ResupplyOperation
RepairFleetOperation
ResupplyAndRepairOperation
LandOnSurfaceOperation
LaunchFromSurfaceOperation
SurveyPlanetOperation
BombardOperation
AssaultHabOperation
DestroyHabOperation
FoundPlatformOperation
FoundOutpostOperation
ExpandHabModuleOperation
ReplaceHabModuleOperation
SalvageHabModuleOperation
TransferResourcesOperation
TransferArmiesOperation
LayMinesOperation
SweepMinesOperation
```

---

## ShipSelectionSubMode Design (Phase 3)

For Split Fleet and Scuttle Ships, create a new sub-mode:

```csharp
public class ShipSelectionSubMode
{
    private List<TISpaceShipState> availableShips;
    private HashSet<int> selectedIndices;
    private int currentIndex;

    public Action<string, bool> OnSpeak { get; set; }
    public Action<List<TISpaceShipState>> OnConfirmed { get; set; }
    public Action OnCancelled { get; set; }

    // Navigation
    public void Initialize(List<TISpaceShipState> ships, string prompt);
    public void Next();      // Numpad 2
    public void Previous();  // Numpad 8

    // Selection
    public void ToggleCurrent();  // Enter - toggle selection
    public void SelectAll();      // Numpad +
    public void DeselectAll();    // Numpad -

    // Confirmation
    public void Confirm();   // Numpad Enter (when ready)
    public void Cancel();    // Escape

    // Announcements
    public string GetCurrentAnnouncement()
    {
        var ship = availableShips[currentIndex];
        bool selected = selectedIndices.Contains(currentIndex);
        return $"{ship.displayName}, {(selected ? "selected" : "not selected")}";
    }

    public string GetSummary()
    {
        return $"{selectedIndices.Count} of {availableShips.Count} ships selected";
    }
}
```

### Split Fleet Flow

1. Check `SplitFleetOperation.EligibleShips(fleet)` - ships that can be split off
2. Enter ShipSelectionSubMode with eligible ships
3. User toggles selection (validation: must leave at least 1 ship in original fleet)
4. On confirm:
   ```csharp
   var splitOp = operationsLookup[typeof(SplitFleetOperation)] as TISpaceFleetOperationTemplate;
   // The operation expects selected ships as targets
   splitOp.OnOperationConfirm(fleet, selectedShips, null, null);
   ```

### Scuttle Ships Flow

1. Get all ships in fleet
2. Enter ShipSelectionSubMode
3. Confirm with warning: "Scuttle N ships? This cannot be undone."
4. Execute for each selected ship

---

## Testing Checklist

### Phase 1 & 2 (Current)
- [x] Transfer to another body
- [x] Undock from station while docked
- [x] Set homeport to different hab
- [x] Clear homeport
- [x] Merge two fleets at same location
- [x] Merge all fleets at location
- [x] Cancel active operation
- [x] Resupply at hab
- [x] Repair at hab
- [x] Resupply & Repair combined

### Phase 3 (Future)
- [ ] Split fleet (select ships, creates new fleet)
- [ ] Scuttle ships (confirm, ships destroyed)
- [ ] Cannot split if only 1 ship
- [ ] Cannot scuttle all ships

### Phase 4 (Future)
- [ ] Land on surface from orbit
- [ ] Launch from surface to orbit
- [ ] Survey planet for resources

### Phase 5 (Future)
- [ ] Bombard target
- [ ] Assault hab with marines
- [ ] Destroy hab
- [ ] Found platform/outpost

---

## Files Modified

- `mod/ReviewMode/Readers/FleetReader.cs` - Section creation and callbacks
- `mod/ReviewMode/Screens/FleetsScreen.cs` - Callback wiring
- `mod/ReviewMode/ReviewModeController.cs` - Operation execution handlers

## Key Decompiled References

- `decompiled/PavonisInteractive.TerraInvicta/OperationsManager.cs` - Operation registry
- `decompiled/PavonisInteractive.TerraInvicta/TransferOperation.cs` - Transfer pattern
- `decompiled/PavonisInteractive.TerraInvicta/ResupplyOperation.cs` - Maintenance pattern
- `decompiled/PavonisInteractive.TerraInvicta/TISpaceFleetState.cs` - Fleet state and methods
