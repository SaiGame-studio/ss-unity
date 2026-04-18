using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class QuestProgressor : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<StartQuestResponse> OnStartQuestSuccess;
        public event Action<string> OnStartQuestFailure;
        public event Action<CheckQuestResponse> OnCheckQuestSuccess;
        public event Action<string> OnCheckQuestFailure;
        public event Action<ClaimQuestResponse> OnClaimQuestSuccess;
        public event Action<string> OnClaimQuestFailure;

        [Header("Quest Source Settings")]
        [SerializeField] protected QuestSourceType questSourceType = QuestSourceType.ChainQuest;

        [Header("Last Started Quest")]
        [SerializeField] protected StartQuestResponse lastStartedQuest;

        [Header("Last Checked Quest")]
        [SerializeField] protected CheckQuestResponse lastCheckedQuest;

        [Header("Last Claimed Quest")]
        [SerializeField] protected ClaimQuestResponse lastClaimedQuest;

        public StartQuestResponse LastStartedQuest => this.lastStartedQuest;
        public CheckQuestResponse LastCheckedQuest => this.lastCheckedQuest;
        public ClaimQuestResponse LastClaimedQuest => this.lastClaimedQuest;
        public QuestSourceType QuestSourceType => this.questSourceType;

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
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[QuestProgressor] Login detected, ready to start quests.");
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[QuestProgressor] Logout detected, clearing last quest data...");

            this.ClearLastStartedQuest();
        }

        // ── Quest Source Helpers ───────────────────────────────────────────────

        /// <summary>
        /// Builds a flat list of selectable quest entries from the active source.
        /// Extend this method when new source types are added.
        /// </summary>
        public List<QuestPickerEntry> BuildQuestPickerEntries(string chainIdFilter = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#4DD0E1><b>[QuestProgressor] ► Refresh Quest List (source: {this.questSourceType})</b></color>", gameObject);

            var entries = new List<QuestPickerEntry>();

            switch (this.questSourceType)
            {
                case QuestSourceType.ChainQuest:
                    this.AppendChainQuestEntries(entries, chainIdFilter);
                    break;
                case QuestSourceType.DailyQuest:
                    this.AppendDailyQuestEntries(entries);
                    break;
            }

            return entries;
        }

        private void AppendChainQuestEntries(List<QuestPickerEntry> entries, string chainIdFilter)
        {
            ChainQuest chainQuest = SaiServer.Instance?.ChainQuest;
            if (chainQuest == null || !chainQuest.HasChains) return;

            foreach (ChainQuestData chain in chainQuest.CurrentChainResponse.chains)
            {
                if (chain == null) continue;
                if (!string.IsNullOrEmpty(chainIdFilter) && chain.id != chainIdFilter) continue;

                ChainMembersResponse members = chainQuest.GetCachedMembers(chain.id);
                if (members?.members == null) continue;

                foreach (ChainMemberData member in members.members)
                {
                    if (member?.definition == null) continue;

                    entries.Add(new QuestPickerEntry
                    {
                        questDefinitionId = member.definition.id,
                        displayName = member.definition.name,
                        sourceLabel = chain.display_name
                    });
                }
            }
        }

        private void AppendDailyQuestEntries(List<QuestPickerEntry> entries)
        {
            DailyQuest dailyQuest = SaiServer.Instance?.DailyQuest;
            if (dailyQuest == null) return;

            TodayQuestResponse today = dailyQuest.CurrentTodayQuestResponse;
            if (today?.entries == null || today.entries.Length == 0) return;

            string poolLabel = today.pool?.display_name ?? "Daily Pool";

            foreach (DailyQuestEntryData entry in today.entries)
            {
                if (entry?.quest == null) continue;

                entries.Add(new QuestPickerEntry
                {
                    questDefinitionId = entry.quest.id,
                    displayName = entry.quest.name,
                    sourceLabel = poolLabel
                });
            }
        }

        // ── Start Quest ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a quest for the authenticated user.
        /// Endpoint: POST /api/v1/games/{gameId}/quests/{questDefinitionId}/start
        /// </summary>
        public void StartQuest(
            string questDefinitionId,
            System.Action<StartQuestResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[QuestProgressor] ► Start Quest ({questDefinitionId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(questDefinitionId))
            {
                onError?.Invoke("questDefinitionId cannot be empty.");
                return;
            }

            StartCoroutine(this.StartQuestCoroutine(questDefinitionId, onSuccess, onError));
        }

        private IEnumerator StartQuestCoroutine(
            string questDefinitionId,
            System.Action<StartQuestResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/{questDefinitionId}/start";

            yield return SaiServer.Instance.PostRequest(endpoint, "{}",
                response =>
                {
                    try
                    {
                        StartQuestResponse questResponse = JsonUtility.FromJson<StartQuestResponse>(response);
                        this.lastStartedQuest = questResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[QuestProgressor] Quest started: id={questResponse.id}, status={questResponse.status}");

                        this.OnStartQuestSuccess?.Invoke(questResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[QuestProgressor] StartQuest</color> → <b><color=#00FF88>onSuccess</color></b> callback | QuestProgressor.cs › StartQuestCoroutine");
                        onSuccess?.Invoke(questResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse start quest response error: {e.Message}";
                        this.OnStartQuestFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] StartQuest</color> → <b><color=#FF4444>onError</color></b> callback (parse) | QuestProgressor.cs › StartQuestCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnStartQuestFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] StartQuest</color> → <b><color=#FF4444>onError</color></b> callback (network) | QuestProgressor.cs › StartQuestCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Check Quest ────────────────────────────────────────────────────────

        /// <summary>
        /// Checks the current quest progress and returns the updated state.
        /// Endpoint: POST /api/v1/games/{gameId}/quests/{questDefinitionId}/check
        /// </summary>
        public void CheckQuest(
            string questDefinitionId,
            System.Action<CheckQuestResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#4DD0E1><b>[QuestProgressor] ► Check Quest ({questDefinitionId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(questDefinitionId))
            {
                onError?.Invoke("questDefinitionId cannot be empty.");
                return;
            }

            StartCoroutine(this.CheckQuestCoroutine(questDefinitionId, onSuccess, onError));
        }

        private IEnumerator CheckQuestCoroutine(
            string questDefinitionId,
            System.Action<CheckQuestResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/{questDefinitionId}/check";

            yield return SaiServer.Instance.PostRequest(endpoint, "{}",
                response =>
                {
                    try
                    {
                        CheckQuestResponse checkResponse = JsonUtility.FromJson<CheckQuestResponse>(response);

                        // progress_data is a dynamic object — extract it manually as raw JSON
                        if (checkResponse.progress != null)
                        {
                            string progressBlock = this.ExtractJsonObject(response, "progress");
                            if (progressBlock != null)
                                checkResponse.progress.progress_data_json = this.ExtractJsonObject(progressBlock, "progress_data");
                        }

                        this.lastCheckedQuest = checkResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[QuestProgressor] Quest checked: status={checkResponse.progress?.status}, quest={checkResponse.quest_definition?.name}");

                        this.OnCheckQuestSuccess?.Invoke(checkResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[QuestProgressor] CheckQuest</color> → <b><color=#00FF88>onSuccess</color></b> callback | QuestProgressor.cs › CheckQuestCoroutine");
                        onSuccess?.Invoke(checkResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse check quest response error: {e.Message}";
                        this.OnCheckQuestFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] CheckQuest</color> → <b><color=#FF4444>onError</color></b> callback (parse) | QuestProgressor.cs › CheckQuestCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnCheckQuestFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] CheckQuest</color> → <b><color=#FF4444>onError</color></b> callback (network) | QuestProgressor.cs › CheckQuestCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Claim Quest ────────────────────────────────────────────────────────

        /// <summary>
        /// Claims rewards for a completed quest.
        /// Endpoint: POST /api/v1/games/{gameId}/quests/{questDefinitionId}/claim
        /// </summary>
        public void ClaimQuest(
            string questDefinitionId,
            System.Action<ClaimQuestResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#AAFFAA><b>[QuestProgressor] ► Claim Quest ({questDefinitionId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(questDefinitionId))
            {
                onError?.Invoke("questDefinitionId cannot be empty.");
                return;
            }

            StartCoroutine(this.ClaimQuestCoroutine(questDefinitionId, onSuccess, onError));
        }

        private IEnumerator ClaimQuestCoroutine(
            string questDefinitionId,
            System.Action<ClaimQuestResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/{questDefinitionId}/claim";

            yield return SaiServer.Instance.PostRequest(endpoint, "{}",
                response =>
                {
                    try
                    {
                        ClaimQuestResponse claimResponse = JsonUtility.FromJson<ClaimQuestResponse>(response);
                        this.lastClaimedQuest = claimResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[QuestProgressor] Quest claimed: id={claimResponse.id}  claimed_at={claimResponse.claimed_at}");

                        this.OnClaimQuestSuccess?.Invoke(claimResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[QuestProgressor] ClaimQuest</color> → <b><color=#00FF88>onSuccess</color></b> callback | QuestProgressor.cs › ClaimQuestCoroutine");
                        onSuccess?.Invoke(claimResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse claim quest response error: {e.Message}";
                        this.OnClaimQuestFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] ClaimQuest</color> → <b><color=#FF4444>onError</color></b> callback (parse) | QuestProgressor.cs › ClaimQuestCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnClaimQuestFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[QuestProgressor] ClaimQuest</color> → <b><color=#FF4444>onError</color></b> callback (network) | QuestProgressor.cs › ClaimQuestCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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

        // ── Local State ───────────────────────────────────────────────────────

        public void ClearLastStartedQuest()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[QuestProgressor] ► Clear Last Quest</b></color>", gameObject);

            this.lastStartedQuest = null;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[QuestProgressor] Last started quest cleared");
        }

        public void SetQuestSourceType(QuestSourceType sourceType)
        {
            this.questSourceType = sourceType;
        }
    }
}
