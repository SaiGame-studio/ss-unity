using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-98)]
    public class DailyTimeframe : SaiBehaviour
    {
        public enum TimeframePreset
        {
            ThisWeek,
            ThisMonth,
        }

        public event Action<DailyTimeframeResponse> OnGetTimeframeSuccess;
        public event Action<string> OnGetTimeframeFailure;
        public event Action<DailyQuestPoolsResponse> OnGetPoolsSuccess;
        public event Action<string> OnGetPoolsFailure;

        [Header("Daily Timeframe Settings")]
        [SerializeField] private string poolKey = "";
        [SerializeField] private TimeframePreset timeframePreset = TimeframePreset.ThisWeek;
        [SerializeField] private string startDate = "";
        [SerializeField] private string endDate = "";

        [Header("Current Daily Timeframe Data")]
        [SerializeField] private DailyTimeframeResponse currentResponse;
        [SerializeField] private DailyQuestPoolsResponse currentPoolsResponse;

        public DailyTimeframeResponse CurrentResponse => this.currentResponse;
        public DailyQuestPoolsResponse CurrentPoolsResponse => this.currentPoolsResponse;
        public DailyQuest DailyQuestSource => SaiServer.Instance?.DailyQuest;

        protected override void ResetValue()
        {
            base.ResetValue();
            this.SetThisWeekDateRange();
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (string.IsNullOrEmpty(this.startDate) || string.IsNullOrEmpty(this.endDate))
                this.SetThisWeekDateRange();
        }

        public void CopyPoolsFromDailyQuest()
        {
            DailyQuestPoolsResponse sourceResponse = this.DailyQuestSource?.CurrentPoolsResponse;
            if (sourceResponse != null)
                this.currentPoolsResponse = sourceResponse;
        }

        public void LoadPools(
            Action<DailyQuestPoolsResponse> onSuccess = null,
            Action<string> onError = null)
        {
            DailyQuest source = this.DailyQuestSource;
            if (source == null)
            {
                onError?.Invoke("DailyQuest service was not found on SaiServer.");
                return;
            }

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyTimeframe] ► Load Pools via DailyQuest</b></color>", gameObject);

            source.GetPools(
                response =>
                {
                    this.currentPoolsResponse = response;
                    this.OnGetPoolsSuccess?.Invoke(response);
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    this.OnGetPoolsFailure?.Invoke(error);
                    onError?.Invoke(error);
                });
        }

        public void GetTimeframe(
            string requestedPoolKey = null,
            string requestedStartDate = null,
            string requestedEndDate = null,
            Action<DailyTimeframeResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[DailyTimeframe] ► Get Timeframe</b></color>", gameObject);

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

            string actualPoolKey = string.IsNullOrEmpty(requestedPoolKey) ? this.poolKey : requestedPoolKey;
            string actualStartDate = string.IsNullOrEmpty(requestedStartDate) ? this.startDate : requestedStartDate;
            string actualEndDate = string.IsNullOrEmpty(requestedEndDate) ? this.endDate : requestedEndDate;

            if (string.IsNullOrEmpty(actualPoolKey))
            {
                onError?.Invoke("poolKey cannot be empty.");
                return;
            }

            if (string.IsNullOrEmpty(actualStartDate) || string.IsNullOrEmpty(actualEndDate))
            {
                onError?.Invoke("startDate and endDate cannot be empty.");
                return;
            }

            StartCoroutine(this.GetTimeframeCoroutine(actualPoolKey, actualStartDate, actualEndDate, onSuccess, onError));
        }

        private IEnumerator GetTimeframeCoroutine(
            string requestedPoolKey,
            string requestedStartDate,
            string requestedEndDate,
            Action<DailyTimeframeResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/daily-quests/pools/{Uri.EscapeDataString(requestedPoolKey)}/assigned-timeframe" +
                              $"?start_date={Uri.EscapeDataString(requestedStartDate)}&end_date={Uri.EscapeDataString(requestedEndDate)}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        DailyTimeframeResponse timeframeResponse = JsonUtility.FromJson<DailyTimeframeResponse>(response);
                        this.currentResponse = timeframeResponse;
                        this.OnGetTimeframeSuccess?.Invoke(timeframeResponse);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[DailyTimeframe] Loaded {timeframeResponse.days?.Length ?? 0} days from {timeframeResponse.start_date} to {timeframeResponse.end_date}");
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[DailyTimeframe] GetTimeframe</color> → <b><color=#00FF88>onSuccess</color></b> callback | DailyTimeframe.cs › GetTimeframeCoroutine");

                        onSuccess?.Invoke(timeframeResponse);
                    }
                    catch (Exception exception)
                    {
                        string errorMessage = $"Parse daily timeframe response error: {exception.Message}";
                        this.OnGetTimeframeFailure?.Invoke(errorMessage);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[DailyTimeframe] GetTimeframe</color> → <b><color=#FF4444>onError</color></b> callback (parse) | DailyTimeframe.cs › GetTimeframeCoroutine | {errorMessage}");
                        onError?.Invoke(errorMessage);
                    }
                },
                error =>
                {
                    this.OnGetTimeframeFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[DailyTimeframe] GetTimeframe</color> → <b><color=#FF4444>onError</color></b> callback (network) | DailyTimeframe.cs › GetTimeframeCoroutine | {error}");
                    onError?.Invoke(error);
                });
        }

        public void ClearData()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[DailyTimeframe] ► Clear Data</b></color>", gameObject);

            this.currentResponse = null;
        }

        public string GetPoolKey() => this.poolKey;
        public string GetStartDate() => this.startDate;
        public string GetEndDate() => this.endDate;
        public TimeframePreset GetTimeframePreset() => this.timeframePreset;

        public void SetPoolKey(string value) => this.poolKey = value;
        public void SetDateRange(string start, string end)
        {
            this.startDate = start;
            this.endDate = end;
        }

        private void SetThisWeekDateRange()
        {
            DateTime today = DateTime.Today;
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime start = today.AddDays(-daysFromMonday);
            this.startDate = start.ToString("yyyy-MM-dd");
            this.endDate = start.AddDays(6).ToString("yyyy-MM-dd");
        }
    }
}
