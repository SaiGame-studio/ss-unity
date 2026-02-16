using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiService))]
    public class SaiServiceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
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
            
            DrawDefaultInspector();

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
                    bool hasUser = saiService.CurrentUser != null && !string.IsNullOrEmpty(saiService.CurrentUser.username);
                    string userInfo = hasUser ? $"User: {saiService.CurrentUser.username}" : "No user";
                    Debug.Log($"Base URL: {saiService.BaseUrl}, Authenticated: {saiService.IsAuthenticated}, {userInfo}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}