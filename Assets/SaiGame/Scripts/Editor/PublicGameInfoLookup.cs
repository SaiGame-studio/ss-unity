using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    // Inspector-only lookup of SaiServer's Game Id in
    // GET /api/v1/public/games/{game_id}/info, so a wrong Game Id or Steam AppID
    // shows at a glance. State is shared by every Inspector, so the SaiServer and
    // SteamLink Inspectors, and Inspectors rebuilt on entering Play Mode,
    // send one request per server URL and Game Id.
    public static class PublicGameInfoLookup
    {
        public const float RefreshButtonWidth = 60f;

        private const string PublicInfoEndpoint = "/api/v1/public/games/{0}/info";
        private const string GameNotPublic = "GAME_NOT_PUBLIC";
        private const string PublicInfoNotFound = "GAME_PUBLIC_INFO_NOT_FOUND";
        private const int TimeoutSeconds = 10;

        [Serializable]
        private class PublicInfoResponse
        {
            public string game_name = "";
            // 0 when Steam is not enabled for the game (the server sends null).
            public long steam_app_id = 0;
        }

        [Serializable]
        private class ErrorResponse
        {
            public string message_code = "";
        }

        private static string requestedKey;
        private static string baseUrl = "";
        private static UnityWebRequest request;
        private static SaiServer server;

        public static string GameName { get; private set; } = "";
        public static long SteamAppId { get; private set; }
        public static bool Loaded { get; private set; }
        public static bool IsLoading => request != null;
        public static string Status { get; private set; } = "";
        public static MessageType StatusType { get; private set; } = MessageType.None;

        // Fetches only when the Game Id or the server URL changed since the last request.
        public static void Update(SaiServer target)
        {
            if (Key(target) != requestedKey) Send(target);
        }

        public static void Refresh(SaiServer target)
        {
            Send(target);
        }

        public static void DrawStatus()
        {
            if (!string.IsNullOrEmpty(Status))
                EditorGUILayout.HelpBox(Status, StatusType);
        }

        private static string Key(SaiServer target) => target.BaseUrl + "|" + target.GameId;

        private static void Cancel()
        {
            EditorApplication.update -= Poll;
            if (request == null) return;
            request.Abort();
            request.Dispose();
            request = null;
        }

        private static void Send(SaiServer target)
        {
            Cancel();
            server = target;
            requestedKey = Key(target);
            baseUrl = target.BaseUrl;
            GameName = "";
            SteamAppId = 0;
            Loaded = false;
            string gameId = target.GameId;
            if (string.IsNullOrEmpty(gameId))
            {
                SetStatus("", MessageType.None);
                return;
            }
            if (!Guid.TryParse(gameId, out _))
            {
                SetStatus("Game Id is not a valid UUID.", MessageType.Warning);
                return;
            }

            string url = baseUrl.TrimEnd('/') + string.Format(PublicInfoEndpoint, Uri.EscapeDataString(gameId));
            request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;
            request.redirectLimit = 0;
            request.SetRequestHeader(SaiServer.CLIENT_ID_HEADER, gameId);
            request.SetRequestHeader(SaiServer.CLIENT_VERSION_HEADER, SaiServer.CLIENT_NAME + "/" + SaiServer.PACKAGE_VERSION);
            // Lets a signed-in studio member read a game that is not public yet.
            if (target.HasValidAccessToken) request.SetRequestHeader("Authorization", "Bearer " + target.AccessToken);
            request.SendWebRequest();
            if (target.ShowUrlRequest) Debug.Log($"[SaiServer] GET {url} (Inspector public game info lookup)");
            SetStatus("Loading public game info from " + baseUrl + "…", MessageType.Info);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null) { EditorApplication.update -= Poll; return; }
            if (!request.isDone) return;
            EditorApplication.update -= Poll;

            bool log = server != null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                if (log && server.ShowJsonResponse) Debug.Log($"[SaiServer] GET Response\n{request.downloadHandler.text}");
                PublicInfoResponse info = TryParse<PublicInfoResponse>(request.downloadHandler.text);
                GameName = info != null && info.game_name != null ? info.game_name : "";
                SteamAppId = info != null ? info.steam_app_id : 0;
                SteamAppIdFileSync.Sync(SteamAppId);
                Loaded = info != null;
                if (string.IsNullOrEmpty(GameName)) SetStatus("The server returned no game name.", MessageType.Warning);
                else SetStatus("", MessageType.None);
            }
            else
            {
                ErrorResponse error = TryParse<ErrorResponse>(request.downloadHandler?.text);
                string code = error != null && !string.IsNullOrEmpty(error.message_code) ? error.message_code : request.error;
                SetStatus(Describe(code), MessageType.Warning);
                if (log && server.ShowDebug) Debug.LogWarning($"[SaiServer] Public game info lookup failed: {code} (HTTP {request.responseCode})");
            }

            request.Dispose();
            request = null;
            InternalEditorUtility.RepaintAllViews();
        }

        private static string Describe(string code)
        {
            switch (code)
            {
                case PublicInfoNotFound: return "No game with this Game Id on " + baseUrl + ". Check the Game Id and the server.";
                case GameNotPublic: return "The game exists but is not public. Sign in with a studio member account in Play Mode to read its public info.";
                default: return "Public game info lookup failed on " + baseUrl + ": " + code;
            }
        }

        private static void SetStatus(string message, MessageType type)
        {
            Status = message;
            StatusType = type;
        }

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception) { return null; }
        }
    }
}
