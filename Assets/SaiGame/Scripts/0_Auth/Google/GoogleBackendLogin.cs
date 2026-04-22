using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class GoogleBackendLogin : SaiBehaviour
    {
        public event Action<GoogleSessionResponse> OnSessionCreated;
        public event Action<LoginResponse> OnLoginSuccess;
        public event Action<string> OnLoginFailure;

        [Header("Authentication Data")]
        [SerializeField] protected string accessToken;
        [SerializeField] protected string refreshToken;
        [SerializeField] protected int expiresIn;
        [SerializeField] protected UserData userData;
        [SerializeField] protected float loginTime;

        [Header("Session State")]
        [SerializeField] protected string sessionId;
        [SerializeField] protected string authUrl;
        [SerializeField] protected bool isLoggingIn;

        [Header("Session Settings")]
        [Tooltip("Hạn chót tổng cho toàn bộ flow login.")]
        [SerializeField] protected float maxLoginDurationSeconds = 300f;

        [Tooltip("Khoảng cách poll khi server không trả poll_interval_seconds.")]
        [SerializeField] protected int defaultPollIntervalSeconds = 2;

        private const string ENDPOINT_SESSION = "/api/v1/client/auth/google/session";

        private Coroutine flowCoroutine;

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public bool IsLoggingIn => flowCoroutine != null;
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;
        public string SessionId => sessionId;
        public string AuthUrl => authUrl;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        protected override void ResetValue()
        {
            base.ResetValue();
        }

        public void StartLogin(Action<LoginResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#DB4437><b>[GoogleBackendLogin] ► Start Login</b></color>", gameObject);

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (this.IsLoggingIn)
            {
                string msg = "Login already in progress!";
                OnLoginFailure?.Invoke(msg);
                onError?.Invoke(msg);
                return;
            }

            flowCoroutine = StartCoroutine(LoginCoroutine(onSuccess, onError));
        }

        public void CancelLogin()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#DB4437><b>[GoogleBackendLogin] ► Cancel Login</b></color>", gameObject);

            if (flowCoroutine == null) return;

            StopCoroutine(flowCoroutine);
            ResetSessionState();

            OnLoginFailure?.Invoke("cancelled");
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.LogWarning("<color=#66CCFF>[GoogleBackendLogin] Login</color> → <b><color=#FF4444>onError</color></b> callback (cancelled) | GoogleBackendLogin.cs › CancelLogin");
        }

        private IEnumerator LoginCoroutine(Action<LoginResponse> onSuccess, Action<string> onError)
        {
            this.isLoggingIn = true;

            GoogleSessionResponse session = null;
            string createErr = null;
            yield return CreateSessionCoroutine((r, e) => { session = r; createErr = e; });

            if (createErr != null || session == null || string.IsNullOrEmpty(session.session_id))
            {
                FailLogin("create_session_failed: " + (createErr ?? "empty response"), onError);
                yield break;
            }

            this.sessionId = session.session_id;
            this.authUrl = session.auth_url;

            OnSessionCreated?.Invoke(session);

            if (!string.IsNullOrEmpty(session.auth_url))
            {
                Application.OpenURL(session.auth_url);
            }

            yield return PollCoroutine(session, onSuccess, onError);
        }

        private IEnumerator CreateSessionCoroutine(Action<GoogleSessionResponse, string> cb)
        {
            GoogleSessionRequest body = new GoogleSessionRequest
            {
                game_id            = SaiServer.Instance.GameId,
                platform           = Application.platform.ToString(),
                client_fingerprint = SystemInfo.deviceUniqueIdentifier,
            };

            string jsonData = JsonUtility.ToJson(body);

            yield return SaiServer.Instance.PostRequest(ENDPOINT_SESSION, jsonData,
                response =>
                {
                    try
                    {
                        GoogleSessionResponse parsed = JsonUtility.FromJson<GoogleSessionResponse>(response);
                        cb(parsed, null);
                    }
                    catch (Exception e)
                    {
                        cb(null, $"parse_error: {e.Message}");
                    }
                },
                error =>
                {
                    cb(null, error);
                }
            );
        }

        private IEnumerator PollCoroutine(GoogleSessionResponse session, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            float deadline = Time.realtimeSinceStartup + maxLoginDurationSeconds;
            int interval = session.poll_interval_seconds > 0 ? session.poll_interval_seconds : defaultPollIntervalSeconds;
            interval = Mathf.Max(1, interval);

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSeconds(interval);

                GoogleSessionPollResponse resp = null;
                string err = null;
                yield return PollOnceCoroutine(this.sessionId, (r, e) => { resp = r; err = e; });

                if (err != null || resp == null)
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"<color=#DB4437>[GoogleBackendLogin] poll transient error: {err}</color>");
                    continue;
                }

                switch (resp.status)
                {
                    case "completed":
                        SucceedLogin(resp, onSuccess);
                        yield break;

                    case "denied":
                        FailLogin("user_denied:" + (resp.error ?? ""), onError);
                        yield break;

                    case "expired":
                        FailLogin("session_expired", onError);
                        yield break;

                    case "error":
                        FailLogin("server_error:" + (resp.error ?? ""), onError);
                        yield break;

                    case "pending":
                    default:
                        break;
                }
            }

            FailLogin("timeout", onError);
        }

        private IEnumerator PollOnceCoroutine(string sessionId, Action<GoogleSessionPollResponse, string> cb)
        {
            string endpoint = ENDPOINT_SESSION + "/" + UnityWebRequest.EscapeURL(sessionId);

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        GoogleSessionPollResponse parsed = JsonUtility.FromJson<GoogleSessionPollResponse>(response);
                        cb(parsed, null);
                    }
                    catch (Exception e)
                    {
                        cb(null, $"parse_error: {e.Message}");
                    }
                },
                error =>
                {
                    cb(null, error);
                }
            );
        }

        private void SucceedLogin(GoogleSessionPollResponse resp, Action<LoginResponse> onSuccess)
        {
            LoginResponse result = new LoginResponse
            {
                user          = resp.user,
                access_token  = resp.access_token,
                refresh_token = resp.refresh_token,
                expires_in    = resp.expires_in,
            };

            SaiServer.Instance.SetLoginData(result.access_token, result.refresh_token, result.expires_in, result.user);

            this.accessToken = result.access_token;
            this.refreshToken = result.refresh_token;
            this.expiresIn = result.expires_in;
            this.userData = result.user;
            this.loginTime = Time.time;

            ResetSessionState();

            OnLoginSuccess?.Invoke(result);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.Log("<color=#66CCFF>[GoogleBackendLogin] Login</color> → <b><color=#00FF88>onSuccess</color></b> callback | GoogleBackendLogin.cs › LoginCoroutine");
            onSuccess?.Invoke(result);
        }

        private void FailLogin(string reason, Action<string> onError)
        {
            ResetSessionState();

            OnLoginFailure?.Invoke(reason);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.LogWarning($"<color=#66CCFF>[GoogleBackendLogin] Login</color> → <b><color=#FF4444>onError</color></b> callback | GoogleBackendLogin.cs › LoginCoroutine | {reason}");
            onError?.Invoke(reason);
        }

        private void ResetSessionState()
        {
            flowCoroutine = null;
            sessionId = null;
            authUrl = null;
            isLoggingIn = false;
        }

        public void SetAccessToken(string token)
        {
            this.accessToken = token;
        }

        public void SetLoginData(string access, string refresh, int expires, UserData user = null)
        {
            this.accessToken = access;
            this.refreshToken = refresh;
            this.expiresIn = expires;
            this.userData = user;
        }

        public void ClearAuthData()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[GoogleBackendLogin] ► Clear Auth Data</b></color>", gameObject);

            if (SaiServer.Instance != null)
            {
                SaiServer.Instance.SetLoginData("", "", 0, null);
            }

            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.loginTime = 0;
        }
    }
}
