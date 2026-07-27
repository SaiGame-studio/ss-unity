# SaiGame Unity Client

Unity client and SDK for the SaiGame platform. It connects games to services provided through `ai.saigame.studio` for authentication, player progression, inventory, quests, shops, leaderboards, and battle sessions.

The SDK version declared by the runtime entry point is `0.2.43` ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)). The project is configured for Unity `6000.3.8f1` ([ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)).

## Platform relationship

```text
Unity game  ───── HTTPS / JSON / JWT ─────►  ai.saigame.studio
  ss-unity                                  SaiGame platform services
```

| Application | Responsibility | Integration point |
|---|---|---|
| `ai.saigame.studio` | SaiGame platform services. | Receives HTTPS requests from the SDK. |
| `server.saigame.studio` | Game content management console. | Use it to configure content consumed by the game. |
| `ss-unity` | Unity client SDK and demo scene. | `SaiServer` creates authenticated `UnityWebRequest` calls to the platform. |

## Repository layout

```text
Assets/
  SaiGame/
    Prefabs/SaiServer.prefab    Pre-wired SDK entry prefab
    Scenes/demo.unity           Demo scene
    Scripts/
      SaiServer.cs              SDK entry point and HTTP transport
      0_Auth/                   Authentication and token refresh
      1_GamerProgress/          Player data and progression
      2_Mailbox/                Player mail and claims
      3_ItemContainer/          Inventory, equipment, crafting, gacha
      4_Shop/                   Shop queries and purchases
      5_Quest/                  Chain, daily, progress, and claim flows
      6_Journey/                Player event tracking
      7_Leaderboard/            Ranking queries
      8_Battle/                 Battle sessions and scripts
      Common/                   Shared Unity behaviours and configuration
      Editor/                   Custom inspectors and manual test tools
Packages/                       Unity package manifest
ProjectSettings/                Unity project configuration
```

The `SaiServer` prefab owns references to the service components and provides them through properties such as `SaiServer.Instance.DailyQuest` and `SaiServer.Instance.Leaderboard` ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).

## Prerequisites

- Unity Hub with Unity `6000.3.8f1`.

## Open and configure Unity

1. Open `ss-unity` in Unity Hub with the required Unity version.
2. Open [demo.unity](Assets/SaiGame/Scenes/demo.unity), or add [SaiServer.prefab](Assets/SaiGame/Prefabs/SaiServer.prefab) to your own scene.
3. Select the `SaiServer` object and set **Server Endpoint** to **Production HTTPS** for `ai.saigame.studio`. The custom inspector persists the selected endpoint in `PlayerPrefs` ([SaiServerEditor.cs](Assets/SaiGame/Scripts/Editor/SaiServerEditor.cs), [SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).
4. Set the Game ID for your SaiGame project.
5. Configure only the services required by the scene. Services with `autoLoadOnLogin` subscribe to successful login and retrieve their initial data automatically; Daily Quest is one example ([DailyQuest.cs](Assets/SaiGame/Scripts/5_Quest/Daily/DailyQuest.cs)).

## Runtime model

### Authentication and requests

`SaiAuth` publishes login and refresh events. Successful login starts token-expiration monitoring when auto-refresh is enabled ([SaiAuth.cs](Assets/SaiGame/Scripts/0_Auth/SaiAuth.cs)). `SaiServer` creates `UnityWebRequest` instances, sets the request timeout, and adds `Authorization: Bearer <token>` only for authenticated sessions ([SaiServer.cs](Assets/SaiGame/Scripts/SaiServer.cs)).

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
| Unity requests fail | Confirm the device can reach `ai.saigame.studio` and that **Production HTTPS** is selected. |
| Unity calls the wrong environment | Re-select **Production HTTPS** in the `SaiServer` inspector; the selection is stored in `PlayerPrefs`. |
| API returns unauthenticated errors | Authenticate through `SaiAuth` before calling protected services and verify the current Game ID. |
| Daily quest data is empty | Confirm the player is authenticated, the selected pool exists for the configured game, and the requested timeframe is valid. |
