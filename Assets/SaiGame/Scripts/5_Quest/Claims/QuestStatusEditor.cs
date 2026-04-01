using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(QuestStatus))]
    [CanEditMultipleObjects]
    public class QuestStatusEditor : Editor
    {
        private QuestStatus questClaims;
        private SerializedProperty claimsLimit;
        private SerializedProperty claimsOffset;

        private bool showClaimsList = true;
        private bool showCheckStatus = true;

        private bool isLoading = false;
        private bool isCheckingStatus = false;
        private string checkStatusQuestDefId = "";
        private string checkStatusError = "";

        private void OnEnable()
        {
            this.questClaims = (QuestStatus)target;
            this.claimsLimit = serializedObject.FindProperty("claimsLimit");
            this.claimsOffset = serializedObject.FindProperty("claimsOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIStyle sectionHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            GUIStyle rich          = new GUIStyle(EditorStyles.label) { richText = true };
            GUIStyle richSummary   = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 };

            bool canAct = Application.isPlaying
                          && SaiService.Instance != null
                          && SaiService.Instance.IsAuthenticated;

            // ════════════════════════════════════════════════════════════════
            //  SECTION 1 — QUEST DETAIL
            // ════════════════════════════════════════════════════════════════
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Quest Detail", sectionHeader);
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

            // ════════════════════════════════════════════════════════════════
            //  SECTION 2 — QUEST CLAIMS
            // ════════════════════════════════════════════════════════════════
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quest Claims", sectionHeader);
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
            this.showClaimsList = EditorGUILayout.Foldout(this.showClaimsList, "Cached Claims", true);
            if (this.showClaimsList)
            {
                EditorGUI.indentLevel++;
                QuestClaimsResponse response = this.questClaims.CurrentClaimsResponse;

                if (response != null && response.claims != null && response.claims.Length > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"<b>Total:</b> <color=#00FF88>{response.total}</color>", richSummary);
                    EditorGUILayout.LabelField($"<b>Loaded:</b> <color=#66CCFF>{response.claims.Length}</color>", richSummary);
                    EditorGUILayout.LabelField($"<b>Limit:</b> {response.limit}  <b>Offset:</b> {response.offset}", richSummary);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(4);
                    foreach (QuestClaimRecord claim in response.claims)
                        this.DrawClaimRecord(claim);
                }
                else
                {
                    EditorGUILayout.HelpBox("No claims loaded yet. Click Get Claims.", MessageType.None);
                }
                EditorGUI.indentLevel--;
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

            // Status badge header
            string statusColor = result.status switch
            {
                "claimed"     => "#FFD700",
                "completed"   => "#00FF88",
                "in_progress" => "#66CCFF",
                _             => "#AAAAAA"
            };
            string questName = result.quest_definition?.name ?? result.progress?.quest_definition_id ?? "—";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<b>{questName}</b>", rich);
            EditorGUILayout.LabelField(
                $"<color={statusColor}><b>{(result.status ?? "unknown").ToUpper()}</b></color>",
                rich, GUILayout.MaxWidth(100f));
            EditorGUILayout.EndHorizontal();

            // ── PROGRESS ──────────────────────────────────────────────────────
            if (result.progress != null)
            {
                QuestProgressSnapshot p = result.progress;
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("<b>PROGRESS</b>", rich);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<color=#888888>Progress ID: {p.id}</color>", mini);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = p.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<color=#888888>Def ID:      {p.quest_definition_id}</color>", mini);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = p.quest_definition_id;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b>Version:</b> {p.version}", rich, GUILayout.MaxWidth(100f));
                if (!string.IsNullOrEmpty(p.completed_at))
                    EditorGUILayout.LabelField($"<color=#00FF88><b>Completed:</b> {p.completed_at}</color>", rich);
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(p.claimed_at))
                    EditorGUILayout.LabelField($"<color=#FFD700><b>Claimed At:</b> {p.claimed_at}</color>", rich);

                EditorGUILayout.LabelField(
                    $"<color=#888888>Created: {p.created_at}  Updated: {p.updated_at}</color>", mini);

                EditorGUILayout.EndVertical();
            }

            // ── QUEST DEFINITION ──────────────────────────────────────────────
            if (result.quest_definition != null)
            {
                QuestDefinitionData def = result.quest_definition;
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("<b>QUEST DEFINITION</b>", rich);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<color=#888888>ID: {def.id}</color>", mini);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = def.id;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"<color=#AADDFF><b>{def.quest_type?.ToUpper()}</b></color>", rich, GUILayout.MaxWidth(90f));
                string activeColor = def.is_active ? "#00FF88" : "#FF4444";
                EditorGUILayout.LabelField(
                    $"<color={activeColor}><b>{(def.is_active ? "ACTIVE" : "INACTIVE")}</b></color>",
                    rich, GUILayout.MaxWidth(70f));
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(def.description))
                    EditorGUILayout.LabelField(def.description, EditorStyles.wordWrappedMiniLabel);

                if (def.rewards != null && def.rewards.Length > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("<b>Rewards:</b>", rich);
                    foreach (QuestReward r in def.rewards)
                    {
                        if (r.reward_type == "coin")
                            EditorGUILayout.LabelField(
                                $"  <color=#FFD700>● coin</color>  <b>×{r.amount}</b>", rich);
                        else if (r.reward_type == "item")
                            EditorGUILayout.LabelField(
                                $"  <color=#66CCFF>● item</color>  <color=#AAAAAA>{r.item_definition_id}</color>  <b>×{r.quantity_min}–{r.quantity_max}</b>",
                                rich);
                        else
                            EditorGUILayout.LabelField($"  ● {r.reward_type}", rich);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
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

        private void DrawClaimRecord(QuestClaimRecord claim)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle rich = new GUIStyle(EditorStyles.label) { richText = true };
            GUIStyle richBold = new GUIStyle(EditorStyles.boldLabel) { richText = true };
            GUIStyle mini = new GUIStyle(EditorStyles.miniLabel) { richText = true };

            // ── Header row: quest name + claimed at ───────────────────────────
            string title = claim.quest_definition != null && !string.IsNullOrEmpty(claim.quest_definition.name)
                ? claim.quest_definition.name
                : claim.quest_definition_id;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<b>{title}</b>", richBold);
            EditorGUILayout.LabelField(
                $"<color=#FFD700><b>Claimed At:</b> {claim.claimed_at}</color>",
                rich, GUILayout.MaxWidth(260f));
            EditorGUILayout.EndHorizontal();

            // ── Technical IDs (dimmed) ────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<color=#888888>Claim: {claim.id}</color>", mini);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.id;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<color=#888888>Def: {claim.quest_definition_id}</color>", mini);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.quest_definition_id;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"<color=#888888>Progress: {claim.progress_id}</color>", mini);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = claim.progress_id;
            EditorGUILayout.EndHorizontal();

            // ── Quest Definition badge row ────────────────────────────────────
            if (claim.quest_definition != null)
            {
                QuestDefinitionData def = claim.quest_definition;

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();

                // Type badge
                if (!string.IsNullOrEmpty(def.quest_type))
                    EditorGUILayout.LabelField(
                        $"<color=#AADDFF><b>{def.quest_type.ToUpper()}</b></color>",
                        rich, GUILayout.MaxWidth(100f));

                // Active badge
                string activeColor = def.is_active ? "#00FF88" : "#FF4444";
                string activeLabel = def.is_active ? "ACTIVE" : "INACTIVE";
                EditorGUILayout.LabelField(
                    $"<color={activeColor}><b>{activeLabel}</b></color>",
                    rich, GUILayout.MaxWidth(70f));

                // Sort order (small)
                EditorGUILayout.LabelField(
                    $"<color=#888888>sort: {def.sort_order}</color>",
                    mini, GUILayout.MaxWidth(60f));

                EditorGUILayout.EndHorizontal();

                // Description
                if (!string.IsNullOrEmpty(def.description))
                {
                    EditorGUILayout.LabelField(def.description, EditorStyles.wordWrappedMiniLabel);
                }

                // Conditions
                if (def.conditions != null && def.conditions.clauses != null && def.conditions.clauses.Length > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField(
                        $"<b>Conditions</b>  <color=#AAAAAA>({def.conditions.operator_type})</color>  <color=#66CCFF>×{def.conditions.clauses.Length}</color>",
                        rich);
                    foreach (QuestClause clause in def.conditions.clauses)
                        EditorGUILayout.LabelField(
                            $"  <color=#AAAAAA>[{clause.clause_id}]</color>  <b>{clause.type}</b>",
                            rich);
                }

                // Definition rewards
                if (def.rewards != null && def.rewards.Length > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("<b>Quest Rewards:</b>", rich);
                    foreach (QuestReward r in def.rewards)
                    {
                        if (r.reward_type == "coin")
                            EditorGUILayout.LabelField(
                                $"  <color=#FFD700>● coin</color>  <b>×{r.amount}</b>", rich);
                        else if (r.reward_type == "item")
                            EditorGUILayout.LabelField(
                                $"  <color=#66CCFF>● item</color>  <color=#AAAAAA>{r.item_definition_id}</color>  <b>×{r.quantity_min}–{r.quantity_max}</b>",
                                rich);
                        else
                            EditorGUILayout.LabelField($"  ● {r.reward_type}", rich);
                    }
                }
            }

            // ── Rewards Granted ───────────────────────────────────────────────
            if (claim.rewards_granted != null && claim.rewards_granted.Length > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("<b>Rewards Granted:</b>", rich);
                foreach (ClaimQuestGrantedReward r in claim.rewards_granted)
                {
                    string rewardColor = r.reward_type == "coin" ? "#FFD700"
                        : r.reward_type == "item" ? "#66CCFF"
                        : "#FFFFFF";

                    string line = $"  <color={rewardColor}>● {r.reward_type}</color>";
                    if (r.amount > 0) line += $"  <b>×{r.amount}</b>";
                    if (r.quantity > 0) line += $"  <b>qty {r.quantity}</b>";
                    if (!string.IsNullOrEmpty(r.item_definition_id))
                        line += $"  <color=#AAAAAA>({r.item_definition_id})</color>";

                    EditorGUILayout.LabelField(line, rich);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void LoadClaims()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[QuestClaimsEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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
    }
}
