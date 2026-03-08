using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class ChainQuest : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<ChainQuestResponse> OnGetChainsSuccess;
        public event Action<string> OnGetChainsFailure;
        public event Action<ChainMembersResponse> OnGetChainMembersSuccess;
        public event Action<string> OnGetChainMembersFailure;
        public event Action<ChainQuestTreeResponse> OnGetChainTreeSuccess;
        public event Action<string> OnGetChainTreeFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Chain Data")]
        [SerializeField] protected ChainQuestResponse currentChainResponse;
        [SerializeField] protected int chainLimit = 50;
        [SerializeField] protected int chainOffset = 0;

        public ChainQuestResponse CurrentChainResponse => this.currentChainResponse;
        public bool HasChains => this.currentChainResponse != null
                                 && this.currentChainResponse.chains != null
                                 && this.currentChainResponse.chains.Length > 0;

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

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ChainQuest] Auto-loading chains after successful login...");

            this.GetChains(
                onSuccess: chains =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[ChainQuest] Chains auto-loaded: {chains.chains.Length} chains, total: {chains.total}");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[ChainQuest] Auto-load chains failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ChainQuest] Logout successful, clearing chain data...");

            this.ClearLocalChains();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ChainQuest] Chain data cleared successfully");
        }

        /// <summary>
        /// Fetches the list of quest chains from the server.
        /// Supports pagination via limit and offset.
        /// Endpoint: GET /api/v1/games/{gameId}/quests/chains
        /// </summary>
        public void GetChains(
            int? limit = null,
            int? offset = null,
            System.Action<ChainQuestResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ChainQuest] ► Get Chains</b></color>", gameObject);

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

            int actualLimit = limit ?? this.chainLimit;
            int actualOffset = offset ?? this.chainOffset;

            StartCoroutine(this.GetChainsCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetChainsCoroutine(
            int limit,
            int offset,
            System.Action<ChainQuestResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/chains?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ChainQuestResponse chainResponse = JsonUtility.FromJson<ChainQuestResponse>(response);
                        this.currentChainResponse = chainResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ChainQuest] Chains loaded: {chainResponse.chains.Length} chains, total: {chainResponse.total}");

                        this.OnGetChainsSuccess?.Invoke(chainResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ChainQuest] GetChains</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiChainQuest.cs › GetChainsCoroutine");
                        onSuccess?.Invoke(chainResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get chains response error: {e.Message}";
                        this.OnGetChainsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChains</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiChainQuest.cs › GetChainsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetChainsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChains</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiChainQuest.cs › GetChainsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears chain data locally and resets pagination state.
        /// </summary>
        public void ClearChains()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ChainQuest] ► Clear Chains</b></color>", gameObject);

            this.ClearLocalChains();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ChainQuest] Chain data cleared locally");
        }

        private void ClearLocalChains()
        {
            this.currentChainResponse = new ChainQuestResponse
            {
                chains = new ChainQuestData[0],
                limit = this.chainLimit,
                offset = 0,
                total = 0
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached chain with the given id, or null.</summary>
        public ChainQuestData GetChainById(string chainId)
        {
            if (this.currentChainResponse == null || this.currentChainResponse.chains == null)
                return null;

            foreach (ChainQuestData chain in this.currentChainResponse.chains)
            {
                if (chain.id == chainId)
                    return chain;
            }

            return null;
        }

        /// <summary>Returns the locally cached chain with the given chain_key, or null.</summary>
        public ChainQuestData GetChainByKey(string chainKey)
        {
            if (this.currentChainResponse == null || this.currentChainResponse.chains == null)
                return null;

            foreach (ChainQuestData chain in this.currentChainResponse.chains)
            {
                if (chain.chain_key == chainKey)
                    return chain;
            }

            return null;
        }

        /// <summary>Returns all locally cached chains that match the given chain_type.</summary>
        public ChainQuestData[] GetChainsByType(string chainType)
        {
            if (this.currentChainResponse == null || this.currentChainResponse.chains == null)
                return new ChainQuestData[0];

            var result = new System.Collections.Generic.List<ChainQuestData>();

            foreach (ChainQuestData chain in this.currentChainResponse.chains)
            {
                if (chain.chain_type == chainType)
                    result.Add(chain);
            }

            return result.ToArray();
        }

        /// <summary>Returns all locally cached chains that are currently active.</summary>
        public ChainQuestData[] GetActiveChains()
        {
            if (this.currentChainResponse == null || this.currentChainResponse.chains == null)
                return new ChainQuestData[0];

            var result = new System.Collections.Generic.List<ChainQuestData>();

            foreach (ChainQuestData chain in this.currentChainResponse.chains)
            {
                if (chain.is_active)
                    result.Add(chain);
            }

            return result.ToArray();
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetChainLimit(int limit) => this.chainLimit = limit;
        public void SetChainOffset(int offset) => this.chainOffset = offset;

        public int GetChainLimit() => this.chainLimit;
        public int GetChainOffset() => this.chainOffset;

        // ── Runtime members cache (filled by editor or runtime calls) ──────────

        private readonly System.Collections.Generic.Dictionary<string, ChainMembersResponse> runtimeMembersCache
            = new System.Collections.Generic.Dictionary<string, ChainMembersResponse>();

        /// <summary>Returns the cached members response for the given chainId, or null if not loaded.</summary>
        public ChainMembersResponse GetCachedMembers(string chainId)
        {
            if (this.runtimeMembersCache.TryGetValue(chainId, out ChainMembersResponse cached))
                return cached;
            return null;
        }

        /// <summary>Stores a members response into the runtime cache (called after GetChainMembers succeeds).</summary>
        public void CacheMembers(string chainId, ChainMembersResponse response)
        {
            this.runtimeMembersCache[chainId] = response;
        }

        // ── Chain Members ──────────────────────────────────────────────────────

        /// <summary>
        /// Fetches all quests (members) that belong to the given chain.
        /// Endpoint: GET /api/v1/games/{gameId}/quests/chains/{chainId}/members
        /// </summary>
        public void GetChainMembers(
            string chainId,
            System.Action<ChainMembersResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FFFF><b>[ChainQuest] ► Get Chain Members ({chainId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(chainId))
            {
                onError?.Invoke("chainId cannot be empty.");
                return;
            }

            StartCoroutine(this.GetChainMembersCoroutine(chainId, onSuccess, onError));
        }

        private IEnumerator GetChainMembersCoroutine(
            string chainId,
            System.Action<ChainMembersResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/chains/{chainId}/members";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ChainMembersResponse membersResponse = JsonUtility.FromJson<ChainMembersResponse>(response);
                        this.CacheMembers(chainId, membersResponse);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ChainQuest] Chain members loaded: {membersResponse.members?.Length ?? 0} members");

                        this.OnGetChainMembersSuccess?.Invoke(membersResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ChainQuest] GetChainMembers</color> → <b><color=#00FF88>onSuccess</color></b> callback | ChainQuest.cs › GetChainMembersCoroutine");
                        onSuccess?.Invoke(membersResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get chain members response error: {e.Message}";
                        this.OnGetChainMembersFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChainMembers</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ChainQuest.cs › GetChainMembersCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetChainMembersFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChainMembers</color> → <b><color=#FF4444>onError</color></b> callback (network) | ChainQuest.cs › GetChainMembersCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }
        // \u2500\u2500 Chain Tree \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        /// <summary>
        /// Fetches the quest tree of a chain, showing node relationships and status.
        /// Endpoint: GET /api/v1/games/{gameId}/quests/chains/{chainId}/tree
        /// </summary>
        public void GetChainTree(
            string chainId,
            System.Action<ChainQuestTreeResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#AAFFFF><b>[ChainQuest] \u25ba Get Chain Tree ({chainId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(chainId))
            {
                onError?.Invoke("chainId cannot be empty.");
                return;
            }

            StartCoroutine(this.GetChainTreeCoroutine(chainId, onSuccess, onError));
        }

        private IEnumerator GetChainTreeCoroutine(
            string chainId,
            System.Action<ChainQuestTreeResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/chains/{chainId}/tree";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ChainQuestTreeResponse treeResponse = JsonUtility.FromJson<ChainQuestTreeResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ChainQuest] Chain tree loaded: {treeResponse.nodes?.Length ?? 0} root nodes");

                        this.OnGetChainTreeSuccess?.Invoke(treeResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ChainQuest] GetChainTree</color> \u2192 <b><color=#00FF88>onSuccess</color></b> callback | ChainQuest.cs \u203a GetChainTreeCoroutine");
                        onSuccess?.Invoke(treeResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get chain tree response error: {e.Message}";
                        this.OnGetChainTreeFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChainTree</color> \u2192 <b><color=#FF4444>onError</color></b> callback (parse) | ChainQuest.cs \u203a GetChainTreeCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetChainTreeFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ChainQuest] GetChainTree</color> \u2192 <b><color=#FF4444>onError</color></b> callback (network) | ChainQuest.cs \u203a GetChainTreeCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }    }
}
