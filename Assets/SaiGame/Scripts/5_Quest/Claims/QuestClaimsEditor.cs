using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(QuestClaims))]
    [CanEditMultipleObjects]
    public class QuestClaimsEditor : Editor
    {
        private QuestClaims questClaims;
        private SerializedProperty claimsLimit;
        private SerializedProperty claimsOffset;

        private bool showPagination = true;
        private bool showClaimsList = true;
        private bool showUtilityButtons = true;

        private bool isLoading = false;

        private void OnEnable()
        {
            this.questClaims = (QuestClaims)target;
            this.claimsLimit = serializedObject.FindProperty("claimsLimit");
            this.claimsOffset = serializedObject.FindProperty("claimsOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quest Claims", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ── Pagination ────────────────────────────────────────────────────
            this.showPagination = EditorGUILayout.Foldout(this.showPagination, "Pagination Settings", true);
            if (this.showPagination)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.claimsLimit, new GUIContent("Limit", "Max number of claims per request"));
                EditorGUILayout.PropertyField(this.claimsOffset, new GUIContent("Offset", "Offset for pagination"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Utility Buttons ───────────────────────────────────────────────
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                bool canLoad = Application.isPlaying
                               && SaiService.Instance != null
                               && SaiService.Instance.IsAuthenticated;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = (canLoad && !this.isLoading) ? Color.cyan : Color.gray;
                EditorGUI.BeginDisabledGroup(!canLoad || this.isLoading);
                if (GUILayout.Button(this.isLoading ? "Loading..." : "Get Claims", GUILayout.Height(30)))
                    this.LoadClaims();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Claims", GUILayout.Height(30)))
                    this.questClaims.ClearClaims();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode and log in to fetch claims.", MessageType.Info);
                else if (!canLoad)
                    EditorGUILayout.HelpBox("Not authenticated. Please login first.", MessageType.Warning);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Cached Claims List ────────────────────────────────────────────
            this.showClaimsList = EditorGUILayout.Foldout(this.showClaimsList, "Cached Claims", true);
            if (this.showClaimsList)
            {
                EditorGUI.indentLevel++;

                QuestClaimsResponse response = this.questClaims.CurrentClaimsResponse;

                if (response != null && response.claims != null && response.claims.Length > 0)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total: {response.total}");
                    EditorGUILayout.LabelField($"Loaded: {response.claims.Length}");
                    EditorGUILayout.LabelField($"Limit: {response.limit}  |  Offset: {response.offset}");

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawClaimRecord(QuestClaimRecord claim)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };

            // ── Header: quest name or id ──────────────────────────────────────
            string title = claim.quest_definition != null && !string.IsNullOrEmpty(claim.quest_definition.name)
                ? claim.quest_definition.name
                : claim.quest_definition_id;

            EditorGUILayout.LabelField($"<b>{title}</b>", richStyle);
            EditorGUILayout.LabelField($"Claim ID: {claim.id}");
            EditorGUILayout.LabelField($"Quest Def ID: {claim.quest_definition_id}");
            EditorGUILayout.LabelField($"Progress ID: {claim.progress_id}");
            EditorGUILayout.LabelField($"Claimed At: {claim.claimed_at}");

            // ── Rewards Granted ───────────────────────────────────────────────
            if (claim.rewards_granted != null && claim.rewards_granted.Length > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Rewards Granted:", EditorStyles.boldLabel);
                foreach (ClaimQuestGrantedReward r in claim.rewards_granted)
                {
                    string line = $"  • {r.reward_type}";
                    if (r.amount > 0) line += $" × {r.amount}";
                    if (r.quantity > 0) line += $" qty:{r.quantity}";
                    if (!string.IsNullOrEmpty(r.item_definition_id)) line += $" (item: {r.item_definition_id})";
                    EditorGUILayout.LabelField(line);
                }
            }

            // ── Embedded Quest Definition ─────────────────────────────────────
            if (claim.quest_definition != null)
            {
                QuestDefinitionData def = claim.quest_definition;

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Quest Definition:", EditorStyles.boldLabel);

                EditorGUILayout.LabelField($"  Type: {def.quest_type}  |  Active: {def.is_active}  |  Sort: {def.sort_order}");

                if (!string.IsNullOrEmpty(def.description))
                    EditorGUILayout.LabelField($"  Description: {def.description}");

                if (def.conditions != null && def.conditions.clauses != null && def.conditions.clauses.Length > 0)
                {
                    EditorGUILayout.LabelField($"  <b>Conditions ({def.conditions.operator_type})</b>", richStyle);
                    foreach (QuestClause clause in def.conditions.clauses)
                        EditorGUILayout.LabelField($"    • [{clause.clause_id}]  {clause.type}");
                }

                if (def.rewards != null && def.rewards.Length > 0)
                {
                    EditorGUILayout.LabelField("  Rewards:");
                    foreach (QuestReward r in def.rewards)
                    {
                        if (r.reward_type == "coin")
                            EditorGUILayout.LabelField($"    • coin × {r.amount}");
                        else if (r.reward_type == "item")
                            EditorGUILayout.LabelField($"    • item {r.item_definition_id} × {r.quantity_min}-{r.quantity_max}");
                        else
                            EditorGUILayout.LabelField($"    • {r.reward_type}");
                    }
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
