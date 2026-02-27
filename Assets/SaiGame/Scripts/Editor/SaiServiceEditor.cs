using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiService))]
    public class SaiServiceEditor : Editor
    {
        private bool showDebugSettings = true;
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
            }

            EditorGUILayout.Space(5);
            DrawPropertiesExcluding(this.serializedObject, "m_Script", "serverEndpoint", "domainOption", "port", "useHttps", "showDebug", "showButtonsLog", "showCallbackLog");

            // Debug Settings foldout
            EditorGUILayout.Space(5);
            this.showDebugSettings = EditorGUILayout.Foldout(this.showDebugSettings, "Debug Settings", true);
            if (this.showDebugSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showDebug"), new GUIContent("Show Debug"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showButtonsLog"), new GUIContent("Show Buttons Log"));
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("showCallbackLog"), new GUIContent("Show Callback Log"));
                EditorGUI.indentLevel--;
            }

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
                    Debug.Log("✓ Game ID saved to PlayerPrefs!");
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(30)))
            {
                if (saiService != null)
                {
                    saiService.ManualClearGameId();
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
                            Debug.Log("✓ Connection test passed!");
                        else
                            Debug.LogError("✗ Connection test failed!");
                    });
                }
            }

            if (GUILayout.Button("Show Service Info", GUILayout.Height(25)))
            {
                if (saiService != null)
                {
                    Debug.Log("<color=#FFCC00><b>[SaiService] ► Show Service Info</b></color>");
                    bool hasUser = saiService.CurrentUser != null && !string.IsNullOrEmpty(saiService.CurrentUser.username);
                    string userInfo = hasUser ? $"User: {saiService.CurrentUser.username}" : "No user";
                    Debug.Log($"Base URL: {saiService.BaseUrl}, Authenticated: {saiService.IsAuthenticated}, {userInfo}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}