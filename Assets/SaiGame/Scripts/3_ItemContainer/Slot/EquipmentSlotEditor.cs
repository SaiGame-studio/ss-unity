using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(EquipmentSlot))]
    [CanEditMultipleObjects]
    public class EquipmentSlotEditor : Editor
    {
        private EquipmentSlot equipmentSlotManager;
        private SerializedProperty autoGetSlots;
        private SerializedProperty autoGetEquipped;

        private bool showCurrentSlots = true;
        private bool showSlotList = true;
        private bool showUtilityButtons = true;

        // Per-slot state (keyed by slot.id)
        private readonly Dictionary<string, InventoryItemData> slotAssignments = new Dictionary<string, InventoryItemData>();
        private readonly Dictionary<string, string> slotSearchText = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> slotDropdownOpen = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> slotFoldout = new Dictionary<string, bool>();
        private readonly HashSet<string> slotBusy = new HashSet<string>();
        private readonly Dictionary<string, string> slotDataText = new Dictionary<string, string>();
        private bool isLoadingItems = false;
        private bool isLoadingEquipped = false;

        private void OnEnable()
        {
            this.equipmentSlotManager = (EquipmentSlot)target;
            this.autoGetSlots = serializedObject.FindProperty("autoGetSlots");
            this.autoGetEquipped = serializedObject.FindProperty("autoGetEquipped");

            this.equipmentSlotManager.OnGetEquippedSuccess += this.HandleEquippedLoaded;
            this.equipmentSlotManager.OnGetSlotsSuccess += this.HandleSlotsLoaded;
        }

        private void OnDisable()
        {
            if (this.equipmentSlotManager == null) return;
            this.equipmentSlotManager.OnGetEquippedSuccess -= this.HandleEquippedLoaded;
            this.equipmentSlotManager.OnGetSlotsSuccess -= this.HandleSlotsLoaded;
        }

        private void HandleEquippedLoaded(EquippedItemsResponse response)
        {
            this.PopulateSlotAssignmentsFromEquipped(response);
            this.Repaint();
        }

        private void HandleSlotsLoaded(EquipmentSlotsResponse response)
        {
            // Re-apply any previously loaded equipped data now that slots are available
            if (this.equipmentSlotManager.CurrentEquipped != null)
                this.PopulateSlotAssignmentsFromEquipped(this.equipmentSlotManager.CurrentEquipped);
            this.Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Equipment Slot Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoGetSlots, new GUIContent("Auto Get Slots", "Automatically load equipment slots after successful login"));
            EditorGUILayout.PropertyField(this.autoGetEquipped, new GUIContent("Auto Get Equipped", "Automatically load equipped items after successful login"));

            EditorGUILayout.Space();

            // Current Slot Data
            this.showCurrentSlots = EditorGUILayout.Foldout(this.showCurrentSlots, "Current Equipment Slot Data", true);
            if (this.showCurrentSlots)
            {
                EditorGUI.indentLevel++;

                if (this.equipmentSlotManager.CurrentSlots != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Slots: {this.equipmentSlotManager.CurrentSlots.total}");
                    EditorGUILayout.LabelField($"Loaded Slots: {this.equipmentSlotManager.CurrentSlots.slots?.Length ?? 0}");

                    if (this.equipmentSlotManager.CurrentSlots.slots != null
                        && this.equipmentSlotManager.CurrentSlots.slots.Length > 0)
                    {
                        this.showSlotList = EditorGUILayout.Foldout(this.showSlotList, "Slot List", true);
                        if (this.showSlotList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (EquipmentSlotData slot in this.equipmentSlotManager.CurrentSlots.slots)
                            {
                                this.DrawSlotWithAssignment(slot);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No equipment slot data loaded yet.", MessageType.None);
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

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Get Slots", GUILayout.Height(30)))
                    this.LoadSlots();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = isLoadingEquipped ? Color.gray : new Color(1f, 0.65f, 0.1f);
                EditorGUI.BeginDisabledGroup(isLoadingEquipped);
                if (GUILayout.Button(isLoadingEquipped ? "Loading..." : "Get Equipped", GUILayout.Height(30)))
                    this.LoadEquipped();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = isLoadingItems ? Color.gray : Color.cyan;
                EditorGUI.BeginDisabledGroup(isLoadingItems);
                if (GUILayout.Button(isLoadingItems ? "Syncing..." : "Sync with Player Item", GUILayout.Height(30)))
                    this.LoadItems();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Slots", GUILayout.Height(30)))
                    this.equipmentSlotManager.ClearSlots();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSlotWithAssignment(EquipmentSlotData slot)
        {
            if (!this.slotFoldout.ContainsKey(slot.id))
                this.slotFoldout[slot.id] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Slot header foldout — show equipped indicator in the label
            bool hasItem = this.slotAssignments.TryGetValue(slot.id, out InventoryItemData previewItem) && previewItem != null;
            string foldoutLabel = hasItem
                ? $"{slot.name} ({slot.slot_key})  ●  {previewItem.definition?.name ?? previewItem.id}"
                : $"{slot.name} ({slot.slot_key})";

            this.slotFoldout[slot.id] = EditorGUILayout.Foldout(this.slotFoldout[slot.id], foldoutLabel, true, EditorStyles.foldoutHeader);

            if (!this.slotFoldout[slot.id])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {slot.id}");
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = slot.id;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(slot.description))
                EditorGUILayout.LabelField($"Description: {slot.description}");

            if (slot.metadata != null && !string.IsNullOrEmpty(slot.metadata.slot_type))
                EditorGUILayout.LabelField($"Type: {slot.metadata.slot_type}  |  Icon: {slot.metadata.icon}");

            if (slot.allowed_categories != null && slot.allowed_categories.Length > 0)
            {
                EditorGUILayout.LabelField("Allowed Categories:", EditorStyles.boldLabel);
                this.DrawBadgeRow(slot.allowed_categories, new Color(0.4f, 0.8f, 1f));
            }
            else
            {
                EditorGUILayout.LabelField("Allowed Categories:", EditorStyles.boldLabel);
                this.DrawBadgeRow(new string[] { "(any category)" }, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            }

            EditorGUILayout.Space(2);

            if (slot.allowed_item_definition_ids != null && slot.allowed_item_definition_ids.Length > 0)
            {
                EditorGUILayout.LabelField("Allowed Item Definition IDs:", EditorStyles.boldLabel);
                this.DrawBadgeRow(slot.allowed_item_definition_ids, new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                EditorGUILayout.LabelField("Allowed Item Definition IDs:", EditorStyles.boldLabel);
                this.DrawBadgeRow(new string[] { "(any definition)" }, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            }

            EditorGUILayout.LabelField($"Active: {slot.is_active}");

            EditorGUILayout.Space(4);

            // Assignment area
            bool hasAssignment = this.slotAssignments.TryGetValue(slot.id, out InventoryItemData assignedItem) && assignedItem != null;

            if (hasAssignment)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Equipped Item", EditorStyles.boldLabel);
                string itemName = assignedItem.definition?.name ?? assignedItem.id;
                string itemCategory = assignedItem.definition?.category ?? "-";
                EditorGUILayout.LabelField($"{itemName}  |  Category: {itemCategory}  |  Qty: {assignedItem.quantity}");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Item ID: {assignedItem.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = assignedItem.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                // Slot data editor
                if (!this.slotDataText.ContainsKey(slot.id))
                    this.slotDataText[slot.id] = "{}";

                EditorGUILayout.LabelField("Slot Data (JSON)");
                this.slotDataText[slot.id] = EditorGUILayout.TextArea(
                    this.slotDataText[slot.id],
                    GUILayout.MinHeight(60),
                    GUILayout.ExpandWidth(true)
                );

                bool isBusy = this.slotBusy.Contains(slot.id);

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginDisabledGroup(isBusy);
                GUI.backgroundColor = isBusy ? Color.gray : new Color(1f, 0.85f, 0.2f);
                if (GUILayout.Button(isBusy ? "Updating..." : "Update", GUILayout.Height(24)))
                    this.RequestUpdateSlotData(slot, assignedItem);
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(isBusy);
                GUI.backgroundColor = isBusy ? Color.gray : new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button(isBusy ? "Unequipping..." : "Unequip", GUILayout.Height(24)))
                    this.RequestUnequip(slot, assignedItem);
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                this.DrawItemSearchDropdown(slot);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawBadgeRow(string[] items, Color badgeColor, int perRow = 4)
        {
            GUILayout.Space(2);
            int count = items.Length;
            int i = 0;
            while (i < count)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                int rowEnd = Mathf.Min(i + perRow, count);
                for (int j = i; j < rowEnd; j++)
                {
                    GUI.backgroundColor = badgeColor;
                    GUILayout.Label(items[j], EditorStyles.helpBox, GUILayout.Height(18), GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
                i += perRow;
            }
            GUILayout.Space(2);
        }

        private void DrawItemSearchDropdown(EquipmentSlotData slot)
        {
            InventoryItemData[] allItems = this.GetAvailableItems(slot);

            if (allItems == null || allItems.Length == 0)
            {
                EditorGUILayout.HelpBox("No items loaded. Load inventory via PlayerItem first.", MessageType.None);
                return;
            }

            if (!this.slotSearchText.ContainsKey(slot.id))
                this.slotSearchText[slot.id] = "";

            if (!this.slotDropdownOpen.ContainsKey(slot.id))
                this.slotDropdownOpen[slot.id] = false;

            // Search row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Equip Item", GUILayout.Width(72));

            EditorGUI.BeginChangeCheck();
            string newSearch = EditorGUILayout.TextField(this.slotSearchText[slot.id], GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
            {
                this.slotSearchText[slot.id] = newSearch;
                this.slotDropdownOpen[slot.id] = true;
                Repaint();
            }

            if (GUILayout.Button(this.slotDropdownOpen[slot.id] ? "▲" : "▼", GUILayout.Width(24), GUILayout.Height(20)))
            {
                this.slotDropdownOpen[slot.id] = !this.slotDropdownOpen[slot.id];
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            if (!this.slotDropdownOpen[slot.id]) return;

            // Filter
            string filter = (this.slotSearchText[slot.id] ?? "").ToLowerInvariant();
            List<InventoryItemData> filtered = new List<InventoryItemData>();

            foreach (InventoryItemData item in allItems)
            {
                string name = item.definition?.name ?? item.id;
                string cat = item.definition?.category ?? "";

                if (string.IsNullOrEmpty(filter)
                    || name.ToLowerInvariant().Contains(filter)
                    || cat.ToLowerInvariant().Contains(filter)
                    || item.id.ToLowerInvariant().Contains(filter))
                {
                    // skip items already equipped in another slot
                    bool usedElsewhere = false;
                    foreach (KeyValuePair<string, InventoryItemData> kv in this.slotAssignments)
                    {
                        if (kv.Key != slot.id && kv.Value != null && kv.Value.id == item.id)
                        {
                            usedElsewhere = true;
                            break;
                        }
                    }
                    if (!usedElsewhere)
                        filtered.Add(item);
                }
            }

            if (filtered.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("No items match the search.", MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUI.indentLevel++;
            int displayLimit = Mathf.Min(filtered.Count, 8);

            for (int i = 0; i < displayLimit; i++)
            {
                InventoryItemData item = filtered[i];
                string name = item.definition?.name ?? item.id;
                string cat = item.definition?.category ?? "-";
                string label = $"{name}  [{cat}]  ×{item.quantity}";

                bool busy = this.slotBusy.Contains(slot.id);
                EditorGUI.BeginDisabledGroup(busy);
                GUI.backgroundColor = busy ? Color.gray : new Color(0.65f, 1f, 0.65f);
                if (GUILayout.Button(label, GUILayout.Height(22)))
                    this.RequestEquip(slot, item);
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();
            }

            if (filtered.Count > 8)
                EditorGUILayout.LabelField($"... and {filtered.Count - 8} more. Refine your search.", EditorStyles.miniLabel);

            EditorGUI.indentLevel--;
        }

        private InventoryItemData[] GetAvailableItems(EquipmentSlotData slot)
        {
            InventoryItemData[] all = SaiService.Instance?.PlayerItem?.CurrentInventory?.items;
            if (all == null) return null;

            bool hasDefinitions = slot.allowed_item_definition_ids != null && slot.allowed_item_definition_ids.Length > 0;
            bool hasCategories = slot.allowed_categories != null && slot.allowed_categories.Length > 0;

            // No restrictions — return everything
            if (!hasDefinitions && !hasCategories) return all;

            // Definition IDs take full priority — when present, filter by them exclusively
            if (hasDefinitions)
            {
                List<InventoryItemData> result = new List<InventoryItemData>();
                foreach (InventoryItemData item in all)
                {
                    foreach (string defId in slot.allowed_item_definition_ids)
                    {
                        if (defId == item.item_definition_id)
                        {
                            result.Add(item);
                            break;
                        }
                    }
                }
                return result.ToArray();
            }

            // Fall back to category filter
            {
                List<InventoryItemData> result = new List<InventoryItemData>();
                foreach (InventoryItemData item in all)
                {
                    if (item.definition == null) continue;
                    foreach (string cat in slot.allowed_categories)
                    {
                        if (cat == item.definition.category)
                        {
                            result.Add(item);
                            break;
                        }
                    }
                }
                return result.ToArray();
            }
        }

        private void RequestEquip(EquipmentSlotData slot, InventoryItemData item)
        {
            if (SaiService.Instance == null || !SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated!");
                return;
            }

            this.slotBusy.Add(slot.id);
            Repaint();

            this.equipmentSlotManager.EquipItem(
                itemId: item.id,
                slotKey: slot.slot_key,
                slotDataJson: "{}",
                onSuccess: _ =>
                {
                    this.slotAssignments[slot.id] = item;
                    this.slotDropdownOpen[slot.id] = false;
                    this.slotSearchText[slot.id] = "";
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                },
                onError: error =>
                {
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                    Debug.LogError($"[EquipmentSlotEditor] Equip failed: {error}");
                }
            );
        }

        private void RequestUpdateSlotData(EquipmentSlotData slot, InventoryItemData item)
        {
            if (SaiService.Instance == null || !SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated!");
                return;
            }

            if (!this.slotDataText.TryGetValue(slot.id, out string slotDataJson) || string.IsNullOrEmpty(slotDataJson))
                slotDataJson = "{}";

            this.slotBusy.Add(slot.id);
            Repaint();

            this.equipmentSlotManager.EquipItem(
                itemId: item.id,
                slotKey: slot.slot_key,
                slotDataJson: slotDataJson,
                onSuccess: _ =>
                {
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                },
                onError: error =>
                {
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                    Debug.LogError($"[EquipmentSlotEditor] Update slot data failed: {error}");
                }
            );
        }

        private void RequestUnequip(EquipmentSlotData slot, InventoryItemData item)
        {
            if (SaiService.Instance == null || !SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated!");
                return;
            }

            this.slotBusy.Add(slot.id);
            Repaint();

            this.equipmentSlotManager.UnequipItem(
                itemId: item.id,
                onSuccess: () =>
                {
                    this.slotAssignments.Remove(slot.id);
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                },
                onError: error =>
                {
                    this.slotBusy.Remove(slot.id);
                    Repaint();
                    Debug.LogError($"[EquipmentSlotEditor] Unequip failed: {error}");
                }
            );
        }

        private void LoadItems()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[EquipmentSlotEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated! Please login first.");
                return;
            }

            PlayerItem playerItem = SaiService.Instance.PlayerItem;
            if (playerItem == null)
            {
                Debug.LogError("[EquipmentSlotEditor] PlayerItem not found on SaiService!");
                return;
            }

            this.isLoadingItems = true;
            Repaint();

            playerItem.GetItems(
                onSuccess: response =>
                {
                    this.isLoadingItems = false;
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[EquipmentSlotEditor] Loaded {response.items?.Length ?? 0} items (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoadingItems = false;
                    Repaint();
                    Debug.LogError($"[EquipmentSlotEditor] Failed to load items: {error}");
                }
            );
        }

        private void LoadSlots()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[EquipmentSlotEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated! Please login first.");
                return;
            }

            this.equipmentSlotManager.GetSlots(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[EquipmentSlotEditor] Loaded {response.slots?.Length ?? 0} slots (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[EquipmentSlotEditor] Failed to load slots: {error}");
                }
            );
        }

        private void LoadEquipped()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[EquipmentSlotEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[EquipmentSlotEditor] Not authenticated! Please login first.");
                return;
            }

            this.isLoadingEquipped = true;
            Repaint();

            this.equipmentSlotManager.GetEquippedItems(
                onSuccess: response =>
                {
                    this.isLoadingEquipped = false;
                    this.PopulateSlotAssignmentsFromEquipped(response);
                    Repaint();
                },
                onError: error =>
                {
                    this.isLoadingEquipped = false;
                    Repaint();
                    Debug.LogError($"[EquipmentSlotEditor] Failed to load equipped items: {error}");
                }
            );
        }

        // Matches each equipped entry (by slot_key) to its EquipmentSlotData, then finds
        // the InventoryItemData by item_id so the assignment boxes light up immediately.
        private void PopulateSlotAssignmentsFromEquipped(EquippedItemsResponse response)
        {
            if (response?.equipped == null) return;

            EquipmentSlotsResponse slotsResponse = this.equipmentSlotManager.CurrentSlots;
            if (slotsResponse?.slots == null) return;

            InventoryItemData[] inventory = SaiService.Instance?.PlayerItem?.CurrentInventory?.items;

            this.slotAssignments.Clear();

            foreach (EquippedItemData equipped in response.equipped)
            {
                // Find the matching slot definition by slot_key
                EquipmentSlotData matchedSlot = null;
                foreach (EquipmentSlotData slot in slotsResponse.slots)
                {
                    if (slot.slot_key == equipped.slot_key)
                    {
                        matchedSlot = slot;
                        break;
                    }
                }

                if (matchedSlot == null) continue;

                // Try to find a full InventoryItemData from the inventory
                InventoryItemData matchedItem = null;
                if (inventory != null)
                {
                    foreach (InventoryItemData invItem in inventory)
                    {
                        if (invItem.id == equipped.item_id)
                        {
                            matchedItem = invItem;
                            break;
                        }
                    }
                }

                // Fall back to a synthetic placeholder so the UI still shows something
                if (matchedItem == null)
                {
                    matchedItem = new InventoryItemData
                    {
                        id = equipped.item_id,
                        item_definition_id = equipped.item_definition_id,
                        definition = new ItemDefinitionData { name = equipped.item_name, category = equipped.category }
                    };
                }

                this.slotAssignments[matchedSlot.id] = matchedItem;

                // Load slot_data JSON into the textarea for this slot
                string rawSlotData = equipped.slot_data_raw;
                this.slotDataText[matchedSlot.id] = !string.IsNullOrEmpty(rawSlotData) ? rawSlotData : "{}";
            }
        }
    }
}
