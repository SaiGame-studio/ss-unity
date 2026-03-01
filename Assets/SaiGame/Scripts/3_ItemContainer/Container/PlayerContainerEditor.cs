using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(PlayerContainer))]
    [CanEditMultipleObjects]
    public class PlayerContainerEditor : Editor
    {
        private PlayerContainer playerContainer;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty containerLimit;
        private SerializedProperty containerOffset;

        private bool showCurrentContainers = true;
        private bool showContainerList = true;
        private bool showUtilityButtons = true;

        // Per-container items state
        private readonly Dictionary<string, InventoryItemData[]> containerItems = new Dictionary<string, InventoryItemData[]>();
        private readonly Dictionary<string, bool> showItemsFoldout = new Dictionary<string, bool>();
        private readonly HashSet<string> loadingContainerItems = new HashSet<string>();
        // Per-item gacha loading state (keyed by item.id)
        private readonly HashSet<string> loadingGachaItems = new HashSet<string>();

        // Per-container filter state
        private readonly Dictionary<string, ItemFilterOptions> containerFilters = new Dictionary<string, ItemFilterOptions>();
        private readonly Dictionary<string, bool> showFilterPanel = new Dictionary<string, bool>();
        // Cached filtered results so we only recalculate when the filter changes
        private readonly Dictionary<string, InventoryItemData[]> filteredItems = new Dictionary<string, InventoryItemData[]>();

        private void OnEnable()
        {
            this.playerContainer = (PlayerContainer)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.containerLimit = serializedObject.FindProperty("containerLimit");
            this.containerOffset = serializedObject.FindProperty("containerOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Container Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load containers when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.containerLimit, new GUIContent("Container Limit", "Number of containers to load per request"));
            EditorGUILayout.PropertyField(this.containerOffset, new GUIContent("Container Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current Container Data
            this.showCurrentContainers = EditorGUILayout.Foldout(this.showCurrentContainers, "Current Container Data", true);
            if (this.showCurrentContainers)
            {
                EditorGUI.indentLevel++;

                if (this.playerContainer.CurrentContainers != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Loaded Containers: {this.playerContainer.CurrentContainers.containers?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Has More: {this.playerContainer.CurrentContainers.has_more}");
                    EditorGUILayout.LabelField($"Limit: {this.playerContainer.CurrentContainers.limit}  |  Offset: {this.playerContainer.CurrentContainers.offset}");

                    if (this.playerContainer.CurrentContainers.containers != null
                        && this.playerContainer.CurrentContainers.containers.Length > 0)
                    {
                        this.showContainerList = EditorGUILayout.Foldout(this.showContainerList, "Container List", true);
                        if (this.showContainerList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (ContainerData container in this.playerContainer.CurrentContainers.containers)
                            {
                                this.DrawContainerSummary(container);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No container data loaded yet.", MessageType.None);
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
                if (GUILayout.Button("Get Containers", GUILayout.Height(30)))
                {
                    this.LoadContainers();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Containers", GUILayout.Height(30)))
                {
                    this.playerContainer.ClearContainers();
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

        private void DrawContainerSummary(ContainerData container)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"ID: {container.id}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {container.container_type}");

            if (container.definition != null)
            {
                EditorGUILayout.LabelField($"Name: {container.definition.name}");
                EditorGUILayout.LabelField($"Grid: {container.definition.grid_cols}×{container.definition.grid_rows}  |  Portable: {container.definition.is_portable}");
            }

            EditorGUILayout.LabelField($"Created: {container.created_at}");

            // Items button
            EditorGUILayout.Space(4);
            bool isLoading = this.loadingContainerItems.Contains(container.id);
            GUI.backgroundColor = isLoading ? Color.gray : new Color(0.4f, 1f, 0.6f);
            EditorGUI.BeginDisabledGroup(isLoading);
            if (GUILayout.Button(isLoading ? "Loading..." : "Items", GUILayout.Height(22)))
            {
                this.LoadContainerItems(container.id);
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            // Loaded items foldout
            if (this.containerItems.TryGetValue(container.id, out InventoryItemData[] items) && items != null)
            {
                if (!this.showItemsFoldout.ContainsKey(container.id))
                    this.showItemsFoldout[container.id] = true;

                // ── Filter panel ──────────────────────────────────────────────
                if (!this.containerFilters.ContainsKey(container.id))
                    this.containerFilters[container.id] = new ItemFilterOptions();
                if (!this.showFilterPanel.ContainsKey(container.id))
                    this.showFilterPanel[container.id] = false;

                ItemFilterOptions filter = this.containerFilters[container.id];

                EditorGUILayout.BeginHorizontal();
                this.showFilterPanel[container.id] = EditorGUILayout.Foldout(
                    this.showFilterPanel[container.id], "Filter Items", true);
                if (!filter.IsEmpty)
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.1f);
                    if (GUILayout.Button("✕ Clear", GUILayout.Width(60), GUILayout.Height(16)))
                    {
                        filter.Clear();
                        this.filteredItems.Remove(container.id);
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                if (this.showFilterPanel[container.id])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Name search
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField(
                        new GUIContent("Name", "Substring search on item name (case-insensitive)"),
                        filter.nameSearch);
                    if (EditorGUI.EndChangeCheck())
                    {
                        filter.nameSearch = newName;
                        this.filteredItems.Remove(container.id);
                    }

                    // Category
                    EditorGUI.BeginChangeCheck();
                    string newCat = EditorGUILayout.TextField(
                        new GUIContent("Category", "Exact category match, e.g. weapon / gacha_pack (empty = any)"),
                        filter.category);
                    if (EditorGUI.EndChangeCheck())
                    {
                        filter.category = newCat;
                        this.filteredItems.Remove(container.id);
                    }

                    // Rarity
                    EditorGUI.BeginChangeCheck();
                    string newRarity = EditorGUILayout.TextField(
                        new GUIContent("Rarity", "Exact rarity match, e.g. common / rare (empty = any)"),
                        filter.rarity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        filter.rarity = newRarity;
                        this.filteredItems.Remove(container.id);
                    }

                    // Stackable only
                    EditorGUI.BeginChangeCheck();
                    bool newStackable = EditorGUILayout.Toggle(
                        new GUIContent("Stackable Only", "Show only items where is_stackable is true"),
                        filter.stackableOnly);
                    if (EditorGUI.EndChangeCheck())
                    {
                        filter.stackableOnly = newStackable;
                        this.filteredItems.Remove(container.id);
                    }

                    // Apply button
                    EditorGUILayout.Space(2);
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
                    if (GUILayout.Button("Apply Filter", GUILayout.Height(22)))
                    {
                        this.filteredItems[container.id] = this.playerContainer.FilterItems(items, filter);
                        this.showItemsFoldout[container.id] = true;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }

                // Determine display list
                InventoryItemData[] displayItems = this.filteredItems.TryGetValue(container.id, out InventoryItemData[] cached)
                    ? cached
                    : items;

                string foldoutLabel = filter.IsEmpty
                    ? $"Items ({displayItems.Length})"
                    : $"Items ({displayItems.Length} / {items.Length} filtered)";

                this.showItemsFoldout[container.id] = EditorGUILayout.Foldout(
                    this.showItemsFoldout[container.id], foldoutLabel, true);

                if (this.showItemsFoldout[container.id])
                {
                    EditorGUI.indentLevel++;
                    if (displayItems.Length == 0)
                    {
                        EditorGUILayout.LabelField("No items match the current filter.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        foreach (InventoryItemData item in displayItems)
                        {
                            this.DrawItemSummary(item, container.id);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawItemSummary(InventoryItemData item, string containerId)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"ID: {item.id}", EditorStyles.miniLabel);

            if (item.definition != null)
            {
                EditorGUILayout.LabelField($"Name: {item.definition.name}  |  Category: {item.definition.category}  |  Rarity: {item.definition.rarity}");
                EditorGUILayout.LabelField($"Grid Size: {item.definition.grid_width}×{item.definition.grid_height}  |  Stackable: {item.definition.is_stackable}");
            }

            EditorGUILayout.LabelField($"Qty: {item.quantity}  |  Level: {item.level}  |  Pos: ({item.grid_x}, {item.grid_y})");

            // Show Gacha button only when metadata contains a gacha_pack_id
            string gachaPackId = this.GetGachaPackIdFromMetadata(item.definition);
            if (!string.IsNullOrEmpty(gachaPackId))
            {
                EditorGUILayout.Space(2);
                bool isGachaLoading = this.loadingGachaItems.Contains(item.id);
                GUI.backgroundColor = isGachaLoading ? Color.gray : new Color(1f, 0.85f, 0.1f);
                EditorGUI.BeginDisabledGroup(isGachaLoading);
                if (GUILayout.Button(isGachaLoading ? "Opening..." : "Gacha", GUILayout.Height(22)))
                {
                    this.OpenGachaPack(item.id, gachaPackId, containerId);
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void OpenGachaPack(string itemId, string gachaPackId, string containerId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerContainerEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerContainerEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingGachaItems.Add(itemId);
            Repaint();

            this.playerContainer.OpenGachaPack(
                gachaPackDefId: gachaPackId,
                containerId: containerId,
                onSuccess: response =>
                {
                    this.loadingGachaItems.Remove(itemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                    {
                        int count = response.items_granted?.Length ?? 0;
                        Debug.Log($"[PlayerContainerEditor] Gacha opened! {count} item(s) granted. Transaction: {response.transaction_id}");
                    }

                    Repaint();
                },
                onError: error =>
                {
                    this.loadingGachaItems.Remove(itemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[PlayerContainerEditor] Failed to open gacha pack: {error}");

                    Repaint();
                }
            );
        }

        /// <summary>
        /// Returns gacha_pack_id from the definition's metadata if present, otherwise null.
        /// </summary>
        private string GetGachaPackIdFromMetadata(ItemDefinitionData definition)
        {
            if (definition == null || definition.metadata == null)
                return null;

            return string.IsNullOrEmpty(definition.metadata.gacha_pack_id)
                ? null
                : definition.metadata.gacha_pack_id;
        }

        private void LoadContainerItems(string containerId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerContainerEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerContainerEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingContainerItems.Add(containerId);
            Repaint();

            this.playerContainer.GetContainerItems(
                containerId: containerId,
                limit: 50,
                offset: 0,
                onSuccess: response =>
                {
                    this.containerItems[containerId] = response.items ?? new InventoryItemData[0];
                    this.showItemsFoldout[containerId] = true;
                    this.loadingContainerItems.Remove(containerId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[PlayerContainerEditor] Loaded {this.containerItems[containerId].Length} items for container {containerId}");

                    Repaint();
                },
                onError: error =>
                {
                    this.loadingContainerItems.Remove(containerId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[PlayerContainerEditor] Failed to load items for container {containerId}: {error}");

                    Repaint();
                }
            );
        }

        private void LoadContainers()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerContainerEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerContainerEditor] Not authenticated! Please login first.");
                return;
            }

            this.playerContainer.GetContainers(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[PlayerContainerEditor] Loaded {response.containers.Length} containers");
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[PlayerContainerEditor] Failed to load containers: {error}");
                }
            );
        }
    }
}
