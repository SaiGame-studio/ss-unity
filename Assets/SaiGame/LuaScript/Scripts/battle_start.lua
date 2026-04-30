-- Usage: create or update this file as a backend Lua script, then run it through the script API.
-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
-- Headers:
--   Authorization: Bearer {access_token}
--   Content-Type: application/json
-- Example request body:
-- {
--   "payload": {
--     "battle_mode": "fast" | "normal" | "long",
--     "enemy_entity_key": "enemy_key",
--     "preset_instance_id": "preset-uuid"
--   }
-- }

-- Deck size limits — shared with player deck validation
local DECK_CARD_MIN = 25
local DECK_CARD_MAX = 52

local check_enemy            -- forward declaration
local verify_player_preset   -- forward declaration
local validate_payload       -- forward declaration
local resolve_enemy          -- forward declaration
local resolve_mode           -- forward declaration
local build_state            -- forward declaration
local load_player_the_source -- forward declaration
local load_enemy_the_source  -- forward declaration

local function main()
    local err = validate_payload()
    if err ~= nil then output.error = err ; return end

    local enemy, fetch_err = resolve_enemy()
    if fetch_err ~= nil then output.error = fetch_err ; return end

    local check_err = check_enemy(enemy)
    if check_err ~= nil then output.error = check_err ; return end

    local preset_err = verify_player_preset(payload.preset_instance_id)
    if preset_err ~= nil then output.error = preset_err ; return end

    local player_the_source, player_src_err = load_player_the_source(payload.preset_instance_id)
    if player_src_err ~= nil then output.error = player_src_err ; return end

    local enemy_the_source = load_enemy_the_source(enemy)

    local selected_mode = resolve_mode(enemy)

    local state = build_state(enemy, selected_mode, player_the_source, enemy_the_source)

    local session_id, create_err = game.battle_session_create(state)
    if create_err ~= nil then output.error = create_err ; return end

    output.session_id = session_id
    output.status     = state.status
    output.turn       = state.turn
    output.omega      = enemy
end

-- ─── Functions ───────────────────────────────────────────────────────────────

validate_payload = function()
    if payload.battle_mode == nil or payload.battle_mode == "" then
        return "battle_mode is required (fast, normal, long)"
    end
    if payload.battle_mode ~= "fast" and payload.battle_mode ~= "normal" and payload.battle_mode ~= "long" then
        return "battle_mode must be one of: fast, normal, long"
    end
    if payload.enemy_entity_key == nil or payload.enemy_entity_key == "" then
        return "enemy_entity_key is required"
    end
    if payload.preset_instance_id == nil or payload.preset_instance_id == "" then
        return "preset_instance_id is required"
    end
    return nil
end

resolve_enemy = function()
    local enemy, err = game.get_entity_def_by_key(payload.enemy_entity_key)
    if err ~= nil then return nil, err end
    if enemy == nil then return nil, "enemy not found" end
    return enemy, nil
end

resolve_mode = function(enemy)
    local selected = payload.battle_mode
    if enemy.metadata ~= nil and enemy.metadata.battle_modes ~= nil then
        local supported = false
        for _, m in ipairs(enemy.metadata.battle_modes) do
            if m == selected then supported = true ; break end
        end
        if not supported then selected = "normal" end
    end
    return selected
end

build_state = function(enemy, selected_mode, player_the_source, enemy_the_source)
    local hp_map = { fast = 4000, normal = 7000, long = 16000 }
    local hp = hp_map[selected_mode]
    return {
        metadata = {
            alpha_id           = ctx.player_id,
            preset_instance_id = payload.preset_instance_id,
            omega              = enemy,
            battle_mode        = selected_mode,
            started_at         = ctx.timestamp,
        },
        alpha_hp           = hp,
        alpha_the_source   = player_the_source,
        alpha_the_void     = {},
        alpha_hand         = {},  -- max 7 slots
        alpha_front_line   = {},  -- max 5 slots
        alpha_back_line    = {},  -- max 5 slots
        omega_hp           = hp,
        omega_the_source   = enemy_the_source,
        omega_the_void     = {},
        omega_hand         = {},  -- max 7 slots
        omega_front_line   = {},  -- max 5 slots
        omega_back_line    = {},  -- max 5 slots
        turn               = 1,  -- increments when alpha or omega runs out of actions
        action             = 1,  -- each action is one card played
        status             = "active",
    }
end

load_player_the_source = function(preset_instance_id)
    local slots, err = game.get_preset_slots(preset_instance_id)
    if err ~= nil then return nil, err end
    return slots, nil
end

load_enemy_the_source = function(enemy)
    local source = {}
    if enemy.abilities ~= nil then
        for _, ability in ipairs(enemy.abilities) do
            local count = ability.card_count or 0
            for _ = 1, count do
                source[#source + 1] = ability
            end
        end
    end
    return source
end

verify_player_preset = function(preset_instance_id)
    local preset, err = game.get_preset_by_id(preset_instance_id)
    if err ~= nil then return err end
    if preset == nil then return "preset not found" end

    local slots, slots_err = game.get_preset_slots(preset_instance_id)
    if slots_err ~= nil then return slots_err end

    local total = slots ~= nil and #slots or 0
    if total <= DECK_CARD_MIN then
        return "player deck must have more than " .. DECK_CARD_MIN .. " cards (has " .. total .. ")"
    end
    if total >= DECK_CARD_MAX then
        return "player deck must have fewer than " .. DECK_CARD_MAX .. " cards (has " .. total .. ")"
    end
    return nil
end

check_enemy = function(e)
    if e == nil then return "enemy not found" end
    local total = 0
    if e.abilities ~= nil then
        for _, ability in ipairs(e.abilities) do
            total = total + (ability.card_count or 0)
        end
    end
    if total <= DECK_CARD_MIN then
        return "enemy deck must have more than " .. DECK_CARD_MIN .. " cards (has " .. total .. ")"
    end
    if total >= DECK_CARD_MAX then
        return "enemy deck must have fewer than " .. DECK_CARD_MAX .. " cards (has " .. total .. ")"
    end
    return nil
end

main()