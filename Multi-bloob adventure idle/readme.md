 - # This marks your game as modded
   ## This may potentially ruin the fun of the game for yourself
   ## This may cause unintended issues. Including but not limited to; low frames, stutters, incorrect movement and crashes.
   ## The game's developer is NOT responsible if issues occur when using mods
   ### Under no circumstances must you provide bug reports to the official Discord when using mods, make them on GitHub with an issue request. We will dig into it and check to see if it is a mod issue or a game issue.

----------

### What It Does

The **Multi-blood Adventure Idle** mod adds basic multiplayer synchronization to the single‑player Bloobs Adventure Idle game. It connects your game client to a central NodeJS server to:

-   **Broadcast your position, appearance, and skill data** to other players.
    
-   **Receive updates** about other connected players and display their ghosts in your world.
    
-   **Render nameplates**, skill levels and names for each ghost.
    

This enables multiple players to see each other moving, leveling, but not interact with each other (due to limitations with how the game is designed, it wasn't meant to be multiplayer after all).

----------

### Features

-   **WebSocket-Based Sync**  
    Automatically connects to our backend NodeJS server and maintains a messaging queue to send/receive JSON payloads.
    
-   **Ghost Players**  
    Instantiates and updates clones of other players with accurate position, color, and movement speed.
    
-   **Skill & Prestige Panel**  
    Gathers your skill levels and prestige from in‑scene components and optionally displays a floating panel when hovering your mouse over ghosts.
    
-   **Ghost Cosmetic Display**  
    Sends and renders equipped hat and wings on both local and ghost characters.
    - Not currently written and nonfunctional.
    
-   **Hover UI Integration**  
    Custom UI framework to build mouse-over tooltips
    
    

----------

### Dependencies

-   [BepInEx v5.4.23.3](https://github.com/BepInEx/BepInEx)
    
-   WebSocketSharp (This goes within `<GameFolder>/Bloobs Adventure Idle_Data/Managed`)

    
   ### Optional Dependency

 - [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
	 - Allows you to dynamically modify configuration files of any plugin that properly registers BepInEx configuration entries without the need to restart the game to apply effects.

----------

### Installation

1.  Install **BepInEx** as per its official instructions.
2.  Place `Multi-bloob adventure idle.dll` into **`<GameFolder>/BepInEx/plugins/`**.
3.  Launch the game once to generate the configuration entries.
    

----------

### Configuration

After first run, edit **`BepInEx/config/Multiblood Adventure Idle.cfg`** to adjust visual options:

Locate `BepInEx\config\SouldexEntriesManager.cfg` and adjust the following settings:


| `Enable Level Panel?` | `true` | Toggle hover‐panel showing player levels |

  

Example:

  

```cfg

[Visual]

## Hover panel width
# Setting type: Int32
# Default value: 300
Panel Width = 150

## Hover panel height
# Setting type: Int32
# Default value: 150
Panel Height = 75

## Hover panel vertical offset
# Setting type: Int32
# Default value: -100
Panel Pos Y = 100

## Background R 0–255
# Setting type: Int32
# Default value: 30
Panel Color R = 30

## Background G 0–255
# Setting type: Int32
# Default value: 30
Panel Color G = 30

## Background B 0–255
# Setting type: Int32
# Default value: 30
Panel Color B = 30

## Background opacity 0–1
# Setting type: Single
# Default value: 0.7
Panel Alpha = 0.4

## Enable panel drop shadow
# Setting type: Boolean
# Default value: true
Shadow Enabled = false

## Shadow color R 0–255
# Setting type: Int32
# Default value: 0
Shadow Color R = 0

## Shadow color G 0–255
# Setting type: Int32
# Default value: 0
Shadow Color G = 0

## Shadow color B 0–255
# Setting type: Int32
# Default value: 0
Shadow Color B = 0

## Shadow opacity 0–1
# Setting type: Single
# Default value: 0.5
Shadow Alpha = 0.5

## Shadow horizontal offset
# Setting type: Int32
# Default value: 4
Shadow Offset X = 4

## Shadow vertical offset
# Setting type: Int32
# Default value: -4
Shadow Offset Y = -4

## Hover text font size
# Setting type: Int32
# Default value: 14
Font Size = 8

## Text color R 0–255
# Setting type: Int32
# Default value: 240
Text Color R = 240

## Text color G 0–255
# Setting type: Int32
# Default value: 240
Text Color G = 240

## Text color B 0–255
# Setting type: Int32
# Default value: 240
Text Color B = 240

## Text opacity 0–1
# Setting type: Single
# Default value: 1
Text Alpha = 1

## Text margin left
# Setting type: Int32
# Default value: 8
Margin Left = 8

## Text margin top
# Setting type: Int32
# Default value: 8
Margin Top = 8

## Text margin right
# Setting type: Int32
# Default value: 8
Margin Right = 8

## Text margin bottom
# Setting type: Int32
# Default value: 8
Margin Bottom = 8

[Visual]

## Toggles whether or not the skill level panel is displayed when hovering your mouse over a ghost
# Setting type: Boolean
# Default value: true
Enable Level Panel? = true

```



Save and restart to apply.

----------

### Usage

1.  **Connect** – Load your save, the mod will handle the rest.
    
2.  **Spawn Ghosts** – Other connected players appear as color ­tinted clones that move and animate in real time.
    
3.  **Inspect** – Hover your mouse over a ghost to see their level panel (if enabled).
    
4.  **Enjoy** – Observe fellow adventurers sharing the same idle world.
    


Happy adventuring with friends, without leaving your single‑player realm!
