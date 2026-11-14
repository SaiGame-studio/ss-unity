using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SteamBackendLogin))]
    public class SteamBackendLoginEditor : Editor
    {
        private const string PREF_STEAM_SETTINGS = "SteamBackendLoginEditor.showSteamSettings";

        private const string NotLinked = "STEAM_NOT_LINKED";
        private const string TwoFactorRequired = "TWO_FACTOR_REQUIRED";
        private const string LinkTwoFactorRequired = "STEAM_LINK_TWO_FACTOR_REQUIRED";
        private const string LinkReauthRequired = "STEAM_LINK_REAUTH_REQUIRED";
        private const string LinkConflict = "STEAM_LINK_CONFLICT";
        private const string InvalidTwoFactorCode = "TWO_FACTOR_INVALID_CODE";
        private const string GameNotOwned = "STEAM_GAME_NOT_OWNED";
        private const string NotLoggedOn = "STEAM_NOT_LOGGED_ON";
        private const string NotInitialized = "STEAM_NOT_INITIALIZED";
        private const string ProviderRequired = "STEAM_TICKET_PROVIDER_REQUIRED";
        private const string SteamworksMissing = "STEAMWORKS_NET_MISSING";
        private const string Unauthorized = "UNAUTHORIZED";

        private string loginTwoFactorCode = "";
        private string linkTwoFactorCode = "";
        private string linkRecoveryCode = "";

        private bool showSteamSettings
        {
            get => EditorPrefs.GetBool(PREF_STEAM_SETTINGS, true);
            set => EditorPrefs.SetBool(PREF_STEAM_SETTINGS, value);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SteamBackendLogin login = (SteamBackendLogin)target;

            EditorGUILayout.LabelField("authentication data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("accessToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("refreshToken"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("expiresIn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("userData"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loginTime"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("steam state", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isLoggingIn"), new GUIContent("is logging in"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("requiresTwoFactor"), new GUIContent("requires two factor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ownership"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lastError"), new GUIContent("last error"));

            EditorGUILayout.Space();

            bool steamSettings = EditorGUILayout.Foldout(this.showSteamSettings, "steam settings", true);
            if (steamSettings != this.showSteamSettings) this.showSteamSettings = steamSettings;
            if (this.showSteamSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ticketProvider"), new GUIContent("ticket provider"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ticketTimeoutSeconds"), new GUIContent("ticket timeout seconds"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            this.DrawSteamClient(login);
            this.DrawSignedInAccount();
            this.DrawActions(login);
            this.DrawTwoFactor(login);
            this.DrawLink(login);
            this.DrawLastError(login);
        }

        private void DrawSteamClient(SteamBackendLogin login)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("steam client", EditorStyles.boldLabel);

            SteamworksNetTicketProvider provider = login.TicketProvider as SteamworksNetTicketProvider;
            if (login.TicketProvider == null)
            {
                EditorGUILayout.HelpBox("Assign a ticket provider (for example SteamworksNetTicketProvider) in steam settings.", MessageType.Warning);
                return;
            }
            if (provider == null)
            {
                EditorGUILayout.HelpBox("Using a custom ISteamTicketProvider.", MessageType.None);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Steam client status appears in Play Mode.", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("steamworks available", provider.SteamworksAvailable);
                EditorGUILayout.Toggle("steam initialized", provider.SteamInitialized);
                EditorGUILayout.TextField("local steam id", provider.LocalSteamId);
            }

            if (!string.IsNullOrEmpty(provider.InitError))
                EditorGUILayout.HelpBox(provider.InitError + "\n" + Guidance(provider.InitError), MessageType.Warning);

            using (new EditorGUI.DisabledScope(!provider.SteamworksAvailable))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Initialize Steam", GUILayout.Height(20))) provider.InitializeSteam();
                if (GUILayout.Button("Refresh Steam State", GUILayout.Height(20))) provider.RefreshState();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSignedInAccount()
        {
            if (!Application.isPlaying) return;

            SaiServer server = SaiServer.Instance;
            UserData user = server != null && server.SaiAuth != null ? server.SaiAuth.CurrentUser : null;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("signed in account", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("sai account", user != null ? user.username + " (" + user.email + ")" : "not signed in");
            }
        }

        private void DrawActions(SteamBackendLogin login)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(login.IsLoggingIn))
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Login With Steam", GUILayout.Height(25)))
                        login.StartLogin(result => this.LogSuccess("Steam login completed", result), this.LogError);
                    GUI.backgroundColor = Color.white;
                }

                GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
                if (GUILayout.Button("Cancel Login", GUILayout.Height(25)))
                    login.CancelLogin();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("Clear Auth Data", GUILayout.Height(25)))
                    login.ClearAuthData();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("Set Token To SaiAuth", GUILayout.Height(25)))
                    login.SetTokenToSaiAuth();
                GUI.backgroundColor = Color.white;

                using (new EditorGUI.DisabledScope(login.IsLoggingIn))
                {
                    GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                    if (GUILayout.Button("Claim Rewards", GUILayout.Height(25)))
                        login.ClaimRewards(result => this.LogSuccess("Steam reward checked", result), this.LogError);
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTwoFactor(SteamBackendLogin login)
        {
            if (!Application.isPlaying || !login.RequiresTwoFactor) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("two factor login", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This account requires an Authenticator code to finish Steam login.", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            this.loginTwoFactorCode = EditorGUILayout.TextField("authenticator code", this.loginTwoFactorCode);
            using (new EditorGUI.DisabledScope(login.IsLoggingIn))
            {
                if (GUILayout.Button("Verify", GUILayout.Width(80)))
                {
                    login.VerifyTwoFactor(this.loginTwoFactorCode, result =>
                    {
                        this.loginTwoFactorCode = "";
                        if (this.ShowDebug) Debug.Log("[Editor] Steam login completed with 2FA");
                    }, this.LogError);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLink(SteamBackendLogin login)
        {
            if (!Application.isPlaying) return;

            SaiServer server = SaiServer.Instance;
            bool signedIn = server != null && server.IsAuthenticated;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("link steam to signed in account", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Steam never creates an account. Sign in first with the GoogleBackendLogin or SaiAuth Inspector, then link. " +
                "Accounts with 2FA enter an Authenticator or recovery code. " +
                "Accounts without 2FA must have signed in within the last 10 minutes.", MessageType.None);

            this.linkTwoFactorCode = EditorGUILayout.TextField("authenticator code", this.linkTwoFactorCode);
            this.linkRecoveryCode = EditorGUILayout.TextField("recovery code", this.linkRecoveryCode);

            using (new EditorGUI.DisabledScope(!signedIn || login.IsLoggingIn))
            {
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button(signedIn ? "Link Steam" : "Link Steam (sign in first)", GUILayout.Height(25)))
                {
                    login.LinkAccount(result =>
                    {
                        this.linkTwoFactorCode = "";
                        this.linkRecoveryCode = "";
                        this.LogSuccess("Steam linked", result);
                    }, this.LogError, this.linkTwoFactorCode, this.linkRecoveryCode);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawLastError(SteamBackendLogin login)
        {
            if (string.IsNullOrEmpty(login.LastError)) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(login.LastError + "\n" + Guidance(login.LastError), MessageType.Error);
        }

        private static string Guidance(string error)
        {
            switch (error)
            {
                case NotLinked: return "This Steam account is not linked. Sign in with Google or password, then press Link Steam.";
                case TwoFactorRequired: return "Enter the Authenticator code in two factor login, then press Verify.";
                case LinkTwoFactorRequired: return "This account has 2FA. Enter an Authenticator or recovery code, then press Link Steam.";
                case InvalidTwoFactorCode: return "The code is invalid, expired, or already used. Enter a new code.";
                case LinkReauthRequired: return "Your sign-in is older than 10 minutes. Sign in again, then press Link Steam.";
                case LinkConflict: return "This Steam account or this user is already linked to another account.";
                case GameNotOwned: return "This Steam account does not own the game.";
                case NotLoggedOn: return "Start the Steam client and sign in, then press Refresh Steam State.";
                case NotInitialized: return "SteamAPI is not initialized. Initialize it in the game, or enable initialize steam api on SteamworksNetTicketProvider. Check steam_appid.txt when testing in the Editor.";
                case ProviderRequired: return "Assign a ticket provider in steam settings.";
                case SteamworksMissing: return "Install the Steamworks.NET package (com.rlabrecque.steamworks.net).";
                case Unauthorized: return "Sign in with Google or password first.";
                default: return "";
            }
        }

        private bool ShowDebug => SaiServer.Instance == null || SaiServer.Instance.ShowDebug;

        private void LogSuccess(string message, SteamLoginResponse result)
        {
            if (!this.ShowDebug) return;
            string reward = result != null && result.reward != null ? result.reward.status : "none";
            Debug.Log($"[Editor] {message} (reward: {reward})");
        }

        private void LogError(string error)
        {
            if (this.ShowDebug) Debug.LogWarning($"[Editor] Steam operation failed: {error}");
        }
    }
}
