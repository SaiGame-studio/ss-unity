using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    // Steam is not a separate login: the Steam client running the game already
    // identifies the player. This Inspector walks through the link in order:
    // Step 1 the game's Steam AppID, Step 2 signing in to a Sai account,
    // Step 3 linking the running Steam account to it. Each step is enabled only
    // when the previous one is done. Signing in afterwards is SteamLogin.
    [CustomEditor(typeof(SteamLink))]
    public class SteamLinkEditor : Editor
    {
        private const string PREF_SETTINGS = "SteamLinkEditor.showSteamSettings";
        private const string PREF_LAST_RESULT = "SteamLinkEditor.showLastResult";
        private const string SaiAuthUsernameField = "username";
        private const string SaiAuthPasswordField = "password";

        // An Authenticator code is 6 digits; any other value is sent as a recovery code.
        private const int AuthenticatorCodeLength = 6;

        private string linkCode = "";

        private static bool ShowSettings
        {
            get => EditorPrefs.GetBool(PREF_SETTINGS, false);
            set => EditorPrefs.SetBool(PREF_SETTINGS, value);
        }

        private static bool ShowLastResult
        {
            get => EditorPrefs.GetBool(PREF_LAST_RESULT, false);
            set => EditorPrefs.SetBool(PREF_LAST_RESULT, value);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            SteamLink steamLink = (SteamLink)target;
            SaiServer server = SteamEditorUtility.FindServer(steamLink);

            SteamLogin steamLogin = server != null ? server.SteamLogin : steamLink.GetComponent<SteamLogin>();
            SteamLinkState linkState = steamLogin != null ? steamLogin.LinkState : SteamLinkState.Unknown;
            EditorGUILayout.BeginHorizontal();
            switch (linkState)
            {
                case SteamLinkState.Linked:
                    SteamEditorUtility.DrawStatus(true, "steam account linked");
                    break;
                case SteamLinkState.NotLinked:
                    SteamEditorUtility.DrawStatus(false, "steam account not linked");
                    break;
                default:
                    EditorGUILayout.LabelField("link status unknown: run Check Link");
                    break;
            }
            GUILayout.FlexibleSpace();
            SteamEditorUtility.DrawSelectButton(steamLogin, nameof(SteamLogin));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            bool appIdReady;
            bool signedIn;
            using (new EditorGUI.DisabledScope(!steamLink.CanLink))
            {
                EditorGUILayout.LabelField("step 1: steam app id", EditorStyles.boldLabel);
                appIdReady = SteamEditorUtility.DrawAppId(steamLink, server);

                using (new EditorGUI.DisabledScope(!appIdReady))
                    signedIn = this.DrawStep2SignIn(server);
            }

            this.DrawTwoFactorStatus(steamLink, signedIn);

            serializedObject.Update();
            EditorGUILayout.Space(10);
            SerializedProperty autoLink = serializedObject.FindProperty("autoLinkOnLoginSuccess");
            autoLink.boolValue = EditorGUILayout.ToggleLeft("auto link on login success",
                autoLink.boolValue, EditorStyles.wordWrappedLabel);
            serializedObject.ApplyModifiedProperties();

            using (new EditorGUI.DisabledScope(!steamLink.CanLink || !appIdReady || !signedIn))
                this.DrawStep3Link(steamLink);

            if (linkState == SteamLinkState.Linked)
            {
                EditorGUILayout.HelpBox("Steam account linked successfully. Return to SteamLogin and use Sign In With Linked Steam to continue.", MessageType.Info);
                using (new EditorGUI.DisabledScope(steamLogin == null))
                {
                    if (GUILayout.Button("Continue with SteamLogin", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        SteamEditorUtility.Select(steamLogin);
                }
            }

            if (steamLink.IsBusy && GUILayout.Button("Cancel", EditorStyles.miniButton))
                steamLink.Cancel();
            SteamEditorUtility.DrawLastError(steamLink);

            serializedObject.Update();
            EditorGUILayout.Space(10);
            bool showResult = EditorGUILayout.Foldout(ShowLastResult, "last result", true);
            if (showResult != ShowLastResult) ShowLastResult = showResult;
            if (showResult) { EditorGUI.indentLevel++; SteamEditorUtility.DrawResult(serializedObject); EditorGUI.indentLevel--; }
            bool showSettings = EditorGUILayout.Foldout(ShowSettings, "steam settings", true);
            if (showSettings != ShowSettings) ShowSettings = showSettings;
            if (showSettings)
            {
                EditorGUI.indentLevel++;
                SteamEditorUtility.DrawTicketSettings(serializedObject);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("steamLogin"), new GUIContent("steam login"));
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
        }

        // Step 2: a signed-in Sai account, by password (SaiAuth) or Google (GoogleBackendLogin).
        private bool DrawStep2SignIn(SaiServer server)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 2: sai server sign in", EditorStyles.boldLabel);

            SaiAuth saiAuth = server != null ? server.SaiAuth : null;
            GoogleBackendLogin google = server != null ? server.GoogleBackendLogin : null;
            bool signedIn = Application.isPlaying && server != null && server.IsAuthenticated;
            UserData user = saiAuth != null ? saiAuth.CurrentUser : null;

            SteamEditorUtility.DrawStatus(signedIn, signedIn
                ? $"signed in: <b>{(user != null ? user.username + " (" + user.email + ")" : "account")}</b>"
                : "not signed in (required to link)");

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(saiAuth == null || signedIn))
                {
                    if (GUILayout.Button("Password Login", GUILayout.Height(SteamEditorUtility.ButtonHeight))) PasswordLogin(saiAuth);
                }
                SteamEditorUtility.DrawSelectButton(saiAuth, nameof(SaiAuth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(google == null || signedIn || google.IsLoggingIn))
                {
                    if (GUILayout.Button("Google Login", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        google.StartLogin(response => SteamEditorUtility.LogInfo("Signed in with Google"), SteamEditorUtility.LogError);
                }
                SteamEditorUtility.DrawSelectButton(google, nameof(GoogleBackendLogin));
                EditorGUILayout.EndHorizontal();

                if (signedIn && saiAuth != null && GUILayout.Button("Logout", EditorStyles.miniButton))
                    saiAuth.Logout();
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Sign in appears in Play Mode.", MessageType.None);
            if (saiAuth != null && saiAuth.RequiresTwoFactor)
                EditorGUILayout.HelpBox("Password sign-in requires two-factor verification. Select SaiAuth to enter the Authenticator or recovery code.", MessageType.Warning);
            return signedIn;
        }

        private void DrawTwoFactorStatus(SteamLink steamLink, bool signedIn)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("two factor verification", EditorStyles.boldLabel);
            TwoFactorStatusResponse status = steamLink.TwoFactorStatus;
            if (status == null)
                EditorGUILayout.HelpBox("Status unknown. Sign in and press Check Two Factor Status to query the backend for this session.", MessageType.None);
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("two factor enabled", status.enabled);
                    EditorGUILayout.Toggle("verification required", status.verification_required);
                    EditorGUILayout.Toggle("verification available", status.available);
                    EditorGUILayout.TextField("last checked (UTC)", steamLink.TwoFactorStatusCheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                string message = !status.enabled ? "Two-factor authentication is not enabled for this account."
                    : status.verification_required ? "Enter an Authenticator or recovery code when linking Steam."
                    : "This session has recent two-factor verification. Another code is not currently required.";
                EditorGUILayout.HelpBox(message + " This is the last backend snapshot; refresh to recheck. No remaining grace time is returned by the backend.",
                    status.verification_required ? MessageType.Warning : MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !signedIn || steamLink.IsBusy))
            {
                if (GUILayout.Button("Check Two Factor Status", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                    steamLink.CheckTwoFactorStatus(_ => Repaint(), SteamEditorUtility.LogError);
            }
        }

        // Step 3: link the running Steam account to the signed-in Sai account.
        // SteamAPI starts automatically on the first request.
        private void DrawStep3Link(SteamLink steamLink)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 3: link steam", EditorStyles.boldLabel);
            SteamEditorUtility.DrawProviderProblems(steamLink);

            bool linkNeedsCode = steamLink.LastError == SteamErrorGuidance.LinkTwoFactorRequired
                || steamLink.LastError == SteamErrorGuidance.InvalidTwoFactorCode
                || (steamLink.TwoFactorStatus != null && steamLink.TwoFactorStatus.verification_required);
            if (linkNeedsCode)
                this.linkCode = EditorGUILayout.TextField("authenticator or recovery code", this.linkCode);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || steamLink.IsBusy))
            {
                if (GUILayout.Button("Link Steam", GUILayout.Height(30)))
                    this.Link(steamLink);
            }
            EditorGUILayout.HelpBox("Linking does not start a Steam game session or claim items. Sign in with SteamLogin after linking.", MessageType.Info);
        }

        // Signs in with the username and password set on the SaiAuth component (.env or its Inspector).
        private static void PasswordLogin(SaiAuth saiAuth)
        {
            var auth = new SerializedObject(saiAuth);
            string username = auth.FindProperty(SaiAuthUsernameField).stringValue;
            string password = auth.FindProperty(SaiAuthPasswordField).stringValue;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[Editor] Set username and password on SaiAuth (or USERNAME / PASSWORD in .env) first.", saiAuth);
                SteamEditorUtility.Select(saiAuth);
                return;
            }
            saiAuth.Login(username, password, response => SteamEditorUtility.LogInfo("Signed in with password"), SteamEditorUtility.LogError);
        }

        private void Link(SteamLink steamLink)
        {
            string code = this.linkCode.Trim();
            bool authenticator = code.Length == AuthenticatorCodeLength && IsDigits(code);
            steamLink.Link(result =>
            {
                this.linkCode = "";
                SteamEditorUtility.LogResult("Steam linked", result);
            }, SteamEditorUtility.LogError, authenticator ? code : null, authenticator || code.Length == 0 ? null : code);
        }

        private static bool IsDigits(string value)
        {
            foreach (char c in value)
                if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
