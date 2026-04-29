-- Usage: create or update this file as a backend Lua script, then run it through the script API.
-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
-- Headers:
--   Authorization: Bearer {access_token}
--   Content-Type: application/json
-- Example request body:
-- {
--   "payload": {
--     "session_id": "battle-session-uuid",
--     "action": "attack",
--     "damage": 5,
--     "enemy_hp": 20
--   }
-- }
-- Use action "flee" to mark the battle session as fled.

if payload.session_id == nil or payload.session_id == "" then
    output.error = "session_id is required"
    return
end

local state, get_err = game.battle_session_get(payload.session_id)
if get_err ~= nil then
    output.error = get_err
    return
end

if state == nil then
    output.error = "battle session not found"
    return
end

local action = payload.action or "attack"
if action == "flee" then
    local flee_err = game.battle_session_flee(payload.session_id)
    if flee_err ~= nil then
        output.error = flee_err
        return
    end

    output.status = "fled"
    output.session_id = payload.session_id
    return
end

local damage = payload.damage or 1
if damage < 0 then
    damage = 0
end

state.turn = (state.turn or 1) + 1
state.enemy_hp = math.max(0, (state.enemy_hp or payload.enemy_hp or 1) - damage)
state.last_action = action
state.last_damage = damage
state.updated_at = ctx.timestamp

if state.enemy_hp <= 0 then
    state.status = "ended"
    local end_data = {
        winner = "player",
        turn = state.turn,
        ended_at = ctx.timestamp
    }

    local end_err = game.battle_session_end(payload.session_id, end_data)
    if end_err ~= nil then
        output.error = end_err
        return
    end
else
    state.status = "active"
    local update_err = game.battle_session_update(payload.session_id, state)
    if update_err ~= nil then
        output.error = update_err
        return
    end
end

output.session_id = payload.session_id
output.status = state.status
output.turn = state.turn
output.enemy_hp = state.enemy_hp
output.damage = damage