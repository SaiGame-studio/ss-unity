#if (STEAMWORKS_NET || SAI_STEAMWORKS_NET) && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR)
#define SAI_STEAMWORKS_AVAILABLE
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if SAI_STEAMWORKS_AVAILABLE
using Steamworks;
#endif

namespace SaiGame.Services
{
    // Built-in Steam achievement and stat calls through Steamworks.NET.
    // Achievements are defined in Steamworks; this component reads and writes
    // the local player's state. SteamAPI is prepared by SteamworksNetTicketProvider.
    [DefaultExecutionOrder(-98)]
    public class SteamAchievements : SaiBehaviour
    {
        public const string SteamworksMissing = "STEAMWORKS_NET_MISSING";
        public const string SteamClientRequired = "STEAM_CLIENT_REQUIRED";
        public const string NotInitialized = "STEAM_NOT_INITIALIZED";
        public const string NotLoggedOn = "STEAM_NOT_LOGGED_ON";
        public const string StatsUnavailable = "STEAM_STATS_UNAVAILABLE";
        public const string StatsTimeout = "STEAM_STATS_TIMEOUT";
        public const string StoreFailed = "STEAM_STATS_STORE_FAILED";
        public const string AchievementNotFound = "STEAM_ACHIEVEMENT_NOT_FOUND";
        public const string ProgressRejected = "STEAM_ACHIEVEMENT_PROGRESS_REJECTED";
        public const string StatNotFound = "STEAM_STAT_NOT_FOUND";
        public const string InvalidName = "STEAM_INVALID_NAME";
        private const string LogTag = "[SteamAchievements]";

#pragma warning disable CS0067 // Raised only when Steamworks.NET is available.
        public event Action<List<SteamAchievementInfo>> OnAchievementsLoaded;
        public event Action<string> OnAchievementUnlocked;
#pragma warning restore CS0067
        public event Action<string> OnFailure;

        [Header("Achievement State")]
        [SerializeField] protected bool isLoading;
        [SerializeField] protected bool statsLoaded;
        [SerializeField] protected List<SteamAchievementInfo> achievements = new List<SteamAchievementInfo>();
        [Tooltip("Error code of the last failed operation.")]
        [SerializeField] protected string lastError;

        [Header("Steam Settings")]
        [Tooltip("Prepares SteamAPI. Usually the SteamworksNetTicketProvider on the SaiServer prefab.")]
        [SerializeField] protected SteamworksNetTicketProvider steamClient;
        [Tooltip("Maximum time to wait for Steam to return or store stats.")]
        [SerializeField] protected float statsTimeoutSeconds = 15f;

        public bool IsLoading => isLoading;
        public bool StatsLoaded => statsLoaded;
        public IReadOnlyList<SteamAchievementInfo> Achievements => achievements;
        public string LastError => lastError;
        public SteamworksNetTicketProvider SteamClient => steamClient;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSteamClient();
        }

        protected virtual void LoadSteamClient()
        {
            if (this.steamClient != null) return;
            this.steamClient = GetComponentInChildren<SteamworksNetTicketProvider>();
        }

        public SteamAchievementInfo GetAchievement(string apiName)
        {
            return achievements.Find(item => item.api_name == apiName);
        }

        public bool IsUnlocked(string apiName)
        {
            SteamAchievementInfo info = GetAchievement(apiName);
            return info != null && info.achieved;
        }

        // Loads every achievement defined for the app with the player's state.
        public void LoadAchievements(Action<List<SteamAchievementInfo>> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Load Achievements");
#if SAI_STEAMWORKS_AVAILABLE
            pendingLoads.Add((onSuccess, onError));
            if (isLoading) return;
            if (!EnsureSteam(out string error)) { FailLoads(error); return; }
            isLoading = true;
            lastError = "";
            statsReceived.Set(SteamUserStats.RequestUserStats(SteamUser.GetSteamID()));
            loadTimeout = StartCoroutine(LoadTimeout());
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        // Unlocks an achievement by its API name and stores it on Steam.
        public void Unlock(string apiName, Action<string> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Unlock " + apiName);
            if (!ValidName(apiName, onError)) return;
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.SetAchievement(apiName)) { Fail(AchievementNotFound, onError); return; }
                StoreStats(() =>
                {
                    RefreshAchievement(apiName);
                    OnAchievementUnlocked?.Invoke(apiName);
                    Succeed("Unlock " + apiName);
                    onSuccess?.Invoke(apiName);
                }, onError);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        // Shows Steam's progress notification, e.g. 3 / 10. Does not unlock.
        public void IndicateProgress(string apiName, uint current, uint max, Action<string> onSuccess = null, Action<string> onError = null)
        {
            LogButton($"Indicate Progress {apiName} {current}/{max}");
            if (!ValidName(apiName, onError)) return;
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.IndicateAchievementProgress(apiName, current, max)) { Fail(ProgressRejected, onError); return; }
                Succeed("Indicate Progress " + apiName);
                onSuccess?.Invoke(apiName);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        public bool TryGetStat(string statName, out int value)
        {
            value = 0;
#if SAI_STEAMWORKS_AVAILABLE
            try { return statsLoaded && SteamUserStats.GetStat(statName, out value); }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        public bool TryGetStat(string statName, out float value)
        {
            value = 0f;
#if SAI_STEAMWORKS_AVAILABLE
            try { return statsLoaded && SteamUserStats.GetStat(statName, out value); }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        // Sets an INT stat and stores it. Stat-based achievements unlock on Steam's side.
        public void SetStat(string statName, int value, Action<string> onSuccess = null, Action<string> onError = null)
        {
            LogButton($"Set Stat {statName} = {value}");
            if (!ValidName(statName, onError)) return;
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.SetStat(statName, value)) { Fail(StatNotFound, onError); return; }
                StoreStats(() => { RefreshAllAchievements(); Succeed("Set Stat " + statName); onSuccess?.Invoke(statName); }, onError);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        // Sets a FLOAT stat and stores it.
        public void SetStat(string statName, float value, Action<string> onSuccess = null, Action<string> onError = null)
        {
            LogButton($"Set Stat {statName} = {value}");
            if (!ValidName(statName, onError)) return;
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.SetStat(statName, value)) { Fail(StatNotFound, onError); return; }
                StoreStats(() => { RefreshAllAchievements(); Succeed("Set Stat " + statName); onSuccess?.Invoke(statName); }, onError);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        // Testing only: locks an achievement again.
        public void Clear(string apiName, Action<string> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Clear " + apiName);
            if (!ValidName(apiName, onError)) return;
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.ClearAchievement(apiName)) { Fail(AchievementNotFound, onError); return; }
                StoreStats(() => { RefreshAchievement(apiName); Succeed("Clear " + apiName); onSuccess?.Invoke(apiName); }, onError);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

        // Testing only: resets every stat, and achievements when achievementsToo is true.
        public void ResetAll(bool achievementsToo, Action<List<SteamAchievementInfo>> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Reset All Stats");
#if SAI_STEAMWORKS_AVAILABLE
            RunWithStats(() =>
            {
                if (!SteamUserStats.ResetAllStats(achievementsToo)) { Fail(StoreFailed, onError); return; }
                StoreStats(() =>
                {
                    statsLoaded = false;
                    LoadAchievements(onSuccess, onError);
                }, onError);
            }, onError);
#else
            Fail(SteamworksMissing, onError);
#endif
        }

#if SAI_STEAMWORKS_AVAILABLE
        private readonly List<(Action<List<SteamAchievementInfo>> success, Action<string> error)> pendingLoads =
            new List<(Action<List<SteamAchievementInfo>>, Action<string>)>();
        private readonly List<(Action success, Action<string> error)> pendingStores = new List<(Action, Action<string>)>();
        private CallResult<UserStatsReceived_t> statsReceived;
        private Callback<UserStatsStored_t> statsStored;
        private Coroutine loadTimeout;
        private Coroutine storeTimeout;

        private bool EnsureSteam(out string error)
        {
            error = null;
            if (steamClient == null) error = SteamClientRequired;
            else if (!steamClient.EnsureSteamReady()) error = NotInitialized;
            else
            {
                try { if (!SteamUser.BLoggedOn()) error = NotLoggedOn; }
                catch (Exception) { error = NotInitialized; }
            }
            if (error != null) return false;
            if (statsReceived == null) statsReceived = CallResult<UserStatsReceived_t>.Create(OnStatsReceived);
            if (statsStored == null) statsStored = Callback<UserStatsStored_t>.Create(OnStatsStored);
            return true;
        }

        private void RunWithStats(Action action, Action<string> onError)
        {
            if (statsLoaded && EnsureSteam(out _)) { action(); return; }
            LoadAchievements(_ => action(), onError);
        }

        private void OnStatsReceived(UserStatsReceived_t data, bool ioFailure)
        {
            if (!isLoading) return;
            StopLoadTimeout();
            isLoading = false;
            if (ioFailure || data.m_eResult != EResult.k_EResultOK) { FailLoads(StatsUnavailable); return; }

            statsLoaded = true;
            RefreshAllAchievements();
            var callbacks = pendingLoads.ToArray();
            pendingLoads.Clear();
            OnAchievementsLoaded?.Invoke(achievements);
            Succeed("Load Achievements (" + achievements.Count + ")");
            foreach (var callback in callbacks) callback.success?.Invoke(achievements);
        }

        private IEnumerator LoadTimeout()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, statsTimeoutSeconds));
            loadTimeout = null;
            if (!isLoading) yield break;
            isLoading = false;
            statsReceived?.Cancel();
            FailLoads(StatsTimeout);
        }

        private void StopLoadTimeout()
        {
            if (loadTimeout != null) StopCoroutine(loadTimeout);
            loadTimeout = null;
        }

        private void FailLoads(string error)
        {
            var callbacks = pendingLoads.ToArray();
            pendingLoads.Clear();
            if (callbacks.Length == 0) { Fail(error, null); return; }
            for (int i = 0; i < callbacks.Length; i++) Fail(error, callbacks[i].error, i == 0);
        }

        private void StoreStats(Action onSuccess, Action<string> onError)
        {
            pendingStores.Add((onSuccess, onError));
            if (pendingStores.Count > 1) return;
            if (!SteamUserStats.StoreStats()) { FinishStores(StoreFailed); return; }
            storeTimeout = StartCoroutine(StoreTimeout());
        }

        private void OnStatsStored(UserStatsStored_t data)
        {
            if (pendingStores.Count == 0 || data.m_nGameID != new CGameID(SteamUtils.GetAppID()).m_GameID) return;
            FinishStores(data.m_eResult == EResult.k_EResultOK ? null : StoreFailed);
        }

        private IEnumerator StoreTimeout()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, statsTimeoutSeconds));
            storeTimeout = null;
            if (pendingStores.Count > 0) FinishStores(StatsTimeout);
        }

        private void FinishStores(string error)
        {
            if (storeTimeout != null) StopCoroutine(storeTimeout);
            storeTimeout = null;
            var callbacks = pendingStores.ToArray();
            pendingStores.Clear();
            for (int i = 0; i < callbacks.Length; i++)
            {
                if (error == null) callbacks[i].success?.Invoke();
                else Fail(error, callbacks[i].error, i == 0);
            }
        }

        private void RefreshAllAchievements()
        {
            achievements.Clear();
            uint count = SteamUserStats.GetNumAchievements();
            for (uint i = 0; i < count; i++)
            {
                string apiName = SteamUserStats.GetAchievementName(i);
                if (!string.IsNullOrEmpty(apiName)) achievements.Add(ReadAchievement(apiName));
            }
        }

        private void RefreshAchievement(string apiName)
        {
            int index = achievements.FindIndex(item => item.api_name == apiName);
            SteamAchievementInfo info = ReadAchievement(apiName);
            if (index >= 0) achievements[index] = info;
            else achievements.Add(info);
        }

        private static SteamAchievementInfo ReadAchievement(string apiName)
        {
            SteamUserStats.GetAchievementAndUnlockTime(apiName, out bool achieved, out uint unlockTime);
            return new SteamAchievementInfo
            {
                api_name = apiName,
                display_name = SteamUserStats.GetAchievementDisplayAttribute(apiName, "name"),
                description = SteamUserStats.GetAchievementDisplayAttribute(apiName, "desc"),
                hidden = SteamUserStats.GetAchievementDisplayAttribute(apiName, "hidden") == "1",
                achieved = achieved,
                unlock_time = achieved ? unlockTime : 0
            };
        }

        private void OnDisable()
        {
            StopLoadTimeout();
            isLoading = false;
            pendingLoads.Clear();
            if (storeTimeout != null) StopCoroutine(storeTimeout);
            storeTimeout = null;
            pendingStores.Clear();
        }

        private void OnDestroy()
        {
            statsReceived?.Dispose();
            statsStored?.Dispose();
        }
#endif

        private bool ValidName(string name, Action<string> onError)
        {
            if (!string.IsNullOrWhiteSpace(name)) return true;
            Fail(InvalidName, onError);
            return false;
        }

        private void Succeed(string action)
        {
            lastError = "";
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.Log($"<color=#66CCFF>{LogTag} {action}</color> → <b><color=#00FF88>onSuccess</color></b> callback");
        }

        private void Fail(string error, Action<string> onError, bool raiseEvent = true)
        {
            lastError = error;
            if (raiseEvent)
            {
                OnFailure?.Invoke(error);
                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                    Debug.LogWarning($"<color=#66CCFF>{LogTag}</color> → <b><color=#FF4444>onError</color></b> callback | {error}");
            }
            onError?.Invoke(error);
        }

        private void LogButton(string action)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#1B2838><b>{LogTag} ► {action}</b></color>", gameObject);
        }
    }
}
