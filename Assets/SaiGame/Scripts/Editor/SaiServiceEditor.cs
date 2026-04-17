using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiService))]
    public class SaiServiceEditor : Editor
    {
        private bool showServiceReferences = false;
        private bool showDebugSettings = false;
        private static readonly string[] SERVER_ENDPOINT_OPTIONS =
        {
            "Local API (HTTP) - local-api.saigame.studio:82",
            "Production API (HTTPS) - api.saigame.studio"
        };

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

            EditorGUILayout.LabelField(SaiService.PACKAGE_NAME, packageStyle);
            EditorGUILayout.LabelField($"v{SaiService.PACKAGE_VERSION}", versionStyle);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            SerializedProperty serverEndpointProperty = this.serializedObject.FindProperty("serverEndpoint");
            int currentIndex = Mathf.Clamp(serverEndpointProperty.enumValueIndex, 0, SERVER_ENDPOINT_OPTIONS.Length - 1);
            int newIndex = EditorGUILayout.Popup("Server Endpoint", currentIndex, SERVER_ENDPOINT_OPTIONS);
            if (newIndex != currentIndex)
            {
                serverEndpointProperty.enumValueIndex = newIndex;
                this.serializedObject.ApplyModifiedProperties();
                SaiService svc = (SaiService)this.target;
                if (svc != null) svc.ManualSaveServerEndpoint();
            }

            // Service References foldout
            EditorGUILayout.Space(2);
            this.showServiceReferences = EditorGUILayout.Foldout(this.showServiceReferences, "Service References", true);
            if (this.showServiceReferences)
            {
                EditorGUI.indentLevel++;

                // ── Root (SaiService) ────────────────────────────────────────
                EditorGUILayout.LabelField("Root Object", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("saiAuth"),              new GUIContent("Sai Auth"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("gamerProgress"),        new GUIContent("Gamer Progress"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("mailbox"),              new GUIContent("Mailbox"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("playerEvent"),          new GUIContent("Player Event"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("leaderboard"),          new GUIContent("Leaderboard"));

                // ── Item child ───────────────────────────────────────────────
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Item Child Object", EditorStyles.miniBoldLabel);
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
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("questClaims"),          new GUIContent("Quest Status"));
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
            EditorGUILayout.LabelField("Game Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("gameId"), new GUIContent("Game Id"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("API Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("requestTimeout"), new GUIContent("Request Timeout"));

            this.serializedObject.ApplyModifiedProperties();

            SaiService saiService = (SaiService)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Service Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
            if (GUILayout.Button("Save Game ID to PlayerPrefs", GUILayout.Height(30)))
            {
                if (saiService != null)
                {
                    saiService.ManualSaveGameId();
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log("✓ Game ID saved to PlayerPrefs!");
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(30)))
            {
                if (saiService != null)
                {
                    saiService.ManualClearGameId();
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log("✓ Game ID cleared from PlayerPrefs!");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Connection", GUILayout.Height(25)))
            {
                if (saiService != null)
                {
                    saiService.TestConnection(success =>
                    {
                        if (success)
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.Log("✓ Connection test passed!");
                        }
                        else
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.LogError("✗ Connection test failed!");
                        }
                    });
                }
            }

            if (GUILayout.Button("Show Service Info", GUILayout.Height(25)))
            {
                if (saiService != null)
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                    {
                        Debug.Log("<color=#FFCC00><b>[SaiService] ► Show Service Info</b></color>");
                        bool hasUser = saiService.CurrentUser != null && !string.IsNullOrEmpty(saiService.CurrentUser.username);
                        string userInfo = hasUser ? $"User: {saiService.CurrentUser.username}" : "No user";
                        Debug.Log($"Base URL: {saiService.BaseUrl}, Authenticated: {saiService.IsAuthenticated}, {userInfo}");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}