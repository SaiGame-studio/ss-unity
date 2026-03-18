using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(Leaderboard))]
    [CanEditMultipleObjects]
    public class LeaderboardEditor : Editor
    {
        private Leaderboard leaderboard;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty currentBoards;
        private SerializedProperty currentTopRankings;
        private SerializedProperty currentMyRank;
        private SerializedProperty selectedBoardId;
        private SerializedProperty selectedBoardKey;
        private SerializedProperty topN;

        private bool showCurrentBoards = true;
        private bool showBoardList = true;
        private bool showTopRankings = true;
        private bool showMyRank = true;
        private bool showUtilityButtons = true;
        private System.Collections.Generic.Dictionary<string, bool> boardFoldouts = new System.Collections.Generic.Dictionary<string, bool>();

        private void OnEnable()
        {
            this.leaderboard = (Leaderboard)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.currentBoards = serializedObject.FindProperty("currentBoards");
            this.currentTopRankings = serializedObject.FindProperty("currentTopRankings");
            this.currentMyRank = serializedObject.FindProperty("currentMyRank");
            this.selectedBoardId = serializedObject.FindProperty("selectedBoardId");
            this.selectedBoardKey = serializedObject.FindProperty("selectedBoardKey");
            this.topN = serializedObject.FindProperty("topN");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Leaderboard Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load leaderboard boards when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.selectedBoardId, new GUIContent("Selected Board ID", "Board UUID used for Get Board / Top N / My Rank"));
            EditorGUILayout.PropertyField(this.topN, new GUIContent("Top N", "Number of top rankings to fetch"));

            EditorGUILayout.Space();

            // Current Boards Data
            this.showCurrentBoards = EditorGUILayout.Foldout(this.showCurrentBoards, "Current Boards Data", true);
            if (this.showCurrentBoards)
            {
                EditorGUI.indentLevel++;

                if (this.leaderboard.CurrentBoards != null && this.leaderboard.CurrentBoards.boards != null)
                {
                    int count = this.leaderboard.CurrentBoards.boards.Length;
                    EditorGUILayout.LabelField($"Loaded Boards: {count}");

                    if (count > 0)
                    {
                        this.showBoardList = EditorGUILayout.Foldout(this.showBoardList, $"Board List ({count})", true);
                        if (this.showBoardList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (LeaderboardBoard board in this.leaderboard.CurrentBoards.boards)
                            {
                                DrawBoardSummary(board);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No boards loaded.", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Top Rankings Data
            var topRankings = this.leaderboard.CurrentTopRankings;
            string topRankingsLabel = (topRankings != null && topRankings.entries != null)
                ? $"Top N Rankings  ({topRankings.entries.Length}/{topRankings.total})"
                : "Top N Rankings";
            this.showTopRankings = EditorGUILayout.Foldout(this.showTopRankings, topRankingsLabel, true);
            if (this.showTopRankings)
            {
                EditorGUI.indentLevel++;

                if (topRankings != null && topRankings.entries != null && topRankings.entries.Length > 0)
                {
                    EditorGUILayout.LabelField($"Limit: {topRankings.limit}   Total: {topRankings.total}");
                    EditorGUILayout.Space(2);
                    foreach (LeaderboardRankingEntry entry in topRankings.entries)
                    {
                        DrawRankingEntry(entry);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No rankings loaded. Press \"Get Top N Rankings\".", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // My Rank Data
            var myRank = this.leaderboard.CurrentMyRank;
            string myRankLabel = (myRank != null && myRank.user_id != null)
                ? $"My Rank  (#{myRank.rank}  score: {myRank.score})"
                : "My Rank";
            this.showMyRank = EditorGUILayout.Foldout(this.showMyRank, myRankLabel, true);
            if (this.showMyRank)
            {
                EditorGUI.indentLevel++;

                if (myRank != null && myRank.user_id != null)
                {
                    EditorGUILayout.LabelField($"Rank: #{myRank.rank}");
                    EditorGUILayout.LabelField($"User ID: {myRank.user_id}");
                    EditorGUILayout.LabelField($"Score: {myRank.score}");
                    if (!string.IsNullOrEmpty(myRank.metadata) && myRank.metadata != "null")
                        EditorGUILayout.LabelField($"Metadata: {myRank.metadata}");
                    if (myRank.season != null && !string.IsNullOrEmpty(myRank.season.id))
                        EditorGUILayout.LabelField($"Season: #{myRank.season.season_number}  ({myRank.season.id})");
                    EditorGUILayout.LabelField($"Updated: {myRank.updated_at}");
                }
                else
                {
                    EditorGUILayout.LabelField("No rank loaded. Press \"Get My Rank\".", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                // List Boards & Clear
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("List Boards", GUILayout.Height(30)))
                    this.EditorListBoards();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Boards", GUILayout.Height(30)))
                    this.leaderboard.ClearBoards();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Get Board (using selectedBoardId)
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button($"Get Board  \"{this.leaderboard.GetSelectedBoardId()}\"", GUILayout.Height(28)))
                    this.EditorGetBoard();
                GUI.backgroundColor = Color.white;

                // Get Top N Rankings (using selectedBoardId)
                GUI.backgroundColor = new Color(1f, 0.84f, 0f);
                if (GUILayout.Button($"Get Top N Rankings  \"{this.leaderboard.GetSelectedBoardId()}\"", GUILayout.Height(28)))
                    this.EditorGetTopRankings();
                GUI.backgroundColor = Color.white;

                // Get Local Ranking (using selectedBoardId)
                GUI.backgroundColor = new Color(0.6f, 0.4f, 1f);
                if (GUILayout.Button($"Get My Rank  \"{this.leaderboard.GetSelectedBoardId()}\"", GUILayout.Height(28)))
                    this.EditorGetLocalRanking();
                GUI.backgroundColor = Color.white;

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRankingEntry(LeaderboardRankingEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"#{entry.rank}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"User ID: {entry.user_id}");
            EditorGUILayout.LabelField($"Score: {entry.score}");
            if (!string.IsNullOrEmpty(entry.metadata) && entry.metadata != "null")
                EditorGUILayout.LabelField($"Metadata: {entry.metadata}");
            EditorGUILayout.LabelField($"Updated: {entry.updated_at}");
            EditorGUILayout.EndVertical();
        }

        private void DrawBoardSummary(LeaderboardBoard board)
        {
            if (!this.boardFoldouts.ContainsKey(board.id))
                this.boardFoldouts[board.id] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            this.boardFoldouts[board.id] = EditorGUILayout.Foldout(
                this.boardFoldouts[board.id],
                $"{board.name}  [{board.board_key}]",
                true,
                EditorStyles.foldoutHeader);

            if (this.boardFoldouts[board.id])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"ID: {board.id}");
                EditorGUILayout.LabelField($"Score Mode: {board.score_mode}   Sort: {board.sort_direction}   Reset: {board.reset_schedule}");
                EditorGUILayout.LabelField($"Active: {board.is_active}   Source: {board.score_source_type}");
                if (!string.IsNullOrEmpty(board.season_id))
                    EditorGUILayout.LabelField($"Season ID: {board.season_id}");
                EditorGUI.indentLevel--;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Get Board", GUILayout.Height(22)))
                {
                    this.leaderboard.SetSelectedBoardId(board.id);
                    this.EditorGetBoard(board.id);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.84f, 0f);
                if (GUILayout.Button("Top N", GUILayout.Height(22)))
                {
                    this.leaderboard.SetSelectedBoardId(board.id);
                    this.EditorGetTopRankings(board.id);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.6f, 0.4f, 1f);
                if (GUILayout.Button("My Rank", GUILayout.Height(22)))
                {
                    this.leaderboard.SetSelectedBoardId(board.id);
                    this.EditorGetLocalRanking(board.id);
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void EditorListBoards()
        {
            if (!this.CheckReady("[LeaderboardEditor]")) return;

            this.leaderboard.ListBoards(
                onSuccess: result =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Loaded {result.boards?.Length ?? 0} boards");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] List boards failed: {error}");
                }
            );
        }

        private void EditorGetBoard(string boardId = null)
        {
            if (!this.CheckReady("[LeaderboardEditor]")) return;

            string id = boardId ?? this.leaderboard.GetSelectedBoardId();
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[LeaderboardEditor] Selected Board ID is empty!");
                return;
            }

            this.leaderboard.GetBoard(
                id,
                onSuccess: result =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Got board: {result?.name} (id: {result?.id})");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] Get board failed: {error}");
                }
            );
        }

        private void EditorGetTopRankings(string boardId = null)
        {
            if (!this.CheckReady("[LeaderboardEditor]")) return;

            string id = boardId ?? this.leaderboard.GetSelectedBoardId();
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[LeaderboardEditor] Selected Board ID is empty!");
                return;
            }

            this.leaderboard.GetTopRankings(
                id,
                onSuccess: result =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Got {result?.entries?.Length ?? 0} entries (total: {result?.total}) for board: {id}");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] Get top N rankings failed: {error}");
                }
            );
        }

        private void EditorGetLocalRanking(string boardId = null)
        {
            if (!this.CheckReady("[LeaderboardEditor]")) return;

            string id = boardId ?? this.leaderboard.GetSelectedBoardId();
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[LeaderboardEditor] Selected Board ID is empty!");
                return;
            }

            this.leaderboard.GetLocalRanking(
                id,
                onSuccess: result =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] My rank for {id}: rank #{result?.rank}, score: {result?.score}");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] Get my rank failed: {error}");
                }
            );
        }

        private bool CheckReady(string prefix)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError($"{prefix} SaiService not found!");
                return false;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError($"{prefix} Not authenticated! Please login first.");
                return false;
            }

            return true;
        }
    }
}
