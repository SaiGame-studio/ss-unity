# SaiGame API request: item definition and server time

## Purpose

`Assets/_sg03` must not modify the SaiGame package. Please add the APIs below to the SaiGame package and release a version containing them. The SG03 UI already calls these APIs, so their absence currently produces compilation errors.

## Affected SG03 callers

| SaiGame API | SG03 callers |
| --- | --- |
| `PlayerItem.GetItemDefinition(...)` | `UI/Quest/MainQuest/MainQuestContentUI.cs:374` |
| `SaiServer.HasServerTime` | `UI/Components/ServerTimeLabelComponent.cs:108`; `UI/Quest/DailyQuest/DailyQuestContentUI.cs:397` |
| `SaiServer.CurrentServerTime` | `UI/Components/ServerTimeLabelComponent.cs:115`; `UI/Quest/DailyQuest/DailyQuestContentUI.cs:399` |

The compiler message for `GetItemDefinition` was reported twice; it is one missing API.

## 1. `SaiServer`: expose server time

**Target:** `Assets/SaiGame/Scripts/SaiServer.cs` (`SaiGame.Services.SaiServer`)

The server already obtains `/api/v1/time` after a successful login. Please retain the received timestamp and a local realtime baseline, then expose the following public read-only properties:

```csharp
public bool HasServerTime => this.serverTimestamp > 0;

public DateTime CurrentServerTime => !this.HasServerTime
    ? DateTime.MinValue
    : this.serverTimeAtSync.AddSeconds(Time.realtimeSinceStartup - this.serverTimeSyncRealtime);
```

Required backing state and update when `/api/v1/time` succeeds:

```csharp
[NonSerialized] private DateTime serverTimeAtSync;
[NonSerialized] private float serverTimeSyncRealtime;

this.serverTime = response.server_time ?? string.Empty;
this.serverTimestamp = response.timestamp;
this.serverTimezone = response.timezone ?? string.Empty;
this.serverTimeAtSync = this.ParseServerTime(response.server_time, response.timestamp);
this.serverTimeSyncRealtime = Time.realtimeSinceStartup;
```

`ParseServerTime` should parse the ISO server-time string with `DateTimeOffset.TryParse`; if that fails, fall back to `DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime`.

**Behavior expected by SG03:**

- Before a valid server-time response exists, `HasServerTime` is `false` and `CurrentServerTime` is `DateTime.MinValue`.
- After synchronization, `CurrentServerTime` advances using `Time.realtimeSinceStartup`, rather than staying at the time of the HTTP response.

## 2. `PlayerItem`: fetch one item definition

**Target:** `Assets/SaiGame/Scripts/3_ItemContainer/Item/PlayerItem.cs` (`SaiGame.Services.PlayerItem`)

Please add this public API:

```csharp
/// <summary>
/// Fetches one item definition by ID.
/// Endpoint: GET /api/v1/games/{gameId}/items/{itemDefinitionId}
/// </summary>
public void GetItemDefinition(
    string itemDefinitionId,
    Action<ItemDefinitionData> onSuccess = null,
    Action<string> onError = null)
{
    if (SaiServer.Instance == null)
    {
        onError?.Invoke("SaiServer not found!");
        return;
    }

    if (!SaiServer.Instance.IsAuthenticated)
    {
        onError?.Invoke("Not authenticated! Please login first.");
        return;
    }

    if (string.IsNullOrEmpty(itemDefinitionId))
    {
        onError?.Invoke("itemDefinitionId cannot be empty.");
        return;
    }

    StartCoroutine(this.GetItemDefinitionCoroutine(itemDefinitionId, onSuccess, onError));
}
```

The coroutine should issue:

```csharp
string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/items/{itemDefinitionId}";
```

On a successful response, deserialize to `ItemDefinitionData` and invoke `onSuccess`. The backend may return either a direct item-definition object or wrap it in one of these object properties: `item_definition`, `item`, or `data`; support all four shapes. On HTTP or parsing failure, invoke `onError` with a useful message.

## Acceptance checks

- `MainQuestContentUI` compiles and can resolve an item reward's definition by ID.
- `ServerTimeLabelComponent` and `DailyQuestContentUI` compile.
- Before login/time sync, SG03 shows its placeholder time without an exception.
- After login/time sync, the displayed time increments once per second and daily-quest date logic uses the server date.
