using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    public class SaiGamerProgress : SaiBehaviour
    {
        [SerializeField] protected SaiService saiService;

        // Events for other classes to listen to
        public event Action<GamerProgress> OnCreateProgressSuccess;
        public event Action<string> OnCreateProgressFailure;
        public event Action<GamerProgress> OnGetProgressSuccess;
        public event Action<string> OnGetProgressFailure;
        public event Action OnDeleteProgressSuccess;
        public event Action<string> OnDeleteProgressFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = true;

        [Header("Current Progress Data")]
        [SerializeField] protected GamerProgress currentProgress;
        
        [Header("Update Delta Values")]
        [SerializeField] protected int experienceDelta = 100;
        [SerializeField] protected int goldDelta = 50;
        [SerializeField] [TextArea(3, 10)] protected string gameData = "{}";

        public GamerProgress CurrentProgress => currentProgress;
        public bool HasProgress => currentProgress != null && !string.IsNullOrEmpty(currentProgress.id);

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSaiService();
            this.RegisterLoginListener();
        }

        protected virtual void LoadSaiService()
        {
            if (this.saiService != null) return;
            this.saiService = GetComponent<SaiService>();
        }

        protected virtual void RegisterLoginListener()
        {
            if (this.saiService == null) return;
            
            SaiAuth saiAuth = this.saiService.GetComponent<SaiAuth>();
            if (saiAuth != null)
            {
                saiAuth.OnLoginSuccess += HandleLoginSuccess;
            }
        }

        protected virtual void OnDestroy()
        {
            if (this.saiService != null)
            {
                SaiAuth saiAuth = this.saiService.GetComponent<SaiAuth>();
                if (saiAuth != null)
                {
                    saiAuth.OnLoginSuccess -= HandleLoginSuccess;
                }
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!autoLoadOnLogin) return;
            
            if (saiService != null && saiService.ShowDebug)
                Debug.Log("Auto-loading progress after successful login...");
            
            GetProgress(
                progress => 
                {
                    if (saiService != null && saiService.ShowDebug)
                        Debug.Log($"Progress auto-loaded: Level {progress.level}, XP {progress.experience}, Gold {progress.gold}");
                },
                error => 
                {
                    if (saiService != null && saiService.ShowDebug)
                        Debug.LogWarning($"Auto-load progress failed: {error}");
                }
            );
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

        public void CreateProgress(System.Action<GamerProgress> onSuccess = null, System.Action<string> onError = null)
        {
            if (saiService == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!saiService.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(CreateProgressCoroutine(onSuccess, onError));
        }

        private IEnumerator CreateProgressCoroutine(System.Action<GamerProgress> onSuccess, System.Action<string> onError)
        {
            string gameId = saiService.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gamer-progress";

            string userId = saiService.CurrentUser?.id ?? "";
            
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

            yield return saiService.PostRequest(endpoint, jsonData,
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
                        onSuccess?.Invoke(progressResponse.data);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse create progress response error: {e.Message}";
                        OnCreateProgressFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnCreateProgressFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void GetProgress(System.Action<GamerProgress> onSuccess = null, System.Action<string> onError = null)
        {
            if (saiService == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!saiService.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(GetProgressCoroutine(onSuccess, onError));
        }

        private IEnumerator GetProgressCoroutine(System.Action<GamerProgress> onSuccess, System.Action<string> onError)
        {
            string gameId = saiService.GameId;
            string endpoint = $"/api/v1/games/{gameId}/my-gamer-progress";

            yield return saiService.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        string gameDataJson = ExtractGameDataFromJson(response);
                        
                        GamerProgress progress = JsonUtility.FromJson<GamerProgress>(response);
                        this.currentProgress = progress;
                        
                        if (this.currentProgress != null)
                        {
                            this.currentProgress.game_data = gameDataJson;
                            
                            if (!string.IsNullOrEmpty(gameDataJson) && gameDataJson != "{}")
                            {
                                this.gameData = FormatJsonString(gameDataJson);
                            }
                        }

                        if (saiService != null && saiService.ShowDebug)
                            Debug.Log($"Progress loaded: Level {progress.level}, XP {progress.experience}, Gold {progress.gold}, Game Data: {progress.game_data}");

                        OnGetProgressSuccess?.Invoke(progress);
                        onSuccess?.Invoke(progress);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get progress response error: {e.Message}";
                        OnGetProgressFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetProgressFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void UpdateProgress(int experienceDelta, int goldDelta, string newGameData = null, System.Action<GamerProgress> onSuccess = null, System.Action<string> onError = null)
        {
            if (this.currentProgress == null)
            {
                onError?.Invoke("No current progress found! Create or get progress first.");
                return;
            }

            StartCoroutine(UpdateProgressCoroutine(experienceDelta, goldDelta, newGameData, onSuccess, onError));
        }

        private IEnumerator UpdateProgressCoroutine(int experienceDelta, int goldDelta, string newGameData, System.Action<GamerProgress> onSuccess, System.Action<string> onError)
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

            if (saiService != null && saiService.ShowDebug)
            {
                Debug.Log($"Updating progress with deltas - XP: +{experienceDelta}, Gold: +{goldDelta}");
                Debug.Log($"Request JSON: {jsonData}");
            }

            yield return saiService.PatchRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        string gameDataJson = ExtractGameDataFromJson(response);
                        
                        GamerProgress updatedProgress = JsonUtility.FromJson<GamerProgress>(response);
                        this.currentProgress = updatedProgress;
                        
                        if (this.currentProgress != null)
                            this.currentProgress.game_data = gameDataJson;

                        if (saiService != null && saiService.ShowDebug)
                            Debug.Log($"Progress updated successfully! New values - Level: {updatedProgress.level}, XP: {updatedProgress.experience}, Gold: {updatedProgress.gold}");

                        onSuccess?.Invoke(updatedProgress);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse update progress response error: {e.Message}";
                        onError?.Invoke(errorMsg);
                    }
                },
                error => onError?.Invoke(error)
            );
        }

        public void ClearProgress()
        {
            if (saiService == null)
            {
                ClearLocalProgress();
                Debug.Log("Progress data cleared locally (no service available)");
                return;
            }

            if (!saiService.IsAuthenticated)
            {
                ClearLocalProgress();
                if (saiService != null && saiService.ShowDebug)
                    Debug.Log("Progress data cleared locally (not authenticated)");
                return;
            }

            StartCoroutine(ClearProgressCoroutine());
        }

        private IEnumerator ClearProgressCoroutine()
        {
            string gameId = saiService.GameId;
            string endpoint = $"/api/v1/games/{gameId}/my-gamer-progress";

            yield return saiService.DeleteRequest(endpoint,
                response =>
                {
                    ClearLocalProgress();
                    OnDeleteProgressSuccess?.Invoke();
                    if (saiService != null && saiService.ShowDebug)
                        Debug.Log("Progress deleted successfully from server and local data cleared");
                },
                error =>
                {
                    ClearLocalProgress();
                    OnDeleteProgressFailure?.Invoke(error);
                    if (saiService != null && saiService.ShowDebug)
                        Debug.LogError($"Delete progress failed but local data cleared: {error}");
                }
            );
        }

        private void ClearLocalProgress()
        {
            this.currentProgress = null;
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