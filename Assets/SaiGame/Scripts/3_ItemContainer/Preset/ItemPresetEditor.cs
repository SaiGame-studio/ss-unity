using UnityEditor;
using UnityEngine;
using System.Linq;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemPreset))]
    [CanEditMultipleObjects]
    public class ItemPresetEditor : Editor
    {
        private ItemPreset itemPreset;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty createMode;
        private SerializedProperty codeName;
        private SerializedProperty definitionId;
        private SerializedProperty presetName;

        private bool showCurrentPresets = true;
        private bool showPresetList = true;
        private bool showUtilityButtons = true;

        private System.Collections.Generic.Dictionary<string, bool> presetFoldouts = new System.Collections.Generic.Dictionary<string, bool>();
        private System.Collections.Generic.Dictionary<string, int> presetDropdownIndices = new System.Collections.Generic.Dictionary<string, int>();
        private System.Collections.Generic.Dictionary<string, int> presetSlotIndices = new System.Collections.Generic.Dictionary<string, int>();
        private System.Collections.Generic.Dictionary<string, bool> presetSlotFoldouts = new System.Collections.Generic.Dictionary<string, bool>();

        private void OnEnable()
        {
            this.itemPreset = (ItemPreset)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.createMode = serializedObject.FindProperty("createMode");
            this.codeName = serializedObject.FindProperty("codeName");
            this.definitionId = serializedObject.FindProperty("definitionId");
            this.presetName = serializedObject.FindProperty("presetName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Preset Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin,
                new GUIContent("Auto Load on Login", "Automatically load presets when the user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Create Preset Input", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("pick create mode: code name or definition id (from admin /games/id/items, tab preset)", MessageType.Info);

            EditorGUILayout.PropertyField(this.createMode,
                new GUIContent("create mode", "Choose whether to create the preset by code_name or definition_id"));

            ItemPreset.CreatePresetMode mode = (ItemPreset.CreatePresetMode)this.createMode.enumValueIndex;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            if (mode == ItemPreset.CreatePresetMode.CodeName)
            {
                this.codeName.stringValue = EditorGUILayout.TextField(
                    new GUIContent("code name", "Preset definition code_name (e.g. deck_492)"),
                    this.codeName.stringValue);
            }
            else
            {
                this.definitionId.stringValue = EditorGUILayout.TextField(
                    new GUIContent("definition id", "Preset definition id (uuid)"),
                    this.definitionId.stringValue);
            }

            this.presetName.stringValue = EditorGUILayout.TextField(
                new GUIContent("preset name", "Optional display name for the new preset"),
                this.presetName.stringValue);
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Create", GUILayout.Width(60), GUILayout.ExpandHeight(true)))
            {
                this.InvokeCreatePreset(mode);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Current Preset Data foldout
            this.showCurrentPresets = EditorGUILayout.Foldout(this.showCurrentPresets, "Current Preset Data", true);
            if (this.showCurrentPresets)
            {
                EditorGUI.indentLevel++;

                if (this.itemPreset.CurrentPresets != null && this.itemPreset.CurrentPresets.containers != null)
                {
                    EditorGUILayout.LabelField($"Loaded Presets: {this.itemPreset.CurrentPresets.containers.Length}");

                    if (this.itemPreset.CurrentPresets.containers.Length > 0)
                    {
                        this.showPresetList = EditorGUILayout.Foldout(this.showPresetList, "Preset List", true);
                        if (this.showPresetList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (PresetData preset in this.itemPreset.CurrentPresets.containers)
                            {
                                this.DrawPresetSummary(preset);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No preset data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons foldout
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Create Preset", GUILayout.Height(30)))
                {
                    this.InvokeCreatePreset((ItemPreset.CreatePresetMode)this.createMode.enumValueIndex);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Presets", GUILayout.Height(30)))
                {
                    this.itemPreset.GetPresets(
                        onSuccess: presets =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log($"[Editor] Presets loaded: {presets.containers?.Length ?? 0} presets");
                            Repaint();
                        },
                        onError: error =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Get presets failed: {error}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Presets", GUILayout.Height(30)))
                {
                    this.itemPreset.ClearPresets();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;


                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Sync with PlayerItem", GUILayout.Height(30)))
                {
                    PlayerItem pItem = FindObjectOfType<PlayerItem>();
                    if (pItem != null)
                    {
                        pItem.GetItems(null, null, null,
                            onSuccess: res =>
                            {
                                if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                    Debug.Log($"[Editor] Loaded {res.items.Length} inventory items");
                                Repaint();
                            },
                            onError: err =>
                            {
                                if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                    Debug.LogError($"[Editor] Failed to load inventory items: {err}");
                            });
                    }
                    else
                    {
                        Debug.LogWarning("[Editor] No PlayerItem instance found in the scene. Please click 'Get Items' on PlayerItem manually.");
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Events are automatically registered/unregistered with SaiAuth login/logout events.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void InvokeDeletePreset(PresetData preset)
        {
            string displayLabel = !string.IsNullOrEmpty(preset.name) ? preset.name : preset.id;
            if (!EditorUtility.DisplayDialog(
                "Delete Preset",
                $"Are you sure you want to delete preset \"{displayLabel}\"?\n\nThis cannot be undone.",
                "Delete",
                "Cancel"))
                return;

            this.itemPreset.DeletePreset(preset.id,
                onSuccess: deletedId =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[Editor] Preset {deletedId} deleted from server");
                    this.presetFoldouts.Remove(deletedId);
                    Repaint();
                },
                onError: err =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[Editor] Failed to delete preset: {err}");
                }
            );
        }

        private void InvokeGetPresetSlots(string presetId)
        {
            this.itemPreset.GetPreset(presetId,
                onSuccess: updatedPreset =>
                {
                    if (this.itemPreset.CurrentPresets != null && this.itemPreset.CurrentPresets.containers != null)
                    {
                        for (int i = 0; i < this.itemPreset.CurrentPresets.containers.Length; i++)
                        {
                            if (this.itemPreset.CurrentPresets.containers[i].id == updatedPreset.id)
                            {
                                this.itemPreset.CurrentPresets.containers[i] = updatedPreset;
                                break;
                            }
                        }
                    }
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[Editor] Refreshed preset {presetId} from server");
                    Repaint();
                },
                onError: err =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[Editor] Failed to refresh preset: {err}");
                }
            );
        }

        private void InvokeCreatePreset(ItemPreset.CreatePresetMode mode)
        {
            System.Action<PresetData> onSuccess = preset =>
            {
                if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                    Debug.Log($"[Editor] Preset created! ID: {preset.id} | Type: {preset.preset_type} | Max Slots: {preset.max_slots}");
                Repaint();
            };

            System.Action<string> onError = error =>
            {
                if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                    Debug.LogError($"[Editor] Create preset failed: {error}");
            };

            if (mode == ItemPreset.CreatePresetMode.CodeName)
            {
                this.itemPreset.CreatePresetByCodeName(
                    this.codeName.stringValue,
                    this.presetName.stringValue,
                    onSuccess,
                    onError);
            }
            else
            {
                this.itemPreset.CreatePresetByDefinitionId(
                    this.definitionId.stringValue,
                    this.presetName.stringValue,
                    onSuccess,
                    onError);
            }
        }

        private void DrawPresetSummary(PresetData preset)
        {
            string presetId = string.IsNullOrEmpty(preset.id) ? "unknown" : preset.id;
            if (!this.presetFoldouts.TryGetValue(presetId, out bool isExpanded))
            {
                isExpanded = false;
                this.presetFoldouts[presetId] = isExpanded;
            }

            string displayName = !string.IsNullOrEmpty(preset.name)
                ? preset.name
                : (preset.definition != null && !string.IsNullOrEmpty(preset.definition.name)
                    ? preset.definition.name
                    : $"Preset: {preset.id}");

            int slotCount = preset.slots != null ? preset.slots.Length : 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // === COLLAPSIBLE HEADER ===
            string headerLabel = $"★ {displayName}  [{slotCount}/{preset.max_slots}]";

            Color typeColor = this.GetPresetTypeColor(preset.preset_type);
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor = typeColor;
            foldoutStyle.onNormal.textColor = typeColor;
            foldoutStyle.focused.textColor = typeColor;
            foldoutStyle.onFocused.textColor = typeColor;
            foldoutStyle.active.textColor = typeColor;
            foldoutStyle.onActive.textColor = typeColor;

            EditorGUILayout.BeginHorizontal();
            this.presetFoldouts[presetId] = EditorGUILayout.Foldout(isExpanded, headerLabel, true, foldoutStyle);

            // Type badge (right-aligned)
            if (!string.IsNullOrEmpty(preset.preset_type))
            {
                GUIStyle typeBadgeStyle = new GUIStyle(EditorStyles.label);
                typeBadgeStyle.fontSize = 11;
                typeBadgeStyle.fontStyle = FontStyle.Bold;
                typeBadgeStyle.normal.textColor = typeColor;
                typeBadgeStyle.alignment = TextAnchor.MiddleRight;
                EditorGUILayout.LabelField(preset.preset_type.ToUpper(), typeBadgeStyle, GUILayout.MinWidth(70));
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑 Delete", GUILayout.Width(80)))
            {
                this.InvokeDeletePreset(preset);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!this.presetFoldouts[presetId])
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

            // === PRESET TYPE BANNER ===
            this.DrawPresetTypeBanner(preset.preset_type, preset.is_temp);

            // === COMPACT INFO ===
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);

            if (!string.IsNullOrEmpty(preset.name))
                EditorGUILayout.LabelField($"NAME: {preset.name}", labelStyle);
            EditorGUILayout.LabelField($"MAX SLOTS: {preset.max_slots}    USED: {slotCount}    IS TEMP: {preset.is_temp}", labelStyle);
            if (!string.IsNullOrEmpty(preset.created_at))
                EditorGUILayout.LabelField($"CREATED: {preset.created_at}", labelStyle);
            if (!string.IsNullOrEmpty(preset.updated_at))
                EditorGUILayout.LabelField($"UPDATED: {preset.updated_at}", labelStyle);

            // Preset ID + actions
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {preset.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = preset.id;
            EditorGUILayout.EndHorizontal();

            // Definition ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"DEF: {preset.definition_id}", labelStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = preset.definition_id;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // === DEFINITION CARD ===
            if (preset.definition != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUIStyle defHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                defHeaderStyle.fontSize = 11;
                defHeaderStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField("📐 DEFINITION", defHeaderStyle);

                EditorGUILayout.Space(2);

                GUIStyle defValueStyle = new GUIStyle(EditorStyles.label);
                defValueStyle.fontSize = 10;
                defValueStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

                if (!string.IsNullOrEmpty(preset.definition.name))
                    EditorGUILayout.LabelField($"Name: {preset.definition.name}", defValueStyle);
                if (!string.IsNullOrEmpty(preset.definition.code_name))
                    EditorGUILayout.LabelField($"Code Name: {preset.definition.code_name}", defValueStyle);
                EditorGUILayout.LabelField($"Type: {preset.definition.preset_type}    Max Slots: {preset.definition.max_slots}", defValueStyle);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Def ID: {preset.definition.id}", labelStyle);
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = preset.definition.id;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No Definition payload received", MessageType.Warning);
            }

            EditorGUILayout.Space(6);

            // === SLOTS SECTION ===
            GUIStyle slotsHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            slotsHeaderStyle.fontSize = 11;
            slotsHeaderStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🎰 PRESET SLOTS ({slotCount}/{preset.max_slots})", slotsHeaderStyle);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("🔄 Get Preset Slots", GUILayout.Width(140)))
            {
                this.InvokeGetPresetSlots(preset.id);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            if (preset.slots != null && preset.slots.Length > 0)
            {
                PlayerItem pItemInfo = FindObjectOfType<PlayerItem>();
                var sortedSlots = System.Linq.Enumerable.OrderBy(preset.slots, s => s.slot_index).ToArray();

                foreach (var slot in sortedSlots)
                {
                    string slotKey = $"{preset.id}_{slot.slot_index}";
                    if (!this.presetSlotFoldouts.ContainsKey(slotKey))
                        this.presetSlotFoldouts[slotKey] = false;

                    InventoryItemData itemInfo = null;
                    if (pItemInfo != null && pItemInfo.CurrentInventory != null && pItemInfo.CurrentInventory.items != null)
                    {
                        itemInfo = System.Array.Find(pItemInfo.CurrentInventory.items, i => i.id == slot.inventory_item_id);
                    }

                    string itemName = (itemInfo != null && itemInfo.definition != null && !string.IsNullOrEmpty(itemInfo.definition.name))
                        ? itemInfo.definition.name
                        : "Unknown Item";

                    Color slotRarityColor = (itemInfo != null && itemInfo.definition != null)
                        ? this.GetRarityColor(itemInfo.definition.rarity)
                        : new Color(0.7f, 0.7f, 0.7f);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    GUIStyle slotFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                    slotFoldoutStyle.fontSize = 12;
                    slotFoldoutStyle.fontStyle = FontStyle.Bold;
                    slotFoldoutStyle.normal.textColor = slotRarityColor;
                    slotFoldoutStyle.onNormal.textColor = slotRarityColor;
                    slotFoldoutStyle.focused.textColor = slotRarityColor;
                    slotFoldoutStyle.onFocused.textColor = slotRarityColor;
                    slotFoldoutStyle.active.textColor = slotRarityColor;
                    slotFoldoutStyle.onActive.textColor = slotRarityColor;

                    string slotDisplayName = $"🎯 Slot {slot.slot_index}  ·  {itemName}";
                    if (itemInfo != null)
                        slotDisplayName += $"  ×{itemInfo.quantity}";

                    EditorGUILayout.BeginHorizontal();
                    this.presetSlotFoldouts[slotKey] = EditorGUILayout.Foldout(this.presetSlotFoldouts[slotKey], slotDisplayName, true, slotFoldoutStyle);

                    if (itemInfo != null && itemInfo.definition != null && !string.IsNullOrEmpty(itemInfo.definition.rarity))
                    {
                        GUIStyle rarityBadge = new GUIStyle(EditorStyles.label);
                        rarityBadge.fontSize = 10;
                        rarityBadge.fontStyle = FontStyle.Bold;
                        rarityBadge.normal.textColor = slotRarityColor;
                        rarityBadge.alignment = TextAnchor.MiddleRight;
                        EditorGUILayout.LabelField(itemInfo.definition.rarity.ToUpper(), rarityBadge, GUILayout.MinWidth(70));
                    }
                    EditorGUILayout.EndHorizontal();

                    if (this.presetSlotFoldouts[slotKey])
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Inventory ID: {slot.inventory_item_id}", labelStyle);
                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                            GUIUtility.systemCopyBuffer = slot.inventory_item_id;
                        EditorGUILayout.EndHorizontal();

                        if (itemInfo != null)
                        {
                            GUIStyle slotValueStyle = new GUIStyle(EditorStyles.label);
                            slotValueStyle.fontSize = 10;
                            slotValueStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                            EditorGUILayout.LabelField($"Quantity: {itemInfo.quantity}", slotValueStyle);

                            if (itemInfo.definition != null)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"Def ID: {itemInfo.definition.id}", labelStyle);
                                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                    GUIUtility.systemCopyBuffer = itemInfo.definition.id;
                                EditorGUILayout.EndHorizontal();
                                EditorGUILayout.LabelField($"Category: {itemInfo.definition.category}", slotValueStyle);
                                EditorGUILayout.LabelField($"Item Code: {itemInfo.definition.item_code}", slotValueStyle);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Full item data not found in PlayerItem inventory. Click 'Sync Player Inventory'.", MessageType.None);
                        }
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No items in this preset.", MessageType.None);
            }

            EditorGUILayout.Space(6);

            // === ADD ITEM TO SLOT ===
            GUIStyle addHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            addHeaderStyle.fontSize = 11;
            addHeaderStyle.normal.textColor = new Color(0.4f, 1f, 0.6f);
            EditorGUILayout.LabelField("➕ ADD ITEM TO SLOT", addHeaderStyle);

            PlayerItem playerItem = FindObjectOfType<PlayerItem>();
            if (playerItem == null || playerItem.CurrentInventory == null || playerItem.CurrentInventory.items == null || playerItem.CurrentInventory.items.Length == 0)
            {
                EditorGUILayout.HelpBox("No PlayerItem or Inventory data found. Click 'Sync Player Inventory' under Utility Actions first.", MessageType.Warning);
            }
            else
            {
                var items = playerItem.CurrentInventory.items;
                string[] options = new string[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    string name = items[i].definition != null && !string.IsNullOrEmpty(items[i].definition.name)
                        ? items[i].definition.name
                        : "Unknown";
                    options[i] = $"{name} (x{items[i].quantity}) - {items[i].id}";
                }

                if (!this.presetDropdownIndices.ContainsKey(presetId))
                    this.presetDropdownIndices[presetId] = 0;

                if (!this.presetSlotIndices.ContainsKey(presetId))
                    this.presetSlotIndices[presetId] = 0;

                int selectedIndex = this.presetDropdownIndices[presetId];
                if (selectedIndex >= items.Length) selectedIndex = 0;

                this.presetSlotIndices[presetId] = EditorGUILayout.IntField("Target Slot Index:", this.presetSlotIndices[presetId]);

                EditorGUILayout.BeginHorizontal();
                this.presetDropdownIndices[presetId] = EditorGUILayout.Popup(selectedIndex, options);

                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
                if (GUILayout.Button("Add", GUILayout.Width(60), GUILayout.Height(22)))
                {
                    string selectedInventoryId = items[this.presetDropdownIndices[presetId]].id;
                    int selectedSlotIndex = this.presetSlotIndices[presetId];
                    this.itemPreset.AddItemToPreset(preset.id, selectedSlotIndex, selectedInventoryId,
                        onSuccess: p =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log($"[Editor] Item {selectedInventoryId} added to preset {preset.id} at slot {selectedSlotIndex}");

                            if (this.itemPreset.CurrentPresets != null && this.itemPreset.CurrentPresets.containers != null)
                            {
                                for (int j = 0; j < this.itemPreset.CurrentPresets.containers.Length; j++)
                                {
                                    if (this.itemPreset.CurrentPresets.containers[j].id == p.id)
                                    {
                                        this.itemPreset.CurrentPresets.containers[j] = p;
                                        break;
                                    }
                                }
                            }
                            Repaint();
                        },
                        onError: err =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Failed to add item: {err}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawPresetTypeBanner(string presetType, bool isTemp)
        {
            string dest = string.IsNullOrEmpty(presetType) ? "unknown" : presetType.ToLower();

            string icon;
            Color bg;
            Color fg;

            switch (dest)
            {
                case "desk":
                    icon = "🃏";
                    bg = new Color(0.15f, 0.45f, 0.85f);
                    fg = Color.white;
                    break;
                case "loadout":
                case "equipment":
                    icon = "⚔";
                    bg = new Color(0.6f, 0.25f, 0.75f);
                    fg = Color.white;
                    break;
                case "hotbar":
                    icon = "🎮";
                    bg = new Color(0.85f, 0.55f, 0.15f);
                    fg = Color.white;
                    break;
                default:
                    icon = "📋";
                    bg = new Color(0.35f, 0.55f, 0.45f);
                    fg = Color.white;
                    break;
            }

            string label = $"{icon} {dest.ToUpper()}" + (isTemp ? " · TEMP" : "");

            GUIContent labelContent = new GUIContent(label);
            GUIStyle bannerStyle = new GUIStyle(GUI.skin.label);
            bannerStyle.fontSize = 10;
            bannerStyle.fontStyle = FontStyle.Bold;
            bannerStyle.alignment = TextAnchor.MiddleLeft;
            bannerStyle.normal.textColor = fg;
            bannerStyle.padding = new RectOffset(8, 8, 0, 0);

            float pillWidth = bannerStyle.CalcSize(labelContent).x + 16;
            Rect bannerRect = EditorGUILayout.GetControlRect(false, 16, GUILayout.Width(pillWidth));

            EditorGUI.DrawRect(bannerRect, bg);
            EditorGUI.DrawRect(new Rect(bannerRect.x, bannerRect.y, 3, bannerRect.height), new Color(1f, 1f, 1f, 0.65f));

            GUI.Label(bannerRect, labelContent, bannerStyle);

            EditorGUILayout.Space(3);
        }

        private Color GetPresetTypeColor(string presetType)
        {
            if (string.IsNullOrEmpty(presetType))
                return Color.white;

            switch (presetType.ToLower())
            {
                case "desk":
                    return new Color(0.3f, 0.7f, 1f);
                case "loadout":
                case "equipment":
                    return new Color(0.85f, 0.45f, 1f);
                case "hotbar":
                    return new Color(1f, 0.75f, 0.3f);
                default:
                    return new Color(0.5f, 0.9f, 0.7f);
            }
        }

        private Color GetRarityColor(string rarity)
        {
            if (string.IsNullOrEmpty(rarity))
                return Color.white;

            switch (rarity.ToLower())
            {
                case "common":
                    return new Color(0.7f, 0.7f, 0.7f);
                case "uncommon":
                    return new Color(0.3f, 1f, 0.3f);
                case "rare":
                    return new Color(0.3f, 0.6f, 1f);
                case "epic":
                    return new Color(0.8f, 0.3f, 1f);
                case "legendary":
                    return new Color(1f, 0.6f, 0f);
                case "mythic":
                    return new Color(1f, 0.3f, 0.3f);
                default:
                    return Color.white;
            }
        }
    }
}
