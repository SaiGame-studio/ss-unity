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
        }
    }
}
