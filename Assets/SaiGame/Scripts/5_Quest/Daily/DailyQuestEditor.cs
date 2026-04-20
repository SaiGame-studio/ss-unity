using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(DailyQuest))]
    [CanEditMultipleObjects]
    public class DailyQuestEditor : Editor
    {
        private DailyQuest dailyQuest;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty dqPoolId;
        private SerializedProperty daysAhead;

        private bool showCurrentData = false;
        private bool showDaysList = true;
        private bool showTodayData = false;
        private bool showUtilityButtons = true;

        // Pool dropdown state
        private DailyQuestPoolData[] loadedPools = null;
        private string[] poolDisplayOptions = null;
        private int selectedPoolIndex = -1;
        private bool isLoadingPools = false;

        // Loading states
        private bool isLoading = false;          // Assign-ahead
        private bool isTodayLoading = false;     // Today Quest
        private string startingQuestId = null;
        private string checkingQuestId = null;
        private string claimingQuestId = null;

        // Per-day collapse state (keyed by date string)
        private readonly Dictionary<string, bool> expandedDays = new Dictionary<string, bool>();
        // Per-quest-entry collapse state (keyed by date|questDefId for assign-ahead, today|questDefId for today)
        private readonly Dictionary<string, bool> expandedQuests = new Dictionary<string, bool>();

        private void OnEnable()
        {
            this.dailyQuest = (DailyQuest)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.dqPoolId = serializedObject.FindProperty("dqPoolId");
            this.daysAhead = serializedObject.FindProperty("daysAhead");
            this.SyncDropdownSelectionFromProperty();

            if (this.dailyQuest != null)
                this.dailyQuest.OnGetPoolsSuccess += this.HandlePoolsLoaded;
        }

        private void OnDisable()
        {
            if (this.dailyQuest != null)
                this.dailyQuest.OnGetPoolsSuccess -= this.HandlePoolsLoaded;
        }

        private void HandlePoolsLoaded(DailyQuestPoolsResponse response)
        {
            this.loadedPools = response.pools ?? new DailyQuestPoolData[0];

            this.poolDisplayOptions = new string[this.loadedPools.Length];
            for (int i = 0; i < this.loadedPools.Length; i++)
            {
                DailyQuestPoolData p = this.loadedPools[i];
                this.poolDisplayOptions[i] = $"{p.display_name}  ({p.pool_key})";
            }

            this.SyncDropdownSelectionFromProperty();

            if (this.selectedPoolIndex < 0 && this.loadedPools.Length > 0)
            {
                this.selectedPoolIndex = 0;
                this.dqPoolId.stringValue = this.loadedPools[0].id;
                serializedObject.ApplyModifiedProperties();
            }

            Repaint();
        }

        private void SyncDropdownSelectionFromProperty()
        {
            if (this.loadedPools == null || this.loadedPools.Length == 0)
            {
                this.selectedPoolIndex = -1;
                return;
            }

            string currentId = this.dqPoolId.stringValue;
            this.selectedPoolIndex = -1;
            for (int i = 0; i < this.loadedPools.Length; i++)
            {
                if (this.loadedPools[i].id == currentId)
                {
                    this.selectedPoolIndex = i;
                    break;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load pools when user logs in"));

            EditorGUILayout.Space();

            this.DrawPoolDropdownRow();

            // Selected pool details
            if (this.loadedPools != null && this.selectedPoolIndex >= 0 && this.selectedPoolIndex < this.loadedPools.Length)
                this.DrawSelectedPoolInfo(this.loadedPools[this.selectedPoolIndex]);

            EditorGUILayout.Space();

            // Current Today Quest Data
            TodayQuestResponse todayData = this.dailyQuest.CurrentTodayQuestResponse;
            this.showTodayData = EditorGUILayout.Foldout(this.showTodayData, "Today Quests", true);
            if (this.showTodayData)
            {
                EditorGUI.indentLevel++;

                // Today Quest action button (lives inside this section)
                bool hasPoolId = !string.IsNullOrEmpty(this.dqPoolId.stringValue);
                bool todayDisabled = this.isTodayLoading || !hasPoolId;
                string todayLabel = this.isTodayLoading ? "Loading..."
                    : hasPoolId ? "Today Quest"
                    : "Today Quest (no pool)";
                GUI.backgroundColor = todayDisabled ? Color.gray : new Color(1f, 0.85f, 0.2f);
                EditorGUI.BeginDisabledGroup(todayDisabled);
                if (GUILayout.Button(todayLabel, GUILayout.Height(28)))
                    this.RunGetTodayQuests();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(2);

                if (todayData != null)
                    this.DrawTodayQuestData(todayData);
                else
                    EditorGUILayout.HelpBox("No today quest data loaded yet.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Current Assign-Ahead Data
            this.showCurrentData = EditorGUILayout.Foldout(this.showCurrentData, "Quests Assign Ahead", true);
            if (this.showCurrentData)
            {
                EditorGUI.indentLevel++;

                // Days Ahead input + Assign Ahead button on the same row
                bool hasPoolIdForAssign = !string.IsNullOrEmpty(this.dqPoolId.stringValue);
                bool canAssign = this.SelectedPoolSupportsAssignAhead();
                bool assignDisabled = this.isLoading || !canAssign || !hasPoolIdForAssign;
                string assignLabel = this.isLoading ? "Loading..."
                    : !hasPoolIdForAssign ? "Assign Ahead (no pool)"
                    : canAssign ? "Assign Ahead"
                    : "Assign Ahead (N/A)";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(this.daysAhead, new GUIContent("Days Ahead", "Number of days to assign ahead"));
                GUI.backgroundColor = assignDisabled ? Color.gray : Color.cyan;
                EditorGUI.BeginDisabledGroup(assignDisabled);
                if (GUILayout.Button(assignLabel, GUILayout.Width(160), GUILayout.Height(20)))
                    this.RunAssignAhead();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                AssignAheadResponse data = this.dailyQuest.CurrentAssignAheadResponse;
                if (data != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Pool ID: {data.pool_id}");
                    EditorGUILayout.LabelField($"Days Ahead: {data.days_ahead}");
                    EditorGUILayout.LabelField($"Period: {data.start_date}  →  {data.end_date}");
                    EditorGUILayout.LabelField($"Days Loaded: {data.days?.Length ?? 0}");

                    if (data.days != null && data.days.Length > 0)
                    {
                        this.showDaysList = EditorGUILayout.Foldout(this.showDaysList, $"Days ({data.days.Length})", true);
                        if (this.showDaysList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (DailyDayData day in data.days)
                                this.DrawDaySummary(day);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No daily quest data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            EditorGUI.indentLevel++;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Data", GUILayout.Height(30)))
                this.dailyQuest.ClearData();
            GUI.backgroundColor = Color.white;

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Local data is automatically cleared on SaiAuth logout.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Pool Dropdown Row ─────────────────────────────────────────────────

        private void DrawPoolDropdownRow()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Pool ID", "The daily quest pool used for assign-ahead"));

            if (this.loadedPools != null && this.loadedPools.Length > 0)
            {
                int newIndex = EditorGUILayout.Popup(this.selectedPoolIndex < 0 ? 0 : this.selectedPoolIndex, this.poolDisplayOptions);
                if (newIndex != this.selectedPoolIndex || this.dqPoolId.stringValue != this.loadedPools[newIndex].id)
                {
                    this.selectedPoolIndex = newIndex;
                    this.dqPoolId.stringValue = this.loadedPools[newIndex].id;
                }
            }
            else
            {
                string currentId = this.dqPoolId.stringValue;
                string preview = string.IsNullOrEmpty(currentId) ? "— load pools first —" : currentId;
                EditorGUILayout.LabelField(preview, EditorStyles.helpBox);
            }

            GUI.backgroundColor = this.isLoadingPools ? Color.gray : new Color(0.4f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(this.isLoadingPools);
            if (GUILayout.Button(this.isLoadingPools ? "..." : "Load Pools", GUILayout.Width(80), GUILayout.Height(18)))
                this.LoadPools();
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private bool SelectedPoolSupportsAssignAhead()
        {
            if (this.loadedPools == null || this.selectedPoolIndex < 0 || this.selectedPoolIndex >= this.loadedPools.Length)
                return true;
            return this.loadedPools[this.selectedPoolIndex].assignment_strategy == "weighted_random";
        }

        // ── Selected Pool Info Card ───────────────────────────────────────────

        private void DrawSelectedPoolInfo(DailyQuestPoolData pool)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header: name + active badge
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 12;
            headerStyle.normal.textColor = new Color(0.9f, 0.9f, 1f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"★ {pool.display_name}", headerStyle, GUILayout.ExpandWidth(true));

            GUIStyle badgeStyle = new GUIStyle(EditorStyles.label);
            badgeStyle.fontSize = 11;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleRight;
            badgeStyle.normal.textColor = pool.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(pool.is_active ? "ACTIVE" : "INACTIVE", badgeStyle, GUILayout.MinWidth(70));
            EditorGUILayout.EndHorizontal();

            this.DrawSeparator();

            if (!string.IsNullOrEmpty(pool.description))
            {
                GUIStyle descStyle = new GUIStyle(EditorStyles.label);
                descStyle.fontSize = 10;
                descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                descStyle.wordWrap = true;
                descStyle.fontStyle = FontStyle.Italic;
                EditorGUILayout.LabelField(pool.description, descStyle);
            }

            // Compact info
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField($"KEY: {pool.pool_key}", labelStyle);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {pool.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = pool.id;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Strategy + slots + reset (rich)
            GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };
            richBold.fontSize = 11;
            string strategyColor = pool.assignment_strategy == "weighted_random" ? "#00FF88" : "#FFD700";
            EditorGUILayout.LabelField(
                $"⚙ Strategy: <color={strategyColor}><b>{pool.assignment_strategy}</b></color>  |  " +
                $"Slots/day: <b>{pool.slots_per_day}</b>  |  Reset UTC: <b>{pool.reset_hour_utc}:00</b>",
                richBold);

            if (pool.assignment_strategy != "weighted_random")
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    $"Strategy '{pool.assignment_strategy}' does not support Assign Ahead.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        // ── Day Card ──────────────────────────────────────────────────────────

        private void DrawDaySummary(DailyDayData day)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string dayKey = day.date ?? "unknown";
            if (!this.expandedDays.ContainsKey(dayKey))
                this.expandedDays[dayKey] = false;

            // Header: date + TODAY badge + assigned badge (right-aligned)
            int questCount = day.quests?.Length ?? 0;
            string headerLabel = $"📅 {day.date}  [{questCount}]";

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            Color titleColor = day.is_today ? new Color(1f, 0.84f, 0.2f) : new Color(0.85f, 0.85f, 0.85f);
            foldoutStyle.normal.textColor = titleColor;
            foldoutStyle.onNormal.textColor = titleColor;
            foldoutStyle.focused.textColor = titleColor;
            foldoutStyle.onFocused.textColor = titleColor;
            foldoutStyle.active.textColor = titleColor;
            foldoutStyle.onActive.textColor = titleColor;

            EditorGUILayout.BeginHorizontal();
            this.expandedDays[dayKey] = EditorGUILayout.Foldout(this.expandedDays[dayKey], headerLabel, true, foldoutStyle);

            // Right-aligned tags
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.label);
            badgeStyle.fontSize = 10;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleRight;

            if (day.is_today)
            {
                badgeStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
                EditorGUILayout.LabelField("TODAY", badgeStyle, GUILayout.Width(70));
            }
            badgeStyle.normal.textColor = day.already_assigned ? new Color(0.3f, 1f, 0.5f) : new Color(0.55f, 0.55f, 0.55f);
            EditorGUILayout.LabelField(day.already_assigned ? "ASSIGNED" : "NEW", badgeStyle, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            if (!this.expandedDays[dayKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            this.DrawSeparator();

            if (day.quests != null && day.quests.Length > 0)
            {
                foreach (DailyQuestEntryData entry in day.quests)
                {
                    string entryKey = $"{dayKey}|{entry.quest?.id ?? entry.assignment?.quest_definition_id ?? string.Empty}";
                    this.DrawQuestEntry(entry, entryKey, withActions: false);
                }
            }
            else
            {
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.label);
                emptyStyle.fontSize = 10;
                emptyStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                emptyStyle.fontStyle = FontStyle.Italic;
                EditorGUILayout.LabelField("No quests assigned.", emptyStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ── Today Quest Data ──────────────────────────────────────────────────

        private void DrawTodayQuestData(TodayQuestResponse data)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 12;
            headerStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
            EditorGUILayout.LabelField($"📅 {data.assigned_date}  [{data.entries?.Length ?? 0}]", headerStyle);

            this.DrawSeparator();

            // Pool snapshot (from response)
            if (data.pool != null)
            {
                GUIStyle poolHeader = new GUIStyle(EditorStyles.boldLabel);
                poolHeader.fontSize = 10;
                poolHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("🗂 POOL", poolHeader);

                GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };
                richBold.fontSize = 10;
                string strategyColor = data.pool.assignment_strategy == "weighted_random" ? "#00FF88" : "#FFD700";
                string activeTag = data.pool.is_active ? " <color=#00FF88>[active]</color>" : " <color=#FF4444>[inactive]</color>";
                EditorGUILayout.LabelField($"<b>{data.pool.display_name}</b>{activeTag}", richBold);

                GUIStyle dimStyle = new GUIStyle(EditorStyles.label);
                dimStyle.fontSize = 9;
                dimStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                if (!string.IsNullOrEmpty(data.pool.description)) EditorGUILayout.LabelField(data.pool.description, dimStyle);
                if (!string.IsNullOrEmpty(data.pool.pool_key)) EditorGUILayout.LabelField($"Key: {data.pool.pool_key}", dimStyle);
                if (!string.IsNullOrEmpty(data.pool.id)) EditorGUILayout.LabelField($"ID: {data.pool.id}", dimStyle);

                EditorGUILayout.LabelField(
                    $"Strategy: <color={strategyColor}><b>{data.pool.assignment_strategy}</b></color>  |  " +
                    $"Slots/day: <b>{data.pool.slots_per_day}</b>  |  Reset UTC: <b>{data.pool.reset_hour_utc}:00</b>",
                    new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 });

                EditorGUILayout.Space(3);
            }

            // Streak info
            if (data.streak != null)
            {
                DailyStreakData s = data.streak;

                GUIStyle streakHeader = new GUIStyle(EditorStyles.boldLabel);
                streakHeader.fontSize = 10;
                streakHeader.normal.textColor = new Color(1f, 0.7f, 0.4f);
                EditorGUILayout.LabelField("🔥 STREAK", streakHeader);

                GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
                richStyle.fontSize = 11;
                EditorGUILayout.LabelField(
                    $"Current: <color=#FFD700><b>{s.current_streak}</b></color>  |  " +
                    $"Longest: <b>{s.longest_streak}</b>  |  " +
                    $"Completions: <b>{s.total_completions}</b>  |  " +
                    $"Version: <b>{s.version}</b>",
                    richStyle);

                GUIStyle dimStyle = new GUIStyle(EditorStyles.label);
                dimStyle.fontSize = 9;
                dimStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                if (!string.IsNullOrEmpty(s.id)) EditorGUILayout.LabelField($"ID: {s.id}", dimStyle);
                if (!string.IsNullOrEmpty(s.created_at)) EditorGUILayout.LabelField($"Created: {s.created_at}  |  Updated: {s.updated_at}", dimStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            // Entries
            if (data.entries != null && data.entries.Length > 0)
            {
                foreach (DailyQuestEntryData entry in data.entries)
                {
                    string entryKey = $"today|{entry.quest?.id ?? entry.assignment?.quest_definition_id ?? string.Empty}";
                    this.DrawQuestEntry(entry, entryKey, withActions: true);
                }
            }
        }

        // ── Quest Entry Card (used by both day list and today list) ───────────

        private void DrawQuestEntry(DailyQuestEntryData entry, string entryKey, bool withActions)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string questDefId = entry.quest?.id ?? entry.assignment?.quest_definition_id ?? string.Empty;
            string questName = entry.quest?.name ?? questDefId;

            if (!this.expandedQuests.ContainsKey(entryKey))
                this.expandedQuests[entryKey] = false;

            // Collapsible header: quest name + status badge
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 12;
            foldoutStyle.fontStyle = FontStyle.Bold;
            Color titleColor = new Color(0.9f, 0.9f, 1f);
            foldoutStyle.normal.textColor = titleColor;
            foldoutStyle.onNormal.textColor = titleColor;
            foldoutStyle.focused.textColor = titleColor;
            foldoutStyle.onFocused.textColor = titleColor;
            foldoutStyle.active.textColor = titleColor;
            foldoutStyle.onActive.textColor = titleColor;

            EditorGUILayout.BeginHorizontal();
            this.expandedQuests[entryKey] = EditorGUILayout.Foldout(this.expandedQuests[entryKey], $"◆ {questName}", true, foldoutStyle);

            if (!string.IsNullOrEmpty(entry.status))
            {
                GUIStyle statusBadge = new GUIStyle(EditorStyles.miniLabel);
                statusBadge.fontSize = 10;
                statusBadge.fontStyle = FontStyle.Bold;
                statusBadge.alignment = TextAnchor.MiddleRight;
                statusBadge.normal.textColor = QuestStatusIcons.GetColor(entry.status);
                GUILayout.Label($"{QuestStatusIcons.GetIcon(entry.status)} {entry.status.ToLower()}", statusBadge, GUILayout.ExpandWidth(false));
            }
            EditorGUILayout.EndHorizontal();

            if (!this.expandedQuests[entryKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }

            this.DrawSeparator();

            // Compact info
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            if (entry.quest != null)
            {
                QuestDefinitionData quest = entry.quest;
                EditorGUILayout.LabelField($"TYPE: {quest.quest_type?.ToUpper()}", labelStyle);
                EditorGUILayout.LabelField($"SORT: {quest.sort_order}  |  HIDDEN: {(quest.is_hidden ? "YES" : "NO")}", labelStyle);

                GUIStyle activeStyle = new GUIStyle(EditorStyles.label);
                activeStyle.fontSize = 10;
                activeStyle.fontStyle = FontStyle.Bold;
                activeStyle.normal.textColor = quest.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField($"STATUS: {(quest.is_active ? "ACTIVE" : "INACTIVE")}", activeStyle);

                this.DrawCodeAndIdRow(quest.code_name, quest.id);

                if (!string.IsNullOrEmpty(quest.description))
                {
                    GUIStyle descStyle = new GUIStyle(EditorStyles.label);
                    descStyle.fontSize = 10;
                    descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    descStyle.wordWrap = true;
                    descStyle.fontStyle = FontStyle.Italic;
                    EditorGUILayout.LabelField(quest.description, descStyle);
                }

                GUIStyle metaStyle = new GUIStyle(EditorStyles.label);
                metaStyle.fontSize = 9;
                metaStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
                if (!string.IsNullOrEmpty(quest.created_at))
                    EditorGUILayout.LabelField($"Created: {quest.created_at}  |  Updated: {quest.updated_at}", metaStyle);

                // Conditions
                this.DrawConditions(quest.conditions);

                int visibleRewardCount = this.CountVisibleRewards(quest.rewards);
                if (visibleRewardCount > 0)
                {
                    EditorGUILayout.Space(3);
                    GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                    sectionStyle.fontSize = 10;
                    sectionStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
                    EditorGUILayout.LabelField($"🎁 REWARDS ({visibleRewardCount})", sectionStyle);

                    foreach (QuestReward reward in quest.rewards)
                    {
                        if (this.IsHiddenReward(reward)) continue;
                        this.DrawReward(reward);
                    }
                }
            }

            // Assignment info
            if (entry.assignment != null)
            {
                EditorGUILayout.Space(3);
                GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionStyle.fontSize = 10;
                sectionStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("📌 ASSIGNMENT", sectionStyle);

                DailyAssignmentData a = entry.assignment;
                GUIStyle idStyle = new GUIStyle(EditorStyles.label);
                idStyle.fontSize = 10;
                idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Assignment ID: {a.id}", idStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = a.id;
                EditorGUILayout.EndHorizontal();

                GUIStyle dimStyle = new GUIStyle(EditorStyles.label);
                dimStyle.fontSize = 9;
                dimStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                this.DrawDimIdRow("Pool", a.pool_id, dimStyle);
                this.DrawDimIdRow("User", a.user_id, dimStyle);
                if (!string.IsNullOrEmpty(a.assigned_date)) EditorGUILayout.LabelField($"Assigned: {a.assigned_date}", dimStyle);
                if (!string.IsNullOrEmpty(a.created_at)) EditorGUILayout.LabelField($"Created: {a.created_at}", dimStyle);

                GUIStyle expireStyle = new GUIStyle(EditorStyles.label) { richText = true };
                expireStyle.fontSize = 10;
                EditorGUILayout.LabelField($"⏰ Expires: <color=#FF8888>{a.expires_at}</color>", expireStyle);
            }

            // Progress info
            if (entry.progress != null)
                this.DrawProgressBlock(entry.progress);

            // Action buttons
            if (withActions && !string.IsNullOrEmpty(questDefId))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();

                bool isStarting = this.startingQuestId == questDefId;
                bool isChecking = this.checkingQuestId == questDefId;
                bool isClaiming = this.claimingQuestId == questDefId;
                bool anyBusy = isStarting || isChecking || isClaiming;

                GUI.backgroundColor = isStarting ? Color.gray : new Color(1f, 0.82f, 0.2f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isStarting ? "▶ Starting..." : "▶ Start", GUILayout.Height(28)))
                    this.RunStartQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = isChecking ? Color.gray : new Color(0.4f, 0.8f, 1f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isChecking ? "🔄 Checking..." : "🔄 Check", GUILayout.Height(28)))
                    this.RunCheckQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = isClaiming ? Color.gray : new Color(0.4f, 1f, 0.6f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isClaiming ? "✓ Claiming..." : "✓ Claim", GUILayout.Height(28)))
                    this.RunClaimQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawReward(QuestReward reward)
        {
            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            richStyle.fontSize = 10;

            if (reward.reward_type == "item")
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  <color=#66CCFF>● item</color> {reward.item_definition_id} × <b>{reward.quantity_min}-{reward.quantity_max}</b>", richStyle);
                if (!string.IsNullOrEmpty(reward.item_definition_id) && GUILayout.Button("Copy", GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = reward.item_definition_id;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField($"  <color=#AAAAAA>●</color> {reward.reward_type}", richStyle);
            }
        }

        // Coin rewards are hidden — backend doesn't process them.
        private bool IsHiddenReward(QuestReward reward) => reward != null && reward.reward_type == "coin";

        private int CountVisibleRewards(QuestReward[] rewards)
        {
            if (rewards == null) return 0;
            int count = 0;
            foreach (QuestReward r in rewards)
                if (!this.IsHiddenReward(r)) count++;
            return count;
        }

        // ── Style helpers ─────────────────────────────────────────────────────

        private void DrawSeparator()
        {
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);
        }

        // ── Network actions ───────────────────────────────────────────────────

        private void RunGetTodayQuests()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[DailyQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.isTodayLoading = true;
            Repaint();

            this.dailyQuest.GetTodayQuests(
                onSuccess: response =>
                {
                    this.isTodayLoading = false;
                    Debug.Log($"[DailyQuestEditor] Today quests loaded: {response.entries?.Length ?? 0} entries for {response.assigned_date}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isTodayLoading = false;
                    Debug.LogError($"[DailyQuestEditor] Today quest failed: {error}");
                    Repaint();
                }
            );
        }

        private void RunStartQuest(string questDefinitionId)
        {
            if (SaiServer.Instance == null || SaiServer.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.startingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.StartQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.startingQuestId = null;
                    Debug.Log($"[DailyQuestEditor] Quest started: id={response.id}, status={response.status}");
                    this.ApplyEntryStatus(questDefinitionId, response.status);
                    Repaint();
                },
                onError: error =>
                {
                    this.startingQuestId = null;
                    Debug.LogError($"[DailyQuestEditor] Start quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        private void RunCheckQuest(string questDefinitionId)
        {
            if (SaiServer.Instance == null || SaiServer.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.checkingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.CheckQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.checkingQuestId = null;
                    this.ApplyCheckResponseToEntry(questDefinitionId, response);
                    Debug.Log($"[DailyQuestEditor] Quest checked: quest={response.quest_definition?.id}, status={response.status}");
                    Repaint();
                },
                onError: error =>
                {
                    this.checkingQuestId = null;
                    Debug.LogError($"[DailyQuestEditor] Check quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        private void RunClaimQuest(string questDefinitionId)
        {
            if (SaiServer.Instance == null || SaiServer.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.claimingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.ClaimQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.claimingQuestId = null;
                    Debug.Log($"[DailyQuestEditor] Quest claimed: id={response.id}, claimed_at={response.claimed_at}");
                    // Claim response has no status field — claimed_at is present, so the quest is "claimed".
                    this.ApplyEntryStatus(questDefinitionId, "claimed");
                    Repaint();
                },
                onError: error =>
                {
                    this.claimingQuestId = null;
                    Debug.LogError($"[DailyQuestEditor] Claim quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        private void RunAssignAhead()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[DailyQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.isLoading = true;
            Repaint();

            this.dailyQuest.AssignAhead(
                onSuccess: response =>
                {
                    this.isLoading = false;
                    Debug.Log($"[DailyQuestEditor] Assign ahead success: {response.days?.Length ?? 0} days ({response.start_date} → {response.end_date})");
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoading = false;
                    Debug.LogError($"[DailyQuestEditor] Assign ahead failed: {error}");
                    Repaint();
                }
            );
        }

        private void LoadPools()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[DailyQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.isLoadingPools = true;
            Repaint();

            this.dailyQuest.GetPools(
                onSuccess: response =>
                {
                    this.isLoadingPools = false;
                    this.loadedPools = response.pools ?? new DailyQuestPoolData[0];

                    this.poolDisplayOptions = new string[this.loadedPools.Length];
                    for (int i = 0; i < this.loadedPools.Length; i++)
                    {
                        DailyQuestPoolData p = this.loadedPools[i];
                        this.poolDisplayOptions[i] = $"{p.display_name}  ({p.pool_key})";
                    }

                    this.SyncDropdownSelectionFromProperty();

                    if (this.selectedPoolIndex < 0 && this.loadedPools.Length > 0)
                    {
                        this.selectedPoolIndex = 0;
                        this.dqPoolId.stringValue = this.loadedPools[0].id;
                        serializedObject.ApplyModifiedProperties();
                    }

                    Debug.Log($"[DailyQuestEditor] Loaded {this.loadedPools.Length} pools");
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoadingPools = false;
                    Debug.LogError($"[DailyQuestEditor] Failed to load pools: {error}");
                    Repaint();
                }
            );
        }

        // ── CODE + ID row helper ──────────────────────────────────────────────

        /// <summary>
        /// Draws "CODE: xxx [Copy]" and "ID: yyy [Copy]" on two separate rows.
        /// If code_name is empty, only the ID row is rendered.
        /// </summary>
        private void DrawCodeAndIdRow(string codeName, string id)
        {
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            if (!string.IsNullOrEmpty(codeName))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"CODE: {codeName}", idStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = codeName;
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(id))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {id}", idStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = id;
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Renders a "{label}: {id} [Copy]" row using the supplied dim style. No-op when id is empty.
        /// </summary>
        private void DrawDimIdRow(string label, string id, GUIStyle style)
        {
            if (string.IsNullOrEmpty(id)) return;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{label}: {id}", style);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = id;
            EditorGUILayout.EndHorizontal();
        }

        // ── Check API → entry sync ────────────────────────────────────────────

        /// <summary>
        /// Writes the new status back onto the matching <see cref="DailyQuestEntryData"/>
        /// in <c>CurrentTodayQuestResponse</c> so the inspector pill updates immediately
        /// after Start / Claim without waiting for a Check round-trip.
        /// </summary>
        private void ApplyEntryStatus(string questDefinitionId, string newStatus)
        {
            if (string.IsNullOrEmpty(questDefinitionId) || string.IsNullOrEmpty(newStatus)) return;

            TodayQuestResponse today = this.dailyQuest?.CurrentTodayQuestResponse;
            if (today?.entries == null) return;

            foreach (DailyQuestEntryData entry in today.entries)
            {
                string entryQuestId = entry?.quest?.id ?? entry?.assignment?.quest_definition_id;
                if (entryQuestId == questDefinitionId)
                {
                    entry.status = newStatus;
                    break;
                }
            }
        }

        /// <summary>
        /// Updates the matching entry in CurrentTodayQuestResponse with fresh data from a Check call.
        /// </summary>
        private void ApplyCheckResponseToEntry(string questDefinitionId, CheckQuestResponse response)
        {
            if (response == null) return;

            TodayQuestResponse today = this.dailyQuest?.CurrentTodayQuestResponse;
            if (today?.entries == null) return;

            foreach (DailyQuestEntryData entry in today.entries)
            {
                string entryQuestId = entry?.quest?.id ?? entry?.assignment?.quest_definition_id;
                if (entryQuestId != questDefinitionId) continue;

                if (response.quest_definition != null)
                    entry.quest = response.quest_definition;

                entry.progress = this.ToDailyProgress(response.progress);

                // Prefer progress.status as source of truth — backend's top-level
                // "status" can be stale relative to the actual progress record.
                if (!string.IsNullOrEmpty(entry.progress?.status))
                    entry.status = entry.progress.status;
                else if (!string.IsNullOrEmpty(response.status))
                    entry.status = response.status;

                break;
            }
        }

        private DailyQuestProgressData ToDailyProgress(CheckQuestProgressRecord src)
        {
            if (src == null) return null;
            return new DailyQuestProgressData
            {
                id = src.id,
                studio_id = src.studio_id,
                game_id = src.game_id,
                user_id = src.user_id,
                quest_definition_id = src.quest_definition_id,
                status = src.status,
                completed_at = src.completed_at,
                claimed_at = src.claimed_at,
                reset_at = src.reset_at,
                version = src.version,
                created_at = src.created_at,
                updated_at = src.updated_at,
                progress_data_json = src.progress_data_json,
            };
        }

        // ── Conditions ────────────────────────────────────────────────────────

        private void DrawConditions(QuestConditions conditions)
        {
            if (conditions == null || conditions.clauses == null || conditions.clauses.Length == 0) return;

            EditorGUILayout.Space(3);
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionStyle.fontSize = 10;
            sectionStyle.normal.textColor = new Color(1f, 0.7f, 0.4f);
            string op = string.IsNullOrEmpty(conditions.operator_type) ? "AND" : conditions.operator_type.ToUpper();
            EditorGUILayout.LabelField($"⚙ CONDITIONS [{op}] ({conditions.clauses.Length})", sectionStyle);

            GUIStyle clauseHeader = new GUIStyle(EditorStyles.label) { richText = true };
            clauseHeader.fontSize = 10;
            clauseHeader.fontStyle = FontStyle.Bold;
            clauseHeader.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            GUIStyle subStyle = new GUIStyle(EditorStyles.label) { richText = true };
            subStyle.fontSize = 9;
            subStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            foreach (QuestClause clause in conditions.clauses)
            {
                if (clause == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"  • <color=#AAAAAA>[{clause.clause_id}]</color> <color=#66CCFF>{clause.type}</color>", clauseHeader);

                if (clause.items != null && clause.items.Length > 0)
                {
                    foreach (QuestClauseItem item in clause.items)
                    {
                        if (item == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"      <color=#888888>item:</color> {item.item_definition_id}  <b>×{item.quantity}</b>", subStyle);
                        if (!string.IsNullOrEmpty(item.item_definition_id) && GUILayout.Button("Copy", GUILayout.Width(50)))
                            GUIUtility.systemCopyBuffer = item.item_definition_id;
                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (clause.packs != null && !string.IsNullOrEmpty(clause.packs.gacha_pack_id))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"      <color=#888888>gacha:</color> {clause.packs.gacha_pack_id}  <b>×{clause.packs.quantity}</b>", subStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50)))
                        GUIUtility.systemCopyBuffer = clause.packs.gacha_pack_id;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        // ── Progress Block ────────────────────────────────────────────────────

        private void DrawProgressBlock(DailyQuestProgressData p)
        {
            EditorGUILayout.Space(3);
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionStyle.fontSize = 10;
            sectionStyle.normal.textColor = new Color(0.6f, 1f, 0.8f);
            EditorGUILayout.LabelField("📈 PROGRESS", sectionStyle);

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            richStyle.fontSize = 10;

            string statusColor = QuestStatusIcons.GetHex(p.status);
            string statusIcon = QuestStatusIcons.GetIcon(p.status);
            EditorGUILayout.LabelField($"Status: <color={statusColor}><b>{statusIcon} {p.status}</b></color>  |  Version: <b>{p.version}</b>", richStyle);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Progress ID: {p.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = p.id;
            EditorGUILayout.EndHorizontal();

            GUIStyle dimStyle = new GUIStyle(EditorStyles.label);
            dimStyle.fontSize = 9;
            dimStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);

            if (!string.IsNullOrEmpty(p.completed_at)) EditorGUILayout.LabelField($"Completed: {p.completed_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.claimed_at)) EditorGUILayout.LabelField($"Claimed: {p.claimed_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.reset_at)) EditorGUILayout.LabelField($"Reset: {p.reset_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.created_at)) EditorGUILayout.LabelField($"Created: {p.created_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.updated_at)) EditorGUILayout.LabelField($"Updated: {p.updated_at}", dimStyle);

            if (!string.IsNullOrEmpty(p.progress_data_json))
                this.DrawProgressData(p.progress_data_json);
        }

        // ── Dynamic JSON renderer (for progress_data) ─────────────────────────

        private void DrawProgressData(string json)
        {
            EditorGUILayout.Space(2);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 10;
            headerStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
            EditorGUILayout.LabelField("📊 PROGRESS DATA", headerStyle);

            GUIStyle clauseHeader = new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10 };
            GUIStyle fieldLabel = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 };

            foreach (var clause in this.ParseTopLevelEntries(json))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"<color=#4DD0E1><b>{clause.Key}</b></color>", clauseHeader);

                if (clause.Value.TrimStart().StartsWith("{"))
                {
                    foreach (var field in this.ParseFlatObject(clause.Value))
                    {
                        bool isNumericProgress = field.Key == "opened" || field.Key == "required";
                        string valColor = isNumericProgress ? "#FFD700" : "#CCCCCC";
                        EditorGUILayout.LabelField(
                            $"  <color=#888888>{field.Key}:</color>  <color={valColor}>{field.Value}</color>",
                            fieldLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"  <color=#CCCCCC>{clause.Value}</color>", fieldLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
            ParseTopLevelEntries(string json)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }
                i++;

                int keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string key = json.Substring(keyStart, i - keyStart);
                i++;

                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':')) i++;
                string value = this.ReadJsonValue(json, ref i);
                result.Add(new System.Collections.Generic.KeyValuePair<string, string>(key, value));
            }
            return result;
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
            ParseFlatObject(string json)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '"') { i++; continue; }
                i++;

                int keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                string key = json.Substring(keyStart, i - keyStart);
                i++;

                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':')) i++;
                string value = this.ReadJsonValue(json, ref i);

                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);

                result.Add(new System.Collections.Generic.KeyValuePair<string, string>(key, value));
            }
            return result;
        }

        private string ReadJsonValue(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return "";

            if (json[i] == '{' || json[i] == '[')
            {
                char open = json[i];
                char close = open == '{' ? '}' : ']';
                int start = i, depth = 0;
                while (i < json.Length)
                {
                    if (json[i] == open) depth++;
                    else if (json[i] == close) { depth--; if (depth == 0) { i++; break; } }
                    i++;
                }
                return json.Substring(start, i - start);
            }
            if (json[i] == '"')
            {
                int start = i++;
                while (i < json.Length && json[i] != '"') { if (json[i] == '\\') i++; i++; }
                i++;
                return json.Substring(start, i - start);
            }
            {
                int start = i;
                while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']') i++;
                return json.Substring(start, i - start).Trim();
            }
        }
    }
}
