using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemsCached))]
    public class ItemsCachedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            SerializedProperty itemDefinitions = this.serializedObject.FindProperty("itemDefinitions");
            int count = itemDefinitions.arraySize;

            EditorGUILayout.LabelField($"Item Definitions ({count})", EditorStyles.boldLabel);

            if (count == 0)
            {
                EditorGUILayout.HelpBox("No item definitions have been cached yet.", MessageType.Info);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                SerializedProperty definition = itemDefinitions.GetArrayElementAtIndex(i);
                SerializedProperty itemCodeProperty = definition.FindPropertyRelative("item_code");

                string itemCode = itemCodeProperty == null || string.IsNullOrEmpty(itemCodeProperty.stringValue)
                    ? "(No Item Code)"
                    : itemCodeProperty.stringValue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                definition.isExpanded = EditorGUILayout.Foldout(
                    definition.isExpanded,
                    itemCode,
                    true,
                    EditorStyles.foldout);

                if (definition.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    this.DrawDefinitionProperties(definition);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            this.serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefinitionProperties(SerializedProperty definition)
        {
            this.DrawDefinitionProperty(definition, "id");
            this.DrawDefinitionProperty(definition, "studio_id");
            this.DrawDefinitionProperty(definition, "game_id");
            this.DrawDefinitionProperty(definition, "item_code");
            this.DrawDefinitionProperty(definition, "name");
            this.DrawDefinitionProperty(definition, "category");
            this.DrawDefinitionProperty(definition, "rarity");
            this.DrawDefinitionProperty(definition, "base_stats");
            this.DrawDefinitionProperty(definition, "metadata");
            this.DrawDefinitionProperty(definition, "is_stackable");
            this.DrawDefinitionProperty(definition, "max_stack_size");
            this.DrawDefinitionProperty(definition, "grid_width");
            this.DrawDefinitionProperty(definition, "grid_height");
            this.DrawDefinitionProperty(definition, "client_writable");
            this.DrawDefinitionProperty(definition, "allow_client_update_qty");
            this.DrawDefinitionProperty(definition, "created_by");
            this.DrawDefinitionProperty(definition, "created_at");
            this.DrawDefinitionProperty(definition, "updated_at");
        }

        private void DrawDefinitionProperty(SerializedProperty definition, string propertyName)
        {
            SerializedProperty property = definition.FindPropertyRelative(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }
    }
}
