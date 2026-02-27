using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class GamerProgress : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<GamerProgressData> OnCreateProgressSuccess;
        public event Action<string> OnCreateProgressFailure;
        public event Action<GamerProgressData> OnGetProgressSuccess;
        public event Action<string> OnGetProgressFailure;
        public event Action OnDeleteProgressSuccess;
        public event Action<string> OnDeleteProgressFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Progress Data")]
        [SerializeField] protected GamerProgressData currentProgress;

        [Header("Update Delta Values")]
        [SerializeField] protected int experienceDelta = 100;
        [SerializeField] protected int goldDelta = 50;
        [SerializeField][TextArea(3, 10)] protected string gameData = "{}";

        public GamerProgressData CurrentProgress => currentProgress;
        public bool HasProgress => currentProgress != null && !string.IsNullOrEmpty(currentProgress.id);

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
        }

        protected virtual void RegisterLoginListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLoginSuccess += HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLogoutSuccess += HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiService.Instance?.SaiAuth != null)
            {
                SaiService.Instance.SaiAuth.OnLoginSuccess -= HandleLoginSuccess;
                SaiService.Instance.SaiAuth.OnLogoutSuccess -= HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!autoLoadOnLogin) return;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("Auto-loading progress after successful login...");

            GetProgress(
                progress =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"Progress auto-loaded: Level {progress.level}, XP {progress.experience}, Gold {progress.gold}");
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"Auto-load progress failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[GamerProgress] Logout successful, clearing progress data...");

            ClearLocalProgress();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[GamerProgress] Progress data cleared successfully");
        }

        private string ExtractGameDataFromJson(string jsonResponse)
        {
            int dataIndex = jsonResponse.IndexOf("\"game_data\"");
            if (dataIndex == -1) return "{}";

            int colonIndex = jsonResponse.IndexOf(':', dataIndex);
            if (colonIndex == -1) return "{}";

            int startIndex = colonIndex + 1;
            while (startIndex < jsonResponse.Length && char.IsWhiteSpace(jsonResponse[startIndex]))
                startIndex++;

            if (startIndex >= jsonResponse.Length) return "{}";

            char firstChar = jsonResponse[startIndex];
            int endIndex = startIndex;

            if (firstChar == '{')
            {
                int braceCount = 1;
                endIndex++;
                while (endIndex < jsonResponse.Length && braceCount > 0)
                {
                    if (jsonResponse[endIndex] == '{') braceCount++;
                    else if (jsonResponse[endIndex] == '}') braceCount--;
                    endIndex++;
                }
                return jsonResponse.Substring(startIndex, endIndex - startIndex);
            }
            else if (firstChar == '"')
            {
                endIndex++;
                while (endIndex < jsonResponse.Length && jsonResponse[endIndex] != '"')
                {
                    if (jsonResponse[endIndex] == '\\') endIndex++;
                    endIndex++;
                }
                endIndex++;
                return jsonResponse.Substring(startIndex, endIndex - startIndex);
            }

            return "{}";
        }

        private string FormatJsonString(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString) || jsonString == "{}") return "{}";

            try
            {
                int indent = 0;
                System.Text.StringBuilder formatted = new System.Text.StringBuilder();
                bool inString = false;
                bool escaped = false;

                foreach (char c in jsonString)
                {
                    if (escaped)
                    {
                        formatted.Append(c);
                        escaped = false;
                        continue;
                    }

                    if (c == '\\' && inString)
                    {
                        formatted.Append(c);
                        escaped = true;
                        continue;
                    }

                    if (c == '\"')
                    {
                        inString = !inString;
                        formatted.Append(c);
                        continue;
                    }

                    if (inString)
                    {
                        formatted.Append(c);
                        continue;
                    }

                    switch (c)
                    {
                        case '{':
                        case '[':
                            formatted.Append(c);
                            formatted.Append("\n");
                            indent++;
                            formatted.Append(new string(' ', indent * 2));
                            break;
                        case '}':
                        case ']':
                            formatted.Append("\n");
                            indent--;
                            formatted.Append(new string(' ', indent * 2));
                            formatted.Append(c);
                            break;
                        case ',':
                            formatted.Append(c);
                            formatted.Append("\n");
                            formatted.Append(new string(' ', indent * 2));
                            break;
                        case ':':
                            formatted.Append(c);
                            formatted.Append(' ');
                            break;
                        default:
                            if (!char.IsWhiteSpace(c))
                                formatted.Append(c);
                            break;
                    }
                }

                return formatted.ToString();
            }
            catch (System.Exception)
            {
                return jsonString;
            }
        }

        public void CreateProgress(System.Action<GamerProgressData> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[GamerProgress] ► Create Progress</b></color>", gameObject);
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

            StartCoroutine(CreateProgressCoroutine(onSuccess, onError));
        }

        private IEnumerator CreateProgressCoroutine(System.Action<GamerProgressData> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gamer-progress";

            string userId = SaiService.Instance.CurrentUser?.id ?? "";

            // Ensure game_data is valid JSON
            string gameDataJson = string.IsNullOrEmpty(this.gameData) ? "{}" : this.gameData;

            // Manually construct JSON to avoid escaping game_data
            string jsonData = $@"{{
    ""user_id"": ""{userId}"",
    ""game_id"": ""{gameId}"",
    ""experience"": 0,
    ""gold"": 0,
    ""game_data"": {gameDataJson}
}}";

            yield return SaiService.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        string gameDataJson = ExtractGameDataFromJson(response);

                        CreateGamerProgressResponse progressResponse = JsonUtility.FromJson<CreateGamerProgressResponse>(response);
                        this.currentProgress = progressResponse.data;

                        if (this.currentProgress != null)
                            this.currentProgress.game_data = gameDataJson;

                        OnCreateProgressSuccess?.Invoke(progressResponse.data);
                        Debug.Log("<color=#66CCFF>[GamerProgress] CreateProgress</color> → <b><color=#00FF88>onSuccess</color></b> callback | GamerProgress.cs › CreateProgressCoroutine");
                        onSuccess?.Invoke(progressResponse.data);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse create progress response error: {e.Message}";
                        OnCreateProgressFailure?.Invoke(errorMsg);
                        Debug.LogWarning($"<color=#66CCFF>[GamerProgress] CreateProgress</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GamerProgress.cs › CreateProgressCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnCreateProgressFailure?.Invoke(error);
                    Debug.LogWarning($"<color=#66CCFF>[GamerProgress] CreateProgress</color> → <b><color=#FF4444>onError</color></b> callback (network) | GamerProgress.cs › CreateProgressCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void GetProgress(System.Action<GamerProgressData> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FF88><b>[GamerProgress] ► Get Progress</b></color>", gameObject);
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

            StartCoroutine(GetProgressCoroutine(onSuccess, onError));
        }

        private IEnumerator GetProgressCoroutine(System.Action<GamerProgressData> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/my-gamer-progress";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        string gameDataJson = ExtractGameDataFromJson(response);

                        GamerProgressData progress = JsonUtility.FromJson<GamerProgressData>(response);
                        this.currentProgress = progress;

                        if (this.currentProgress != null)
                        {
                            this.currentProgress.game_data = gameDataJson;

                            if (!string.IsNullOrEmpty(gameDataJson) && gameDataJson != "{}")
                            {
                                this.gameData = FormatJsonString(gameDataJson);
                            }
                        }

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Progress loaded: Level {progress.level}, XP {progress.experience}, Gold {progress.gold}, Game Data: {progress.game_data}");

                        OnGetProgressSuccess?.Invoke(progress);
                        Debug.Log("<color=#66CCFF>[GamerProgress] GetProgress</color> → <b><color=#00FF88>onSuccess</color></b> callback | GamerProgress.cs › GetProgressCoroutine");
                        onSuccess?.Invoke(progress);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get progress response error: {e.Message}";
                        OnGetProgressFailure?.Invoke(errorMsg);
                        Debug.LogWarning($"<color=#66CCFF>[GamerProgress] GetProgress</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GamerProgress.cs › GetProgressCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetProgressFailure?.Invoke(error);
                    Debug.LogWarning($"<color=#66CCFF>[GamerProgress] GetProgress</color> → <b><color=#FF4444>onError</color></b> callback (network) | GamerProgress.cs › GetProgressCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void UpdateProgress(int experienceDelta, int goldDelta, string newGameData = null, System.Action<GamerProgressData> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[GamerProgress] ► Update Progress</b></color>", gameObject);
            if (this.currentProgress == null)
            {
                onError?.Invoke("No current progress found! Create or get progress first.");
                return;
            }

            StartCoroutine(UpdateProgressCoroutine(experienceDelta, goldDelta, newGameData, onSuccess, onError));
        }

        private IEnumerator UpdateProgressCoroutine(int experienceDelta, int goldDelta, string newGameData, System.Action<GamerProgressData> onSuccess, System.Action<string> onError)
        {
            string endpoint = $"/api/v1/gamer-progress/{currentProgress.id}";

            // Use provided newGameData, or fallback to Inspector gameData field, or use current progress data, or default to {}
            string gameDataJson = !string.IsNullOrEmpty(newGameData) ? newGameData :
                                  !string.IsNullOrEmpty(this.gameData) ? this.gameData :
                                  !string.IsNullOrEmpty(currentProgress.game_data) ? currentProgress.game_data :
                                  "{}";

            // Manually construct JSON to avoid escaping game_data
            string jsonData = $@"{{
    ""experience_delta"": {experienceDelta},
    ""gold_delta"": {goldDelta},
    ""game_data"": {gameDataJson}
}}";

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
            {
                Debug.Log($"Updating progress with deltas - XP: +{experienceDelta}, Gold: +{goldDelta}");
                Debug.Log($"Request JSON: {jsonData}");
            }

            yield return SaiService.Instance.PatchRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        string gameDataJson = ExtractGameDataFromJson(response);

                        GamerProgressData updatedProgress = JsonUtility.FromJson<GamerProgressData>(response);
                        this.currentProgress = updatedProgress;

                        if (this.currentProgress != null)
                            this.currentProgress.game_data = gameDataJson;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Progress updated successfully! New values - Level: {updatedProgress.level}, XP: {updatedProgress.experience}, Gold: {updatedProgress.gold}");

                        Debug.Log("<color=#66CCFF>[GamerProgress] UpdateProgress</color> → <b><color=#00FF88>onSuccess</color></b> callback | GamerProgress.cs › UpdateProgressCoroutine");
                        onSuccess?.Invoke(updatedProgress);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse update progress response error: {e.Message}";
                        Debug.LogWarning($"<color=#66CCFF>[GamerProgress] UpdateProgress</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GamerProgress.cs › UpdateProgressCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    Debug.LogWarning($"<color=#66CCFF>[GamerProgress] UpdateProgress</color> → <b><color=#FF4444>onError</color></b> callback (network) | GamerProgress.cs › UpdateProgressCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClearProgress()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[GamerProgress] ► Clear Progress</b></color>", gameObject);
            if (SaiService.Instance == null)
            {
                ClearLocalProgress();
                Debug.Log("Progress data cleared locally (no service available)");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                ClearLocalProgress();
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log("Progress data cleared locally (not authenticated)");
                return;
            }

            StartCoroutine(ClearProgressCoroutine());
        }

        private IEnumerator ClearProgressCoroutine()
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/my-gamer-progress";

            yield return SaiService.Instance.DeleteRequest(endpoint,
                response =>
                {
                    ClearLocalProgress();
                    OnDeleteProgressSuccess?.Invoke();
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log("Progress deleted successfully from server and local data cleared");
                },
                error =>
                {
                    ClearLocalProgress();
                    OnDeleteProgressFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogError($"Delete progress failed but local data cleared: {error}");
                }
            );
        }

        private void ClearLocalProgress()
        {
            this.currentProgress = new GamerProgressData
            {
                id = "",
                user_id = "",
                game_id = "",
                level = 0,
                experience = 0,
                gold = 0,
                game_data = "{}",
                created_at = 0,
                updated_at = 0,
                version = 0
            };
            this.gameData = "{}";
        }

        public void SetGameData(string jsonData)
        {
            if (this.currentProgress != null)
            {
                this.currentProgress.game_data = jsonData;
            }
            else
            {
                this.gameData = jsonData;
            }
        }

        public string GetGameData()
        {
            return this.currentProgress?.game_data ?? this.gameData;
        }
    }
}