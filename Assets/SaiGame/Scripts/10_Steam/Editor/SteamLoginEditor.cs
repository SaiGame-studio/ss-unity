using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    // Signing in with the Sai account linked to the running Steam account.
    // Check Link asks the Sai Server whether that Steam account is linked; Sign In
    // With Linked Steam is enabled only once it is.
    [CustomEditor(typeof(SteamLogin))]
    public class SteamLoginEditor : Editor
    {
        private const string PREF_SETTINGS = "SteamLoginEditor.showSteamSettings";
        private const string PREF_LAST_RESULT = "SteamLoginEditor.showLastResult";

        private string signInCode = "";

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
            SteamLogin steamLogin = (SteamLogin)target;
            SaiServer server = SteamEditorUtility.FindServer(steamLogin);

            // The session created by the last Steam sign-in, shown as on GoogleBackendLogin.
            serializedObject.Update();
            EditorGUILayout.LabelField("authentication data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("accessToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("expiresIn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("userData"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("signInTime"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("steam app id", EditorStyles.boldLabel);
            bool appIdReady = SteamEditorUtility.DrawAppId(steamLogin, server);

            using (new EditorGUI.DisabledScope(!appIdReady))
                this.DrawSignIn(steamLogin, server);

            if (steamLogin.IsBusy && GUILayout.Button("Cancel", EditorStyles.miniButton))
                steamLogin.Cancel();
            SteamEditorUtility.DrawLastError(steamLogin);

            serializedObject.Update();
            EditorGUILayout.Space(10);
            bool showResult = EditorGUILayout.Foldout(ShowLastResult, "last result", true);
            if (showResult != ShowLastResult) ShowLastResult = showResult;
            if (showResult) { EditorGUI.indentLevel++; SteamEditorUtility.DrawResult(serializedObject); EditorGUI.indentLevel--; }
            bool showSettings = EditorGUILayout.Foldout(ShowSettings, "steam settings", true);
            if (showSettings != ShowSettings) ShowSettings = showSettings;
            if (showSettings) { EditorGUI.indentLevel++; SteamEditorUtility.DrawTicketSettings(serializedObject); EditorGUI.indentLevel--; }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSignIn(SteamLogin steamLogin, SaiServer server)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("sign in with linked steam", EditorStyles.boldLabel);
            SteamEditorUtility.DrawProviderProblems(steamLogin);

            switch (steamLogin.LinkState)
            {
                case SteamLinkState.Linked:
                    SteamEditorUtility.DrawStatus(true, $"steam account linked{SteamIdSuffix(steamLogin)}");
                    break;
                case SteamLinkState.NotLinked:
                    SteamEditorUtility.DrawStatus(false, $"steam account not linked{SteamIdSuffix(steamLogin)}");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox("This Steam account is not linked to a Sai account. Link it with SteamLink first.", MessageType.Warning);
                    SteamLink steamLink = server != null ? server.SteamLink : steamLogin.GetComponent<SteamLink>();
                    SteamEditorUtility.DrawSelectButton(steamLink, nameof(SteamLink));
                    EditorGUILayout.EndHorizontal();
                    break;
                default:
                    SteamEditorUtility.DrawStatus(false, "link not checked yet: press Check Link");
                    break;
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || steamLogin.IsBusy))
            {
                EditorGUILayout.BeginHorizontal();
                if (SteamEditorUtility.SteamButton("Check Link", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                    steamLogin.CheckLinked(linked => SteamEditorUtility.LogInfo(linked ? "Steam account is linked" : "Steam account is not linked"),
                        SteamEditorUtility.LogError);
                using (new EditorGUI.DisabledScope(steamLogin.LinkState != SteamLinkState.Linked))
                {
                    if (SteamEditorUtility.SteamButton("Sign In With Linked Steam", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        steamLogin.SignIn(result => SteamEditorUtility.LogResult("Signed in with the linked Steam account", result),
                            SteamEditorUtility.LogError);
                }
                EditorGUILayout.EndHorizontal();

                // Pushes the session from the last sign-in into SaiAuth again, as on GoogleBackendLogin.
                using (new EditorGUI.DisabledScope(!steamLogin.IsAuthenticated))
                {
                    if (SteamEditorUtility.SteamButton("Set Token To SaiAuth", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        steamLogin.SetTokenToSaiAuth();
                }
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Check Link and sign in appear in Play Mode.", MessageType.None);

            if (steamLogin.RequiresTwoFactor) this.DrawTwoFactor(steamLogin);
        }

        // Shown only when the linked Sai account has 2FA and the server asks for a code.
        private void DrawTwoFactor(SteamLogin steamLogin)
        {
            EditorGUILayout.BeginHorizontal();
            this.signInCode = EditorGUILayout.TextField("authenticator code", this.signInCode);
            using (new EditorGUI.DisabledScope(steamLogin.IsBusy))
            {
                if (SteamEditorUtility.SteamButton("Verify", GUILayout.Width(80)))
                {
                    steamLogin.VerifyTwoFactor(this.signInCode, result =>
                    {
                        this.signInCode = "";
                        SteamEditorUtility.LogInfo("Signed in with the linked Steam account (2FA)");
                    }, SteamEditorUtility.LogError);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string SteamIdSuffix(SteamLogin steamLogin)
        {
            return string.IsNullOrEmpty(steamLogin.SteamId) ? "" : $" (steam id <b>{steamLogin.SteamId}</b>)";
        }
    }
}
