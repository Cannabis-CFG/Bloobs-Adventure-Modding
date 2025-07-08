 - # This marks your game as modded
 - # This is an example mod and not intended for live use
 - ## As such this will not be included in release packages
   ## This breaks the balance of the game
   ## This will potentially ruin the fun of the game for yourself
   ## The game's developer is NOT responsible if issues occur when using mods
   ### Under no circumstances must you provide bug reports to the official Discord when using mods, make them on GitHub with an issue request. We will dig into it and check to see if it is a mod issue or a game issue.


### What It Does

  

The **Souldex Entries Manager** is a small Bloobs mod that modifies the Soul Compendium UI by:
-  **Increasing the maximum number of auto-managed entries** in your Souldex (Soul Compendium).

-  **Expanding the scrollable area** so you can comfortably view all of your entries.

  

This means you can automatically add far more auto managed item entries than the game’s default limits allow, while being able to view them all as well.

  

---

  

### Features

  

-  **Configurable Multiplier**\

Multiplies your max auto managed entries by your set value.

  

-  **Adjustable Window Height**\

Sets how tall the scrollable list of entries can grow, letting view more than the originally allowed amount of entries.

  

---

  

### Installation

  
1 Ensure you have **[BepInEx 5.4.23.3](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3)** installed for your game.
2. Copy the `SouldexEntriesManager.dll` into your game\BepInEx\plugins\ directory.
3. Run the game once to generate the configuration file.

## Optional Dependency

 - [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
	 - Allows you to dynamically modify configuration files of any plugin that properly registers BepInEx configuration entries without the need to restart the game to apply effects.

  

---

  

### Configuration

  

Locate `BepInEx\config\SouldexEntriesManager.cfg` and adjust the following settings:


| `Multiplier` | `10` | Multiplies the maximum auto-managed entries by this number. |

| `Window Y Size` | `15000.0` | Height (in pixels) of the scrollable content area in the entry UI. |

  

Example:

  

```ini

[General]

# How many times to multiply the max auto-managed entries

Multiplier = 10

  

# Maximum scrollable height of the entries window

Window Y Size = 15000.0

```

  

Save the file and restart the game to apply changes.
*If Configuration Manager is installed you can close and reopen the Auto Manager tab to apply changes*

  

---

  

### Usage

  

1. Open your soul compendium in-game.
2. Enter your auto manager

3. Auto-manage entries as usual—now with increased capacity and support to view them all!

