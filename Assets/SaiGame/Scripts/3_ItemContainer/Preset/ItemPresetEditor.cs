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
        private SerializedProperty definitionId;

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
            this.definitionId = serializedObject.FindProperty("definitionId");
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
            EditorGUILayout.HelpBox("Get Definition ID from admin /games/id/items (tab preset)", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            
            this.definitionId.stringValue = EditorGUILayout.TextField(
                new GUIContent("Definition ID", "Get Definition ID from admin /games/id/items (tab preset)"),
                this.definitionId.stringValue);
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                this.itemPreset.CreatePreset(
                    this.definitionId.stringValue,
                    onSuccess: preset =>
                    {
                        if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                            Debug.Log($"[Editor] Preset created! ID: {preset.id} | Type: {preset.preset_type} | Max Slots: {preset.max_slots}");
                        Repaint();
                    },
                    onError: error =>
                    {
                        if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                            Debug.LogError($"[Editor] Create preset failed: {error}");
                    }
                );
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

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Presets", GUILayout.Height(30)))
                {
                    this.itemPreset.GetPresets(
                        onSuccess: presets =>
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.Log($"[Editor] Presets loaded: {presets.containers?.Length ?? 0} presets");
                            Repaint();
                        },
                        onError: error =>
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Get presets failed: {error}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Presets", GUILayout.Height(30)))
                {
                    this.itemPreset.ClearPresets();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Create Preset", GUILayout.Height(30)))
                {
                    this.itemPreset.CreatePreset(
                        this.definitionId.stringValue,
                        onSuccess: preset =>
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.Log($"[Editor] Preset created! ID: {preset.id} | Type: {preset.preset_type} | Max Slots: {preset.max_slots}");
                            Repaint();
                        },
                        onError: error =>
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Create preset failed: {error}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Sync Player Inventory", GUILayout.Height(30)))
                {
                    PlayerItem pItem = FindObjectOfType<PlayerItem>();
                    if (pItem != null)
                    {
                        pItem.GetItems(null, null, null,
                            onSuccess: res => {
                                if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                    Debug.Log($"[Editor] Loaded {res.items.Length} inventory items");
                                Repaint();
                            },
                            onError: err => {
                                if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
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

        private void DrawPresetSummary(PresetData preset)
        {
            string presetId = string.IsNullOrEmpty(preset.id) ? "unknown" : preset.id;
            if (!this.presetFoldouts.TryGetValue(presetId, out bool isExpanded))
            {
                isExpanded = false;
                this.presetFoldouts[presetId] = isExpanded;
            }

            string displayName = preset.definition != null && !string.IsNullOrEmpty(preset.definition.name) 
                ? preset.definition.name 
                : $"Preset: {preset.id}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            this.presetFoldouts[presetId] = EditorGUILayout.Foldout(isExpanded, displayName, true, EditorStyles.foldoutHeader);
            
            if (this.presetFoldouts[presetId])
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {preset.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = preset.id;
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get", GUILayout.Width(60)))
                {
                    this.itemPreset.GetPreset(preset.id, 
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
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.Log($"[Editor] Refreshed preset {preset.id} from server");
                            Repaint();
                        },
                        onError: err => 
                        {
                            if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Failed to refresh preset: {err}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Definition ID: {preset.definition_id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = preset.definition_id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Type: {preset.preset_type}  |  Max Slots: {preset.max_slots}  |  Is Temp: {preset.is_temp}");
                EditorGUILayout.LabelField($"Created: {preset.created_at}");

                if (preset.definition != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Definition Variables", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Name: {preset.definition.name}");
                    EditorGUILayout.LabelField($"Type: {preset.definition.preset_type}");
                    EditorGUILayout.LabelField($"Max Slots: {preset.definition.max_slots}");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Def ID: {preset.definition.id}");
                    if (GUILayout.Button("Copy", GUILayout.Width(50)))
                        GUIUtility.systemCopyBuffer = preset.definition.id;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("Warning: No Definition payload received", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Preset Slots", EditorStyles.boldLabel);
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
                        
                        string slotDisplayName = $"[Slot {slot.slot_index}] {itemName}";

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        this.presetSlotFoldouts[slotKey] = EditorGUILayout.Foldout(this.presetSlotFoldouts[slotKey], slotDisplayName, true, EditorStyles.foldoutHeader);

                        if (this.presetSlotFoldouts[slotKey])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"Inventory ID: {slot.inventory_item_id}");
                            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                GUIUtility.systemCopyBuffer = slot.inventory_item_id;
                            EditorGUILayout.EndHorizontal();
                            
                            if (itemInfo != null)
                            {
                                EditorGUILayout.LabelField($"Quantity: {itemInfo.quantity}");
                                if (itemInfo.definition != null)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField($"Def ID: {itemInfo.definition.id}");
                                    if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                        GUIUtility.systemCopyBuffer = itemInfo.definition.id;
                                    EditorGUILayout.EndHorizontal();
                                    EditorGUILayout.LabelField($"Category: {itemInfo.definition.category}");
                                    EditorGUILayout.LabelField($"Rarity: {itemInfo.definition.rarity}");
                                    EditorGUILayout.LabelField($"Item Code: {itemInfo.definition.item_code}");
                                }
                            }
                            else
                            {
                                EditorGUILayout.LabelField("Full item data not found in PlayerItem inventory.", EditorStyles.miniLabel);
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No items in this preset.", MessageType.None);
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Add Item to Slot", EditorStyles.boldLabel);

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
                    
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        string selectedInventoryId = items[this.presetDropdownIndices[presetId]].id;
                        int selectedSlotIndex = this.presetSlotIndices[presetId];
                        this.itemPreset.AddItemToPreset(preset.id, selectedSlotIndex, selectedInventoryId, 
                            onSuccess: p => 
                            {
                                if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                    Debug.Log($"[Editor] Item {selectedInventoryId} added to preset {preset.id} at slot {selectedSlotIndex}");
                                
                                // Update the specific preset inside the current list
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
                                if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                    Debug.LogError($"[Editor] Failed to add item: {err}");
                            }
                        );
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}
