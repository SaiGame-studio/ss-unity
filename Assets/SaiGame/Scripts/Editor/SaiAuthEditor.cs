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
            
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("Save Credentials to PlayerPrefs", GUILayout.Height(30)))
            {
                saiAuth.ManualSaveCredentials();
                Debug.Log("✓ Credentials saved to PlayerPrefs!");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Register", GUILayout.Height(35)))
            {
                saiAuth.Register(
                    serializedObject.FindProperty("registerEmail").stringValue,
                    serializedObject.FindProperty("registerUsername").stringValue,
                    serializedObject.FindProperty("registerPassword").stringValue,
                    response => Debug.Log($"Registration success! User: {response.user.username} ({response.user.email}), Active: {response.user.is_active}, Verified: {response.user.is_verified}"),
                    error => Debug.LogError($"Registration failed: {error}")
                );
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
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

            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("Logout", GUILayout.Height(35)))
            {
                saiAuth.Logout();
                Debug.Log("Logged out and cleared all auth data");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("Refresh Token", GUILayout.Height(25)))
            {
                if (saiAuth.IsAuthenticated)
                {
                    saiAuth.RefreshAuthToken(
                        response => Debug.Log($"Token refreshed successfully! New token expires in: {response.expires_in}s"),
                        error => Debug.LogError($"Refresh token failed: {error}")
                    );
                }
                else
                {
                    Debug.LogWarning("Not authenticated! Please login first.");
                }
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
            if (GUILayout.Button("Get Me", GUILayout.Height(25)))
            {
                if (saiAuth.IsAuthenticated)
                {
                    saiAuth.GetMyProfile(
                        userData => Debug.Log($"Profile retrieved: {userData.username} ({userData.email}), Active: {userData.is_active}, Verified: {userData.is_verified}"),
                        error => Debug.LogError($"Get profile failed: {error}")
                    );
                }
                else
                {
                    Debug.LogWarning("Not authenticated! Please login first.");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
