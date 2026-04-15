using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class ItemCrafting : SaiBehaviour
    {
        // Events
        public event Action<CraftingResponse> OnCraftSuccess;
        public event Action<string> OnCraftFailure;

        public event Action<CraftingHistoryResponse> OnGetHistorySuccess;
        public event Action<string> OnGetHistoryFailure;

        public event Action<RecipeDetail> OnGetRecipeByKeySuccess;
        public event Action<string> OnGetRecipeByKeyFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Crafting History")]
        [SerializeField] protected CraftingHistoryResponse currentHistory;

        [Header("Query Parameters")]
        [SerializeField] protected int historyPage = 1;
        [SerializeField] protected int historyPageSize = 20;

        public CraftingHistoryResponse CurrentHistory => this.currentHistory;
        
        public bool HasHistory => this.currentHistory != null
                                     && this.currentHistory.transactions != null
                                     && this.currentHistory.transactions.Length > 0;

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
                Debug.Log("[ItemCrafting] Auto-loading crafting history after login...");

            this.GetCraftingHistory(
                page: this.historyPage,
                pageSize: this.historyPageSize,
                onSuccess: history =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemCrafting] History auto-loaded: {history.transactions.Length} transactions");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemCrafting] Auto-load history failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemCrafting] Logout successful, clearing history data...");

            this.ClearHistory();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemCrafting] History data cleared successfully");
        }

        /// <summary>
        /// Crafts an item using the specified recipe.
        /// Endpoint: POST /api/v1/games/{game_id}/crafting/craft
        /// </summary>
        public void Craft(
            string recipeId,
            string idempotencyKey = null,
            System.Action<CraftingResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[ItemCrafting] ► Craft: {recipeId}</b></color>", gameObject);

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

            StartCoroutine(this.CraftCoroutine(recipeId, idempotencyKey, onSuccess, onError));
        }

        /// <summary>
        /// Crafts an item using the recipe key (code) instead of id.
        /// Endpoint: POST /api/v1/games/{game_id}/crafting/craft
        /// </summary>
        public void CraftByKey(
            string recipeKey,
            string idempotencyKey = null,
            System.Action<CraftingResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[ItemCrafting] ► CraftByKey: {recipeKey}</b></color>", gameObject);

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

            StartCoroutine(this.CraftByKeyCoroutine(recipeKey, idempotencyKey, onSuccess, onError));
        }

        private IEnumerator CraftCoroutine(
            string recipeId,
            string idempotencyKey,
            System.Action<CraftingResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/crafting/craft";

            // Use provided key or generate a new one
            if (string.IsNullOrEmpty(idempotencyKey))
                idempotencyKey = Guid.NewGuid().ToString();

            string body = JsonUtility.ToJson(new CraftRequest
            {
                recipe_id = recipeId,
                idempotency_key = idempotencyKey
            });

            yield return this.SendCraftRequest(endpoint, body, onSuccess, onError);
        }

        private IEnumerator CraftByKeyCoroutine(
            string recipeKey,
            string idempotencyKey,
            System.Action<CraftingResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/crafting/craft";

            if (string.IsNullOrEmpty(idempotencyKey))
                idempotencyKey = Guid.NewGuid().ToString();

            string body = JsonUtility.ToJson(new CraftByKeyRequest
            {
                recipe_key = recipeKey,
                idempotency_key = idempotencyKey
            });

            yield return this.SendCraftRequest(endpoint, body, onSuccess, onError);
        }

        private IEnumerator SendCraftRequest(
            string endpoint,
            string body,
            System.Action<CraftingResponse> onSuccess,
            System.Action<string> onError)
        {
            yield return SaiService.Instance.PostRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        var craftingResponse = JsonUtility.FromJson<CraftingResponse>(response);
                        
                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ItemCrafting] Crafted successfully. Tx Id: {craftingResponse.transaction_id}");

                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#FFD700>[ItemCrafting] Craft</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemCrafting.cs › CraftCoroutine");

                        this.OnCraftSuccess?.Invoke(craftingResponse);
                        onSuccess?.Invoke(craftingResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse craft response error: {e.Message}";
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#FFD700>[ItemCrafting] Craft</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemCrafting.cs › CraftCoroutine | {errorMsg}");
                        this.OnCraftFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#FFD700>[ItemCrafting] Craft</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemCrafting.cs › CraftCoroutine | {error}");
                    this.OnCraftFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Fetches crafting history for the player.
        /// Endpoint: GET /api/v1/games/{game_id}/crafting/history?page={page}&page_size={page_size}
        /// </summary>
        public void GetCraftingHistory(
            int? page = null,
            int? pageSize = null,
            string recipeId = null,
            string status = null, // e.g., "success", "failed"
            System.Action<CraftingHistoryResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemCrafting] ► Get Crafting History</b></color>", gameObject);

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

            int actualPage = page ?? this.historyPage;
            int actualPageSize = pageSize ?? this.historyPageSize;

            StartCoroutine(this.GetCraftingHistoryCoroutine(actualPage, actualPageSize, recipeId, status, onSuccess, onError));
        }

        private IEnumerator GetCraftingHistoryCoroutine(
            int page,
            int pageSize,
            string recipeId,
            string status,
            System.Action<CraftingHistoryResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/crafting/history?page={page}&page_size={pageSize}";
            
            if (!string.IsNullOrEmpty(recipeId))
                endpoint += $"&recipe_id={recipeId}";
            if (!string.IsNullOrEmpty(status))
                endpoint += $"&status={status}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        var historyResponse = JsonUtility.FromJson<CraftingHistoryResponse>(response);
                        this.currentHistory = historyResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ItemCrafting] History loaded: {historyResponse.transactions?.Length ?? 0} records");

                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemCrafting] GetCraftingHistory</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemCrafting.cs › GetCraftingHistoryCoroutine");

                        this.OnGetHistorySuccess?.Invoke(historyResponse);
                        onSuccess?.Invoke(historyResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse history response error: {e.Message}";
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemCrafting] GetCraftingHistory</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemCrafting.cs › GetCraftingHistoryCoroutine | {errorMsg}");
                        this.OnGetHistoryFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemCrafting] GetCraftingHistory</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemCrafting.cs › GetCraftingHistoryCoroutine | {error}");
                    this.OnGetHistoryFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Fetches a recipe definition by its recipe_key.
        /// Endpoint: GET /api/v1/games/{game_id}/crafting/recipes-by-key/{recipe_key}
        /// </summary>
        public void GetRecipeByKey(
            string recipeKey,
            System.Action<RecipeDetail> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#66CCFF><b>[ItemCrafting] ► Get Recipe By Key: {recipeKey}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(recipeKey))
            {
                onError?.Invoke("Recipe key cannot be empty.");
                return;
            }

            StartCoroutine(this.GetRecipeByKeyCoroutine(recipeKey, onSuccess, onError));
        }

        private IEnumerator GetRecipeByKeyCoroutine(
            string recipeKey,
            System.Action<RecipeDetail> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/crafting/recipes-by-key/{recipeKey}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        var recipe = JsonUtility.FromJson<RecipeDetail>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ItemCrafting] Recipe loaded: {recipe.name} ({recipe.recipe_key})");

                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemCrafting] GetRecipeByKey</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemCrafting.cs › GetRecipeByKeyCoroutine");

                        this.OnGetRecipeByKeySuccess?.Invoke(recipe);
                        onSuccess?.Invoke(recipe);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse recipe response error: {e.Message}";
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemCrafting] GetRecipeByKey</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemCrafting.cs › GetRecipeByKeyCoroutine | {errorMsg}");
                        this.OnGetRecipeByKeyFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemCrafting] GetRecipeByKey</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemCrafting.cs › GetRecipeByKeyCoroutine | {error}");
                    this.OnGetRecipeByKeyFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void ClearHistory()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ItemCrafting] ► Clear History</b></color>", gameObject);
            this.ClearLocalHistory();
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemCrafting] History data cleared locally");
        }

        private void ClearLocalHistory()
        {
            this.currentHistory = new CraftingHistoryResponse
            {
                transactions = new CraftingHistoryTransaction[0],
                page = this.historyPage,
                page_size = this.historyPageSize,
                total = 0
            };
        }

        public void SetHistoryPage(int page) => this.historyPage = page;
        public void SetHistoryPageSize(int size) => this.historyPageSize = size;
        public int GetHistoryPage() => this.historyPage;
        public int GetHistoryPageSize() => this.historyPageSize;
    }
}
