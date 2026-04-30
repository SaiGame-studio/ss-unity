-- Usage: create or update this file as a backend Lua script, then run it through the script API.
-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
-- Headers:
--   Authorization: Bearer {access_token}
--   Content-Type: application/json
-- Example request body:
-- {
--   "payload": {
--     "session_id": "battle-session-uuid",
--     "target": "alpha",
--     "hp": -10
--   }
-- }
-- target: "alpha" or "omega"
-- hp: positive value heals the target, negative value damages the target

local validate_payload  -- forward declaration
local load_session      -- forward declaration
local apply_hp_delta    -- forward declaration

local function main()
    local err = validate_payload()
    if err ~= nil then output.error = err ; return end

    local state, load_err = load_session(payload.session_id)
    if load_err ~= nil then output.error = load_err ; return end

    local result, apply_err = apply_hp_delta(state, payload.target, payload.hp)
    if apply_err ~= nil then output.error = apply_err ; return end

    output.session_id = payload.session_id
    output.target     = payload.target
    output.hp_before  = result.hp_before
    output.hp_after   = result.hp_after
    output.hp_delta   = payload.hp
end

-- ─── Functions ───────────────────────────────────────────────────────────────

validate_payload = function()
    if payload.session_id == nil or payload.session_id == "" then
        return "session_id is required"
    end
    if payload.target ~= "alpha" and payload.target ~= "omega" then
        return "target must be 'alpha' or 'omega'"
    end
    if payload.hp == nil then
        return "hp is required"
    end
    return nil
end

load_session = function(session_id)
    local state, err = game.battle_session_get(session_id)
    if err ~= nil then return nil, err end
    if state == nil then return nil, "battle session not found" end
    return state, nil
end

apply_hp_delta = function(state, target, hp_delta)
    local current_hp, new_hp

    if target == "alpha" then
        current_hp      = state.alpha_hp or 0
        new_hp          = current_hp + hp_delta
        state.alpha_hp  = new_hp
    elseif target == "omega" then
        current_hp      = state.omega_hp or 0
        new_hp          = current_hp + hp_delta
        state.omega_hp  = new_hp
    end

    state.last_debug_target   = target
    state.last_debug_hp_delta = hp_delta
    state.updated_at          = ctx.timestamp

    local err = game.battle_session_update(payload.session_id, state)
    if err ~= nil then return nil, err end

    return { hp_before = current_hp, hp_after = new_hp }, nil
end

main()