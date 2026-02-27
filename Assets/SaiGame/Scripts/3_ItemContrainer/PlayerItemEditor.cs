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
        private bool showItemList = true;
        private bool showUtilityButtons = true;

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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"ID: {item.id}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Qty: {item.quantity}  |  Level: {item.level}  |  Grid: ({item.grid_x}, {item.grid_y})");

            if (item.definition != null)
            {
                EditorGUILayout.LabelField($"Name: {item.definition.name}");
                EditorGUILayout.LabelField($"Category: {item.definition.category}  |  Rarity: {item.definition.rarity}");
                EditorGUILayout.LabelField($"Stackable: {item.definition.is_stackable}  |  Grid: {item.definition.grid_width}x{item.definition.grid_height}");
            }

            EditorGUILayout.LabelField($"Acquired: {item.acquired_at}");

            EditorGUILayout.EndVertical();
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
