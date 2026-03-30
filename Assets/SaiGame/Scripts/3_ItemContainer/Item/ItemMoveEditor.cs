using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemMove))]
    [CanEditMultipleObjects]
    public class ItemMoveEditor : Editor
    {
        private ItemMove itemMove;

        // Serialized properties
        private SerializedProperty playerItemProp;
        private SerializedProperty playerContainerProp;
        private SerializedProperty itemIdProp;
        private SerializedProperty targetContainerIdProp;
        private SerializedProperty quantityProp;
        private SerializedProperty gridXProp;
        private SerializedProperty gridYProp;

        // UI state
        private bool showItemList      = true;
        private bool showContainerList = true;
        private bool showActions       = true;

        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();

        // Running state
        private bool isRunning = false;

        private void OnEnable()
        {
            this.itemMove               = (ItemMove)target;
            this.playerItemProp         = serializedObject.FindProperty("playerItem");
            this.playerContainerProp    = serializedObject.FindProperty("playerContainer");
            this.itemIdProp             = serializedObject.FindProperty("itemId");
            this.targetContainerIdProp  = serializedObject.FindProperty("targetContainerId");
            this.quantityProp           = serializedObject.FindProperty("quantity");
            this.gridXProp              = serializedObject.FindProperty("gridX");
            this.gridYProp              = serializedObject.FindProperty("gridY");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Header ──────────────────────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Move", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "POST /api/v1/games/{game_id}/inventory/move\n" +
                "• Select an item from the list below  →  fills Item ID\n" +
                "• Select a target container           →  fills Target Container ID\n" +
                "• Set quantity, grid_x, grid_y and press Move.",
                MessageType.Info);
            EditorGUILayout.Space();

            // ── References ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.playerItemProp,
                new GUIContent("Player Item", "Source for the item list"));
            EditorGUILayout.PropertyField(this.playerContainerProp,
                new GUIContent("Player Container", "Source for the container list"));

            EditorGUILayout.Space();

            // ── Item List ───────────────────────────────────────────────────────
            this.showItemList = EditorGUILayout.Foldout(this.showItemList, "Items", true);
            if (this.showItemList)
            {
                EditorGUI.indentLevel++;

                PlayerItem playerItem = this.itemMove.PlayerItemRef;
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
                    foreach (InventoryItemData item in playerItem.CurrentInventory.items)
                        this.DrawItemRow(item);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // ── Container List ──────────────────────────────────────────────────
            this.showContainerList = EditorGUILayout.Foldout(this.showContainerList, "Target Containers", true);
            if (this.showContainerList)
            {
                EditorGUI.indentLevel++;

                PlayerContainer playerContainer = this.itemMove.PlayerContainerRef;
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
            EditorGUILayout.PropertyField(this.itemIdProp,
                new GUIContent("Item ID", "The instance ID of the item to move"));
            EditorGUILayout.PropertyField(this.targetContainerIdProp,
                new GUIContent("Target Container ID", "The container to move the item into"));
            EditorGUILayout.PropertyField(this.quantityProp,
                new GUIContent("Quantity", "Amount to move (must be > 0)"));
            EditorGUILayout.PropertyField(this.gridXProp,
                new GUIContent("Grid X", "Target grid column position"));
            EditorGUILayout.PropertyField(this.gridYProp,
                new GUIContent("Grid Y", "Target grid row position"));

            EditorGUILayout.Space();

            // ── Actions ─────────────────────────────────────────────────────────
            this.showActions = EditorGUILayout.Foldout(this.showActions, "Actions", true);
            if (this.showActions)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space(4);

                // Action button
                bool canExecute = !this.isRunning
                    && !string.IsNullOrEmpty(this.itemIdProp.stringValue)
                    && !string.IsNullOrEmpty(this.targetContainerIdProp.stringValue)
                    && this.quantityProp.intValue > 0;

                string btnLabel = this.isRunning ? "Moving..." : "▶  Move Item";

                GUI.backgroundColor = this.isRunning || !canExecute
                    ? Color.gray
                    : new Color(0.4f, 0.8f, 1f);

                EditorGUI.BeginDisabledGroup(!canExecute || this.isRunning);
                if (GUILayout.Button(btnLabel, GUILayout.Height(34)))
                    this.ExecuteMove();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;


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
                
                if (string.IsNullOrEmpty(this.itemIdProp.stringValue))
                    EditorGUILayout.HelpBox("Item ID is required.", MessageType.Warning);
                else if (string.IsNullOrEmpty(this.targetContainerIdProp.stringValue))
                    EditorGUILayout.HelpBox("Target Container ID is required.", MessageType.Warning);
                else if (this.quantityProp.intValue <= 0)
                    EditorGUILayout.HelpBox("Quantity must be greater than 0.", MessageType.Warning);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        // ── Row drawing helpers ────────────────────────────────────────────────

        private void DrawItemRow(InventoryItemData item)
        {
            string name  = item.definition?.name ?? item.item_definition_id;
            string label = $"{name}  [{item.definition?.category ?? ""}]  ×{item.quantity}";

            bool isSelected = this.itemIdProp.stringValue == item.id;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!this.itemFoldouts.ContainsKey(item.id))
                this.itemFoldouts[item.id] = false;

            EditorGUILayout.BeginHorizontal();
            this.itemFoldouts[item.id] = EditorGUILayout.Foldout(this.itemFoldouts[item.id], label, true);

            GUI.backgroundColor = isSelected ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(isSelected ? "✔ Selected" : "Select", GUILayout.Width(80), GUILayout.Height(20)))
            {
                this.itemIdProp.stringValue = isSelected ? "" : item.id;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (this.itemFoldouts[item.id])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Item ID",        item.id);
                EditorGUILayout.LabelField("Definition ID",  item.item_definition_id);
                EditorGUILayout.LabelField("Container ID",   item.item_container_id);
                EditorGUILayout.LabelField("Quantity",       item.quantity.ToString());
                EditorGUILayout.LabelField("Grid",           $"({item.grid_x}, {item.grid_y})");
                if (item.definition != null)
                {
                    EditorGUILayout.LabelField("Category",   item.definition.category);
                    EditorGUILayout.LabelField("Rarity",     item.definition.rarity);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawContainerRow(ContainerData container)
        {
            bool isSelected = this.targetContainerIdProp.stringValue == container.id;
            string label = $"{(string.IsNullOrEmpty(container.container_type) ? container.id : container.container_type)}  [{container.id}]";

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = isSelected ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(isSelected ? "✔ Selected" : "Select", GUILayout.Width(80), GUILayout.Height(20)))
            {
                this.targetContainerIdProp.stringValue = isSelected ? "" : container.id;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void SyncItems()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemMoveEditor] SaiService not found!");
                return;
            }
            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemMoveEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerItem playerItem = this.itemMove.PlayerItemRef;
            if (playerItem == null)
            {
                Debug.LogError("[ItemMoveEditor] No PlayerItem reference assigned!");
                return;
            }

            playerItem.GetItems(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemMoveEditor] Synced {response.items.Length} items.");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemMoveEditor] Sync items failed: {error}");
                }
            );
        }

        private void SyncContainers()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemMoveEditor] SaiService not found!");
                return;
            }
            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemMoveEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerContainer playerContainer = this.itemMove.PlayerContainerRef;
            if (playerContainer == null)
            {
                Debug.LogError("[ItemMoveEditor] No PlayerContainer reference assigned!");
                return;
            }

            playerContainer.GetContainers(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemMoveEditor] Synced {response.containers.Length} containers.");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemMoveEditor] Sync containers failed: {error}");
                }
            );
        }

        private void ExecuteMove()
        {
            string iId  = this.itemIdProp.stringValue;
            string cId  = this.targetContainerIdProp.stringValue;
            int    qty  = this.quantityProp.intValue;
            int    gx   = this.gridXProp.intValue;
            int    gy   = this.gridYProp.intValue;

            if (qty <= 0) return;

            this.isRunning = true;
            Repaint();

            this.itemMove.Move(
                itemId:            iId,
                targetContainerId: cId,
                quantity:          qty,
                gridX:             gx,
                gridY:             gy,
                onSuccess: response =>
                {
                    this.isRunning = false;
                    Debug.Log($"[ItemMoveEditor] Move success: {response}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isRunning = false;
                    Debug.LogError($"[ItemMoveEditor] Move failed: {error}");
                    Repaint();
                }
            );
        }
    }
}
