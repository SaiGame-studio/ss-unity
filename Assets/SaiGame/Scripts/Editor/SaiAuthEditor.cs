using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiAuth))]
    public class SaiAuthEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SaiAuth saiAuth = (SaiAuth)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Login", GUILayout.Height(35)))
            {
                saiAuth.Login(
                    serializedObject.FindProperty("username").stringValue,
                    serializedObject.FindProperty("password").stringValue,
                    response => Debug.Log($"Login success! User: {response.user.username}, Token expires in: {response.expires_in}s"),
                    error => Debug.LogError($"Login failed: {error}")
                );
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("Logout", GUILayout.Height(25)))
            {
                saiAuth.Logout();
                Debug.Log("Logged out and cleared all auth data");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Test Connection", GUILayout.Height(25)))
            {
                SerializedProperty saiServiceProp = serializedObject.FindProperty("saiService");
                SaiService saiService = saiServiceProp.objectReferenceValue as SaiService;
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
                SerializedProperty saiServiceProp = serializedObject.FindProperty("saiService");
                SaiService saiService = saiServiceProp.objectReferenceValue as SaiService;
                if (saiService != null)
                {
                    SerializedProperty usernameProp = serializedObject.FindProperty("userData.username");
                    bool hasUser = !string.IsNullOrEmpty(usernameProp.stringValue);
                    string userInfo = hasUser ? $"User: {usernameProp.stringValue}" : "No user";
                    Debug.Log($"Base URL: {saiService.BaseUrl}, Authenticated: {saiService.IsAuthenticated}, {userInfo}");
                }
            }
        }
    }
}
