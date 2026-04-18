# SaiGame Services SDK

**Version: 0.2.28**

A comprehensive game backend integration SDK for Unity, providing a service layer that connects games to the Sai backend. Manages authentication, player progression, inventory, quests, leaderboards, battle sessions, and more.

## Online Tutorials

Full tutorials and documentation are available at: https://admin.saigame.studio/tutorials


## Architecture

- **Pattern**: Service-based with Singleton (`SaiSingleton<T>`) and event-driven design
- **Networking**: REST API via `UnityWebRequest` (GET/POST/PUT/PATCH/DELETE)
- **UI**: Unity UIElements / UIDocument (UXML-based)
- **Serialization**: `JsonUtility` with custom helpers
- **Entry Point**: `SaiServer` singleton manages all subsystems and HTTP communication

## Project Structure

```
Assets/SaiGame/
├── Scripts/
│   ├── 0_Auth/             # Authentication, login/register, token management
│   ├── 1_GamerProgress/    # Player XP, levels, gold, custom game data
│   ├── 2_Mailbox/          # In-game messaging, reward claiming
│   ├── 3_ItemContainer/    # Inventory & equipment system
│   │   ├── Container/      #   Storage containers
│   │   ├── Item/           #   Core item management
│   │   ├── Slot/           #   Equipment slots
│   │   ├── Tag/            #   Item categorization
│   │   ├── Crafting/       #   Item crafting
│   │   ├── Gacha/          #   Gacha/loot box system
│   │   ├── Generator/      #   Procedural item generation
│   │   └── Preset/         #   Preset item configurations
│   ├── 4_Shop/             # Shop browsing & purchases
│   ├── 5_Quest/            # Quest system
│   │   ├── Chain/          #   Sequential quest lines
│   │   ├── Daily/          #   Daily quests
│   │   ├── Progress/       #   Quest progression tracking
│   │   └── Claims/         #   Quest completion & rewards
│   ├── 6_Journey/          # Event tracking / analytics
│   ├── 7_Leaderboard/      # Competitive rankings
│   ├── 8_Battle/           # Battle session management
│   ├── Common/             # SaiServer, singletons, encryption, HTTP layer
│   └── Editor/             # Custom inspectors & debug tools
├── UI/
│   ├── Common/             # UIRouter, UIPanelBase, TopNavigatorUI
│   ├── GamerProgress/      # Player stats panel
│   └── Login/              # Auth UI
├── Prefabs/
│   └── SaiServer.prefab   # Drop-in prefab with all services pre-wired
└── Scenes/
    ├── demo.unity           # Main demo scene
    └── demo-ui.unity        # UI demo scene
```

## Key Systems

| System | Description |
|---|---|
| **Auth** | Login, register, logout, auto-refresh tokens |
| **GamerProgress** | Player level, XP, gold, custom data (CRUD) |
| **Mailbox** | Send/receive messages, claim rewards, bulk operations |
| **ItemContainer** | Full inventory: items, containers, slots, tags, crafting, gacha, generators, presets |
| **Shop** | Browse shops, purchase items |
| **Quest** | Chain quests, daily quests, progression tracking, reward claiming |
| **Journey** | Track in-game events with session management |
| **Leaderboard** | View top rankings and local player rank |
| **Battle** | Battle sessions, scripts, and history |


## Quick Start

1. Drop the `SaiServer` prefab into your scene
2. Configure the server endpoint (Local / Production) on the `SaiServer` component
3. Set your Game ID
4. Enable `autoLoadOnLogin` on subsystems you need
5. Use events (e.g. `OnLoginSuccess`, `OnGetItemsSuccess`) to react to service responses

## Design Principles

- **Modular**: Each system can be enabled/disabled independently
- **Event-Driven**: Services publish events for loose coupling
- **Auto-Load**: Most systems support automatic initialization on login
- **Editor Tools**: Custom inspectors for manual testing during development
- **Secure**: AES encryption (CBC/PKCS7) for sensitive data, automatic token refresh

## Dependencies

- Unity UIElements (`UIDocument`, `VisualElement`, UXML)
- `UnityEngine.Networking` (`UnityWebRequest`)
- .NET Cryptography (AES)

## Changelog

### v0.2.28
- Initial documented version
- 8 core service systems (Auth, GamerProgress, Mailbox, ItemContainer, Shop, Quest, Journey, Leaderboard)
- Battle session management
- UIRouter-based navigation system
- Editor debugging tools
- AES encryption utility
- Local and production server support
