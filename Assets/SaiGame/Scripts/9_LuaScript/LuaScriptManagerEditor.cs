using System.IO;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(LuaScriptManager))]
    public class LuaScriptManagerEditor : Editor
    {
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

            EditorGUILayout.PropertyField(this.battleTurnScriptName, new GUIContent("Battle Turn Script File Name"));

            string battleTurnScriptNameValue = this.NormalizeScriptName(this.battleTurnScriptName.stringValue);
            bool isValidBattleTurnScriptName = this.IsValidScriptName(battleTurnScriptNameValue);
            if (!isValidBattleTurnScriptName)
            {
                EditorGUILayout.HelpBox("Battle turn script file name must match: ^[a-z][a-z0-9_]*$", MessageType.Warning);
            }

            this.serializedObject.ApplyModifiedProperties();

            GUI.backgroundColor = new Color(0.35f, 0.8f, 0.95f);
            using (new EditorGUI.DisabledScope(!isValidBattleTurnScriptName))
            {
                if (GUILayout.Button("Create BattleTurn script", GUILayout.Height(30)))
                {
                    this.CreateLuaScript(battleTurnScriptNameValue, this.CreateBattleTurnTemplate());
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
                SerializedProperty hasLocalFile = scriptFile.FindPropertyRelative("hasLocalFile");
                SerializedProperty hasBackendScript = scriptFile.FindPropertyRelative("hasBackendScript");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptName, new GUIContent("Script Name"));
                    EditorGUILayout.PropertyField(fileName, new GUIContent("File Name"));
                    EditorGUILayout.PropertyField(hasLocalFile, new GUIContent("Has Local File"));
                    EditorGUILayout.PropertyField(hasBackendScript, new GUIContent("Has Backend Script"));
                }

                EditorGUILayout.PropertyField(scriptId, new GUIContent("Script Id"));
                EditorGUILayout.PropertyField(description, new GUIContent("Description"));
                EditorGUILayout.PropertyField(isActive, new GUIContent("Is Active"));

                this.serializedObject.ApplyModifiedProperties();

                EditorGUILayout.BeginHorizontal();
                if (hasLocalFile.boolValue)
                {
                    if (this.DrawColoredButton("Create", new Color(0.3f, 0.9f, 0.5f)))
                    {
                        this.CreateScriptApi(index, this.GetScriptDisplayName(fileName, scriptName));
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!hasBackendScript.boolValue))
                    {
                        if (this.DrawColoredButton("Download", new Color(0.4f, 0.7f, 1f)))
                        {
                            this.DownloadScript(index, this.GetScriptDisplayName(fileName, scriptName));
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(!hasLocalFile.boolValue || !hasBackendScript.boolValue))
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
                "--     \"enemy_entity_id\": \"enemy-definition-uuid\",",
                "--     \"enemy_entity_key\": \"enemy_key\",",
                "--     \"enemy_pool_key\": \"enemy_pool_key\",",
                "--     \"battle_data\": {}",
                "--   }",
                "-- }",
                "-- Provide one enemy selector: enemy_entity_id, enemy_entity_key, or enemy_pool_key.",
                "",
                "local enemy = nil",
                "local err = nil",
                "",
                "if payload.enemy_entity_id ~= nil and payload.enemy_entity_id ~= \"\" then",
                "    enemy, err = game.get_entity_def_by_id(payload.enemy_entity_id)",
                "elseif payload.enemy_entity_key ~= nil and payload.enemy_entity_key ~= \"\" then",
                "    enemy, err = game.get_entity_def_by_key(payload.enemy_entity_key)",
                "elseif payload.enemy_pool_key ~= nil and payload.enemy_pool_key ~= \"\" then",
                "    enemy, err = game.entity_pool_random(payload.enemy_pool_key)",
                "else",
                "    output.error = \"enemy_entity_id, enemy_entity_key, or enemy_pool_key is required\"",
                "    return",
                "end",
                "",
                "if err ~= nil then",
                "    output.error = err",
                "    return",
                "end",
                "",
                "if enemy == nil then",
                "    output.error = \"enemy not found\"",
                "    return",
                "end",
                "",
                "local state = {",
                "    player_id = ctx.player_id,",
                "    enemy = enemy,",
                "    turn = 1,",
                "    status = \"active\",",
                "    started_at = ctx.timestamp,",
                "    data = payload.battle_data or {}",
                "}",
                "",
                "local session_id, create_err = game.battle_session_create(state)",
                "if create_err ~= nil then",
                "    output.error = create_err",
                "    return",
                "end",
                "",
                "output.session_id = session_id",
                "output.status = state.status",
                "output.turn = state.turn",
                "output.enemy = enemy"
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
                "--     \"action\": \"attack\",",
                "--     \"damage\": 5,",
                "--     \"enemy_hp\": 20",
                "--   }",
                "-- }",
                "-- Use action \"flee\" to mark the battle session as fled.",
                "",
                "if payload.session_id == nil or payload.session_id == \"\" then",
                "    output.error = \"session_id is required\"",
                "    return",
                "end",
                "",
                "local state, get_err = game.battle_session_get(payload.session_id)",
                "if get_err ~= nil then",
                "    output.error = get_err",
                "    return",
                "end",
                "",
                "if state == nil then",
                "    output.error = \"battle session not found\"",
                "    return",
                "end",
                "",
                "local action = payload.action or \"attack\"",
                "if action == \"flee\" then",
                "    local flee_err = game.battle_session_flee(payload.session_id)",
                "    if flee_err ~= nil then",
                "        output.error = flee_err",
                "        return",
                "    end",
                "",
                "    output.status = \"fled\"",
                "    output.session_id = payload.session_id",
                "    return",
                "end",
                "",
                "local damage = payload.damage or 1",
                "if damage < 0 then",
                "    damage = 0",
                "end",
                "",
                "state.turn = (state.turn or 1) + 1",
                "state.enemy_hp = math.max(0, (state.enemy_hp or payload.enemy_hp or 1) - damage)",
                "state.last_action = action",
                "state.last_damage = damage",
                "state.updated_at = ctx.timestamp",
                "",
                "if state.enemy_hp <= 0 then",
                "    state.status = \"ended\"",
                "    local end_data = {",
                "        winner = \"player\",",
                "        turn = state.turn,",
                "        ended_at = ctx.timestamp",
                "    }",
                "",
                "    local end_err = game.battle_session_end(payload.session_id, end_data)",
                "    if end_err ~= nil then",
                "        output.error = end_err",
                "        return",
                "    end",
                "else",
                "    state.status = \"active\"",
                "    local update_err = game.battle_session_update(payload.session_id, state)",
                "    if update_err ~= nil then",
                "        output.error = update_err",
                "        return",
                "    end",
                "end",
                "",
                "output.session_id = payload.session_id",
                "output.status = state.status",
                "output.turn = state.turn",
                "output.enemy_hp = state.enemy_hp",
                "output.damage = damage"
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
                "--     \"session_id\": \"battle-session-uuid\",",
                "--     \"winner\": \"player\",",
                "--     \"reason\": \"completed\",",
                "--     \"turn\": 3",
                "--   }",
                "-- }",
                "",
                "if payload.session_id == nil or payload.session_id == \"\" then",
                "    output.error = \"session_id is required\"",
                "    return",
                "end",
                "",
                "local state, get_err = game.battle_session_get(payload.session_id)",
                "if get_err ~= nil then",
                "    output.error = get_err",
                "    return",
                "end",
                "",
                "if state == nil then",
                "    output.error = \"battle session not found\"",
                "    return",
                "end",
                "",
                "local winner = payload.winner or state.winner or \"player\"",
                "local end_data = {",
                "    winner = winner,",
                "    reason = payload.reason or \"completed\",",
                "    turn = state.turn or payload.turn or 1,",
                "    ended_at = ctx.timestamp,",
                "    state = state",
                "}",
                "",
                "local end_err = game.battle_session_end(payload.session_id, end_data)",
                "if end_err ~= nil then",
                "    output.error = end_err",
                "    return",
                "end",
                "",
                "output.session_id = payload.session_id",
                "output.status = \"ended\"",
                "output.winner = winner",
                "output.reason = end_data.reason",
                "output.turn = end_data.turn"
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