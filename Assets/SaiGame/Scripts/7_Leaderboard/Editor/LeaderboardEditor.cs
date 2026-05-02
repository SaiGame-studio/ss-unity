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
        private SerializedProperty selectedBoardId;
        private SerializedProperty selectedBoardKey;

        private bool showCurrentBoards = true;
        private bool showBoardList = true;
        private bool showUtilityButtons = true;
        private System.Collections.Generic.Dictionary<string, bool> boardFoldouts = new System.Collections.Generic.Dictionary<string, bool>();
    // Dictionary for ranking entry expand state: key = "boardId_rank"
    private System.Collections.Generic.Dictionary<string, bool> expandedRankings = new System.Collections.Generic.Dictionary<string, bool>();
    // Per-board Top N input value (keyed by board id); default 10
    private System.Collections.Generic.Dictionary<string, int> boardTopNInputs = new System.Collections.Generic.Dictionary<string, int>();

        private void OnEnable()
        {
            this.leaderboard = (Leaderboard)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.currentBoards = serializedObject.FindProperty("currentBoards");
            this.currentTopRankings = serializedObject.FindProperty("currentTopRankings");
            this.selectedBoardId = serializedObject.FindProperty("selectedBoardId");
            this.selectedBoardKey = serializedObject.FindProperty("selectedBoardKey");
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

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRankingEntry(LeaderboardRankingEntry entry, string boardId)
        {
            string entryKey = $"{boardId}_{entry.rank}";
            if (!this.expandedRankings.ContainsKey(entryKey))
                this.expandedRankings[entryKey] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Compact header with foldout
            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(rowRect.x, rowRect.y, 14f, rowRect.height);
            Rect rankRect = new Rect(rowRect.x + 16f, rowRect.y, 48f, rowRect.height);
            Rect displayNameRect = new Rect(rowRect.x + 66f, rowRect.y, rowRect.width - 148f, rowRect.height);
            Rect scoreRect = new Rect(rowRect.x + rowRect.width - 80f, rowRect.y, 80f, rowRect.height);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                this.expandedRankings[entryKey] = !this.expandedRankings[entryKey];
                currentEvent.Use();
            }

            this.expandedRankings[entryKey] = EditorGUI.Foldout(foldoutRect, this.expandedRankings[entryKey], GUIContent.none, true);

            GUIStyle rankStyle = new GUIStyle(EditorStyles.boldLabel);
            rankStyle.fontSize = 11;
            rankStyle.normal.textColor = new Color(1f, 0.72f, 0.25f);

            GUIStyle displayNameStyle = new GUIStyle(EditorStyles.label);
            displayNameStyle.fontSize = 11;
            displayNameStyle.fontStyle = FontStyle.Bold;
            displayNameStyle.normal.textColor = new Color(0.45f, 0.85f, 1f);
            displayNameStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle scoreStyle = new GUIStyle(EditorStyles.boldLabel);
            scoreStyle.fontSize = 11;
            scoreStyle.normal.textColor = new Color(0.5f, 1f, 0.65f);
            scoreStyle.alignment = TextAnchor.MiddleRight;

            string safeDisplayName = string.IsNullOrEmpty(entry.display_name) ? "(no name)" : entry.display_name;
            GUI.Label(rankRect, $"#{entry.rank}", rankStyle);
            GUI.Label(displayNameRect, safeDisplayName, displayNameStyle);
            GUI.Label(scoreRect, entry.score.ToString(), scoreStyle);

            // Expanded details
            if (this.expandedRankings[entryKey])
            {
                EditorGUI.indentLevel++;

                GUIStyle detailLabelStyle = new GUIStyle(EditorStyles.label);
                detailLabelStyle.fontSize = 9;
                detailLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

                GUIStyle scoreStyleExpanded = new GUIStyle(scoreStyle);
                scoreStyleExpanded.alignment = TextAnchor.MiddleLeft;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rank:", detailLabelStyle, GUILayout.Width(90));
                EditorGUILayout.LabelField($"#{entry.rank}", rankStyle);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Player Name:", detailLabelStyle, GUILayout.Width(90));
                EditorGUILayout.LabelField(safeDisplayName, displayNameStyle);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Score:", detailLabelStyle, GUILayout.Width(90));
                EditorGUILayout.LabelField(entry.score.ToString(), scoreStyleExpanded);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                
                EditorGUILayout.BeginHorizontal();
                GUIStyle idLabelStyle = new GUIStyle(EditorStyles.label);
                idLabelStyle.fontSize = 9;
                idLabelStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField($"User ID: {entry.user_id}", idLabelStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = entry.user_id;
                EditorGUILayout.EndHorizontal();
                
                GUIStyle detailStyle = new GUIStyle(EditorStyles.label);
                detailStyle.fontSize = 9;
                detailStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                
                if (!string.IsNullOrEmpty(entry.metadata) && entry.metadata != "null")
                    EditorGUILayout.LabelField($"Metadata: {entry.metadata}", detailStyle);
                EditorGUILayout.LabelField($"Updated: {entry.updated_at}", detailStyle);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBoardSummary(LeaderboardBoard board)
        {
            if (!this.boardFoldouts.ContainsKey(board.id))
                this.boardFoldouts[board.id] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!board.is_active)
            {
                this.boardFoldouts[board.id] = false;
                DrawDisabledBoardHeader(board);
            }
            else
            {
                this.boardFoldouts[board.id] = EditorGUILayout.Foldout(
                    this.boardFoldouts[board.id],
                    $"{board.name}  [{board.board_key}]",
                    true,
                    EditorStyles.foldoutHeader);
            }

            if (this.boardFoldouts[board.id])
            {
                EditorGUI.indentLevel++;

                GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
                separatorStyle.fontSize = 8;
                separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
                EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);

                GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionTitleStyle.fontSize = 11;
                sectionTitleStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("Board Details", sectionTitleStyle);

                GUIStyle richStyle = new GUIStyle(EditorStyles.label);
                richStyle.fontSize = 10;
                richStyle.richText = true;

                string cId        = ColorUtility.ToHtmlStringRGB(new Color(0.55f, 0.85f, 1f));    // sky blue
                string cGameId    = ColorUtility.ToHtmlStringRGB(new Color(0.55f, 1f,    0.80f)); // mint
                string cStudioId  = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.80f, 0.55f)); // peach
                string cSeasonId  = ColorUtility.ToHtmlStringRGB(new Color(0.80f, 0.80f, 1f));    // periwinkle
                string cBoardKey  = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.95f, 0.50f)); // light yellow
                string cSourceRef = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.70f, 0.95f)); // magenta
                string cName      = ColorUtility.ToHtmlStringRGB(new Color(0.45f, 1f,    1f));    // cyan
                string cDesc      = ColorUtility.ToHtmlStringRGB(new Color(0.90f, 0.90f, 0.90f)); // soft white
                string cScoreMode = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.75f, 0.35f)); // orange
                string cSort      = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.60f, 0.85f)); // pink
                string cReset     = ColorUtility.ToHtmlStringRGB(new Color(1f,    1f,    0.40f)); // yellow
                string cActive    = ColorUtility.ToHtmlStringRGB(board.is_active ? new Color(0.50f, 1f, 0.50f) : new Color(1f, 0.50f, 0.50f));
                string cSource    = ColorUtility.ToHtmlStringRGB(new Color(0.75f, 0.65f, 1f));    // lavender
                string cDate      = ColorUtility.ToHtmlStringRGB(new Color(0.75f, 0.75f, 0.75f)); // gray

                // IDs Section - all at the top with copy buttons
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Board ID: <b><color=#{cId}>{board.id}</color></b>", richStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.id;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Game ID: <b><color=#{cGameId}>{board.game_id}</color></b>", richStyle);
                if (!string.IsNullOrEmpty(board.game_id) && GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.game_id;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(board.studio_id))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Studio ID: <b><color=#{cStudioId}>{board.studio_id}</color></b>", richStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.studio_id;
                    EditorGUILayout.EndHorizontal();
                }

                if (!string.IsNullOrEmpty(board.season_id))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Season ID: <b><color=#{cSeasonId}>{board.season_id}</color></b>", richStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.season_id;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Board Key: <b><color=#{cBoardKey}>{board.board_key}</color></b>", richStyle);
                if (!string.IsNullOrEmpty(board.board_key) && GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.board_key;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(board.score_source_ref_id))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Source Ref ID: <b><color=#{cSourceRef}>{board.score_source_ref_id}</color></b>", richStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = board.score_source_ref_id;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"Name: <b><color=#{cName}>{board.name}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Description: <b><color=#{cDesc}>{board.description}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Score Mode: <b><color=#{cScoreMode}>{board.score_mode}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Sort: <b><color=#{cSort}>{board.sort_direction}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Reset: <b><color=#{cReset}>{board.reset_schedule}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Active: <b><color=#{cActive}>{board.is_active}</color></b>", richStyle);
                EditorGUILayout.LabelField($"Source: <b><color=#{cSource}>{board.score_source_type}</color></b>", richStyle);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"Created: <color=#{cDate}>{board.created_at}</color>", richStyle);
                EditorGUILayout.LabelField($"Updated: <color=#{cDate}>{board.updated_at}</color>", richStyle);

                EditorGUI.indentLevel--;

                // Draw Top Rankings for this board (always visible)
                var topRankings = this.leaderboard.GetBoardTopRankings(board.id);
                EditorGUILayout.Space(4);

                GUIStyle topNHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                topNHeaderStyle.fontSize = 10;
                topNHeaderStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);

                bool hasTopRankings = topRankings != null && topRankings.entries != null && topRankings.entries.Length > 0;
                string topNHeader = hasTopRankings
                    ? $"📊 Top N Rankings ({topRankings.entries.Length}/{topRankings.total})"
                    : "📊 Top N Rankings";
                EditorGUILayout.LabelField(topNHeader, topNHeaderStyle);

                EditorGUI.indentLevel++;
                if (hasTopRankings)
                {
                    foreach (LeaderboardRankingEntry entry in topRankings.entries)
                    {
                        DrawRankingEntry(entry, board.id);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No rankings loaded. Press \"Get Top N\".", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;

                // Draw My Rank for this board (always visible)
                var myRank = this.leaderboard.GetBoardMyRank(board.id);
                EditorGUILayout.Space(4);

                GUIStyle myRankHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                myRankHeaderStyle.fontSize = 10;
                myRankHeaderStyle.normal.textColor = new Color(0.85f, 0.75f, 1f);

                bool hasMyRank = myRank != null && !string.IsNullOrEmpty(myRank.user_id);
                string myRankHeader = hasMyRank
                    ? $"🏅 My Rank  (#{myRank.rank}  score: {myRank.score})"
                    : "🏅 My Rank";
                EditorGUILayout.LabelField(myRankHeader, myRankHeaderStyle);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (hasMyRank)
                {
                    GUIStyle myRankRichStyle = new GUIStyle(EditorStyles.label);
                    myRankRichStyle.fontSize = 10;
                    myRankRichStyle.richText = true;

                    string cMyRank   = ColorUtility.ToHtmlStringRGB(new Color(1f,    0.72f, 0.25f)); // amber
                    string cMyScore  = ColorUtility.ToHtmlStringRGB(new Color(0.50f, 1f,    0.65f)); // green
                    string cMyUser   = ColorUtility.ToHtmlStringRGB(new Color(0.55f, 0.85f, 1f));    // sky blue
                    string cMyMeta   = ColorUtility.ToHtmlStringRGB(new Color(0.90f, 0.90f, 0.90f)); // soft white
                    string cMySeason = ColorUtility.ToHtmlStringRGB(new Color(0.80f, 0.80f, 1f));    // periwinkle
                    string cMyDate   = ColorUtility.ToHtmlStringRGB(new Color(0.75f, 0.75f, 0.75f)); // gray

                    EditorGUILayout.LabelField($"Rank: <b><color=#{cMyRank}>#{myRank.rank}</color></b>", myRankRichStyle);
                    EditorGUILayout.LabelField($"Score: <b><color=#{cMyScore}>{myRank.score}</color></b>", myRankRichStyle);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"User ID: <b><color=#{cMyUser}>{myRank.user_id}</color></b>", myRankRichStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = myRank.user_id;
                    EditorGUILayout.EndHorizontal();

                    if (!string.IsNullOrEmpty(myRank.metadata) && myRank.metadata != "null")
                        EditorGUILayout.LabelField($"Metadata: <color=#{cMyMeta}>{myRank.metadata}</color>", myRankRichStyle);

                    if (myRank.season != null && !string.IsNullOrEmpty(myRank.season.id))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Season: <b><color=#{cMySeason}>#{myRank.season.season_number}  ({myRank.season.id})</color></b>", myRankRichStyle);
                        if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = myRank.season.id;
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.LabelField($"Updated: <color=#{cMyDate}>{myRank.updated_at}</color>", myRankRichStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("No rank loaded. Press \"My Rank\".", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginHorizontal();

                /* Temporarily hidden — do not delete
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Get Board", GUILayout.Height(22)))
                {
                    this.leaderboard.SetSelectedBoardId(board.id);
                    this.EditorGetBoard(board.id);
                }
                GUI.backgroundColor = Color.white;
                */

                if (!this.boardTopNInputs.ContainsKey(board.id))
                    this.boardTopNInputs[board.id] = 10;

                float prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 70;
                this.boardTopNInputs[board.id] = EditorGUILayout.IntField("Top N", this.boardTopNInputs[board.id], GUILayout.Height(22), GUILayout.Width(120));
                EditorGUIUtility.labelWidth = prevLabelWidth;
                if (this.boardTopNInputs[board.id] < 1) this.boardTopNInputs[board.id] = 1;

                int topNValue = this.boardTopNInputs[board.id];

                GUI.backgroundColor = new Color(1f, 0.84f, 0f);
                if (GUILayout.Button($"Get Top {topNValue}", GUILayout.Height(22), GUILayout.MinWidth(70)))
                {
                    this.leaderboard.SetSelectedBoardId(board.id);
                    this.EditorGetTopRankings(board.id, topNValue);
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

        private void DrawDisabledBoardHeader(LeaderboardBoard board)
        {
            EditorGUILayout.BeginHorizontal();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            EditorGUILayout.LabelField($"{board.name}  [{board.board_key}]", titleStyle);

            Rect badgeRect = GUILayoutUtility.GetRect(70f, 18f, GUILayout.Width(70f), GUILayout.Height(18f));
            EditorGUI.DrawRect(badgeRect, new Color(0.75f, 0.25f, 0.25f));

            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = Color.white;
            GUI.Label(badgeRect, "disabled", badgeStyle);

            EditorGUILayout.EndHorizontal();
        }

        private void EditorListBoards()
        {
            if (!this.CheckReady("[LeaderboardEditor]")) return;

            this.leaderboard.ListBoards(
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Loaded {result.boards?.Length ?? 0} boards");
                    EditorUtility.SetDirty(target);
                    serializedObject.Update();
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
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
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Got board: {result?.name} (id: {result?.id})");
                    EditorApplication.delayCall += () =>
                    {
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        Repaint();
                    };
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] Get board failed: {error}");
                }
            );
        }

        private void EditorGetTopRankings(string boardId = null, int? limit = null)
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
                limit: limit,
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] Got {result?.entries?.Length ?? 0} entries (total: {result?.total}) for board: {id}");
                    EditorApplication.delayCall += () =>
                    {
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        Repaint();
                    };
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
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
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[LeaderboardEditor] My rank for {id}: rank #{result?.rank}, score: {result?.score}");
                    EditorApplication.delayCall += () =>
                    {
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        Repaint();
                    };
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[LeaderboardEditor] Get my rank failed: {error}");
                }
            );
        }

        private bool CheckReady(string prefix)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError($"{prefix} SaiServer not found!");
                return false;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError($"{prefix} Not authenticated! Please login first.");
                return false;
            }

            return true;
        }
    }
}
