# Steam backend login

Steam login is optional. The SDK compiles and runs without Steamworks.NET;
games that do not ship on Steam need no setup and can ignore this folder.

## Setup in a game

1. **Backend (once per game).** In Sai Server, open the game's
   **Settings > Steam**, enter the Steam AppID, publisher Web API key, and
   reward items, then enable Steam. The server must have migrations through
   271 and `GAME_SECRETS_ENCRYPTION_KEY` set (see
   `ss-go/docs/api/steam-login-rewards.md`). Never put the publisher key in the
   game, the Inspector, or a client `.env`.
2. **Steamworks.NET.** The SDK does not bundle a Steam wrapper. In the game
   project, add the UPM package `com.rlabrecque.steamworks.net`:

   ```json
   "com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.165.0"
   ```

   The package adds the `STEAMWORKS_NET` scripting define (`SAI_STEAMWORKS_NET`
   is also accepted). Save the project afterwards so the define is written to
   `ProjectSettings`. On first import it writes `steam_appid.txt` (AppID 480)
   to the project root; put the game's own AppID there for Editor testing.
3. **Components.** The `SaiServer` prefab has a `Steam` child object holding
   `SteamworksNetTicketProvider`, `SteamBackendLogin`, and `SteamAchievements`,
   already wired to each other and to `SaiServer`. Nothing needs to be wired.
   All Steam scripts live in `Scripts/10_Steam/`.
4. **SteamAPI.** Nothing happens at startup. On the first Steam request the
   provider reuses a SteamAPI the game has already initialized; it never
   initializes it twice and never shuts down a SteamAPI it did not start. If the
   game has no Steam manager, the provider initializes SteamAPI itself and pumps
   `SteamAPI.RunCallbacks`. Turn off **Initialize Steam Api** to require the
   game to initialize it (requests then fail with `STEAM_NOT_INITIALIZED`).
   `SteamAPI.RestartAppIfNecessary` remains the game's responsibility.
5. **Game ID.** SaiServer's Game ID is the **backend game UUID**, not the
   numeric Steam AppID.
6. **Testing.** Run the Steam client signed in with an account that owns the
   AppID, or launch the build through Steam.

`SteamworksNetTicketProvider` always compiles, so the prefab never shows a
missing script. Without Steamworks.NET, or on platforms other than Windows,
macOS, and Linux standalone, every ticket request fails with
`STEAMWORKS_NET_MISSING`. To use a different Steam wrapper, implement
`ISteamTicketProvider` on a component and assign it to the **Ticket Provider**
field instead. Request `GetAuthTicketForWebApi` with identity `ss-go`, wait
for the ticket callback, and return hex-encoded ticket bytes. Do not use
`GetAuthSessionTicket` for this flow.

## Usage

```csharp
using SaiGame.Services;

// SaiServer and the Steam components must already be initialized.
SaiServer.Instance.SteamBackendLogin.StartLogin(
    result => UnityEngine.Debug.Log("Steam login completed: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

Successful login pushes the access token, refresh token, expiry, and user into
SaiServer/SaiAuth using the existing session storage behavior. Subscribe to
`OnLoginSuccess`, `OnTwoFactorRequired`, `OnRewardResult`, or `OnLoginFailure`
as needed. `Ownership` and `Reward` expose the latest backend result.

If `RequiresTwoFactor` is true, call `VerifyTwoFactor(code, ...)` on
`SteamBackendLogin`, not on the separate password login component. The Steam
component completes the existing backend challenge and attempts the reward
with a fresh ticket before firing `OnLoginSuccess`. A failed reward attempt
does not invalidate a successful login; inspect `Reward.status` and retry.

Steam login never creates an account. If the SteamID is not linked yet, the
error callback receives `STEAM_NOT_LINKED`. Ask the player to sign in or
register with Google/password, then link Steam once:

```csharp
SaiServer.Instance.SteamBackendLogin.LinkAccount(
    result => UnityEngine.Debug.Log("Steam linked: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

Linking requires proof of the account owner, not only a session:

- Accounts with 2FA pass a code: `LinkAccount(onSuccess, onError, "123456")`,
  or a recovery code as the fourth argument. Without one the error is
  `STEAM_LINK_TWO_FACTOR_REQUIRED`; a wrong code is `TWO_FACTOR_INVALID_CODE`.
- Accounts without 2FA must have signed in with Google/password within the last
  10 minutes. A restored or refreshed old session gets
  `STEAM_LINK_REAUTH_REQUIRED`; sign in again, then link.

After linking, `StartLogin` signs into that same account. Linking never merges
or transfers accounts. A conflict requires account
resolution; repeatedly retrying cannot overwrite the existing link.

Retry a pending reward after freeing inventory space, or check rewards after
restoring an existing SaiAuth session:

```csharp
SaiServer.Instance.SteamBackendLogin.ClaimRewards(
    result => UnityEngine.Debug.Log("Steam reward: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

All operations get a fresh ticket and leave AppID, SteamID, eligibility, item
IDs, and quantities to the backend. Status values are `granted`,
`already_granted`, `pending`, `ineligible`, or `not_configured`.
Each Steam owner receives the configured items once per game. The backend checks
the inventory transaction log; there is no reward code to enter or send. Editing
reward items does not grant again to owners who already received their reward.
Family Sharing can authenticate with an active license but cannot claim the
permanent ownership reward. Steam key activations with permanent ownership
also qualify; the response is not a purchase receipt.

The Steam transport requires HTTPS (HTTP is allowed only for loopback local
development), validates TLS certificates, rejects redirects, and returns the
backend's `message_code` through error callbacks. It does not use SaiServer's
development certificate bypass or depend on a preloaded GamerProgress.

`LoginWithTicket(hexTicket, ...)` is available for external wrappers. The caller
owns cancellation of that ticket handle and must keep it valid until the
response. Without a configured provider, a completed 2FA login reports a
pending reward; use a provider and `ClaimRewards` to obtain it.

`CancelLogin` or disabling the component stops the current operation and
cancels provider-owned tickets. Requests that already reached the backend may
still commit; the claim endpoint is safe to retry. Cancellation does not log
out a previously authenticated SaiAuth session.

Steam request bodies and authentication responses are omitted from SaiServer's
JSON debug logging. Do not log raw ticket strings or token-bearing responses
in game callbacks.

## Inspector

`SteamBackendLogin` follows the same layout as `GoogleBackendLogin`:

- **authentication data**: access token, refresh token, expiry, user, and
  login time of the last Steam login.
- **steam state**: operation in progress, pending 2FA, ownership, reward, and
  the last error code.
- **steam settings**: ticket provider and ticket timeout.
- **steam client** (Play Mode): Steamworks availability, SteamAPI state, local
  SteamID, with Initialize Steam and Refresh Steam State buttons.
- **actions**: Login With Steam, Cancel Login, Clear Auth Data, Set Token To
  SaiAuth, Claim Rewards.
- **two factor login**: shown when Steam login returns `TWO_FACTOR_REQUIRED`.
- **link steam to signed in account**: Authenticator code, recovery code, and
  Link Steam. Sign in first with the `GoogleBackendLogin` or `SaiAuth`
  Inspector.

The last error is shown with the next step to take, for example
`STEAM_NOT_LINKED` or `STEAM_LINK_REAUTH_REQUIRED`. The Inspector is the only
UI this SDK provides.
