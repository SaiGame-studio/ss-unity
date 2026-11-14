# Steam backend login

Task source: user request, 2026-11-12; no external ticket ID.

## Setup

1. Enable the backend as described in `ss-go/docs/api/steam-login-rewards.md`:
   migrations through 271 and the server game-secret encryption key. In Studio,
   open the game **Settings > Steam**, configure its AppID, publisher key,
   and reward items, then enable Steam. Configuration is stored per game in the
   database and applies without a backend restart.
2. Import [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) in the
   consuming game and add `SAI_STEAMWORKS_NET` to its scripting define symbols.
   This SDK does not install or initialize a native Steam wrapper for you.
3. The host game must initialize SteamAPI using its AppID and pump
   `SteamAPI.RunCallbacks`. Use a Steam account with a valid license and a build
   launched through Steam, or the Steamworks development setup for that AppID.
4. Add `SteamBackendLogin` to the SaiServer object or a child, then add
   `SteamworksNetTicketProvider` and assign it to the login component's
   **Ticket Provider** field. Assign the login component to SaiServer's
   **Steam Backend Login** field if it is added after SaiServer initializes.
5. Set SaiServer's Game ID to the **backend game UUID**, not the numeric Steam
   AppID. Do not put a publisher key in the game, Inspector, or client `.env`.

The adapter is compiled only with `SAI_STEAMWORKS_NET` on supported desktop
platforms. The rest of the SDK compiles without Steamworks.NET. If you use a
different Steam wrapper, implement `ISteamTicketProvider` on a component and
assign it instead. Request `GetAuthTicketForWebApi` with identity `ss-go`, wait
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

For an existing Google/password account, sign into that account first:

```csharp
SaiServer.Instance.SteamBackendLogin.LinkAccount(
    result => UnityEngine.Debug.Log("Steam linked: " + result.reward.status),
    error => UnityEngine.Debug.LogWarning(error));
```

Link before direct Steam login to avoid creating a separate Steam account.
Linking never merges or transfers accounts. A conflict requires account
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

## Validation and remaining setup

The SDK and optional adapter were compiled against the project's Unity
references and upstream Steamworks.NET source. No scene/prefab was edited and
no native Steam login was executed. The consumer must install the wrapper,
wire components, configure the actual AppID/backend rewards, and verify login
using its Steamworks account before release. No tests were written or run.
