using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class SaiShop : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<ShopResponse> OnGetShopsSuccess;
        public event Action<string> OnGetShopsFailure;
        public event Action<ShopItemsResponse> OnGetShopItemsSuccess;
        public event Action<string> OnGetShopItemsFailure;
        public event Action<PurchaseResponse> OnPurchaseSuccess;
        public event Action<string> OnPurchaseFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;
        [SerializeField] protected bool autoRefreshAfterPurchase = false;

        [Header("Current Shop Data")]
        [SerializeField] protected ShopResponse currentShopResponse;
        [SerializeField] protected int shopLimit = 20;
        [SerializeField] protected int shopOffset = 0;

        [Header("Current Shop Items")]
        [SerializeField] protected ShopItemsResponse currentShopItemsResponse;
        [SerializeField] protected string lastLoadedShopId = "";

        public ShopResponse CurrentShopResponse => this.currentShopResponse;
        public ShopItemsResponse CurrentShopItemsResponse => this.currentShopItemsResponse;
        public bool AutoRefreshAfterPurchase => this.autoRefreshAfterPurchase;
        public bool HasShops => this.currentShopResponse != null
                                && this.currentShopResponse.shops != null
                                && this.currentShopResponse.shops.Length > 0;

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
                Debug.Log("[Shop] Auto-loading shops after successful login...");

            this.GetShops(
                onSuccess: shops =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[Shop] Shops auto-loaded: {shops.shops.Length} shops, total: {shops.total}");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[Shop] Auto-load shops failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Shop] Logout successful, clearing shop data...");

            this.ClearLocalShops();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Shop] Shop data cleared successfully");
        }

        /// <summary>
        /// Fetches the list of shops from the server.
        /// Supports pagination via limit and offset.
        /// Endpoint: GET /api/v1/games/{gameId}/shops
        /// </summary>
        public void GetShops(
            int? limit = null,
            int? offset = null,
            System.Action<ShopResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[Shop] ► Get Shops</b></color>", gameObject);

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

            int actualLimit = limit ?? this.shopLimit;
            int actualOffset = offset ?? this.shopOffset;

            StartCoroutine(this.GetShopsCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetShopsCoroutine(
            int limit,
            int offset,
            System.Action<ShopResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/shops?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ShopResponse shopResponse = JsonUtility.FromJson<ShopResponse>(response);
                        this.currentShopResponse = shopResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Shop] Shops loaded: {shopResponse.shops.Length} shops, total: {shopResponse.total}");

                        this.OnGetShopsSuccess?.Invoke(shopResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Shop] GetShops</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiShop.cs › GetShopsCoroutine");
                        onSuccess?.Invoke(shopResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get shops response error: {e.Message}";
                        this.OnGetShopsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Shop] GetShops</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiShop.cs › GetShopsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetShopsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Shop] GetShops</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiShop.cs › GetShopsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears shop data locally and resets pagination state.
        /// </summary>
        public void ClearShops()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[Shop] ► Clear Shops</b></color>", gameObject);
            this.ClearLocalShops();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Shop] Shop data cleared locally");
        }

        private void ClearLocalShops()
        {
            this.currentShopResponse = new ShopResponse
            {
                shops = new ShopData[0],
                limit = this.shopLimit,
                offset = 0,
                total = 0
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached shop with the given id, or null.</summary>
        public ShopData GetShopById(string shopId)
        {
            if (this.currentShopResponse == null || this.currentShopResponse.shops == null)
                return null;

            foreach (ShopData shop in this.currentShopResponse.shops)
            {
                if (shop.id == shopId)
                    return shop;
            }

            return null;
        }

        /// <summary>Returns the locally cached shop with the given shop_key, or null.</summary>
        public ShopData GetShopByKey(string shopKey)
        {
            if (this.currentShopResponse == null || this.currentShopResponse.shops == null)
                return null;

            foreach (ShopData shop in this.currentShopResponse.shops)
            {
                if (shop.shop_key == shopKey)
                    return shop;
            }

            return null;
        }

        /// <summary>Returns all locally cached shops that match the given shop_type.</summary>
        public ShopData[] GetShopsByType(string shopType)
        {
            if (this.currentShopResponse == null || this.currentShopResponse.shops == null)
                return new ShopData[0];

            var result = new System.Collections.Generic.List<ShopData>();

            foreach (ShopData shop in this.currentShopResponse.shops)
            {
                if (shop.shop_type == shopType)
                    result.Add(shop);
            }

            return result.ToArray();
        }

        /// <summary>Returns all locally cached shops that are currently active.</summary>
        public ShopData[] GetActiveShops()
        {
            if (this.currentShopResponse == null || this.currentShopResponse.shops == null)
                return new ShopData[0];

            var result = new System.Collections.Generic.List<ShopData>();

            foreach (ShopData shop in this.currentShopResponse.shops)
            {
                if (shop.is_active)
                    result.Add(shop);
            }

            return result.ToArray();
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetShopLimit(int limit) => this.shopLimit = limit;
        public void SetShopOffset(int offset) => this.shopOffset = offset;

        public int GetShopLimit() => this.shopLimit;
        public int GetShopOffset() => this.shopOffset;

        // ── Shop Items ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches the items of a specific shop from the server.
        /// Endpoint: GET /api/v1/games/{gameId}/shops/{shopId}/items
        /// </summary>
        public void GetShopItems(
            string shopId,
            System.Action<ShopItemsResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FFFF><b>[Shop] ► Get Shop Items ({shopId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(shopId))
            {
                onError?.Invoke("shopId cannot be empty.");
                return;
            }

            StartCoroutine(this.GetShopItemsCoroutine(shopId, onSuccess, onError));
        }

        private IEnumerator GetShopItemsCoroutine(
            string shopId,
            System.Action<ShopItemsResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/shops/{shopId}/items";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ShopItemsResponse itemsResponse = JsonUtility.FromJson<ShopItemsResponse>(response);
                        this.currentShopItemsResponse = itemsResponse;
                        this.lastLoadedShopId = shopId;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Shop] Shop items loaded: {itemsResponse.items?.Length ?? 0} items");

                        this.OnGetShopItemsSuccess?.Invoke(itemsResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Shop] GetShopItems</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiShop.cs › GetShopItemsCoroutine");
                        onSuccess?.Invoke(itemsResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get shop items response error: {e.Message}";
                        this.OnGetShopItemsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Shop] GetShopItems</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiShop.cs › GetShopItemsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetShopItemsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Shop] GetShopItems</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiShop.cs › GetShopItemsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Purchase ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Purchases an item from a shop.
        /// Endpoint: POST /api/v1/games/{gameId}/shops/{shopId}/purchase
        /// </summary>
        public void PurchaseItem(
            string shopId,
            string shopItemId,
            int quantity,
            string idempotencyKey,
            System.Action<PurchaseResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[Shop] ▶ Purchase Item ({shopItemId})</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(shopId))
            {
                onError?.Invoke("shopId cannot be empty.");
                return;
            }

            if (string.IsNullOrEmpty(shopItemId))
            {
                onError?.Invoke("shopItemId cannot be empty.");
                return;
            }

            StartCoroutine(this.PurchaseItemCoroutine(shopId, shopItemId, quantity, idempotencyKey, onSuccess, onError));
        }

        private IEnumerator PurchaseItemCoroutine(
            string shopId,
            string shopItemId,
            int quantity,
            string idempotencyKey,
            System.Action<PurchaseResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/shops/{shopId}/purchase";

            PurchaseRequest requestBody = new PurchaseRequest
            {
                shop_item_id = shopItemId,
                quantity = quantity,
                idempotency_key = idempotencyKey
            };
            string json = JsonUtility.ToJson(requestBody);

            yield return SaiService.Instance.PostRequest(endpoint, json,
                response =>
                {
                    try
                    {
                        PurchaseResponse purchaseResponse = JsonUtility.FromJson<PurchaseResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        {
                            PurchaseRecord r = purchaseResponse.purchase_record;
                            Debug.Log(
                                $"[Shop] Purchase successful\n" +
                                $"  id:                  {r?.id}\n" +
                                $"  shop_id:             {r?.shop_id}\n" +
                                $"  shop_item_id:        {r?.shop_item_id}\n" +
                                $"  user_id:             {r?.user_id}\n" +
                                $"  game_id:             {r?.game_id}\n" +
                                $"  quantity:            {r?.quantity}\n" +
                                $"  unit_price:          {r?.unit_price}\n" +
                                $"  total_price:         {r?.total_price}\n" +
                                $"  idempotency_key:     {r?.idempotency_key}\n" +
                                $"  currency_item_def:   {r?.currency_item_def_id}\n" +
                                $"  created_at:          {r?.created_at}");
                        }

                        this.OnPurchaseSuccess?.Invoke(purchaseResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Shop] PurchaseItem</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiShop.cs › PurchaseItemCoroutine");
                        onSuccess?.Invoke(purchaseResponse);

                        // Auto-refresh shop items after purchase
                        if (this.autoRefreshAfterPurchase)
                        {
                            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                                Debug.Log($"[Shop] Auto-refreshing items for shop {shopId}...");
                            this.GetShopItems(shopId);
                        }
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse purchase response error: {e.Message}";
                        this.OnPurchaseFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Shop] PurchaseItem</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiShop.cs › PurchaseItemCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnPurchaseFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Shop] PurchaseItem</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiShop.cs › PurchaseItemCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }
    }
}
