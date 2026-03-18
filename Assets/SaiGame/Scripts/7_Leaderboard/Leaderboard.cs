using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class Leaderboard : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<LeaderboardBoardsResponse> OnListBoardsSuccess;
        public event Action<string> OnListBoardsFailure;
        public event Action<LeaderboardBoard> OnGetBoardSuccess;
        public event Action<string> OnGetBoardFailure;
        public event Action<LeaderboardRankingsResponse> OnGetTopRankingsSuccess;
        public event Action<string> OnGetTopRankingsFailure;
        public event Action<LeaderboardLocalRankingResponse> OnGetLocalRankingSuccess;
        public event Action<string> OnGetLocalRankingFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Leaderboard Data")]
        [SerializeField] protected LeaderboardBoardsResponse currentBoards;
        [SerializeField] protected LeaderboardRankingsResponse currentTopRankings;
        [SerializeField] protected LeaderboardLocalRankingResponse currentMyRank;

        [Header("Query Settings")]
        [SerializeField] protected string selectedBoardId = "";
        [SerializeField] protected string selectedBoardKey = "";
        [SerializeField] protected int topN = 10;

        public LeaderboardBoardsResponse CurrentBoards => this.currentBoards;
        public bool HasBoards => this.currentBoards != null && this.currentBoards.boards != null && this.currentBoards.boards.Length > 0;
        public LeaderboardRankingsResponse CurrentTopRankings => this.currentTopRankings;
        public LeaderboardLocalRankingResponse CurrentMyRank => this.currentMyRank;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
        }

        protected virtual void RegisterLoginListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLoginSuccess += HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLogoutSuccess += HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiService.Instance?.SaiAuth != null)
            {
                SaiService.Instance.SaiAuth.OnLoginSuccess -= HandleLoginSuccess;
                SaiService.Instance.SaiAuth.OnLogoutSuccess -= HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!this.autoLoadOnLogin) return;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Leaderboard] Auto-loading boards after successful login...");

            this.ListBoards(
                onSuccess: boards =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[Leaderboard] Auto-loaded {boards.boards?.Length ?? 0} boards");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[Leaderboard] Auto-load boards failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Leaderboard] Logout successful, clearing leaderboard data...");

            this.ClearLocalBoards();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Leaderboard] Leaderboard data cleared successfully");
        }

        // ─── List Boards ────────────────────────────────────────────────────────────

        public void ListBoards(Action<LeaderboardBoardsResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[Leaderboard] ► List Boards</b></color>", gameObject);

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

            StartCoroutine(ListBoardsCoroutine(onSuccess, onError));
        }

        private IEnumerator ListBoardsCoroutine(Action<LeaderboardBoardsResponse> onSuccess, Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/leaderboards";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        LeaderboardBoardsResponse parsed = JsonUtility.FromJson<LeaderboardBoardsResponse>(response);
                        this.currentBoards = parsed;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Leaderboard] Loaded {parsed.boards?.Length ?? 0} boards");

                        OnListBoardsSuccess?.Invoke(parsed);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Leaderboard] ListBoards</color> → <b><color=#00FF88>onSuccess</color></b> callback | Leaderboard.cs › ListBoardsCoroutine");
                        onSuccess?.Invoke(parsed);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse list boards response error: {e.Message}";
                        OnListBoardsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Leaderboard] ListBoards</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Leaderboard.cs › ListBoardsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnListBoardsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Leaderboard] ListBoards</color> → <b><color=#FF4444>onError</color></b> callback (network) | Leaderboard.cs › ListBoardsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ─── Get Board ───────────────────────────────────────────────────────────────

        public void GetBoard(string boardId, Action<LeaderboardBoard> onSuccess = null, Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF88><b>[Leaderboard] ► Get Board: {boardId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(boardId))
            {
                onError?.Invoke("Board ID cannot be empty!");
                return;
            }

            StartCoroutine(GetBoardCoroutine(boardId, onSuccess, onError));
        }

        private IEnumerator GetBoardCoroutine(string boardId, Action<LeaderboardBoard> onSuccess, Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/leaderboards/{boardId}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        // Try wrapped format first: { "board": {...} }
                        LeaderboardBoardResponse wrapped = JsonUtility.FromJson<LeaderboardBoardResponse>(response);
                        LeaderboardBoard board = (wrapped != null && wrapped.board != null && !string.IsNullOrEmpty(wrapped.board.id))
                            ? wrapped.board
                            : JsonUtility.FromJson<LeaderboardBoard>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Leaderboard] Got board: {board?.name} (key: {board?.board_key})");

                        OnGetBoardSuccess?.Invoke(board);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Leaderboard] GetBoard</color> → <b><color=#00FF88>onSuccess</color></b> callback | Leaderboard.cs › GetBoardCoroutine");
                        onSuccess?.Invoke(board);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse get board response error: {e.Message}";
                        OnGetBoardFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetBoard</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Leaderboard.cs › GetBoardCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetBoardFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetBoard</color> → <b><color=#FF4444>onError</color></b> callback (network) | Leaderboard.cs › GetBoardCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ─── Get Top N Rankings (uses board_id) ──────────────────────────────────────────────────────

        public void GetTopRankings(string boardId, int? limit = null, Action<LeaderboardRankingsResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#FFD700><b>[Leaderboard] ► Get Top N Rankings: {boardId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(boardId))
            {
                onError?.Invoke("Board ID cannot be empty!");
                return;
            }

            int actualLimit = limit ?? this.topN;
            StartCoroutine(GetTopRankingsCoroutine(boardId, actualLimit, onSuccess, onError));
        }

        private IEnumerator GetTopRankingsCoroutine(string boardId, int limit, Action<LeaderboardRankingsResponse> onSuccess, Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/leaderboards/{boardId}/top?limit={limit}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        LeaderboardRankingsResponse parsed = JsonUtility.FromJson<LeaderboardRankingsResponse>(response);
                        this.currentTopRankings = parsed;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Leaderboard] Got {parsed.entries?.Length ?? 0} entries (total: {parsed.total}) for board: {boardId}");

                        OnGetTopRankingsSuccess?.Invoke(parsed);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Leaderboard] GetTopRankings</color> → <b><color=#00FF88>onSuccess</color></b> callback | Leaderboard.cs › GetTopRankingsCoroutine");
                        onSuccess?.Invoke(parsed);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse get top rankings response error: {e.Message}";
                        OnGetTopRankingsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetTopRankings</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Leaderboard.cs › GetTopRankingsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetTopRankingsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetTopRankings</color> → <b><color=#FF4444>onError</color></b> callback (network) | Leaderboard.cs › GetTopRankingsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ─── Get My Rank ─────────────────────────────────────────────────────────────

        public void GetLocalRanking(string boardId, Action<LeaderboardLocalRankingResponse> onSuccess = null, Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#CC88FF><b>[Leaderboard] ► Get My Rank: {boardId}</b></color>", gameObject);

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

            if (string.IsNullOrEmpty(boardId))
            {
                onError?.Invoke("Board ID cannot be empty!");
                return;
            }

            StartCoroutine(GetLocalRankingCoroutine(boardId, onSuccess, onError));
        }

        private IEnumerator GetLocalRankingCoroutine(string boardId, Action<LeaderboardLocalRankingResponse> onSuccess, Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/leaderboards/{boardId}/me";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        LeaderboardLocalRankingResponse parsed = JsonUtility.FromJson<LeaderboardLocalRankingResponse>(response);
                        this.currentMyRank = parsed;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[Leaderboard] My rank for board {boardId}: rank #{parsed.rank}, score: {parsed.score}");

                        OnGetLocalRankingSuccess?.Invoke(parsed);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[Leaderboard] GetLocalRanking</color> → <b><color=#00FF88>onSuccess</color></b> callback | Leaderboard.cs › GetLocalRankingCoroutine");
                        onSuccess?.Invoke(parsed);
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"Parse get my rank response error: {e.Message}";
                        OnGetLocalRankingFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetLocalRanking</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Leaderboard.cs › GetLocalRankingCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetLocalRankingFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[Leaderboard] GetLocalRanking</color> → <b><color=#FF4444>onError</color></b> callback (network) | Leaderboard.cs › GetLocalRankingCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ─── Utility ─────────────────────────────────────────────────────────────────

        public void ClearBoards()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[Leaderboard] ► Clear Boards</b></color>", gameObject);

            this.ClearLocalBoards();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[Leaderboard] Board data cleared locally");
        }

        private void ClearLocalBoards()
        {
            this.currentBoards = new LeaderboardBoardsResponse
            {
                boards = new LeaderboardBoard[0]
            };
        }

        public LeaderboardBoard GetBoardByKey(string boardKey)
        {
            if (this.currentBoards == null || this.currentBoards.boards == null)
                return null;

            foreach (LeaderboardBoard board in this.currentBoards.boards)
            {
                if (board.board_key == boardKey)
                    return board;
            }

            return null;
        }

        public LeaderboardBoard GetBoardById(string id)
        {
            if (this.currentBoards == null || this.currentBoards.boards == null)
                return null;

            foreach (LeaderboardBoard board in this.currentBoards.boards)
            {
                if (board.id == id)
                    return board;
            }

            return null;
        }

        public void SetSelectedBoardId(string boardId)
        {
            this.selectedBoardId = boardId;
        }

        public string GetSelectedBoardId()
        {
            return this.selectedBoardId;
        }

        public void SetSelectedBoardKey(string boardKey)
        {
            this.selectedBoardKey = boardKey;
        }

        public string GetSelectedBoardKey()
        {
            return this.selectedBoardKey;
        }

        public void SetTopN(int n)
        {
            this.topN = n;
        }

        public int GetTopN()
        {
            return this.topN;
        }
    }
}
