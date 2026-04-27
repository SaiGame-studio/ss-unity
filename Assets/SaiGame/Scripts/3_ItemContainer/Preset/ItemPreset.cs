using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class ItemPreset : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<PresetData> OnCreatePresetSuccess;
        public event Action<string> OnCreatePresetFailure;
        public event Action<PresetResponse> OnGetPresetsSuccess;
        public event Action<string> OnGetPresetsFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Preset Data")]
        [SerializeField] protected PresetResponse currentPresets;

        public enum CreatePresetMode { CodeName, DefinitionId }

        [Header("Create Preset Input")]
        [SerializeField] protected CreatePresetMode createMode = CreatePresetMode.CodeName;
        [SerializeField] protected string codeName = "";
        [SerializeField] protected string definitionId = "";
        [SerializeField] protected string presetName = "";

        public PresetResponse CurrentPresets => this.currentPresets;
        public bool HasPresets => this.currentPresets != null
                                  && this.currentPresets.containers != null
                                  && this.currentPresets.containers.Length > 0;

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
                Debug.Log("[ItemPreset] Auto-loading presets after successful login...");

            this.GetPresets(
                onSuccess: presets =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemPreset] Presets auto-loaded: {presets.containers.Length} presets");
                },
                onError: error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemPreset] Auto-load presets failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemPreset] Logout successful, clearing preset data...");

            this.ClearLocalPresets();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemPreset] Preset data cleared successfully");
        }

        /// <summary>
        /// Creates a new preset from the given definition.
        /// Endpoint: POST /api/v1/games/{game_id}/presets
        /// </summary>
        public void CreatePresetByCodeName(
            string codeName,
            string name,
            System.Action<PresetData> onSuccess = null,
            System.Action<string> onError = null)
        {
            this.CreatePresetInternal(CreatePresetMode.CodeName, codeName, name, onSuccess, onError);
        }

        public void CreatePresetByDefinitionId(
            string definitionId,
            string name,
            System.Action<PresetData> onSuccess = null,
            System.Action<string> onError = null)
        {
            this.CreatePresetInternal(CreatePresetMode.DefinitionId, definitionId, name, onSuccess, onError);
        }

        private void CreatePresetInternal(
            CreatePresetMode mode,
            string identifier,
            string name,
            System.Action<PresetData> onSuccess,
            System.Action<string> onError)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFCC><b>[ItemPreset] ► Create Preset</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(identifier))
            {
                string fieldName = mode == CreatePresetMode.CodeName ? "code_name" : "definition_id";
                onError?.Invoke($"{fieldName} cannot be empty.");
                return;
            }

            StartCoroutine(this.CreatePresetCoroutine(mode, identifier, name, onSuccess, onError));
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private IEnumerator CreatePresetCoroutine(
            CreatePresetMode mode,
            string identifier,
            string name,
            System.Action<PresetData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/presets";

            string idKey = mode == CreatePresetMode.CodeName ? "code_name" : "definition_id";
            string escapedIdentifier = EscapeJsonString(identifier);
            string jsonData = string.IsNullOrEmpty(name)
                ? $"{{\"{idKey}\":\"{escapedIdentifier}\"}}"
                : $"{{\"{idKey}\":\"{escapedIdentifier}\",\"name\":\"{EscapeJsonString(name)}\"}}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemPreset] Creating preset with {idKey}: {identifier} | name: {name}");

            yield return SaiServer.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        PresetData preset = JsonUtility.FromJson<PresetData>(response);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[ItemPreset] Preset created: {preset.id} | type: {preset.preset_type} | max_slots: {preset.max_slots}");

                        this.OnCreatePresetSuccess?.Invoke(preset);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemPreset] CreatePreset</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › CreatePresetCoroutine");
                        onSuccess?.Invoke(preset);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse create preset response error: {e.Message}";
                        this.OnCreatePresetFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemPreset] CreatePreset</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemPreset.cs › CreatePresetCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnCreatePresetFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] CreatePreset</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemPreset.cs › CreatePresetCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Adds an item to a preset slot.
        /// Endpoint: PUT /api/v1/games/{game_id}/presets/{preset_id}/slots/0
        /// </summary>
        public void AddItemToPreset(
            string presetId,
            int slotIndex,
            string inventoryItemId,
            System.Action<PresetData> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFCC><b>[ItemPreset] ► Add Item To Preset</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(presetId) || string.IsNullOrEmpty(inventoryItemId))
            {
                onError?.Invoke("preset_id and inventory_item_id cannot be empty.");
                return;
            }

            StartCoroutine(this.AddItemToPresetCoroutine(presetId, slotIndex, inventoryItemId, onSuccess, onError));
        }

        private IEnumerator AddItemToPresetCoroutine(
            string presetId,
            int slotIndex,
            string inventoryItemId,
            System.Action<PresetData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string putEndpoint = $"/api/v1/games/{gameId}/presets/{presetId}/slots/{slotIndex}";
            string jsonData = $"{{\"inventory_item_id\":\"{inventoryItemId}\"}}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemPreset] Adding item {inventoryItemId} to preset {presetId} at slot {slotIndex}");

            yield return SaiServer.Instance.PutRequest(putEndpoint, jsonData,
                putResponse =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemPreset] PUT successful. Fetching updated preset...");

                    string getEndpoint = $"/api/v1/games/{gameId}/presets/{presetId}";
                    StartCoroutine(SaiServer.Instance.GetRequest(getEndpoint,
                        getResponse =>
                        {
                            try
                            {
                                PresetDetailResponse detail = JsonUtility.FromJson<PresetDetailResponse>(getResponse);
                                if (detail != null && detail.container != null)
                                {
                                    detail.container.slots = detail.slots;

                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                        Debug.Log($"[ItemPreset] Preset data updated: {detail.container.id}");

                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                        Debug.Log("<color=#66CCFF>[ItemPreset] AddItemToPreset</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › AddItemToPresetCoroutine");
                                    
                                    onSuccess?.Invoke(detail.container);
                                }
                                else
                                {
                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                        Debug.LogWarning("[ItemPreset] AddItemToPreset → Error: Received empty or invalid PresetDetailResponse");
                                    onError?.Invoke("Received empty or invalid PresetDetailResponse");
                                }
                            }
                            catch (System.Exception e)
                            {
                                string errorMsg = $"Parse get preset detail response error: {e.Message}";
                                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                    Debug.LogWarning($"<color=#66CCFF>[ItemPreset] AddItemToPreset</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemPreset.cs › AddItemToPresetCoroutine | {errorMsg}");
                                onError?.Invoke(errorMsg);
                            }
                        },
                        getError =>
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.LogWarning($"<color=#66CCFF>[ItemPreset] AddItemToPreset</color> → <b><color=#FF4444>onError</color></b> callback (GET network) | ItemPreset.cs › AddItemToPresetCoroutine | {getError}");
                            onError?.Invoke(getError);
                        }
                    ));
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] AddItemToPreset</color> → <b><color=#FF4444>onError</color></b> callback (PUT network) | ItemPreset.cs › AddItemToPresetCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Removes an item from a preset slot.
        /// Endpoint: DELETE /api/v1/games/{game_id}/presets/{container_id}/slots/{slot_index}
        /// </summary>
        public void RemoveItemFromPreset(
            string presetId,
            int slotIndex,
            System.Action<PresetData> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FF6666><b>[ItemPreset] ► Remove Item From Preset slot {slotIndex}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(presetId))
            {
                onError?.Invoke("preset_id cannot be empty.");
                return;
            }

            StartCoroutine(this.RemoveItemFromPresetCoroutine(presetId, slotIndex, onSuccess, onError));
        }

        private IEnumerator RemoveItemFromPresetCoroutine(
            string presetId,
            int slotIndex,
            System.Action<PresetData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string deleteEndpoint = $"/api/v1/games/{gameId}/presets/{presetId}/slots/{slotIndex}";

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemPreset] Removing item from preset {presetId} at slot {slotIndex}");

            yield return SaiServer.Instance.DeleteRequest(deleteEndpoint,
                deleteResponse =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemPreset] DELETE slot successful. Fetching updated preset...");

                    string getEndpoint = $"/api/v1/games/{gameId}/presets/{presetId}";
                    StartCoroutine(SaiServer.Instance.GetRequest(getEndpoint,
                        getResponse =>
                        {
                            try
                            {
                                PresetDetailResponse detail = JsonUtility.FromJson<PresetDetailResponse>(getResponse);
                                if (detail != null && detail.container != null)
                                {
                                    detail.container.slots = detail.slots;

                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                        Debug.Log($"[ItemPreset] Preset data updated after remove: {detail.container.id}");

                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                        Debug.Log("<color=#66CCFF>[ItemPreset] RemoveItemFromPreset</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › RemoveItemFromPresetCoroutine");

                                    onSuccess?.Invoke(detail.container);
                                }
                                else
                                {
                                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                        Debug.LogWarning("[ItemPreset] RemoveItemFromPreset → Error: Received empty or invalid PresetDetailResponse");
                                    onError?.Invoke("Received empty or invalid PresetDetailResponse");
                                }
                            }
                            catch (System.Exception e)
                            {
                                string errorMsg = $"Parse get preset detail response error: {e.Message}";
                                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                    Debug.LogWarning($"<color=#66CCFF>[ItemPreset] RemoveItemFromPreset</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemPreset.cs › RemoveItemFromPresetCoroutine | {errorMsg}");
                                onError?.Invoke(errorMsg);
                            }
                        },
                        getError =>
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.LogWarning($"<color=#66CCFF>[ItemPreset] RemoveItemFromPreset</color> → <b><color=#FF4444>onError</color></b> callback (GET network) | ItemPreset.cs › RemoveItemFromPresetCoroutine | {getError}");
                            onError?.Invoke(getError);
                        }
                    ));
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] RemoveItemFromPreset</color> → <b><color=#FF4444>onError</color></b> callback (DELETE network) | ItemPreset.cs › RemoveItemFromPresetCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Fetches all presets for the current game.
        /// Endpoint: GET /api/v1/games/{game_id}/presets
        /// </summary>
        public void GetPresets(
            System.Action<PresetResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFCC><b>[ItemPreset] ► Get Presets</b></color>", gameObject);

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

            StartCoroutine(this.GetPresetsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetPresetsCoroutine(
            System.Action<PresetResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/presets";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        PresetResponse presetResponse = JsonUtility.FromJson<PresetResponse>(response);
                        this.currentPresets = presetResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[ItemPreset] Presets loaded: {presetResponse.containers.Length} presets");

                        this.OnGetPresetsSuccess?.Invoke(presetResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemPreset] GetPresets</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › GetPresetsCoroutine");
                        onSuccess?.Invoke(presetResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get presets response error: {e.Message}";
                        this.OnGetPresetsFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemPreset] GetPresets</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemPreset.cs › GetPresetsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetPresetsFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] GetPresets</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemPreset.cs › GetPresetsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Fetches details for a specific preset, including its slots.
        /// Endpoint: GET /api/v1/games/{game_id}/presets/{preset_id}
        /// </summary>
        public void GetPreset(
            string presetId,
            System.Action<PresetData> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FFCC><b>[ItemPreset] ► Get Preset {presetId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(presetId))
            {
                onError?.Invoke("preset_id cannot be empty.");
                return;
            }

            StartCoroutine(this.GetPresetCoroutine(presetId, onSuccess, onError));
        }

        private IEnumerator GetPresetCoroutine(
            string presetId,
            System.Action<PresetData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/presets/{presetId}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        PresetDetailResponse detail = JsonUtility.FromJson<PresetDetailResponse>(response);
                        if (detail != null && detail.container != null)
                        {
                            detail.container.slots = detail.slots;

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                Debug.Log($"[ItemPreset] Preset detail loaded: {detail.container.id}");

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.Log("<color=#66CCFF>[ItemPreset] GetPreset</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › GetPresetCoroutine");
                            
                            onSuccess?.Invoke(detail.container);
                        }
                        else
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                                Debug.LogWarning("[ItemPreset] GetPreset → Error: Received empty or invalid PresetDetailResponse");
                            onError?.Invoke("Received empty or invalid PresetDetailResponse");
                        }
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get preset detail response error: {e.Message}";
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemPreset] GetPreset</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemPreset.cs › GetPresetCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] GetPreset</color> → <b><color=#FF4444>onError</color></b> callback (GET network) | ItemPreset.cs › GetPresetCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Deletes a preset from the server.
        /// Endpoint: DELETE /api/v1/games/{game_id}/presets/{id}
        /// </summary>
        public void DeletePreset(
            string presetId,
            System.Action<string> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FF6666><b>[ItemPreset] ► Delete Preset {presetId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(presetId))
            {
                onError?.Invoke("preset_id cannot be empty.");
                return;
            }

            StartCoroutine(this.DeletePresetCoroutine(presetId, onSuccess, onError));
        }

        private IEnumerator DeletePresetCoroutine(
            string presetId,
            System.Action<string> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/presets/{presetId}";

            yield return SaiServer.Instance.DeleteRequest(endpoint,
                response =>
                {
                    if (this.currentPresets != null && this.currentPresets.containers != null)
                    {
                        var list = new System.Collections.Generic.List<PresetData>(this.currentPresets.containers);
                        list.RemoveAll(p => p.id == presetId);
                        this.currentPresets.containers = list.ToArray();
                    }

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"[ItemPreset] Preset {presetId} deleted successfully");

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[ItemPreset] DeletePreset</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemPreset.cs › DeletePresetCoroutine");
                    onSuccess?.Invoke(presetId);
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemPreset] DeletePreset</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemPreset.cs › DeletePresetCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears preset data locally.
        /// </summary>
        public void ClearPresets()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ItemPreset] ► Clear Presets</b></color>", gameObject);

            this.ClearLocalPresets();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[ItemPreset] Preset data cleared locally");
        }

        private void ClearLocalPresets()
        {
            this.currentPresets = new PresetResponse
            {
                containers = new PresetData[0]
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached preset with the given id, or null.</summary>
        public PresetData GetPresetById(string presetId)
        {
            if (this.currentPresets == null || this.currentPresets.containers == null)
                return null;

            foreach (PresetData preset in this.currentPresets.containers)
            {
                if (preset.id == presetId)
                    return preset;
            }

            return null;
        }

        /// <summary>Returns all locally cached presets that match the given preset_type.</summary>
        public PresetData[] GetPresetsByType(string presetType)
        {
            if (this.currentPresets == null || this.currentPresets.containers == null)
                return new PresetData[0];

            var result = new System.Collections.Generic.List<PresetData>();

            foreach (PresetData preset in this.currentPresets.containers)
            {
                if (preset.preset_type == presetType)
                    result.Add(preset);
            }

            return result.ToArray();
        }
    }
}
