using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemDefinitions))]
    public class ItemDefinitionsEditor : Editor
    {
        private readonly Dictionary<string, bool> definitionFoldouts = new Dictionary<string, bool>();

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();
            ItemDefinitions service = (ItemDefinitions)this.target;
            SerializedProperty fetchItemId = this.serializedObject.FindProperty("fetchItemId");
            SerializedProperty fetchItemCode = this.serializedObject.FindProperty("fetchItemCode");

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
            EditorGUILayout.LabelField($"Item Definitions ({service.Definitions.Count})", EditorStyles.boldLabel);
            for (int i = 0; i < service.Definitions.Count; i++)
            {
                ItemDefinitionData definition = service.Definitions[i];
                if (definition == null)
                    continue;

                string label = string.IsNullOrEmpty(definition.item_code)
                    ? "(No Item Code)"
                    : definition.item_code;
                string foldoutKey = string.IsNullOrEmpty(definition.id) ? i.ToString() : definition.id;
                this.definitionFoldouts.TryGetValue(foldoutKey, out bool isExpanded);

                isExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
                this.definitionFoldouts[foldoutKey] = isExpanded;
                if (isExpanded)
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

        private void DrawDefinitionProperties(ItemDefinitionData definition)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(JsonUtility.ToJson(definition, true));
            EditorGUI.EndDisabledGroup();
        }
    }
}
