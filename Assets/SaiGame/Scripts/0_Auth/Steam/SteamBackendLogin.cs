using System;
using System.Collections;
using SaiGame.Services;
using UnityEngine;

namespace SaiGame.Auth.Steam
{
    public class SteamBackendLogin : SaiBehaviour
    {
        public const string TicketIdentity = "ss-go";
        public const string LoginEndpoint = "/api/v1/player/auth/steam";
        public const string LinkEndpoint = "/api/v1/player/auth/steam/link";
        public const string ClaimEndpoint = "/api/v1/player/auth/steam/rewards/claim";
        private const string TwoFactorEndpoint = "/api/v1/auth/2fa/verify";

        [Tooltip("A component implementing ISteamTicketProvider, such as SteamworksNetTicketProvider.")]
        [SerializeField] private MonoBehaviour ticketProvider;
        [Tooltip("Maximum time to wait for Steam to return a Web API ticket.")]
        [SerializeField] private float ticketTimeoutSeconds = 30f;

        public event Action<SteamLoginResponse> OnLoginSuccess;
        public event Action<LoginResponse> OnTwoFactorRequired;
        public event Action<SteamRewardResult> OnRewardResult;
        public event Action<string> OnLoginFailure;

        public bool IsLoggingIn => busy;
        public bool RequiresTwoFactor => challenge != null && DateTime.UtcNow < challengeExpiresAt;
        public SteamOwnership Ownership { get; private set; }
        public SteamRewardResult Reward { get; private set; }

        private bool busy;
        private Coroutine operation;
        private ISteamTicketProvider activeProvider;
        private LoginResponse challenge;
        private DateTime challengeExpiresAt;

        public void StartLogin(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            StartTicketOperation(LoginEndpoint, true, onSuccess, onError);
        }

        public void LinkAccount(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            StartTicketOperation(LinkEndpoint, false, onSuccess, onError);
        }

        public void ClaimRewards(Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            StartTicketOperation(ClaimEndpoint, false, onSuccess, onError);
        }

        // Games using another Steam wrapper can supply a fresh ticket directly.
        // The caller must keep its ticket handle alive until either callback.
        public void LoginWithTicket(string hexTicket, Action<SteamLoginResponse> onSuccess = null, Action<string> onError = null)
        {
            if (!CanStart(true, onError)) return;
            if (string.IsNullOrEmpty(hexTicket)) { Fail("STEAM_INVALID_TICKET", onError); return; }
            challenge = null;
            Ownership = null;
            Reward = null;
            busy = true;
            operation = StartCoroutine(SendTicket(LoginEndpoint, hexTicket, true, onSuccess, onError));
        }

        public void VerifyTwoFactor(string code, Action<LoginResponse> onSuccess = null, Action<string> onError = null)
        {
            if (!CanStart(true, onError)) return;
            if (!RequiresTwoFactor) { Fail("TWO_FACTOR_CHALLENGE_EXPIRED", onError); return; }
            if (string.IsNullOrWhiteSpace(code)) { Fail("INVALID_TWO_FACTOR_CODE", onError); return; }
            busy = true;
            operation = StartCoroutine(CompleteTwoFactor(code.Trim(), onSuccess, onError));
        }

        private void StartTicketOperation(string endpoint, bool login, Action<SteamLoginResponse> onSuccess, Action<string> onError)
        {
            if (!CanStart(login, onError)) return;
            var provider = ticketProvider as ISteamTicketProvider;
            if (provider == null) { Fail("STEAM_TICKET_PROVIDER_REQUIRED", onError); return; }
            if (login) { challenge = null; Ownership = null; Reward = null; }
            busy = true;
            activeProvider = provider;
            operation = StartCoroutine(RequestAndSend(provider, endpoint, login, onSuccess, onError));
        }

        private bool CanStart(bool login, Action<string> onError)
        {
            string error = null;
            if (busy) error = "STEAM_OPERATION_IN_PROGRESS";
            else if (SaiServer.Instance == null || string.IsNullOrEmpty(SaiServer.Instance.GameId)) error = "SAI_SERVER_GAME_REQUIRED";
            else if (!login && (SaiServer.Instance.SaiAuth == null || !SaiServer.Instance.SaiAuth.IsAuthenticated)) error = "UNAUTHORIZED";
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
            yield return SteamBackendTransport.Post(endpoint, JsonUtility.ToJson(request), value => response = value, value => error = value);
            SteamLoginResponse result = null;
            if (error == null)
            {
                try { result = JsonUtility.FromJson<SteamLoginResponse>(response); }
                catch (Exception) { error = "STEAM_INVALID_RESPONSE"; }
            }
            FinishOperation();
            if (error != null || result == null) { ReportError(error ?? "STEAM_INVALID_RESPONSE", onError, reportFailure); yield break; }
            if (login && result.message_code == "TWO_FACTOR_REQUIRED")
            {
                challenge = result;
                challengeExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, result.expires_in));
                OnTwoFactorRequired?.Invoke(result);
                Fail("TWO_FACTOR_REQUIRED", onError);
                yield break;
            }
            if (login)
            {
                if (string.IsNullOrEmpty(result.access_token) || result.user == null) { Fail("STEAM_INVALID_RESPONSE", onError); yield break; }
                SaiServer.Instance.SetLoginData(result.access_token, result.refresh_token, result.expires_in, result.user);
                challenge = null;
            }
            Ownership = result.ownership;
            Reward = result.reward;
            if (result.reward != null) OnRewardResult?.Invoke(result.reward);
            if (login) OnLoginSuccess?.Invoke(result);
            onSuccess?.Invoke(result);
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
            challenge = null;
            SaiServer.Instance.SetLoginData(result.access_token, result.refresh_token, result.expires_in, result.user);
            var completed = new SteamLoginResponse
            {
                user = result.user, access_token = result.access_token,
                refresh_token = result.refresh_token, expires_in = result.expires_in,
                reward = new SteamRewardResult { status = "pending", message_code = "STEAM_REWARD_PENDING" }
            };
            // Obtain a fresh ticket after 2FA. Optional reward failure must not
            // turn a completed login into failure.
            var provider = ticketProvider as ISteamTicketProvider;
            if (provider != null)
            {
                busy = true;
                activeProvider = provider;
                yield return RequestAndSend(provider, ClaimEndpoint, false,
                    claimed => { completed.ownership = claimed.ownership; completed.reward = claimed.reward; },
                    errorCode => { }, false);
            }
            Ownership = completed.ownership;
            Reward = completed.reward;
            OnLoginSuccess?.Invoke(completed);
            onSuccess?.Invoke(completed);
        }

        public void CancelLogin()
        {
            if (operation != null) StopCoroutine(operation);
            bool wasBusy = busy;
            FinishOperation();
            challenge = null;
            if (wasBusy) OnLoginFailure?.Invoke("STEAM_CANCELLED");
        }

        private void OnDisable() { CancelLogin(); }

        private void FinishOperation()
        {
            try { activeProvider?.CancelTicket(); }
            catch (Exception) { Debug.LogWarning("[SteamBackendLogin] Steam ticket cleanup failed."); }
            activeProvider = null;
            operation = null;
            busy = false;
        }

        private void Fail(string error, Action<string> onError)
        {
            OnLoginFailure?.Invoke(error);
            onError?.Invoke(error);
        }

        private void ReportError(string error, Action<string> onError, bool reportFailure)
        {
            if (reportFailure) Fail(error, onError);
            else onError?.Invoke(error);
        }
    }
}
