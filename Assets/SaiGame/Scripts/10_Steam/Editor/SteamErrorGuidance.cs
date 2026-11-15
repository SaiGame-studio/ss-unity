namespace SaiGame.Services
{
    // The next step to take for a Steam message_code, shown by the SteamLink and
    // SteamLogin Inspectors.
    public static class SteamErrorGuidance
    {
        public const string NotLinked = "STEAM_NOT_LINKED";
        public const string TwoFactorRequired = "TWO_FACTOR_REQUIRED";
        public const string LinkTwoFactorRequired = "STEAM_LINK_TWO_FACTOR_REQUIRED";
        public const string LinkReauthRequired = "STEAM_LINK_REAUTH_REQUIRED";
        public const string LinkConflict = "STEAM_LINK_CONFLICT";
        public const string InvalidTwoFactorCode = "TWO_FACTOR_INVALID_CODE";
        public const string GameNotOwned = "STEAM_GAME_NOT_OWNED";
        public const string NotLoggedOn = "STEAM_NOT_LOGGED_ON";
        public const string NotInitialized = "STEAM_NOT_INITIALIZED";
        public const string ProviderRequired = "STEAM_TICKET_PROVIDER_REQUIRED";
        public const string SteamworksMissing = "STEAMWORKS_NET_MISSING";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string NotConfigured = "STEAM_NOT_CONFIGURED";
        public const string PublisherKeyMissing = "STEAM_PUBLISHER_KEY_MISSING";
        public const string PublisherKeyRejected = "STEAM_PUBLISHER_KEY_REJECTED";
        public const string SteamTimeout = "STEAM_TIMEOUT";
        public const string SteamUnavailable = "STEAM_UNAVAILABLE";
        public const string SecretsUnavailable = "STEAM_SECRETS_UNAVAILABLE";

        public static string For(string error)
        {
            switch (error)
            {
                case NotLinked: return "This Steam account is not linked to a Sai account yet. Link it with SteamLink first.";
                case TwoFactorRequired: return "Enter the Authenticator code of the linked Sai account, then press Verify.";
                case LinkTwoFactorRequired: return "This Sai account has 2FA. Enter an Authenticator or recovery code, then press Link Steam.";
                case InvalidTwoFactorCode: return "The code is invalid, expired, or already used. Enter a new code.";
                case LinkReauthRequired: return "Your sign-in is older than 10 minutes. Logout, sign in again in step 2, then press Link Steam.";
                case LinkConflict: return "This Steam account or this Sai account is already linked to another account.";
                case GameNotOwned: return "This Steam account does not own the game.";
                case NotLoggedOn: return "Start the Steam client and sign in to Steam, then try again.";
                case NotInitialized: return "SteamAPI could not start. Check that the Steam client is running and the Steam App ID.";
                case ProviderRequired: return "Assign a ticket provider in steam settings.";
                case SteamworksMissing: return "Install the Steamworks.NET package (com.rlabrecque.steamworks.net).";
                case Unauthorized: return "Sign in to a Sai account first.";
                case NotConfigured: return "Steam is disabled for this game or has no App ID. Enable Steam and set the App ID in Game Settings > Steam on the Sai Server dashboard.";
                case PublisherKeyMissing: return "The game has no Steam publisher Web API key. Save it in Game Settings > Steam on the Sai Server dashboard.";
                case PublisherKeyRejected: return "Steam rejected the game's publisher Web API key. Check that it is a publisher key with the General permission for this App ID, then save it again.";
                case SteamTimeout: return "Steam did not answer the Sai Server in time. Try again later.";
                case SteamUnavailable: return "The Sai Server could not verify the ticket with Steam. Try again later; if it persists, check the server's [Steam] logs.";
                case SecretsUnavailable: return "The Sai Server cannot read game credentials. Check the server's encryption key configuration.";
                default: return "";
            }
        }
    }
}
