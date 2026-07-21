---@meta ss-go-game-api

-- Editor-only contract stub for ss-go studio Lua scripts.
-- This file documents globals injected by the server runtime. It is not executed by production code.

---@alias UUID string
---@alias LuaError string|nil
---@alias LuaMap table<string, any>
---@alias LuaList any[]

---@class SSGameDetail
---@field id UUID
---@field studio_id UUID
---@field name string
---@field description string|nil
---@field tags string[]
---@field status "development"|"alpha"|"beta"|"released"|"archived"
---@field is_active boolean
---@field limits LuaMap   -- effective resource limits (system defaults + plugin boosts)
---@field usage LuaMap    -- current usage counters per resource key
---@field settings LuaMap -- studio-configurable game-level options (JSONB)
---@field created_at number
---@field updated_at number

---@class SSContext
---@field player_id UUID
---@field game_id UUID
---@field studio_id UUID
---@field timestamp number
---@field script_version number Integer version of the currently executing script definition.
---@field game SSGameDetail Full detail of the current game (always present when server wires gameRepo).
---@field [string] any

---@class SSGameAPI
local SSGameAPI = {}

---Append a message to the execution log. Max 100 lines.
---@param msg any
function SSGameAPI.log(msg) end

---Grant item units to the authenticated player.
---@param item_def_id UUID
---@param amount integer
---@return LuaError err
function SSGameAPI.grant_item(item_def_id, amount) end

---Deduct item units from the authenticated player.
---@param item_def_id UUID
---@param amount integer
---@return LuaError err
function SSGameAPI.deduct_item(item_def_id, amount) end

---Fetch an item definition by UUID.
---@param id UUID
---@return LuaMap|nil item_def
---@return LuaError err
function SSGameAPI.get_item_def_by_id(id) end

---Fetch an item definition by code.
---@param code string
---@return LuaMap|nil item_def
---@return LuaError err
function SSGameAPI.get_item_def_by_code(code) end

---Fetch multiple item definitions by UUID array in a single DB query.
---@param ids UUID[]
---@return LuaList|nil item_defs
---@return LuaError err
function SSGameAPI.get_item_defs_by_ids(ids) end

---Fetch multiple item definitions by code array in a single DB query.
---@param codes string[]
---@return LuaList|nil item_defs
---@return LuaError err
function SSGameAPI.get_item_defs_by_codes(codes) end

---Fetch a player inventory item instance by UUID.
---@param id UUID
---@return LuaMap|nil item
---@return LuaError err
function SSGameAPI.get_item_instance_by_id(id) end

---Merge private properties into an inventory item. The level key is reserved.
---@param item_id UUID
---@param version integer
---@param props LuaMap
---@return LuaError err
function SSGameAPI.update_item_private_properties(item_id, version, props) end

---Fetch an item container definition by UUID.
---@param id UUID
---@return LuaMap|nil container_def
---@return LuaError err
function SSGameAPI.get_container_def_by_id(id) end

---Fetch a player item container by UUID.
---@param id UUID
---@return LuaMap|nil container
---@return LuaError err
function SSGameAPI.get_container_by_id(id) end

---Fetch a gacha pack definition by UUID.
---@param id UUID
---@return LuaMap|nil pack
---@return LuaError err
function SSGameAPI.get_gacha_pack_by_id(id) end

---Open one gacha pack for the authenticated player.
---@param pack_id UUID
---@param container_id? UUID
---@param idempotency_key? string
---@return LuaMap|nil result
---@return LuaError err
function SSGameAPI.open_gacha_pack(pack_id, container_id, idempotency_key) end

---Fetch a quest definition by UUID.
---@param id UUID
---@return LuaMap|nil quest_def
---@return LuaError err
function SSGameAPI.get_quest_def_by_id(id) end

---Fetch an event type by UUID or name.
---@param id_or_name string
---@return LuaMap|nil event_type
---@return LuaError err
function SSGameAPI.get_event_type_by_id(id_or_name) end

---Fetch an event type by name.
---@param name string
---@return LuaMap|nil event_type
---@return LuaError err
function SSGameAPI.get_event_type_by_name(name) end

---Fetch an entity definition by UUID.
---@param id UUID
---@return LuaMap|nil entity_def
---@return LuaError err
function SSGameAPI.get_entity_def_by_id(id) end

---Fetch an entity definition by key.
---@param key string
---@return LuaMap|nil entity_def
---@return LuaError err
function SSGameAPI.get_entity_def_by_key(key) end

---Pick one weighted random entity from an entity pool.
---@param pool_key string
---@return LuaMap|nil entity
---@return LuaError err
function SSGameAPI.entity_pool_random(pool_key) end

---Get the entity or entities with the lowest stat value in a pool. Count defaults to 1 and is capped at 100.
---@param pool_key string
---@param stat_key string
---@param count? integer
---@return LuaMap|LuaList|nil result
---@return LuaError err
function SSGameAPI.entity_pool_min(pool_key, stat_key, count) end

---Get the entity or entities with the highest stat value in a pool. Count defaults to 1 and is capped at 100.
---@param pool_key string
---@param stat_key string
---@param count? integer
---@return LuaMap|LuaList|nil result
---@return LuaError err
function SSGameAPI.entity_pool_max(pool_key, stat_key, count) end

---Fetch an entity pool definition by UUID.
---@param id UUID
---@return LuaMap|nil pool_def
---@return LuaError err
function SSGameAPI.get_entity_pool_def_by_id(id) end

---Fetch an entity pool definition by key.
---@param pool_key string
---@return LuaMap|nil pool_def
---@return LuaError err
function SSGameAPI.get_entity_pool_def_by_key(pool_key) end

---Fetch a preset definition by UUID.
---@param id UUID
---@return LuaMap|nil preset_def
---@return LuaError err
function SSGameAPI.get_preset_def_by_id(id) end

---Fetch a preset instance by UUID.
---@param id UUID
---@return LuaMap|nil preset
---@return LuaError err
function SSGameAPI.get_preset_by_id(id) end

---Fetch all slots for a preset instance.
---@param preset_id UUID
---@return LuaList|nil slots
---@return LuaError err
function SSGameAPI.get_preset_slots(preset_id) end

---Fetch the authenticated player's equipped item in a slot.
---@param slot_key string
---@return LuaMap|nil item
---@return LuaError err
function SSGameAPI.get_equipped_in_slot(slot_key) end

---Create an active battle session.
---@param state LuaMap
---@return UUID|nil session_id
---@return LuaError err
function SSGameAPI.battle_session_create(state) end

---Return the current active battle session ID for the script's game/player context.
---@return UUID|nil session_id
---@return LuaError err
function SSGameAPI.battle_session_current_id() end

---Read battle session state.
---@param session_id UUID
---@return LuaMap|nil state
---@return LuaError err
function SSGameAPI.battle_session_get(session_id) end

---Overwrite battle session state.
---@param session_id UUID
---@param state LuaMap
---@return LuaError err
function SSGameAPI.battle_session_update(session_id, state) end

---End a battle session.
---@param session_id UUID
---@param end_data? LuaMap
---@return LuaError err
function SSGameAPI.battle_session_end(session_id, end_data) end

---Mark a battle session as fled.
---@param session_id UUID
---@return LuaError err
function SSGameAPI.battle_session_flee(session_id) end

---Open all configured entity drop packs for a defeated entity. Max 7 pack IDs.
---@param session_id UUID
---@param entity_def_id UUID
---@param pack_ids UUID[]
---@return LuaList|nil results
---@return LuaError err
function SSGameAPI.open_entity_drop_packs(session_id, entity_def_id, pack_ids) end

---@type LuaMap
payload = payload

---@type SSContext
ctx = ctx

---@type LuaMap
output = output

---@type SSGameAPI
game = game

---Alias for game.log.
---@param msg any
function print(msg) end

return SSGameAPI
