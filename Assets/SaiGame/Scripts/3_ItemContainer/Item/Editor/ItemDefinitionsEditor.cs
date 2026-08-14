using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemDefinitions))]
    public class ItemDefinitionsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();
            ItemDefinitions service = (ItemDefinitions)this.target;
            SerializedProperty fetchItemId = this.serializedObject.FindProperty("fetchItemId");
            SerializedProperty fetchItemCode = this.serializedObject.FindProperty("fetchItemCode");
            SerializedProperty itemDefinitions = this.serializedObject.FindProperty("itemDefinitions");

            EditorGUILayout.LabelField("Fetch Item Definition", EditorStyles.boldLabel);
            this.DrawFetchRow("Item ID", fetchItemId, () =>
            {
                this.serializedObject.ApplyModifiedProperties();
                service.FetchById(fetchItemId.stringValue, this.HandleFetchSuccess, this.HandleFetchError);
            });
            this.DrawFetchRow("Item Code", fetchItemCode, () =>
            {
                this.serializedObject.ApplyModifiedProperties();
                service.FetchByCode(fetchItemCode.stringValue, this.HandleFetchSuccess, this.HandleFetchError);
            });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Item Definitions ({itemDefinitions.arraySize})", EditorStyles.boldLabel);
            for (int i = 0; i < itemDefinitions.arraySize; i++)
            {
                SerializedProperty definition = itemDefinitions.GetArrayElementAtIndex(i);
                SerializedProperty itemCode = definition.FindPropertyRelative("item_code");
                string label = itemCode == null || string.IsNullOrEmpty(itemCode.stringValue)
                    ? "(No Item Code)"
                    : itemCode.stringValue;

                definition.isExpanded = EditorGUILayout.Foldout(definition.isExpanded, label, true);
                if (definition.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    this.DrawDefinitionProperties(definition);
                    EditorGUI.indentLevel--;
                }
            }

            this.serializedObject.ApplyModifiedProperties();
        }

        private void DrawFetchRow(string label, SerializedProperty input, System.Action onGet)
        {
            const float labelWidth = 110f;
            const float getButtonWidth = 56f;
            const float spacing = 4f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            input.stringValue = EditorGUILayout.TextField(input.stringValue);
            GUILayout.Space(spacing);
            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Get", GUILayout.Width(getButtonWidth)))
                onGet();
            GUI.backgroundColor = previousBackgroundColor;
            EditorGUILayout.EndHorizontal();
        }

        private void HandleFetchSuccess(ItemDefinitionData definition)
        {
            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                Debug.Log($"[ItemDefinitionsEditor] Cached {definition.item_code} ({definition.id})", this.target);
            this.Repaint();
        }

        private void HandleFetchError(string error)
        {
            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                Debug.LogError($"[ItemDefinitionsEditor] Fetch failed: {error}", this.target);
            this.Repaint();
        }

        private void DrawDefinitionProperties(SerializedProperty definition)
        {
            string[] propertyNames =
            {
                "id", "studio_id", "game_id", "item_code", "name", "category", "rarity",
                "base_stats", "metadata", "is_stackable", "max_stack_size", "max_owned_quantity", "grid_width",
                "grid_height", "client_writable", "allow_client_update_qty", "created_by",
                "updated_by", "created_at", "updated_at"
            };

            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = definition.FindPropertyRelative(propertyName);
                if (property != null)
                    EditorGUILayout.PropertyField(property);
            }
        }
    }
}
