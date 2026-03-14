using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class PlayerEvent : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<TrackEventResponse> OnTrackEventSuccess;
        public event Action<string> OnTrackEventFailure;

        [Header("Session Settings")]
        [SerializeField] protected string sessionId = "";

        [Header("Event Settings")]
        [SerializeField] protected string eventType = "join_game";
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
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLoginSuccess += this.HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLogoutSuccess += this.HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiService.Instance?.SaiAuth != null)
            {
                SaiService.Instance.SaiAuth.OnLoginSuccess -= this.HandleLoginSuccess;
                SaiService.Instance.SaiAuth.OnLogoutSuccess -= this.HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            // Generate a fresh session ID for each login
            this.sessionId = Guid.NewGuid().ToString();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log($"[PlayerEvent] New session ID generated on login: {this.sessionId}");
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF88FF><b>[PlayerEvent] ► Track Event</b></color>", gameObject);

            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/events";

            // Build JSON manually so raw event_data is embedded without re-encoding
            string dataJson = string.IsNullOrEmpty(eventDataJson) ? "{}" : eventDataJson;
            string escapedType = eventType.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string escapedSession = sessionId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string jsonData = $"{{\"event_type\":\"{escapedType}\",\"session_id\":\"{escapedSession}\",\"event_data\":{dataJson}}}";

            yield return SaiService.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        TrackEventResponse trackResponse = JsonUtility.FromJson<TrackEventResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[PlayerEvent] Event tracked: {eventType} (session: {sessionId})");

                        this.OnTrackEventSuccess?.Invoke(trackResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerEvent.cs › TrackEventCoroutine");
                        onSuccess?.Invoke(trackResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse track event response error: {e.Message}";
                        this.OnTrackEventFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#FF4444>onError</color></b> callback (parse) | PlayerEvent.cs › TrackEventCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnTrackEventFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[PlayerEvent] TrackEvent</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerEvent.cs › TrackEventCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>Generates a new random session ID, replacing the current one.</summary>
        public void RegenerateSessionId()
        {
            this.sessionId = Guid.NewGuid().ToString();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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
