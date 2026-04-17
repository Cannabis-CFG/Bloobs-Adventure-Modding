<div align="center">

# Multi-Bloobs Adventure Idle

**A social multiplayer layer for Bloobs Adventure Idle built with BepInEx 5**

See other players in real time, chat globally or privately, inspect player profiles, create clans, progress clan skills and unlock clan upgrades natively from inside the mod. Now with an auto update system!

</div>

---

## Table of Contents

- [What This Mod Is](#what-this-mod-is)
- [Important Warning](#important-warning)
- [Feature Overview](#feature-overview)
- [Requirements](#requirements)
- [Installation](#installation)
- [In-Game Chat Commands](#in-game-chat-commands)
- [Profiles](#profiles)
- [Clan System](#clan-system)
- [Native Updater](#native-updater)
- [What This Mod Does Not Do](#what-this-mod-does-not-do)
- [Bug Reports and Support](#bug-reports-and-support)
- [Credits](#credits)
- [Disclaimer](#disclaimer)

---

## What This Mod Is

**Multi-Bloobs Adventure Idle** is not a full MMO conversion.

It adds a lightweight online social layer on top of a primarily local single-player game. Players can:

- appear as live remote ghosts
- chat in **Global**, **Private**, and **Clan** channels
- inspect **player profiles** and **clan profiles**
- create and manage **clans**
- contribute live gameplay actions to **clan progression**
- update the mod through a **native GitHub-based updater**

The goal is to keep the game local and lightweight while making it feel socially connected.

---

## Important Warning

> [!WARNING]
> **This marks your game as modded.**
>
> This mod may potentially reduce the fun of the game for yourself, introduce unintended behavior, and cause issues including but not limited to:
>
> - lower FPS
> - stutters or hitching
> - UI issues
> - movement oddities
> - desync-looking behavior
> - crashes
>
> The game's developer is **not responsible** for issues caused while using mods.

> [!CAUTION]
> Do **not** report mod-related issues to the official game Discord as if they were vanilla-game issues.
>
> If you report a problem while using mods, it first needs to be checked to determine whether it is a mod problem or a base-game problem.

---

## Feature Overview

### Social Presence

- Real-time player presence
- Remote player ghosts
- Nameplates and hover displays
- Right-click player context menus
- Copy SteamID / whisper / block / view profile actions

### Chat

- Global chat
- Private whispers and replies
- Clan chat
- System and server broadcast messages
- Chat bubbles above players
- Unlockable chat titles
- Local block list
- Persistent chat window size, position, pinning, and theme settings

### Profiles

- In-game **player profiles**
- In-game **clan profiles**
- Clan-aware profile information
- Member contribution visibility for clan members

### Clans

- Clan creation, invites, leaving, kicking, and disbanding
- Clan role hierarchy:
  - **Owner**
  - **Elder**
  - **Deputy**
  - **Member**
- Editable role permissions
- Clan skills with manual prestige gating
- Infinite-style clan upgrades (Coming soon, to be finalized)
- Action-based clan XP contribution batching
- Action-based boss kill contribution tracking
- Public and member-only clan profile visibility

### Native Updater

- GitHub release checks from inside the mod
- Install now
- Install on close
- Ignore specific release version
- Release-name and asset-name filtering

---

## Requirements

- **Bloobs Adventure Idle**
- **BepInEx 5.x**

---

## Installation

### Client

1. Install **BepInEx 5** into the game.
2. Launch the game once so BepInEx can initialize.
3. Place the compiled mod `.dll` into:

   ```text
   BepInEx/plugins/
   ```

4. Launch the game again to generate config entries.


---

## In-Game Chat Commands

Use `/help` in-game for the latest full list.

Common commands include:

```text
/help
/w <name|partial|steamId> <message>
/r <message>
/c <message>
/clan help
/wselect <number>
/block <partialName|steamId>
/blockselect <number>
/unblock <partialName|steamId>
/pmclear
/pmclose
/title
/cleartitle
```

---

## Profiles

### Player Profiles

Player profiles can include:

- display name
- SteamID
- active title
- clan name and tag
- save-type flags such as turbo-save state
- total level and prestige summaries
- skill summaries
- boss kill summaries
- clan contribution summaries when visible to the viewer

### Clan Profiles

Clan profiles can include:

- clan name and tag
- owner
- member list
- clan skill progression
- clan upgrade status
- clan permissions
- aggregate clan totals
- boss kill totals
- member contribution summaries

Some clan information is public, while deeper management and contribution details are intentionally restricted to clan members.

---

## Clan System

### Design Goals

The clan system is meant to support **social multiplayer progression** without turning the game into a shared-economy game.

### Clan Roles

- **Owner** — full control
- **Elder** — broad management access
- **Deputy** — limited management access
- **Member** — standard clan participation

### Role Permissions

Permissions can be configured per role and currently cover actions such as:

- inviting players
- kicking members
- managing roles
- managing permissions
- purchasing upgrades
- toggling upgrades
- prestiging clan skills

### Clan Skills

Clan skills are exactly the same as regular skilling, **but shared as a clan.**

### Clan Skill Prestige

Clan skill prestige follows the base game, **but without a prestige limit.**

### Clan Upgrades

Clan upgrades are under development, **more information to come.**

### Clan Contributions

Clan contribution tracking is action-based and tied to the player’s **current clan at the time of contribution**.

Contribution categories include:

- clan XP contribution
- boss kill contribution

### Social-Only Direction

This mod does **not** and **will not** include:

- a clan bank
- item sharing
- shared inventory storage
- direct co-op economy systems

This is intentional.

The mod is designed to remain a **social multiplayer** experience rather than a full cooperative progression rebalance.

---


## Native Updater

The mod includes a native GitHub-based update system.

### What it can do

- check GitHub releases from inside the mod
- filter by repository owner and repository name
- filter releases by text in the release name or tag
- filter assets by preferred asset name text
- prompt the player in-game when an update is available
- download the selected update asset
- install immediately after confirmation
- stage installation for game close

### Typical updater config

These values are generated in the BepInEx config and can be adjusted as needed:

```text
Updates/Check For Updates = true
Updates/GitHub Owner = Cannabis-CFG
Updates/GitHub Repo = Bloobs-Adventure-Modding
Updates/Release Name Contains = Multiplayer
Updates/Asset Name Contains =
```

---

## What This Mod Does Not Do

This mod does **not**:

- turn the game into a full MMO
- synchronize the full game world as a shared online simulation
- make combat fully shared
- rebalance the game around multiplayer progression
- provide a clan bank or shared inventory economy

Gameplay remains primarily local to the client.

---

## Bug Reports and Support

If you encounter problems while using this mod:

- report them to the **mod developers**
- include logs when possible
- include reproduction steps when possible
- mention whether other mods were installed
- do **not** report mod-caused issues to the main game developer as if they were vanilla-game bugs

---

## Credits

- Mod development: **Cannabis**, **Unreal_Unicorn (AKA J)**
- Built with:
  - **BepInEx**
  - **Harmony**
  - **WebSocketSharp**
  - **Node.js**

---

## Disclaimer

This project is a fan-made multiplayer layer for **Bloobs Adventure Idle**.

It is **not** an official online mode and is provided **as-is**.
Any issues or bugs related to this mod should be reported to the mod developers, not to the base game developer as vanilla-game issues.
