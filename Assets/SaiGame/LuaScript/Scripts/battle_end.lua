-- Usage: create or update this file as a backend Lua script, then run it through the script API.
-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
-- Headers:
--   Authorization: Bearer {access_token}
--   Content-Type: application/json
-- Example request body:
-- {
--   "payload": {
--     "session_id": "battle-session-uuid",
--     "winner": "player",
--     "reason": "completed",
--     "turn": 3
--   }
-- }

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

local winner = payload.winner or state.winner or "player"
local end_data = {
    winner = winner,
    reason = payload.reason or "completed",
    turn = state.turn or payload.turn or 1,
    ended_at = ctx.timestamp,
    state = state
}

local end_err = game.battle_session_end(payload.session_id, end_data)
if end_err ~= nil then
    output.error = end_err
    return
end

output.session_id = payload.session_id
output.status = "ended"
output.winner = winner
output.reason = end_data.reason
output.turn = end_data.turn