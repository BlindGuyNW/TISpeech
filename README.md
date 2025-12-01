# TISpeech - Terra Invicta Screen Reader Accessibility Mod

TISpeech is a MelonLoader mod for Terra Invicta that provides screen reader accessibility. It announces tooltips and UI elements using the Tolk library, supporting NVDA, JAWS, and SAPI screen readers.

## Features

- **Tooltip Announcements**: Automatically speaks tooltips when hovering over UI elements
- **Review Mode**: Full keyboard-only navigation system for browsing game data without a mouse
- **Menu Navigation**: Accessible main menu, load game, new game, and options screens
- **Resource Announcements**: Quick hotkeys to hear your faction's resources
- **Time Controls**: Pause, play, and adjust game speed from Review Mode
- **Transfer Planner**: Plan fleet transfers with accessible trajectory information

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for Terra Invicta
2. Copy `TISpeechMod.dll` to the game's `Mods` folder
3. Copy `Tolk.dll` and the NVDA controller DLLs to the game directory
4. Launch Terra Invicta - the mod will automatically initialize

## Keyboard Reference

### Toggle Review Mode

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Toggle Review Mode | `Numpad 0` | `Ctrl+R` |

### Review Mode Navigation

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Navigate previous | `Numpad 8` | `Up Arrow` |
| Navigate next | `Numpad 2` | `Down Arrow` |
| Alternative prev/next | `Numpad 4/6` | - |
| Drill down / Activate | `Numpad Enter` or `Numpad 5` | `Enter` or `Right Arrow` |
| Back out | - | `Escape`, `Left Arrow`, or `Backspace` |
| Read detailed info | `Numpad *` | `-` (minus/dash) |
| List all items | `Numpad /` | `=` (equals) |
| Quick screen switch | `Page Up/Down` | `Page Up/Down` |
| Confirm assignments | `Numpad +` | `\` (backslash) |
| Toggle Mine/All view | - | `Tab` |
| Cycle faction filter | - | `[` / `]` |
| Jump to letter | - | `A-Z` |

### Screen-Specific Controls

| Action | Key |
|--------|-----|
| Sort menu (Nations/Space Bodies) | `Ctrl+S` |
| Cycle faction filter (Nations) | `Ctrl+F` |
| Probe all bodies (Space Bodies) | `Ctrl+P` |
| Enter transfer planner | `T` |

### Time Controls (In Review Mode)

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Pause/Play | - | `Space` |
| Set speed 1-6 | `Numpad 1` (speed 1 only) | `1-6` |
| Read full time status | `Numpad 7` | - |

### Priority Grid Mode

When navigating national priorities:

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Navigate rows (priorities) | `Numpad 8/2` | `Up/Down Arrow` |
| Navigate columns (control points) | `Numpad 4/6` | `Left/Right Arrow` |
| Increment value | `Numpad Enter` or `Numpad 5` | `Enter` |
| Decrement value | `Numpad -` | - |
| Mass change row | `Ctrl+Enter` | `Ctrl+Enter` |
| Row summary | `Numpad *` | - |
| Column summary | `Numpad /` | `=` |
| Sync from current CP | - | `S` |
| Apply preset | - | `P` |
| Read priority description | - | `D` |
| Exit grid mode | - | `Escape` |

### Transfer Planner Mode

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Navigate options | `Numpad 8/2` | `Up/Down Arrow` |
| Select / Confirm | `Numpad Enter` or `Numpad 5` | `Enter` or `Right Arrow` |
| Go back | - | `Escape`, `Backspace`, or `Left Arrow` |
| Read detail | `Numpad *` | `-` (minus) |
| Cycle sort mode | - | `Tab` |
| Jump to letter | - | `A-Z` |

For numeric input (acceleration/delta-V):
- Type digits `0-9`
- Use `.` for decimal point
- `Backspace` to delete
- `Enter` to confirm
- `Escape` to cancel

### Combat Mode

When space combat begins:

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Navigate options | `Numpad 8/2` | `Up/Down Arrow` |
| Adjust value (bidding) | `Numpad 4/6` | `Left/Right Arrow` |
| Activate option | `Numpad Enter` or `Numpad 5` | `Enter` |
| Read detail | `Numpad *` | `-` (minus) |
| List all options | `Numpad /` | `=` |
| Fleet summary | - | `F` |

### Menu Mode Navigation

Works in main menu, load game, new game, and options screens:

| Action | Numpad | Laptop Alternative |
|--------|--------|--------------------|
| Navigate controls | `Numpad 8/2` | `Up/Down Arrow` |
| Adjust control (sliders) | `Numpad 4/6` | `Left/Right Arrow` |
| Activate control | `Numpad Enter` or `Numpad 5` | `Enter` |
| Go back / Exit | - | `Escape` |
| Read detail | `Numpad *` | `-` (minus) |
| List all controls | `Numpad /` | `=` |
| Jump to letter | - | `A-Z` |

### Global Accessibility Hotkeys

These work anytime during gameplay (outside Review Mode):

| Action | Hotkey |
|--------|--------|
| Read faction resources | `Alt+R` |
| Screen info (in development) | `Alt+S` |
| List items (in development) | `Alt+L` |
| Detailed selection (in development) | `Alt+D` |
| Objectives (in development) | `Alt+O` |

## Available Screens in Review Mode

Review Mode provides access to these game screens:

1. **Council** - Browse your councilors, view stats, assign missions, manage automation
2. **Technology** - Navigate research slots, queue technologies, browse tech tree
3. **Nations** - Review all nations, control points, policies, and priorities
4. **Org Market** - Browse and acquire organizations
5. **Fleets** - Manage space fleets, view ships, plan transfers
6. **Space Bodies** - Explore planets, moons, asteroids; launch probes
7. **Habs** - Review space habitats and their modules

### Navigation Hierarchy

Review Mode uses a 4-level hierarchy:
1. **Screens** - The high-level categories listed above
2. **Items** - Objects within a screen (councilors, nations, fleets, etc.)
3. **Sections** - Categories within an item (Stats, Missions, Organizations)
4. **Section Items** - Individual details within a section

Use `Enter` or `Right Arrow` to drill into deeper levels, `Escape` or `Left Arrow` to back out.

## Tips for New Users

1. **Start with Review Mode**: Press `Numpad 0` or `Ctrl+R` to enter Review Mode and explore
2. **Learn the hierarchy**: Use `Numpad /` or `=` to list all items at your current level
3. **Use letter navigation**: Press any letter key to jump to items starting with that letter
4. **Get details**: Press `Numpad *` or `-` for more detailed information about the current item
5. **Time is paused**: You can take your time - use `Space` to pause/play when ready

## Requirements

- Terra Invicta (Steam version)
- MelonLoader 0.5.7 or later
- A screen reader (NVDA, JAWS, or Windows SAPI)

## Building from Source

```bash
./build_and_deploy.bat
```

This builds the project and deploys it to your game's Mods folder.

## License

This mod is provided as-is for accessibility purposes.

## Support

For issues or feature requests, please open an issue on GitHub.
