using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemSwap))]
    [CanEditMultipleObjects]
    public class ItemSwapEditor : Editor
    {
        private ItemSwap itemSwap;

        // Serialized properties
        private SerializedProperty playerItemProp;
        private SerializedProperty playerContainerProp;
        private SerializedProperty itemAIdProp;
        private SerializedProperty itemBIdProp;

        // UI state
        private bool showItemList = true;
        private bool showActions  = true;

        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, Color> containerColorMap = new Dictionary<string, Color>();
        private int containerColorIndex = 0;

        private static readonly Color[] containerPalette = new Color[]
        {
            new Color(0.2f, 0.8f, 0.4f),   // green
            new Color(0.3f, 0.6f, 1.0f),   // blue
            new Color(1.0f, 0.6f, 0.2f),   // orange
            new Color(0.8f, 0.3f, 0.8f),   // purple
            new Color(1.0f, 0.85f, 0.2f),  // yellow
            new Color(0.2f, 0.85f, 0.85f), // cyan
            new Color(1.0f, 0.4f, 0.4f),   // red
            new Color(0.6f, 0.8f, 0.2f),   // lime
        };

        // Running state
        private bool isRunning = false;

        private void OnEnable()
        {
            this.itemSwap      = (ItemSwap)target;
            this.playerItemProp      = serializedObject.FindProperty("playerItem");
            this.playerContainerProp = serializedObject.FindProperty("playerContainer");
            this.itemAIdProp         = serializedObject.FindProperty("itemAId");
            this.itemBIdProp         = serializedObject.FindProperty("itemBId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Header ──────────────────────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Swap", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "POST /api/v1/games/{game_id}/inventory/swap\n" +
                "• Click an item → fills Item A ID\n" +
                "• Click another item → fills Item B ID\n" +
                "• Press Swap to exchange their positions/containers.",
                MessageType.Info);
            EditorGUILayout.Space();

            // ── References ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.playerItemProp,
                new GUIContent("Player Item", "Source for the item list"));
            EditorGUILayout.PropertyField(this.playerContainerProp,
                new GUIContent("Player Container", "Source for container name lookup"));

            EditorGUILayout.Space();

            // ── Item List ───────────────────────────────────────────────────────
            this.showItemList = EditorGUILayout.Foldout(this.showItemList, "Items", true);
            if (this.showItemList)
            {
                EditorGUI.indentLevel++;

                PlayerItem playerItem = this.itemSwap.PlayerItemRef;
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

            // ── Input Fields ────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.itemAIdProp,
                new GUIContent("Item A ID", "First item instance to swap"));
            EditorGUILayout.PropertyField(this.itemBIdProp,
                new GUIContent("Item B ID", "Second item instance to swap"));

            // Same-item warning
            if (!string.IsNullOrEmpty(this.itemAIdProp.stringValue)
                && this.itemAIdProp.stringValue == this.itemBIdProp.stringValue)
            {
                EditorGUILayout.HelpBox("Item A and Item B must not be the same.", MessageType.Error);
            }

            EditorGUILayout.Space();

            // ── Actions ─────────────────────────────────────────────────────────
            this.showActions = EditorGUILayout.Foldout(this.showActions, "Actions", true);
            if (this.showActions)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space(4);

                // Action button
                bool canExecute = !this.isRunning
                    && !string.IsNullOrEmpty(this.itemAIdProp.stringValue)
                    && !string.IsNullOrEmpty(this.itemBIdProp.stringValue)
                    && this.itemAIdProp.stringValue != this.itemBIdProp.stringValue;

                string btnLabel = this.isRunning ? "Swapping..." : "▶  Swap Items";

                GUI.backgroundColor = this.isRunning || !canExecute
                    ? Color.gray
                    : new Color(1f, 0.85f, 0.3f);

                EditorGUI.BeginDisabledGroup(!canExecute || this.isRunning);
                if (GUILayout.Button(btnLabel, GUILayout.Height(34)))
                    this.ExecuteSwap();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                // Sync row
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Sync Items", GUILayout.Height(28)))
                    this.SyncItems();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                if (string.IsNullOrEmpty(this.itemAIdProp.stringValue))
                    EditorGUILayout.HelpBox("Item A ID is required.", MessageType.Warning);
                else if (string.IsNullOrEmpty(this.itemBIdProp.stringValue))
                    EditorGUILayout.HelpBox("Item B ID is required.", MessageType.Warning);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        // ── Row drawing helpers ────────────────────────────────────────────────

        private void DrawItemRow(InventoryItemData item)
        {
            string name  = item.definition?.name ?? item.item_definition_id;
            string containerName = this.GetContainerName(item.item_container_id);
            string label = $"{name}  [{item.definition?.category ?? ""}]  ×{item.quantity}";

            bool isSelectedA = this.itemAIdProp.stringValue == item.id;
            bool isSelectedB = this.itemBIdProp.stringValue == item.id;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!this.itemFoldouts.ContainsKey(item.id))
                this.itemFoldouts[item.id] = false;

            EditorGUILayout.BeginHorizontal();
            this.itemFoldouts[item.id] = EditorGUILayout.Foldout(this.itemFoldouts[item.id], label, true);

            // Container name tag (colored text)
            if (!string.IsNullOrEmpty(containerName))
            {
                Color tagColor = this.GetContainerColor(item.item_container_id);
                GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = tagColor },
                };
                GUIContent tagContent = new GUIContent(containerName);
                float tagWidth = tagStyle.CalcSize(tagContent).x + 8f;
                GUILayout.Label(tagContent, tagStyle, GUILayout.Width(tagWidth), GUILayout.Height(20f));
            }

            // Button A
            GUI.backgroundColor = isSelectedA ? new Color(0.4f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(isSelectedA ? "✔ A" : "A", GUILayout.Width(40), GUILayout.Height(20)))
            {
                this.itemAIdProp.stringValue = isSelectedA ? "" : item.id;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }

            // Button B
            GUI.backgroundColor = isSelectedB ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(isSelectedB ? "✔ B" : "B", GUILayout.Width(40), GUILayout.Height(20)))
            {
                this.itemBIdProp.stringValue = isSelectedB ? "" : item.id;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (this.itemFoldouts[item.id])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Item ID",       item.id);
                EditorGUILayout.LabelField("Definition ID", item.item_definition_id);
                EditorGUILayout.LabelField("Container ID",  item.item_container_id);
                EditorGUILayout.LabelField("Quantity",      item.quantity.ToString());
                EditorGUILayout.LabelField("Grid",          $"({item.grid_x}, {item.grid_y})");
                if (item.definition != null)
                {
                    EditorGUILayout.LabelField("Category",  item.definition.category);
                    EditorGUILayout.LabelField("Rarity",    item.definition.rarity);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // ── Container color helper ────────────────────────────────────────────

        private Color GetContainerColor(string containerId)
        {
            if (this.containerColorMap.TryGetValue(containerId, out Color color))
                return color;

            color = containerPalette[this.containerColorIndex % containerPalette.Length];
            this.containerColorIndex++;
            this.containerColorMap[containerId] = color;
            return color;
        }

        // ── Container name lookup ─────────────────────────────────────────────

        private string GetContainerName(string containerId)
        {
            if (string.IsNullOrEmpty(containerId)) return null;

            PlayerContainer playerContainer = this.itemSwap.PlayerContainerRef;
            if (playerContainer == null || !playerContainer.HasContainers) return null;

            ContainerData container = playerContainer.GetContainerById(containerId);
            return container?.definition?.name;
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void SyncItems()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[ItemSwapEditor] SaiServer not found!");
                return;
            }
            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemSwapEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerItem playerItem = this.itemSwap.PlayerItemRef;
            if (playerItem == null)
            {
                Debug.LogError("[ItemSwapEditor] No PlayerItem reference assigned!");
                return;
            }

            playerItem.GetItems(
                onSuccess: response =>
                {
                    Debug.Log($"[ItemSwapEditor] Synced {response.items.Length} items.");
                    Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[ItemSwapEditor] Sync items failed: {error}");
                }
            );
        }

        private void ExecuteSwap()
        {
            string aId = this.itemAIdProp.stringValue;
            string bId = this.itemBIdProp.stringValue;

            this.isRunning = true;
            Repaint();

            this.itemSwap.Swap(
                itemAId:   aId,
                itemBId:   bId,
                onSuccess: response =>
                {
                    this.isRunning = false;
                    Debug.Log($"[ItemSwapEditor] Swap success: {response}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isRunning = false;
                    Debug.LogError($"[ItemSwapEditor] Swap failed: {error}");
                    Repaint();
                }
            );
        }
    }
}
