using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class GiftCodeRedeemer : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<GiftCodeResponse> OnRedeemSuccess;
        public event Action<string> OnRedeemFailure;

        [Header("Gift Code Settings")]
        [SerializeField] private string defaultGiftCode = "";

        [Header("Last Redeem Result")]
        [SerializeField] private GiftCodeResponse lastResponse;

        public GiftCodeResponse LastResponse => this.lastResponse;
        public string DefaultGiftCode => this.defaultGiftCode;

        /// <summary>
        /// Redeems a gift code.
        /// Endpoint: POST /api/v1/games/{game_id}/gift-codes/{code}/claim
        /// </summary>
        public void Redeem(
            string code = null,
            Action<GiftCodeResponse> onSuccess = null,
            Action<string> onError = null)
        {
            string giftCode = code ?? this.defaultGiftCode;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[GiftCodeRedeemer] ► Redeem Gift Code: {giftCode}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(giftCode))
            {
                onError?.Invoke("Gift Code is empty!");
                return;
            }

            StartCoroutine(this.RedeemCoroutine(giftCode, onSuccess, onError));
        }

        private IEnumerator RedeemCoroutine(
            string code,
            Action<GiftCodeResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/gift-codes/{code}/claim";
            string body = "{}";

            yield return SaiServer.Instance.PostRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        GiftCodeResponse redeemResponse = JsonUtility.FromJson<GiftCodeResponse>(response);
                        this.lastResponse = redeemResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"[GiftCodeRedeemer] Gift code '{code}' redeemed successfully! Total Items: {redeemResponse.total_items_granted}, Gacha Rolls: {redeemResponse.results?.Length ?? 0}");

                        this.OnRedeemSuccess?.Invoke(redeemResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#FFD700>[GiftCodeRedeemer] Redeem</color> → <b><color=#00FF88>onSuccess</color></b> callback | GiftCodeRedeemer.cs › RedeemCoroutine");

                        onSuccess?.Invoke(redeemResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse gift code response error: {e.Message}";
                        this.OnRedeemFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#FFD700>[GiftCodeRedeemer] Redeem</color> → <b><color=#FF4444>onError</color></b> callback (parse) | GiftCodeRedeemer.cs › RedeemCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnRedeemFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#FFD700>[GiftCodeRedeemer] Redeem</color> → <b><color=#FF4444>onError</color></b> callback (network) | GiftCodeRedeemer.cs › RedeemCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears the last redeem response data.
        /// </summary>
        public void ClearLastResponse()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[GiftCodeRedeemer] ► Clear Last Response</b></color>", gameObject);

            this.lastResponse = null;
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetDefaultGiftCode(string code) => this.defaultGiftCode = code;
    }
}
