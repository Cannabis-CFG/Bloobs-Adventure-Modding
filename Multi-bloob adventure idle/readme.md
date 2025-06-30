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
	- Currently broken and in testing.
    

This enables multiple players to see each other moving, leveling, but not interact with each other (due to limitations with how the game is designed, it wasn't meant to be multiplayer after all).

----------

### Features

-   **WebSocket-Based Sync**  
    Automatically connects to our backend NodeJS server and maintains a messaging queue to send/receive JSON payloads.
    
-   **Ghost Players**  
    Instantiates and updates clones of other players with accurate position, color, and movement speed.
    
-   **Skill & Prestige Panel**  
    Gathers your skill levels and prestige from in‑scene components and optionally displays a floating panel when hovering your mouse over ghosts.
    - Currently broken and in testing.
    
-   **Ghost Cosmetic Display**  
    Sends and renders equipped hat and wings on both local and ghost characters.
    
-   **Hover UI Integration**  
    Custom UI framework to build mouse-over tooltips
    -	Currently broken and in testing.
    
    

----------

### Dependencies

-   [BepInEx](https://github.com/BepInEx/BepInEx)
    
-   [WebSocketSharp](https://github.com/sta/websocket-sharp) (This goes within `<GameFolder>/Bloobs Adventure Idle_Data/Managed`)

    
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

  

```ini

[Visual]

# Toggle hover‐panel showing player levels
Enable Level Panel? = true


```



Save and restart to apply.

----------

### Usage

1.  **Connect** – When you hit the main menu, enter your save moderately fast. Timings for grabbing data have yet to be properly refined
    
2.  **Spawn Ghosts** – Other connected players appear as color ­tinted clones that move and animate in real time.
    
3.  ~~**Inspect** – Hover your mouse over a ghost to see their level panel (if enabled).~~*
    
4.  **Enjoy** – Observe fellow adventurers sharing the same idle world.
    
*= Currently broken and in testing.

Happy adventuring with friends—without leaving your single‑player realm!
