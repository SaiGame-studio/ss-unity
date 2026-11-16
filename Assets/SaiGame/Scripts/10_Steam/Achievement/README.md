# Steam achievements

`SteamAchievements` provides built-in calls for Steam achievements and stats
through Steamworks.NET (`SteamUserStats`). It runs entirely on the client and
does not call the SaiGame backend.

Steam achievements are optional. Without Steamworks.NET, or on platforms other
than Windows, macOS, and Linux standalone, every call fails with
`STEAMWORKS_NET_MISSING`.

## Setup

1. Complete the Steamworks.NET part of the [Steam account link setup](../Link/README.md).
   Linking a Steam account is not required for achievements.
2. Define achievements (and stats for progress-based achievements) in the
   Steamworks partner site for the AppID, then publish the changes. Unpublished
   achievements are not visible to the client.
3. The `SaiServer` prefab already contains `SteamAchievements` on its `Steam`
   child object, wired to `SteamworksNetTicketProvider` on the same object. SteamAPI is prepared the same
   way as for Steam account link: a SteamAPI initialized by the game is reused and never
   shut down; otherwise the provider initializes it when
   **Initialize Steam Api** is on.
4. `SteamAPI.RunCallbacks` must run every frame (the provider does this when it
   initialized SteamAPI). Without it, calls end with `STEAM_STATS_TIMEOUT`.

## Usage

```csharp
using SaiGame.Services;

SteamAchievements achievements = SaiServer.Instance.SteamAchievements;

// Unlock by API name. Stats are loaded automatically on first use.
achievements.Unlock("ACH_WIN_ONE_GAME",
    apiName => UnityEngine.Debug.Log("Unlocked " + apiName),
    error => UnityEngine.Debug.LogWarning(error));

// Progress-based achievement driven by a stat defined in Steamworks.
achievements.SetStat("NumWins", 10);

// Steam progress notification only (does not unlock).
achievements.IndicateProgress("ACH_WIN_100_GAMES", 10, 100);

// Read state after loading.
achievements.LoadAchievements(list =>
{
    bool unlocked = achievements.IsUnlocked("ACH_WIN_ONE_GAME");
    if (achievements.TryGetStat("NumWins", out int wins)) { /* ... */ }
});
```

| Call | Purpose |
|---|---|
| `LoadAchievements` | Request the player's stats and list every achievement (API name, display name, description, hidden, achieved, unlock time). |
| `Unlock` | `SetAchievement` + `StoreStats`. Unlocking an achieved achievement succeeds without change. |
| `IndicateProgress` | Steam progress notification. Rejected when already unlocked or `current >= max`. |
| `SetStat` (int / float) | `SetStat` + `StoreStats`; refreshes achievements because Steam may unlock stat-based ones. |
| `TryGetStat` (int / float) | Read a stat after stats are loaded. |
| `IsUnlocked`, `GetAchievement`, `Achievements` | Cached state from the last load or change. |
| `Clear`, `ResetAll` | Testing only. Lock an achievement again, or reset all stats (and optionally achievements). |

Events: `OnAchievementsLoaded`, `OnAchievementUnlocked`, `OnFailure`.

## Error codes

| Code | Meaning |
|---|---|
| `STEAMWORKS_NET_MISSING` | Steamworks.NET not installed, or unsupported platform. |
| `STEAM_CLIENT_REQUIRED` | No `SteamworksNetTicketProvider` assigned. |
| `STEAM_NOT_INITIALIZED` / `STEAM_NOT_LOGGED_ON` | SteamAPI not running, or no Steam user signed in. |
| `STEAM_STATS_UNAVAILABLE` | Steam could not return stats for this AppID and account. |
| `STEAM_STATS_TIMEOUT` | No callback from Steam within **Stats Timeout Seconds**. |
| `STEAM_STATS_STORE_FAILED` | Steam rejected the store. |
| `STEAM_ACHIEVEMENT_NOT_FOUND` | Unknown API name or unpublished achievement. |
| `STEAM_ACHIEVEMENT_PROGRESS_REJECTED` | Progress notification rejected. |
| `STEAM_STAT_NOT_FOUND` | Unknown stat, wrong type, or a stat that only the game server may set. |
| `STEAM_INVALID_NAME` | Empty API or stat name. |

## Security note

Client-side achievements can be unlocked by a modified client, which is normal
for Steam games. Achievements that must not be cheated need a server-side
unlock through the Steam Web API; that is not part of this SDK.

## Inspector

In Play Mode the `SteamAchievements` Inspector loads the list, shows each
achievement as locked or unlocked with Unlock and Clear buttons, sends progress
notifications, gets and sets stats, and resets stats after a confirmation
dialog. The last error is shown with the next step.
