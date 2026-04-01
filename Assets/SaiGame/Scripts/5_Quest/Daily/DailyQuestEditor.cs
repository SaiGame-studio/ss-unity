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

        private bool showCurrentData = true;
        private bool showDaysList = true;
        private bool showTodayData = true;
        private bool showUtilityButtons = true;

        // Pool dropdown state
        private DailyQuestPoolData[] loadedPools = null;
        private string[] poolDisplayOptions = null;
        private int selectedPoolIndex = -1;
        private bool isLoadingPools = false;

        // Track loading state for the assign-ahead button
        private bool isLoading = false;
        // Track loading state for the today quest button
        private bool isTodayLoading = false;
        // Track per-quest action loading state (questDefinitionId)
        private string startingQuestId = null;
        private string checkingQuestId = null;
        private string claimingQuestId = null;

        private void OnEnable()
        {
            this.dailyQuest = (DailyQuest)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.dqPoolId = serializedObject.FindProperty("dqPoolId");
            this.daysAhead = serializedObject.FindProperty("daysAhead");
            this.SyncDropdownSelectionFromProperty();
        }

        // Finds the dropdown index that matches the current dqPoolId string value.
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Daily Quest Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin,
                new GUIContent("Auto Load on Login", "Automatically assign-ahead when user logs in (requires Pool ID to be set)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Request Settings", EditorStyles.boldLabel);

            // Pool ID dropdown row
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

            // Show raw ID for reference when dropdown is active
            if (this.loadedPools != null && this.selectedPoolIndex >= 0)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("ID", this.dqPoolId.stringValue, EditorStyles.miniLabel);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(this.daysAhead, new GUIContent("Days Ahead", "Number of days to assign ahead"));

            // Selected pool details
            if (this.loadedPools != null && this.selectedPoolIndex >= 0 && this.selectedPoolIndex < this.loadedPools.Length)
                this.DrawSelectedPoolInfo(this.loadedPools[this.selectedPoolIndex]);

            EditorGUILayout.Space();

            // Current Today Quest Data
            TodayQuestResponse todayData = this.dailyQuest.CurrentTodayQuestResponse;
            this.showTodayData = EditorGUILayout.Foldout(this.showTodayData, "Today Quest Data", true);
            if (this.showTodayData)
            {
                EditorGUI.indentLevel++;
                if (todayData != null)
                    this.DrawTodayQuestData(todayData);
                else
                    EditorGUILayout.HelpBox("No today quest data loaded yet.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Current Assign-Ahead Data
            this.showCurrentData = EditorGUILayout.Foldout(this.showCurrentData, "Current Daily Quest Data", true);
            if (this.showCurrentData)
            {
                EditorGUI.indentLevel++;

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

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = this.isTodayLoading ? Color.gray : new Color(1f, 0.85f, 0.2f);
                EditorGUI.BeginDisabledGroup(this.isTodayLoading);
                if (GUILayout.Button(this.isTodayLoading ? "Loading..." : "Today Quest", GUILayout.Height(30)))
                    this.RunGetTodayQuests();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                bool canAssign = this.SelectedPoolSupportsAssignAhead();
                bool assignDisabled = this.isLoading || !canAssign;
                string assignLabel = this.isLoading ? "Loading..."
                    : canAssign ? "Assign Ahead"
                    : "Assign Ahead (N/A)";
                GUI.backgroundColor = assignDisabled ? Color.gray : Color.cyan;
                EditorGUI.BeginDisabledGroup(assignDisabled);
                if (GUILayout.Button(assignLabel, GUILayout.Height(30)))
                    this.RunAssignAhead();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Data", GUILayout.Height(30)))
                    this.dailyQuest.ClearData();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // Returns true only when the selected pool uses weighted_random strategy.
        private bool SelectedPoolSupportsAssignAhead()
        {
            if (this.loadedPools == null || this.selectedPoolIndex < 0 || this.selectedPoolIndex >= this.loadedPools.Length)
                return true; // no pools loaded yet — allow by default
            return this.loadedPools[this.selectedPoolIndex].assignment_strategy == "weighted_random";
        }

        private void DrawSelectedPoolInfo(DailyQuestPoolData pool)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };

            string activeTag = pool.is_active
                ? " <color=#00FF88>[active]</color>"
                : " <color=#FF4444>[inactive]</color>";
            EditorGUILayout.LabelField($"{pool.display_name}{activeTag}", richBold);

            if (!string.IsNullOrEmpty(pool.description))
                EditorGUILayout.LabelField(pool.description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Key: {pool.pool_key}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"ID: {pool.id}", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);

            string strategyColor = pool.assignment_strategy == "weighted_random" ? "#00FF88" : "#FFD700";
            EditorGUILayout.LabelField(
                $"Strategy: <color={strategyColor}><b>{pool.assignment_strategy}</b></color>  |  " +
                $"Slots/day: {pool.slots_per_day}  |  Reset UTC: {pool.reset_hour_utc}:00",
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

        private void DrawDaySummary(DailyDayData day)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true };
            string todayTag = day.is_today ? " <color=#FFD700>[TODAY]</color>" : "";
            string assignedTag = day.already_assigned ? " <color=#00FF88>(assigned)</color>" : " <color=#888888>(new)</color>";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{day.date}{todayTag}{assignedTag}", headerStyle);

            if (day.quests != null && day.quests.Length > 0)
            {
                EditorGUILayout.LabelField($"Quests: {day.quests.Length}");
                EditorGUI.indentLevel++;
                foreach (DailyQuestEntryData entry in day.quests)
                    this.DrawQuestEntry(entry);
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("No quests assigned.");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestEntry(DailyQuestEntryData entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (entry.quest != null)
            {
                QuestDefinitionData quest = entry.quest;
                EditorGUILayout.LabelField(quest.name, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {quest.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = quest.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Type: {quest.quest_type}  |  Active: {quest.is_active}");

                if (!string.IsNullOrEmpty(quest.description))
                    EditorGUILayout.LabelField($"Description: {quest.description}");

                if (quest.rewards != null && quest.rewards.Length > 0)
                {
                    EditorGUILayout.LabelField($"Rewards ({quest.rewards.Length}):");
                    foreach (QuestReward reward in quest.rewards)
                    {
                        if (reward.reward_type == "coin")
                            EditorGUILayout.LabelField($"  • coin × {reward.amount}");
                        else if (reward.reward_type == "item")
                            EditorGUILayout.LabelField($"  • item {reward.item_definition_id} × {reward.quantity_min}-{reward.quantity_max}");
                        else
                            EditorGUILayout.LabelField($"  • {reward.reward_type}");
                    }
                }
            }

            if (entry.assignment != null)
            {
                DailyAssignmentData a = entry.assignment;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Assignment ID: {a.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = a.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Expires: {a.expires_at}");
            }

            EditorGUILayout.EndVertical();
        }

        private void RunGetTodayQuests()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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

        private void DrawTodayQuestData(TodayQuestResponse data)
        {
            GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };

            EditorGUILayout.LabelField($"Date: <b>{data.assigned_date}</b>", richBold);
            EditorGUILayout.LabelField($"Entries: {data.entries?.Length ?? 0}");

            // Streak
            if (data.streak != null)
            {
                DailyStreakData s = data.streak;
                EditorGUILayout.LabelField(
                    $"Streak: <color=#FFD700><b>{s.current_streak}</b></color>  |  " +
                    $"Longest: {s.longest_streak}  |  " +
                    $"Completions: {s.total_completions}",
                    richBold);
            }

            // Entries
            if (data.entries != null && data.entries.Length > 0)
            {
                EditorGUI.indentLevel++;
                foreach (DailyQuestEntryData entry in data.entries)
                    this.DrawTodayQuestEntryWithActions(entry);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTodayQuestEntryWithActions(DailyQuestEntryData entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string questDefId = entry.quest?.id ?? entry.assignment?.quest_definition_id ?? string.Empty;

            if (entry.quest != null)
            {
                QuestDefinitionData quest = entry.quest;
                EditorGUILayout.LabelField(quest.name, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {quest.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = quest.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Type: {quest.quest_type}  |  Active: {quest.is_active}");

                if (!string.IsNullOrEmpty(quest.description))
                    EditorGUILayout.LabelField($"Description: {quest.description}");

                if (quest.rewards != null && quest.rewards.Length > 0)
                {
                    EditorGUILayout.LabelField($"Rewards ({quest.rewards.Length}):");
                    foreach (QuestReward reward in quest.rewards)
                    {
                        if (reward.reward_type == "coin")
                            EditorGUILayout.LabelField($"  \u2022 coin \u00d7 {reward.amount}");
                        else if (reward.reward_type == "item")
                            EditorGUILayout.LabelField($"  \u2022 item {reward.item_definition_id} \u00d7 {reward.quantity_min}-{reward.quantity_max}");
                        else
                            EditorGUILayout.LabelField($"  \u2022 {reward.reward_type}");
                    }
                }
            }

            if (entry.assignment != null)
            {
                DailyAssignmentData a = entry.assignment;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Assignment ID: {a.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = a.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Expires: {a.expires_at}");
            }

            // Status badge
            if (!string.IsNullOrEmpty(entry.status))
            {
                GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true };
                string statusColor;
                switch (entry.status)
                {
                    case "completed":   statusColor = "#00FF88"; break;
                    case "claimed":     statusColor = "#FFD700"; break;
                    case "in_progress": statusColor = "#66CCFF"; break;
                    default:            statusColor = "#AAAAAA"; break; // not_started
                }
                EditorGUILayout.LabelField(
                    $"Status: <color={statusColor}><b>{entry.status}</b></color>",
                    statusStyle);
            }

            // Action buttons
            if (!string.IsNullOrEmpty(questDefId))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();

                bool isStarting = this.startingQuestId == questDefId;
                bool isChecking = this.checkingQuestId == questDefId;
                bool isClaiming = this.claimingQuestId == questDefId;
                bool anyBusy = isStarting || isChecking || isClaiming;

                GUI.backgroundColor = isStarting ? Color.gray : new Color(1f, 0.82f, 0.2f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isStarting ? "Starting..." : "Start", GUILayout.Height(22)))
                    this.RunStartQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = isChecking ? Color.gray : new Color(0.4f, 0.8f, 1f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isChecking ? "Checking..." : "Check", GUILayout.Height(22)))
                    this.RunCheckQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = isClaiming ? Color.gray : new Color(0.4f, 1f, 0.6f);
                EditorGUI.BeginDisabledGroup(anyBusy);
                if (GUILayout.Button(isClaiming ? "Claiming..." : "Claim", GUILayout.Height(22)))
                    this.RunClaimQuest(questDefId);
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void RunStartQuest(string questDefinitionId)
        {
            if (SaiService.Instance == null || SaiService.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.startingQuestId = questDefinitionId;
            Repaint();

            SaiService.Instance.QuestProgressor.StartQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.startingQuestId = null;
                    Debug.Log($"[DailyQuestEditor] Quest started: id={response.id}, status={response.status}");
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
            if (SaiService.Instance == null || SaiService.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.checkingQuestId = questDefinitionId;
            Repaint();

            SaiService.Instance.QuestProgressor.CheckQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.checkingQuestId = null;
                    Debug.Log($"[DailyQuestEditor] Quest checked: quest={response.quest_definition?.id}");
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
            if (SaiService.Instance == null || SaiService.Instance.QuestProgressor == null)
            {
                Debug.LogError("[DailyQuestEditor] QuestProgressor not found!");
                return;
            }

            this.claimingQuestId = questDefinitionId;
            Repaint();

            SaiService.Instance.QuestProgressor.ClaimQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.claimingQuestId = null;
                    Debug.Log($"[DailyQuestEditor] Quest claimed: id={response.id}, claimed_at={response.claimed_at}");
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
            if (SaiService.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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
            if (SaiService.Instance == null)
            {
                Debug.LogError("[DailyQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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

                    // Build display labels: "display_name (pool_key)"
                    this.poolDisplayOptions = new string[this.loadedPools.Length];
                    for (int i = 0; i < this.loadedPools.Length; i++)
                    {
                        DailyQuestPoolData p = this.loadedPools[i];
                        this.poolDisplayOptions[i] = $"{p.display_name}  ({p.pool_key})";
                    }

                    this.SyncDropdownSelectionFromProperty();

                    // If nothing matched, default to first entry
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
    }
}
