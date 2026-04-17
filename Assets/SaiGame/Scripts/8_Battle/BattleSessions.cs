using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class BattleSessions : SaiBehaviour
    {
        public event Action<BattleSessionsResponse> OnGetSessionsSuccess;
        public event Action<string> OnGetSessionsFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Sessions Data")]
        [SerializeField] protected BattleSessionsResponse currentSessions;

        [Header("Query Parameters")]
        [SerializeField] protected int sessionLimit = 50;
        [SerializeField] protected int sessionOffset = 0;

        public BattleSessionsResponse CurrentSessions => this.currentSessions;
        public bool HasSessions => this.currentSessions != null
                                   && this.currentSessions.sessions != null
                                   && this.currentSessions.sessions.Length > 0;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
        }

        protected virtual void RegisterLoginListener()
        {
            if (SaiServer.Instance?.SaiAuth == null) return;
            SaiServer.Instance.SaiAuth.OnLoginSuccess += this.HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiServer.Instance?.SaiAuth == null) return;
            SaiServer.Instance.SaiAuth.OnLogoutSuccess += this.HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiServer.Instance?.SaiAuth != null)
            {
                SaiServer.Instance.SaiAuth.OnLoginSuccess -= this.HandleLoginSuccess;
                SaiServer.Instance.SaiAuth.OnLogoutSuccess -= this.HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!this.autoLoadOnLogin) return;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[BattleSessions] Auto-loading battle sessions after successful login...");

            this.GetSessions(
                onSuccess: sessions =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[BattleSessions] Sessions auto-loaded: {sessions.sessions.Length} sessions, total: {sessions.total}");
                },
                onError: error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[BattleSessions] Auto-load sessions failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[BattleSessions] Logout successful, clearing sessions data...");

            this.ClearLocalSessions();
        }

        /// <summary>
        /// Fetches the player's battle sessions from the server.
        /// Endpoint: GET /api/v1/games/{game_id}/me/battle-sessions
        /// </summary>
        public void GetSessions(
            int? limit = null,
            int? offset = null,
            Action<BattleSessionsResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[BattleSessions] ► Get Sessions</b></color>", gameObject);

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            int actualLimit = limit ?? this.sessionLimit;
            int actualOffset = offset ?? this.sessionOffset;

            StartCoroutine(this.GetSessionsCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetSessionsCoroutine(
            int limit,
            int offset,
            Action<BattleSessionsResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/me/battle-sessions?limit={limit}&offset={offset}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        BattleSessionsResponse sessionsResponse = JsonUtility.FromJson<BattleSessionsResponse>(response);
                        this.currentSessions = sessionsResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[BattleSessions] Sessions loaded: {sessionsResponse.sessions.Length} sessions, total: {sessionsResponse.total}");

                        this.OnGetSessionsSuccess?.Invoke(sessionsResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[BattleSessions] GetSessions</color> → <b><color=#00FF88>onSuccess</color></b> callback | BattleSessions.cs › GetSessionsCoroutine");
                        onSuccess?.Invoke(sessionsResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse get sessions response error: {e.Message}";
                        this.OnGetSessionsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[BattleSessions] GetSessions</color> → <b><color=#FF4444>onError</color></b> callback (parse) | BattleSessions.cs › GetSessionsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetSessionsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[BattleSessions] GetSessions</color> → <b><color=#FF4444>onError</color></b> callback (network) | BattleSessions.cs › GetSessionsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>Clears local session data.</summary>
        public void ClearSessions()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[BattleSessions] ► Clear Sessions</b></color>", gameObject);
            this.ClearLocalSessions();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[BattleSessions] Session data cleared locally");
        }

        private void ClearLocalSessions()
        {
            this.currentSessions = new BattleSessionsResponse
            {
                sessions = new BattleSessionData[0],
                limit = this.sessionLimit,
                offset = 0,
                total = 0
            };
        }

        /// <summary>Returns the locally cached session with the given id, or null.</summary>
        public BattleSessionData GetSessionById(string sessionId)
        {
            if (this.currentSessions == null || this.currentSessions.sessions == null)
                return null;

            foreach (BattleSessionData session in this.currentSessions.sessions)
            {
                if (session.id == sessionId)
                    return session;
            }

            return null;
        }

        public void SetSessionLimit(int limit) => this.sessionLimit = limit;
        public void SetSessionOffset(int offset) => this.sessionOffset = offset;

        public int GetSessionLimit() => this.sessionLimit;
        public int GetSessionOffset() => this.sessionOffset;
    }
}
