using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiService))]
    public class SaiServiceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SaiService saiService = (SaiService)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Service Actions", EditorStyles.boldLabel);

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

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Show Service Info", GUILayout.Height(25)))
            {
                if (saiService != null)
                {
                    bool hasUser = saiService.CurrentUser != null && !string.IsNullOrEmpty(saiService.CurrentUser.username);
                    string userInfo = hasUser ? $"User: {saiService.CurrentUser.username}" : "No user";
                    Debug.Log($"Base URL: {saiService.BaseUrl}, Authenticated: {saiService.IsAuthenticated}, {userInfo}");
                }
            }
        }
    }
}