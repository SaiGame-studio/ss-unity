using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    // Signs into the Sai account linked to the running Steam account, with no
    // input. CheckLinked asks the Sai Server whether that Steam account is linked
    // without signing in; until it is, link it with SteamLink.
    [DefaultExecutionOrder(-99)]
    public class SteamLogin : SteamTicketService
    {
        public const string SignInEndpoint = "/api/v1/player/auth/steam";
        public const string StatusEndpoint = "/api/v1/player/auth/steam/status";
        public const string ClaimEndpoint = "/api/v1/player/auth/steam/rewards/claim";
        public const string CheckClaimEndpoint = "/api/v1/player/auth/steam/rewards/check";
        private SteamClaimStatus checkedClaim;
        private string checkedClaimToken;
        public SteamClaimStatus CheckedClaim => HasActiveSteamSession && checkedClaimToken == accessToken ? checkedClaim : null;
        public bool CanClaimGame => CheckedClaim != null && CheckedClaim.can_claim;

        private const string TwoFactorEndpoint = "/api/v1/auth/2fa/verify";
        private const string TwoFactorRequiredCode = "TWO_FACTOR_REQUIRED";
        private const string NotLinkedCode = "STEAM_NOT_LINKED";

        public event Action<SteamLinkResponse> OnSignedIn;
        public event Action<LoginResponse> OnTwoFactorRequired;

        [Header("Link")]
        [SerializeField] protected SteamLinkState linkState;
        [SerializeField] protected string steamId;

        [Header("Session")]
        [SerializeField] protected string accessToken;
        [SerializeField] protected string refreshToken;
        [SerializeField] protected int expiresIn;
        [SerializeField] protected UserData userData;
        [SerializeField] protected float signInTime;
        [SerializeField] protected bool requiresTwoFactor;

        private LoginResponse challenge;
        private DateTime challengeExpiresAt;

        protected override string LogTag => "[SteamLogin]";

        public SteamLinkState LinkState => linkState;
        public string SteamId => steamId;
        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public bool HasActiveSteamSession => IsAuthenticated && SaiServer.Instance != null
            && SaiServer.Instance.IsAuthenticated && SaiServer.Instance.AccessToken == accessToken;
        public bool RequiresTwoFactor => challenge != null && DateTime.UtcNow < challengeExpiresAt;
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;

        // Asks the Sai Server whether the running Steam account is linked. Never signs in.
        public void CheckLinked(Action<bool> onResult = null, Action<string> onError = null)
        {
            LogButton("Check Link");
            if (!CanStart(false, onError)) return;
            this.linkState = SteamLinkState.Unknown;
            checkedClaim = null;
            checkedClaimToken = null;
            StartTicketOperation(false, onError, ticket =>
                PostTicket<SteamLinkStatusResponse>(StatusEndpoint, NewTicketRequest(ticket), result =>
                {
                    this.steamId = result.steam_id;
                    this.linkState = result.linked ? SteamLinkState.Linked : SteamLinkState.NotLinked;
                    Succeed("Check Link", result.linked ? "linked" : "not linked");
                    onResult?.Invoke(result.linked);
                }, onError));
        }

        // Signs into the Sai account linked to the running Steam account.
        // Fails with STEAM_NOT_LINKED until the account is linked with SteamLink.
        public void SignIn(Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Sign In With Linked Steam");
            if (IsBusy) { Fail("STEAM_OPERATION_IN_PROGRESS", onError); return; }
            if (linkState != SteamLinkState.Linked) { Fail(linkState == SteamLinkState.NotLinked ? NotLinkedCode : "STEAM_LINK_CHECK_REQUIRED", onError); return; }
            ResetSignInState();
            StartTicketOperation(false, onError, ticket => SendSignIn(ticket, onSuccess, onError));
        }

        // Reads claim status without granting items. A failed check leaves Claim Game disabled.
        public void CheckClaim(Action<SteamClaimStatus> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Check Claim");
            if (!CanStart(true, onError)) return;
            ClearResult();
            checkedClaim = null;
            checkedClaimToken = null;
            if (linkState != SteamLinkState.Linked || !HasActiveSteamSession) { Fail("STEAM_LOGIN_REQUIRED", onError); return; }
            string sessionToken = accessToken;
            StartTicketOperation(true, onError, ticket =>
                PostTicket<SteamClaimStatus>(CheckClaimEndpoint, NewTicketRequest(ticket), result =>
                {
                    if (!HasActiveSteamSession || accessToken != sessionToken) { Fail("STEAM_LOGIN_REQUIRED", onError); return; }
                    checkedClaim = result;
                    checkedClaimToken = sessionToken;
                    Succeed("Check Claim", result.claimed ? "already claimed" : result.can_claim ? "ready to claim" : result.message_code);
                    onSuccess?.Invoke(result);
                }, onError));
        }

        // Retries Claim Game and keeps its result on this login component.
        public void ClaimGame(Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Claim Game");
            if (linkState != SteamLinkState.Linked || !HasActiveSteamSession) { Fail("STEAM_LOGIN_REQUIRED", onError); return; }
            if (!CanStart(true, onError)) return;
            if (!CanClaimGame) { Fail("STEAM_CLAIM_CHECK_REQUIRED", onError); return; }
            checkedClaim = null;
            checkedClaimToken = null;
            StartTicketOperation(true, onError, ticket =>
                PostTicket<SteamLinkResponse>(ClaimEndpoint, NewTicketRequest(ticket), result =>
                {
                    ApplyClaimGameResult(result.ownership, result.reward);
                    Succeed("Claim Game", ClaimGameStatus(result.reward));
                    onSuccess?.Invoke(result);
                }, onError));
        }

        // Games using another Steam wrapper can supply a fresh ticket directly.
        // The caller must keep its ticket handle alive until either callback.
        public void SignInWithTicket(string hexTicket, Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null)
        {
            if (!CanStart(false, onError)) return;
            if (linkState != SteamLinkState.Linked) { Fail(linkState == SteamLinkState.NotLinked ? NotLinkedCode : "STEAM_LINK_CHECK_REQUIRED", onError); return; }
            if (string.IsNullOrEmpty(hexTicket)) { Fail("STEAM_INVALID_TICKET", onError); return; }
            ResetSignInState();
            StartOperation(SendSignIn(hexTicket, onSuccess, onError));
        }

        public void VerifyTwoFactor(string code, Action<LoginResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Verify Two Factor");
            if (!CanStart(false, onError)) return;
            if (!RequiresTwoFactor) { SetTwoFactorChallenge(null); Fail("TWO_FACTOR_CHALLENGE_EXPIRED", onError); return; }
            if (string.IsNullOrWhiteSpace(code)) { Fail("INVALID_TWO_FACTOR_CODE", onError); return; }
            StartOperation(CompleteTwoFactor(code.Trim(), onSuccess, onError));
        }

        // Called by SteamLink after a successful link.
        public void MarkLinked(string linkedSteamId)
        {
            this.linkState = SteamLinkState.Linked;
            if (!string.IsNullOrEmpty(linkedSteamId)) this.steamId = linkedSteamId;
        }

        protected override void OnCancelled()
        {
            SetTwoFactorChallenge(null);
        }

        private IEnumerator SendSignIn(string ticket, Action<SteamLinkResponse> onSuccess, Action<string> onError)
        {
            return PostTicket<SteamLinkResponse>(SignInEndpoint, NewTicketRequest(ticket), result =>
            {
                this.linkState = SteamLinkState.Linked;
                if (result.message_code == TwoFactorRequiredCode)
                {
                    SetTwoFactorChallenge(result);
                    OnTwoFactorRequired?.Invoke(result);
                    Fail(TwoFactorRequiredCode, onError);
                    return;
                }
                if (string.IsNullOrEmpty(result.access_token) || result.user == null) { Fail("STEAM_INVALID_RESPONSE", onError); return; }
                ApplySession(result);
                ApplyClaimGameResult(result.ownership, result.reward);
                if (result.ownership != null) this.steamId = result.ownership.steam_id;
                OnSignedIn?.Invoke(result);
                Succeed("Sign In With Linked Steam", ClaimGameStatus(result.reward));
                onSuccess?.Invoke(result);
            }, error =>
            {
                if (error == NotLinkedCode) this.linkState = SteamLinkState.NotLinked;
                onError?.Invoke(error);
            });
        }

        private IEnumerator CompleteTwoFactor(string code, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            var request = new TwoFactorLoginRequest { user_id = challenge.user_id, challenge_token = challenge.challenge_token, code = code };
            string response = null;
            string error = null;
            yield return SteamBackendTransport.Post(TwoFactorEndpoint, JsonUtility.ToJson(request), value => response = value, value => error = value);
            LoginResponse result = null;
            if (error == null)
            {
                try { result = JsonUtility.FromJson<LoginResponse>(response); }
                catch (Exception) { error = "STEAM_INVALID_RESPONSE"; }
            }
            FinishOperation();
            if (error != null || result == null || string.IsNullOrEmpty(result.access_token))
            { Fail(error ?? "STEAM_INVALID_RESPONSE", onError); yield break; }

            ApplySession(result);
            var completed = new SteamLinkResponse
            {
                user = result.user, access_token = result.access_token,
                refresh_token = result.refresh_token, expires_in = result.expires_in
            };
            ApplyClaimGameResult(completed.ownership, completed.reward);
            OnSignedIn?.Invoke(completed);
            Succeed("Verify Two Factor", ClaimGameStatus(completed.reward));
            onSuccess?.Invoke(completed);
        }

        private void ApplySession(LoginResponse result)
        {
            SetTwoFactorChallenge(null);
            this.accessToken = result.access_token;
            this.refreshToken = result.refresh_token;
            this.expiresIn = result.expires_in;
            this.userData = result.user;
            this.signInTime = Time.time;
            this.SetTokenToSaiAuth();
        }

        private void ResetSignInState()
        {
            SetTwoFactorChallenge(null);
            ClearResult();
            checkedClaim = null;
            checkedClaimToken = null;
        }

        private void SetTwoFactorChallenge(LoginResponse value)
        {
            challenge = value;
            challengeExpiresAt = value != null ? DateTime.UtcNow.AddSeconds(Math.Max(0, value.expires_in)) : DateTime.MinValue;
            requiresTwoFactor = value != null;
        }

        public void SetLoginData(string access, string refresh, int expires, UserData user = null)
        {
            checkedClaim = null;
            checkedClaimToken = null;
            this.accessToken = access;
            this.refreshToken = refresh;
            this.expiresIn = expires;
            this.userData = user;
            this.signInTime = string.IsNullOrEmpty(access) ? 0 : Time.time;
        }

        public void SetTokenToSaiAuth()
        {
            LogButton("Set Token To SaiAuth");
            if (SaiServer.Instance == null)
            {
                Debug.LogWarning(LogTag + " SaiServer not found!");
                return;
            }

            SaiServer.Instance.SetLoginData(this.accessToken, this.refreshToken, this.expiresIn, this.userData);

            if (SaiServer.Instance.ShowDebug)
                Debug.Log($"<color={LogColor}>{LogTag} Pushed token to SaiAuth (master) | access expires in {this.expiresIn}s</color>");
        }

        public void ClearAuthData()
        {
            LogButton("Clear Auth Data");
            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.signInTime = 0;
            ClearResult();
            checkedClaim = null;
            checkedClaimToken = null;
            SetTwoFactorChallenge(null);
        }
    }
}
