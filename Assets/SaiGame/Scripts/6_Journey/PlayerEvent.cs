using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Sends player behavior events to the server for analytics.
    ///
    /// Why Session ID:
    /// - Groups events belonging to the same play session, so they can be distinguished
    ///   from other sessions of the same user.
    /// - Enables per-session analytics (duration, action sequence, drop-off) that user_id alone cannot.
    /// - Lets us replay the exact flow of one play session when debugging.
    /// - Generated fresh on each login, cleared on logout — avoids mixing sessions across
    ///   multiple logins or devices.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class PlayerEvent : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<TrackEventResponse> OnTrackEventSuccess;
        public event Action<string> OnTrackEventFailure;

        [SerializeField] protected string sessionId = "";
        [SerializeField] protected string eventType = "";
        [SerializeField, TextArea(4, 12)] protected string eventDataJson = "{\n  \"source\": \"game\"\n}";

        public string SessionId => this.sessionId;
        public string EventType => this.eventType;
        public string EventDataJson => this.eventDataJson;

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
            // Generate a fresh session ID for each login
            this.sessionId = Guid.NewGuid().ToString();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[PlayerEvent] New session ID generated on login: {this.sessionId}");
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[PlayerEvent] Logout - clearing session ID");

            this.sessionId = "";
        }

        /// <summary>
        /// Sends a game event to the server.
        /// Session ID is automatically generated on login; override via parameter if needed.
        /// Endpoint: POST /api/v1/games/{game_id}/events
        /// </summary>
        public void TrackEvent(
            string eventType = null,
            string eventDataJson = null,
            string sessionId = null,
            System.Action<TrackEventResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF88FF><b>[PlayerEvent] ► Track Event</b></color>", gameObject);

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

            string actualEventType = eventType ?? this.eventType;
            string actualDataJson = eventDataJson ?? this.eventDataJson;
            string actualSessionId = sessionId ?? this.sessionId;

            // Fallback: generate a session ID if still empty
            if (string.IsNullOrEmpty(actualSessionId))
                actualSessionId = Guid.NewGuid().ToString();

            StartCoroutine(this.TrackEventCoroutine(actualEventType, actualDataJson, actualSessionId, onSuccess, onError));
        }

        private IEnumerator TrackEventCoroutine(
            string eventType,
            string eventDataJson,
            string sessionId,
            System.Action<TrackEventResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/events";

            // Build JSON manually so raw event_data is embedded without re-encoding
            string dataJson = string.IsNullOrEmpty(eventDataJson) ? "{}" : eventDataJson;
            string escapedType = eventType.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string escapedSession = sessionId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string jsonData = $"{{\"event_type\":\"{escapedType}\",\"session_id\":\"{escapedSession}\",\"event_data\":{dataJson}}}";

            yield return SaiServer.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        TrackEventResponse trackResponse = JsonUtility.FromJson<TrackEventResponse>(response);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[PlayerEvent] Event tracked: {eventType} (session: {sessionId})");

                        this.OnTrackEventSuccess?.Invoke(trackResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerEvent.cs › TrackEventCoroutine");
                        onSuccess?.Invoke(trackResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse track event response error: {e.Message}";
                        this.OnTrackEventFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#FF4444>onError</color></b> callback (parse) | PlayerEvent.cs › TrackEventCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnTrackEventFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerEvent.cs › TrackEventCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>Generates a new random session ID, replacing the current one.</summary>
        public void RegenerateSessionId()
        {
            this.sessionId = Guid.NewGuid().ToString();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[PlayerEvent] Session ID regenerated: {this.sessionId}");
        }

        public void SetEventType(string type) => this.eventType = type;
        public void SetEventDataJson(string json) => this.eventDataJson = json;
        public void SetSessionId(string id) => this.sessionId = id;

        public string GetEventType() => this.eventType;
        public string GetEventDataJson() => this.eventDataJson;
        public string GetSessionId() => this.sessionId;
    }
}
