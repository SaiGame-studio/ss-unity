# SaiGame Services SDK

**Version: 0.2.60**

A comprehensive game backend integration SDK for Unity, providing a service layer that connects games to the Sai backend. Manages authentication (password, Google, Steam), player progression, inventory, quests, leaderboards, battle sessions, battle Lua scripts, and more.

## Online Tutorials

Steam authentication and ownership rewards: [SDK setup](Scripts/10_Steam/Login/README.md).

Full tutorials and documentation are available at: https://admin.saigame.studio/tutorials


## Architecture

- **Pattern**: Service-based with Singleton (`SaiSingleton<T>`) and event-driven design
- **Networking**: REST API via `UnityWebRequest` (GET/POST/PUT/PATCH/DELETE)
- **Serialization**: `JsonUtility` with custom helpers; Newtonsoft.Json for the recursive Chain Quest tree
- **Entry Point**: `SaiServer` singleton (`Scripts/SaiServer.cs`) manages all subsystems and HTTP communication against `https://api.saigame.studio`

## Project Structure

```
Assets/SaiGame/
├── Scripts/
│   ├── SaiServer.cs        # SDK entry point and HTTP transport
│   ├── 0_Auth/             # Password and Google (Google/) login, token management
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
│   ├── 9_LuaScript/        # Battle Lua script management
│   ├── 10_Steam/           # Steam (Steamworks.NET): shared SteamAPI provider
│   │   ├── Login/          #   Steam login, account linking, ownership rewards
│   │   ├── Achievement/    #   Steam achievements and stats
│   │   └── Editor/         #   Steam inspectors
│   ├── Common/             # Base behaviours, singletons, encryption, endpoint options
│   └── Editor/             # Custom inspectors & debug tools
├── Prefabs/
│   └── SaiServer.prefab   # Drop-in prefab with all services pre-wired
├── Scenes/
│   └── demo.unity           # Demo scene (URP, reference only)
├── LuaScript/               # Local battle Lua scripts and contract pack
├── env.example.txt          # Template for the project-root .env (Editor only)
└── env.build.example.txt    # Template for the project-root .env.build (player builds)
```

## Key Systems

| System | Description |
|---|---|
| **Auth** | Password and Google login, register, logout, auto-refresh tokens |
| **GamerProgress** | Player level, XP, gold, custom data (CRUD) |
| **Mailbox** | Send/receive messages, claim rewards, bulk operations |
| **ItemContainer** | Full inventory: items, containers, slots, tags, crafting, gacha, generators, presets |
| **Shop** | Browse shops, purchase items |
| **Quest** | Chain quests, daily quests, progression tracking, reward claiming |
| **Journey** | Track in-game events with session management |
| **Leaderboard** | View top rankings and local player rank |
| **Battle** | Battle sessions, scripts, and history |
| **LuaScript** | Load, create, update, delete, and download the game's battle Lua scripts |
| **Steam** | Steam login and ownership rewards ([guide](Scripts/10_Steam/Login/README.md)) |
| **SteamAchievement** | Steam achievements and stats: load, unlock, progress, stats ([guide](Scripts/10_Steam/Achievement/README.md)) |


## Quick Start

1. Install the required package `com.unity.nuget.newtonsoft-json` (see Dependencies)
2. Drop the `SaiServer` prefab into your scene
3. Keep **Server Endpoint** on **Production HTTPS** (`https://api.saigame.studio`); `SERVER_ENDPOINT` in `.env` overrides it in the Editor
4. Set your Game ID (required; the prefab ships with it empty)
5. Enable `autoLoadOnLogin` on services that should load data after login (`PlayerItem` has no such option)
6. After login, call `GamerProgress.CreateProgress` or `GamerProgress.GetProgress`; other service calls are blocked until GamerProgress is loaded
7. Use events (e.g. `OnLoginSuccess`, `OnGetItemsSuccess`) to react to service responses

## Design Principles

- **Modular**: Each system can be enabled/disabled independently
- **Event-Driven**: Services publish events for loose coupling
- **Auto-Load**: Most systems support automatic initialization on login
- **Editor Tools**: Custom inspectors for manual testing during development
- **Secure**: AES encryption (CBC/PKCS7) for sensitive data, automatic token refresh

## Dependencies

- **Required:** `com.unity.nuget.newtonsoft-json` (Chain Quest tree parsing). Add `"com.unity.nuget.newtonsoft-json": "3.2.2"` to the game's `Packages/manifest.json`, or install it by name in Package Manager. Without it the SDK does not compile.
- **Optional:** Steamworks.NET, only for Steam login and Steam achievements. See [Steam setup](Scripts/10_Steam/Login/README.md).
- `UnityEngine.Networking` (`UnityWebRequest`)
- .NET Cryptography (AES)
- The demo scene uses URP and is for reference only. `SaiServer.prefab` has no render-pipeline dependency.

## Security Defaults

- TLS certificates are always validated. Validation is skipped only when the server URL is a loopback address (local development).
- The prefab ships with JSON request/response logging off. Keep it off in release builds.
- Request and response bodies of login, register, 2FA, refresh, logout, Google, and Steam requests are never logged.

## Changelog

### v0.2.60
- Steam achievements and stats (`SteamAchievements`)
- Steam login (`SteamBackendLogin`) with account linking, 2FA, and ownership rewards; Steamworks.NET is optional
- TLS certificate validation is enforced except for loopback servers
- Token-bearing auth requests are excluded from logs; JSON logging is off in the prefab
- The prefab ships with an empty Game ID
- Newtonsoft.Json is a declared requirement

### v0.2.28
- Initial documented version
- 8 core service systems (Auth, GamerProgress, Mailbox, ItemContainer, Shop, Quest, Journey, Leaderboard)
- Battle session management
- Editor debugging tools
- AES encryption utility
- Local and production server support
