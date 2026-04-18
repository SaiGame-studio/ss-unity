using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class BattleScript : SaiBehaviour
    {
        public event Action<string> OnRunScriptSuccess;
        public event Action<string> OnRunScriptFailure;

        [Header("Script Settings")]
        [SerializeField] protected string scriptName = "";

        [Header("Request Body")]
        [SerializeField] [TextArea(6, 15)] protected string requestBody = "{\n    \"payload\": {\n\n    }\n}";

        [Header("Response")]
        [SerializeField] [TextArea(5, 20)] protected string jsonResponse = "";

        public string JsonResponse  => this.jsonResponse;
        public string ScriptName    => this.scriptName;
        public string RequestBody   => this.requestBody;

        /// <summary>
        /// Runs a Lua script on the server and stores the raw JSON response.
        /// Endpoint: POST /api/v1/games/{game_id}/scripts/{script_name}/run
        /// </summary>
        public void RunScript(
            string overrideScriptName = null,
            string overrideRequestBody = null,
            Action<string> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[BattleScript] ► Run Script</b></color>", gameObject);

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

            string name = overrideScriptName ?? this.scriptName;

            if (string.IsNullOrEmpty(name))
            {
                onError?.Invoke("Script name is empty!");
                return;
            }

            StartCoroutine(this.RunScriptCoroutine(name, overrideRequestBody ?? this.requestBody, onSuccess, onError));
        }

        private IEnumerator RunScriptCoroutine(
            string name,
            string jsonBody,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/scripts/{name}/run";

            yield return SaiServer.Instance.PostRequest(endpoint, jsonBody ?? "{}",
                response =>
                {
                    this.jsonResponse = JsonBeautify(response);

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[BattleScript] Script '{name}' response received.");

                    this.OnRunScriptSuccess?.Invoke(response);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[BattleScript] RunScript</color> → <b><color=#00FF88>onSuccess</color></b> callback | BattleScript.cs › RunScriptCoroutine");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    this.OnRunScriptFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[BattleScript] RunScript</color> → <b><color=#FF4444>onError</color></b> callback | BattleScript.cs › RunScriptCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>Formats the stored JSON response with indentation.</summary>
        public void BeautifyResponse()
        {
            if (string.IsNullOrEmpty(this.jsonResponse)) return;
            this.jsonResponse = JsonBeautify(this.jsonResponse);
        }

        public string BeautifyJson(string json) => JsonBeautify(json);

        private static string JsonBeautify(string json)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(new string(' ', indent * 4));
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent--;
                        sb.Append(new string(' ', indent * 4));
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(new string(' ', indent * 4));
                        break;
                    case ':':
                        sb.Append(c);
                        sb.Append(' ');
                        break;
                    default:
                        if (!char.IsWhiteSpace(c))
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>Clears the stored JSON response.</summary>
        public void ClearResponse()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[BattleScript] ► Clear Response</b></color>", gameObject);

            this.jsonResponse = string.Empty;
        }
    }
}
