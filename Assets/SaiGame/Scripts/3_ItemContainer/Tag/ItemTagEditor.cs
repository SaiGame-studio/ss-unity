using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemTag))]
    public class ItemTagEditor : Editor
    {
        private readonly Dictionary<string, bool> tagFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();
        // Cached items per tag_key
        private readonly Dictionary<string, InventoryResponse> tagItemsCache = new Dictionary<string, InventoryResponse>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ItemTag itemTag = (ItemTag)target;

            // Auto Load Settings
            SerializedProperty autoLoadProp = serializedObject.FindProperty("autoLoadOnLogin");
            EditorGUILayout.PropertyField(autoLoadProp);

            EditorGUILayout.Space(5);

            // Query Parameters
            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            SerializedProperty tagLimitProp = serializedObject.FindProperty("tagLimit");
            SerializedProperty tagOffsetProp = serializedObject.FindProperty("tagOffset");
            EditorGUILayout.PropertyField(tagLimitProp, new GUIContent("Tag Limit"));
            EditorGUILayout.PropertyField(tagOffsetProp, new GUIContent("Tag Offset"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            // Current Tags Data — readonly display
            this.DrawCurrentTagsReadonly(itemTag);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tag Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.66f, 0.33f, 0.97f);
            if (GUILayout.Button("Get Tags", GUILayout.Height(30)))
            {
                itemTag.GetTags(
                    tags =>
                    {
                        if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                            Debug.Log($"[Editor] Tags loaded! Total: {tags.total}, Count: {tags.tags?.Length ?? 0}");
                    },
                    error =>
                    {
                        if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                            Debug.LogError($"[Editor] Get tags failed: {error}");
                    }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Tags", GUILayout.Height(30)))
            {
                itemTag.ClearTags();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCurrentTagsReadonly(ItemTag itemTag)
        {
            ItemTagsResponse data = itemTag.CurrentTags;

            EditorGUILayout.LabelField("Current Tags Data", EditorStyles.boldLabel);

            if (data == null)
            {
                EditorGUILayout.HelpBox("No tags loaded.", MessageType.None);
                return;
            }

            GUI.enabled = false;
            EditorGUILayout.IntField("Limit", data.limit);
            EditorGUILayout.IntField("Offset", data.offset);
            GUI.enabled = true;

            EditorGUILayout.Space(3);

            if (data.tags == null || data.tags.Length == 0)
            {
                EditorGUILayout.HelpBox("Tags array is empty.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField($"Tags ({data.tags.Length})", EditorStyles.boldLabel);

                foreach (ItemTagData tag in data.tags)
                {
                    string foldoutKey = tag.tag_key ?? tag.id ?? "unknown";

                    if (!this.tagFoldouts.ContainsKey(foldoutKey))
                        this.tagFoldouts[foldoutKey] = false;

                    this.tagFoldouts[foldoutKey] = EditorGUILayout.Foldout(
                        this.tagFoldouts[foldoutKey],
                        foldoutKey,
                        true
                    );

                    if (this.tagFoldouts[foldoutKey])
                    {
                        EditorGUI.indentLevel++;
                        GUI.enabled = false;
                        EditorGUILayout.TextField("Id", tag.id ?? "");
                        EditorGUILayout.TextField("Studio Id", tag.studio_id ?? "");
                        EditorGUILayout.TextField("Game Id", tag.game_id ?? "");
                        EditorGUILayout.TextField("Tag Key", tag.tag_key ?? "");
                        EditorGUILayout.TextField("Label", tag.label ?? "");
                        EditorGUILayout.TextField("Color", tag.color ?? "");
                        EditorGUILayout.TextField("Metadata", tag.metadata ?? "{}");
                        EditorGUILayout.TextField("Created By", tag.created_by ?? "");
                        EditorGUILayout.TextField("Created At", tag.created_at ?? "");
                        EditorGUILayout.TextField("Updated At", tag.updated_at ?? "");
                        EditorGUILayout.IntField("Item Count", tag.item_count);
                        GUI.enabled = true;

                        // Get Items button
                        EditorGUILayout.Space(4);
                        string capturedKey = foldoutKey;
                        GUI.backgroundColor = Color.green;
                        if (GUILayout.Button($"Get Items ({foldoutKey})", GUILayout.Height(22)))
                        {
                            itemTag.GetItemsByTag(capturedKey,
                                result =>
                                {
                                    this.tagItemsCache[capturedKey] = result;
                                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                        Debug.Log($"[Editor] Items for [{capturedKey}]: {result.total} total");
                                    Repaint();
                                },
                                error =>
                                {
                                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                                        Debug.LogError($"[Editor] Get items for [{capturedKey}] failed: {error}");
                                }
                            );
                        }
                        GUI.backgroundColor = Color.white;

                        // Show cached items for this tag
                        if (this.tagItemsCache.TryGetValue(foldoutKey, out InventoryResponse tagItems) && tagItems?.items != null)
                        {
                            EditorGUILayout.Space(3);
                            EditorGUILayout.LabelField($"Items ({tagItems.items.Length} / {tagItems.total})", EditorStyles.boldLabel);

                            foreach (InventoryItemData item in tagItems.items)
                            {
                                string itemName = item.definition?.name ?? item.item_definition_id ?? item.id;
                                string itemFoldoutKey = foldoutKey + "_" + item.id;

                                if (!this.itemFoldouts.ContainsKey(itemFoldoutKey))
                                    this.itemFoldouts[itemFoldoutKey] = false;

                                this.itemFoldouts[itemFoldoutKey] = EditorGUILayout.Foldout(
                                    this.itemFoldouts[itemFoldoutKey],
                                    itemName,
                                    true
                                );

                                if (this.itemFoldouts[itemFoldoutKey])
                                {
                                    EditorGUI.indentLevel++;
                                    GUI.enabled = false;
                                    EditorGUILayout.TextField("Id", item.id ?? "");
                                    EditorGUILayout.TextField("Item Definition Id", item.item_definition_id ?? "");
                                    EditorGUILayout.TextField("Container Id", item.item_container_id ?? "");
                                    EditorGUILayout.IntField("Grid X", item.grid_x);
                                    EditorGUILayout.IntField("Grid Y", item.grid_y);
                                    EditorGUILayout.IntField("Quantity", item.quantity);
                                    EditorGUILayout.IntField("Level", item.level);
                                    EditorGUILayout.TextField("Acquired At", item.acquired_at ?? "");
                                    EditorGUILayout.IntField("Version", item.version);
                                    if (item.definition != null)
                                    {
                                        EditorGUILayout.Space(2);
                                        EditorGUILayout.LabelField("Definition", EditorStyles.miniBoldLabel);
                                        EditorGUILayout.TextField("Name", item.definition.name ?? "");
                                        EditorGUILayout.TextField("Item Code", item.definition.item_code ?? "");
                                        EditorGUILayout.TextField("Category", item.definition.category ?? "");
                                        EditorGUILayout.TextField("Rarity", item.definition.rarity ?? "");
                                        EditorGUILayout.Toggle("Is Stackable", item.definition.is_stackable);
                                        EditorGUILayout.IntField("Max Stack Size", item.definition.max_stack_size);
                                    }
                                    GUI.enabled = true;
                                    EditorGUI.indentLevel--;
                                }
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space(3);
            GUI.enabled = false;
            EditorGUILayout.IntField("Total", data.total);
            GUI.enabled = true;
        }
    }
}
