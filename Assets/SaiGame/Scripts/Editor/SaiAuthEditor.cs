using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiAuth))]
    public class SaiAuthEditor : Editor
    {
        private const string PREF_AUTO_SETTINGS = "SaiAuthEditor.showAutoSettings";
        private const string PREF_LOGIN_INPUTS = "SaiAuthEditor.showLoginInputs";
        private const string PREF_REGISTER_INPUTS = "SaiAuthEditor.showRegisterInputs";
        private const string PREF_SHOW_PASSWORDS = "SaiAuthEditor.showPasswords";
        private const string LoginColor = "#00FF00";
        private const string TwoFactorColor = "#66E6FF";
        private const string LogoutColor = "#FFB34D";

        private static bool AuthButton(string label, string colorCode, params GUILayoutOption[] options)
        {
            Color previous = GUI.backgroundColor;
            ColorUtility.TryParseHtmlString(colorCode, out Color color);
            GUI.backgroundColor = color;
            bool pressed = GUILayout.Button(label, options);
            GUI.backgroundColor = previous;
            if (pressed && (SaiServer.Instance == null || SaiServer.Instance.ShowDebug))
                Debug.Log($"<color={colorCode}>[Editor] {label}</color>");
            return pressed;
        }

        private static void LogAuth(string message, string color, bool error = false, bool warning = false)
        {
            if (SaiServer.Instance != null && !SaiServer.Instance.ShowDebug) return;
            string log = $"<color={color}>[Editor] {message}</color>";
            if (warning) Debug.LogWarning(log);
            else if (error) Debug.LogError(log);
            else Debug.Log(log);
        }

        private string twoFactorCode = string.Empty;

        private bool showAutoSettings
        {
            get => EditorPrefs.GetBool(PREF_AUTO_SETTINGS, true);
            set => EditorPrefs.SetBool(PREF_AUTO_SETTINGS, value);
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
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is Login", saiAuth.IsAuthenticated);
                EditorGUILayout.Toggle("Requires Two Factor", saiAuth.RequiresTwoFactor);
            }
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
                EditorGUILayout.PropertyField(serializedObject.FindProperty("persistTokens"), new GUIContent("Persist Tokens"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoLogin"), new GUIContent("Auto Login On Start"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRefreshToken"), new GUIContent("Auto Refresh Token"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshBeforeExpireMinutes"), new GUIContent("Refresh Before Expire (minutes)", "Minutes before the access token expires at which it is refreshed automatically."));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Login Inputs (collapsible)
            bool loginInputs = EditorGUILayout.Foldout(this.showLoginInputs, "Login Inputs", true);
            if (loginInputs != this.showLoginInputs) this.showLoginInputs = loginInputs;
            if (this.showLoginInputs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("username"), new GUIContent("Username"));
                this.DrawPasswordPropertyField(serializedObject.FindProperty("password"), "Password");
                EditorGUI.indentLevel--;

            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUI.DisabledScope(saiAuth.RequiresTwoFactor))
                {
                    if (AuthButton("Login (Check 2FA)", LoginColor, GUILayout.Height(25)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        saiAuth.Login(
                            serializedObject.FindProperty("username").stringValue,
                            serializedObject.FindProperty("password").stringValue,
                            response => { LogAuth("Login successful.", LoginColor); Repaint(); },
                            error =>
                            {
                                bool requiresTwoFactor = error != null && error.StartsWith("TWO_FACTOR_REQUIRED:", System.StringComparison.Ordinal);
                                LogAuth(requiresTwoFactor ? error : $"Login failed: {error}", LoginColor,
                                    error: !requiresTwoFactor, warning: requiresTwoFactor);
                                Repaint();
                            });
                    }
                }
                using (new EditorGUI.DisabledScope(!saiAuth.RequiresTwoFactor))
                    this.twoFactorCode = EditorGUILayout.TextField("2FA Code", this.twoFactorCode);
                using (new EditorGUI.DisabledScope(!saiAuth.RequiresTwoFactor))
                {
                    if (AuthButton("2FA Login", TwoFactorColor, GUILayout.Height(25)))
                    {
                        string code = this.twoFactorCode;
                        this.twoFactorCode = string.Empty;
                        saiAuth.VerifyTwoFactor(code,
                            response => { LogAuth("2FA login successful.", TwoFactorColor); Repaint(); },
                            error => { LogAuth($"2FA login failed: {error}", TwoFactorColor, true); Repaint(); });
                    }
                }
                if (AuthButton("Logout", LogoutColor, GUILayout.Height(25)))
                    saiAuth.Logout();
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

                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.Space(2);
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
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("Refresh Token", GUILayout.Height(25)))
            {
                if (saiAuth.HasAccessToken)
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
                if (saiAuth.HasAccessToken)
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
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }
}
