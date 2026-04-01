using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(BattleSessions))]
    [CanEditMultipleObjects]
    public class BattleSessionsEditor : Editor
    {
        private BattleSessions battleSessions;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty sessionLimit;
        private SerializedProperty sessionOffset;

        private bool showCurrentSessions = true;
        private bool showSessionList = false;
        private bool showUtilityButtons = true;

        private readonly Dictionary<string, bool> sessionFoldouts = new Dictionary<string, bool>();

        private void OnEnable()
        {
            this.battleSessions = (BattleSessions)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.sessionLimit    = serializedObject.FindProperty("sessionLimit");
            this.sessionOffset   = serializedObject.FindProperty("sessionOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Battle Sessions Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load battle sessions when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.sessionLimit,  new GUIContent("Session Limit",  "Number of sessions to load per request"));
            EditorGUILayout.PropertyField(this.sessionOffset, new GUIContent("Session Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current Sessions Data
            this.showCurrentSessions = EditorGUILayout.Foldout(this.showCurrentSessions, "Current Sessions Data", true);
            if (this.showCurrentSessions)
            {
                EditorGUI.indentLevel++;

                if (this.battleSessions.CurrentSessions != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Sessions: {this.battleSessions.CurrentSessions.total}");
                    EditorGUILayout.LabelField($"Loaded Sessions: {this.battleSessions.CurrentSessions.sessions?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Limit: {this.battleSessions.CurrentSessions.limit}  |  Offset: {this.battleSessions.CurrentSessions.offset}");

                    if (this.battleSessions.CurrentSessions.sessions != null
                        && this.battleSessions.CurrentSessions.sessions.Length > 0)
                    {
                        this.showSessionList = EditorGUILayout.Foldout(this.showSessionList, "Session List", true);
                        if (this.showSessionList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (BattleSessionData session in this.battleSessions.CurrentSessions.sessions)
                            {
                                this.DrawSessionSummary(session);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No session data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Sessions", GUILayout.Height(30)))
                {
                    this.LoadSessions();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Sessions", GUILayout.Height(30)))
                {
                    this.battleSessions.ClearSessions();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSessionSummary(BattleSessionData session)
        {
            if (!this.sessionFoldouts.ContainsKey(session.id))
                this.sessionFoldouts[session.id] = false;

            bool victory = session.end_data != null && session.end_data.victory;
            Color statusColor = GetStatusColor(session.status, victory);

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor    = statusColor;
            foldoutStyle.onNormal.textColor  = statusColor;
            foldoutStyle.focused.textColor   = statusColor;
            foldoutStyle.onFocused.textColor = statusColor;
            foldoutStyle.active.textColor    = statusColor;
            foldoutStyle.onActive.textColor  = statusColor;

            string resultLabel = session.end_data != null
                ? (session.end_data.victory ? "Victory" : "Defeat")
                : "In Progress";
            string label = $"[{session.status.ToUpper()}]  {resultLabel}  —  {session.started_at}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            this.sessionFoldouts[session.id] = EditorGUILayout.Foldout(this.sessionFoldouts[session.id], label, true, foldoutStyle);

            if (this.sessionFoldouts[session.id])
            {
                EditorGUI.indentLevel++;

                // ── Key Stats Card ─────────────────────────────────────────────
                EditorGUILayout.Space(4);
                this.DrawKeyStatsCard(session);
                EditorGUILayout.Space(4);

                // ── Session Fields ─────────────────────────────────────────────
                EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ID", session.id);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = session.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Game ID", session.game_id);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = session.game_id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Player ID", session.player_id);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = session.player_id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Status",     session.status);
                EditorGUILayout.LabelField("Started At", session.started_at);
                EditorGUILayout.LabelField("Expires At", session.expires_at);
                EditorGUILayout.LabelField("Ended At",   session.ended_at);

                // ── Start Data ────────────────────────────────────────────────
                if (session.start_data != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Start Data (JSON)", EditorStyles.boldLabel);
                    string startJson = JsonUtility.ToJson(session.start_data, true);
                    GUIStyle startJsonStyle = new GUIStyle(EditorStyles.textArea);
                    startJsonStyle.wordWrap = true;
                    float startHeight = startJsonStyle.CalcHeight(new GUIContent(startJson), EditorGUIUtility.currentViewWidth - 40);
                    EditorGUILayout.SelectableLabel(startJson, startJsonStyle, GUILayout.Height(startHeight));
                }

                // ── End Data ──────────────────────────────────────────────────
                if (session.end_data != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("End Data (JSON)", EditorStyles.boldLabel);
                    string endJson = JsonUtility.ToJson(session.end_data, true);
                    GUIStyle endJsonStyle = new GUIStyle(EditorStyles.textArea);
                    endJsonStyle.wordWrap = true;
                    float endHeight = endJsonStyle.CalcHeight(new GUIContent(endJson), EditorGUIUtility.currentViewWidth - 40);
                    EditorGUILayout.SelectableLabel(endJson, endJsonStyle, GUILayout.Height(endHeight));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawKeyStatsCard(BattleSessionData session)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 11;
            headerStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.LabelField("✦ KEY STATS", headerStyle);

            GUIStyle labelCol = new GUIStyle(EditorStyles.label);
            labelCol.fontSize = 11;
            labelCol.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            GUIStyle valueCol = new GUIStyle(EditorStyles.boldLabel);
            valueCol.fontSize = 11;

            // Status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", labelCol, GUILayout.Width(90));
            valueCol.normal.textColor = GetStatusColor(session.status, session.end_data?.victory ?? false);
            EditorGUILayout.LabelField(session.status.ToUpper(), valueCol);
            EditorGUILayout.EndHorizontal();

            if (session.end_data != null)
            {
                // Victory/Defeat
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Result:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = session.end_data.victory ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(session.end_data.victory ? "Victory ✓" : "Defeat ✕", valueCol);
                EditorGUILayout.EndHorizontal();

                // Kills
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Kills:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = new Color(1f, 0.65f, 0.2f);
                EditorGUILayout.LabelField(session.end_data.kills.ToString(), valueCol);
                EditorGUILayout.EndHorizontal();

                // Turns
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Turns:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = new Color(0.4f, 1f, 0.9f);
                EditorGUILayout.LabelField(session.end_data.turns_taken.ToString(), valueCol);
                EditorGUILayout.EndHorizontal();

                // Survival
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Survival:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = Color.white;
                EditorGUILayout.LabelField($"{session.end_data.survival_pct}%", valueCol);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static Color GetStatusColor(string status, bool victory)
        {
            if (string.IsNullOrEmpty(status)) return Color.white;
            switch (status.ToLower())
            {
                case "ended":   return victory ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f);
                case "active":  return new Color(0.4f, 0.85f, 1f);
                case "expired": return new Color(0.6f, 0.6f, 0.6f);
                default:        return Color.white;
            }
        }

        private void LoadSessions()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[BattleSessionsEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[BattleSessionsEditor] Not authenticated! Please login first.");
                return;
            }

            this.battleSessions.GetSessions(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[BattleSessionsEditor] Loaded {response.sessions.Length} sessions (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[BattleSessionsEditor] Failed to load sessions: {error}");
                }
            );
        }
    }
}
