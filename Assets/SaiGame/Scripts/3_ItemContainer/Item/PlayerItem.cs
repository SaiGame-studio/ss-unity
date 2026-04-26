using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class PlayerItem : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<InventoryResponse> OnGetItemsSuccess;
        public event Action<string> OnGetItemsFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;
        [SerializeField] protected string autoLoadCategory = "";

        [Header("Current Inventory Data")]
        [SerializeField] protected InventoryResponse currentInventory;

        [Header("Query Parameters")]
        [SerializeField] protected int itemLimit = 50;
        [SerializeField] protected int itemOffset = 0;
        [SerializeField] protected string categoryFilter = "";

        public InventoryResponse CurrentInventory => this.currentInventory;
        public bool HasItems => this.currentInventory != null
                                && this.currentInventory.items != null
                                && this.currentInventory.items.Length > 0;

        // Category cache
        public event Action<string[]> OnCategoriesLoaded;
        public static string[] RuntimeCategoriesCache => runtimeCategoriesCache;

        private const string PREF_CATEGORIES = "SaiGame_ItemCategories";
        private const string PREF_CATEGORIES_TIME = "SaiGame_ItemCategoriesTime";
        private const long CACHE_DURATION_SECONDS = 86400L; // 24 hours
        private static string[] runtimeCategoriesCache = null;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
            this.InitializeCategories();
        }

        /// <summary>
        /// Called once at game startup. Loads categories from PlayerPrefs cache if still valid (24 h),
        /// otherwise fetches from the server and stores the result in cache.
        /// </summary>
        private void InitializeCategories()
        {
            string[] cached = LoadCategoriesFromPrefs();
            if (cached != null)
            {
                runtimeCategoriesCache = cached;
                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.Log($"[ItemContainer] Categories loaded from cache ({cached.Length} items, still valid)");
                return;
            }

            // Cache missing or expired — fetch in background
            StartCoroutine(this.FetchAndCacheCategoriesCoroutine());
        }

        private IEnumerator FetchAndCacheCategoriesCoroutine()
        {
            // Wait one frame so that SaiServer fully initialises
            yield return null;

            if (SaiServer.Instance == null)
            {
                Debug.LogWarning("[ItemContainer] Cannot fetch categories: SaiServer not found");
                yield break;
            }

            if (!SaiServer.Instance.IsAuthenticated)
                yield break;

            yield return StartCoroutine(this.GetItemCategoriesCoroutine(
                categories =>
                {
                    SaveCategoriesToPrefs(categories);
                    runtimeCategoriesCache = categories;

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemContainer] Categories fetched and cached ({categories.Length} items)");

                    this.OnCategoriesLoaded?.Invoke(categories);
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemContainer] Failed to fetch categories on startup: {error}");
                }
            ));
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

            // Fetch categories now that we're authenticated, if cache was missing/expired at startup
            if (runtimeCategoriesCache == null)
                StartCoroutine(this.FetchAndCacheCategoriesCoroutine());

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemContainer] Auto-loading inventory after successful login...");

            this.GetItems(
                category: this.autoLoadCategory,
                onSuccess: inventory =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemContainer] Inventory auto-loaded: {inventory.items.Length} items, total: {inventory.total}");
                },
                onError: error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemContainer] Auto-load inventory failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemContainer] Logout successful, clearing inventory data...");

            this.ClearLocalInventory();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemContainer] Inventory data cleared successfully");
        }

        /// <summary>
        /// Fetches the player's inventory items from the server.
        /// Supports optional category filtering and pagination.
        /// </summary>
        public void GetItems(
            int? limit = null,
            int? offset = null,
            string category = null,
            System.Action<InventoryResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemContainer] ► Get Items</b></color>", gameObject);

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

            int actualLimit = limit ?? this.itemLimit;
            int actualOffset = offset ?? this.itemOffset;
            string actualCategory = category ?? this.categoryFilter;

            StartCoroutine(this.GetItemsCoroutine(actualLimit, actualOffset, actualCategory, onSuccess, onError));
        }

        private IEnumerator GetItemsCoroutine(
            int limit,
            int offset,
            string category,
            System.Action<InventoryResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory?limit={limit}&offset={offset}&include_metadata=true";

            if (!string.IsNullOrEmpty(category))
                endpoint += $"&category={category}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        // Pre-process: convert JSON object fields (base_stats, public_properties,
                        // private_properties) from object literals to escaped JSON strings so that
                        // Unity's JsonUtility can map them into C# string fields without skipping items.
                        string sanitized = InventoryJsonHelper.StringifyObjectFields(response);

                        InventoryResponse inventoryResponse = JsonUtility.FromJson<InventoryResponse>(sanitized);
                        this.currentInventory = inventoryResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[ItemContainer] Inventory loaded: {inventoryResponse.items.Length} items, total: {inventoryResponse.total}");

                        this.OnGetItemsSuccess?.Invoke(inventoryResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemContainer] GetItems</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemContainer.cs › GetItemsCoroutine");
                        onSuccess?.Invoke(inventoryResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get items response error: {e.Message}";
                        this.OnGetItemsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemContainer] GetItems</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemContainer.cs › GetItemsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetItemsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemContainer] GetItems</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemContainer.cs › GetItemsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Update Item Public Properties (client_writable) ────────────────────

        /// <summary>
        /// Sends a PATCH request to merge <paramref name="propertiesJson"/> into the item's
        /// public_properties. The item definition must have client_writable = true.
        /// Endpoint: PATCH /api/v1/games/{game_id}/inventory-items/{item_id}
        /// </summary>
        /// <param name="itemId">The inventory item id to update.</param>
        /// <param name="propertiesJson">
        /// A valid JSON object string, e.g. {"skin":"golden","effect":"glow"}.
        /// Values are merged (not replaced) server-side via JSONB || operator.
        /// Max 50 total keys (including nested).
        /// </param>
        public void UpdateItemProperties(
            string itemId,
            string propertiesJson,
            System.Action<InventoryItemData> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemContainer] ► Update Item Properties</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(itemId))
            {
                onError?.Invoke("itemId must not be empty.");
                return;
            }

            StartCoroutine(this.UpdateItemPropertiesCoroutine(itemId, propertiesJson, onSuccess, onError));
        }

        private IEnumerator UpdateItemPropertiesCoroutine(
            string itemId,
            string propertiesJson,
            System.Action<InventoryItemData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory-items/{itemId}";

            // version is required by the schema but ignored server-side for optimistic locking
            string body = $"{{\"version\":0,\"properties\":{propertiesJson}}}";

            yield return SaiServer.Instance.PatchRequest(endpoint, body,
                response =>
                {
                    // Server returns {"message":"properties updated successfully"} — not a full item object.
                    // Find the cached item, patch public_properties locally, then invoke onSuccess.
                    InventoryItemData cachedItem = null;

                    if (this.currentInventory?.items != null)
                    {
                        for (int i = 0; i < this.currentInventory.items.Length; i++)
                        {
                            if (this.currentInventory.items[i].id == itemId)
                            {
                                cachedItem = this.currentInventory.items[i];
                                // Optimistically update public_properties with the sent JSON
                                cachedItem.public_properties = propertiesJson;
                                break;
                            }
                        }
                    }

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemContainer] Item {itemId} public_properties updated successfully. Server: {response}");

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[ItemContainer] UpdateItemProperties</color> → <b><color=#00FF88>onSuccess</color></b> callback | PlayerItem.cs › UpdateItemPropertiesCoroutine");

                    onSuccess?.Invoke(cachedItem);
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemContainer] UpdateItemProperties</color> → <b><color=#FF4444>onError</color></b> callback (network) | PlayerItem.cs › UpdateItemPropertiesCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears inventory data both locally and resets pagination state.
        /// </summary>
        public void ClearInventory()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ItemContainer] ► Clear Inventory</b></color>", gameObject);
            this.ClearLocalInventory();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemContainer] Inventory data cleared locally");
        }

        private void ClearLocalInventory()
        {
            this.currentInventory = new InventoryResponse
            {
                items = new InventoryItemData[0],
                limit = this.itemLimit,
                offset = 0,
                total = 0
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached item with the given id, or null.</summary>
        public InventoryItemData GetItemById(string itemId)
        {
            if (this.currentInventory == null || this.currentInventory.items == null)
                return null;

            foreach (InventoryItemData item in this.currentInventory.items)
            {
                if (item.id == itemId)
                    return item;
            }

            return null;
        }

        /// <summary>Returns all locally cached items that match the given category.</summary>
        public InventoryItemData[] GetItemsByCategory(string category)
        {
            if (this.currentInventory == null || this.currentInventory.items == null)
                return new InventoryItemData[0];

            var result = new System.Collections.Generic.List<InventoryItemData>();

            foreach (InventoryItemData item in this.currentInventory.items)
            {
                if (item.definition != null && item.definition.category == category)
                    result.Add(item);
            }

            return result.ToArray();
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        /// <summary>
        /// Returns categories. Serves from the 24-hour PlayerPrefs cache when valid.
        /// Pass forceRefresh = true to bypass cache and fetch from the server.
        /// Endpoint: GET /api/v1/items/categories
        /// </summary>
        public void GetItemCategories(
            bool forceRefresh = false,
            System.Action<string[]> onSuccess = null,
            System.Action<string> onError = null)
        {
            // Serve from in-memory runtime cache unless a force-refresh is requested
            if (!forceRefresh && runtimeCategoriesCache != null)
            {
                onSuccess?.Invoke(runtimeCategoriesCache);
                return;
            }

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            StartCoroutine(this.GetItemCategoriesCoroutine(
                categories =>
                {
                    SaveCategoriesToPrefs(categories);
                    runtimeCategoriesCache = categories;
                    this.OnCategoriesLoaded?.Invoke(categories);
                    onSuccess?.Invoke(categories);
                },
                onError
            ));
        }

        private IEnumerator GetItemCategoriesCoroutine(
            System.Action<string[]> onSuccess,
            System.Action<string> onError)
        {
            const string endpoint = "/api/v1/items/categories";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ItemCategoriesResponse result = JsonUtility.FromJson<ItemCategoriesResponse>(response);
                        onSuccess?.Invoke(result.categories);
                    }
                    catch (System.Exception e)
                    {
                        onError?.Invoke($"Parse categories response error: {e.Message}");
                    }
                },
                error => onError?.Invoke(error)
            );
        }

        // ── PlayerPrefs cache helpers (static — safe to call from the Editor) ────

        /// <summary>Loads categories from PlayerPrefs. Returns null when absent or older than 24 h.</summary>
        public static string[] LoadCategoriesFromPrefs()
        {
            if (!PlayerPrefs.HasKey(PREF_CATEGORIES_TIME) || !PlayerPrefs.HasKey(PREF_CATEGORIES))
                return null;

            long savedTime;
            if (!long.TryParse(PlayerPrefs.GetString(PREF_CATEGORIES_TIME, "0"), out savedTime))
                return null;

            long nowTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            if (nowTime - savedTime > CACHE_DURATION_SECONDS)
                return null; // expired

            string stored = PlayerPrefs.GetString(PREF_CATEGORIES, "");
            if (string.IsNullOrEmpty(stored))
                return null;

            return stored.Split('|');
        }

        /// <summary>Writes categories and current timestamp to PlayerPrefs.</summary>
        public static void SaveCategoriesToPrefs(string[] categories)
        {
            if (categories == null || categories.Length == 0) return;

            long nowTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            PlayerPrefs.SetString(PREF_CATEGORIES_TIME, nowTime.ToString());
            PlayerPrefs.SetString(PREF_CATEGORIES, string.Join("|", categories));
            PlayerPrefs.Save();
        }

        /// <summary>Removes the category cache from PlayerPrefs and clears the in-memory cache.</summary>
        public static void ClearCategoriesCache()
        {
            runtimeCategoriesCache = null;
            PlayerPrefs.DeleteKey(PREF_CATEGORIES);
            PlayerPrefs.DeleteKey(PREF_CATEGORIES_TIME);
            PlayerPrefs.Save();
        }

        public void SetItemLimit(int limit) => this.itemLimit = limit;
        public void SetItemOffset(int offset) => this.itemOffset = offset;
        public void SetCategoryFilter(string category) => this.categoryFilter = category;

        public int GetItemLimit() => this.itemLimit;
        public int GetItemOffset() => this.itemOffset;
        public string GetCategoryFilter() => this.categoryFilter;
    }
}
