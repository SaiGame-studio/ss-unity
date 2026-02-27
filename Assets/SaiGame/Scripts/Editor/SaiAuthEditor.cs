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
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("Save Credentials", GUILayout.Height(30)))
            {
                saiAuth.ManualSaveCredentials();
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(30)))
            {
                saiAuth.ManualClearCredentials();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Register", GUILayout.Height(35)))
            {
                saiAuth.Register(
                    serializedObject.FindProperty("registerEmail").stringValue,
                    serializedObject.FindProperty("registerUsername").stringValue,
                    serializedObject.FindProperty("registerPassword").stringValue,
                    response => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Registration success! User: {response.user.username} ({response.user.email}), Active: {response.user.is_active}, Verified: {response.user.is_verified}"); },
                    error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Registration failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Login", GUILayout.Height(35)))
            {
                saiAuth.Login(
                    serializedObject.FindProperty("username").stringValue,
                    serializedObject.FindProperty("password").stringValue,
                    response => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Login success! User: {response.user.username}, Token expires in: {response.expires_in}s"); },
                    error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Login failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("Logout", GUILayout.Height(35)))
            {
                saiAuth.Logout();
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
                        response => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Token refreshed successfully! New token expires in: {response.expires_in}s"); },
                        error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Refresh token failed: {error}"); }
                    );
                }
                else
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogWarning("[Editor] Not authenticated! Please login first.");
                }
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
            if (GUILayout.Button("Get Me", GUILayout.Height(25)))
            {
                if (saiAuth.IsAuthenticated)
                {
                    saiAuth.GetMyProfile(
                        userData => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Profile retrieved: {userData.username} ({userData.email}), Active: {userData.is_active}, Verified: {userData.is_verified}"); },
                        error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Get profile failed: {error}"); }
                    );
                }
                else
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogWarning("[Editor] Not authenticated! Please login first.");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
