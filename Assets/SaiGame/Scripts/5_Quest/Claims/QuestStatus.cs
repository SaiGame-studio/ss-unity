using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Handles GET /api/v1/games/{gameId}/quest-claims
    /// Fetches and caches the list of quest claims for the authenticated user.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class QuestStatus : SaiBehaviour
    {
        // Events
        public event Action<QuestClaimsResponse> OnGetClaimsSuccess;
        public event Action<string> OnGetClaimsFailure;
        public event Action<QuestDefinitionStatusResponse> OnGetQuestStatusSuccess;
        public event Action<string> OnGetQuestStatusFailure;

        [Header("Pagination Settings")]
        [SerializeField] protected int claimsLimit = 50;
        [SerializeField] protected int claimsOffset = 0;

        [Header("Cached Response")]
        [SerializeField] protected QuestClaimsResponse currentClaimsResponse;
        [SerializeField] protected QuestDefinitionStatusResponse currentQuestStatusResponse;

        public QuestClaimsResponse CurrentClaimsResponse => this.currentClaimsResponse;
        public QuestDefinitionStatusResponse CurrentQuestStatusResponse
        {
            get => this.currentQuestStatusResponse;
            set => this.currentQuestStatusResponse = value;
        }
        public bool HasClaims => this.currentClaimsResponse != null
                                 && this.currentClaimsResponse.claims != null
                                 && this.currentClaimsResponse.claims.Length > 0;

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
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[QuestClaims] Login detected, ready to fetch quest claims.");
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[QuestClaims] Logout detected, clearing cached claims...");

            this.ClearLocalClaims();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches the paginated list of quest claims for the current user.
        /// Endpoint: GET /api/v1/games/{gameId}/quest-claims?limit={limit}&offset={offset}
        /// </summary>
        public void GetClaims(
            int? limit = null,
            int? offset = null,
            Action<QuestClaimsResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[QuestClaims] ► Get Claims</b></color>", gameObject);

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

            int actualLimit = limit ?? this.claimsLimit;
            int actualOffset = offset ?? this.claimsOffset;

            StartCoroutine(this.GetClaimsCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetClaimsCoroutine(
            int limit,
            int offset,
            Action<QuestClaimsResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quest-claims?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        QuestClaimsResponse claimsResponse = JsonUtility.FromJson<QuestClaimsResponse>(response);
                        this.currentClaimsResponse = claimsResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[QuestClaims] Claims loaded: {claimsResponse.claims.Length} claims, total: {claimsResponse.total}");

                        this.OnGetClaimsSuccess?.Invoke(claimsResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[QuestClaims] GetClaims</color> → <b><color=#00FF88>onSuccess</color></b> callback | QuestClaims.cs › GetClaimsCoroutine");
                        onSuccess?.Invoke(claimsResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse get claims response error: {e.Message}";
                        this.OnGetClaimsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[QuestClaims] GetClaims</color> → <b><color=#FF4444>onError</color></b> callback (parse) | QuestClaims.cs › GetClaimsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetClaimsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[QuestClaims] GetClaims</color> → <b><color=#FF4444>onError</color></b> callback (network) | QuestClaims.cs › GetClaimsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Fetches the progress + definition for a single quest definition.
        /// Endpoint: GET /api/v1/games/{gameId}/quests/{questDefinitionId}
        /// </summary>
        public void GetQuestStatus(
            string questDefinitionId,
            Action<QuestDefinitionStatusResponse> onSuccess = null,
            Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FFFF><b>[QuestStatus] ► Get Quest Status: {questDefinitionId}</b></color>", gameObject);

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

            if (string.IsNullOrWhiteSpace(questDefinitionId))
            {
                onError?.Invoke("Quest Definition ID is required.");
                return;
            }

            StartCoroutine(this.GetQuestStatusCoroutine(questDefinitionId.Trim(), onSuccess, onError));
        }

        private IEnumerator GetQuestStatusCoroutine(
            string questDefinitionId,
            Action<QuestDefinitionStatusResponse> onSuccess,
            Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/quests/{questDefinitionId}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        QuestDefinitionStatusResponse statusResponse =
                            JsonUtility.FromJson<QuestDefinitionStatusResponse>(response);
                        this.currentQuestStatusResponse = statusResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[QuestStatus] Status for {questDefinitionId}: {statusResponse.status}");

                        this.OnGetQuestStatusSuccess?.Invoke(statusResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[QuestStatus] GetQuestStatus</color> → <b><color=#00FF88>onSuccess</color></b> callback | QuestStatus.cs › GetQuestStatusCoroutine");
                        onSuccess?.Invoke(statusResponse);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse quest status response error: {e.Message}";
                        this.OnGetQuestStatusFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[QuestStatus] GetQuestStatus</color> → <b><color=#FF4444>onError</color></b> callback (parse) | QuestStatus.cs › GetQuestStatusCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetQuestStatusFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[QuestStatus] GetQuestStatus</color> → <b><color=#FF4444>onError</color></b> callback (network) | QuestStatus.cs › GetQuestStatusCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Cache management ───────────────────────────────────────────────────

        /// <summary>Clears locally cached claims and resets pagination state.</summary>
        public void ClearClaims()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[QuestClaims] ► Clear Claims</b></color>", gameObject);

            this.ClearLocalClaims();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[QuestClaims] Claims data cleared locally");
        }

        private void ClearLocalClaims()
        {
            this.currentClaimsResponse = new QuestClaimsResponse
            {
                claims = new QuestClaimRecord[0],
                limit = this.claimsLimit,
                offset = 0,
                total = 0
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the cached claim with the given id, or null.</summary>
        public QuestClaimRecord GetClaimById(string claimId)
        {
            if (this.currentClaimsResponse?.claims == null) return null;

            foreach (QuestClaimRecord claim in this.currentClaimsResponse.claims)
            {
                if (claim.id == claimId)
                    return claim;
            }

            return null;
        }

        /// <summary>Returns all cached claims for the given quest_definition_id.</summary>
        public QuestClaimRecord[] GetClaimsByQuestDefinitionId(string questDefinitionId)
        {
            if (this.currentClaimsResponse?.claims == null) return new QuestClaimRecord[0];

            var result = new List<QuestClaimRecord>();

            foreach (QuestClaimRecord claim in this.currentClaimsResponse.claims)
            {
                if (claim.quest_definition_id == questDefinitionId)
                    result.Add(claim);
            }

            return result.ToArray();
        }

        // ── Inspector-exposed setters ──────────────────────────────────────────

        public void SetClaimsLimit(int limit) => this.claimsLimit = limit;
        public void SetClaimsOffset(int offset) => this.claimsOffset = offset;

        public int GetClaimsLimit() => this.claimsLimit;
        public int GetClaimsOffset() => this.claimsOffset;
    }
}
