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

        // Per-chain members cache: chainId → response
        private readonly Dictionary<string, ChainMembersResponse> membersCache = new Dictionary<string, ChainMembersResponse>();
        // Per-chain members foldout state
        private readonly Dictionary<string, bool> membersFoldout = new Dictionary<string, bool>();
        // Per-chain members loading state
        private readonly HashSet<string> loadingMembers = new HashSet<string>();

        // Per-chain tree cache: chainId → response
        private readonly Dictionary<string, ChainQuestTreeResponse> treeCache = new Dictionary<string, ChainQuestTreeResponse>();
        // Per-chain tree foldout state
        private readonly Dictionary<string, bool> treeFoldout = new Dictionary<string, bool>();
        // Per-chain tree loading state
        private readonly HashSet<string> loadingTree = new HashSet<string>();

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

        private void DrawChainSummary(ChainQuestData chain)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"{chain.display_name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {chain.id}");
            EditorGUILayout.LabelField($"Key: {chain.chain_key}");
            EditorGUILayout.LabelField($"Type: {chain.chain_type}  |  Active: {chain.is_active}");

            if (!string.IsNullOrEmpty(chain.description))
                EditorGUILayout.LabelField($"Description: {chain.description}");

            EditorGUILayout.LabelField($"Created: {chain.created_at}");

            EditorGUILayout.Space(4);

            // Members button
            bool isLoading = this.loadingMembers.Contains(chain.id);
            bool hasCached = this.membersCache.ContainsKey(chain.id);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isLoading ? Color.gray : new Color(0.4f, 1f, 0.6f);
            EditorGUI.BeginDisabledGroup(isLoading);
            if (GUILayout.Button(isLoading ? "Loading..." : "Quest Flat", GUILayout.Height(24)))
                this.LoadChainMembers(chain.id);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            if (hasCached)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Clear", GUILayout.Height(24), GUILayout.Width(50)))
                {
                    this.membersCache.Remove(chain.id);
                    this.membersFoldout.Remove(chain.id);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Inline members display
            if (hasCached)
            {
                ChainMembersResponse cached = this.membersCache[chain.id];

                if (!this.membersFoldout.ContainsKey(chain.id))
                    this.membersFoldout[chain.id] = true;

                this.membersFoldout[chain.id] = EditorGUILayout.Foldout(
                    this.membersFoldout[chain.id],
                    $"Quest Flat ({cached.members?.Length ?? 0})",
                    true);

                if (this.membersFoldout[chain.id] && cached.members != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (ChainMemberData member in cached.members)
                        this.DrawMemberSummary(member);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(2);

            // Tree button
            bool isLoadingTree = this.loadingTree.Contains(chain.id);
            bool hasCachedTree = this.treeCache.ContainsKey(chain.id);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isLoadingTree ? Color.gray : new Color(0.6f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(isLoadingTree);
            if (GUILayout.Button(isLoadingTree ? "Loading..." : "Quest Tree", GUILayout.Height(24)))
                this.LoadChainTree(chain.id);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            if (hasCachedTree)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Clear", GUILayout.Height(24), GUILayout.Width(50)))
                {
                    this.treeCache.Remove(chain.id);
                    this.treeFoldout.Remove(chain.id);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Inline tree display
            if (hasCachedTree)
            {
                ChainQuestTreeResponse cachedTree = this.treeCache[chain.id];

                if (!this.treeFoldout.ContainsKey(chain.id))
                    this.treeFoldout[chain.id] = true;

                this.treeFoldout[chain.id] = EditorGUILayout.Foldout(
                    this.treeFoldout[chain.id],
                    $"Quest Tree — {cachedTree.chain_name} ({cachedTree.nodes?.Length ?? 0} root nodes)",
                    true);

                if (this.treeFoldout[chain.id] && cachedTree.nodes != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (QuestTreeNode node in cachedTree.nodes)
                        this.DrawTreeNode(node, 0);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMemberSummary(ChainMemberData member)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string questName = member.definition != null ? member.definition.name : member.quest_definition_id;
            EditorGUILayout.LabelField(questName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Member ID: {member.id}");
            EditorGUILayout.LabelField($"Quest Def ID: {member.quest_definition_id}");
            EditorGUILayout.LabelField($"Sort Order: {member.sort_order}");

            if (member.unlock_quest_ids != null && member.unlock_quest_ids.Length > 0)
                EditorGUILayout.LabelField($"Unlocks: {string.Join(", ", member.unlock_quest_ids)}");

            if (member.definition != null)
            {
                QuestDefinitionData def = member.definition;
                EditorGUILayout.LabelField($"Type: {def.quest_type}  |  Active: {def.is_active}");

                if (!string.IsNullOrEmpty(def.description))
                    EditorGUILayout.LabelField($"Description: {def.description}");

                if (def.conditions != null && def.conditions.clauses != null && def.conditions.clauses.Length > 0)
                {
                    GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
                    EditorGUILayout.LabelField($"<b>Conditions ({def.conditions.operator_type})</b>", richStyle);
                    foreach (QuestClause clause in def.conditions.clauses)
                        EditorGUILayout.LabelField($"  • [{clause.clause_id}] {clause.type}");
                }

                if (def.rewards != null && def.rewards.Length > 0)
                {
                    EditorGUILayout.LabelField($"Rewards ({def.rewards.Length}):");
                    foreach (QuestReward reward in def.rewards)
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

            EditorGUILayout.EndVertical();
        }

        private void LoadChains()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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

        private void DrawTreeNode(QuestTreeNode node, int depth)
        {
            if (node == null) return;

            string indent = new string(' ', depth * 4);
            string statusColor = node.status == "completed" ? "#00FF88"
                               : node.status == "active"    ? "#FFD700"
                               : "#AAAAAA";

            GUIStyle richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            EditorGUILayout.LabelField(
                $"{indent}<b>{node.quest_name}</b>  <color={statusColor}>[{node.status}]</color>  <color=#888888>{node.quest_id}</color>",
                richStyle);

            if (node.children != null && node.children.Length > 0)
            {
                EditorGUI.indentLevel++;
                foreach (QuestTreeNode child in node.children)
                    this.DrawTreeNode(child, depth + 1);
                EditorGUI.indentLevel--;
            }
        }

        private void LoadChainMembers(string chainId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ChainQuestEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
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
