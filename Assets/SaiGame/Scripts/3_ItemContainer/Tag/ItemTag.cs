using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class ItemTag : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<ItemTagsResponse> OnGetTagsSuccess;
        public event Action<string> OnGetTagsFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Tags Data")]
        [SerializeField] protected ItemTagsResponse currentTags;

        [Header("Query Parameters")]
        [SerializeField] protected int tagLimit = 50;
        [SerializeField] protected int tagOffset = 0;

        public ItemTagsResponse CurrentTags => this.currentTags;
        public bool HasTags => this.currentTags != null
                               && this.currentTags.tags != null
                               && this.currentTags.tags.Length > 0;

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
            if (!this.autoLoadOnLogin) return;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemTag] Auto-loading tags after successful login...");

            this.GetTags(
                tags =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemTag] Tags loaded: {tags.total} total");
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemTag] Auto-load tags failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemTag] Logout successful, clearing tags data...");

            this.ClearTags();
        }

        public void GetTags(System.Action<ItemTagsResponse> onSuccess = null, System.Action<string> onError = null)
        {
            this.GetTags(this.tagLimit, this.tagOffset, onSuccess, onError);
        }

        public void GetTags(int limit, int offset, System.Action<ItemTagsResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#A855F7><b>[ItemTag] ► Get Tags</b></color>", gameObject);

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

            StartCoroutine(this.GetTagsCoroutine(limit, offset, onSuccess, onError));
        }

        private IEnumerator GetTagsCoroutine(int limit, int offset, System.Action<ItemTagsResponse> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/item-tags?limit={limit}&offset={offset}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        ItemTagsResponse parsed = JsonUtility.FromJson<ItemTagsResponse>(response);

                        if (parsed != null)
                        {
                            this.currentTags = parsed;

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                Debug.Log($"[ItemTag] Tags loaded: {parsed.total} total, {parsed.tags?.Length ?? 0} returned");

                            this.OnGetTagsSuccess?.Invoke(this.currentTags);

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.Log($"<color=#66CCFF>[ItemTag] GetTags</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemTag.cs › GetTagsCoroutine | total: {parsed.total}");

                            onSuccess?.Invoke(this.currentTags);
                        }
                        else
                        {
                            string errorMsg = "Failed to parse ItemTagsResponse";
                            this.OnGetTagsFailure?.Invoke(errorMsg);

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetTags</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemTag.cs › GetTagsCoroutine | {errorMsg}");

                            onError?.Invoke(errorMsg);
                        }
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse error: {e.Message}";
                        this.OnGetTagsFailure?.Invoke(errorMsg);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetTags</color> → <b><color=#FF4444>onError</color></b> callback (exception) | ItemTag.cs › GetTagsCoroutine | {errorMsg}");

                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetTagsFailure?.Invoke(error);

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetTags</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemTag.cs › GetTagsCoroutine | {error}");

                    onError?.Invoke(error);
                }
            );
        }

        public void ClearTags()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ItemTag] ► Clear Tags</b></color>", gameObject);

            this.currentTags = null;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemTag] Tags data cleared");
        }

        // Returns a tag by its id from the current cached data
        public ItemTagData GetTagById(string id)
        {
            if (this.currentTags?.tags == null) return null;

            foreach (ItemTagData tag in this.currentTags.tags)
            {
                if (tag.id == id) return tag;
            }

            return null;
        }

        public void GetItemsByTag(string tagKey, System.Action<InventoryResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF88><b>[ItemTag] ► Get Items by Tag [{tagKey}]</b></color>", gameObject);

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

            StartCoroutine(this.GetItemsByTagCoroutine(tagKey, onSuccess, onError));
        }

        private IEnumerator GetItemsByTagCoroutine(string tagKey, System.Action<InventoryResponse> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/item-tags/{tagKey}/items";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        InventoryResponse parsed = JsonUtility.FromJson<InventoryResponse>(response);

                        if (parsed != null)
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                Debug.Log($"[ItemTag] Items for tag [{tagKey}]: {parsed.total} total, {parsed.items?.Length ?? 0} returned");

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.Log($"<color=#66CCFF>[ItemTag] GetItemsByTag [{tagKey}]</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemTag.cs › GetItemsByTagCoroutine | total: {parsed.total}");

                            onSuccess?.Invoke(parsed);
                        }
                        else
                        {
                            string errorMsg = $"Failed to parse InventoryResponse for tag [{tagKey}]";
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetItemsByTag [{tagKey}]</color> → <b><color=#FF4444>onError</color></b> callback (parse) | {errorMsg}");
                            onError?.Invoke(errorMsg);
                        }
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse error for tag [{tagKey}]: {e.Message}";
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetItemsByTag [{tagKey}]</color> → <b><color=#FF4444>onError</color></b> callback (exception) | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemTag] GetItemsByTag [{tagKey}]</color> → <b><color=#FF4444>onError</color></b> callback (network) | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // Returns a tag by its tag_key from the current cached data
        public ItemTagData GetTagByKey(string tagKey)
        {
            if (this.currentTags?.tags == null) return null;

            foreach (ItemTagData tag in this.currentTags.tags)
            {
                if (tag.tag_key == tagKey) return tag;
            }

            return null;
        }
    }
}
