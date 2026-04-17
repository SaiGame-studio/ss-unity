using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Adds or deducts quantity for an item definition via the v2 inventory API.
    /// Endpoint: PUT /api/v2/games/{game_id}/item-inventories/{item_definition_id}/qty
    ///
    /// Only item definitions with <c>allow_client_update_qty = true</c> are accepted by the server.
    ///
    /// Usage:
    ///  1. Assign PlayerItem and PlayerContainer references in the Inspector.
    ///  2. In Play mode, use the custom editor (ItemAddDeductEditor) to:
    ///     - Click an item (only items with allow_client_update_qty = true are shown) → fills Item Definition ID
    ///     - Click a container → fills Container ID
    ///     - Enter quantity (positive = Add, negative = Deduct) and press the action button.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class ItemAddDeduct : SaiBehaviour
    {
        // Events
        public event Action<string> OnAddDeductSuccess;
        public event Action<string> OnAddDeductFailure;

        [Header("References")]
        [SerializeField] protected PlayerItem playerItem;
        [SerializeField] protected PlayerContainer playerContainer;

        [Header("Input")]
        [SerializeField] protected string itemDefinitionId = "";
        [SerializeField] protected string containerId = "";
        [SerializeField] protected int quantity = 1;

        // Public accessors for the editor
        public string ItemDefinitionId => this.itemDefinitionId;
        public string ContainerId      => this.containerId;
        public int    Quantity         => this.quantity;

        public PlayerItem      PlayerItemRef      => this.playerItem;
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

        public void SetItemDefinitionId(string id) => this.itemDefinitionId = id;
        public void SetContainerId(string id)       => this.containerId = id;
        public void SetQuantity(int qty)            => this.quantity = qty;

        // ── API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a PUT request to add or deduct quantity for the given item definition.
        /// Positive quantity = Add. Negative quantity = Deduct.
        /// The item definition must have <c>allow_client_update_qty = true</c> on the server.
        /// Endpoint: PUT /api/v2/games/{game_id}/item-inventories/{item_definition_id}/qty
        /// Body: { "quantity": &lt;qty&gt; [, "container_id": "..."] }
        /// </summary>
        public void AddDeduct(
            string itemDefinitionId,
            int quantity,
            string containerId = null,
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF88><b>[ItemAddDeduct] ► {(quantity >= 0 ? "Add" : "Deduct")} Item Qty</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(itemDefinitionId))
            {
                onError?.Invoke("item_definition_id must not be empty.");
                return;
            }

            if (quantity == 0)
            {
                onError?.Invoke("quantity must not be 0.");
                return;
            }

            StartCoroutine(this.AddDeductCoroutine(itemDefinitionId, quantity, containerId, onSuccess, onError));
        }

        /// <summary>Convenience overload that uses the serialized field values.</summary>
        public void AddDeduct(
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            this.AddDeduct(
                itemDefinitionId: this.itemDefinitionId,
                quantity:         this.quantity,
                containerId:      string.IsNullOrEmpty(this.containerId) ? null : this.containerId,
                onSuccess:        onSuccess,
                onError:          onError);
        }

        private IEnumerator AddDeductCoroutine(
            string itemDefinitionId,
            int    quantity,
            string containerId,
            System.Action<string> onSuccess,
            System.Action<string> onError)
        {
            string gameId   = SaiServer.Instance.GameId;
            string endpoint = $"/api/v2/games/{gameId}/item-inventories/{itemDefinitionId}/qty";

            string body = string.IsNullOrEmpty(containerId)
                ? $"{{\"quantity\":{quantity}}}"
                : $"{{\"quantity\":{quantity},\"container_id\":\"{containerId}\"}}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemAddDeduct] PUT {endpoint} | body: {body}");

            yield return SaiServer.Instance.PutRequest(endpoint, body,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemAddDeduct] Qty updated successfully. Server: {response}");

                    this.OnAddDeductSuccess?.Invoke(response);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[ItemAddDeduct] AddDeduct</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemAddDeduct.cs › AddDeductCoroutine");

                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    this.OnAddDeductFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemAddDeduct] AddDeduct</color> → <b><color=#FF4444>onError</color></b> callback | ItemAddDeduct.cs › AddDeductCoroutine | {error}");

                    onError?.Invoke(error);
                }
            );
        }
    }
}
