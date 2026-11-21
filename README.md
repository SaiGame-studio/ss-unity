# SaiGame Unity Client

The Unity SDK for [Sai Server](https://saigame.studio/products/sai-server/), the game backend platform by [SaiGame](https://saigame.studio/).

## About Sai Server

Sai Server gives your game accounts, cloud save, inventory, in-game economy, quests, leaderboards, analytics and server-side Lua logic, prebuilt and managed from one web admin. This SDK connects a Unity game to it: add the `SaiServer` prefab, set your Game ID, and the services are ready to call. Every game starts on a free base.

- **Product page and pricing:** [saigame.studio/products/sai-server](https://saigame.studio/products/sai-server/)
- **Why Sai Server:** [server.saigame.studio/why-sai-server](https://server.saigame.studio/why-sai-server)
- **Tutorials:** [server.saigame.studio/tutorials](https://server.saigame.studio/tutorials), starting with [Create a Studio](https://server.saigame.studio/tutorials/basic/get-started/create-a-studio)
- **API documentation:** [Postman docs](https://documenter.getpostman.com/view/401284/2sBYHbrbq5) for every HTTP API the SDK calls; import the collection from [postman/](https://github.com/SaiGame-studio/ss-unity/tree/main/postman) to call them by hand
- **Download the SDK:** [Releases](https://github.com/SaiGame-studio/ss-unity/releases)
- **Community:** [Discord](https://discord.gg/UU3YarmVde)

## This repository

Unity client and SDK for the SaiGame platform. It connects games to the SaiGame API at `https://api.saigame.studio` for authentication (password, Google, Steam), player progression, inventory, quests, shops, leaderboards, battle sessions, and battle Lua scripts.

The SDK version declared by the runtime entry point is `0.2.60` ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)). The project is configured for Unity `6000.3.8f1` ([ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)).

## Platform relationship

```text
Unity game  ───── HTTPS / JSON / JWT ─────►  api.saigame.studio
  ss-unity                                  SaiGame platform services
```

| Application | Responsibility | Integration point |
|---|---|---|
| `api.saigame.studio` | SaiGame platform API (`SaiServer.PRODUCTION_BASE_URL`). | Receives HTTPS requests from the SDK. |
| `server.saigame.studio` | Game content management console. | Use it to configure content consumed by the game. |
| `ss-unity` | Unity client SDK and demo scene. | `SaiServer` creates authenticated `UnityWebRequest` calls to the platform. |

## Repository layout

```text
Assets/
  SaiGame/
    Prefabs/SaiServer.prefab    Pre-wired SDK entry prefab
    Scenes/demo.unity           Demo scene (URP, reference only)
    LuaScript/                  Local battle Lua scripts and the Lua contract pack
    env.example.txt             Template for the project-root .env (Editor only)
    env.build.example.txt       Template for the project-root .env.build (player builds)
    Scripts/
      SaiServer.cs              SDK entry point and HTTP transport
      0_Auth/                   Authentication (password, Google) and token refresh
      1_GamerProgress/          Player data and progression
      2_Mailbox/                Player mail and claims
      3_ItemContainer/          Inventory, equipment, crafting, gacha
      4_Shop/                   Shop queries and purchases
      5_Quest/                  Chain, daily, progress, and claim flows
      6_Journey/                Player event tracking
      7_Leaderboard/            Ranking queries
      8_Battle/                 Battle sessions and scripts
      9_LuaScript/              Battle Lua script management (load, create, update, delete, download)
      10_Steam/                 Steam integration (Steamworks.NET): Login/, Achievement/, shared SteamAPI provider, Editor/
      Common/                   Shared behaviours, endpoint options, encryption, certificate handling
      Editor/                   Custom inspectors and manual test tools
Packages/                       Unity package manifest
ProjectSettings/                Unity project configuration
postman/v1/                     Postman collection of every HTTP API the SDK calls (see its README)
```

The `SaiServer` prefab owns references to the service components and provides them through properties such as `SaiServer.Instance.DailyQuest` and `SaiServer.Instance.Leaderboard` ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).

## Prerequisites

- Unity Hub with Unity `6000.3.8f1`.

### Installing the SDK into a game

The SDK ships as the `Assets/SaiGame` folder. The game project needs:

- `com.unity.nuget.newtonsoft-json` (used by Chain Quest). Add it in Package Manager by name, or add `"com.unity.nuget.newtonsoft-json": "3.2.2"` to `Packages/manifest.json`. Without it the SDK does not compile.
- Optional: Steamworks.NET, only for Steam login and Steam achievements. See [Steam setup](Assets/SaiGame/Scripts/10_Steam/Login/README.md) and [Steam achievements](Assets/SaiGame/Scripts/10_Steam/Achievement/README.md).

For Android or iOS games using rewarded ads, see the [optional LevelPlay sample and setup guide](Assets/SaiGame/Optional/LevelPlay/README.md).

The demo scene uses URP and is for reference only; games use [SaiServer.prefab](Assets/SaiGame/Prefabs/SaiServer.prefab), which has no render-pipeline dependency.

The prefab ships with an empty Game ID and with JSON request/response logging turned off. Set the Game ID before the first request. Keep JSON logging off in release builds.

TLS certificates are always validated. Validation is skipped only when the server URL is a loopback address (local development).

## Open and configure Unity

1. Open `ss-unity` in Unity Hub with the required Unity version.
2. Open [demo.unity](Assets/SaiGame/Scenes/demo.unity), or add [SaiServer.prefab](Assets/SaiGame/Prefabs/SaiServer.prefab) to your own scene.
3. Select the `SaiServer` object. **Server Endpoint** has one option, **Production HTTPS** (`https://api.saigame.studio`); a different server is set with `SERVER_ENDPOINT` in `.env` (see below). The custom inspector persists the selected endpoint in `PlayerPrefs` ([SaiServerEditor.cs](Assets/SaiGame/Scripts/Editor/SaiServerEditor.cs), [SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).
4. Set the Game ID for your SaiGame project (required; the prefab ships with it empty).
5. Configure only the services required by the scene. Services with `autoLoadOnLogin` subscribe to successful login and retrieve their initial data automatically; Daily Quest is one example ([DailyQuest.cs](Assets/SaiGame/Scripts/5_Quest/Daily/DailyQuest.cs)). `PlayerItem` no longer has this option.
6. After login, load GamerProgress (`GamerProgress.CreateProgress` or `GetProgress`) before calling other game services. `SaiServer` blocks those requests until GamerProgress is loaded ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs), `ValidateGamerProgressRequest`).

### Local environment files

`.env` and `.env.build` live in the project root and are git-ignored. Copy them from [env.example.txt](Assets/SaiGame/env.example.txt) and [env.build.example.txt](Assets/SaiGame/env.build.example.txt).

- `.env` (Editor only): `GAME_ID`, `USERNAME`, `PASSWORD`, `SERVER_ENDPOINT`. Applied in Play Mode when **Load .env on Play** and the matching override toggles are on in the `SaiServer` inspector ([EnvLoader.cs](Assets/SaiGame/Scripts/Editor/EnvLoader.cs)). Credentials are written to `PlayerPrefs`, not to serialized fields.
- `.env.build`: `GAME_ID` applied to every `SaiServer` in built scenes without modifying the assets ([BuildEnvSceneProcessor.cs](Assets/SaiGame/Scripts/Editor/BuildEnvSceneProcessor.cs)).

Because the prefab ships with an empty Game ID, development in this repository takes `GAME_ID` from `.env`.

## Runtime model

### Authentication and requests

`SaiAuth` publishes login and refresh events. Successful login starts token-expiration monitoring when auto-refresh is enabled ([SaiAuth.cs](Assets/SaiGame/Scripts/0_Auth/SaiAuth.cs)). `SaiServer` creates `UnityWebRequest` instances, sets the request timeout, and adds `Authorization: Bearer <token>` only for authenticated sessions ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).

Google sign-in is handled by `GoogleBackendLogin` (browser session polled until tokens arrive) and Steam sign-in by `SteamBackendLogin`; both push tokens into `SaiAuth` ([GoogleBackendLogin.cs](Assets/SaiGame/Scripts/0_Auth/Google/GoogleBackendLogin.cs), [Steam setup](Assets/SaiGame/Scripts/10_Steam/Login/README.md)).

Use service events or per-call callbacks to update game UI. For example:

```csharp
SaiServer.Instance.SaiAuth.Login(
    username,
    password,
    response => { /* update the game after authentication */ },
    error => { /* display the request error */ });
```

Do not place access tokens, refresh tokens, or private service credentials in source-controlled assets.

### Services and custom inspectors

Runtime services live beside their models, requests, and responses under `Assets/SaiGame/Scripts`. Editor-only inspectors live in feature-specific `Editor` folders and support manual development workflows. Keep editor helpers out of runtime game paths.

`SaiBehaviour` provides the component lifecycle hooks used by the SDK, while `SaiSingleton<T>` supplies the singleton pattern used by `SaiServer` ([SaiBehaviour.cs](Assets/SaiGame/Scripts/Common/SaiBehaviour.cs), [SaiSingleton.cs](Assets/SaiGame/Scripts/Common/SaiSingleton.cs)).

## Daily quest integration

The daily quest service supports pool loading, current-day quests, and assigning quests ahead. The client routes these calls through the current `GameId` and authenticated `SaiServer` instance ([DailyQuest.cs](Assets/SaiGame/Scripts/5_Quest/Daily/DailyQuest.cs)).

| Unity service | Service route |
|---|---|
| `DailyQuest.GetTodayQuests` | `GET /api/v1/games/{game_id}/daily-quests/{pool_id}` |
| `DailyQuest.AssignAhead` | `POST /api/v1/games/{game_id}/daily-quests/{pool_id}/assign-ahead` |
| `DailyTimeframe.GetTimeframe` | `GET /api/v1/games/{game_id}/daily-quests/pools/{pool_key}/assigned-timeframe?start_date=YYYY-MM-DD&end_date=YYYY-MM-DD` |

`DailyTimeframe` defaults the `ThisWeek` preset to Monday through Sunday ([DailyTimeframe.cs](Assets/SaiGame/Scripts/5_Quest/Daily/DailyTimeframe.cs)).

## Packages

The project uses the Universal Render Pipeline, Input System, UGUI, AI Navigation, Timeline, and the Unity Test Framework, among other Unity modules ([manifest.json](Packages/manifest.json)). Review `Packages/manifest.json` before upgrading Unity or package versions.

| Package | Role |
|---|---|
| `com.unity.nuget.newtonsoft-json` | Required by the SDK (Chain Quest). Games must install it too. |
| `com.rlabrecque.steamworks.net` | Steam login and achievement testing in this repository only. Optional for games. |
| `com.unity.asset-store-tools` (embedded) | Publishing the SDK. Not part of the SDK. |

## Development rules

- Use `SaiBehaviour` for MonoBehaviours that need SDK lifecycle hooks; use `SaiSingleton<T>` for singleton MonoBehaviours.
- Keep serialized field names stable. Use `FormerlySerializedAs` if a serialized field must be renamed.
- Do not edit `.unity` scenes or `.prefab` files as text. Use the Unity Editor.
- Commit `.meta` files alongside every new or moved Unity asset.
- Do not perform `GetComponent`, `FindObjectOfType`, `GameObject.Find`, or `Resources.Load` inside per-frame update methods.
- Add Unity tests only when the work explicitly requests them; use `Assets/Tests/EditMode` or `Assets/Tests/PlayMode`.

See [CLAUDE.md](CLAUDE.md) for the full Unity contribution rules and [Assets/SaiGame/README.md](Assets/SaiGame/README.md) for the SDK feature overview.

## Troubleshooting

| Symptom | Check |
|---|---|
| Unity requests fail | Confirm the device can reach `api.saigame.studio`, that **Production HTTPS** is selected, and that no `SERVER_ENDPOINT` override is active. |
| TLS or certificate errors | Certificates are validated except for loopback servers. Use a valid certificate for any non-local server. |
| `Newtonsoft` compile errors in a game | Install `com.unity.nuget.newtonsoft-json`. |
| `Blocked ... Call GamerProgress.CreateProgress or GamerProgress.GetProgress first` | Load GamerProgress after login before other service calls. |
| Unity calls the wrong environment | Check `SERVER_ENDPOINT` in `.env` and the override toggle; re-select **Production HTTPS** in the `SaiServer` inspector (stored in `PlayerPrefs`). |
| API returns unauthenticated errors | Authenticate through `SaiAuth`, `GoogleBackendLogin`, or `SteamBackendLogin` before calling protected services, and verify the Game ID is set (the prefab ships with it empty). |
| Daily quest data is empty | Confirm the player is authenticated, the selected pool exists for the configured game, and the requested timeframe is valid. |
