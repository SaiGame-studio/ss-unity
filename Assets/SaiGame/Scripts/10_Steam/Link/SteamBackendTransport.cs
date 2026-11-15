using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    // Authentication uses default TLS validation and never logs tickets/tokens.
    public static class SteamBackendTransport
    {
        public static IEnumerator Post(string endpoint, string body, Action<string> onSuccess, Action<string> onError)
        {
            return Send(UnityWebRequest.kHttpVerbPOST, endpoint, body, onSuccess, onError);
        }

        public static IEnumerator Get(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            return Send(UnityWebRequest.kHttpVerbGET, endpoint, null, onSuccess, onError);
        }

        private static IEnumerator Send(string method, string endpoint, string body, Action<string> onSuccess, Action<string> onError)
        {
            var server = SaiServer.Instance;
            if (server == null) { onError?.Invoke("SAI_SERVER_GAME_REQUIRED"); yield break; }
            if (!Uri.TryCreate(server.BaseUrl, UriKind.Absolute, out Uri baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback)))
            { onError?.Invoke("STEAM_HTTPS_REQUIRED"); yield break; }

            using (var request = new UnityWebRequest(server.BaseUrl.TrimEnd('/') + endpoint, method))
            {
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                request.redirectLimit = 0;
                request.SetRequestHeader(SaiServer.CLIENT_ID_HEADER, server.GameId);
                request.SetRequestHeader(SaiServer.CLIENT_VERSION_HEADER, SaiServer.CLIENT_NAME + "/" + SaiServer.PACKAGE_VERSION);
                if (server.HasValidAccessToken) request.SetRequestHeader("Authorization", "Bearer " + server.AccessToken);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                    yield break;
                }
                string code = request.responseCode == 0 ? "STEAM_NETWORK_ERROR" : "STEAM_REQUEST_FAILED";
                try
                {
                    var error = JsonUtility.FromJson<SteamErrorResponse>(request.downloadHandler.text);
                    if (error != null && !string.IsNullOrEmpty(error.message_code)) code = error.message_code;
                }
                catch (Exception) { /* Non-JSON gateway errors use the fallback code. */ }
                onError?.Invoke(code);
            }
        }
    }
}
