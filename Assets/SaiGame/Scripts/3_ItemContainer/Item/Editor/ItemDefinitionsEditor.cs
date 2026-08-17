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

                string label = string.IsNullOrEmpty(definition.name)
                    ? definition.item_code
                    : $"{definition.name}  [{definition.category}]";
                if (string.IsNullOrEmpty(label))
                    label = "(Unnamed Item Definition)";
                string foldoutKey = string.IsNullOrEmpty(definition.id) ? i.ToString() : definition.id;
                this.definitionFoldouts.TryGetValue(foldoutKey, out bool isExpanded);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                };
                Color rarityColor = GetRarityColor(definition.rarity);
                foldoutStyle.normal.textColor = rarityColor;
                foldoutStyle.onNormal.textColor = rarityColor;
                foldoutStyle.focused.textColor = rarityColor;
                foldoutStyle.onFocused.textColor = rarityColor;
                foldoutStyle.active.textColor = rarityColor;
                foldoutStyle.onActive.textColor = rarityColor;

                isExpanded = EditorGUILayout.Foldout(isExpanded, label, true, foldoutStyle);
                this.definitionFoldouts[foldoutKey] = isExpanded;
                if (isExpanded)
                {
                    EditorGUI.indentLevel++;
                    this.DrawDefinitionProperties(definition);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
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
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Definition", EditorStyles.boldLabel);
            DrawIdField("ID", definition.id);
            DrawIdField("Game ID", definition.game_id);
            EditorGUILayout.LabelField("Item Code", definition.item_code);
            EditorGUILayout.LabelField("Name", definition.name);
            EditorGUILayout.LabelField("Description", definition.description);
            EditorGUILayout.LabelField("Category", definition.category);
            EditorGUILayout.LabelField("Rarity", definition.rarity);
            EditorGUILayout.LabelField("Stackable", $"{definition.is_stackable}  (max stack {definition.max_stack_size})");
            EditorGUILayout.LabelField("Max Owned Quantity", definition.max_owned_quantity.ToString());
            EditorGUILayout.LabelField("Grid Size", $"{definition.grid_width} × {definition.grid_height}");
            EditorGUILayout.LabelField("Client Writable", definition.client_writable.ToString());
            EditorGUILayout.LabelField("Allow Client Qty", definition.allow_client_update_qty.ToString());

            DrawJsonField("Base Stats", definition.base_stats);
            DrawJsonField("Metadata", definition.metadata);
        }

        private static void DrawIdField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, value ?? string.Empty);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = value ?? string.Empty;
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawJsonField(string label, string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            string formattedJson = PrettyJson(json);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                formattedJson,
                EditorStyles.textArea,
                GUILayout.MinHeight(EditorStyles.textArea.lineHeight * (CountLines(formattedJson) + 1)));
        }

        private static Color GetRarityColor(string rarity)
        {
            switch (rarity?.ToLowerInvariant())
            {
                case "common": return new Color(0.8f, 0.8f, 0.8f);
                case "uncommon": return new Color(0.35f, 0.9f, 0.45f);
                case "rare": return new Color(0.35f, 0.65f, 1f);
                case "epic": return new Color(0.75f, 0.4f, 1f);
                case "legendary": return new Color(1f, 0.7f, 0.2f);
                default: return EditorStyles.foldout.normal.textColor;
            }
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value)) return 1;

            int count = 1;
            foreach (char character in value)
                if (character == '\n') count++;
            return count;
        }

        private static string PrettyJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return "{}";

            var builder = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char character in json)
            {
                if (character == '"' && (builder.Length == 0 || builder[builder.Length - 1] != '\\'))
                    inString = !inString;

                if (inString)
                {
                    builder.Append(character);
                    continue;
                }

                switch (character)
                {
                    case '{':
                    case '[':
                        builder.Append(character);
                        builder.Append('\n');
                        indent++;
                        builder.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        builder.Append('\n');
                        indent--;
                        builder.Append(new string(' ', indent * 2));
                        builder.Append(character);
                        break;
                    case ',':
                        builder.Append(character);
                        builder.Append('\n');
                        builder.Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        builder.Append(": ");
                        break;
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
