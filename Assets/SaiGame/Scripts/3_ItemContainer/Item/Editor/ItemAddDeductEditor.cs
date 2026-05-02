using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemAddDeduct))]
    [CanEditMultipleObjects]
    public class ItemAddDeductEditor : Editor
    {
        private ItemAddDeduct itemAddDeduct;

        // Serialized properties
        private SerializedProperty playerItemProp;
        private SerializedProperty playerContainerProp;
        private SerializedProperty itemDefinitionIdProp;
        private SerializedProperty containerIdProp;
        private SerializedProperty quantityProp;

        // UI state
        private bool showItemList      = true;
        private bool showContainerList = true;
        private bool showActions       = true;

        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();

        // Running state
        private bool isRunning = false;

        private void OnEnable()
        {
            this.itemAddDeduct        = (ItemAddDeduct)target;
            this.playerItemProp       = serializedObject.FindProperty("playerItem");
            this.playerContainerProp  = serializedObject.FindProperty("playerContainer");
            this.itemDefinitionIdProp = serializedObject.FindProperty("itemDefinitionId");
            this.containerIdProp      = serializedObject.FindProperty("containerId");
            this.quantityProp         = serializedObject.FindProperty("quantity");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Header ──────────────────────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Add / Deduct Qty", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "PUT /api/v2/games/{game_id}/item-inventories/{item_definition_id}/qty\n" +
                "• Positive quantity  →  Add items\n" +
                "• Negative quantity  →  Deduct items\n\n" +
                "⚠ Only items with  Allow Client Update Qty = true  are permitted by the server.\n" +
                "   Items not meeting this condition are hidden from the list below.",
                MessageType.Info);
            EditorGUILayout.Space();

            // ── References ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.playerItemProp,
                new GUIContent("Player Item", "Source for the eligible item list"));
            EditorGUILayout.PropertyField(this.playerContainerProp,
                new GUIContent("Player Container", "Source for the container list"));

            EditorGUILayout.Space();

            // ── Item List ───────────────────────────────────────────────────────
            this.showItemList = EditorGUILayout.Foldout(this.showItemList, "Eligible Items  (allow_client_update_qty = true)", true);
            if (this.showItemList)
            {
                EditorGUI.indentLevel++;

                PlayerItem playerItem = this.itemAddDeduct.PlayerItemRef;
                bool hasItems = playerItem != null
                    && playerItem.CurrentInventory != null
                    && playerItem.CurrentInventory.items != null
                    && playerItem.CurrentInventory.items.Length > 0;

                if (!hasItems)
                {
                    EditorGUILayout.HelpBox(
                        playerItem == null
                            ? "Assign a PlayerItem reference above."
                            : "No inventory loaded. Press 'Sync Items' below.",
                        MessageType.None);
                }
                else
                {
                    int eligibleCount = 0;
                    foreach (InventoryItemData item in playerItem.CurrentInventory.items)
                    {
                        if (item.definition == null || !item.definition.allow_client_update_qty)
                            continue;

                        eligibleCount++;
                        this.DrawItemRow(item);
                    }

                    if (eligibleCount == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "No items have 'Allow Client Update Qty' enabled.\n" +
                            "Enable this flag on the item definition in the server/dashboard.",
                            MessageType.Warning);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Container List ──────────────────────────────────────────────────
            this.showContainerList = EditorGUILayout.Foldout(this.showContainerList, "Containers", true);
            if (this.showContainerList)
            {
                EditorGUI.indentLevel++;

                PlayerContainer playerContainer = this.itemAddDeduct.PlayerContainerRef;
                bool hasContainers = playerContainer != null
                    && playerContainer.CurrentContainers != null
                    && playerContainer.CurrentContainers.containers != null
                    && playerContainer.CurrentContainers.containers.Length > 0;

                if (!hasContainers)
                {
                    EditorGUILayout.HelpBox(
                        playerContainer == null
                            ? "Assign a PlayerContainer reference above."
                            : "No containers loaded. Press 'Sync Containers' below.",
                        MessageType.None);
                }
                else
                {
                    foreach (ContainerData container in playerContainer.CurrentContainers.containers)
                        this.DrawContainerRow(container);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Input Fields ────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.itemDefinitionIdProp,
                new GUIContent("Item Definition ID", "Used in the URL path — must have allow_client_update_qty = true"));
            EditorGUILayout.PropertyField(this.containerIdProp,
                new GUIContent("Container ID (optional)", "Sent in the request body if set"));
            EditorGUILayout.PropertyField(this.quantityProp,
                new GUIContent("Quantity", "Positive = Add  |  Negative = Deduct  |  0 = disabled"));

            // Inline eligibility warning when the typed definition ID belongs to an item that is NOT eligible
            this.DrawEligibilityWarning();

            EditorGUILayout.Space();

            // ── Actions ─────────────────────────────────────────────────────────
            this.showActions = EditorGUILayout.Foldout(this.showActions, "Actions", true);
            if (this.showActions)
            {
                EditorGUI.indentLevel++;

                // Sync row
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Sync Items", GUILayout.Height(28)))
                    this.SyncItems();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.5f, 0.9f, 1f);
                if (GUILayout.Button("Sync Containers", GUILayout.Height(28)))
                    this.SyncContainers();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Action button
                int  qty        = this.quantityProp.intValue;
                bool canExecute = !this.isRunning
                    && !string.IsNullOrEmpty(this.itemDefinitionIdProp.stringValue)
                    && qty != 0;

                string btnLabel = this.isRunning
                    ? (qty > 0 ? "Adding..." : "Deducting...")
                    : (qty > 0 ? "▶  Add Item Qty" : qty < 0 ? "▶  Deduct Item Qty" : "▶  Add / Deduct");

                GUI.backgroundColor = this.isRunning || qty == 0
                    ? Color.gray
                    : qty > 0 ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.55f, 0.4f);

                EditorGUI.BeginDisabledGroup(!canExecute || this.isRunning);
                if (GUILayout.Button(btnLabel, GUILayout.Height(34)))
                    this.ExecuteAddDeduct();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                if (string.IsNullOrEmpty(this.itemDefinitionIdProp.stringValue))
                    EditorGUILayout.HelpBox("Item Definition ID is required.", MessageType.Warning);
                else if (qty == 0)
                    EditorGUILayout.HelpBox("Quantity must not be 0. Positive = Add, Negative = Deduct.", MessageType.Warning);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        // ── Eligibility check ──────────────────────────────────────────────────

        /// <summary>
        /// If the currently typed Item Definition ID matches a loaded item that does NOT have
        /// allow_client_update_qty = true, show a warning so the user knows the API will reject the request.
        /// </summary>
        private void DrawEligibilityWarning()
        {
            string defId = this.itemDefinitionIdProp.stringValue;
            if (string.IsNullOrEmpty(defId)) return;

            PlayerItem playerItem = this.itemAddDeduct.PlayerItemRef;
            if (playerItem?.CurrentInventory?.items == null) return;

            foreach (InventoryItemData item in playerItem.CurrentInventory.items)
            {
                if (item.item_definition_id != defId) continue;

                // Found the item — check eligibility
                if (item.definition != null && !item.definition.allow_client_update_qty)
                {
                    EditorGUILayout.HelpBox(
                        $"⚠ Item \"{item.definition.name}\" has  Allow Client Update Qty = false.\n" +
                        "The server will reject this request. Enable the flag on the item definition first.",
                        MessageType.Error);
                }
                return; // stop after first match
            }
        }

        // ── Row drawing helpers ────────────────────────────────────────────────

        private void DrawItemRow(InventoryItemData item)
        {
            string defId = item.item_definition_id;
            string name  = item.definition?.name ?? defId;
            string label = $"{name}  [{item.definition?.category ?? ""}]  ×{item.quantity}";

            bool isSelected = this.itemDefinitionIdProp.stringValue == defId;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!this.itemFoldouts.ContainsKey(defId))
                this.itemFoldouts[defId] = false;

            EditorGUILayout.BeginHorizontal();
            this.itemFoldouts[defId] = EditorGUILayout.Foldout(this.itemFoldouts[defId], label, true);

            GUI.backgroundColor = isSelected ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(isSelected ? "✔ Selected" : "Select", GUILayout.Width(80), GUILayout.Height(20)))
            {
                this.itemDefinitionIdProp.stringValue = isSelected ? "" : defId;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (this.itemFoldouts[defId])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Item ID", item.id);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Definition ID", defId);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = defId;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Container ID", item.item_container_id);
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.item_container_id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Quantity",            item.quantity.ToString());
                if (item.definition != null)
                {
                    EditorGUILayout.LabelField("Category",            item.definition.category);
                    EditorGUILayout.LabelField("Rarity",              item.definition.rarity);
                    EditorGUILayout.LabelField("Allow Client Qty ✔",  "true");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawContainerRow(ContainerData container)
        {
            bool isSelected = this.containerIdProp.stringValue == container.id;
            string label = $"{(string.IsNullOrEmpty(container.container_type) ? container.id : container.container_type)}  [{container.id}]";

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = isSelected ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(isSelected ? "✔ Selected" : "Select", GUILayout.Width(80), GUILayout.Height(20)))
            {
                this.containerIdProp.stringValue = isSelected ? "" : container.id;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void SyncItems()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ItemAddDeductEditor] SaiServer not found!");
                return;
            }
            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemAddDeductEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerItem playerItem = this.itemAddDeduct.PlayerItemRef;
            if (playerItem == null)
            {
                Debug.LogError("[ItemAddDeductEditor] No PlayerItem reference assigned!");
                return;
            }

            playerItem.GetItems(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemAddDeductEditor] Synced {response.items.Length} items.");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemAddDeductEditor] Sync items failed: {error}");
                }
            );
        }

        private void SyncContainers()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ItemAddDeductEditor] SaiServer not found!");
                return;
            }
            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemAddDeductEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerContainer playerContainer = this.itemAddDeduct.PlayerContainerRef;
            if (playerContainer == null)
            {
                Debug.LogError("[ItemAddDeductEditor] No PlayerContainer reference assigned!");
                return;
            }

            playerContainer.GetContainers(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemAddDeductEditor] Synced {response.containers.Length} containers.");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemAddDeductEditor] Sync containers failed: {error}");
                }
            );
        }

        private void ExecuteAddDeduct()
        {
            string defId = this.itemDefinitionIdProp.stringValue;
            string conId = this.containerIdProp.stringValue;
            int    qty   = this.quantityProp.intValue;

            if (qty == 0) return;

            this.isRunning = true;
            Repaint();

            this.itemAddDeduct.AddDeduct(
                itemDefinitionId: defId,
                quantity:         qty,
                containerId:      string.IsNullOrEmpty(conId) ? null : conId,
                onSuccess: response =>
                {
                    this.isRunning = false;
                    Debug.Log($"[ItemAddDeductEditor] AddDeduct success: {response}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isRunning = false;
                    Debug.LogError($"[ItemAddDeductEditor] AddDeduct failed: {error}");
                    Repaint();
                }
            );
        }
    }
}
