# ss-go Lua Script Contract

Package: ss-go-lua-ai-contract-pack v1.0.6
Ticket: P2-T120

This is the contract an AI agent must follow when generating Lua scripts for studio members.

## Runtime

| Rule | Value |
| --- | --- |
| Lua runtime | Lua 5.1 via gopher-lua |
| Execution timeout | 500 ms |
| Call stack depth | 200 frames |
| Script body size | 32 KB |
| Output keys | 64 |
| Log lines | 100 |
| Standard libraries | `base`, `table`, `string`, `math` |

Forbidden or unavailable builtins: `dofile`, `loadfile`, `load`, `loadstring`, `require()` (function call), `module`, `getfenv`, `setfenv`, `collectgarbage`, `string.dump`, filesystem, network, `os`, and `io`.

> **Library loading:** Use `require "libname"` at the top of a script to load a library. This is a **preprocessor directive** stripped by the server before Lua execution — not the Lua `require()` builtin.

## Script Shape

Script names must match `^[a-z][a-z0-9_]*$`.

Scripts read from `payload` and `ctx`, then write JSON-serializable values to `output`.

```lua
local attack = payload.attack or 0
local defense = payload.defense or 0

output.damage = math.max(0, attack - defense)
game.log("damage=" .. output.damage)
```

## Injected Globals

| Global | Purpose |
| --- | --- |
| `payload` | Request payload converted from JSON to Lua tables. |
| `ctx` | Server execution context with `player_id`, `game_id`, `studio_id`, `timestamp`, `script_version`, plus optional enriched data. |
| `output` | Result table collected by Go and returned in the run response. |
| `game` | Server-authoritative helper API table. |
| `print` | Alias for `game.log`. |

### `ctx` fields

| Field | Type | Description |
| --- | --- | --- |
| `ctx.player_id` | string (UUID) | Authenticated player running the script. |
| `ctx.game_id` | string (UUID) | Game the script belongs to. |
| `ctx.studio_id` | string (UUID) | Studio that owns the game. |
| `ctx.timestamp` | number | Unix epoch seconds at execution start. |
| `ctx.script_version` | number (integer) | Version number of the currently executing script definition. Use this to guard version-specific logic. |

Additional keys (e.g. `ctx.packs`, `ctx.item_definitions`) may be present when the caller requested server-side data enrichment via `payload.context`.

## Error Handling

Every `game.*` call that returns an error must be checked before returned data is used.

```lua
local item, err = game.get_item_def_by_id(payload.item_def_id)
if err ~= nil then
    output.error = err
    return
end

output.item_name = item.name
```

Single-value side-effect helpers return only `err`.

```lua
local err = game.grant_item(payload.item_def_id, 1)
if err ~= nil then
    output.error = err
    return
end
```

## Available `game.*` API

| Function | Return pattern | Notes |
| --- | --- | --- |
| `game.log(msg)` | none | Captures one log line. `print(msg)` is an alias. |
| `game.grant_item(item_def_id, amount)` | `err` | Grants an item definition to the current player. `amount` must be positive. |
| `game.deduct_item(item_def_id, amount)` | `err` | Deducts an item definition from the current player. `amount` must be positive. |
| `game.get_item_def_by_id(id)` | `table, err` | Fetches an item definition by UUID. |
| `game.get_item_def_by_code(code)` | `table, err` | Fetches an item definition by code. |
| `game.get_item_defs_by_ids(ids)` | `list, err` | Fetches multiple item definitions by UUID array — single DB query. |
| `game.get_item_defs_by_codes(codes)` | `list, err` | Fetches multiple item definitions by code array — single DB query. |
| `game.get_item_instance_by_id(id)` | `table, err` | Fetches a player inventory item instance by UUID. |
| `game.update_item_private_properties(item_id, version, props)` | `err` | Merges private properties. The `level` key is reserved. |
| `game.get_container_def_by_id(id)` | `table, err` | Fetches an item container definition by UUID. |
| `game.get_container_by_id(id)` | `table, err` | Fetches a player container by UUID. |
| `game.get_gacha_pack_by_id(id)` | `table, err` | Fetches a gacha pack definition by UUID. |
| `game.open_gacha_pack(pack_id [, container_id [, idempotency_key]])` | `table, err` | Opens one gacha pack for the authenticated player. |
| `game.get_quest_def_by_id(id)` | `table, err` | Fetches a quest definition by UUID. |
| `game.get_event_type_by_id(id_or_name)` | `table, err` | Fetches an event type by UUID or name. |
| `game.get_event_type_by_name(name)` | `table, err` | Fetches an event type by name. |
| `game.get_entity_def_by_id(id)` | `table, err` | Fetches an entity definition by UUID. |
| `game.get_entity_def_by_key(key)` | `table, err` | Fetches an entity definition by key. |
| `game.entity_pool_random(pool_key)` | `table, err` | Weighted random entity from a pool. |
| `game.entity_pool_min(pool_key, stat_key [, count])` | `table|list, err` | Lowest stat entity or list. `count` max is 100. |
| `game.entity_pool_max(pool_key, stat_key [, count])` | `table|list, err` | Highest stat entity or list. `count` max is 100. |
| `game.get_entity_pool_def_by_id(id)` | `table, err` | Fetches an entity pool definition by UUID. |
| `game.get_entity_pool_def_by_key(pool_key)` | `table, err` | Fetches an entity pool definition by key. |
| `game.get_preset_def_by_id(id)` | `table, err` | Fetches a preset definition by UUID. |
| `game.get_preset_by_id(id)` | `table, err` | Fetches a preset instance by UUID. |
| `game.get_preset_slots(preset_id)` | `list, err` | Fetches preset slots. |
| `game.get_equipped_in_slot(slot_key)` | `table, err` | Fetches the player's equipped item in a slot. |
| `game.battle_session_create(state)` | `session_id, err` | Creates an active battle session. |
| `game.battle_session_current_id()` | `session_id, err` | Returns the current active battle session ID for the script's game/player context. |
| `game.battle_session_get(session_id)` | `table, err` | Reads battle session state. |
| `game.battle_session_update(session_id, state)` | `err` | Overwrites battle state. |
| `game.battle_session_end(session_id [, end_data])` | `err` | Ends a battle session. |
| `game.battle_session_flee(session_id)` | `err` | Marks a battle session as fled. |
| `game.open_entity_drop_packs(session_id, entity_def_id, pack_ids)` | `list, err` | Opens enemy drop packs. Max 7 pack IDs. |

## Library Scripts & `require` Directives

A **library script** is a `ScriptDefinition` with `is_library = true`. Libraries may only define Lua functions — no top-level executable statements, no `require` directives.

A **regular script** may declare `require` directives at the very top of its body:

```lua
require "math_utils"
require 'combat_helpers'

local dmg = math_utils.clamp(payload.attack - payload.defense, 0, 999)
output.damage = combat_helpers.apply_crit(dmg, payload.crit_rate)
```

> These are **preprocessor directives** — the server strips them and loads the library before passing code to the Lua VM. They are **not** the Lua `require()` builtin (which is disabled).

### Rules

| Rule | Detail |
| --- | --- |
| Syntax | `require "<libname>"` or `require '<libname>'` — one per line, at the top of the file |
| Library name | Must match `^[a-z][a-z0-9_]*$` |
| Access pattern | `libname.func(args)` — each library is exposed as a sandboxed global table |
| Nesting | Libraries cannot include other libraries |
| Cap | No separate limit (subject to the game's total script quota) |
| Scope | Libraries are game-scoped; a script may only include libraries in the same game |

### Library authoring

```lua
-- math_utils (is_library = true)
function clamp(v, lo, hi)
    if v < lo then return lo end
    if v > hi then return hi end
    return v
end

function lerp(a, b, t)
    return a + (b - a) * t
end
```

Only function definitions are allowed at the top level. Non-function values (numbers, strings, tables) will be rejected at save time.

---

## Agent Rejection Rules

Reject or rewrite scripts that:

- Call any function not listed above.
- Call `require(...)` as a Lua function (use `require "libname"` directives instead).
- Use `os`, `io`, filesystem, network, dynamic code loading, or bytecode APIs.
- Depend on wall-clock randomness for security-critical outcomes without a server-provided seed.
- Modify data outside `output` unless using an explicit documented side-effect helper.
- Ignore `err` from `game.*` before reading returned data.
- Use `require` directives inside a library script.
- Define non-function top-level values in a library script.
