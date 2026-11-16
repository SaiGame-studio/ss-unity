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
                using (new EditorGUI.DisabledScope(saiAuth == null))
                {
                    if (GUILayout.Button("Password Login", GUILayout.Height(SteamEditorUtility.ButtonHeight))) SteamEditorUtility.Select(saiAuth);
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

        // Step 3: link the running Steam account to the signed-in Sai account.
        // SteamAPI starts automatically on the first request.
        private void DrawStep3Link(SteamLink steamLink)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 3: link steam", EditorStyles.boldLabel);
            SteamEditorUtility.DrawProviderProblems(steamLink);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || steamLink.IsBusy))
            {
                if (SteamEditorUtility.SteamButton("Link Steam", GUILayout.Height(30)))
                    this.Link(steamLink);
            }
            EditorGUILayout.HelpBox("Linking does not start a Steam game session or claim items. Sign in with SteamLogin after linking.", MessageType.Info);
        }

        private void Link(SteamLink steamLink)
        {
            steamLink.Link(result => SteamEditorUtility.LogResult("Steam linked", result),
                SteamEditorUtility.LogError);
        }
    }
}
