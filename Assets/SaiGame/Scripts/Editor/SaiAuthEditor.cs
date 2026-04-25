using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiAuth))]
    public class SaiAuthEditor : Editor
    {
        private const string PREF_AUTO_SETTINGS = "SaiAuthEditor.showAutoSettings";
        private const string PREF_AUTO_REFRESH = "SaiAuthEditor.showAutoRefreshSettings";
        private const string PREF_LOGIN_INPUTS = "SaiAuthEditor.showLoginInputs";
        private const string PREF_REGISTER_INPUTS = "SaiAuthEditor.showRegisterInputs";
        private const string PREF_SHOW_PASSWORDS = "SaiAuthEditor.showPasswords";

        private bool showAutoSettings
        {
            get => EditorPrefs.GetBool(PREF_AUTO_SETTINGS, true);
            set => EditorPrefs.SetBool(PREF_AUTO_SETTINGS, value);
        }

        private bool showAutoRefreshSettings
        {
            get => EditorPrefs.GetBool(PREF_AUTO_REFRESH, true);
            set => EditorPrefs.SetBool(PREF_AUTO_REFRESH, value);
        }

        private bool showLoginInputs
        {
            get => EditorPrefs.GetBool(PREF_LOGIN_INPUTS, true);
            set => EditorPrefs.SetBool(PREF_LOGIN_INPUTS, value);
        }

        private bool showRegisterInputs
        {
            get => EditorPrefs.GetBool(PREF_REGISTER_INPUTS, true);
            set => EditorPrefs.SetBool(PREF_REGISTER_INPUTS, value);
        }

        private bool showPasswords
        {
            get => EditorPrefs.GetBool(PREF_SHOW_PASSWORDS, false);
            set => EditorPrefs.SetBool(PREF_SHOW_PASSWORDS, value);
        }

        private void DrawPasswordPropertyField(SerializedProperty property, string label)
        {
            string value = property.stringValue;
            string updatedValue = this.showPasswords
                ? EditorGUILayout.TextField(label, value)
                : EditorGUILayout.PasswordField(label, value);

            if (updatedValue != value)
            {
                property.stringValue = updatedValue;
            }
        }

        private void DrawInputWithSaveLoad(SerializedProperty property, string label, System.Action onSave, System.Action onLoad)
        {
            EditorGUILayout.BeginHorizontal();
            if (label == "Password")
            {
                this.DrawPasswordPropertyField(property, label);
            }
            else
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }

            Color prevColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clean", GUILayout.Width(50)))
            {
                property.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
            }

            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("Save", GUILayout.Width(50)))
            {
                serializedObject.ApplyModifiedProperties();
                onSave?.Invoke();
                serializedObject.Update();
            }

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Load", GUILayout.Width(50)))
            {
                onLoad?.Invoke();
                serializedObject.Update();
            }

            GUI.backgroundColor = prevColor;
            EditorGUILayout.EndHorizontal();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SaiAuth saiAuth = (SaiAuth)target;

            // Authentication Data
            EditorGUILayout.LabelField("Authentication Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("accessToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("expiresIn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("userData"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loginTime"));

            EditorGUILayout.Space();

            // Auto Settings (collapsible)
            bool autoSettings = EditorGUILayout.Foldout(this.showAutoSettings, "Auto Settings", true);
            if (autoSettings != this.showAutoSettings) this.showAutoSettings = autoSettings;
            if (this.showAutoSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoLogin"), new GUIContent("Auto Login On Start"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Auto Refresh Settings (collapsible)
            bool autoRefresh = EditorGUILayout.Foldout(this.showAutoRefreshSettings, "Auto Refresh Settings", true);
            if (autoRefresh != this.showAutoRefreshSettings) this.showAutoRefreshSettings = autoRefresh;
            if (this.showAutoRefreshSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRefreshToken"), new GUIContent("Auto Refresh Token"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshBeforeExpire"), new GUIContent("Refresh Before Expire"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Login Inputs (collapsible)
            bool loginInputs = EditorGUILayout.Foldout(this.showLoginInputs, "Login Inputs", true);
            if (loginInputs != this.showLoginInputs) this.showLoginInputs = loginInputs;
            if (this.showLoginInputs)
            {
                EditorGUI.indentLevel++;
                this.DrawInputWithSaveLoad(
                    serializedObject.FindProperty("username"),
                    "Username",
                    saiAuth.SaveUsername,
                    saiAuth.LoadUsername);
                this.DrawInputWithSaveLoad(
                    serializedObject.FindProperty("password"),
                    "Password",
                    saiAuth.SavePassword,
                    saiAuth.LoadPassword);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Register Inputs (collapsible)
            bool registerInputs = EditorGUILayout.Foldout(this.showRegisterInputs, "Register Inputs", true);
            if (registerInputs != this.showRegisterInputs) this.showRegisterInputs = registerInputs;
            if (this.showRegisterInputs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("registerEmail"), new GUIContent("Register Email"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("registerUsername"), new GUIContent("Register Username"));
                this.DrawPasswordPropertyField(serializedObject.FindProperty("registerPassword"), "Register Password");
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);
            string toggleLabel = this.showPasswords ? "Hide Passwords" : "Show Passwords";
            if (GUILayout.Button(toggleLabel, GUILayout.Width(140)))
            {
                this.showPasswords = !this.showPasswords;
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Register", GUILayout.Height(25)))
            {
                saiAuth.Register(
                    serializedObject.FindProperty("registerEmail").stringValue,
                    serializedObject.FindProperty("registerUsername").stringValue,
                    serializedObject.FindProperty("registerPassword").stringValue,
                    response => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Registration success! User: {response.user.username} ({response.user.email}), Active: {response.user.is_active}, Verified: {response.user.is_verified}"); },
                    error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Registration failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Login", GUILayout.Height(25)))
            {
                saiAuth.Login(
                    serializedObject.FindProperty("username").stringValue,
                    serializedObject.FindProperty("password").stringValue,
                    response => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Login success! User: {response.user.username}, Token expires in: {response.expires_in}s"); },
                    error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Login failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("Logout", GUILayout.Height(25)))
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
                        response => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Token refreshed successfully! New token expires in: {response.expires_in}s"); },
                        error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Refresh token failed: {error}"); }
                    );
                }
                else
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
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
                        userData => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Profile retrieved: {userData.username} ({userData.email}), Active: {userData.is_active}, Verified: {userData.is_verified}"); },
                        error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Get profile failed: {error}"); }
                    );
                }
                else
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogWarning("[Editor] Not authenticated! Please login first.");
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(25)))
            {
                saiAuth.ManualClearCredentials();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }
}
