# Multi-Bloobs Adventure Idle

A social-multiplayer mod for **Bloobs Adventure Idle** built with **BepInEx 5**.

This mod adds lightweight online presence to the game so players can see each other in real time, chat globally, whisper privately, and hang out


  # This marks your game as modded
   ## This may potentially ruin the fun of the game for yourself
   ## This may cause unintended issues. Including but not limited to; low frames, stutters, incorrect movement and crashes.
   ## The game's developer is NOT responsible if issues occur when using mods
   ### Under no circumstances must you provide bug reports to the official Discord when using mods outside of the mod forums under this mods post, if you do we will dig into it and check to see if it is a mod issue or a game issue.



## Features

- Real-time player presence
- Remote player ghosts
- Global chat
- Private whispers and replies
- Chat bubbles above players
- Unlockable chat titles
- Reconnect handling
- Persistent chat window position and size

## Requirements

- **[Bloobs Adventure Idle](https://store.steampowered.com/app/2942780/Bloobs_Adventure_Idle/)**
- **[BepInEx <= 5.4.23.3](https://github.com/BepInEx/BepInEx)**

## Installation

### Client

1. Install **BepInEx 5** into the game.
2. Launch the game once for BepInEx to initialize.
2. Place the compiled mod `.dll` into:

   ```text
   BepInEx/plugins/
   ```

3. Launch the game once to generate config entries if needed.

## Chat Commands

### Player commands

```text
/help
/w <name|partial|steamId> <message>
/wselect <number>
/r <message>
/block <partialName|steamId>
/blockselect <number>
/unblock <partialName|steamId>
/pmclear
/pmclose
/title
/cleartitle
```

## How It Works

The mod does **not** turn the game into a full MMO.

It creates a shared online layer where players can:

- broadcast position and visual state
- appear as remote ghosts in other players’ games
- talk through global and private chat
- receive server messages and title unlocks

This keeps the mod lightweight while still giving the game a social multiplayer feel.

## Chat Titles

Chat titles are handled by the server.

Players only receive titles they have unlocked, enforced by the server.  
Some titles are progression-based, while others can be restricted to specific players in general.

## Notes

- This is a pseudo-multiplayer mod.
- Progression and world state are not converted into a full synchronized online game.
- All gameplay systems remain local to the client.
- The chat window supports resizing, pinning, and saved placement.


## Disclaimer

This project is a fan-made multiplayer layer for Bloobs Adventure Idle.  
It is not an official online mode and is provided as-is.
Any issues or bugs must be reported to the mods developers and shall not be reported to the main games developer

## Credits

- Mod development: **Cannabis**, **Unreal_Unicorn (AKA J)**
- Built with:
  - **BepInEx**
  - **Harmony**
  - **WebSocketSharp**
  - **Node.js**
