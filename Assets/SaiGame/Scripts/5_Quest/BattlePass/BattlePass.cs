using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class BattlePass : SaiBehaviour
    {
        public event Action<BattlePassResponse> OnGetBattlePassesSuccess;
        public event Action<string> OnGetBattlePassesFailure;
        public event Action<string, BattlePassChainsResponse> OnGetBattlePassChainsSuccess;
        public event Action<string> OnGetBattlePassChainsFailure;

        [Header("Current Battle Pass Data")]
        [SerializeField] private BattlePassResponse currentBattlePassResponse;
        [SerializeField] private int battlePassLimit = 50;
        [SerializeField] private int battlePassOffset;

        private readonly Dictionary<string, BattlePassChainsResponse> chainsCache = new Dictionary<string, BattlePassChainsResponse>();

        public BattlePassResponse CurrentBattlePassResponse => this.currentBattlePassResponse;

        public BattlePassChainsResponse GetCachedChains(string battlePassId)
        {
            this.chainsCache.TryGetValue(battlePassId, out BattlePassChainsResponse response);
            return response;
        }

        public void GetBattlePasses(
            int? limit = null,
            int? offset = null,
            Action<BattlePassResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFB74D><b>[BattlePass] ► Get Battle Passes</b></color>", gameObject);

            if (!this.ValidateRequest(onError)) return;

            StartCoroutine(this.GetAllBattlePassesCoroutine(limit ?? this.battlePassLimit, offset ?? this.battlePassOffset, onSuccess, onError));
        }

        public void GetBattlePassChains(
            string battlePassId,
            Action<BattlePassChainsResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD54F><b>[BattlePass] ► Get Chains ({battlePassId})</b></color>", gameObject);

            if (!this.ValidateRequest(onError)) return;

            if (string.IsNullOrEmpty(battlePassId))
            {
                onError?.Invoke("battlePassId cannot be empty.");
                return;
            }

            StartCoroutine(this.GetBattlePassChainsCoroutine(battlePassId, onSuccess, onError));
        }

        public void ClearBattlePasses()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF7043><b>[BattlePass] ► Clear Battle Passes</b></color>", gameObject);

            this.currentBattlePassResponse = new BattlePassResponse
            {
                pools = Array.Empty<BattlePassData>(),
                limit = this.battlePassLimit,
                offset = 0,
                total = 0
            };
            this.chainsCache.Clear();
        }

        private bool ValidateRequest(Action<string> onError)
        {
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return false;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return false;
            }

            return true;
        }

        private IEnumerator GetAllBattlePassesCoroutine(int limit, int offset, Action<BattlePassResponse> onSuccess, Action<string> onError)
        {
            List<BattlePassData> allBattlePasses = new List<BattlePassData>();
            int currentOffset = offset;
            int total = 0;
            bool failed = false;
            string errorMessage = null;

            do
            {
                string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/session-quest-pools?limit={limit}&offset={currentOffset}";
                BattlePassResponse page = null;

                yield return SaiServer.Instance.GetRequest(endpoint,
                    response =>
                    {
                        try
                        {
                            page = JsonUtility.FromJson<BattlePassResponse>(response);
                        }
                        catch (Exception exception)
                        {
                            failed = true;
                            errorMessage = $"Parse battle passes response error: {exception.Message}";
                        }
                    },
                    error =>
                    {
                        failed = true;
                        errorMessage = error;
                    });

                if (failed || page == null) break;

                if (page.pools != null)
                    allBattlePasses.AddRange(page.pools);

                total = page.total;
                currentOffset += page.pools?.Length ?? 0;

                if (page.pools == null || page.pools.Length == 0) break;
            }
            while (currentOffset < total);

            if (failed)
            {
                this.HandleBattlePassesError(errorMessage, onError);
                yield break;
            }

            this.currentBattlePassResponse = new BattlePassResponse
            {
                pools = allBattlePasses.ToArray(),
                limit = limit,
                offset = offset,
                total = total
            };
            Debug.Log($"<color=#FFB74D>[BattlePass] Loaded {this.currentBattlePassResponse.pools.Length} battle passes (total: {total})</color>");
            this.OnGetBattlePassesSuccess?.Invoke(this.currentBattlePassResponse);
            onSuccess?.Invoke(this.currentBattlePassResponse);
        }

        private IEnumerator GetBattlePassChainsCoroutine(string battlePassId, Action<BattlePassChainsResponse> onSuccess, Action<string> onError)
        {
            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/session-quest-pools/{battlePassId}";
            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        BattlePassChainsResponse chainsResponse = JsonUtility.FromJson<BattlePassChainsResponse>(response);
                        this.chainsCache[battlePassId] = chainsResponse;
                        Debug.Log($"<color=#FFD54F>[BattlePass] Loaded {chainsResponse.chains?.Length ?? 0} chains for {battlePassId}</color>");
                        this.OnGetBattlePassChainsSuccess?.Invoke(battlePassId, chainsResponse);
                        onSuccess?.Invoke(chainsResponse);
                    }
                    catch (Exception exception)
                    {
                        this.HandleBattlePassChainsError(battlePassId, $"Parse battle pass chains response error: {exception.Message}", onError);
                    }
                },
                error => this.HandleBattlePassChainsError(battlePassId, error, onError));
        }

        private void HandleBattlePassesError(string error, Action<string> onError)
        {
            Debug.LogWarning($"<color=#FF7043>[BattlePass] Get Battle Passes failed: {error}</color>", gameObject);
            this.OnGetBattlePassesFailure?.Invoke(error);
            onError?.Invoke(error);
        }

        private void HandleBattlePassChainsError(string battlePassId, string error, Action<string> onError)
        {
            Debug.LogWarning($"<color=#FF7043>[BattlePass] Get Chains ({battlePassId}) failed: {error}</color>", gameObject);
            this.OnGetBattlePassChainsFailure?.Invoke(error);
            onError?.Invoke(error);
        }
    }
}
