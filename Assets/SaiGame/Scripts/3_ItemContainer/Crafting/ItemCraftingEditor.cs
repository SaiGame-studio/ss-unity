using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemCrafting))]
    [CanEditMultipleObjects]
    public class ItemCraftingEditor : Editor
    {
        private ItemCrafting crafting;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty historyPage;
        private SerializedProperty historyPageSize;

        private bool showCurrentHistory = true;
        private bool showTransactionList = true;
        private bool showUtilityButtons = true;
        private bool showTestCrafting = true;

        // Collapse states per transaction (keyed by transaction id)
        private readonly Dictionary<string, bool> expandedTransactions = new Dictionary<string, bool>();

        private string testRecipeId = "";
        private string testIdempotencyKey = "";

        private void OnEnable()
        {
            this.crafting = (ItemCrafting)this.target;
            this.autoLoadOnLogin = this.serializedObject.FindProperty("autoLoadOnLogin");
            this.historyPage = this.serializedObject.FindProperty("historyPage");
            this.historyPageSize = this.serializedObject.FindProperty("historyPageSize");
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Crafting Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load history when user logs in"));

            // Query params (always visible, small)
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(this.historyPage, new GUIContent("Page"));
            EditorGUILayout.PropertyField(this.historyPageSize, new GUIContent("Page Size"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            // Current History Data
            this.showCurrentHistory = EditorGUILayout.Foldout(this.showCurrentHistory, "Current History Data", true);
            if (this.showCurrentHistory)
            {
                EditorGUI.indentLevel++;

                if (this.crafting.CurrentHistory != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Transactions: {this.crafting.CurrentHistory.total}");
                    EditorGUILayout.LabelField($"Current Page: {this.crafting.CurrentHistory.page}");
                    EditorGUILayout.LabelField($"Loaded Records: {this.crafting.CurrentHistory.transactions?.Length ?? 0}");

                    if (this.crafting.CurrentHistory.transactions != null
                        && this.crafting.CurrentHistory.transactions.Length > 0)
                    {
                        this.showTransactionList = EditorGUILayout.Foldout(this.showTransactionList, "Transaction List", true);
                        if (this.showTransactionList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (CraftingHistoryTransaction tx in this.crafting.CurrentHistory.transactions)
                                this.DrawTransactionCard(tx);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No history loaded yet.", MessageType.None);
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
                if (GUILayout.Button("Get History", GUILayout.Height(30)))
                    this.LoadHistory();

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear History", GUILayout.Height(30)))
                    this.crafting.ClearHistory();

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Test Crafting
            this.showTestCrafting = EditorGUILayout.Foldout(this.showTestCrafting, "Test Crafting", true);
            if (this.showTestCrafting)
            {
                EditorGUI.indentLevel++;

                // Recipe ID belongs to the crafting receipt definition, not the item recipe definition
                EditorGUILayout.HelpBox("Recipe ID: ID of the crafting receipt definition (not the item recipe definition).", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                this.testRecipeId = EditorGUILayout.TextField("Recipe ID", this.testRecipeId);
                GUI.backgroundColor = new Color(1f, 0.84f, 0f);
                if (GUILayout.Button("Craft", GUILayout.Width(80), GUILayout.Height(18)))
                    this.CraftTest();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                this.testIdempotencyKey = EditorGUILayout.TextField("Idempotency Key", this.testIdempotencyKey);
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button("UUID", GUILayout.Width(50), GUILayout.Height(18)))
                    this.testIdempotencyKey = System.Guid.NewGuid().ToString();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            this.serializedObject.ApplyModifiedProperties();
        }

        private void DrawTransactionCard(CraftingHistoryTransaction tx)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // === COLLAPSIBLE HEADER ===
            string txId = tx.id;
            if (!this.expandedTransactions.ContainsKey(txId))
                this.expandedTransactions[txId] = false;

            string recipeName = tx.recipe_detail?.name ?? "Unknown Recipe";
            bool isSuccess = tx.success;
            string headerLabel = $"★ {recipeName}  [{tx.status}]";

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor = isSuccess ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            foldoutStyle.onNormal.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.focused.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.onFocused.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.active.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.onActive.textColor = foldoutStyle.normal.textColor;

            EditorGUILayout.BeginHorizontal();
            this.expandedTransactions[txId] = EditorGUILayout.Foldout(this.expandedTransactions[txId], headerLabel, true, foldoutStyle);

            // Status badge (right-aligned)
            GUIStyle statusBadge = new GUIStyle(EditorStyles.label);
            statusBadge.fontSize = 11;
            statusBadge.normal.textColor = isSuccess ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            statusBadge.fontStyle = FontStyle.Bold;
            statusBadge.alignment = TextAnchor.MiddleRight;
            EditorGUILayout.LabelField(tx.status.ToUpper(), statusBadge, GUILayout.MinWidth(70));
            EditorGUILayout.EndHorizontal();

            if (!this.expandedTransactions[txId])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            // Separator
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);

            // === COMPACT INFO ===
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            if (tx.recipe_detail != null)
                EditorGUILayout.LabelField($"KEY: {tx.recipe_detail.recipe_key}", labelStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {tx.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = tx.id;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Date: {tx.created_at}", labelStyle);

            EditorGUILayout.Space(6);

            // === STATUS BLOCK ===
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle statusHeader = new GUIStyle(EditorStyles.label);
            statusHeader.fontSize = 11;
            statusHeader.fontStyle = FontStyle.Bold;
            statusHeader.normal.textColor = new Color(1f, 0.7f, 0.2f);
            EditorGUILayout.LabelField("⚡ CRAFT STATUS", statusHeader);

            GUIStyle rowLabelStyle = new GUIStyle(EditorStyles.label);
            rowLabelStyle.fontSize = 11;
            rowLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            GUIStyle rowValueStyle = new GUIStyle(EditorStyles.boldLabel);
            rowValueStyle.fontSize = 11;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Success:", rowLabelStyle, GUILayout.Width(110));
            rowValueStyle.normal.textColor = isSuccess ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(isSuccess ? "✔ Yes" : "✘ No", rowValueStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bonus:", rowLabelStyle, GUILayout.Width(110));
            rowValueStyle.normal.textColor = tx.bonus_triggered ? new Color(1f, 0.84f, 0f) : new Color(0.5f, 0.5f, 0.5f);
            EditorGUILayout.LabelField(tx.bonus_triggered ? "★ Triggered" : "— None", rowValueStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // === MATERIAL SNAPSHOT ===
            if (tx.materials_snapshot != null && tx.materials_snapshot.Length > 0)
            {
                GUIStyle sectionHeader = new GUIStyle(EditorStyles.boldLabel);
                sectionHeader.fontSize = 11;
                sectionHeader.normal.textColor = new Color(1f, 0.7f, 0.5f);
                EditorGUILayout.LabelField($"🧱 MATERIALS ({tx.materials_snapshot.Length})", sectionHeader);
                EditorGUILayout.Space(2);

                foreach (CraftingMaterialItem mat in tx.materials_snapshot)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    GUIStyle matNameStyle = new GUIStyle(EditorStyles.label);
                    matNameStyle.fontSize = 10;
                    matNameStyle.fontStyle = FontStyle.Bold;
                    matNameStyle.normal.textColor = new Color(1f, 0.7f, 0.5f);
                    EditorGUILayout.LabelField(mat.item_definition_name, matNameStyle);

                    GUIStyle rowLabel = new GUIStyle(EditorStyles.label);
                    rowLabel.fontSize = 9;
                    rowLabel.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Quantity:", rowLabel, GUILayout.Width(70));
                    GUIStyle qtyStyle = new GUIStyle(EditorStyles.label);
                    qtyStyle.fontSize = 10;
                    qtyStyle.fontStyle = FontStyle.Bold;
                    qtyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                    EditorGUILayout.LabelField($"x{mat.quantity}", qtyStyle);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Consumed:", rowLabel, GUILayout.Width(70));
                    GUIStyle consumedStyle = new GUIStyle(EditorStyles.label);
                    consumedStyle.fontSize = 10;
                    consumedStyle.normal.textColor = mat.was_consumed ? new Color(1f, 0.4f, 0.4f) : new Color(0.3f, 1f, 0.5f);
                    EditorGUILayout.LabelField(mat.was_consumed ? "✘ Consumed" : "✔ Kept", consumedStyle);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            // === OUTPUT SNAPSHOT ===
            if (tx.outputs_snapshot != null && tx.outputs_snapshot.Length > 0)
            {
                GUIStyle sectionHeader = new GUIStyle(EditorStyles.boldLabel);
                sectionHeader.fontSize = 11;
                sectionHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField($"📤 OUTPUTS ({tx.outputs_snapshot.Length})", sectionHeader);
                EditorGUILayout.Space(2);

                foreach (CraftingOutputItem output in tx.outputs_snapshot)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    GUIStyle itemNameStyle = new GUIStyle(EditorStyles.label);
                    itemNameStyle.fontSize = 10;
                    itemNameStyle.fontStyle = FontStyle.Bold;
                    itemNameStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                    EditorGUILayout.LabelField(output.item_definition_name, itemNameStyle);

                    EditorGUILayout.BeginHorizontal();
                    GUIStyle qtyLabelStyle = new GUIStyle(EditorStyles.label);
                    qtyLabelStyle.fontSize = 9;
                    qtyLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                    EditorGUILayout.LabelField("Quantity:", qtyLabelStyle, GUILayout.Width(70));
                    GUIStyle qtyValueStyle = new GUIStyle(EditorStyles.label);
                    qtyValueStyle.fontSize = 10;
                    qtyValueStyle.fontStyle = FontStyle.Bold;
                    qtyValueStyle.normal.textColor = new Color(0.4f, 1f, 0.6f);
                    EditorGUILayout.LabelField($"x{output.quantity}", qtyValueStyle);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void LoadHistory()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemCraftingEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemCraftingEditor] Not authenticated!");
                return;
            }

            this.crafting.GetCraftingHistory(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemCraftingEditor] Loaded {response.transactions.Length} transactions.");
                    this.Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemCraftingEditor] Load history failed: {error}");
                }
            );
        }

        private void CraftTest()
        {
            if (string.IsNullOrEmpty(this.testRecipeId))
            {
                Debug.LogError("[ItemCraftingEditor] Recipe ID cannot be empty.");
                return;
            }

            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemCraftingEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemCraftingEditor] Not authenticated!");
                return;
            }

            string idempotencyKey = string.IsNullOrEmpty(this.testIdempotencyKey)
                ? null
                : this.testIdempotencyKey;

            this.crafting.Craft(
                this.testRecipeId,
                idempotencyKey: idempotencyKey,
                onSuccess: response =>
                {
                    Debug.Log($"[ItemCraftingEditor] Craft OK. Tx: {response.transaction_id}");
                    this.Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemCraftingEditor] Craft failed: {error}");
                }
            );
        }
    }
}
