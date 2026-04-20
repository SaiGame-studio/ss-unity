using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ChainQuest))]
    [CanEditMultipleObjects]
    public class ChainQuestEditor : Editor
    {
        private ChainQuest chainQuest;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty chainLimit;
        private SerializedProperty chainOffset;

        private bool showCurrentChains = true;
        private bool showChainList = true;
        private bool showUtilityButtons = true;

        // Per-chain collapse state (keyed by chain id)
        private readonly Dictionary<string, bool> expandedChains = new Dictionary<string, bool>();
        // Per-member collapse state (keyed by member id)
        private readonly Dictionary<string, bool> expandedMembers = new Dictionary<string, bool>();

        // Per-chain members cache: chainId → response
        private readonly Dictionary<string, ChainMembersResponse> membersCache = new Dictionary<string, ChainMembersResponse>();
        private readonly Dictionary<string, bool> membersFoldout = new Dictionary<string, bool>();
        private readonly HashSet<string> loadingMembers = new HashSet<string>();

        // Per-chain tree cache: chainId → response
        private readonly Dictionary<string, ChainQuestTreeResponse> treeCache = new Dictionary<string, ChainQuestTreeResponse>();
        private readonly Dictionary<string, bool> treeFoldout = new Dictionary<string, bool>();
        private readonly HashSet<string> loadingTree = new HashSet<string>();

        // Per-quest check cache + per-quest action loading state (keyed by quest_definition_id)
        private readonly Dictionary<string, CheckQuestResponse> memberCheckCache = new Dictionary<string, CheckQuestResponse>();
        private string startingQuestId = null;
        private string checkingQuestId = null;
        private string claimingQuestId = null;

        private void OnEnable()
        {
            this.chainQuest = (ChainQuest)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.chainLimit = serializedObject.FindProperty("chainLimit");
            this.chainOffset = serializedObject.FindProperty("chainOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chain Quest Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load chains when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pagination Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.chainLimit, new GUIContent("Chain Limit", "Number of chains to load per request"));
            EditorGUILayout.PropertyField(this.chainOffset, new GUIContent("Chain Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current Chain Data
            this.showCurrentChains = EditorGUILayout.Foldout(this.showCurrentChains, "Current Chain Data", true);
            if (this.showCurrentChains)
            {
                EditorGUI.indentLevel++;

                if (this.chainQuest.CurrentChainResponse != null
                    && this.chainQuest.CurrentChainResponse.chains != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Chains: {this.chainQuest.CurrentChainResponse.total}");
                    EditorGUILayout.LabelField($"Loaded Chains: {this.chainQuest.CurrentChainResponse.chains.Length}");
                    EditorGUILayout.LabelField($"Limit: {this.chainQuest.CurrentChainResponse.limit}  |  Offset: {this.chainQuest.CurrentChainResponse.offset}");

                    if (this.chainQuest.CurrentChainResponse.chains.Length > 0)
                    {
                        this.showChainList = EditorGUILayout.Foldout(this.showChainList, $"Chain List ({this.chainQuest.CurrentChainResponse.chains.Length})", true);
                        if (this.showChainList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (ChainQuestData chain in this.chainQuest.CurrentChainResponse.chains)
                                this.DrawChainSummary(chain);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No chain data loaded yet.", MessageType.None);
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
                if (GUILayout.Button("Get Chains", GUILayout.Height(30)))
                    this.LoadChains();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Chains", GUILayout.Height(30)))
                    this.chainQuest.ClearChains();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Chain Card ────────────────────────────────────────────────────────

        private void DrawChainSummary(ChainQuestData chain)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string chainId = chain.id;
            if (!this.expandedChains.ContainsKey(chainId))
                this.expandedChains[chainId] = false;

            // Header: chain name + active badge (right-aligned)
            int memberCount = this.membersCache.ContainsKey(chainId) ? (this.membersCache[chainId].members?.Length ?? 0) : -1;
            string memberSuffix = memberCount >= 0 ? $"  [{memberCount}]" : "";
            string headerLabel = $"★ {chain.display_name}{memberSuffix}";

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            Color titleColor = this.GetChainTypeColor(chain.chain_type);
            foldoutStyle.normal.textColor = titleColor;
            foldoutStyle.onNormal.textColor = titleColor;
            foldoutStyle.focused.textColor = titleColor;
            foldoutStyle.onFocused.textColor = titleColor;
            foldoutStyle.active.textColor = titleColor;
            foldoutStyle.onActive.textColor = titleColor;

            EditorGUILayout.BeginHorizontal();
            this.expandedChains[chainId] = EditorGUILayout.Foldout(this.expandedChains[chainId], headerLabel, true, foldoutStyle);

            // Active badge
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.label);
            badgeStyle.fontSize = 11;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleRight;
            badgeStyle.normal.textColor = chain.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField(chain.is_active ? "ACTIVE" : "INACTIVE", badgeStyle, GUILayout.MinWidth(70));
            EditorGUILayout.EndHorizontal();

            if (!this.expandedChains[chainId])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            this.DrawSeparator();

            // Compact info
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            EditorGUILayout.LabelField($"KEY: {chain.chain_key}", labelStyle);
            EditorGUILayout.LabelField($"TYPE: {chain.chain_type.ToUpper()}", labelStyle);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {chain.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = chain.id;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(chain.description))
            {
                GUIStyle descStyle = new GUIStyle(EditorStyles.label);
                descStyle.fontSize = 10;
                descStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                descStyle.wordWrap = true;
                descStyle.fontStyle = FontStyle.Italic;
                EditorGUILayout.LabelField(chain.description, descStyle);
            }

            GUIStyle metaStyle = new GUIStyle(EditorStyles.label);
            metaStyle.fontSize = 9;
            metaStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            EditorGUILayout.LabelField($"Created: {chain.created_at}", metaStyle);

            EditorGUILayout.Space(6);

            // Action buttons row 1: Members
            bool isLoadingMembers = this.loadingMembers.Contains(chainId);
            bool hasCachedMembers = this.membersCache.ContainsKey(chainId);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isLoadingMembers ? Color.gray : new Color(0.4f, 0.9f, 0.6f);
            EditorGUI.BeginDisabledGroup(isLoadingMembers);
            if (GUILayout.Button(isLoadingMembers ? "📋 Loading..." : "📋 Quest Flat", GUILayout.Height(26)))
                this.LoadChainMembers(chainId);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Inline members display
            if (hasCachedMembers)
            {
                ChainMembersResponse cached = this.membersCache[chainId];

                if (!this.membersFoldout.ContainsKey(chainId))
                    this.membersFoldout[chainId] = true;

                GUIStyle sectionHeader = new GUIStyle(EditorStyles.foldout);
                sectionHeader.fontSize = 11;
                sectionHeader.fontStyle = FontStyle.Bold;
                sectionHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                sectionHeader.onNormal.textColor = sectionHeader.normal.textColor;

                this.membersFoldout[chainId] = EditorGUILayout.Foldout(
                    this.membersFoldout[chainId],
                    $"📋 MEMBERS ({cached.members?.Length ?? 0})",
                    true,
                    sectionHeader);

                if (this.membersFoldout[chainId] && cached.members != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (ChainMemberData member in cached.members)
                        this.DrawMemberSummary(member);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(2);

            // Action buttons row 2: Tree
            bool isLoadingTree = this.loadingTree.Contains(chainId);
            bool hasCachedTree = this.treeCache.ContainsKey(chainId);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isLoadingTree ? Color.gray : new Color(0.5f, 0.7f, 1f);
            EditorGUI.BeginDisabledGroup(isLoadingTree);
            if (GUILayout.Button(isLoadingTree ? "🌳 Loading..." : "🌳 Quest Tree", GUILayout.Height(26)))
                this.LoadChainTree(chainId);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Inline tree display
            if (hasCachedTree)
            {
                ChainQuestTreeResponse cachedTree = this.treeCache[chainId];

                if (!this.treeFoldout.ContainsKey(chainId))
                    this.treeFoldout[chainId] = true;

                GUIStyle sectionHeader = new GUIStyle(EditorStyles.foldout);
                sectionHeader.fontSize = 11;
                sectionHeader.fontStyle = FontStyle.Bold;
                sectionHeader.normal.textColor = new Color(0.8f, 0.9f, 1f);
                sectionHeader.onNormal.textColor = sectionHeader.normal.textColor;

                this.treeFoldout[chainId] = EditorGUILayout.Foldout(
                    this.treeFoldout[chainId],
                    $"🌳 QUEST TREE — {cachedTree.chain_name} ({cachedTree.nodes?.Length ?? 0} root)",
                    true,
                    sectionHeader);

                if (this.treeFoldout[chainId] && cachedTree.nodes != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foreach (QuestTreeNode node in cachedTree.nodes)
                        this.DrawTreeNode(node, 0);
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ── Member Card ───────────────────────────────────────────────────────

        private void DrawMemberSummary(ChainMemberData member)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string questName = member.definition != null ? member.definition.name : member.quest_definition_id;
            string memberKey = !string.IsNullOrEmpty(member.id) ? member.id : member.quest_definition_id;
            if (!this.expandedMembers.ContainsKey(memberKey))
                this.expandedMembers[memberKey] = false;

            // Collapsible header: quest name + active badge
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
            this.expandedMembers[memberKey] = EditorGUILayout.Foldout(this.expandedMembers[memberKey], $"◆ {questName}", true, foldoutStyle);

            // Prominent status pill — prefers member.status from API, falls back to cached check.
            string memberStatus = !string.IsNullOrEmpty(member.status)
                ? member.status
                : this.GetCachedMemberStatus(member.quest_definition_id);
            if (!string.IsNullOrEmpty(memberStatus))
                this.DrawStatusPill(memberStatus);

            if (member.definition != null)
            {
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.label);
                badgeStyle.fontSize = 10;
                badgeStyle.fontStyle = FontStyle.Bold;
                badgeStyle.alignment = TextAnchor.MiddleRight;
                badgeStyle.normal.textColor = member.definition.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(member.definition.is_active ? "ACTIVE" : "INACTIVE", badgeStyle, GUILayout.MinWidth(70));
            }
            EditorGUILayout.EndHorizontal();

            if (!this.expandedMembers[memberKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }

            this.DrawSeparator();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            // Compact info
            EditorGUILayout.LabelField($"ORDER: {member.sort_order}", labelStyle);

            if (member.definition != null)
            {
                EditorGUILayout.LabelField($"TYPE: {member.definition.quest_type.ToUpper()}", labelStyle);
            }

            // IDs
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Member ID: {member.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = member.id;
            EditorGUILayout.EndHorizontal();

            // CODE + Quest Def ID side-by-side
            this.DrawCodeAndIdRow(member.definition?.code_name, member.quest_definition_id);

            if (member.unlock_quest_ids != null && member.unlock_quest_ids.Length > 0)
            {
                GUIStyle unlockStyle = new GUIStyle(EditorStyles.label);
                unlockStyle.fontSize = 9;
                unlockStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                unlockStyle.wordWrap = true;
                EditorGUILayout.LabelField($"🔓 Unlocks: {string.Join(", ", member.unlock_quest_ids)}", unlockStyle);
            }

            if (member.definition != null)
            {
                QuestDefinitionData def = member.definition;

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
                this.DrawConditions(def.conditions);

                // Rewards section (coin rewards are hidden — backend ignores them)
                int visibleRewardCount = this.CountVisibleRewards(def.rewards);
                if (visibleRewardCount > 0)
                {
                    EditorGUILayout.Space(3);
                    GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
                    sectionStyle.fontSize = 10;
                    sectionStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
                    EditorGUILayout.LabelField($"🎁 REWARDS ({visibleRewardCount})", sectionStyle);

                    foreach (QuestReward reward in def.rewards)
                    {
                        if (this.IsHiddenReward(reward)) continue;
                        this.DrawReward(reward);
                    }
                }
            }

            // Progress block (from cached check)
            CheckQuestResponse cachedCheck = this.GetCachedMemberCheck(member.quest_definition_id);
            if (cachedCheck?.progress != null)
                this.DrawProgressBlock(cachedCheck.progress);

            // Action buttons: Start / Check / Claim
            this.DrawMemberActionButtons(member.quest_definition_id);

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

        // ── Conditions (mirrors DailyQuestEditor) ─────────────────────────────

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

        // ── Progress Block (renders CheckQuestProgressRecord from cached check) ─

        private void DrawProgressBlock(CheckQuestProgressRecord p)
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
            if (!string.IsNullOrEmpty(p.claimed_at))   EditorGUILayout.LabelField($"Claimed: {p.claimed_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.reset_at))     EditorGUILayout.LabelField($"Reset: {p.reset_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.created_at))   EditorGUILayout.LabelField($"Created: {p.created_at}", dimStyle);
            if (!string.IsNullOrEmpty(p.updated_at))   EditorGUILayout.LabelField($"Updated: {p.updated_at}", dimStyle);

            if (!string.IsNullOrEmpty(p.progress_data_json))
                this.DrawProgressData(p.progress_data_json);
        }

        // ── Action buttons + Network handlers ─────────────────────────────────

        private void DrawMemberActionButtons(string questDefinitionId)
        {
            if (string.IsNullOrEmpty(questDefinitionId)) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            bool isStarting = this.startingQuestId == questDefinitionId;
            bool isChecking = this.checkingQuestId == questDefinitionId;
            bool isClaiming = this.claimingQuestId == questDefinitionId;
            bool anyBusy = isStarting || isChecking || isClaiming;

            GUI.backgroundColor = isStarting ? Color.gray : new Color(1f, 0.82f, 0.2f);
            EditorGUI.BeginDisabledGroup(anyBusy);
            if (GUILayout.Button(isStarting ? "▶ Starting..." : "▶ Start", GUILayout.Height(28)))
                this.RunStartQuest(questDefinitionId);
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = isChecking ? Color.gray : new Color(0.4f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(anyBusy);
            if (GUILayout.Button(isChecking ? "🔄 Checking..." : "🔄 Check", GUILayout.Height(28)))
                this.RunCheckQuest(questDefinitionId);
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = isClaiming ? Color.gray : new Color(0.4f, 1f, 0.6f);
            EditorGUI.BeginDisabledGroup(anyBusy);
            if (GUILayout.Button(isClaiming ? "✓ Claiming..." : "✓ Claim", GUILayout.Height(28)))
                this.RunClaimQuest(questDefinitionId);
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void RunStartQuest(string questDefinitionId)
        {
            if (SaiServer.Instance?.QuestProgressor == null)
            {
                Debug.LogError("[ChainQuestEditor] QuestProgressor not found!");
                return;
            }

            this.startingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.StartQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.startingQuestId = null;
                    Debug.Log($"[ChainQuestEditor] Quest started: id={response.id}, status={response.status}");
                    this.ApplyMemberStatus(questDefinitionId, response.status);
                    Repaint();
                    // Refresh cached state by issuing a Check
                    this.RunCheckQuest(questDefinitionId);
                },
                onError: error =>
                {
                    this.startingQuestId = null;
                    Debug.LogError($"[ChainQuestEditor] Start quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        private void RunCheckQuest(string questDefinitionId)
        {
            if (SaiServer.Instance?.QuestProgressor == null)
            {
                Debug.LogError("[ChainQuestEditor] QuestProgressor not found!");
                return;
            }

            this.checkingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.CheckQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.checkingQuestId = null;
                    this.memberCheckCache[questDefinitionId] = response;
                    Debug.Log($"[ChainQuestEditor] Quest checked: status={response.status}");
                    this.ApplyMemberStatus(questDefinitionId, response.progress?.status ?? response.status);
                    Repaint();
                },
                onError: error =>
                {
                    this.checkingQuestId = null;
                    Debug.LogError($"[ChainQuestEditor] Check quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        private void RunClaimQuest(string questDefinitionId)
        {
            if (SaiServer.Instance?.QuestProgressor == null)
            {
                Debug.LogError("[ChainQuestEditor] QuestProgressor not found!");
                return;
            }

            this.claimingQuestId = questDefinitionId;
            Repaint();

            SaiServer.Instance.QuestProgressor.ClaimQuest(
                questDefinitionId: questDefinitionId,
                onSuccess: response =>
                {
                    this.claimingQuestId = null;
                    Debug.Log($"[ChainQuestEditor] Quest claimed: id={response.id}, claimed_at={response.claimed_at}");
                    // Claim response has no status field — claimed_at is present, so the quest is "claimed".
                    this.ApplyMemberStatus(questDefinitionId, "claimed");
                    Repaint();
                    // Refresh cached state by issuing a Check
                    this.RunCheckQuest(questDefinitionId);
                },
                onError: error =>
                {
                    this.claimingQuestId = null;
                    Debug.LogError($"[ChainQuestEditor] Claim quest failed ({questDefinitionId}): {error}");
                    Repaint();
                }
            );
        }

        // ── Cache lookups & status helpers ───────────────────────────────────

        /// <summary>
        /// Writes the new status back onto every cached <see cref="ChainMemberData"/>
        /// whose quest_definition_id matches, so the inspector pill updates immediately
        /// after Start / Check / Claim without waiting for a full members reload.
        /// </summary>
        private void ApplyMemberStatus(string questDefinitionId, string newStatus)
        {
            if (string.IsNullOrEmpty(questDefinitionId) || string.IsNullOrEmpty(newStatus)) return;
            foreach (ChainMembersResponse cached in this.membersCache.Values)
            {
                if (cached?.members == null) continue;
                foreach (ChainMemberData m in cached.members)
                {
                    if (m != null && m.quest_definition_id == questDefinitionId)
                        m.status = newStatus;
                }
            }
        }

        private CheckQuestResponse GetCachedMemberCheck(string questDefinitionId)
        {
            if (string.IsNullOrEmpty(questDefinitionId)) return null;
            this.memberCheckCache.TryGetValue(questDefinitionId, out CheckQuestResponse cached);
            return cached;
        }

        /// <summary>
        /// Returns the most authoritative status for a member's quest from the local check cache.
        /// Prefers progress.status (DB truth), falls back to top-level response.status.
        /// </summary>
        private string GetCachedMemberStatus(string questDefinitionId)
        {
            CheckQuestResponse cached = this.GetCachedMemberCheck(questDefinitionId);
            if (cached == null) return null;
            if (!string.IsNullOrEmpty(cached.progress?.status)) return cached.progress.status;
            return cached.status;
        }

        /// <summary>
        /// Compact colored status label for the member header — icon + lowercase status,
        /// sized to content so the row height doesn't grow when collapsed.
        /// </summary>
        private void DrawStatusPill(string status)
        {
            GUIStyle s = new GUIStyle(EditorStyles.miniLabel);
            s.fontSize = 10;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleRight;
            s.normal.textColor = QuestStatusIcons.GetColor(status);
            GUILayout.Label($"{QuestStatusIcons.GetIcon(status)} {status.ToLower()}", s, GUILayout.ExpandWidth(false));
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
            GUIStyle fieldLabel   = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 10 };

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
            if (json.EndsWith("}"))  json = json.Substring(0, json.Length - 1);

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
            if (json.EndsWith("}"))  json = json.Substring(0, json.Length - 1);

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
                char open  = json[i];
                char close = open == '{' ? '}' : ']';
                int start = i, depth = 0;
                while (i < json.Length)
                {
                    if (json[i] == open)  depth++;
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

        // ── Tree ──────────────────────────────────────────────────────────────

        private void DrawTreeNode(QuestTreeNode node, int depth)
        {
            if (node == null) return;

            string indent = new string(' ', depth * 4);
            string statusColor = QuestStatusIcons.GetHex(node.status);
            string statusIcon = QuestStatusIcons.GetIcon(node.status);

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            richStyle.fontSize = 10;
            EditorGUILayout.LabelField(
                $"{indent}<color={statusColor}>{statusIcon}</color> <b>{node.quest_name}</b>  <color={statusColor}>[{node.status}]</color>  <color=#666666>{node.quest_id}</color>",
                richStyle);

            if (node.children != null && node.children.Length > 0)
            {
                foreach (QuestTreeNode child in node.children)
                    this.DrawTreeNode(child, depth + 1);
            }
        }

        // ── Style helpers ─────────────────────────────────────────────────────

        private void DrawSeparator()
        {
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);
        }

        /// <summary>
        /// Draws "CODE: xxx [Copy]" and "Quest Def ID: yyy [Copy]" on two separate rows.
        /// Skips either row if its value is empty.
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
                EditorGUILayout.LabelField($"Quest Def ID: {id}", idStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = id;
                EditorGUILayout.EndHorizontal();
            }
        }

        private Color GetChainTypeColor(string chainType)
        {
            switch ((chainType ?? "").ToLower())
            {
                case "linear":     return new Color(0.4f, 0.8f, 1f);   // Blue
                case "branching":  return new Color(0.8f, 0.5f, 1f);   // Purple
                case "tree":       return new Color(0.4f, 1f, 0.6f);   // Green
                case "dag":        return new Color(1f, 0.7f, 0.3f);   // Orange
                default:           return new Color(0.85f, 0.85f, 0.85f); // Light gray
            }
        }

        // ── Network actions ───────────────────────────────────────────────────

        private void LoadChains()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ChainQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.chainQuest.GetChains(
                onSuccess: response =>
                {
                    Debug.Log($"[ChainQuestEditor] Loaded {response.chains.Length} chains (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ChainQuestEditor] Failed to load chains: {error}");
                }
            );
        }

        private void LoadChainMembers(string chainId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ChainQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingMembers.Add(chainId);
            Repaint();

            this.chainQuest.GetChainMembers(
                chainId: chainId,
                onSuccess: response =>
                {
                    this.loadingMembers.Remove(chainId);
                    this.membersCache[chainId] = response;
                    this.membersFoldout[chainId] = true;
                    Debug.Log($"[ChainQuestEditor] Loaded {response.members?.Length ?? 0} members for chain {chainId}");
                    Repaint();
                },
                onError: error =>
                {
                    this.loadingMembers.Remove(chainId);
                    Debug.LogError($"[ChainQuestEditor] Failed to load members for chain {chainId}: {error}");
                    Repaint();
                }
            );
        }

        private void LoadChainTree(string chainId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ChainQuestEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingTree.Add(chainId);
            Repaint();

            this.chainQuest.GetChainTree(
                chainId: chainId,
                onSuccess: response =>
                {
                    this.loadingTree.Remove(chainId);
                    this.treeCache[chainId] = response;
                    this.treeFoldout[chainId] = true;
                    Debug.Log($"[ChainQuestEditor] Loaded tree for chain {chainId}: {response.nodes?.Length ?? 0} root nodes");
                    Repaint();
                },
                onError: error =>
                {
                    this.loadingTree.Remove(chainId);
                    Debug.LogError($"[ChainQuestEditor] Failed to load tree for chain {chainId}: {error}");
                    Repaint();
                }
            );
        }
    }
}
