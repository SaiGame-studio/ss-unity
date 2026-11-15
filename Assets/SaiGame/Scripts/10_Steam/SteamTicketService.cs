using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace SaiGame.Services
{
    // Shared base of SteamLink and SteamLogin: gets a Steam Web API ticket from
    // the running Steam client, sends it to the Sai Server, and tracks the busy
    // state, last error, ownership, and Claim Game result of the last operation.
    public abstract class SteamTicketService : SaiBehaviour
    {
        public const string TicketIdentity = "ss-go";
        private const string PublicGameInfoEndpoint = "/api/v1/public/games/{0}/info";
        // Steam light blue; readable on both the light and dark Console themes.
        // Also the Inspector button color, so buttons and their logs match.
        public const string LogColor = "#66C0F4";

        // SteamAPI runs once per process, so its AppID is resolved once for all services.
        private static bool appIdResolved;

        public event Action<SteamClaimGameResult> OnClaimGameResult;
        public event Action<string> OnFailed;

        [Header("Steam State")]
        [FormerlySerializedAs("isLoggingIn")]
        [SerializeField] protected bool isBusy;
        [SerializeField] protected SteamOwnership ownership;
        [FormerlySerializedAs("reward")]
        [SerializeField] protected SteamClaimGameResult claimGame;
        [Tooltip("message_code of the last failed operation.")]
        [SerializeField] protected string lastError;

        [Header("Steam Settings")]
        [Tooltip("A component implementing ISteamTicketProvider, such as SteamworksNetTicketProvider.")]
        [SerializeField] private MonoBehaviour ticketProvider;
        [Tooltip("Maximum time to wait for Steam to return a Web API ticket.")]
        [SerializeField] private float ticketTimeoutSeconds = 30f;

        private double retryAvailableAt;
        public bool HasRetryAfter => retryAvailableAt > 0;
        public double RetryAfterSeconds => Math.Max(0, retryAvailableAt - Time.realtimeSinceStartupAsDouble);

        private Coroutine operation;
        private ISteamTicketProvider activeProvider;

        protected abstract string LogTag { get; }

        public bool IsBusy => isBusy;
        public SteamOwnership Ownership => ownership;
        public SteamClaimGameResult ClaimGameResult => claimGame;
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

        public void Cancel()
        {
            LogButton("Cancel");
            if (operation != null) StopCoroutine(operation);
            bool wasBusy = isBusy;
            FinishOperation();
            OnCancelled();
            if (wasBusy) Fail("STEAM_CANCELLED", null);
        }

        protected virtual void OnCancelled() { }

        protected virtual void OnDisable()
        {
            if (operation != null) StopCoroutine(operation);
            FinishOperation();
        }

        protected bool CanStart(bool requiresSignIn, Action<string> onError)
        {
            string error = null;
            if (isBusy) error = "STEAM_OPERATION_IN_PROGRESS";
            else if (SaiServer.Instance == null || string.IsNullOrEmpty(SaiServer.Instance.GameId)) error = "SAI_SERVER_GAME_REQUIRED";
            else if (requiresSignIn && !SaiServer.Instance.IsAuthenticated) error = "UNAUTHORIZED";
            if (error == null) return true;
            Fail(error, onError);
            return false;
        }

        // Gets a fresh ticket, then runs send(ticket) as the operation.
        protected void StartTicketOperation(bool requiresSignIn, Action<string> onError, Func<string, IEnumerator> send)
        {
            if (!CanStart(requiresSignIn, onError)) return;
            ISteamTicketProvider provider = TicketProvider;
            if (provider == null) { Fail("STEAM_TICKET_PROVIDER_REQUIRED", onError); return; }
            BeginOperation();
            operation = StartCoroutine(RequestTicketThen(provider, send, onError));
        }

        protected void StartOperation(IEnumerator routine)
        {
            BeginOperation();
            operation = StartCoroutine(routine);
        }

        protected IEnumerator RequestTicketThen(ISteamTicketProvider provider, Func<string, IEnumerator> send,
            Action<string> onError, bool reportFailure = true)
        {
            activeProvider = provider;
            if (!appIdResolved && provider is SteamworksNetTicketProvider steamworks)
                yield return ResolveAppId(steamworks);

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
            yield return send(ticket);
        }

        // Posts a ticket request and parses the response as T. Finishes the operation.
        protected IEnumerator PostTicket<T>(string endpoint, SteamTicketRequest request, Action<T> onResult,
            Action<string> onError, bool reportFailure = true) where T : class
        {
            string response = null;
            string error = null;
            yield return SteamBackendTransport.Post(endpoint, JsonUtility.ToJson(request), value => response = value, value => error = value,
                details => retryAvailableAt = Time.realtimeSinceStartupAsDouble + Math.Max(0, details.retry_after_seconds));
            T result = null;
            if (error == null)
            {
                try { result = JsonUtility.FromJson<T>(response); }
                catch (Exception) { error = "STEAM_INVALID_RESPONSE"; }
            }
            FinishOperation();
            if (error != null || result == null) { ReportError(error ?? "STEAM_INVALID_RESPONSE", onError, reportFailure); yield break; }
            onResult(result);
        }

        protected SteamTicketRequest NewTicketRequest(string ticket)
        {
            return new SteamTicketRequest { game_id = SaiServer.Instance.GameId, ticket = ticket };
        }

        // Reads the game's Steam AppID from its public info so SteamAPI starts as
        // that AppID. On failure (for example a game that is not public yet) the
        // provider falls back to steam_appid.txt and this is retried next time.
        private IEnumerator ResolveAppId(SteamworksNetTicketProvider steamworks)
        {
            string response = null;
            string error = null;
            string endpoint = string.Format(PublicGameInfoEndpoint, Uri.EscapeDataString(SaiServer.Instance.GameId));
            yield return SteamBackendTransport.Get(endpoint, value => response = value, value => error = value);
            SteamPublicGameInfo info = null;
            if (error == null)
            {
                try { info = JsonUtility.FromJson<SteamPublicGameInfo>(response); }
                catch (Exception) { error = "STEAM_INVALID_RESPONSE"; }
            }
            if (error != null || info == null || info.steam_app_id <= 0 || info.steam_app_id > uint.MaxValue)
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.LogWarning($"{LogTag} No Steam AppID from public game info ({error ?? "Steam not enabled"}); using steam_appid.txt.");
                yield break;
            }
            appIdResolved = true;
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"<color={LogColor}>{LogTag} Steam AppID {info.steam_app_id} from public game info</color>");
            steamworks.UseAppId((uint)info.steam_app_id);
        }

        protected void ApplyClaimGameResult(SteamOwnership newOwnership, SteamClaimGameResult result)
        {
            if (newOwnership != null) this.ownership = newOwnership;
            this.claimGame = result;
            if (result != null) OnClaimGameResult?.Invoke(result);
        }

        protected void ClearResult()
        {
            this.ownership = null;
            this.claimGame = null;
            this.lastError = "";
        }

        protected void BeginOperation()
        {
            isBusy = true;
            lastError = "";
            retryAvailableAt = 0;
        }

        protected virtual void FinishOperation()
        {
            try { activeProvider?.CancelTicket(); }
            catch (Exception) { Debug.LogWarning(LogTag + " Steam ticket cleanup failed."); }
            activeProvider = null;
            operation = null;
            isBusy = false;
        }

        protected void Succeed(string action, string detail)
        {
            lastError = "";
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.Log($"<color=#66CCFF>{LogTag} {action}</color> → <b><color=#00FF88>onSuccess</color></b> callback | {detail}");
        }

        protected void Fail(string error, Action<string> onError)
        {
            lastError = error;
            OnFailed?.Invoke(error);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.LogWarning($"<color=#66CCFF>{LogTag}</color> → <b><color=#FF4444>onError</color></b> callback | {error}");
            onError?.Invoke(error);
        }

        protected void ReportError(string error, Action<string> onError, bool reportFailure)
        {
            if (reportFailure) Fail(error, onError);
            else onError?.Invoke(error);
        }

        protected void LogButton(string action)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color={LogColor}><b>{LogTag} ► {action}</b></color>", gameObject);
        }

        protected static string ClaimGameStatus(SteamClaimGameResult value) => value == null ? "claim game: none"
            : "claim game: " + value.status + (string.IsNullOrEmpty(value.message_code) ? "" : " | message_code=" + value.message_code);
    }
}
