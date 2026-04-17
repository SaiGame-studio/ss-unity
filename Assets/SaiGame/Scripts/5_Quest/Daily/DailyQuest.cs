using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class DailyQuest : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<DailyQuestPoolsResponse> OnGetPoolsSuccess;
        public event Action<string> OnGetPoolsFailure;
        public event Action<TodayQuestResponse> OnGetTodayQuestsSuccess;
        public event Action<string> OnGetTodayQuestsFailure;
        public event Action<AssignAheadResponse> OnAssignAheadSuccess;
        public event Action<string> OnAssignAheadFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Daily Quest Settings")]
        [SerializeField] protected string dqPoolId = "";
        [SerializeField] protected int daysAhead = 7;

        [Header("Current Daily Quest Data")]
        [SerializeField] protected AssignAheadResponse currentAssignAheadResponse;
        [SerializeField] protected TodayQuestResponse currentTodayQuestResponse;

        public AssignAheadResponse CurrentAssignAheadResponse => this.currentAssignAheadResponse;
        public TodayQuestResponse CurrentTodayQuestResponse => this.currentTodayQuestResponse;
        public bool HasDays => this.currentAssignAheadResponse != null
                               && this.currentAssignAheadResponse.days != null
                               && this.currentAssignAheadResponse.days.Length > 0;

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
                Debug.Log("[DailyQuest] Auto-loading pools after successful login...");

            this.GetPools(
                onSuccess: pools =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[DailyQuest] Pools auto-loaded: {pools.pools?.Length ?? 0} pools");
                },
                onError: error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[DailyQuest] Auto-load pools failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[DailyQuest] Logout successful, clearing daily quest data...");

            this.ClearLocalData();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[DailyQuest] Daily quest data cleared successfully");
        }

        /// <summary>
        /// Assigns daily quests ahead for the given pool ID.
        /// Endpoint: POST /api/v1/games/{gameId}/daily-quests/{dqPoolId}/assign-ahead
        /// </summary>
        public void AssignAhead(
            string dqPoolId = null,
            int? daysAhead = null,
            System.Action<AssignAheadResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Assign Ahead</b></color>", gameObject);

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

            string poolId = string.IsNullOrEmpty(dqPoolId) ? this.dqPoolId : dqPoolId;

            if (string.IsNullOrEmpty(poolId))
            {
                onError?.Invoke("dqPoolId cannot be empty.");
                return;
            }

            int actualDaysAhead = daysAhead ?? this.daysAhead;

            StartCoroutine(this.AssignAheadCoroutine(poolId, actualDaysAhead, onSuccess, onError));
        }

        private IEnumerator AssignAheadCoroutine(
            string poolId,
            int daysAheadCount,
            System.Action<AssignAheadResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quests/{poolId}/assign-ahead";

            AssignAheadRequest requestBody = new AssignAheadRequest
            {
                days_ahead = daysAheadCount
            };
            string json = JsonUtility.ToJson(requestBody);

            yield return SaiServer.Instance.PostRequest(endpoint, json,
                response =>
                {
                    try
                    {
                        AssignAheadResponse assignResponse = JsonUtility.FromJson<AssignAheadResponse>(response);
                        this.currentAssignAheadResponse = assignResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Assign ahead success: {assignResponse.days?.Length ?? 0} days from {assignResponse.start_date} to {assignResponse.end_date}");

                        this.OnAssignAheadSuccess?.Invoke(assignResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] AssignAhead</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › AssignAheadCoroutine");
                        onSuccess?.Invoke(assignResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse assign-ahead response error: {e.Message}";
                        this.OnAssignAheadFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] AssignAhead</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › AssignAheadCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnAssignAheadFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[DailyQuest] AssignAhead</color> → <b><color=#FF4444>onError</color></b> callback (network) | DailyQuest.cs › AssignAheadCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears locally cached daily quest data.
        /// </summary>
        public void ClearData()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[DailyQuest] ► Clear Data</b></color>", gameObject);

            this.ClearLocalData();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[DailyQuest] Daily quest data cleared locally");
        }

        private void ClearLocalData()
        {
            this.currentAssignAheadResponse = null;
            this.currentTodayQuestResponse = null;
        }

        // ── Get Pools ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches all daily quest pools for the current game.
        /// Endpoint: GET /api/v1/games/{gameId}/daily-quest-pools
        /// </summary>
        public void GetPools(
            System.Action<DailyQuestPoolsResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Get Pools</b></color>", gameObject);

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

            StartCoroutine(this.GetPoolsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetPoolsCoroutine(
            System.Action<DailyQuestPoolsResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quest-pools";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        DailyQuestPoolsResponse poolsResponse = JsonUtility.FromJson<DailyQuestPoolsResponse>(response);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Pools loaded: {poolsResponse.pools?.Length ?? 0} pools");

                        this.OnGetPoolsSuccess?.Invoke(poolsResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] GetPools</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › GetPoolsCoroutine");
                        onSuccess?.Invoke(poolsResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get pools response error: {e.Message}";
                        this.OnGetPoolsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetPools</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › GetPoolsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetPoolsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetPools</color> → <b><color=#FF4444>onError</color></b> callback (network) | DailyQuest.cs › GetPoolsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Today Quest ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches today's daily quests for the selected pool.
        /// Endpoint: GET /api/v1/games/{gameId}/daily-quests/{dqPoolId}
        /// </summary>
        public void GetTodayQuests(
            string dqPoolId = null,
            System.Action<TodayQuestResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Today Quest</b></color>", gameObject);

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

            string poolId = string.IsNullOrEmpty(dqPoolId) ? this.dqPoolId : dqPoolId;

            if (string.IsNullOrEmpty(poolId))
            {
                onError?.Invoke("dqPoolId cannot be empty.");
                return;
            }

            StartCoroutine(this.GetTodayQuestsCoroutine(poolId, onSuccess, onError));
        }

        private IEnumerator GetTodayQuestsCoroutine(
            string poolId,
            System.Action<TodayQuestResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quests/{poolId}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        TodayQuestResponse todayResponse = JsonUtility.FromJson<TodayQuestResponse>(response);

                        // progress_data inside each entry is dynamic JSON — extract manually as raw strings.
                        if (todayResponse.entries != null && todayResponse.entries.Length > 0)
                        {
                            string[] perEntryProgressData = this.ExtractEntriesProgressData(response);
                            for (int i = 0; i < todayResponse.entries.Length && i < perEntryProgressData.Length; i++)
                            {
                                if (todayResponse.entries[i]?.progress != null)
                                    todayResponse.entries[i].progress.progress_data_json = perEntryProgressData[i];
                            }
                        }

                        this.currentTodayQuestResponse = todayResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Today quests loaded: {todayResponse.entries?.Length ?? 0} entries for {todayResponse.assigned_date}");

                        this.OnGetTodayQuestsSuccess?.Invoke(todayResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] GetTodayQuests</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › GetTodayQuestsCoroutine");
                        onSuccess?.Invoke(todayResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse today quest response error: {e.Message}";
                        this.OnGetTodayQuestsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetTodayQuests</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › GetTodayQuestsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetTodayQuestsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetTodayQuests</color> → <b><color=#FF4444>onError</color></b> callback (network) | DailyQuest.cs › GetTodayQuestsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached day entry matching the given date string (yyyy-MM-dd), or null.</summary>
        public DailyDayData GetDayByDate(string date)
        {
            if (this.currentAssignAheadResponse?.days == null) return null;

            foreach (DailyDayData day in this.currentAssignAheadResponse.days)
            {
                if (day.date == date) return day;
            }

            return null;
        }

        /// <summary>Returns the day entry marked as today, or null if not loaded.</summary>
        public DailyDayData GetToday()
        {
            if (this.currentAssignAheadResponse?.days == null) return null;

            foreach (DailyDayData day in this.currentAssignAheadResponse.days)
            {
                if (day.is_today) return day;
            }

            return null;
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetDqPoolId(string poolId) => this.dqPoolId = poolId;
        public void SetDaysAhead(int days) => this.daysAhead = days;

        public string GetDqPoolId() => this.dqPoolId;
        public int GetDaysAhead() => this.daysAhead;

        // ── Dynamic JSON helpers ──────────────────────────────────────────────

        /// <summary>
        /// Walks the top-level entries[] array and extracts each entry's "progress.progress_data"
        /// block as raw JSON. Returns a parallel array (one slot per entry; null if absent).
        /// </summary>
        private string[] ExtractEntriesProgressData(string fullJson)
        {
            int entriesIdx = fullJson.IndexOf("\"entries\"");
            if (entriesIdx < 0) return new string[0];
            int colon = fullJson.IndexOf(':', entriesIdx);
            if (colon < 0) return new string[0];
            int arrayStart = fullJson.IndexOf('[', colon);
            if (arrayStart < 0) return new string[0];

            var result = new System.Collections.Generic.List<string>();
            int depth = 0;
            int objStart = -1;
            for (int i = arrayStart + 1; i < fullJson.Length; i++)
            {
                char c = fullJson[i];
                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string obj = fullJson.Substring(objStart, i - objStart + 1);
                        string progressBlock = this.ExtractJsonObject(obj, "progress");
                        string progressData = progressBlock != null ? this.ExtractJsonObject(progressBlock, "progress_data") : null;
                        result.Add(progressData);
                        objStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    break;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Finds the value of a JSON object key and returns the entire {…} block as a string.
        /// Returns null if the key is not found or the value is not an object.
        /// </summary>
        private string ExtractJsonObject(string json, string key)
        {
            string searchKey = "\"" + key + "\"";
            int keyIdx = json.IndexOf(searchKey);
            if (keyIdx < 0) return null;

            int colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0) return null;

            int start = colonIdx + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\n' || json[start] == '\r' || json[start] == '\t'))
                start++;

            if (start >= json.Length || json[start] != '{') return null;

            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }
    }
}
