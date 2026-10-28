using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiServer))]
    public class SaiServerEditor : Editor
    {
        private bool showServiceReferences = false;
        private bool showServerTime = false;
        private bool showDebugSettings = false;
        private const string ENV_EXAMPLE_PATH = "Assets/SaiGame/env.example.txt";

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            EditorGUILayout.Space(5);

            GUIStyle versionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            GUIStyle packageStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Italic
            };

            EditorGUILayout.LabelField(SaiServer.PACKAGE_NAME, packageStyle);
            EditorGUILayout.LabelField($"v{SaiServer.PACKAGE_VERSION}", versionStyle);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent("Server Endpoint", "Production only. Use SERVER_ENDPOINT in .env to target another server in the Editor."),
                    SaiServer.PRODUCTION_BASE_URL);
            }

            SaiServer endpointTarget = (SaiServer)this.target;
            if (endpointTarget != null && !string.IsNullOrEmpty(endpointTarget.ServerEndpointOverride))
            {
                EditorGUILayout.HelpBox(
                    $"Overridden by .env SERVER_ENDPOINT for this play session: {endpointTarget.ServerEndpointOverride}",
                    MessageType.Info);
            }

            EditorGUILayout.Space(5);
            this.showServerTime = EditorGUILayout.Foldout(this.showServerTime, "Server Time", true);
            if (this.showServerTime)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(this.serializedObject.FindProperty("serverTime"), new GUIContent("Server Time"));
                    EditorGUILayout.PropertyField(this.serializedObject.FindProperty("serverTimestamp"), new GUIContent("Unix Timestamp"));
                    EditorGUILayout.PropertyField(this.serializedObject.FindProperty("serverTimezone"), new GUIContent("Timezone"));
                    EditorGUILayout.PropertyField(this.serializedObject.FindProperty("serverTimeError"), new GUIContent("Last Error"));
                }
                EditorGUI.indentLevel--;
            }

            // Service References foldout
            EditorGUILayout.Space(2);
            this.showServiceReferences = EditorGUILayout.Foldout(this.showServiceReferences, "Service References", true);
            if (this.showServiceReferences)
            {
                EditorGUI.indentLevel++;

                // ── Root (SaiServer) ────────────────────────────────────────
                EditorGUILayout.LabelField("Root Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("saiAuth"),              new GUIContent("Sai Auth"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("gamerProgress"),        new GUIContent("Gamer Progress"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("mailbox"),              new GUIContent("Mailbox"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("playerEvent"),          new GUIContent("Player Event"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("leaderboard"),          new GUIContent("Leaderboard"));

                // ── Item child ───────────────────────────────────────────────
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Item Child Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemDefinitions"),       new GUIContent("Item Definitions"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("playerItem"),           new GUIContent("Player Item"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("playerContainer"),      new GUIContent("Player Container"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemGenerator"),        new GUIContent("Item Generator"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("equipmentSlotManager"), new GUIContent("Equipment Slot"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemTag"),              new GUIContent("Item Tag"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemPreset"),           new GUIContent("Item Preset"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemAddDeduct"),        new GUIContent("Item Add Deduct"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemCrafting"),         new GUIContent("Player Crafting"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemMove"),              new GUIContent("Item Move"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("itemSwap"),              new GUIContent("Item Swap"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("shop"),                 new GUIContent("Shop"));

                // ── Quest child ──────────────────────────────────────────────
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Quest Child Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("questProgressor"),      new GUIContent("Quest Progressor"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("questHistory"),         new GUIContent("Quest History"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("chainQuest"),           new GUIContent("Chain Quest"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("dailyQuest"),           new GUIContent("Daily Quest"));

                // ── Battle child ─────────────────────────────────────────────
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Battle Child Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("battleSessions"),       new GUIContent("Battle Sessions"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("battleScript"),         new GUIContent("Battle Script"));

                EditorGUI.indentLevel--;
            }

            // Debug Settings foldout
            EditorGUILayout.Space(5);
            this.showDebugSettings = EditorGUILayout.Foldout(this.showDebugSettings, "Debug Settings", true);
            if (this.showDebugSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showButtonsLog"), new GUIContent("Show Buttons Log"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showCallbackLog"), new GUIContent("Show Callback Log"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showDebugLog"), new GUIContent("Show Debug Log"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showUrlRequest"), new GUIContent("Show Url Request"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showJsonRequest"), new GUIContent("Show Json Request"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showJsonResponse"), new GUIContent("Show Json Response"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("gameId"), new GUIContent("Game Id"));

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("requestTimeout"), new GUIContent("Request Timeout"));

            this.serializedObject.ApplyModifiedProperties();

            SaiServer saiServer = (SaiServer)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);

            bool envExists = EnvLoader.EnvFileExists();
            EditorGUILayout.HelpBox(
                envExists ? ".env file found at project root." : ".env file not found. Click \"Create .env\" or copy env.example.txt from the SaiGame folder to the project root as .env.",
                envExists ? MessageType.Info : MessageType.Warning);

            GUI.enabled = envExists;
            bool current = EnvAutoLoader.LoadEnvOnPlay;
            bool updated = EditorGUILayout.Toggle(
                new GUIContent(
                    "Load .env on Play",
                    "When checked, GAME_ID / USERNAME / PASSWORD / SERVER_ENDPOINT are applied from .env automatically when entering Play mode. " +
                    "Credentials are written to PlayerPrefs only — never saved to the scene file."),
                current);
            if (updated != current)
            {
                EnvAutoLoader.LoadEnvOnPlay = updated;
            }

            GUI.enabled = envExists && EnvAutoLoader.LoadEnvOnPlay;
            EditorGUI.indentLevel++;
            this.serializedObject.Update();
            EditorGUILayout.PropertyField(
                this.serializedObject.FindProperty("envOverrideServerEndpoint"),
                new GUIContent(
                    "Override server endpoint",
                    "When checked, SERVER_ENDPOINT from .env replaces the Server Endpoint option when entering Play mode. " +
                    "The override lasts for the play session only and is never saved to the scene."));
            EditorGUILayout.PropertyField(
                this.serializedObject.FindProperty("envOverrideGameId"),
                new GUIContent(
                    "Override game id",
                    "When checked, GAME_ID from .env replaces the Game Id when entering Play mode."));
            EditorGUILayout.PropertyField(
                this.serializedObject.FindProperty("envOverrideCredentials"),
                new GUIContent(
                    "Override credentials",
                    "When checked, USERNAME / PASSWORD from .env are applied to SaiAuth when entering Play mode."));
            this.serializedObject.ApplyModifiedProperties();
            EditorGUI.indentLevel--;
            GUI.enabled = true;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Service Actions", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
            if (GUILayout.Button("Full Reset", GUILayout.Height(28)))
            {
                if (saiServer != null)
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Warning: Full Reset",
                        "All settings in every Sai Server class on this object and its child objects " +
                        "(SaiServer, SaiAuth, Shop, Mailbox, Quest, Item, Battle, ...) will be reset to their default configuration.\n\n" +
                        "Values you set in the Inspector (Game Id, auto-load options, limits, references, ...) will be lost.\n\n" +
                        "Do you want to continue?",
                        "Reset All",
                        "Cancel");
                    if (!confirmed)
                        Debug.Log("[SaiServer] Full Reset cancelled by user.");

                    if (confirmed)
                    {
                        SaiBehaviour[] hierarchyComponents = saiServer.GetComponentsInChildren<SaiBehaviour>(true);
                        Undo.RecordObjects(hierarchyComponents, "SaiServer Full Reset");

                        int resetCount = 0;
                        int totalCount = hierarchyComponents.Length;

                        for (int i = 0; i < hierarchyComponents.Length; i++)
                        {
                            SaiBehaviour component = hierarchyComponents[i];
                            if (component == null)
                            {
                                continue;
                            }

                            bool hookInvoked = saiServer.ManualInvokeResetHooks(component);
                            bool serializedReset = this.ResetComponentSerializedFieldsToDefaults(component);
                            if (hookInvoked || serializedReset)
                            {
                                resetCount++;
                            }

                            EditorUtility.SetDirty(component);
                        }

                        for (int i = 0; i < hierarchyComponents.Length; i++)
                        {
                            SaiBehaviour component = hierarchyComponents[i];
                            if (component == null) continue;

                            saiServer.ManualInvokeLoadComponents(component);
                            EditorUtility.SetDirty(component);
                        }

                        EditorSceneManager.MarkSceneDirty(saiServer.gameObject.scene);
                        EditorUtility.DisplayDialog("Full Reset Complete", $"Reset executed on {resetCount}/{totalCount} SaiBehaviour component(s).", "OK");
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.55f);
            if (GUILayout.Button("Create .env", GUILayout.Height(28)))
            {
                this.CreateEnvFileFromExample();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Connection", GUILayout.Height(25)))
            {
                if (saiServer != null)
                {
                    saiServer.TestConnection(success =>
                    {
                        if (success)
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log("✓ Connection test <color=#00FF88><b>passed</b></color>!");
                        }
                        else
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError("✗ Connection test failed!");
                        }
                    });
                }
            }

            if (GUILayout.Button("Show Service Info", GUILayout.Height(25)))
            {
                if (saiServer != null)
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                    {
                        Debug.Log("<color=#FFCC00><b>[SaiServer] ► Show Service Info</b></color>");
                        bool hasUser = saiServer.CurrentUser != null && !string.IsNullOrEmpty(saiServer.CurrentUser.username);
                        string userInfo = hasUser ? $"User: {saiServer.CurrentUser.username}" : "No user";
                        Debug.Log($"Base URL: {saiServer.BaseUrl}, Authenticated: {saiServer.IsAuthenticated}, {userInfo}");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateEnvFileFromExample()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string destPath = Path.Combine(projectRoot, ".env");
            string sourcePath = Path.GetFullPath(ENV_EXAMPLE_PATH);

            if (!File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("env.example.txt Not Found", $"env.example.txt was not found at:\n{sourcePath}", "OK");
                return;
            }

            if (File.Exists(destPath))
            {
                Debug.LogWarning($"[SaiServer] .env already exists at {destPath} — Create .env was cancelled.");
                EditorUtility.DisplayDialog(
                    ".env Already Exists",
                    $".env already exists at:\n{destPath}\n\nIt was not created or overwritten. " +
                    "Copy the keys you need from env.example.txt into the existing .env instead.",
                    "OK");
                return;
            }

            File.Copy(sourcePath, destPath, overwrite: false);
            EditorUtility.DisplayDialog(".env Created", ".env created at project root from env.example.txt.", "OK");
            EditorUtility.OpenWithDefaultApp(destPath);
        }

        private bool ResetComponentSerializedFieldsToDefaults(Component targetComponent)
        {
            if (targetComponent == null)
            {
                return false;
            }

            Type componentType = targetComponent.GetType();
            GameObject tempObject = new GameObject($"__TempReset_{componentType.Name}");
            tempObject.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                Component tempComponent = tempObject.AddComponent(componentType);
                if (tempComponent == null)
                {
                    return false;
                }

                EditorUtility.CopySerialized(tempComponent, targetComponent);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (tempObject != null)
                {
                    DestroyImmediate(tempObject);
                }
            }
        }
    }
}
