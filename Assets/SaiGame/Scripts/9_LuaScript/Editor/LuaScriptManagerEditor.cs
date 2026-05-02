using System.IO;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(LuaScriptManager))]
    public class LuaScriptManagerEditor : Editor
    {
        private const string DEFAULT_CUSTOM_SCRIPT_NAME = "battle_debug_turn";
        private const string LEGACY_BATTLE_TURN_SCRIPT_NAME = "battle_turn";

        private LuaScriptManager luaScriptManager;
        private SerializedProperty battleStartScriptName;
        private SerializedProperty battleTurnScriptName;
        private SerializedProperty battleEndScriptName;
        private SerializedProperty scriptFiles;

        private void OnEnable()
        {
            this.luaScriptManager = (LuaScriptManager)this.target;
            this.battleStartScriptName = this.serializedObject.FindProperty("battleStartScriptName");
            this.battleTurnScriptName = this.serializedObject.FindProperty("battleTurnScriptName");
            this.battleEndScriptName = this.serializedObject.FindProperty("battleEndScriptName");
            this.scriptFiles = this.serializedObject.FindProperty("scriptFiles");
            this.MigrateCustomScriptName();
        }

        private void MigrateCustomScriptName()
        {
            if (this.battleTurnScriptName == null || this.battleTurnScriptName.stringValue != LEGACY_BATTLE_TURN_SCRIPT_NAME)
            {
                return;
            }

            this.battleTurnScriptName.stringValue = DEFAULT_CUSTOM_SCRIPT_NAME;
            this.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(this.luaScriptManager);
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Lua Script Actions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "To help an AI agent write ss-go Lua scripts correctly, extract ss-go-lua-ai-contract-pack-vX.X.X into the project root folder so the agent can read the Lua contract, prompt, Lua language settings, and API stubs.",
                MessageType.Info);

            EditorGUILayout.PropertyField(this.battleStartScriptName, new GUIContent("Battle Start Script File Name"));

            string battleStartScriptNameValue = this.NormalizeScriptName(this.battleStartScriptName.stringValue);
            bool isValidBattleStartScriptName = this.IsValidScriptName(battleStartScriptNameValue);
            if (!isValidBattleStartScriptName)
            {
                EditorGUILayout.HelpBox("Battle start script file name must match: ^[a-z][a-z0-9_]*$", MessageType.Warning);
            }

            this.serializedObject.ApplyModifiedProperties();

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
            using (new EditorGUI.DisabledScope(!isValidBattleStartScriptName))
            {
                if (GUILayout.Button("Create BattleStart script", GUILayout.Height(30)))
                {
                    this.CreateLuaScript(battleStartScriptNameValue, this.CreateBattleStartTemplate());
                }
            }
            GUI.backgroundColor = Color.white;

            this.DrawHorizontalSeparator();

            EditorGUILayout.PropertyField(this.battleEndScriptName, new GUIContent("Battle End Script File Name"));

            string battleEndScriptNameValue = this.NormalizeScriptName(this.battleEndScriptName.stringValue);
            bool isValidBattleEndScriptName = this.IsValidScriptName(battleEndScriptNameValue);
            if (!isValidBattleEndScriptName)
            {
                EditorGUILayout.HelpBox("Battle end script file name must match: ^[a-z][a-z0-9_]*$", MessageType.Warning);
            }

            this.serializedObject.ApplyModifiedProperties();

            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.35f);
            using (new EditorGUI.DisabledScope(!isValidBattleEndScriptName))
            {
                if (GUILayout.Button("Create BattleEnd script", GUILayout.Height(30)))
                {
                    this.CreateLuaScript(battleEndScriptNameValue, this.CreateBattleEndTemplate());
                }
            }
            GUI.backgroundColor = Color.white;

            this.DrawHorizontalSeparator();

            EditorGUILayout.PropertyField(this.battleTurnScriptName, new GUIContent("Custom Script File Name"));

            string battleTurnScriptNameValue = this.NormalizeScriptName(this.battleTurnScriptName.stringValue);
            bool isValidBattleTurnScriptName = this.IsValidScriptName(battleTurnScriptNameValue);
            if (!isValidBattleTurnScriptName)
            {
                EditorGUILayout.HelpBox("Custom script file name must match: ^[a-z][a-z0-9_]*$", MessageType.Warning);
            }

            this.serializedObject.ApplyModifiedProperties();

            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.95f);
            using (new EditorGUI.DisabledScope(!isValidBattleTurnScriptName))
            {
                if (GUILayout.Button("Create Custom script", GUILayout.Height(30)))
                {
                    this.CreateLuaScript(battleTurnScriptNameValue, this.CreateBattleTurnTemplate());
                }
            }
            GUI.backgroundColor = Color.white;

            this.DrawHorizontalSeparator();

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Load Scripts", GUILayout.Height(28)))
            {
                this.LoadScripts();
            }
            GUI.backgroundColor = Color.white;

            this.DrawScriptFilesList();
            this.serializedObject.ApplyModifiedProperties();
        }

        private void DrawHorizontalSeparator()
        {
            EditorGUILayout.Space(8);
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 1f));
            EditorGUILayout.Space(8);
        }

        private void DrawScriptFilesList()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Loaded Scripts", EditorStyles.boldLabel);

            if (this.scriptFiles.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No scripts loaded. Click Load Scripts to scan the Lua scripts folder.", MessageType.None);
                return;
            }

            for (int index = 0; index < this.scriptFiles.arraySize; index++)
            {
                SerializedProperty scriptFile = this.scriptFiles.GetArrayElementAtIndex(index);
                SerializedProperty fileName = scriptFile.FindPropertyRelative("fileName");
                SerializedProperty scriptName = scriptFile.FindPropertyRelative("scriptName");
                SerializedProperty scriptId = scriptFile.FindPropertyRelative("scriptId");
                SerializedProperty description = scriptFile.FindPropertyRelative("description");
                SerializedProperty isActive = scriptFile.FindPropertyRelative("isActive");
                SerializedProperty isLibrary = scriptFile.FindPropertyRelative("isLibrary");
                SerializedProperty hasLocalFile = scriptFile.FindPropertyRelative("hasLocalFile");
                SerializedProperty hasBackendScript = scriptFile.FindPropertyRelative("hasBackendScript");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptName, new GUIContent("Script Name"));
                    EditorGUILayout.PropertyField(fileName, new GUIContent("File Name"));
                }

                EditorGUILayout.PropertyField(scriptId, new GUIContent("Script Id"));
                EditorGUILayout.PropertyField(description, new GUIContent("Description"));

                using (new EditorGUI.DisabledScope(true))
                {
                    float savedLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 110f;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(hasLocalFile, new GUIContent("Has Local File"));
                    EditorGUILayout.PropertyField(hasBackendScript, new GUIContent("Has Backend Script"));
                    EditorGUILayout.EndHorizontal();
                    EditorGUIUtility.labelWidth = savedLabelWidth;
                }

                {
                    float savedLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 110f;
                    EditorGUILayout.BeginHorizontal();

                    using (new EditorGUI.DisabledScope(!hasBackendScript.boolValue))
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(isActive, new GUIContent("Is Active"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            this.serializedObject.ApplyModifiedProperties();
                            this.UpdateScriptFlagsApi(index, this.GetScriptDisplayName(fileName, scriptName));
                        }
                    }

                    using (new EditorGUI.DisabledScope(!hasBackendScript.boolValue))
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(isLibrary, new GUIContent("Is Library"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            this.serializedObject.ApplyModifiedProperties();
                            this.UpdateScriptFlagsApi(index, this.GetScriptDisplayName(fileName, scriptName));
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUIUtility.labelWidth = savedLabelWidth;
                }

                this.serializedObject.ApplyModifiedProperties();

                EditorGUILayout.BeginHorizontal();

                // Download: backend has file but local does not
                if (hasBackendScript.boolValue && !hasLocalFile.boolValue)
                {
                    if (this.DrawColoredButton("Download", new Color(0.4f, 0.7f, 1f)))
                    {
                        this.DownloadScript(index, this.GetScriptDisplayName(fileName, scriptName));
                    }
                }

                // Upload New: local has file but backend does not
                if (hasLocalFile.boolValue && !hasBackendScript.boolValue)
                {
                    if (this.DrawColoredButton("Upload New", new Color(0.3f, 0.9f, 0.5f)))
                    {
                        this.CreateScriptApi(index, this.GetScriptDisplayName(fileName, scriptName));
                    }
                }

                // Update: both local and backend have files
                if (hasLocalFile.boolValue && hasBackendScript.boolValue)
                {
                    if (this.DrawColoredButton("Update", new Color(1f, 0.75f, 0.25f)))
                    {
                        this.UpdateScriptApi(index, this.GetScriptDisplayName(fileName, scriptName));
                    }
                }

                using (new EditorGUI.DisabledScope(!hasBackendScript.boolValue))
                {
                    if (this.DrawColoredButton("Delete", new Color(1f, 0.35f, 0.35f)))
                    {
                        this.DeleteScriptApi(index, this.GetScriptDisplayName(fileName, scriptName));
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private bool DrawColoredButton(string label, Color color)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.backgroundColor = previousColor;
            return clicked;
        }

        private string GetScriptDisplayName(SerializedProperty fileName, SerializedProperty scriptName)
        {
            return !string.IsNullOrWhiteSpace(fileName.stringValue)
                ? fileName.stringValue
                : scriptName.stringValue;
        }

        private void CreateScriptApi(int index, string fileName)
        {
            this.luaScriptManager.CreateScriptAtIndex(
                index,
                response => this.HandleScriptApiSuccess("Create Script", fileName),
                error => this.HandleScriptApiError("Create Script", error));
        }

        private void DownloadScript(int index, string fileName)
        {
            this.luaScriptManager.DownloadScriptAtIndex(
                index,
                response =>
                {
                    AssetDatabase.Refresh();
                    this.HandleScriptApiSuccess("Download Script", response);
                },
                error => this.HandleScriptApiError("Download Script", error));
        }

        private void UpdateScriptApi(int index, string fileName)
        {
            this.luaScriptManager.UpdateScriptAtIndex(
                index,
                response => this.HandleScriptApiSuccess("Update Script", fileName),
                error => this.HandleScriptApiError("Update Script", error));
        }

        private void UpdateScriptFlagsApi(int index, string fileName)
        {
            this.luaScriptManager.UpdateScriptFlagsAtIndex(
                index,
                response => this.HandleScriptApiSuccess("Update Script Flags", fileName),
                error => this.HandleScriptApiError("Update Script Flags", error));
        }

        private void DeleteScriptApi(int index, string fileName)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Script",
                $"Delete backend script for {fileName}? This does not delete the local file.",
                "Delete",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            this.luaScriptManager.DeleteScriptAtIndex(
                index,
                response => this.HandleScriptApiSuccess("Delete Script", fileName),
                error => this.HandleScriptApiError("Delete Script", error));
        }

        private void HandleScriptApiSuccess(string title, string fileName)
        {
            EditorUtility.SetDirty(this.luaScriptManager);
            this.serializedObject.Update();
            this.Repaint();
        }

        private void HandleScriptApiError(string title, string error)
        {
            Debug.LogError($"{title}: {error}");
        }

        private void LoadScripts()
        {
            this.luaScriptManager.LoadScripts(
                response => this.HandleLoadScriptsComplete(),
                error =>
                {
                    this.HandleLoadScriptsComplete();
                    Debug.LogError($"Load Scripts: {error}");
                });
        }

        private void HandleLoadScriptsComplete()
        {
            EditorUtility.SetDirty(this.luaScriptManager);
            this.serializedObject.Update();
            this.Repaint();
        }

        private void CreateLuaScript(string scriptName, string scriptTemplate)
        {
            this.EnsureScriptFolder();

            string assetPath = this.GetScriptAssetPath(scriptName);
            string fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath))
            {
                string currentContent = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(currentContent))
                {
                    this.WriteLuaScriptTemplate(fullPath, assetPath, scriptTemplate);
                    return;
                }

                this.SelectAsset(assetPath);
                return;
            }

            this.WriteLuaScriptTemplate(fullPath, assetPath, scriptTemplate);
        }

        private string GetScriptAssetPath(string scriptName)
        {
            return $"{LuaScriptManager.SCRIPT_FOLDER_ASSET_PATH}/{scriptName}.lua";
        }

        private string NormalizeScriptName(string scriptName)
        {
            string normalizedName = string.IsNullOrWhiteSpace(scriptName) ? string.Empty : scriptName.Trim();
            if (normalizedName.EndsWith(".lua"))
            {
                normalizedName = normalizedName.Substring(0, normalizedName.Length - 4);
            }

            return normalizedName;
        }

        private bool IsValidScriptName(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName))
            {
                return false;
            }

            char firstChar = scriptName[0];
            if (firstChar < 'a' || firstChar > 'z')
            {
                return false;
            }

            for (int i = 1; i < scriptName.Length; i++)
            {
                char currentChar = scriptName[i];
                bool isLowercaseLetter = currentChar >= 'a' && currentChar <= 'z';
                bool isDigit = currentChar >= '0' && currentChar <= '9';
                bool isUnderscore = currentChar == '_';
                if (!isLowercaseLetter && !isDigit && !isUnderscore)
                {
                    return false;
                }
            }

            return true;
        }

        private void WriteLuaScriptTemplate(string fullPath, string assetPath, string scriptTemplate)
        {
            File.WriteAllText(fullPath, scriptTemplate);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            this.SelectAsset(assetPath);
        }

        private string CreateBattleStartTemplate()
        {
            return string.Join("\n", new[]
            {
                "-- Usage: create or update this file as a backend Lua script, then run it through the script API.",
                "-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run",
                "-- Headers:",
                "--   Authorization: Bearer {access_token}",
                "--   Content-Type: application/json",
                "-- Example request body:",
                "-- {",
                "--   \"payload\": {",
                "--     \"battle_mode\": \"fast\" | \"normal\" | \"long\",",
                "--     \"enemy_entity_key\": \"enemy_key\",",
                "--     \"preset_instance_id\": \"preset-uuid\"",
                "--   }",
                "-- }",
                "",
                "-- Deck size limits — shared with player deck validation",
                "local DECK_CARD_MIN = 25",
                "local DECK_CARD_MAX = 52",
                "",
                "local check_enemy            -- forward declaration",
                "local verify_player_preset   -- forward declaration",
                "local validate_payload       -- forward declaration",
                "local resolve_enemy          -- forward declaration",
                "local resolve_mode           -- forward declaration",
                "local build_state            -- forward declaration",
                "local load_player_the_source -- forward declaration",
                "local load_enemy_the_source  -- forward declaration",
                "",
                "local function main()",
                "    local err = validate_payload()",
                "    if err ~= nil then output.error = err ; return end",
                "",
                "    local enemy, fetch_err = resolve_enemy()",
                "    if fetch_err ~= nil then output.error = fetch_err ; return end",
                "",
                "    local check_err = check_enemy(enemy)",
                "    if check_err ~= nil then output.error = check_err ; return end",
                "",
                "    local preset_err = verify_player_preset(payload.preset_instance_id)",
                "    if preset_err ~= nil then output.error = preset_err ; return end",
                "",
                "    local player_the_source, player_src_err = load_player_the_source(payload.preset_instance_id)",
                "    if player_src_err ~= nil then output.error = player_src_err ; return end",
                "",
                "    local enemy_the_source = load_enemy_the_source(enemy)",
                "",
                "    local selected_mode = resolve_mode(enemy)",
                "",
                "    local state = build_state(enemy, selected_mode, player_the_source, enemy_the_source)",
                "",
                "    local session_id, create_err = game.battle_session_create(state)",
                "    if create_err ~= nil then output.error = create_err ; return end",
                "",
                "    output.session_id = session_id",
                "    output.status     = state.status",
                "    output.turn       = state.turn",
                "    output.omega      = enemy",
                "end",
                "",
                "-- ─── Functions ───────────────────────────────────────────────────────────────",
                "",
                "validate_payload = function()",
                "    if payload.battle_mode == nil or payload.battle_mode == \"\" then",
                "        return \"battle_mode is required (fast, normal, long)\"",
                "    end",
                "    if payload.battle_mode ~= \"fast\" and payload.battle_mode ~= \"normal\" and payload.battle_mode ~= \"long\" then",
                "        return \"battle_mode must be one of: fast, normal, long\"",
                "    end",
                "    if payload.enemy_entity_key == nil or payload.enemy_entity_key == \"\" then",
                "        return \"enemy_entity_key is required\"",
                "    end",
                "    if payload.preset_instance_id == nil or payload.preset_instance_id == \"\" then",
                "        return \"preset_instance_id is required\"",
                "    end",
                "    return nil",
                "end",
                "",
                "resolve_enemy = function()",
                "    local enemy, err = game.get_entity_def_by_key(payload.enemy_entity_key)",
                "    if err ~= nil then return nil, err end",
                "    if enemy == nil then return nil, \"enemy not found\" end",
                "    return enemy, nil",
                "end",
                "",
                "resolve_mode = function(enemy)",
                "    local selected = payload.battle_mode",
                "    if enemy.metadata ~= nil and enemy.metadata.battle_modes ~= nil then",
                "        local supported = false",
                "        for _, m in ipairs(enemy.metadata.battle_modes) do",
                "            if m == selected then supported = true ; break end",
                "        end",
                "        if not supported then selected = \"normal\" end",
                "    end",
                "    return selected",
                "end",
                "",
                "build_state = function(enemy, selected_mode, player_the_source, enemy_the_source)",
                "    local hp_map = { fast = 4000, normal = 7000, long = 16000 }",
                "    local hp = hp_map[selected_mode]",
                "    return {",
                "        metadata = {",
                "            alpha_id           = ctx.player_id,",
                "            preset_instance_id = payload.preset_instance_id,",
                "            omega              = enemy,",
                "            battle_mode        = selected_mode,",
                "            started_at         = ctx.timestamp,",
                "        },",
                "        alpha_hp           = hp,",
                "        alpha_the_source   = player_the_source,",
                "        alpha_the_void     = {},",
                "        alpha_hand         = {},  -- max 7 slots",
                "        alpha_front_line   = {},  -- max 5 slots",
                "        alpha_back_line    = {},  -- max 5 slots",
                "        omega_hp           = hp,",
                "        omega_the_source   = enemy_the_source,",
                "        omega_the_void     = {},",
                "        omega_hand         = {},  -- max 7 slots",
                "        omega_front_line   = {},  -- max 5 slots",
                "        omega_back_line    = {},  -- max 5 slots",
                "        turn               = 1,  -- increments when alpha or omega runs out of actions",
                "        action             = 1,  -- each action is one card played",
                "        status             = \"active\",",
                "    }",
                "end",
                "",
                "load_player_the_source = function(preset_instance_id)",
                "    local slots, err = game.get_preset_slots(preset_instance_id)",
                "    if err ~= nil then return nil, err end",
                "    return slots, nil",
                "end",
                "",
                "load_enemy_the_source = function(enemy)",
                "    local source = {}",
                "    if enemy.abilities ~= nil then",
                "        for _, ability in ipairs(enemy.abilities) do",
                "            local count = ability.card_count or 0",
                "            for _ = 1, count do",
                "                source[#source + 1] = ability",
                "            end",
                "        end",
                "    end",
                "    return source",
                "end",
                "",
                "verify_player_preset = function(preset_instance_id)",
                "    local preset, err = game.get_preset_by_id(preset_instance_id)",
                "    if err ~= nil then return err end",
                "    if preset == nil then return \"preset not found\" end",
                "",
                "    local slots, slots_err = game.get_preset_slots(preset_instance_id)",
                "    if slots_err ~= nil then return slots_err end",
                "",
                "    local total = slots ~= nil and #slots or 0",
                "    if total <= DECK_CARD_MIN then",
                "        return \"player deck must have more than \" .. DECK_CARD_MIN .. \" cards (has \" .. total .. \")\"",
                "    end",
                "    if total >= DECK_CARD_MAX then",
                "        return \"player deck must have fewer than \" .. DECK_CARD_MAX .. \" cards (has \" .. total .. \")\"",
                "    end",
                "    return nil",
                "end",
                "",
                "check_enemy = function(e)",
                "    if e == nil then return \"enemy not found\" end",
                "    local total = 0",
                "    if e.abilities ~= nil then",
                "        for _, ability in ipairs(e.abilities) do",
                "            total = total + (ability.card_count or 0)",
                "        end",
                "    end",
                "    if total <= DECK_CARD_MIN then",
                "        return \"enemy deck must have more than \" .. DECK_CARD_MIN .. \" cards (has \" .. total .. \")\"",
                "    end",
                "    if total >= DECK_CARD_MAX then",
                "        return \"enemy deck must have fewer than \" .. DECK_CARD_MAX .. \" cards (has \" .. total .. \")\"",
                "    end",
                "    return nil",
                "end",
                "",
                "main()"
            });
        }

        private string CreateBattleTurnTemplate()
        {
            return string.Join("\n", new[]
            {
                "-- Usage: create or update this file as a backend Lua script, then run it through the script API.",
                "-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run",
                "-- Headers:",
                "--   Authorization: Bearer {access_token}",
                "--   Content-Type: application/json",
                "-- Example request body:",
                "-- {",
                "--   \"payload\": {",
                "--     \"session_id\": \"battle-session-uuid\",",
                "--     \"target\": \"alpha\",",
                "--     \"hp\": -10",
                "--   }",
                "-- }",
                "-- target: \"alpha\" or \"omega\"",
                "-- hp: positive value heals the target, negative value damages the target",
                "",
                "local validate_payload  -- forward declaration",
                "local load_session      -- forward declaration",
                "local apply_hp_delta    -- forward declaration",
                "",
                "local function main()",
                "    local err = validate_payload()",
                "    if err ~= nil then output.error = err ; return end",
                "",
                "    local state, load_err = load_session(payload.session_id)",
                "    if load_err ~= nil then output.error = load_err ; return end",
                "",
                "    local result, apply_err = apply_hp_delta(state, payload.target, payload.hp)",
                "    if apply_err ~= nil then output.error = apply_err ; return end",
                "",
                "    output.session_id = payload.session_id",
                "    output.target     = payload.target",
                "    output.hp_before  = result.hp_before",
                "    output.hp_after   = result.hp_after",
                "    output.hp_delta   = payload.hp",
                "end",
                "",
                "-- ─── Functions ───────────────────────────────────────────────────────────────",
                "",
                "validate_payload = function()",
                "    if payload.session_id == nil or payload.session_id == \"\" then",
                "        return \"session_id is required\"",
                "    end",
                "    if payload.target ~= \"alpha\" and payload.target ~= \"omega\" then",
                "        return \"target must be 'alpha' or 'omega'\"",
                "    end",
                "    if payload.hp == nil then",
                "        return \"hp is required\"",
                "    end",
                "    return nil",
                "end",
                "",
                "load_session = function(session_id)",
                "    local state, err = game.battle_session_get(session_id)",
                "    if err ~= nil then return nil, err end",
                "    if state == nil then return nil, \"battle session not found\" end",
                "    return state, nil",
                "end",
                "",
                "apply_hp_delta = function(state, target, hp_delta)",
                "    local current_hp, new_hp",
                "",
                "    if target == \"alpha\" then",
                "        current_hp      = state.alpha_hp or 0",
                "        new_hp          = current_hp + hp_delta",
                "        state.alpha_hp  = new_hp",
                "    elseif target == \"omega\" then",
                "        current_hp      = state.omega_hp or 0",
                "        new_hp          = current_hp + hp_delta",
                "        state.omega_hp  = new_hp",
                "    end",
                "",
                "    state.last_debug_target   = target",
                "    state.last_debug_hp_delta = hp_delta",
                "    state.updated_at          = ctx.timestamp",
                "",
                "    local err = game.battle_session_update(payload.session_id, state)",
                "    if err ~= nil then return nil, err end",
                "",
                "    return { hp_before = current_hp, hp_after = new_hp }, nil",
                "end",
                "",
                "main()"
            });
        }

        private string CreateBattleEndTemplate()
        {
            return string.Join("\n", new[]
            {
                "-- Usage: create or update this file as a backend Lua script, then run it through the script API.",
                "-- Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run",
                "-- Headers:",
                "--   Authorization: Bearer {access_token}",
                "--   Content-Type: application/json",
                "-- Example request body:",
                "-- {",
                "--   \"payload\": {",
                "--     \"session_id\": \"battle-session-uuid\"",
                "--   }",
                "-- }",
                "",
                "local validate_payload -- forward declaration",
                "local load_session     -- forward declaration",
                "local determine_winner -- forward declaration",
                "local end_session      -- forward declaration",
                "local open_drop_packs  -- forward declaration",
                "",
                "local function main()",
                "    local err = validate_payload()",
                "    if err ~= nil then output.error = err ; return end",
                "",
                "    local state, load_err = load_session()",
                "    if load_err ~= nil then output.error = load_err ; return end",
                "",
                "    local winner, alpha_hp, omega_hp = determine_winner(state)",
                "",
                "    local end_err = end_session(state, winner)",
                "    if end_err ~= nil then output.error = end_err ; return end",
                "",
                "    output.session_id = payload.session_id",
                "    output.status     = \"ended\"",
                "    output.winner     = winner",
                "    output.turn       = state.turn",
                "    output.alpha_hp   = alpha_hp",
                "    output.omega_hp   = omega_hp",
                "",
                "    if winner == \"alpha\" then",
                "        local drops, drop_err = open_drop_packs(state)",
                "        if drop_err ~= nil then output.error = drop_err ; return end",
                "        output.drops = drops",
                "    end",
                "end",
                "",
                "-- ─── Functions ───────────────────────────────────────────────────────────────",
                "",
                "validate_payload = function()",
                "    if payload.session_id == nil or payload.session_id == \"\" then",
                "        return \"session_id is required\"",
                "    end",
                "    return nil",
                "end",
                "",
                "load_session = function()",
                "    local state, err = game.battle_session_get(payload.session_id)",
                "    if err ~= nil then return nil, err end",
                "    if state == nil then return nil, \"battle session not found\" end",
                "    return state, nil",
                "end",
                "",
                "determine_winner = function(state)",
                "    local alpha_hp = state.alpha_hp or 0",
                "    local omega_hp = state.omega_hp or 0",
                "    local winner",
                "    if alpha_hp > omega_hp then",
                "        winner = \"alpha\"",
                "    else",
                "        winner = \"omega\"",
                "    end",
                "    return winner, alpha_hp, omega_hp",
                "end",
                "",
                "end_session = function(state, winner)",
                "    local end_data = {",
                "        winner   = winner,",
                "        reason   = \"completed\",",
                "        turn     = state.turn or 1,",
                "        ended_at = ctx.timestamp,",
                "    }",
                "    return game.battle_session_end(payload.session_id, end_data)",
                "end",
                "",
                "open_drop_packs = function(state)",
                "    local enemy = state.metadata and state.metadata.omega",
                "    if enemy == nil then return {}, nil end",
                "",
                "    local pack_ids = enemy.metadata and enemy.metadata.drop_pack_ids",
                "    if pack_ids == nil or #pack_ids == 0 then return {}, nil end",
                "",
                "    local drops, err = game.open_entity_drop_packs(payload.session_id, enemy.id, pack_ids)",
                "    if err ~= nil then return nil, err end",
                "    return drops or {}, nil",
                "end",
                "",
                "main()"
            });
        }

        private void EnsureScriptFolder()
        {
            if (AssetDatabase.IsValidFolder(LuaScriptManager.SCRIPT_FOLDER_ASSET_PATH))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets/SaiGame/LuaScript", "Scripts");
            AssetDatabase.Refresh();
        }

        private void SelectAsset(string assetPath)
        {
            Object scriptAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (scriptAsset == null)
            {
                return;
            }

            Selection.activeObject = scriptAsset;
            EditorGUIUtility.PingObject(scriptAsset);
        }
    }
}