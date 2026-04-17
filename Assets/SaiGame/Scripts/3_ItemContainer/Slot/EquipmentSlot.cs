using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class EquipmentSlot : SaiBehaviour
    {
        public event Action<EquipmentSlotsResponse> OnGetSlotsSuccess;
        public event Action<string> OnGetSlotsFailure;
        public event Action<EquippedItemsResponse> OnGetEquippedSuccess;
        public event Action<string> OnGetEquippedFailure;
        public event Action<string> OnEquipSuccess;
        public event Action<string> OnEquipFailure;
        public event Action OnUnequipSuccess;
        public event Action<string> OnUnequipFailure;

        [Header("Auto Load Settings")]
        [FormerlySerializedAs("autoLoadOnLogin")]
        [SerializeField] protected bool autoGetSlots = false;
        [SerializeField] protected bool autoGetEquipped = false;

        [Header("Current Equipment Slot Data")]
        [SerializeField] protected EquipmentSlotsResponse currentSlots;
        [SerializeField] protected EquippedItemsResponse currentEquipped;

        public EquipmentSlotsResponse CurrentSlots => this.currentSlots;
        public EquippedItemsResponse CurrentEquipped => this.currentEquipped;
        public bool HasSlots => this.currentSlots != null
                                && this.currentSlots.slots != null
                                && this.currentSlots.slots.Length > 0;

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
            if (this.autoGetSlots)
            {
                this.GetSlots(
                    onSuccess: _ => { if (this.autoGetEquipped) this.GetEquippedItems(); }
                );
                return;
            }

            if (this.autoGetEquipped) this.GetEquippedItems();
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[EquipmentSlot] Logout successful, clearing slot data...");

            this.ClearSlots();
        }

        public void GetSlots(Action<EquipmentSlotsResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FF88><b>[EquipmentSlot] ► Get Equipment Slots</b></color>", gameObject);

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

            StartCoroutine(this.GetSlotsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetSlotsCoroutine(Action<EquipmentSlotsResponse> onSuccess, Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/equipment-slots";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        EquipmentSlotsResponse slotsResponse = JsonUtility.FromJson<EquipmentSlotsResponse>(response);
                        this.currentSlots = slotsResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[EquipmentSlot] Slots loaded: {slotsResponse.slots?.Length ?? 0} slots (total: {slotsResponse.total})");

                        OnGetSlotsSuccess?.Invoke(slotsResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[EquipmentSlot] GetSlots</color> → <b><color=#00FF88>onSuccess</color></b> callback | EquipmentSlotManager.cs › GetSlotsCoroutine");
                        onSuccess?.Invoke(slotsResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse equipment slots response error: {e.Message}";
                        OnGetSlotsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] GetSlots</color> → <b><color=#FF4444>onError</color></b> callback (parse) | EquipmentSlotManager.cs › GetSlotsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetSlotsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] GetSlots</color> → <b><color=#FF4444>onError</color></b> callback (network) | EquipmentSlotManager.cs › GetSlotsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void EquipItem(string itemId, string slotKey, string slotDataJson = "{}",
            Action<string> onSuccess = null, Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[EquipmentSlot] ► Equip Item '{itemId}' → slot '{slotKey}'</b></color>", gameObject);

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

            StartCoroutine(this.EquipItemCoroutine(itemId, slotKey, slotDataJson, onSuccess, onError));
        }

        private IEnumerator EquipItemCoroutine(string itemId, string slotKey, string slotDataJson,
            Action<string> onSuccess, Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/equip";

            // slot_data is an arbitrary JSON object — build manually to avoid double-escape
            string safeSlotData = string.IsNullOrEmpty(slotDataJson) ? "{}" : slotDataJson;
            string jsonBody = $"{{\"item_id\":\"{itemId}\",\"slot_key\":\"{slotKey}\",\"slot_data\":{safeSlotData}}}";

            yield return SaiServer.Instance.PostRequest(endpoint, jsonBody,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[EquipmentSlot] Item '{itemId}' equipped to slot '{slotKey}'");

                    OnEquipSuccess?.Invoke(response);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[EquipmentSlot] EquipItem</color> → <b><color=#00FF88>onSuccess</color></b> callback | EquipmentSlotManager.cs › EquipItemCoroutine");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    OnEquipFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] EquipItem</color> → <b><color=#FF4444>onError</color></b> callback | EquipmentSlotManager.cs › EquipItemCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void UnequipItem(string itemId, Action onSuccess = null, Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FF9944><b>[EquipmentSlot] ► Unequip Item '{itemId}'</b></color>", gameObject);

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

            StartCoroutine(this.UnequipItemCoroutine(itemId, onSuccess, onError));
        }

        private IEnumerator UnequipItemCoroutine(string itemId, Action onSuccess, Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/unequip";
            string jsonBody = $"{{\"item_id\":\"{itemId}\"}}";

            yield return SaiServer.Instance.PostRequest(endpoint, jsonBody,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[EquipmentSlot] Item '{itemId}' unequipped");

                    OnUnequipSuccess?.Invoke();
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[EquipmentSlot] UnequipItem</color> → <b><color=#00FF88>onSuccess</color></b> callback | EquipmentSlotManager.cs › UnequipItemCoroutine");
                    onSuccess?.Invoke();
                },
                error =>
                {
                    OnUnequipFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] UnequipItem</color> → <b><color=#FF4444>onError</color></b> callback | EquipmentSlotManager.cs › UnequipItemCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void GetEquippedItems(Action<EquippedItemsResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFB347><b>[EquipmentSlot] ► Get Equipped Items</b></color>", gameObject);

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

            StartCoroutine(this.GetEquippedItemsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetEquippedItemsCoroutine(Action<EquippedItemsResponse> onSuccess, Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/inventory/equipped";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        EquippedItemsResponse equippedResponse = JsonUtility.FromJson<EquippedItemsResponse>(response);
                        PopulateSlotDataRaw(response, equippedResponse);
                        this.currentEquipped = equippedResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[EquipmentSlot] Equipped items loaded: {equippedResponse.equipped?.Length ?? 0} item(s)");

                        OnGetEquippedSuccess?.Invoke(equippedResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[EquipmentSlot] GetEquippedItems</color> → <b><color=#00FF88>onSuccess</color></b> callback | EquipmentSlotManager.cs › GetEquippedItemsCoroutine");
                        onSuccess?.Invoke(equippedResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse equipped items response error: {e.Message}";
                        OnGetEquippedFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] GetEquippedItems</color> → <b><color=#FF4444>onError</color></b> callback (parse) | EquipmentSlotManager.cs › GetEquippedItemsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetEquippedFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[EquipmentSlot] GetEquippedItems</color> → <b><color=#FF4444>onError</color></b> callback (network) | EquipmentSlotManager.cs › GetEquippedItemsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClearSlots()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[EquipmentSlot] ► Clear Slots</b></color>", gameObject);

            this.currentSlots = null;
            this.currentEquipped = null;
        }

        // Extracts the raw JSON string for each "slot_data" object in the response
        // and stores it in slot_data_raw, since JsonUtility cannot deserialize
        // arbitrary JSON objects into string fields.
        private static void PopulateSlotDataRaw(string rawJson, EquippedItemsResponse response)
        {
            if (response?.equipped == null) return;

            int searchFrom = 0;
            foreach (EquippedItemData item in response.equipped)
            {
                int keyIndex = rawJson.IndexOf("\"slot_data\":", searchFrom, System.StringComparison.Ordinal);
                if (keyIndex < 0) break;

                int valueStart = keyIndex + 12;
                while (valueStart < rawJson.Length && rawJson[valueStart] == ' ') valueStart++;

                if (valueStart >= rawJson.Length) break;

                if (rawJson[valueStart] == '{')
                {
                    item.slot_data_raw = ExtractJsonObject(rawJson, valueStart, out int endIndex);
                    searchFrom = endIndex;
                }
                else
                {
                    item.slot_data_raw = "{}";
                    searchFrom = valueStart + 1;
                }
            }
        }

        private static string ExtractJsonObject(string json, int start, out int endIndex)
        {
            int depth = 0;
            int i = start;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"')
                {
                    i++;
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\') i++;
                        i++;
                    }
                }
                else if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) { endIndex = i + 1; return json.Substring(start, i - start + 1); } }
                i++;
            }
            endIndex = json.Length;
            return json.Substring(start);
        }
    }
}
