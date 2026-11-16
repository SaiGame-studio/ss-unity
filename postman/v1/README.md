# SaiGame Unity API — Postman (debug)

A Postman collection of every HTTP API the SaiGame Unity package (`Assets/SaiGame/Scripts`, v0.2.60c) calls.
Use it to call an API by hand, inspect the real response and compare it with what your game receives. Paths,
methods, headers and request bodies match the C# code; each request names the Unity method that calls it and has
saved example responses.

| File | Content |
| --- | --- |
| `SaiGame.unity-api.postman_collection.json` | All APIs grouped by feature (94 requests) |
| `SaiGame.unity-api.postman_environment.json` | Environment template |

## Quick start

1. Import both files into Postman and select the `SaiGame Unity API` environment.
2. Set `api_url` (default `https://api.saigame.studio`, no `/api` suffix), `game_id`, `username`, `password`.
3. Send **01 Auth > Login** (or Register first). The token is saved to `access_token` automatically. If the
   account has 2FA, Login saves `challenge_token`; send **Verify 2FA login** with your authenticator code.
4. Send **02 Gamer Progress > Get my gamer progress** (or Create). The Unity client refuses every game API until
   gamer progress is loaded (`SaiServer.ValidateGamerProgressRequest`).
5. Call any other request. List requests save the first result's ID (`shop_id`, `container_id`,
   `inventory_item_id`, `mailbox_message_id`, ...) for the detail and action requests; edit the variable to
   target another one.

## Notes

- Fill in when needed: `recipe_key`, `gacha_pack_id`, `gacha_code`, `gift_code`, `preset_definition_id`,
  `battle_script_name`.
- Steam: set `steam_ticket` to a fresh ticket from a running Steam client before each request (single-use).
- Google login: send **Create Google login session**, open `auth_url` in a browser, sign in, then send **Poll**.
- Lua scripts: log in with a studio member account first; player accounts are rejected.
- Purchases, crafting, gacha, claims and **Delete my gamer progress** change real data — debug on a test game.
- When a Unity API call is added or changed, update the matching request here.
