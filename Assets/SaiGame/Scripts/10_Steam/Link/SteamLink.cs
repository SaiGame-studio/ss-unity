using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    // Steam is not a separate login: the Steam client running the game already
    // identifies the player. Link links that Steam account once to the signed-in
    // Sai account. Signing in with the linked account is SteamLogin.
    [DefaultExecutionOrder(-99)]
    public class SteamLink : SteamTicketService
    {
        public const string LinkEndpoint = "/api/v1/player/auth/steam/link";
        private const string TwoFactorStatusEndpoint = "/api/v1/auth/2fa/status";

        [SerializeField] protected SteamLogin steamLogin;
        [Tooltip("Automatically link Steam after a successful Sai account login when Check Link confirms Steam is not linked.")]
        [SerializeField] private bool autoLinkOnLoginSuccess = false;

        private SaiAuth saiAuth;
        private GoogleBackendLogin googleBackendLogin;
        private TwoFactorStatusResponse twoFactorStatus;
        private string twoFactorStatusToken;
        private DateTime twoFactorStatusCheckedAt;

        // A debug snapshot, never authorization to bypass a backend challenge.
        public TwoFactorStatusResponse TwoFactorStatus => SaiServer.Instance != null
            && SaiServer.Instance.IsAuthenticated && SaiServer.Instance.AccessToken == twoFactorStatusToken
            ? twoFactorStatus : null;
        public DateTime TwoFactorStatusCheckedAt => twoFactorStatusCheckedAt;

        protected override string LogTag => "[SteamLink]";
        public bool CanLink => steamLogin != null && !steamLogin.IsBusy
            && steamLogin.LinkState == SteamLinkState.NotLinked;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (this.steamLogin == null) this.steamLogin = GetComponent<SteamLogin>();
        }

        private void OnEnable()
        {
            SaiServer server = SaiServer.Instance;
            if (server == null) return;
            saiAuth = server.SaiAuth;
            googleBackendLogin = server.GoogleBackendLogin;
            if (saiAuth != null) saiAuth.OnLoginSuccess += HandleLoginSuccess;
            if (googleBackendLogin != null) googleBackendLogin.OnLoginSuccess += HandleLoginSuccess;
        }

        protected override void OnDisable()
        {
            if (saiAuth != null) saiAuth.OnLoginSuccess -= HandleLoginSuccess;
            if (googleBackendLogin != null) googleBackendLogin.OnLoginSuccess -= HandleLoginSuccess;
            base.OnDisable();
        }

        private void HandleLoginSuccess(LoginResponse response)
        {
            this.twoFactorStatus = null;
            if (autoLinkOnLoginSuccess && !IsBusy && CanLink && SaiServer.Instance != null
                && SaiServer.Instance.IsAuthenticated && SaiServer.Instance.AccessToken == response.access_token)
                Link();
        }

        public void CheckTwoFactorStatus(Action<TwoFactorStatusResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Check Two Factor Status");
            if (!CanStart(true, onError)) return;
            this.twoFactorStatus = null;
            StartOperation(this.FetchTwoFactorStatus(SaiServer.Instance.AccessToken, onSuccess, onError));
        }

        private IEnumerator FetchTwoFactorStatus(string token, Action<TwoFactorStatusResponse> onSuccess, Action<string> onError)
        {
            string response = null;
            string error = null;
            yield return SteamBackendTransport.Get(TwoFactorStatusEndpoint, value => response = value, value => error = value);
            TwoFactorStatusResponse status = null;
            if (error == null)
            {
                try { status = JsonUtility.FromJson<TwoFactorStatusResponse>(response); }
                catch (Exception) { error = "TWO_FACTOR_INVALID_RESPONSE"; }
            }
            FinishOperation();
            if (SaiServer.Instance == null || !SaiServer.Instance.IsAuthenticated || SaiServer.Instance.AccessToken != token)
            {
                Fail("TWO_FACTOR_SESSION_CHANGED", onError);
                yield break;
            }
            if (error != null || status == null)
            {
                Fail(error ?? "TWO_FACTOR_INVALID_RESPONSE", onError);
                yield break;
            }
            this.twoFactorStatus = status;
            this.twoFactorStatusToken = token;
            this.twoFactorStatusCheckedAt = DateTime.UtcNow;
            Succeed("Check Two Factor Status", $"enabled={status.enabled}, verification_required={status.verification_required}, available={status.available}");
            onSuccess?.Invoke(status);
        }

        // Links the running Steam account to the signed-in Sai account.
        // Accounts with 2FA pass an Authenticator code or a recovery code.
        // Accounts without 2FA must have signed in within the backend's
        // re-authentication window, otherwise STEAM_LINK_REAUTH_REQUIRED.
        public void Link(Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null,
            string twoFactorCode = null, string recoveryCode = null)
        {
            LogButton("Link Steam");
            if (!CanLink)
            {
                Fail(this.steamLogin != null && this.steamLogin.LinkState == SteamLinkState.Linked
                    ? "STEAM_ALREADY_LINKED" : "STEAM_LINK_CHECK_REQUIRED", onError);
                return;
            }
            this.twoFactorStatus = null;
            StartTicketOperation(true, onError, ticket =>
            {
                SteamTicketRequest request = NewTicketRequest(ticket);
                request.code = twoFactorCode?.Trim();
                request.recovery_code = recoveryCode?.Trim();
                return PostTicket<SteamLinkResponse>(LinkEndpoint, request, result =>
                {
                    ClearResult();
                    this.ownership = result.ownership;
                    if (this.steamLogin == null) this.steamLogin = GetComponent<SteamLogin>();
                    if (this.steamLogin != null && result.ownership != null) this.steamLogin.MarkLinked(result.ownership.steam_id);
                    Succeed("Link Steam", "linked: sign in with SteamLogin to start the game session");
                    onSuccess?.Invoke(result);
                }, onError);
            });
        }

    }
}
