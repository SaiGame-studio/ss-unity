using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(PlayerItem))]
    [CanEditMultipleObjects]
    public class PlayerItemEditor : Editor
    {
        private PlayerItem itemContainer;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty autoLoadCategory;
        private SerializedProperty itemLimit;
        private SerializedProperty itemOffset;
        private SerializedProperty categoryFilter;

        private bool showCurrentInventory = true;
        private bool showItemList = false;
        private bool showUtilityButtons = true;

        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();

        // Per-item state for the in-place Update Properties form
        private readonly Dictionary<string, string> itemPropertiesJson = new Dictionary<string, string>();
        private readonly HashSet<string> itemsUpdating = new HashSet<string>();

        // Cached reference to ItemCrafting for the Craft button on recipe items
        private ItemCrafting craftingRef;

        // Cached reference to PlayerContainer for the Gacha button on gacha_pack items
        private PlayerContainer containerRef;

        // Category dropdown state (static so it persists across re-inspects)
        private static string[] cachedCategories = null;
        private static string[] dropdownOptions = new string[] { "(All)" };
        private static bool isFetchingCategories = false;
        private int selectedCategoryIndex = 0;

        private void OnEnable()
        {
            this.itemContainer = (PlayerItem)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.autoLoadCategory = serializedObject.FindProperty("autoLoadCategory");
            this.itemLimit = serializedObject.FindProperty("itemLimit");
            this.itemOffset = serializedObject.FindProperty("itemOffset");
            this.categoryFilter = serializedObject.FindProperty("categoryFilter");

            // Restore dropdown from PlayerPrefs cache (no network call needed)
            if (cachedCategories == null)
            {
                string[] prefsCache = PlayerItem.LoadCategoriesFromPrefs();
                if (prefsCache != null)
                {
                    cachedCategories = prefsCache;
                    this.RebuildDropdownOptions();
                }
            }

            this.SyncDropdownIndexFromProperty();
        }

        private void SyncDropdownIndexFromProperty()
        {
            string current = this.categoryFilter?.stringValue ?? "";
            this.selectedCategoryIndex = 0;
            if (!string.IsNullOrEmpty(current))
            {
                for (int i = 0; i < dropdownOptions.Length; i++)
                {
                    if (dropdownOptions[i] == current)
                    {
                        this.selectedCategoryIndex = i;
                        break;
                    }
                }
            }
        }

        private void RebuildDropdownOptions()
        {
            if (cachedCategories == null || cachedCategories.Length == 0)
            {
                dropdownOptions = new string[] { "(All)" };
            }
            else
            {
                dropdownOptions = new string[cachedCategories.Length + 1];
                dropdownOptions[0] = "(All)";
                for (int i = 0; i < cachedCategories.Length; i++)
                    dropdownOptions[i + 1] = cachedCategories[i];
            }
            this.SyncDropdownIndexFromProperty();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Container Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load inventory when user logs in"));
            if (this.autoLoadOnLogin.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(this.autoLoadCategory, new GUIContent("Auto Load Category", "Category filter applied on auto-load (leave empty for all)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.itemLimit, new GUIContent("Item Limit", "Number of items to load per request"));
            EditorGUILayout.PropertyField(this.itemOffset, new GUIContent("Item Offset", "Offset for pagination"));
            EditorGUILayout.PropertyField(this.categoryFilter, new GUIContent("Category Filter", "Active category filter (synced with dropdown below)"));

            EditorGUILayout.Space();

            // Current Inventory Data
            this.showCurrentInventory = EditorGUILayout.Foldout(this.showCurrentInventory, "Current Inventory Data", true);
            if (this.showCurrentInventory)
            {
                EditorGUI.indentLevel++;

                if (this.itemContainer.CurrentInventory != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Items: {this.itemContainer.CurrentInventory.total}");
                    EditorGUILayout.LabelField($"Loaded Items: {this.itemContainer.CurrentInventory.items?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Limit: {this.itemContainer.CurrentInventory.limit}  |  Offset: {this.itemContainer.CurrentInventory.offset}");

                    if (this.itemContainer.CurrentInventory.items != null
                        && this.itemContainer.CurrentInventory.items.Length > 0)
                    {
                        this.showItemList = EditorGUILayout.Foldout(this.showItemList, "Item List", true);
                        if (this.showItemList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (InventoryItemData item in this.itemContainer.CurrentInventory.items)
                            {
                                this.DrawItemSummary(item);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No inventory data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                // Category Dropdown Row
                EditorGUILayout.LabelField("Category Filter", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(this.selectedCategoryIndex, dropdownOptions, GUILayout.Height(24));
                if (EditorGUI.EndChangeCheck())
                {
                    this.selectedCategoryIndex = newIndex;
                    string selected = (newIndex == 0) ? "" : dropdownOptions[newIndex];
                    this.categoryFilter.stringValue = selected;
                    serializedObject.ApplyModifiedProperties();
                }

                GUI.backgroundColor = isFetchingCategories ? Color.gray : new Color(0.4f, 0.85f, 1f);
                EditorGUI.BeginDisabledGroup(isFetchingCategories);
                if (GUILayout.Button(isFetchingCategories ? "Fetching..." : "Reload", GUILayout.Height(24), GUILayout.Width(80)))
                {
                    this.FetchCategories();
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                if (cachedCategories == null)
                {
                    EditorGUILayout.HelpBox("Press Reload to load categories from the server.", MessageType.Info);
                }

                EditorGUILayout.Space(6);

                // Row 1: Get Items / Clear
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Items", GUILayout.Height(30)))
                {
                    this.LoadItems(this.categoryFilter.stringValue);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Inventory", GUILayout.Height(30)))
                {
                    this.itemContainer.ClearInventory();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawItemSummary(InventoryItemData item)
        {
            if (!this.itemFoldouts.ContainsKey(item.id))
                this.itemFoldouts[item.id] = false;

            bool isClientWritable = item.definition != null && item.definition.client_writable;
            string rarityLabel = item.definition != null && !string.IsNullOrEmpty(item.definition.rarity)
                ? $"  ★{item.definition.rarity}" : "";
            string levelLabel = item.level > 0 ? $"  Lv.{item.level}" : "";
            string label = item.definition != null
                ? $"{item.definition.name}  [{item.definition.category}]{rarityLabel}{levelLabel}  ×{item.quantity}{(isClientWritable ? "  ✎" : "")}"
                : $"{item.id}  ×{item.quantity}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header row: rarity-coloured foldout + optional Recipe badge on far right
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontStyle = FontStyle.Bold;
            Color rarityColor = GetRarityColor(item.definition?.rarity);
            foldoutStyle.normal.textColor    = rarityColor;
            foldoutStyle.onNormal.textColor  = rarityColor;
            foldoutStyle.focused.textColor   = rarityColor;
            foldoutStyle.onFocused.textColor = rarityColor;
            foldoutStyle.active.textColor    = rarityColor;
            foldoutStyle.onActive.textColor  = rarityColor;

            bool isRecipeItem    = string.Equals(item.definition?.category, "recipe",     System.StringComparison.OrdinalIgnoreCase);
            bool isGachaPackItem = string.Equals(item.definition?.category, "gacha_pack", System.StringComparison.OrdinalIgnoreCase);

            EditorGUILayout.BeginHorizontal();
            this.itemFoldouts[item.id] = EditorGUILayout.Foldout(this.itemFoldouts[item.id], label, true, foldoutStyle);
            if (isRecipeItem)
            {
                GUIStyle recipeStyle = new GUIStyle(EditorStyles.miniLabel);
                recipeStyle.fontStyle = FontStyle.Bold;
                recipeStyle.normal.textColor = new Color(0.9f, 0.65f, 0.1f);
                GUILayout.Label("Recipe", recipeStyle, GUILayout.ExpandWidth(false));
            }
            if (isGachaPackItem)
            {
                GUIStyle gachaStyle = new GUIStyle(EditorStyles.miniLabel);
                gachaStyle.fontStyle = FontStyle.Bold;
                gachaStyle.normal.textColor = new Color(0.4f, 0.8f, 1.0f);
                GUILayout.Label("Gacha Pack", gachaStyle, GUILayout.ExpandWidth(false));
            }
            EditorGUILayout.EndHorizontal();

            if (this.itemFoldouts[item.id])
            {
                EditorGUI.indentLevel++;

                // ── Key Stats (highlighted summary) ───────────────────────────
                EditorGUILayout.Space(4);
                this.DrawKeyStatsCard(item);
                EditorGUILayout.Space(4);

                // ── Item fields ──────────────────────────────────────────────
                EditorGUILayout.LabelField("Item", EditorStyles.boldLabel);
                DrawIdField("ID",             item.id);
                DrawIdField("Game ID",        item.game_id);
                DrawIdField("User ID",        item.user_id);
                DrawIdField("Definition ID",  item.item_definition_id);
                DrawIdField("Container ID",   item.item_container_id);
                EditorGUILayout.LabelField("Quantity",            item.quantity.ToString());
                EditorGUILayout.LabelField("Level",               item.level.ToString());
                EditorGUILayout.LabelField("Grid",                $"({item.grid_x}, {item.grid_y})");
                EditorGUILayout.LabelField("Version",             item.version.ToString());
                EditorGUILayout.LabelField("Acquired At",         item.acquired_at);
                EditorGUILayout.LabelField("Last Modified At",    item.last_modified_at);

                // ── Properties ───────────────────────────────────────────────
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

                string prettyPublic  = PrettyJson(item.public_properties);
                string prettyPrivate = PrettyJson(item.private_properties);

                EditorGUILayout.LabelField("public_properties");
                EditorGUI.indentLevel++;
                EditorGUILayout.SelectableLabel(prettyPublic,
                    EditorStyles.textArea,
                    GUILayout.MinHeight(EditorStyles.textArea.lineHeight * (CountLines(prettyPublic) + 1)));
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("private_properties");
                EditorGUI.indentLevel++;
                EditorGUILayout.SelectableLabel(prettyPrivate,
                    EditorStyles.textArea,
                    GUILayout.MinHeight(EditorStyles.textArea.lineHeight * (CountLines(prettyPrivate) + 1)));
                EditorGUI.indentLevel--;

                // ── Definition ───────────────────────────────────────────────
                if (item.definition != null)
                {
                    var d = item.definition;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Definition", EditorStyles.boldLabel);
                    DrawIdField("Def ID",              d.id);
                    EditorGUILayout.LabelField("Item Code",           d.item_code);
                    EditorGUILayout.LabelField("Name",                d.name);
                    EditorGUILayout.LabelField("Category",            d.category);
                    EditorGUILayout.LabelField("Rarity",              d.rarity);
                    EditorGUILayout.LabelField("Stackable",           $"{d.is_stackable}  (max {d.max_stack_size})");
                    EditorGUILayout.LabelField("Grid Size",           $"{d.grid_width} × {d.grid_height}");
                    EditorGUILayout.LabelField("Client Writable",     d.client_writable.ToString());
                    EditorGUILayout.LabelField("Allow Client Qty",    d.allow_client_update_qty.ToString());
                    EditorGUILayout.LabelField("Created By",          d.created_by);
                    EditorGUILayout.LabelField("Created At",          d.created_at);
                    EditorGUILayout.LabelField("Updated At",          d.updated_at);

                    if (!string.IsNullOrEmpty(d.base_stats))
                    {
                        EditorGUILayout.LabelField("base_stats");
                        EditorGUI.indentLevel++;
                        string prettyStats = PrettyJson(d.base_stats);
                        EditorGUILayout.SelectableLabel(prettyStats,
                            EditorStyles.textArea,
                            GUILayout.MinHeight(EditorStyles.textArea.lineHeight * (CountLines(prettyStats) + 1)));
                        EditorGUI.indentLevel--;
                    }

                    // ── Metadata ─────────────────────────────────────────────
                    if (d.metadata != null)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);

                        if (!string.IsNullOrEmpty(d.metadata.flavor_text))
                            EditorGUILayout.LabelField("Flavor Text", d.metadata.flavor_text);

                        if (!string.IsNullOrEmpty(d.metadata.icon))
                            EditorGUILayout.LabelField("Icon", d.metadata.icon);

                        if (!string.IsNullOrEmpty(d.metadata.gacha_pack_id))
                            DrawIdField("Gacha Pack ID", d.metadata.gacha_pack_id);

                        if (d.metadata.gacha_pack_ids != null && d.metadata.gacha_pack_ids.Length > 0)
                        {
                            EditorGUILayout.LabelField($"Gacha Pack IDs ({d.metadata.gacha_pack_ids.Length})");
                            EditorGUI.indentLevel++;
                            foreach (string packId in d.metadata.gacha_pack_ids)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(packId);
                                if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(18)))
                                    GUIUtility.systemCopyBuffer = packId;
                                GUI.backgroundColor = new Color(0.4f, 0.8f, 1.0f);
                                if (GUILayout.Button("Gacha 🎰", GUILayout.Width(80), GUILayout.Height(18)))
                                    this.DoOpenGacha(packId, item.item_container_id);
                                GUI.backgroundColor = Color.white;
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUI.indentLevel--;
                        }

                        if (d.metadata.craft_recipe_input_ids != null && d.metadata.craft_recipe_input_ids.Length > 0)
                        {
                            EditorGUILayout.LabelField($"Craft Recipe Input IDs ({d.metadata.craft_recipe_input_ids.Length})");
                            EditorGUI.indentLevel++;
                            foreach (string recipeInputId in d.metadata.craft_recipe_input_ids)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(recipeInputId);
                                if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(18)))
                                    GUIUtility.systemCopyBuffer = recipeInputId;
                                GUI.backgroundColor = new Color(0.3f, 1f, 0.5f);
                                if (GUILayout.Button("Craft ⚒", GUILayout.Width(70), GUILayout.Height(18)))
                                    this.DoCraft(recipeInputId);
                                GUI.backgroundColor = Color.white;
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }

                // ── Inline update form (only for client_writable items) ──────
                if (isClientWritable)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Update public_properties", EditorStyles.boldLabel);

                    if (!this.itemPropertiesJson.ContainsKey(item.id))
                        this.itemPropertiesJson[item.id] = "{\n  \"key\": \"value\"\n}";

                    this.itemPropertiesJson[item.id] = EditorGUILayout.TextArea(
                        this.itemPropertiesJson[item.id], GUILayout.MinHeight(56));

                    bool isUpdating = this.itemsUpdating.Contains(item.id);

                    EditorGUILayout.BeginHorizontal();

                    // Beautify button
                    GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                    if (GUILayout.Button("Beautify ✦", GUILayout.Height(26), GUILayout.Width(90)))
                    {
                        this.itemPropertiesJson[item.id] = PrettyJson(this.itemPropertiesJson[item.id]);
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;

                    // Update button
                    GUI.backgroundColor = isUpdating ? Color.gray : new Color(0.4f, 1f, 0.6f);
                    EditorGUI.BeginDisabledGroup(isUpdating);
                    if (GUILayout.Button(isUpdating ? "Updating..." : "Update Properties", GUILayout.Height(26)))
                    {
                        string itemId = item.id;
                        string json = this.itemPropertiesJson[itemId];
                        this.itemsUpdating.Add(itemId);
                        Repaint();

                        this.itemContainer.UpdateItemProperties(
                            itemId: itemId,
                            propertiesJson: json,
                            onSuccess: updated =>
                            {
                                this.itemsUpdating.Remove(itemId);
                                Repaint();
                                string id = updated != null ? updated.id : itemId;
                                string props = updated != null ? updated.public_properties : json;
                                Debug.Log($"[ItemContainerEditor] Updated public_properties for item {id}: {props}");
                            },
                            onError: err =>
                            {
                                this.itemsUpdating.Remove(itemId);
                                Repaint();
                                Debug.LogError($"[ItemContainerEditor] UpdateItemProperties failed: {err}");
                            }
                        );
                    }
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawKeyStatsCard(InventoryItemData item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 11;
            headerStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.LabelField("✦ KEY STATS", headerStyle);

            GUIStyle labelCol = new GUIStyle(EditorStyles.label);
            labelCol.fontSize = 11;
            labelCol.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            GUIStyle valueCol = new GUIStyle(EditorStyles.boldLabel);
            valueCol.fontSize = 11;

            if (item.definition != null && !string.IsNullOrEmpty(item.definition.name))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = Color.white;
                EditorGUILayout.LabelField(item.definition.name, valueCol);
                EditorGUILayout.EndHorizontal();
            }

            if (item.definition != null && !string.IsNullOrEmpty(item.definition.category))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = new Color(1f, 0.84f, 0f);
                EditorGUILayout.LabelField(item.definition.category, valueCol);
                EditorGUILayout.EndHorizontal();
            }

            if (item.definition != null && !string.IsNullOrEmpty(item.definition.rarity))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rarity:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = GetRarityColor(item.definition.rarity);
                EditorGUILayout.LabelField($"★ {item.definition.rarity}", valueCol);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quantity:", labelCol, GUILayout.Width(90));
            valueCol.normal.textColor = new Color(0.4f, 1f, 0.9f);
            EditorGUILayout.LabelField($"×{item.quantity}", valueCol);
            EditorGUILayout.EndHorizontal();

            if (item.level > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Level:", labelCol, GUILayout.Width(90));
                valueCol.normal.textColor = new Color(1f, 0.65f, 0.2f);
                EditorGUILayout.LabelField($"Lv. {item.level}", valueCol);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static Color GetRarityColor(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return Color.white;
            switch (rarity.ToLower())
            {
                case "legendary": return new Color(1f,    0.84f, 0f);
                case "epic":      return new Color(0.75f, 0.3f,  1f);
                case "rare":      return new Color(0.4f,  0.7f,  1f);
                case "uncommon":  return new Color(0.3f,  1f,    0.5f);
                case "common":    return new Color(0.7f,  0.7f,  0.7f);
                default:          return Color.white;
            }
        }

        private void DoOpenGacha(string gachaPackId, string containerId)
        {
            if (string.IsNullOrEmpty(gachaPackId))
            {
                Debug.LogError("[PlayerItemEditor] Gacha Pack ID is empty, cannot open.");
                return;
            }

            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerItemEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerItemEditor] Not authenticated!");
                return;
            }

            if (this.containerRef == null)
                this.containerRef = UnityEngine.Object.FindAnyObjectByType<PlayerContainer>();

            if (this.containerRef == null)
            {
                Debug.LogError("[PlayerItemEditor] No PlayerContainer component found in scene!");
                return;
            }

            this.containerRef.OpenGachaPack(
                gachaPackId,
                containerId,
                onSuccess: response =>
                {
                    Debug.Log($"[PlayerItemEditor] Gacha OK. Items granted: {response.items_granted?.Length ?? 0}");
                    this.Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[PlayerItemEditor] Gacha failed: {error}");
                }
            );
        }

        private void DoCraft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                Debug.LogError("[PlayerItemEditor] Recipe ID is empty, cannot craft.");
                return;
            }

            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerItemEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerItemEditor] Not authenticated!");
                return;
            }

            if (this.craftingRef == null)
                this.craftingRef = UnityEngine.Object.FindAnyObjectByType<ItemCrafting>();

            if (this.craftingRef == null)
            {
                Debug.LogError("[PlayerItemEditor] No ItemCrafting component found in scene!");
                return;
            }

            this.craftingRef.Craft(
                recipeId,
                onSuccess: response =>
                {
                    Debug.Log($"[PlayerItemEditor] Craft OK. Tx: {response.transaction_id}");
                    this.Repaint();
                },
                onError: error =>
                {
                    Debug.LogError($"[PlayerItemEditor] Craft failed: {error}");
                }
            );
        }

        /// <summary>Simple JSON pretty-printer: adds newlines and indentation.</summary>
        private static string PrettyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "{}";

            var sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char c in json)
            {
                if (c == '"' && (sb.Length == 0 || sb[sb.Length - 1] != '\\'))
                    inString = !inString;

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent--;
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                        break; // strip original whitespace
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static void DrawIdField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, value);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = value ?? "";
            EditorGUILayout.EndHorizontal();
        }

        private static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            foreach (char c in s)
                if (c == '\n') n++;
            return n;
        }

        private void FetchCategories()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemContainerEditor] SaiService not found!");
                return;
            }

            isFetchingCategories = true;

            // forceRefresh = true — bypass cache and fetch fresh data from server
            this.itemContainer.GetItemCategories(
                forceRefresh: true,
                onSuccess: categories =>
                {
                    cachedCategories = categories;
                    isFetchingCategories = false;
                    this.RebuildDropdownOptions();
                    Repaint();
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemContainerEditor] Reloaded {categories.Length} categories from server");
                },
                onError: error =>
                {
                    isFetchingCategories = false;
                    Repaint();
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ItemContainerEditor] Failed to load categories: {error}");
                }
            );
        }

        private void LoadItems(string category)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemContainerEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemContainerEditor] Not authenticated! Please login first.");
                return;
            }

            this.itemContainer.GetItems(
                category: category,
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemContainerEditor] Loaded {response.items.Length} items (total: {response.total})");
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ItemContainerEditor] Failed to load items: {error}");
                }
            );
        }
    }
}
