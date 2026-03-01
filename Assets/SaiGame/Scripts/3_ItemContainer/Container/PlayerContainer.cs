using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class PlayerContainer : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<ContainerResponse> OnGetContainersSuccess;
        public event Action<string> OnGetContainersFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Container Data")]
        [SerializeField] protected ContainerResponse currentContainers;

        [Header("Query Parameters")]
        [SerializeField] protected int containerLimit = 50;
        [SerializeField] protected int containerOffset = 0;

        public ContainerResponse CurrentContainers => this.currentContainers;
        public bool HasContainers => this.currentContainers != null
                                     && this.currentContainers.containers != null
                                     && this.currentContainers.containers.Length > 0;

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
                Debug.Log("[PlayerContainer] Auto-loading containers after successful login...");

            this.GetContainers(
                onSuccess: containers =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[PlayerContainer] Containers auto-loaded: {containers.containers.Length} containers");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[PlayerContainer] Auto-load containers failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[PlayerContainer] Logout successful, clearing container data...");

            this.ClearLocalContainers();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[PlayerContainer] Container data cleared successfully");
        }

        /// <summary>
        /// Fetches the player's containers from the server with optional pagination.
        /// Endpoint: GET /api/v1/games/{game_id}/containers?limit={limit}&amp;offset={offset}
        /// </summary>
        public void GetContainers(
            int? limit = null,
            int? offset = null,
            System.Action<ContainerResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[PlayerContainer] ► Get Containers</b></color>", gameObject);

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

            int actualLimit = limit ?? this.containerLimit;
            int actualOffset = offset ?? this.containerOffset;

            StartCoroutine(this.GetContainersCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetContainersCoroutine(
            int limit,
            int offset,
            System.Action<ContainerResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/containers?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ContainerResponse containerResponse = JsonUtility.FromJson<ContainerResponse>(response);
                        this.currentContainers = containerResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[PlayerContainer] Containers loaded: {containerResponse.containers.Length} containers");

                        this.OnGetContainersSuccess?.Invoke(containerResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[PlayerContainer] GetContainers</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerContainer.cs › GetContainersCoroutine");
                        onSuccess?.Invoke(containerResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get containers response error: {e.Message}";
                        this.OnGetContainersFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[PlayerContainer] GetContainers</color> → <b><color=#FF4444>onError</color></b> callback (parse) | PlayerContainer.cs › GetContainersCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetContainersFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[PlayerContainer] GetContainers</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerContainer.cs › GetContainersCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears container data locally and resets pagination state.
        /// </summary>
        public void ClearContainers()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[PlayerContainer] ► Clear Containers</b></color>", gameObject);
            this.ClearLocalContainers();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[PlayerContainer] Container data cleared locally");
        }

        private void ClearLocalContainers()
        {
            this.currentContainers = new ContainerResponse
            {
                containers = new ContainerData[0],
                has_more = false,
                limit = this.containerLimit,
                offset = 0
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached container with the given id, or null.</summary>
        public ContainerData GetContainerById(string containerId)
        {
            if (this.currentContainers == null || this.currentContainers.containers == null)
                return null;

            foreach (ContainerData container in this.currentContainers.containers)
            {
                if (container.id == containerId)
                    return container;
            }

            return null;
        }

        /// <summary>Returns all locally cached containers that match the given container_type.</summary>
        public ContainerData[] GetContainersByType(string containerType)
        {
            if (this.currentContainers == null || this.currentContainers.containers == null)
                return new ContainerData[0];

            var result = new System.Collections.Generic.List<ContainerData>();

            foreach (ContainerData container in this.currentContainers.containers)
            {
                if (container.container_type == containerType)
                    result.Add(container);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Filters an item array locally using independent criteria from <see cref="ItemFilterOptions"/>.
        /// Each active criterion is ANDed together. Empty/default fields are skipped.
        /// Logs a coloured summary so developers can trace the call in the Console.
        /// </summary>
        public InventoryItemData[] FilterItems(InventoryItemData[] items, ItemFilterOptions filter)
        {
            if (items == null)   return new InventoryItemData[0];
            if (filter == null || filter.IsEmpty)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log("<color=#00FFDD><b>[PlayerContainer] ► FilterItems</b></color> | no active filters – returning all items | <i>PlayerContainer.cs › FilterItems</i>", gameObject);
                return items;
            }

            var result = new System.Collections.Generic.List<InventoryItemData>();

            foreach (InventoryItemData item in items)
            {
                // ── name search (case-insensitive substring) ───────────────────
                if (!string.IsNullOrEmpty(filter.nameSearch))
                {
                    string itemName = item.definition?.name ?? "";
                    if (itemName.IndexOf(filter.nameSearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                // ── category (exact, case-insensitive) ────────────────────────
                if (!string.IsNullOrEmpty(filter.category))
                {
                    string itemCat = item.definition?.category ?? "";
                    if (!string.Equals(itemCat, filter.category, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // ── rarity (exact, case-insensitive) ──────────────────────────
                if (!string.IsNullOrEmpty(filter.rarity))
                {
                    string itemRarity = item.definition?.rarity ?? "";
                    if (!string.Equals(itemRarity, filter.rarity, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // ── stackable only ─────────────────────────────────────────────
                if (filter.stackableOnly && item.definition != null && !item.definition.is_stackable)
                    continue;

                result.Add(item);
            }

            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
            {
                string nameLabel     = string.IsNullOrEmpty(filter.nameSearch) ? "*"        : $"'{filter.nameSearch}'";
                string catLabel      = string.IsNullOrEmpty(filter.category)   ? "*"        : $"'{filter.category}'";
                string rarityLabel   = string.IsNullOrEmpty(filter.rarity)     ? "*"        : $"'{filter.rarity}'";
                string stackLabel    = filter.stackableOnly                    ? "yes"      : "*";
                Debug.Log(
                    $"<color=#00FFDD><b>[PlayerContainer] ► FilterItems</b></color> | " +
                    $"name={nameLabel}  category={catLabel}  rarity={rarityLabel}  stackable={stackLabel} " +
                    $"→ <b>{result.Count}/{items.Length}</b> items | " +
                    $"<i>PlayerContainer.cs › FilterItems</i>",
                    gameObject);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Fetches all items inside a specific container.
        /// Endpoint: GET /api/v1/containers/{container_id}/items?limit={limit}&amp;offset={offset}
        /// </summary>
        public void GetContainerItems(
            string containerId,
            int limit = 50,
            int offset = 0,
            System.Action<ContainerItemsResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FFFF><b>[PlayerContainer] ► Get Container Items: {containerId}</b></color>", gameObject);

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

            StartCoroutine(this.GetContainerItemsCoroutine(containerId, limit, offset, onSuccess, onError));
        }

        private IEnumerator GetContainerItemsCoroutine(
            string containerId,
            int limit,
            int offset,
            System.Action<ContainerItemsResponse> onSuccess,
            System.Action<string> onError)
        {
            string endpoint = $"/api/v1/containers/{containerId}/items?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ContainerItemsResponse itemsResponse = JsonUtility.FromJson<ContainerItemsResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[PlayerContainer] Container items loaded: {itemsResponse.items?.Length ?? 0} items for container {containerId}");

                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[PlayerContainer] GetContainerItems</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerContainer.cs › GetContainerItemsCoroutine");
                        onSuccess?.Invoke(itemsResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse container items response error: {e.Message}";
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[PlayerContainer] GetContainerItems</color> → <b><color=#FF4444>onError</color></b> callback (parse) | PlayerContainer.cs › GetContainerItemsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[PlayerContainer] GetContainerItems</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerContainer.cs › GetContainerItemsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Opens a gacha pack item and claims the granted rewards.
        /// Endpoint: POST /api/v1/games/{game_id}/gacha/{gacha_pack_id}
        /// </summary>
        /// <param name="gachaPackDefId">The item_definition_id of the gacha pack (used as gacha_pack_id in URL).</param>
        /// <param name="containerId">The container_id where the pack resides (sent in request body).</param>
        public void OpenGachaPack(
            string gachaPackDefId,
            string containerId,
            System.Action<GachaResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[PlayerContainer] ► Open Gacha Pack: {gachaPackDefId}</b></color>", gameObject);

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

            StartCoroutine(this.OpenGachaPackCoroutine(gachaPackDefId, containerId, onSuccess, onError));
        }

        private IEnumerator OpenGachaPackCoroutine(
            string gachaPackDefId,
            string containerId,
            System.Action<GachaResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gacha/{gachaPackDefId}";
            string idempotencyKey = $"{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}";
            string body = $"{{\"idempotency_key\":\"{idempotencyKey}\",\"container_id\":\"{containerId}\"}}";

            yield return SaiService.Instance.PostRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        GachaResponse gachaResponse = JsonUtility.FromJson<GachaResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[PlayerContainer] Gacha pack opened: {gachaResponse.items_granted?.Length ?? 0} items granted (duplicate={gachaResponse.is_duplicate})");

                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#FFD700>[PlayerContainer] OpenGachaPack</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerContainer.cs › OpenGachaPackCoroutine");

                        onSuccess?.Invoke(gachaResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse gacha response error: {e.Message}";
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#FFD700>[PlayerContainer] OpenGachaPack</color> → <b><color=#FF4444>onError</color></b> callback (parse) | PlayerContainer.cs › OpenGachaPackCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#FFD700>[PlayerContainer] OpenGachaPack</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerContainer.cs › OpenGachaPackCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetContainerLimit(int limit) => this.containerLimit = limit;
        public void SetContainerOffset(int offset) => this.containerOffset = offset;

        public int GetContainerLimit() => this.containerLimit;
        public int GetContainerOffset() => this.containerOffset;
    }
}
