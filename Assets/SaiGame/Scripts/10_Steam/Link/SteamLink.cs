using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class SteamBackendLogin : SaiBehaviour
    {
        public const string TicketIdentity = "ss-go";
        public const string LoginEndpoint = "/api/v1/player/auth/steam";
        public const string LinkEndpoint = "/api/v1/player/auth/steam/link";
        public const string ClaimEndpoint = "/api/v1/player/auth/steam/rewards/claim";
        private const string TwoFactorEndpoint = "/api/v1/auth/2fa/verify";
        private const string TwoFactorRequiredCode = "TWO_FACTOR_REQUIRED";
        private const string LogTag = "[SteamBackendLogin]";

        public event Action<SteamLoginResponse> OnLoginSuccess;
        public event Action<LoginResponse> OnTwoFactorRequired;
        public event Action<SteamRewardResult> OnRewardResult;
        public event Action<string> OnLoginFailure;

        [Header("Authentication Data")]
        [SerializeField] protected string accessToken;
        [SerializeField] protected string refreshToken;
        [SerializeField] protected int expiresIn;
        [SerializeField] protected UserData userData;
        [SerializeField] protected float loginTime;

        [Header("Steam State")]
        [SerializeField] protected bool isLoggingIn;
        [SerializeField] protected bool requiresTwoFactor;
        [SerializeField] protected SteamOwnership ownership;
        [SerializeField] protected SteamRewardResult reward;
        [Tooltip("message_code of the last failed operation.")]
        [SerializeField] protected string lastError;

        [Header("Steam Settings")]
        [Tooltip("A component implementing ISteamTicketProvider, such as SteamworksNetTicketProvider.")]
        [SerializeField] private MonoBehaviour ticketProvider;
        [Tooltip("Maximum time to wait for Steam to return a Web API ticket.")]
        [SerializeField] private float ticketTimeoutSeconds = 30f;

        private Coroutine operation;
        private ISteamTicketProvider activeProvider;
        private LoginResponse challenge;
        private DateTime challengeExpiresAt;
        private string linkCode;
        private string linkRecoveryCode;

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public bool IsLoggingIn => isLoggingIn;
        public bool RequiresTwoFactor => challenge != null && DateTime.UtcNow < challengeExpiresAt;
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;
        public SteamOwnership Ownership => ownership;
        public SteamRewardResult Reward => reward;
        public string LastError => lastError;
        public ISteamTicketProvider TicketProvider => ticketProvider as ISteamTicketProvider;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadTicketProvider();
        }

        protected virtual void LoadTicketProvider()
        {
            if (this.ticketProvider != null) return;
            this.ticketProvider = GetComponentInChildren<ISteamTicketProvider>() as MonoBehaviour;
        }

        public void StartLogin(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Start Login");
            StartTicketOperation(LoginEndpoint, true, onSuccess, onError);
        }

        // Accounts with 2FA pass an Authenticator code or a recovery code.
        // Accounts without 2FA must have signed in within the backend's
        // re-authentication window, otherwise STEAM_LINK_REAUTH_REQUIRED.
        public void LinkAccount(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null,
            string twoFactorCode = null, string recoveryCode = null)
        {
            LogButton("Link Account");
            if (isLoggingIn) { Fail("STEAM_OPERATION_IN_PROGRESS", onError); return; }
            linkCode = twoFactorCode?.Trim();
            linkRecoveryCode = recoveryCode?.Trim();
            StartTicketOperation(LinkEndpoint, false, onSuccess, onError);
        }

        public void ClaimRewards(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Claim Rewards");
            StartTicketOperation(ClaimEndpoint, false, onSuccess, onError);
        }

        // Games using another Steam wrapper can supply a fresh ticket directly.
        // The caller must keep its ticket handle alive until either callback.
        public void LoginWithTicket(string hexTicket, Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            if (!CanStart(true, onError)) return;
            if (string.IsNullOrEmpty(hexTicket)) { Fail("STEAM_INVALID_TICKET", onError); return; }
            ResetLoginState();
            BeginOperation();
            operation = StartCoroutine(SendTicket(LoginEndpoint, hexTicket, true, onSuccess, onError));
        }

        public void VerifyTwoFactor(string code, Action<LoginResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Verify Two Factor");
            if (!CanStart(true, onError)) return;
            if (!RequiresTwoFactor) { SetTwoFactorChallenge(null); Fail("TWO_FACTOR_CHALLENGE_EXPIRED", onError); return; }
            if (string.IsNullOrWhiteSpace(code)) { Fail("INVALID_TWO_FACTOR_CODE", onError); return; }
            BeginOperation();
            operation = StartCoroutine(CompleteTwoFactor(code.Trim(), onSuccess, onError));
        }

        public void CancelLogin()
        {
            LogButton("Cancel Login");
            if (operation != null) StopCoroutine(operation);
            bool wasBusy = isLoggingIn;
            FinishOperation();
            SetTwoFactorChallenge(null);
            if (wasBusy) Fail("STEAM_CANCELLED", null);
        }

        private void OnDisable()
        {
            if (operation != null) StopCoroutine(operation);
            FinishOperation();
        }

        private void StartTicketOperation(string endpoint, bool login, Action<SteamLoginResponse> onSuccess, Action<string> onError)
        {
            if (!CanStart(login, onError)) return;
            ISteamTicketProvider provider = TicketProvider;
            if (provider == null) { Fail("STEAM_TICKET_PROVIDER_REQUIRED", onError); return; }
            if (login) ResetLoginState();
            BeginOperation();
            activeProvider = provider;
            operation = StartCoroutine(RequestAndSend(provider, endpoint, login, onSuccess, onError));
        }

        private bool CanStart(bool login, Action<string> onError)
        {
            string error = null;
            if (isLoggingIn) error = "STEAM_OPERATION_IN_PROGRESS";
            else if (SaiServer.Instance == null || string.IsNullOrEmpty(SaiServer.Instance.GameId)) error = "SAI_SERVER_GAME_REQUIRED";
            else if (!login && !SaiServer.Instance.IsAuthenticated) error = "UNAUTHORIZED";
            if (error == null) return true;
            Fail(error, onError);
            return false;
        }

        private IEnumerator RequestAndSend(ISteamTicketProvider provider, string endpoint, bool login,
            Action<SteamLoginResponse> onSuccess, Action<string> onError, bool reportFailure = true)
        {
            string ticket = null;
            string error = null;
            bool finished = false;
            try
            {
                provider.RequestTicket(TicketIdentity,
                    value => { if (!finished) { ticket = value; finished = true; } },
                    value => { if (!finished) { error = value; finished = true; } });
            }
            catch (Exception) { error = "STEAM_TICKET_FAILED"; finished = true; }

            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, ticketTimeoutSeconds);
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            finished = true;
            if (error != null || string.IsNullOrEmpty(ticket))
            {
                FinishOperation();
                ReportError(error ?? "STEAM_TICKET_TIMEOUT", onError, reportFailure);
                yield break;
            }
            yield return SendTicket(endpoint, ticket, login, onSuccess, onError, reportFailure);
        }

        private IEnumerator SendTicket(string endpoint, string ticket, bool login,
            Action<SteamLoginResponse> onSuccess, Action<string> onError, bool reportFailure = true)
        {
            string response = null;
            string error = null;
            var request = new SteamTicketRequest { game_id = SaiServer.Instance.GameId, ticket = ticket };
            if (endpoint == LinkEndpoint) { request.code = linkCode; request.recovery_code = linkRecoveryCode; }
            yield return SteamBackendTransport.Post(endpoint, JsonUtility.ToJson(request), value => response = value, value => error = value);
            SteamLoginResponse result = null;
            if (error == null)
            {
                try { result = JsonUtility.FromJson<SteamLoginResponse>(response); }
                catch (Exception) { error = "STEAM_INVALID_RESPONSE"; }
            }
            FinishOperation();
            if (error != null || result == null) { ReportError(error ?? "STEAM_INVALID_RESPONSE", onError, reportFailure); yield break; }
            if (login && result.message_code == TwoFactorRequiredCode)
            {
                SetTwoFactorChallenge(result);
                OnTwoFactorRequired?.Invoke(result);
                Fail(TwoFactorRequiredCode, onError);
                yield break;
            }
            if (login)
            {
                if (string.IsNullOrEmpty(result.access_token) || result.user == null) { Fail("STEAM_INVALID_RESPONSE", onError); yield break; }
                ApplyLogin(result);
            }
            ApplyRewardResult(result.ownership, result.reward);
            Succeed(login ? "Login" : endpoint == LinkEndpoint ? "Link" : "Claim", result, login, onSuccess);
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

            ApplyLogin(result);
            var completed = new SteamLoginResponse
            {
                user = result.user, access_token = result.access_token,
                refresh_token = result.refresh_token, expires_in = result.expires_in,
                reward = new SteamRewardResult { status = "pending", message_code = "STEAM_REWARD_PENDING" }
            };
            // Obtain a fresh ticket after 2FA. Optional reward failure must not
            // turn a completed login into failure.
            ISteamTicketProvider provider = TicketProvider;
            if (provider != null)
            {
                BeginOperation();
                activeProvider = provider;
                yield return RequestAndSend(provider, ClaimEndpoint, false,
                    claimed => { completed.ownership = claimed.ownership; completed.reward = claimed.reward; },
                    errorCode => { }, false);
            }
            ApplyRewardResult(completed.ownership, completed.reward);
            Succeed("Verify Two Factor", completed, true, null);
            onSuccess?.Invoke(completed);
        }

        private void ApplyLogin(LoginResponse result)
        {
            SetTwoFactorChallenge(null);
            this.accessToken = result.access_token;
            this.refreshToken = result.refresh_token;
            this.expiresIn = result.expires_in;
            this.userData = result.user;
            this.loginTime = Time.time;
            this.SetTokenToSaiAuth();
        }

        private void ApplyRewardResult(SteamOwnership newOwnership, SteamRewardResult newReward)
        {
            if (newOwnership != null) this.ownership = newOwnership;
            this.reward = newReward;
            if (newReward != null) OnRewardResult?.Invoke(newReward);
        }

        private void ResetLoginState()
        {
            SetTwoFactorChallenge(null);
            this.ownership = null;
            this.reward = null;
        }

        private void SetTwoFactorChallenge(LoginResponse value)
        {
            challenge = value;
            challengeExpiresAt = value != null ? DateTime.UtcNow.AddSeconds(Math.Max(0, value.expires_in)) : DateTime.MinValue;
            requiresTwoFactor = value != null;
        }

        private void BeginOperation()
        {
            isLoggingIn = true;
            lastError = "";
        }

        private void FinishOperation()
        {
            try { activeProvider?.CancelTicket(); }
            catch (Exception) { Debug.LogWarning(LogTag + " Steam ticket cleanup failed."); }
            activeProvider = null;
            operation = null;
            isLoggingIn = false;
            linkCode = null;
            linkRecoveryCode = null;
        }

        private void Succeed(string action, SteamLoginResponse result, bool login, Action<SteamLoginResponse> onSuccess)
        {
            lastError = "";
            if (login) OnLoginSuccess?.Invoke(result);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.Log($"<color=#66CCFF>{LogTag} {action}</color> → <b><color=#00FF88>onSuccess</color></b> callback | reward: {(result.reward != null ? result.reward.status : "none")}");
            onSuccess?.Invoke(result);
        }

        private void Fail(string error, Action<string> onError)
        {
            lastError = error;
            OnLoginFailure?.Invoke(error);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.LogWarning($"<color=#66CCFF>{LogTag}</color> → <b><color=#FF4444>onError</color></b> callback | {error}");
            onError?.Invoke(error);
        }

        private void ReportError(string error, Action<string> onError, bool reportFailure)
        {
            if (reportFailure) Fail(error, onError);
            else onError?.Invoke(error);
        }

        private void LogButton(string action)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#1B2838><b>{LogTag} ► {action}</b></color>", gameObject);
        }

        public void SetLoginData(string access, string refresh, int expires, UserData user = null)
        {
            this.accessToken = access;
            this.refreshToken = refresh;
            this.expiresIn = expires;
            this.userData = user;
            this.loginTime = string.IsNullOrEmpty(access) ? 0 : Time.time;
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
                Debug.Log($"{LogTag} Pushed token to SaiAuth (master) | access expires in {this.expiresIn}s");
        }

        public void ClearAuthData()
        {
            LogButton("Clear Auth Data");
            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.loginTime = 0;
            this.ownership = null;
            this.reward = null;
            this.lastError = "";
            SetTwoFactorChallenge(null);
        }
    }
}
