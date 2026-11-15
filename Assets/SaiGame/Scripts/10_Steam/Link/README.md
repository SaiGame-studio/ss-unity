# Steam account link

Steam is not a separate login. The Steam client running the game already
identifies the player, so the player never types Steam credentials. The only
login the player does is the Sai account (Google or password), once:

1. **Link (once).** The player signs in with Google or password, then the game
   calls `Link`. The Steam account running the game is linked to that
   Sai account.
2. **Every launch after.** The game calls `SteamLogin.SignIn`, which signs
   into the linked Sai account with no input ("Sign In With Linked Steam").
   `SteamLogin.CheckLinked` asks whether the running Steam account is linked
   without signing in.
3. **Unlink.** Only on Sai Play (account page). The SDK has no unlink. After
   an unlink, the Steam account cannot be linked to another Sai account, and
   the Sai account cannot link another Steam account, for 30 days
   (`STEAM_RELINK_COOLDOWN`). Unlinking revokes what is left of the Claim Game
   items; the next Sai account linked to that Steam account receives them.

Steam support is optional: the SDK compiles and runs without
Steamworks.NET; games that do not ship on Steam can ignore this folder.

## Setup in a game

1. **Backend (once per game).** In Sai Server, open the game's
   **Settings > Steam**, enter the Steam AppID, publisher Web API key, and
   Claim Game items, then enable Steam. The server must have migrations through
   273 and `GAME_SECRETS_ENCRYPTION_KEY` set (see
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
   to the project root. Do not edit it: the Editor reads the game's AppID from
   its public info (`steam_app_id`, set in Game Settings > Steam on the Sai
   Server dashboard) after each script reload and writes it into
   `steam_appid.txt` when it differs. `SteamLink` and `SteamLogin` also start SteamAPI as that
   AppID before the first ticket. Restart the Editor after the AppID changes if
   SteamAPI already started. A game that is not public yet returns no AppID
   unless a studio member is signed in; the file then keeps its last value.
3. **Components.** The `SaiServer` prefab has a `Steam` child object holding
   `SteamworksNetTicketProvider`, `SteamLink`, `SteamLogin`, and `SteamAchievements`,
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
// Asks whether the running Steam account is linked, without signing in.
SaiServer.Instance.SteamLogin.CheckLinked(
    linked => UnityEngine.Debug.Log(linked ? "linked" : "not linked: link it first"),
    error => UnityEngine.Debug.LogWarning(error));

// Signs into the Sai account linked to the running Steam account.
SaiServer.Instance.SteamLogin.SignIn(
    result => UnityEngine.Debug.Log("Signed in with linked Steam, claim game: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

Successful sign-in pushes the access token, refresh token, expiry, and user into
SaiServer/SaiAuth using the existing session storage behavior. Subscribe to
`OnSignedIn`, `OnTwoFactorRequired`, `OnClaimGameResult`, or `OnFailed`
as needed. `Ownership` and `ClaimGameResult` expose the latest backend result.

If `RequiresTwoFactor` is true, call `VerifyTwoFactor(code, ...)` on
`SteamLogin`, not on the separate password login component. The Steam
component completes the existing backend challenge and attempts Claim Game
with a fresh ticket before firing `OnSignedIn`. A failed Claim Game attempt
does not invalidate a successful sign-in; inspect `ClaimGameResult.status` and retry.

Steam never creates an account. If the SteamID is not linked yet, the error
callback receives `STEAM_NOT_LINKED`. Ask the player to sign in or register
with Google/password, then link Steam once:

```csharp
SaiServer.Instance.SteamLink.Link(
    result => UnityEngine.Debug.Log("Steam linked, claim game: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

Linking requires proof of the account owner, not only a session:

- Accounts with 2FA pass a code: `Link(onSuccess, onError, "123456")`,
  or a recovery code as the fourth argument. Without one the error is
  `STEAM_LINK_TWO_FACTOR_REQUIRED`; a wrong code is `TWO_FACTOR_INVALID_CODE`.
- Accounts without 2FA must have signed in with Google/password within the last
  10 minutes. A restored or refreshed old session gets
  `STEAM_LINK_REAUTH_REQUIRED`; sign in again, then link.

After linking, `SignIn` signs into that same account. Linking never merges
or transfers accounts. A conflict requires account
resolution; repeatedly retrying cannot overwrite the existing link.

## Claim Game

Claim Game grants the items the studio set in Game Settings > Steam to a player
whose Steam account owns the game. It runs automatically on link and on sign-in.
Call it to retry a pending Claim Game after freeing inventory space, or after
restoring an existing SaiAuth session:

```csharp
SaiServer.Instance.SteamLink.ClaimGame(
    result => UnityEngine.Debug.Log("Claim game: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

All operations get a fresh ticket and leave AppID, SteamID, eligibility, item
IDs, and quantities to the backend. Status values are `granted`,
`transferred`, `already_granted`, `pending`, `ineligible`, or `not_configured`.
Claim Game items belong to the Steam account: granted once per game, they move
with the Steam account. Unlinking (on Sai Play) revokes what is left of them, and
the next linked Sai account receives exactly that (status `transferred`); items
already used do not move. The backend checks the
inventory transaction log; there is no code to enter or send. Editing the items
does not grant again to players who already claimed the game. The JSON key of
the result is still `reward`. Family Sharing can authenticate with an active
license but cannot Claim Game. Steam key activations with permanent ownership
also qualify; the response is not a purchase receipt.

The Steam transport requires HTTPS (HTTP is allowed only for loopback local
development), validates TLS certificates, rejects redirects, and returns the
backend's `message_code` through error callbacks. It does not use SaiServer's
development certificate bypass or depend on a preloaded GamerProgress.

`SignInWithTicket(hexTicket, ...)` is available for external wrappers. The caller
owns cancellation of that ticket handle and must keep it valid until the
response. Without a configured provider, a completed 2FA login reports a
pending Claim Game; use a provider and `ClaimGame` to obtain it.

`Cancel` or disabling the component stops the current operation and
cancels provider-owned tickets. Requests that already reached the backend may
still commit; the claim endpoint is safe to retry. Cancellation does not log
out a previously authenticated SaiAuth session.

Steam request bodies and authentication responses are omitted from SaiServer's
JSON debug logging. Do not log raw ticket strings or token-bearing responses
in game callbacks.

## Inspector

Neither Inspector is a login form: they have no credential fields, and tokens
live in `SaiAuth`.

`SteamLink` walks through the link in order; each step is enabled only when the
previous one is done:

- **step 1: steam app id**: the AppID the Sai Server has for the game (set in
  Game Settings > Steam on the dashboard) and, in Play Mode, an error when
  SteamAPI runs as a different AppID.
- **step 2: sai server sign in**: whether a Sai account is signed in, with
  Password Login (`SaiAuth`) and Google Login (`GoogleBackendLogin`).
- **step 3: link steam**: Link Steam and Claim Game. A code field appears
  only when the server asks for one (2FA).

`SteamLogin` signs in with the linked account:

- **steam app id**: as in `SteamLink`.
- **sign in with linked steam**: Check Link asks whether the running Steam
  account is linked. Sign In With Linked Steam is enabled only when it is;
  otherwise the Inspector says the account is not linked, with a button to
  select `SteamLink`.

SteamAPI starts automatically on the first Steam request. Cancel appears only
while a request runs; **last result** and **steam settings** are collapsed
foldouts. The last error is shown with the next step to take, for example
`STEAM_NOT_LINKED` or `STEAM_LINK_REAUTH_REQUIRED`. The Inspector is the only
UI this SDK provides.