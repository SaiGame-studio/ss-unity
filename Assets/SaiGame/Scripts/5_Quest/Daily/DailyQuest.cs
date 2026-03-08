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
            if (!this.autoLoadOnLogin) return;
            if (string.IsNullOrEmpty(this.dqPoolId)) return;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[DailyQuest] Auto-assigning daily quests after successful login...");

            this.AssignAhead(
                dqPoolId: this.dqPoolId,
                daysAhead: this.daysAhead,
                onSuccess: result =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[DailyQuest] Quests auto-assigned: {result.days?.Length ?? 0} days from {result.start_date} to {result.end_date}");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[DailyQuest] Auto-assign failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[DailyQuest] Logout successful, clearing daily quest data...");

            this.ClearLocalData();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Assign Ahead</b></color>", gameObject);

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
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quests/{poolId}/assign-ahead";

            AssignAheadRequest requestBody = new AssignAheadRequest
            {
                days_ahead = daysAheadCount
            };
            string json = JsonUtility.ToJson(requestBody);

            yield return SaiService.Instance.PostRequest(endpoint, json,
                response =>
                {
                    try
                    {
                        AssignAheadResponse assignResponse = JsonUtility.FromJson<AssignAheadResponse>(response);
                        this.currentAssignAheadResponse = assignResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Assign ahead success: {assignResponse.days?.Length ?? 0} days from {assignResponse.start_date} to {assignResponse.end_date}");

                        this.OnAssignAheadSuccess?.Invoke(assignResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] AssignAhead</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › AssignAheadCoroutine");
                        onSuccess?.Invoke(assignResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse assign-ahead response error: {e.Message}";
                        this.OnAssignAheadFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] AssignAhead</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › AssignAheadCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnAssignAheadFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
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
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[DailyQuest] ► Clear Data</b></color>", gameObject);

            this.ClearLocalData();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Get Pools</b></color>", gameObject);

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

            StartCoroutine(this.GetPoolsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetPoolsCoroutine(
            System.Action<DailyQuestPoolsResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quest-pools";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        DailyQuestPoolsResponse poolsResponse = JsonUtility.FromJson<DailyQuestPoolsResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Pools loaded: {poolsResponse.pools?.Length ?? 0} pools");

                        this.OnGetPoolsSuccess?.Invoke(poolsResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] GetPools</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › GetPoolsCoroutine");
                        onSuccess?.Invoke(poolsResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get pools response error: {e.Message}";
                        this.OnGetPoolsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetPools</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › GetPoolsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetPoolsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
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
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyQuest] ► Today Quest</b></color>", gameObject);

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
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quests/{poolId}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        TodayQuestResponse todayResponse = JsonUtility.FromJson<TodayQuestResponse>(response);
                        this.currentTodayQuestResponse = todayResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[DailyQuest] Today quests loaded: {todayResponse.entries?.Length ?? 0} entries for {todayResponse.assigned_date}");

                        this.OnGetTodayQuestsSuccess?.Invoke(todayResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyQuest] GetTodayQuests</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyQuest.cs › GetTodayQuestsCoroutine");
                        onSuccess?.Invoke(todayResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse today quest response error: {e.Message}";
                        this.OnGetTodayQuestsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyQuest] GetTodayQuests</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyQuest.cs › GetTodayQuestsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetTodayQuestsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
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
    }
}
