-- Usage: create or update this file as a backend Lua script, then run it through the script API.
-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
-- Headers:
--   Authorization: Bearer {access_token}
--   Content-Type: application/json
-- Example request body:
-- {
--   "payload": {
--     "enemy_entity_id": "enemy-definition-uuid",
--     "enemy_entity_key": "enemy_key",
--     "enemy_pool_key": "enemy_pool_key",
--     "battle_data": {}
--   }
-- }
-- Provide one enemy selector: enemy_entity_id, enemy_entity_key, or enemy_pool_key.

local enemy = nil
local err = nil

if payload.enemy_entity_id ~= nil and payload.enemy_entity_id ~= "" then
    enemy, err = game.get_entity_def_by_id(payload.enemy_entity_id)
elseif payload.enemy_entity_key ~= nil and payload.enemy_entity_key ~= "" then
    enemy, err = game.get_entity_def_by_key(payload.enemy_entity_key)
elseif payload.enemy_pool_key ~= nil and payload.enemy_pool_key ~= "" then
    enemy, err = game.entity_pool_random(payload.enemy_pool_key)
else
    output.error = "enemy_entity_id, enemy_entity_key, or enemy_pool_key is required"
    return
end

if err ~= nil then
    output.error = err
    return
end

if enemy == nil then
    output.error = "enemy not found"
    return
end

local state = {
    player_id = ctx.player_id,
    enemy = enemy,
    turn = 1,
    status = "active",
    started_at = ctx.timestamp,
    data = payload.battle_data or {}
}

local session_id, create_err = game.battle_session_create(state)
if create_err ~= nil then
    output.error = create_err
    return
end

output.session_id = session_id
output.status = state.status
output.turn = state.turn
output.enemy = enemy