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

            EditorGUILayout.LabelField("steam session tokens", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("access token", steamLogin.AccessToken ?? "");
                EditorGUILayout.TextField("refresh token", steamLogin.RefreshToken ?? "");
            }
            EditorGUILayout.Space(10);

            this.DrawFullAuto(steamLogin, server);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("steam app id", EditorStyles.boldLabel);
            bool appIdReady = SteamEditorUtility.DrawAppId(steamLogin, server);

            this.DrawSignIn(steamLogin, server, appIdReady);

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

        private void DrawFullAuto(SteamLogin steamLogin, SaiServer server)
        {
            serializedObject.Update();
            SerializedProperty[] autoProperties =
            {
                serializedObject.FindProperty("autoCheckOnStart"),
                serializedObject.FindProperty("autoSignInOnCheckLinkSuccess"),
                serializedObject.FindProperty("autoCheckClaimOnSignInSuccess"),
                serializedObject.FindProperty("autoClaimGameOnCheckClaimSuccess")
            };

            SteamLink steamLink = server != null ? server.SteamLink : steamLogin.GetComponent<SteamLink>();
            SerializedObject linkObject = steamLink != null ? new SerializedObject(steamLink) : null;
            linkObject?.Update();
            SerializedProperty autoLink = linkObject?.FindProperty("autoLinkOnLoginSuccess");
            bool allEnabled = autoLink == null || autoLink.boolValue;
            bool anyEnabled = autoLink != null && autoLink.boolValue;
            foreach (SerializedProperty property in autoProperties)
            {
                allEnabled &= property.boolValue;
                anyEnabled |= property.boolValue;
            }

            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = anyEnabled && !allEnabled;
            EditorGUI.BeginChangeCheck();
            bool fullAuto = EditorGUILayout.ToggleLeft("full auto", allEnabled, EditorStyles.wordWrappedLabel);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = previousMixedValue;
            if (!changed) return;

            foreach (SerializedProperty property in autoProperties)
                property.boolValue = fullAuto;
            if (autoLink != null) autoLink.boolValue = fullAuto;
            serializedObject.ApplyModifiedProperties();
            linkObject?.ApplyModifiedProperties();
        }

        private void DrawSignIn(SteamLogin steamLogin, SaiServer server, bool appIdReady)
        {
            SteamEditorUtility.DrawProviderProblems(steamLogin);
            SteamLink steamLink = server != null ? server.SteamLink : steamLogin.GetComponent<SteamLink>();
            bool busy = steamLogin.IsBusy || (steamLink != null && steamLink.IsBusy);
            bool operationsDisabled = !appIdReady || busy;

            serializedObject.Update();
            EditorGUILayout.Space(10);
            SerializedProperty autoCheckOnStart = serializedObject.FindProperty("autoCheckOnStart");
            autoCheckOnStart.boolValue = EditorGUILayout.ToggleLeft("auto check on start",
                autoCheckOnStart.boolValue, EditorStyles.wordWrappedLabel);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 1: check steam link", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || operationsDisabled))
            {
                if (SteamEditorUtility.SteamButton("Check Link", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                    steamLogin.CheckLinked(linked => SteamEditorUtility.LogInfo(linked ? "Steam account is linked" : "Steam account is not linked"),
                        SteamEditorUtility.LogError);
            }

            this.DrawLinkStatus(steamLogin, server);
            serializedObject.Update();
            EditorGUILayout.Space(10);
            SerializedProperty autoSignIn = serializedObject.FindProperty("autoSignInOnCheckLinkSuccess");
            autoSignIn.boolValue = EditorGUILayout.ToggleLeft("auto sign in on check link success",
                autoSignIn.boolValue, EditorStyles.wordWrappedLabel);
            serializedObject.ApplyModifiedProperties();
            this.DrawStep2SignIn(steamLogin, operationsDisabled);
            serializedObject.Update();
            EditorGUILayout.Space(10);
            SerializedProperty autoCheckClaim = serializedObject.FindProperty("autoCheckClaimOnSignInSuccess");
            autoCheckClaim.boolValue = EditorGUILayout.ToggleLeft("auto check claim",
                autoCheckClaim.boolValue, EditorStyles.wordWrappedLabel);
            SerializedProperty autoClaimGame = serializedObject.FindProperty("autoClaimGameOnCheckClaimSuccess");
            autoClaimGame.boolValue = EditorGUILayout.ToggleLeft("auto claim game",
                autoClaimGame.boolValue, EditorStyles.wordWrappedLabel);
            serializedObject.ApplyModifiedProperties();
            this.DrawStep3ClaimGame(steamLogin, operationsDisabled);
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Steam operations are available in Play Mode.", MessageType.None);
        }

        private void DrawStep2SignIn(SteamLogin steamLogin, bool busy)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 2: sign in with linked steam", EditorStyles.boldLabel);
            SteamEditorUtility.DrawStatus(steamLogin.HasActiveSteamSession, steamLogin.HasActiveSteamSession
                ? "signed in with Steam: game session ready"
                : "Steam sign-in required before Claim Game");
            using (new EditorGUI.DisabledScope(!Application.isPlaying || busy
                || steamLogin.LinkState != SteamLinkState.Linked))
            {
                if (SteamEditorUtility.SteamButton("Sign In With Linked Steam", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                    steamLogin.SignIn(result => SteamEditorUtility.LogResult("Signed in with the linked Steam account", result),
                        SteamEditorUtility.LogError);
                if (steamLogin.RequiresTwoFactor) this.DrawTwoFactor(steamLogin);
                using (new EditorGUI.DisabledScope(!steamLogin.IsAuthenticated))
                {
                    if (SteamEditorUtility.SteamButton("Set Token To SaiAuth", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        steamLogin.SetTokenToSaiAuth();
                }
            }
        }

        private void DrawStep3ClaimGame(SteamLogin steamLogin, bool busy)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("step 3: claim game", EditorStyles.boldLabel);
            SteamClaimStatus check = steamLogin.CheckedClaim;
            string claimStatus = steamLogin.ClaimGameResult?.status;
            bool claimed = check != null && check.claimed || claimStatus == "granted"
                || claimStatus == "already_granted" || claimStatus == "transferred";
            SteamEditorUtility.DrawStatus(claimed, claimed ? "game already claimed"
                : claimStatus == "pending" ? $"claim game pending ({steamLogin.ClaimGameResult.message_code})"
                : check == null ? "claim status unknown: press Check Claim"
                : check.can_claim ? "ready to claim" : $"cannot claim ({check.message_code})");
            if (claimStatus == "pending" || claimStatus == "ineligible")
                EditorGUILayout.HelpBox($"Claim Game did not complete. Status: {claimStatus}. Reason: {steamLogin.ClaimGameResult.message_code}. "
                    + "Check the backend logs for the failed stage and original error. Run Check Claim before retrying.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || busy
                || steamLogin.LinkState != SteamLinkState.Linked || !steamLogin.HasActiveSteamSession))
            {
                EditorGUILayout.BeginHorizontal();
                if (SteamEditorUtility.SteamButton("Check Claim", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                    steamLogin.CheckClaim(result => SteamEditorUtility.LogInfo(result.claimed ? "Game already claimed" : result.can_claim ? "Game ready to claim" : result.message_code),
                        SteamEditorUtility.LogError);
                using (new EditorGUI.DisabledScope(!steamLogin.CanClaimGame || steamLogin.IsBusy))
                {
                    if (SteamEditorUtility.SteamButton("Claim Game", GUILayout.Height(SteamEditorUtility.ButtonHeight)))
                        steamLogin.ClaimGame(result => SteamEditorUtility.LogResult("Claim Game response", result),
                            SteamEditorUtility.LogError);
                }
                EditorGUILayout.EndHorizontal();
            }
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

        private void DrawLinkStatus(SteamLogin steamLogin, SaiServer server)
        {
            SteamLink steamLink = server != null ? server.SteamLink : steamLogin.GetComponent<SteamLink>();
            EditorGUILayout.BeginHorizontal();
            switch (steamLogin.LinkState)
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
            SteamEditorUtility.DrawSelectButton(steamLink, nameof(SteamLink));
            EditorGUILayout.EndHorizontal();
            if (steamLogin.LinkState == SteamLinkState.NotLinked)
                EditorGUILayout.HelpBox("Switch to SteamLink to continue linking, then return here to sign in.", MessageType.None);
        }
    }
}
