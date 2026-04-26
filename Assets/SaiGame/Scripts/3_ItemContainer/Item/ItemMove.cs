using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Moves an item instance to a target container via the v1 inventory API.
    /// Endpoint: POST /api/v1/games/{game_id}/inventory/move
    ///
    /// Usage:
    ///  1. Assign PlayerItem and PlayerContainer references in the Inspector.
    ///  2. In Play mode, use the custom editor (ItemMoveEditor) to:
    ///     - Click an item → fills Item ID
    ///     - Click a container → fills Target Container ID
    ///     - Enter quantity, grid_x, grid_y and press Move.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class ItemMove : SaiBehaviour
    {
        // Events
        public event Action<string> OnMoveSuccess;
        public event Action<string> OnMoveFailure;

        [Header("References")]
        [SerializeField] protected PlayerItem playerItem;
        [SerializeField] protected PlayerContainer playerContainer;

        [Header("Input")]
        [SerializeField] protected string itemId = "";
        [SerializeField] protected string targetContainerId = "";
        [SerializeField] protected int quantity = 1;
        [SerializeField] protected int gridX = 0;
        [SerializeField] protected int gridY = 0;

        // Public accessors for the editor
        public string ItemId              => this.itemId;
        public string TargetContainerId   => this.targetContainerId;
        public int    Quantity            => this.quantity;
        public int    GridX               => this.gridX;
        public int    GridY               => this.gridY;

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
                    ?? FindFirstObjectByType<PlayerItem>(FindObjectsInactive.Include);

            if (this.playerContainer == null)
                this.playerContainer = GetComponentInParent<PlayerContainer>(true)
                    ?? FindFirstObjectByType<PlayerContainer>(FindObjectsInactive.Include);
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetItemId(string id)              => this.itemId = id;
        public void SetTargetContainerId(string id)   => this.targetContainerId = id;
        public void SetQuantity(int qty)              => this.quantity = qty;
        public void SetGridX(int x)                   => this.gridX = x;
        public void SetGridY(int y)                   => this.gridY = y;

        // ── API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a POST request to move an item instance to a target container.
        /// Endpoint: POST /api/v1/games/{game_id}/inventory/move
        /// Body: { "item_id", "target_container_id", "quantity", "grid_x", "grid_y" }
        /// </summary>
        public void Move(
            string itemId,
            string targetContainerId,
            int quantity,
            int gridX = 0,
            int gridY = 0,
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF88><b>[ItemMove] ► Move Item</b></color>", gameObject);

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
                onError?.Invoke("item_id must not be empty.");
                return;
            }

            if (string.IsNullOrEmpty(targetContainerId))
            {
                onError?.Invoke("target_container_id must not be empty.");
                return;
            }

            if (quantity <= 0)
            {
                onError?.Invoke("quantity must be greater than 0.");
                return;
            }

            StartCoroutine(this.MoveCoroutine(itemId, targetContainerId, quantity, gridX, gridY, onSuccess, onError));
        }

        /// <summary>Convenience overload that uses the serialized field values.</summary>
        public void Move(
            System.Action<string> onSuccess = null,
            System.Action<string> onError   = null)
        {
            this.Move(
                itemId:            this.itemId,
                targetContainerId: this.targetContainerId,
                quantity:          this.quantity,
                gridX:             this.gridX,
                gridY:             this.gridY,
                onSuccess:         onSuccess,
                onError:           onError);
        }

        private IEnumerator MoveCoroutine(
            string itemId,
            string targetContainerId,
            int    quantity,
            int    gridX,
            int    gridY,
            System.Action<string> onSuccess,
            System.Action<string> onError)
        {
            string gameId   = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/move";

            string body = $"{{\"item_id\":\"{itemId}\",\"target_container_id\":\"{targetContainerId}\",\"quantity\":{quantity},\"grid_x\":{gridX},\"grid_y\":{gridY}}}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemMove] POST {endpoint} | body: {body}");

            yield return SaiServer.Instance.PostRequest(endpoint, body,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemMove] Item moved successfully. Server: {response}");

                    this.OnMoveSuccess?.Invoke(response);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[ItemMove] Move</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemMove.cs › MoveCoroutine");

                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    this.OnMoveFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemMove] Move</color> → <b><color=#FF4444>onError</color></b> callback | ItemMove.cs › MoveCoroutine | {error}");

                    onError?.Invoke(error);
                }
            );
        }
    }
}
