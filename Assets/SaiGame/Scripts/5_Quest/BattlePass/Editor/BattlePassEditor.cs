using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(BattlePass))]
    public class BattlePassEditor : Editor
    {
        private BattlePass battlePass;
        private readonly Dictionary<string, bool> expandedBattlePasses = new Dictionary<string, bool>();
        private readonly HashSet<string> loadingBattlePassIds = new HashSet<string>();

        private void OnEnable()
        {
            this.battlePass = (BattlePass)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "currentBattlePassResponse");

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(1f, 0.72f, 0.3f);
            if (GUILayout.Button("Load Battle Passes", GUILayout.Height(30)))
                this.LoadBattlePasses();
            GUI.backgroundColor = Color.white;

            BattlePassResponse response = this.battlePass.CurrentBattlePassResponse;
            if (response == null || response.pools == null)
            {
                EditorGUILayout.HelpBox("No battle passes loaded yet.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Battle Passes: {response.pools.Length} loaded / {response.total} total", EditorStyles.boldLabel);
                foreach (BattlePassData battlePassData in response.pools)
                    this.DrawBattlePass(battlePassData);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBattlePass(BattlePassData battlePassData)
        {
            if (battlePassData == null || string.IsNullOrEmpty(battlePassData.id)) return;

            if (!this.expandedBattlePasses.ContainsKey(battlePassData.id))
                this.expandedBattlePasses[battlePassData.id] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.78f, 0.32f) },
                onNormal = { textColor = new Color(1f, 0.78f, 0.32f) }
            };
            this.expandedBattlePasses[battlePassData.id] = EditorGUILayout.Foldout(
                this.expandedBattlePasses[battlePassData.id],
                battlePassData.display_name,
                true,
                foldoutStyle);

            GUI.backgroundColor = this.loadingBattlePassIds.Contains(battlePassData.id) ? Color.gray : new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button(this.loadingBattlePassIds.Contains(battlePassData.id) ? "Loading..." : "Load Chains", GUILayout.Width(105)))
                this.LoadChains(battlePassData.id);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (this.expandedBattlePasses[battlePassData.id])
            {
                this.DrawSeparator();
                this.DrawDetailRow("Battle pass key", battlePassData.pool_key);
                this.DrawCopyableIdRow("Battle pass identifier", battlePassData.id);
                this.DrawStatusRow("Configuration status", battlePassData.is_active);
                if (!string.IsNullOrEmpty(battlePassData.description))
                    this.DrawDetailRow("Description", battlePassData.description);

                this.DrawSessionSchedule(battlePassData.type_config?.session);

                BattlePassChainsResponse chainsResponse = this.battlePass.GetCachedChains(battlePassData.id);
                if (chainsResponse != null)
                {
                    this.DrawDetailRow("Current session state", chainsResponse.pool_state);
                    this.DrawChains(chainsResponse);
                }
                else
                {
                    EditorGUILayout.HelpBox("Load chains to view the current session state and assigned chain list.", MessageType.None);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSessionSchedule(BattlePassSessionData session)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Session Schedule", EditorStyles.boldLabel);
            if (session == null)
            {
                EditorGUILayout.HelpBox("No session schedule was included in the battle pass response.", MessageType.Warning);
                return;
            }

            switch (session.schedule_mode)
            {
                case "fixed":
                    this.DrawDetailRow("Schedule type", "Fixed");
                    this.DrawDetailRow("Session start time (UTC)", session.session_start_at);
                    this.DrawDetailRow("Session end time (UTC)", session.session_end_at);
                    break;
                case "annual":
                    this.DrawDetailRow("Schedule type", "Annual");
                    this.DrawDetailRow("Session start time (UTC)", session.session_start_at);
                    this.DrawDetailRow("Session end time (UTC)", session.session_end_at);
                    break;
                case "interval":
                    this.DrawDetailRow("Schedule type", "Interval");
                    this.DrawDetailRow("Cycle start time (UTC)", session.cycle_start_at);
                    this.DrawDetailRow("Repeats every", $"{session.repeat_amount} {session.repeat_type}");
                    break;
                default:
                    EditorGUILayout.HelpBox("The battle pass response contains an unsupported session schedule.", MessageType.Warning);
                    break;
            }
        }

        private void DrawChains(BattlePassChainsResponse response)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Chains ({response.chains?.Length ?? 0})", EditorStyles.boldLabel);
            if (response.chains == null || response.chains.Length == 0)
            {
                EditorGUILayout.HelpBox("This battle pass has no assigned chains.", MessageType.Info);
                return;
            }

            foreach (BattlePassChainData chainData in response.chains)
            {
                ChainQuestData chain = chainData?.chain;
                if (chain == null) continue;
                this.DrawChainCard(chain);
            }
        }

        private void DrawChainCard(ChainQuestData chain)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = this.GetChainTypeColor(chain.chain_type) }
            };
            EditorGUILayout.LabelField(chain.display_name, nameStyle);

            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = chain.is_active ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.7f) }
            };
            EditorGUILayout.LabelField(chain.is_active ? "ACTIVE" : "INACTIVE", statusStyle, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            this.DrawDetailRow("Chain key", chain.chain_key);
            this.DrawDetailRow("Chain type", chain.chain_type);
            if (!string.IsNullOrEmpty(chain.description))
                this.DrawDetailRow("Description", chain.description);
            this.DrawCopyableIdRow("Chain identifier", chain.id);

            GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
            if (GUILayout.Button("Send ID to ChainQuest"))
                this.SendChainIdToChainQuest(chain.id);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value)) value = "Not available";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(145));
            EditorGUILayout.SelectableLabel(value, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusRow(string label, bool isActive)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(145));
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = isActive ? new Color(0.3f, 0.9f, 0.5f) : new Color(1f, 0.55f, 0.35f) }
            };
            EditorGUILayout.LabelField(isActive ? "ACTIVE" : "INACTIVE", statusStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCopyableIdRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(145));
            EditorGUILayout.SelectableLabel(value, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = value;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
            EditorGUILayout.Space(4);
        }

        private Color GetChainTypeColor(string chainType)
        {
            switch ((chainType ?? string.Empty).ToLowerInvariant())
            {
                case "linear": return new Color(0.4f, 0.8f, 1f);
                case "branching": return new Color(0.8f, 0.5f, 1f);
                case "tree": return new Color(0.4f, 1f, 0.6f);
                case "dag": return new Color(1f, 0.7f, 0.3f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        private void LoadBattlePasses()
        {
            this.battlePass.GetBattlePasses(
                onSuccess: _ => Repaint(),
                onError: error => Debug.LogError($"[BattlePassEditor] Failed to load battle passes: {error}"));
        }

        private void SendChainIdToChainQuest(string chainId)
        {
            ChainQuest chainQuest = SaiServer.Instance?.ChainQuest;
            if (chainQuest == null)
            {
                Debug.LogError("[BattlePassEditor] ChainQuest service not found.");
                return;
            }

            chainQuest.SetChainId(chainId);
            EditorUtility.SetDirty(chainQuest);
            chainQuest.GetChains(
                onSuccess: response =>
                {
                    Debug.Log($"[BattlePassEditor] Loaded {response.chains?.Length ?? 0} chain from Battle Pass selection: {chainId}");
                },
                onError: error =>
                {
                    Debug.LogError($"[BattlePassEditor] Failed to load selected chain {chainId}: {error}");
                });
        }

        private void LoadChains(string battlePassId)
        {
            if (this.loadingBattlePassIds.Contains(battlePassId)) return;

            this.loadingBattlePassIds.Add(battlePassId);
            this.battlePass.GetBattlePassChains(
                battlePassId,
                onSuccess: _ =>
                {
                    this.loadingBattlePassIds.Remove(battlePassId);
                    this.expandedBattlePasses[battlePassId] = true;
                    Repaint();
                },
                onError: error =>
                {
                    this.loadingBattlePassIds.Remove(battlePassId);
                    Debug.LogError($"[BattlePassEditor] Failed to load battle pass chains: {error}");
                    Repaint();
                });
        }
    }
}
