using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(QuestHistory))]
    [CanEditMultipleObjects]
    public class QuestHistoryEditor : Editor
    {
        private QuestHistory questClaims;
        private SerializedProperty claimsLimit;
        private SerializedProperty claimsOffset;

        private bool showClaimsList = true;
        private bool showCheckStatus = true;
        private bool showQuestDetail = false;
        private bool showQuestHistory = false;

        private bool isLoading = false;
        private bool isCheckingStatus = false;
        private string checkStatusQuestDefId = "";
        private string checkStatusError = "";

        // Per-claim collapse state (keyed by claim id)
        private readonly Dictionary<string, bool> expandedClaims = new Dictionary<string, bool>();

        private void OnEnable()
        {
            this.questClaims = (QuestHistory)target;
            this.claimsLimit = serializedObject.FindProperty("claimsLimit");
            this.claimsOffset = serializedObject.FindProperty("claimsOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };

            bool canAct = Application.isPlaying
                          && SaiServer.Instance != null
                          && SaiServer.Instance.IsAuthenticated;

            // ════════════════════════════════════════════════════════════════
            //  SECTION 1 — QUEST DETAIL
            // ════════════════════════════════════════════════════════════════
            EditorGUILayout.Space(6);
            GUIStyle sectionFoldout = new GUIStyle(EditorStyles.foldout) { fontSize = 12, fontStyle = FontStyle.Bold };
            this.showQuestDetail = EditorGUILayout.Foldout(this.showQuestDetail, "Quest Detail", true, sectionFoldout);
            if (this.showQuestDetail)
            {
                EditorGUILayout.LabelField(
                    "<color=#888888>Look up progress + definition for a single Quest Definition ID.</color>",
                    new GUIStyle(EditorStyles.miniLabel) { richText = true });
                EditorGUILayout.Space(2);

                // Input
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Quest Def ID", GUILayout.Width(82f));
                this.checkStatusQuestDefId = EditorGUILayout.TextField(this.checkStatusQuestDefId);
                EditorGUILayout.EndHorizontal();

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                bool hasId = !string.IsNullOrWhiteSpace(this.checkStatusQuestDefId);
                GUI.backgroundColor = (canAct && hasId && !this.isCheckingStatus)
                    ? new Color(0.4f, 0.8f, 1f) : Color.gray;
                EditorGUI.BeginDisabledGroup(!canAct || !hasId || this.isCheckingStatus);
                if (GUILayout.Button(this.isCheckingStatus ? "Checking..." : "Check Status", GUILayout.Height(26)))
                    this.RunCheckStatus();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Clear", GUILayout.Height(26), GUILayout.Width(52)))
                {
                    this.checkStatusError = "";
                    if (this.questClaims != null) this.questClaims.CurrentQuestStatusResponse = null;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();

                if (!canAct)
                    EditorGUILayout.HelpBox(
                        Application.isPlaying ? "Not authenticated. Please login first." : "Enter Play Mode and log in.",
                        MessageType.Info);

                if (!string.IsNullOrEmpty(this.checkStatusError))
                    EditorGUILayout.HelpBox(this.checkStatusError, MessageType.Error);

                // Result
                QuestDefinitionStatusResponse result = this.questClaims?.CurrentQuestStatusResponse;
                if (result != null)
                {
                    EditorGUILayout.Space(4);
                    this.showCheckStatus = EditorGUILayout.Foldout(this.showCheckStatus, "Result", true);
                    if (this.showCheckStatus)
                        this.DrawCheckStatusResult(result, rich,
                            new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }
            }

            // ════════════════════════════════════════════════════════════════
            //  SECTION 2 — QUEST History
            // ════════════════════════════════════════════════════════════════
            EditorGUILayout.Space(10);
            Rect divider = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(divider, new Color(0.35f, 0.35f, 0.35f, 1f));
            EditorGUILayout.Space(6);
            this.showQuestHistory = EditorGUILayout.Foldout(this.showQuestHistory, "Quests History", true, sectionFoldout);
            if (this.showQuestHistory)
            {
                EditorGUILayout.LabelField(
                    "<color=#888888>Fetch and browse the paginated list of all claimed quests for this user.</color>",
                    new GUIStyle(EditorStyles.miniLabel) { richText = true });
                EditorGUILayout.Space(2);

                // Pagination config inline
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Limit", GUILayout.Width(38f));
                this.claimsLimit.intValue = EditorGUILayout.IntField(this.claimsLimit.intValue, GUILayout.Width(60f));
                GUILayout.Space(16f);
                EditorGUILayout.LabelField("Offset", GUILayout.Width(44f));
                this.claimsOffset.intValue = EditorGUILayout.IntField(this.claimsOffset.intValue, GUILayout.Width(60f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = (canAct && !this.isLoading) ? Color.cyan : Color.gray;
                EditorGUI.BeginDisabledGroup(!canAct || this.isLoading);
                if (GUILayout.Button(this.isLoading ? "Loading..." : "Get Claims", GUILayout.Height(26)))
                    this.LoadClaims();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("Clear Claims", GUILayout.Height(26), GUILayout.Width(90f)))
                    this.questClaims.ClearClaims();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (!canAct)
                    EditorGUILayout.HelpBox(
                        Application.isPlaying ? "Not authenticated. Please login first." : "Enter Play Mode and log in.",
                        MessageType.Info);

                // Claims list
                EditorGUILayout.Space(4);
                QuestClaimsResponse claimsResponse = this.questClaims.CurrentClaimsResponse;
                int claimsCount = claimsResponse?.claims?.Length ?? 0;
                this.showClaimsList = EditorGUILayout.Foldout(this.showClaimsList, $"Cached Claims ({claimsCount})", true);
                if (this.showClaimsList)
                {
                    EditorGUI.indentLevel++;

                    if (claimsResponse != null && claimsResponse.claims != null && claimsResponse.claims.Length > 0)
                    {
                        this.DrawClaimsSummaryCard(claimsResponse);
                        EditorGUILayout.Space(4);

                        foreach (QuestClaimRecord claim in claimsResponse.claims)
                            this.DrawClaimRecord(claim);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No claims loaded yet. Click Get Claims.", MessageType.None);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // ════════════════════════════════════════════════════════════════
            //  Event Listeners
            // ════════════════════════════════════════════════════════════════
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Events are automatically registered/unregistered with SaiAuth login/logout events.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCheckStatusResult(QuestDefinitionStatusResponse result, GUIStyle rich, GUIStyle mini)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            GUIStyle dimStyle = new GUIStyle(EditorStyles.label);
            dimStyle.fontSize = 9;
            dimStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);

            // ── Header: quest name + status badge ─────────────────────────────
            string statusColor = QuestStatusIcons.GetHex(result.status);
            string statusIcon = QuestStatusIcons.GetIcon(result.status);
            string statusText = (result.status ?? "unknown").ToLower();
            string questName = result.quest_definition?.name ?? result.progress?.quest_definition_id ?? "—";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<b>{questName}</b>", rich);
            EditorGUILayout.LabelField(
                $"<color={statusColor}><b>{statusIcon} {statusText}</b></color>",
                rich, GUILayout.MaxWidth(120f));
            EditorGUILayout.EndHorizontal();

            // ── PROGRESS ──────────────────────────────────────────────────────
            EditorGUILayout.Space(3);
            GUIStyle progressHeader = new GUIStyle(EditorStyles.boldLabel);
            progressHeader.fontSize = 10;
            progressHeader.normal.textColor = new Color(0.6f, 1f, 0.8f);
            EditorGUILayout.LabelField("📈 PROGRESS", progressHeader);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (result.progress != null)
            {
                QuestProgressSnapshot p = result.progress;

                string progressStatusColor = QuestStatusIcons.GetHex(p.status);
                string progressStatusIcon = QuestStatusIcons.GetIcon(p.status);
                EditorGUILayout.LabelField(
                    $"Status: <color={progressStatusColor}><b>{progressStatusIcon} {p.status}</b></color>  |  Version: <b>{p.version}</b>",
                    rich);

                this.DrawCopyIdRow("Progress ID", p.id, idStyle);
                this.DrawCopyIdRow("Quest Def ID", p.quest_definition_id, idStyle);
                this.DrawCopyIdRow("User ID", p.user_id, idStyle);
                this.DrawCopyIdRow("Game ID", p.game_id, idStyle);
                this.DrawCopyIdRow("Studio ID", p.studio_id, idStyle);

                if (!string.IsNullOrEmpty(p.completed_at))
                    EditorGUILayout.LabelField($"<color=#00FF88><b>Completed:</b> {p.completed_at}</color>", rich);
                if (!string.IsNullOrEmpty(p.claimed_at))
                    EditorGUILayout.LabelField($"<color=#FFD700><b>Claimed At:</b> {p.claimed_at}</color>", rich);
                if (!string.IsNullOrEmpty(p.created_at))
                    EditorGUILayout.LabelField($"Created: {p.created_at}", dimStyle);
                if (!string.IsNullOrEmpty(p.updated_at))
                    EditorGUILayout.LabelField($"Updated: {p.updated_at}", dimStyle);
            }
            else
            {
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.label);
                emptyStyle.fontSize = 10;
                emptyStyle.fontStyle = FontStyle.Italic;
                emptyStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                EditorGUILayout.LabelField("null — quest not started", emptyStyle);
            }

            EditorGUILayout.EndVertical();

            // ── QUEST DEFINITION ──────────────────────────────────────────────
            if (result.quest_definition != null)
            {
                QuestDefinitionData def = result.quest_definition;

                EditorGUILayout.Space(3);
                GUIStyle defHeader = new GUIStyle(EditorStyles.boldLabel);
                defHeader.fontSize = 10;
                defHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("📋 QUEST DEFINITION", defHeader);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Name + code
                GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };
                richBold.fontSize = 11;
                EditorGUILayout.LabelField($"<b>{def.name}</b>", richBold);

                // Badge row: type + active + hidden + sort
                EditorGUILayout.BeginHorizontal();
                if (!string.IsNullOrEmpty(def.quest_type))
                    EditorGUILayout.LabelField(
                        $"<color=#AADDFF><b>{def.quest_type.ToUpper()}</b></color>",
                        rich, GUILayout.MaxWidth(90f));

                string activeColor = def.is_active ? "#00FF88" : "#FF4444";
                EditorGUILayout.LabelField(
                    $"<color={activeColor}><b>{(def.is_active ? "ACTIVE" : "INACTIVE")}</b></color>",
                    rich, GUILayout.MaxWidth(80f));

                string hiddenColor = def.is_hidden ? "#FF8844" : "#888888";
                EditorGUILayout.LabelField(
                    $"<color={hiddenColor}><b>{(def.is_hidden ? "HIDDEN" : "VISIBLE")}</b></color>",
                    rich, GUILayout.MaxWidth(80f));

                EditorGUILayout.LabelField($"<color=#888888>sort: {def.sort_order}</color>", rich, GUILayout.MaxWidth(70f));
                EditorGUILayout.EndHorizontal();

                // IDs
                this.DrawCopyIdRow("ID", def.id, idStyle);
                this.DrawCopyIdRow("CODE", def.code_name, idStyle);
                this.DrawCopyIdRow("Game ID", def.game_id, idStyle);
                this.DrawCopyIdRow("Studio ID", def.studio_id, idStyle);

                // Description
                if (!string.IsNullOrEmpty(def.description))
                {
                    GUIStyle descStyle = new GUIStyle(EditorStyles.label);
                    descStyle.fontSize = 10;
                    descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    descStyle.wordWrap = true;
                    descStyle.fontStyle = FontStyle.Italic;
                    EditorGUILayout.LabelField(def.description, descStyle);
                }

                // Conditions (full)
                this.DrawConditionsBlock(def.conditions);

                // Rewards (coin rewards are skipped)
                this.DrawRewardsBlock(def.rewards);

                // Meta
                if (!string.IsNullOrEmpty(def.created_at) || !string.IsNullOrEmpty(def.updated_at))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField($"Created: {def.created_at}  |  Updated: {def.updated_at}", dimStyle);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCopyIdRow(string label, string id, GUIStyle style)
        {
            if (string.IsNullOrEmpty(id)) return;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{label}: {id}", style);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = id;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConditionsBlock(QuestConditions conditions)
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
                EditorGUILayout.LabelField(
                    $"• <color=#AAAAAA>[{clause.clause_id}]</color> <color=#66CCFF>{clause.type}</color>",
                    clauseHeader);

                if (clause.items != null && clause.items.Length > 0)
                {
                    foreach (QuestClauseItem item in clause.items)
                    {
                        if (item == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(
                            $"    <color=#888888>item:</color> {item.item_definition_id}  <b>×{item.quantity}</b>",
                            subStyle);
                        if (!string.IsNullOrEmpty(item.item_definition_id) && GUILayout.Button("Copy", GUILayout.Width(50)))
                            GUIUtility.systemCopyBuffer = item.item_definition_id;
                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (clause.packs != null && !string.IsNullOrEmpty(clause.packs.gacha_pack_id))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        $"    <color=#888888>gacha:</color> {clause.packs.gacha_pack_id}  <b>×{clause.packs.quantity}</b>",
                        subStyle);
                    if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = clause.packs.gacha_pack_id;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRewardsBlock(QuestReward[] rewards)
        {
            int visible = CountVisibleQuestRewards(rewards);
            if (visible == 0) return;

            EditorGUILayout.Space(3);
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionStyle.fontSize = 10;
            sectionStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
            EditorGUILayout.LabelField($"🎁 REWARDS ({visible})", sectionStyle);

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            richStyle.fontSize = 10;

            foreach (QuestReward r in rewards)
            {
                if (r == null) continue;
                if (IsHiddenQuestReward(r)) continue;
                if (r.reward_type == "item")
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        $"  <color=#66CCFF>● item</color> {r.item_definition_id} × <b>{r.quantity_min}-{r.quantity_max}</b>",
                        richStyle);
                    if (!string.IsNullOrEmpty(r.item_definition_id) && GUILayout.Button("Copy", GUILayout.Width(50)))
                        GUIUtility.systemCopyBuffer = r.item_definition_id;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField($"  <color=#AAAAAA>●</color> {r.reward_type}", richStyle);
                }
            }
        }

        private void RunCheckStatus()
        {
            this.checkStatusError = "";
            this.isCheckingStatus = true;
            Repaint();

            this.questClaims.GetQuestStatus(
                this.checkStatusQuestDefId.Trim(),
                onSuccess: _ =>
                {
                    this.isCheckingStatus = false;
                    Repaint();
                },
                onError: err =>
                {
                    this.isCheckingStatus = false;
                    this.checkStatusError = err;
                    Repaint();
                }
            );
        }

        private void DrawClaimsSummaryCard(QuestClaimsResponse response)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 11;
            headerStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
            EditorGUILayout.LabelField("🏆 CLAIMS SUMMARY", headerStyle);

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            richStyle.fontSize = 11;
            EditorGUILayout.LabelField(
                $"Total: <color=#00FF88><b>{response.total}</b></color>  |  " +
                $"Loaded: <color=#66CCFF><b>{response.claims.Length}</b></color>  |  " +
                $"Limit: <b>{response.limit}</b>  |  Offset: <b>{response.offset}</b>",
                richStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawClaimRecord(QuestClaimRecord claim)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string claimKey = !string.IsNullOrEmpty(claim.id) ? claim.id : claim.quest_definition_id;
            if (!this.expandedClaims.ContainsKey(claimKey))
                this.expandedClaims[claimKey] = false;

            string questName = claim.quest_definition != null && !string.IsNullOrEmpty(claim.quest_definition.name)
                ? claim.quest_definition.name
                : claim.quest_definition_id;

            // Collapsible header: quest name + CLAIMED badge
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
            this.expandedClaims[claimKey] = EditorGUILayout.Foldout(this.expandedClaims[claimKey], $"◆ {questName}", true, foldoutStyle);

            // Every record in the claims list is by definition claimed — use the shared palette.
            GUIStyle statusBadge = new GUIStyle(EditorStyles.miniLabel);
            statusBadge.fontSize = 10;
            statusBadge.fontStyle = FontStyle.Bold;
            statusBadge.alignment = TextAnchor.MiddleRight;
            statusBadge.normal.textColor = QuestStatusIcons.GetColor("claimed");
            GUILayout.Label($"{QuestStatusIcons.GetIcon("claimed")} claimed", statusBadge, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            if (!this.expandedClaims[claimKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }

            this.DrawSeparator();

            GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };

            // Claimed at
            GUIStyle claimedStyle = new GUIStyle(EditorStyles.label) { richText = true };
            claimedStyle.fontSize = 10;
            EditorGUILayout.LabelField($"⏰ <color=#FFD700><b>Claimed At:</b> {claim.claimed_at}</color>", claimedStyle);

            // Compact type info
            if (claim.quest_definition != null)
            {
                QuestDefinitionData def = claim.quest_definition;

                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.fontSize = 10;
                labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

                if (!string.IsNullOrEmpty(def.quest_type))
                    EditorGUILayout.LabelField($"TYPE: {def.quest_type.ToUpper()}", labelStyle);
                EditorGUILayout.LabelField($"SORT: {def.sort_order}", labelStyle);

                GUIStyle activeStyle = new GUIStyle(EditorStyles.label);
                activeStyle.fontSize = 10;
                activeStyle.fontStyle = FontStyle.Bold;
                activeStyle.normal.textColor = def.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField($"STATUS: {(def.is_active ? "ACTIVE" : "INACTIVE")}", activeStyle);
            }

            // Technical IDs (gold)
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Claim ID: {claim.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.id;
            EditorGUILayout.EndHorizontal();

            string codeName = claim.quest_definition?.code_name;
            if (!string.IsNullOrEmpty(codeName))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"CODE: {codeName}", idStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = codeName;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Quest Def ID: {claim.quest_definition_id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.quest_definition_id;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Progress ID: {claim.progress_id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.progress_id;
            EditorGUILayout.EndHorizontal();

            // Description + metadata
            if (claim.quest_definition != null)
            {
                QuestDefinitionData def = claim.quest_definition;

                if (!string.IsNullOrEmpty(def.description))
                {
                    GUIStyle descStyle = new GUIStyle(EditorStyles.label);
                    descStyle.fontSize = 10;
                    descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    descStyle.wordWrap = true;
                    descStyle.fontStyle = FontStyle.Italic;
                    EditorGUILayout.LabelField(def.description, descStyle);
                }

                GUIStyle metaStyle = new GUIStyle(EditorStyles.label);
                metaStyle.fontSize = 9;
                metaStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
                if (!string.IsNullOrEmpty(def.created_at))
                    EditorGUILayout.LabelField($"Created: {def.created_at}  |  Updated: {def.updated_at}", metaStyle);

                // Conditions
                if (def.conditions != null && def.conditions.clauses != null && def.conditions.clauses.Length > 0)
                {
                    EditorGUILayout.Space(3);
                    GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                    sectionStyle.fontSize = 10;
                    sectionStyle.normal.textColor = new Color(1f, 0.7f, 0.4f);
                    string op = string.IsNullOrEmpty(def.conditions.operator_type) ? "AND" : def.conditions.operator_type.ToUpper();
                    EditorGUILayout.LabelField($"⚙ CONDITIONS [{op}] ({def.conditions.clauses.Length})", sectionStyle);

                    foreach (QuestClause clause in def.conditions.clauses)
                        EditorGUILayout.LabelField(
                            $"  <color=#AAAAAA>[{clause.clause_id}]</color>  <b>{clause.type}</b>",
                            rich);
                }

                // Quest rewards (definition)
                int visibleDefRewards = CountVisibleQuestRewards(def.rewards);
                if (visibleDefRewards > 0)
                {
                    EditorGUILayout.Space(3);
                    GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                    sectionStyle.fontSize = 10;
                    sectionStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
                    EditorGUILayout.LabelField($"🎁 QUEST REWARDS ({visibleDefRewards})", sectionStyle);

                    foreach (QuestReward r in def.rewards)
                    {
                        if (IsHiddenQuestReward(r)) continue;
                        if (r.reward_type == "item")
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(
                                $"  <color=#66CCFF>● item</color> {r.item_definition_id} × <b>{r.quantity_min}-{r.quantity_max}</b>",
                                rich);
                            if (!string.IsNullOrEmpty(r.item_definition_id) && GUILayout.Button("Copy", GUILayout.Width(50)))
                                GUIUtility.systemCopyBuffer = r.item_definition_id;
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"  <color=#AAAAAA>●</color> {r.reward_type}", rich);
                        }
                    }
                }
            }

            // Rewards granted
            int visibleGranted = CountVisibleGrantedRewards(claim.rewards_granted);
            if (visibleGranted > 0)
            {
                EditorGUILayout.Space(3);
                GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionStyle.fontSize = 10;
                sectionStyle.normal.textColor = new Color(0.6f, 1f, 0.8f);
                EditorGUILayout.LabelField($"✨ REWARDS GRANTED ({visibleGranted})", sectionStyle);

                foreach (ClaimQuestGrantedReward r in claim.rewards_granted)
                {
                    if (IsHiddenGrantedReward(r)) continue;
                    string rewardColor = r.reward_type == "item" ? "#66CCFF" : "#FFFFFF";

                    string line = $"  <color={rewardColor}>● {r.reward_type}</color>";
                    if (r.amount > 0) line += $"  <b>×{r.amount}</b>";
                    if (r.quantity > 0) line += $"  <b>qty {r.quantity}</b>";
                    if (!string.IsNullOrEmpty(r.item_definition_id))
                        line += $"  <color=#AAAAAA>({r.item_definition_id})</color>";

                    if (r.reward_type == "item" && !string.IsNullOrEmpty(r.item_definition_id))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(line, rich);
                        if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = r.item_definition_id;
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.LabelField(line, rich);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawSeparator()
        {
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);
        }

        private void LoadClaims()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[QuestClaimsEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[QuestClaimsEditor] Not authenticated! Please login first.");
                return;
            }

            this.isLoading = true;
            Repaint();

            this.questClaims.GetClaims(
                onSuccess: response =>
                {
                    this.isLoading = false;
                    Debug.Log($"[QuestClaimsEditor] Loaded {response.claims.Length} claims (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoading = false;
                    Debug.LogError($"[QuestClaimsEditor] Failed to load claims: {error}");
                    Repaint();
                }
            );
        }

        // Coin rewards are hidden — backend doesn't process them.
        private static bool IsHiddenQuestReward(QuestReward r) => r != null && r.reward_type == "coin";
        private static bool IsHiddenGrantedReward(ClaimQuestGrantedReward r) => r != null && r.reward_type == "coin";

        private static int CountVisibleQuestRewards(QuestReward[] rewards)
        {
            if (rewards == null) return 0;
            int count = 0;
            foreach (QuestReward r in rewards)
                if (!IsHiddenQuestReward(r)) count++;
            return count;
        }

        private static int CountVisibleGrantedRewards(ClaimQuestGrantedReward[] rewards)
        {
            if (rewards == null) return 0;
            int count = 0;
            foreach (ClaimQuestGrantedReward r in rewards)
                if (!IsHiddenGrantedReward(r)) count++;
            return count;
        }
    }
}
