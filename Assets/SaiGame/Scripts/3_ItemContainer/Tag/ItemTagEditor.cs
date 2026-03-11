using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemTag))]
    public class ItemTagEditor : Editor
    {
        private readonly Dictionary<string, bool> tagFoldouts = new Dictionary<string, bool>();

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
