using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GoogleBackendLogin))]
    public class GoogleBackendLoginEditor : Editor
    {
        private const string PREF_SESSION_SETTINGS = "GoogleBackendLoginEditor.showSessionSettings";

        private bool showSessionSettings
        {
            get => EditorPrefs.GetBool(PREF_SESSION_SETTINGS, true);
            set => EditorPrefs.SetBool(PREF_SESSION_SETTINGS, value);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GoogleBackendLogin login = (GoogleBackendLogin)target;

            EditorGUILayout.LabelField("authentication data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("accessToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("expiresIn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("userData"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loginTime"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("session state", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sessionId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("authUrl"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isLoggingIn"));

            EditorGUILayout.Space();

            bool sessionSettings = EditorGUILayout.Foldout(this.showSessionSettings, "session settings", true);
            if (sessionSettings != this.showSessionSettings) this.showSessionSettings = sessionSettings;
            if (this.showSessionSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxLoginDurationSeconds"), new GUIContent("max login duration seconds"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultPollIntervalSeconds"), new GUIContent("default poll interval seconds"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("actions", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Vào Play mode để thực hiện login qua Google backend.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Start Login", GUILayout.Height(25)))
                {
                    login.StartLogin(
                        response =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log($"[Editor] Google login success! User: {response.user.username} ({response.user.email}), Token expires in: {response.expires_in}s");
                        },
                        error =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Google login failed: {error}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
                if (GUILayout.Button("Cancel Login", GUILayout.Height(25)))
                {
                    login.CancelLogin();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("Clear Auth Data", GUILayout.Height(25)))
                {
                    login.ClearAuthData();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button("Open Auth URL", GUILayout.Height(25)))
                {
                    string url = login.AuthUrl;
                    if (string.IsNullOrEmpty(url))
                    {
                        Debug.LogWarning("[Editor] Không có auth_url. Hãy bấm Start Login trước.");
                    }
                    else
                    {
                        Application.OpenURL(url);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }
}
