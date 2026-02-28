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

                this.showItemsFoldout[container.id] = EditorGUILayout.Foldout(
                    this.showItemsFoldout[container.id],
                    $"Items ({items.Length})",
                    true);

                if (this.showItemsFoldout[container.id])
                {
                    EditorGUI.indentLevel++;
                    if (items.Length == 0)
                    {
                        EditorGUILayout.LabelField("No items in this container.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        foreach (InventoryItemData item in items)
                        {
                            this.DrawItemSummary(item);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawItemSummary(InventoryItemData item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"ID: {item.id}", EditorStyles.miniLabel);

            if (item.definition != null)
            {
                EditorGUILayout.LabelField($"Name: {item.definition.name}  |  Category: {item.definition.category}  |  Rarity: {item.definition.rarity}");
                EditorGUILayout.LabelField($"Grid Size: {item.definition.grid_width}×{item.definition.grid_height}  |  Stackable: {item.definition.is_stackable}");
            }

            EditorGUILayout.LabelField($"Qty: {item.quantity}  |  Level: {item.level}  |  Pos: ({item.grid_x}, {item.grid_y})");

            EditorGUILayout.EndVertical();
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
