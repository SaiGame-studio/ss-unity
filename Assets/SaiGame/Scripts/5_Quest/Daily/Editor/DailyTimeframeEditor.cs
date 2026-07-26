using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(DailyTimeframe))]
    [CanEditMultipleObjects]
    public class DailyTimeframeEditor : Editor
    {
        private DailyTimeframe dailyTimeframe;
        private SerializedProperty poolKey;
        private SerializedProperty timeframePreset;
        private SerializedProperty startDate;
        private SerializedProperty endDate;

        private bool isLoading;
        private bool isLoadingPools;
        private bool showCurrentData = true;
        private DailyQuest dailyQuest;
        private DailyQuestPoolsResponse poolsResponse;
        private DailyQuestPoolData[] loadedPools = new DailyQuestPoolData[0];
        private string[] poolDisplayOptions = new string[0];
        private int selectedPoolIndex = -1;
        private readonly Dictionary<string, bool> expandedDays = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> expandedQuests = new Dictionary<string, bool>();

        private void OnEnable()
        {
            this.dailyTimeframe = (DailyTimeframe)target;
            this.poolKey = serializedObject.FindProperty("poolKey");
            this.timeframePreset = serializedObject.FindProperty("timeframePreset");
            this.startDate = serializedObject.FindProperty("startDate");
            this.endDate = serializedObject.FindProperty("endDate");
            this.ApplyPresetDates((DailyTimeframe.TimeframePreset)this.timeframePreset.enumValueIndex);
            this.RegisterDailyQuest();
            this.RefreshPoolsFromDailyQuest();
        }

        private void OnDisable()
        {
            if (this.dailyQuest != null)
                this.dailyQuest.OnGetPoolsSuccess -= this.HandlePoolsLoaded;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            this.RegisterDailyQuest();
            this.RefreshPoolsFromDailyQuest();
            this.DrawPoolKeyRow();

            DailyTimeframe.TimeframePreset oldPreset = (DailyTimeframe.TimeframePreset)this.timeframePreset.enumValueIndex;
            EditorGUILayout.PropertyField(this.timeframePreset, new GUIContent("Timeframe", "Select a date preset for the request"));
            DailyTimeframe.TimeframePreset newPreset = (DailyTimeframe.TimeframePreset)this.timeframePreset.enumValueIndex;
            if (oldPreset != newPreset)
                this.ApplyPresetDates(newPreset);

            EditorGUILayout.PropertyField(this.startDate, new GUIContent("Start Date", "Inclusive date in YYYY-MM-DD format"));
            EditorGUILayout.PropertyField(this.endDate, new GUIContent("End Date", "Inclusive date in YYYY-MM-DD format"));

            EditorGUILayout.Space(4);
            bool canLoad = !this.isLoading && !string.IsNullOrEmpty(this.poolKey.stringValue) &&
                           !string.IsNullOrEmpty(this.startDate.stringValue) && !string.IsNullOrEmpty(this.endDate.stringValue);
            GUI.backgroundColor = canLoad ? new Color(0.25f, 0.85f, 1f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!canLoad);
            if (GUILayout.Button(this.isLoading ? "Loading..." : "Get Daily Timeframe", GUILayout.Height(28)))
                this.LoadTimeframe();
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            this.showCurrentData = EditorGUILayout.Foldout(this.showCurrentData, "Current Daily Timeframe Data", true);
            if (this.showCurrentData)
                this.DrawCurrentData(this.dailyTimeframe.CurrentResponse);

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("Clear Data", GUILayout.Height(24)))
                this.dailyTimeframe.ClearData();
            GUI.backgroundColor = Color.white;

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyPresetDates(DailyTimeframe.TimeframePreset preset)
        {
            DateTime today = DateTime.Today;
            DateTime start;
            DateTime end;

            if (preset == DailyTimeframe.TimeframePreset.ThisMonth)
            {
                start = new DateTime(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
            }
            else
            {
                int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                start = today.AddDays(-daysFromMonday);
                end = start.AddDays(6);
            }

            this.startDate.stringValue = start.ToString("yyyy-MM-dd");
            this.endDate.stringValue = end.ToString("yyyy-MM-dd");
        }

        private void RegisterDailyQuest()
        {
            DailyQuest currentDailyQuest = this.dailyTimeframe.DailyQuestSource;
            if (this.dailyQuest == currentDailyQuest) return;

            if (this.dailyQuest != null)
                this.dailyQuest.OnGetPoolsSuccess -= this.HandlePoolsLoaded;

            this.dailyQuest = currentDailyQuest;
            if (this.dailyQuest != null)
                this.dailyQuest.OnGetPoolsSuccess += this.HandlePoolsLoaded;
        }

        private void RefreshPoolsFromDailyQuest()
        {
            DailyQuestPoolsResponse response = this.dailyQuest?.CurrentPoolsResponse;
            if (response != null && this.poolsResponse != response)
                this.HandlePoolsLoaded(response);
        }

        private void HandlePoolsLoaded(DailyQuestPoolsResponse response)
        {
            this.poolsResponse = response;
            this.loadedPools = response.pools ?? new DailyQuestPoolData[0];
            this.poolDisplayOptions = new string[this.loadedPools.Length];
            for (int index = 0; index < this.loadedPools.Length; index++)
            {
                DailyQuestPoolData pool = this.loadedPools[index];
                this.poolDisplayOptions[index] = string.Format("{0} ({1})", pool.display_name, pool.pool_key);
            }

            this.selectedPoolIndex = -1;
            for (int index = 0; index < this.loadedPools.Length; index++)
            {
                if (this.loadedPools[index].pool_key == this.poolKey.stringValue)
                {
                    this.selectedPoolIndex = index;
                    break;
                }
            }

            if (this.selectedPoolIndex < 0 && this.loadedPools.Length > 0)
            {
                this.SelectDailyQuestPoolById();
                if (this.selectedPoolIndex < 0)
                {
                    this.selectedPoolIndex = 0;
                    this.poolKey.stringValue = this.loadedPools[0].pool_key;
                }
            }

            Repaint();
        }

        private void SelectDailyQuestPoolById()
        {
            if (this.dailyQuest == null) return;

            string dailyQuestPoolId = this.dailyQuest.GetDqPoolId();
            for (int index = 0; index < this.loadedPools.Length; index++)
            {
                if (this.loadedPools[index].id != dailyQuestPoolId) continue;

                this.selectedPoolIndex = index;
                this.poolKey.stringValue = this.loadedPools[index].pool_key;
                return;
            }
        }

        private void DrawPoolKeyRow()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Pool Key", "Pool key mapped from DailyQuest's loaded pools"));

            if (this.loadedPools.Length > 0)
            {
                int currentIndex = Mathf.Clamp(this.selectedPoolIndex, 0, this.loadedPools.Length - 1);
                int newIndex = EditorGUILayout.Popup(currentIndex, this.poolDisplayOptions);
                if (newIndex != this.selectedPoolIndex)
                {
                    this.selectedPoolIndex = newIndex;
                    this.poolKey.stringValue = this.loadedPools[newIndex].pool_key;
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Popup(0, new[] { "— no pools loaded —" });
                EditorGUI.EndDisabledGroup();
            }

            GUI.backgroundColor = this.isLoadingPools ? Color.gray : new Color(0.4f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(this.isLoadingPools || this.dailyQuest == null);
            if (GUILayout.Button(this.isLoadingPools ? "..." : "Load Pools", GUILayout.Width(80), GUILayout.Height(18)))
                this.LoadPoolsFromDailyQuest();
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (this.dailyQuest == null)
                EditorGUILayout.HelpBox("DailyQuest service was not found on SaiServer.", MessageType.Warning);
        }

        private void LoadPoolsFromDailyQuest()
        {
            this.isLoadingPools = true;
            this.dailyQuest.GetPools(
                onSuccess: response =>
                {
                    this.isLoadingPools = false;
                    this.HandlePoolsLoaded(response);
                    Debug.Log(string.Format("<color=#66CCFF>[DailyTimeframeEditor] Reused {0} pools from DailyQuest</color>", response.pools?.Length ?? 0));
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoadingPools = false;
                    Debug.LogWarning(string.Format("<color=#66CCFF>[DailyTimeframeEditor] Failed to load pools from DailyQuest: <color=#FF4444>{0}</color></color>", error));
                    Repaint();
                });
        }

        private void LoadTimeframe()
        {
            this.isLoading = true;
            this.dailyTimeframe.GetTimeframe(
                onSuccess: response =>
                {
                    this.isLoading = false;
                    Debug.Log(string.Format(
                        "<color=#66CCFF>[DailyTimeframeEditor] Loaded {0} days ({1} → {2})</color>",
                        response.days?.Length ?? 0, response.start_date, response.end_date));
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoading = false;
                    Debug.LogWarning(string.Format(
                        "<color=#66CCFF>[DailyTimeframeEditor] Failed to load timeframe: <color=#FF4444>{0}</color></color>",
                        error));
                    Repaint();
                });
        }

        private void DrawCurrentData(DailyTimeframeResponse response)
        {
            if (response == null)
            {
                EditorGUILayout.HelpBox("No daily timeframe data loaded yet.", MessageType.None);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 12;
            headerStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            EditorGUILayout.LabelField("DAILY QUEST TIMEFRAME", headerStyle);

            GUIStyle summaryStyle = new GUIStyle(EditorStyles.label);
            summaryStyle.fontSize = 10;
            summaryStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            EditorGUILayout.LabelField(string.Format("Period: {0} → {1}  |  Days: {2}", response.start_date, response.end_date, response.days?.Length ?? 0), summaryStyle);
            this.DrawCopyRow("Pool ID", response.pool_id, new Color(1f, 0.84f, 0f));
            EditorGUILayout.EndVertical();

            if (response.days != null)
            {
                foreach (DailyDayData day in response.days)
                    this.DrawDayCard(day);
            }
            else
                EditorGUILayout.HelpBox("The response does not contain any days.", MessageType.Info);
        }

        private void DrawDayCard(DailyDayData day)
        {
            if (day == null) return;

            string dayKey = day.date ?? "unknown";
            if (!this.expandedDays.ContainsKey(dayKey))
                this.expandedDays[dayKey] = day.is_today || day.already_assigned;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int questCount = day.quests?.Length ?? 0;
            GUIStyle foldoutStyle = this.CreateFoldoutStyle(day.is_today ? new Color(1f, 0.84f, 0.2f) : new Color(0.85f, 0.85f, 0.85f), 13);

            EditorGUILayout.BeginHorizontal();
            this.expandedDays[dayKey] = EditorGUILayout.Foldout(this.expandedDays[dayKey], string.Format("{0}  [{1} quests]", day.date, questCount), true, foldoutStyle);
            this.DrawDayBadges(day);
            EditorGUILayout.EndHorizontal();

            if (!this.expandedDays[dayKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
                return;
            }

            this.DrawSeparator();
            if (day.quests == null || day.quests.Length == 0)
            {
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.label);
                emptyStyle.fontSize = 10;
                emptyStyle.fontStyle = FontStyle.Italic;
                emptyStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                EditorGUILayout.LabelField("No quests assigned.", emptyStyle);
            }
            else
            {
                foreach (DailyQuestEntryData entry in day.quests)
                {
                    string questId = entry?.quest?.id ?? entry?.assignment?.quest_definition_id ?? "unknown";
                    this.DrawQuestCard(entry, string.Format("{0}|{1}", dayKey, questId));
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        private void DrawDayBadges(DailyDayData day)
        {
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeStyle.fontSize = 10;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleRight;

            if (day.is_today)
            {
                badgeStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
                GUILayout.Label("TODAY", badgeStyle, GUILayout.Width(55));
            }

            badgeStyle.normal.textColor = day.already_assigned ? new Color(0.3f, 1f, 0.5f) : new Color(0.55f, 0.55f, 0.55f);
            GUILayout.Label(day.already_assigned ? "ASSIGNED" : "NOT ASSIGNED", badgeStyle, GUILayout.Width(95));
        }

        private void DrawQuestCard(DailyQuestEntryData entry, string entryKey)
        {
            if (entry == null) return;

            if (!this.expandedQuests.ContainsKey(entryKey))
                this.expandedQuests[entryKey] = false;

            string questId = entry.quest?.id ?? entry.assignment?.quest_definition_id ?? "Unknown Quest";
            string questName = entry.quest?.name ?? questId;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            this.expandedQuests[entryKey] = EditorGUILayout.Foldout(
                this.expandedQuests[entryKey],
                questName,
                true,
                this.CreateFoldoutStyle(new Color(0.9f, 0.9f, 1f), 12));

            if (!string.IsNullOrEmpty(entry.status))
            {
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                statusStyle.fontSize = 10;
                statusStyle.fontStyle = FontStyle.Bold;
                statusStyle.alignment = TextAnchor.MiddleRight;
                statusStyle.normal.textColor = QuestStatusIcons.GetColor(entry.status);
                GUILayout.Label(string.Format("{0} {1}", QuestStatusIcons.GetIcon(entry.status), entry.status.ToLower()), statusStyle, GUILayout.Width(110));
            }
            EditorGUILayout.EndHorizontal();

            if (!this.expandedQuests[entryKey])
            {
                EditorGUILayout.EndVertical();
                return;
            }

            this.DrawSeparator();
            if (entry.quest != null)
            {
                GUIStyle detailStyle = new GUIStyle(EditorStyles.label);
                detailStyle.fontSize = 10;
                detailStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
                EditorGUILayout.LabelField(string.Format("Type: {0}  |  Active: {1}", entry.quest.quest_type, entry.quest.is_active ? "YES" : "NO"), detailStyle);
                this.DrawCopyRow("Quest ID", entry.quest.id, new Color(1f, 0.84f, 0f));
                if (!string.IsNullOrEmpty(entry.quest.code_name))
                    EditorGUILayout.LabelField(string.Format("Code: {0}", entry.quest.code_name), detailStyle);
                if (!string.IsNullOrEmpty(entry.quest.description))
                {
                    GUIStyle descriptionStyle = new GUIStyle(detailStyle);
                    descriptionStyle.wordWrap = true;
                    descriptionStyle.fontStyle = FontStyle.Italic;
                    EditorGUILayout.LabelField(entry.quest.description, descriptionStyle);
                }
            }

            if (entry.assignment != null)
            {
                EditorGUILayout.Space(3);
                GUIStyle assignmentHeader = new GUIStyle(EditorStyles.boldLabel);
                assignmentHeader.fontSize = 10;
                assignmentHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("ASSIGNMENT", assignmentHeader);
                this.DrawCopyRow("Assignment ID", entry.assignment.id, new Color(1f, 0.84f, 0f));

                GUIStyle assignmentStyle = new GUIStyle(EditorStyles.label);
                assignmentStyle.fontSize = 9;
                assignmentStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                EditorGUILayout.LabelField(string.Format("Assigned: {0}", entry.assignment.assigned_date), assignmentStyle);
                EditorGUILayout.LabelField(string.Format("Expires: {0}", entry.assignment.expires_at), assignmentStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private GUIStyle CreateFoldoutStyle(Color color, int fontSize)
        {
            GUIStyle style = new GUIStyle(EditorStyles.foldout);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = color;
            style.onNormal.textColor = color;
            style.focused.textColor = color;
            style.onFocused.textColor = color;
            style.active.textColor = color;
            style.onActive.textColor = color;
            return style;
        }

        private void DrawCopyRow(string label, string value, Color color)
        {
            if (string.IsNullOrEmpty(value)) return;

            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.fontSize = 10;
            style.normal.textColor = color;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Format("{0}: {1}", label, value), style);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = value;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.6f));
            EditorGUILayout.Space(2);
        }
    }
}
