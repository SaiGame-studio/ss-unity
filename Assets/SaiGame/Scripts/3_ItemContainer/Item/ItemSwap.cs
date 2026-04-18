using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Swaps two item instances via the v1 inventory API.
    /// Endpoint: POST /api/v1/games/{game_id}/inventory/swap
    ///
    /// Usage:
    ///  1. Assign PlayerItem reference in the Inspector.
    ///  2. In Play mode, use the custom editor (ItemSwapEditor) to:
    ///     - Click an item → fills Item A ID
    ///     - Click another item → fills Item B ID
    ///     - Press Swap.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class ItemSwap : SaiBehaviour
    {
        // Events
        public event Action<string> OnSwapSuccess;
        public event Action<string> OnSwapFailure;

        [Header("References")]
        [SerializeField] protected PlayerItem playerItem;
        [SerializeField] protected PlayerContainer playerContainer;

        [Header("Input")]
        [SerializeField] protected string itemAId = "";
        [SerializeField] protected string itemBId = "";

        // Public accessors for the editor
        public string ItemAId => this.itemAId;
        public string ItemBId => this.itemBId;

        public PlayerItem PlayerItemRef => this.playerItem;
        public PlayerContainer PlayerContainerRef => this.playerContainer;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.AutoFillReferences();
        }

        private void AutoFillReferences()
        {
            if (this.playerItem == null)
                this.playerItem = GetComponentInParent<PlayerItem>(true)
                    ?? FindObjectOfType<PlayerItem>(true);

            if (this.playerContainer == null)
                this.playerContainer = GetComponentInParent<PlayerContainer>(true)
                    ?? FindObjectOfType<PlayerContainer>(true);
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetItemAId(string id) => this.itemAId = id;
        public void SetItemBId(string id) => this.itemBId = id;

        // ── API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a POST request to swap two item instances.
        /// Endpoint: POST /api/v1/games/{game_id}/inventory/swap
        /// Body: { "item_a_id": "...", "item_b_id": "..." }
        /// </summary>
        public void Swap(
            string itemAId,
            string itemBId,
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF88><b>[ItemSwap] ► Swap Items</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(itemAId))
            {
                onError?.Invoke("item_a_id must not be empty.");
                return;
            }

            if (string.IsNullOrEmpty(itemBId))
            {
                onError?.Invoke("item_b_id must not be empty.");
                return;
            }

            if (itemAId == itemBId)
            {
                onError?.Invoke("item_a_id and item_b_id must not be the same.");
                return;
            }

            StartCoroutine(this.SwapCoroutine(itemAId, itemBId, onSuccess, onError));
        }

        /// <summary>Convenience overload that uses the serialized field values.</summary>
        public void Swap(
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            this.Swap(
                itemAId:   this.itemAId,
                itemBId:   this.itemBId,
                onSuccess: onSuccess,
                onError:   onError);
        }

        private IEnumerator SwapCoroutine(
            string itemAId,
            string itemBId,
            System.Action<string> onSuccess,
            System.Action<string> onError)
        {
            string gameId   = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/swap";

            string body = $"{{\"item_a_id\":\"{itemAId}\",\"item_b_id\":\"{itemBId}\"}}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemSwap] POST {endpoint} | body: {body}");

            yield return SaiServer.Instance.PostRequest(endpoint, body,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemSwap] Items swapped successfully. Server: {response}");

                    this.OnSwapSuccess?.Invoke(response);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[ItemSwap] Swap</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemSwap.cs › SwapCoroutine");

                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    this.OnSwapFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemSwap] Swap</color> → <b><color=#FF4444>onError</color></b> callback | ItemSwap.cs › SwapCoroutine | {error}");

                    onError?.Invoke(error);
                }
            );
        }
    }
}
