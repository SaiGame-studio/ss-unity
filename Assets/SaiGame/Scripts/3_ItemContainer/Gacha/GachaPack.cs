using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class GachaPack : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<GachaResponse> OnOpenGachaSuccess;
        public event Action<string> OnOpenGachaFailure;

        [Header("Gacha Pack Settings")]
        [SerializeField] protected string gachaPackId = "";
        [SerializeField] protected string gachaPackCode = "";
        [SerializeField] protected string containerId = "";

        [Header("Last Gacha Result")]
        [SerializeField] protected GachaResponse lastResponse;

        public GachaResponse LastResponse => this.lastResponse;
        public string GachaPackId => this.gachaPackId;
        public string GachaPackCode => this.gachaPackCode;
        public string ContainerId => this.containerId;

        /// <summary>
        /// Opens a gacha pack.
        /// Endpoint: POST /api/v1/games/{game_id}/gacha/{gacha_pack_id}
        /// </summary>
        public void OpenGachaPack(
            string gachaPackDefId = null,
            string targetContainerId = null,
            Action<GachaResponse> onSuccess = null,
            Action<string> onError = null)
        {
            string packId = gachaPackDefId ?? this.gachaPackId;
            string contId = targetContainerId ?? this.containerId;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[GachaPack] ► Open Gacha Pack: {packId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(packId))
            {
                onError?.Invoke("Gacha Pack ID is empty!");
                return;
            }

            if (string.IsNullOrEmpty(contId))
            {
                onError?.Invoke("Container ID is empty!");
                return;
            }

            StartCoroutine(this.OpenGachaPackCoroutine(packId, contId, onSuccess, onError));
        }

        private IEnumerator OpenGachaPackCoroutine(
            string gachaPackDefId,
            string targetContainerId,
            Action<GachaResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gacha/{gachaPackDefId}";
            string idempotencyKey = $"{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}";
            string body = $"{{\"idempotency_key\":\"{idempotencyKey}\",\"container_id\":\"{targetContainerId}\"}}";

            yield return SaiServer.Instance.PostRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        GachaResponse gachaResponse = JsonUtility.FromJson<GachaResponse>(response);
                        this.lastResponse = gachaResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[GachaPack] Gacha pack opened: {gachaResponse.items_granted?.Length ?? 0} items granted (duplicate={gachaResponse.is_duplicate})");

                        this.OnOpenGachaSuccess?.Invoke(gachaResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#FFD700>[GachaPack] OpenGachaPack</color> → <b><color=#00FF88>onSuccess</color></b> callback | GachaPack.cs › OpenGachaPackCoroutine");

                        onSuccess?.Invoke(gachaResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse gacha response error: {e.Message}";
                        this.OnOpenGachaFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#FFD700>[GachaPack] OpenGachaPack</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GachaPack.cs › OpenGachaPackCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnOpenGachaFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#FFD700>[GachaPack] OpenGachaPack</color> → <b><color=#FF4444>onError</color></b> callback (network) | GachaPack.cs › OpenGachaPackCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Opens a gacha pack by its code name.
        /// Endpoint: POST /api/v1/games/{game_id}/gacha/by-code/{code}
        /// </summary>
        public void OpenGachaPackByCode(
            string code = null,
            string targetContainerId = null,
            Action<GachaResponse> onSuccess = null,
            Action<string> onError = null)
        {
            string packCode = code ?? this.gachaPackCode;
            string contId = targetContainerId ?? this.containerId;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[GachaPack] ► Open Gacha By Code: {packCode}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(packCode))
            {
                onError?.Invoke("Gacha Pack Code is empty!");
                return;
            }

            if (string.IsNullOrEmpty(contId))
            {
                onError?.Invoke("Container ID is empty!");
                return;
            }

            StartCoroutine(this.OpenGachaPackByCodeCoroutine(packCode, contId, onSuccess, onError));
        }

        private IEnumerator OpenGachaPackByCodeCoroutine(
            string code,
            string targetContainerId,
            Action<GachaResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gacha/by-code/{code}";
            string idempotencyKey = $"{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}-{UnityEngine.Random.Range(1000000, 9999999)}";
            string body = $"{{\"idempotency_key\":\"{idempotencyKey}\",\"container_id\":\"{targetContainerId}\"}}";

            yield return SaiServer.Instance.PostRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        GachaResponse gachaResponse = JsonUtility.FromJson<GachaResponse>(response);
                        this.lastResponse = gachaResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[GachaPack] Gacha by code '{code}' opened: {gachaResponse.items_granted?.Length ?? 0} items granted (duplicate={gachaResponse.is_duplicate})");

                        this.OnOpenGachaSuccess?.Invoke(gachaResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#FFD700>[GachaPack] OpenGachaPackByCode</color> → <b><color=#00FF88>onSuccess</color></b> callback | GachaPack.cs › OpenGachaPackByCodeCoroutine");

                        onSuccess?.Invoke(gachaResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse gacha response error: {e.Message}";
                        this.OnOpenGachaFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#FFD700>[GachaPack] OpenGachaPackByCode</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GachaPack.cs › OpenGachaPackByCodeCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnOpenGachaFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#FFD700>[GachaPack] OpenGachaPackByCode</color> → <b><color=#FF4444>onError</color></b> callback (network) | GachaPack.cs › OpenGachaPackByCodeCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears the last gacha response data.
        /// </summary>
        public void ClearLastResponse()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[GachaPack] ► Clear Last Response</b></color>", gameObject);

            this.lastResponse = null;
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetGachaPackId(string id) => this.gachaPackId = id;
        public void SetGachaPackCode(string code) => this.gachaPackCode = code;
        public void SetContainerId(string id) => this.containerId = id;
    }
}
