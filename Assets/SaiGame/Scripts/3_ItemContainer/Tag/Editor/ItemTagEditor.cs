using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemTag))]
    public class ItemTagEditor : Editor
    {
        private readonly Dictionary<string, bool> tagFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, InventoryResponse> tagItemsCache = new Dictionary<string, InventoryResponse>();
        private readonly HashSet<string> loadingItemTags = new HashSet<string>();

        private bool showCurrentTags = true;
        private bool showTagList = true;
        private bool showUtilityButtons = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ItemTag itemTag = (ItemTag)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Tag Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            SerializedProperty autoLoadProp = serializedObject.FindProperty("autoLoadOnLogin");
            EditorGUILayout.PropertyField(autoLoadProp, new GUIContent("Auto Load on Login", "Automatically load tags when user logs in"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            SerializedProperty tagLimitProp = serializedObject.FindProperty("tagLimit");
            SerializedProperty tagOffsetProp = serializedObject.FindProperty("tagOffset");
            EditorGUILayout.PropertyField(tagLimitProp, new GUIContent("Tag Limit"));
            EditorGUILayout.PropertyField(tagOffsetProp, new GUIContent("Tag Offset"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            // Current Tags Data
            this.showCurrentTags = EditorGUILayout.Foldout(this.showCurrentTags, "Current Tags Data", true);
            if (this.showCurrentTags)
            {
                EditorGUI.indentLevel++;
                this.DrawCurrentTags(itemTag);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.66f, 0.33f, 0.97f);
                if (GUILayout.Button("Get Tags", GUILayout.Height(30)))
                {
                    itemTag.GetTags(
                        tags =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log($"[Editor] Tags loaded! Total: {tags.total}, Count: {tags.tags?.Length ?? 0}");
                            Repaint();
                        },
                        error =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Get tags failed: {error}");
                        }
                    );
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Tags", GUILayout.Height(30)))
                {
                    itemTag.ClearTags();
                    this.tagItemsCache.Clear();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);
        }

        private void DrawCurrentTags(ItemTag itemTag)
        {
            ItemTagsResponse data = itemTag.CurrentTags;

            if (data == null)
            {
                EditorGUILayout.HelpBox("No tags loaded yet.", MessageType.None);
                return;
            }

            // Summary
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Loaded Tags: {data.tags?.Length ?? 0} / {data.total}");
            EditorGUILayout.LabelField($"Limit: {data.limit}    Offset: {data.offset}");

            EditorGUILayout.Space(2);

            if (data.tags == null || data.tags.Length == 0)
            {
                EditorGUILayout.HelpBox("Tags array is empty.", MessageType.None);
                return;
            }

            this.showTagList = EditorGUILayout.Foldout(this.showTagList, "Tag List", true);
            if (!this.showTagList) return;

            EditorGUI.indentLevel++;
            foreach (ItemTagData tag in data.tags)
            {
                this.DrawTagCard(itemTag, tag);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawTagCard(ItemTag itemTag, ItemTagData tag)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string foldoutKey = tag.tag_key ?? tag.id ?? "unknown";
            if (!this.tagFoldouts.ContainsKey(foldoutKey))
                this.tagFoldouts[foldoutKey] = false;

            Color tagColor = this.ParseHexColor(tag.color, new Color(0.66f, 0.33f, 0.97f));
            string headerName = !string.IsNullOrEmpty(tag.label) ? tag.label : foldoutKey;
            string headerLabel = $"🏷  {headerName}  [{tag.item_count}]";

            // === COLLAPSIBLE HEADER ===
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor = tagColor;
            foldoutStyle.onNormal.textColor = tagColor;
            foldoutStyle.focused.textColor = tagColor;
            foldoutStyle.onFocused.textColor = tagColor;
            foldoutStyle.active.textColor = tagColor;
            foldoutStyle.onActive.textColor = tagColor;

            EditorGUILayout.BeginHorizontal();
            this.tagFoldouts[foldoutKey] = EditorGUILayout.Foldout(this.tagFoldouts[foldoutKey], headerLabel, true, foldoutStyle);

            // Tag key badge (right-aligned)
            GUIStyle keyStyle = new GUIStyle(EditorStyles.label);
            keyStyle.fontSize = 11;
            keyStyle.normal.textColor = tagColor;
            keyStyle.fontStyle = FontStyle.Bold;
            keyStyle.alignment = TextAnchor.MiddleRight;
            EditorGUILayout.LabelField(foldoutKey, keyStyle, GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();

            if (!this.tagFoldouts[foldoutKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            // Subtle separator
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);

            // === COLOR BANNER ===
            this.DrawColorBanner(tag.color, tagColor);

            // === COMPACT INFO ===
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            EditorGUILayout.LabelField($"key: {tag.tag_key}", labelStyle);
            if (!string.IsNullOrEmpty(tag.label))
                EditorGUILayout.LabelField($"label: {tag.label}", labelStyle);
            EditorGUILayout.LabelField($"items: {tag.item_count}", labelStyle);

            // ID with copy
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"id: {tag.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = tag.id ?? "";
            EditorGUILayout.EndHorizontal();

            // Game ID with copy
            EditorGUILayout.BeginHorizontal();
            GUIStyle subIdStyle = new GUIStyle(EditorStyles.label);
            subIdStyle.fontSize = 10;
            subIdStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            EditorGUILayout.LabelField($"game: {tag.game_id}", subIdStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = tag.game_id ?? "";
            EditorGUILayout.EndHorizontal();

            // Metadata (italic, when present and not empty)
            if (!string.IsNullOrEmpty(tag.metadata) && tag.metadata.Trim() != "{}")
            {
                EditorGUILayout.Space(2);
                GUIStyle metaStyle = new GUIStyle(EditorStyles.label);
                metaStyle.fontSize = 9;
                metaStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                metaStyle.fontStyle = FontStyle.Italic;
                metaStyle.wordWrap = true;
                EditorGUILayout.LabelField($"metadata: {tag.metadata}", metaStyle);
            }

            // Timestamps (compact)
            EditorGUILayout.Space(2);
            GUIStyle timeStyle = new GUIStyle(EditorStyles.label);
            timeStyle.fontSize = 9;
            timeStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);
            EditorGUILayout.LabelField($"⏱ created {tag.created_at}    by {tag.created_by}", timeStyle);
            EditorGUILayout.LabelField($"⏱ updated {tag.updated_at}", timeStyle);

            EditorGUILayout.Space(8);

            // === ACTION BUTTONS ===
            string capturedKey = foldoutKey;
            bool isLoading = this.loadingItemTags.Contains(capturedKey);
            bool hasItems = this.tagItemsCache.TryGetValue(capturedKey, out InventoryResponse cachedItems) && cachedItems?.items != null;

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = isLoading ? Color.gray : new Color(0.3f, 0.9f, 0.5f);
            EditorGUI.BeginDisabledGroup(isLoading);
            string btnLabel = isLoading
                ? "🔄 Loading..."
                : (hasItems ? $"🔄 Reload Items ({cachedItems.items.Length})" : "📥 Get Items");
            if (GUILayout.Button(btnLabel, GUILayout.Height(28)))
            {
                this.loadingItemTags.Add(capturedKey);
                Repaint();

                itemTag.GetItemsByTag(capturedKey,
                    result =>
                    {
                        this.loadingItemTags.Remove(capturedKey);
                        this.tagItemsCache[capturedKey] = result;
                        if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                            Debug.Log($"[Editor] Items for [{capturedKey}]: {result.total} total");
                        Repaint();
                    },
                    error =>
                    {
                        this.loadingItemTags.Remove(capturedKey);
                        if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                            Debug.LogError($"[Editor] Get items for [{capturedKey}] failed: {error}");
                        Repaint();
                    }
                );
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            if (hasItems)
            {
                GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
                if (GUILayout.Button("✕ Clear", GUILayout.Height(28), GUILayout.Width(80)))
                {
                    this.tagItemsCache.Remove(capturedKey);
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // === ITEMS LIST (Card-based) ===
            if (hasItems)
            {
                EditorGUILayout.Space(6);

                GUIStyle itemsHeader = new GUIStyle(EditorStyles.boldLabel);
                itemsHeader.fontSize = 11;
                itemsHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField($"📦 items ({cachedItems.items.Length} / {cachedItems.total})", itemsHeader);

                EditorGUILayout.Space(3);

                foreach (InventoryItemData item in cachedItems.items)
                {
                    this.DrawItemCard(foldoutKey, item);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawItemCard(string parentKey, InventoryItemData item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string itemFoldoutKey = parentKey + "_" + item.id;
            if (!this.itemFoldouts.ContainsKey(itemFoldoutKey))
                this.itemFoldouts[itemFoldoutKey] = false;

            string itemName = item.definition?.name ?? item.item_definition_id ?? item.id;
            string rarity = item.definition?.rarity ?? "common";
            Color rarityColor = this.GetRarityColor(rarity);

            string headerLabel = $"★ {itemName}  ×{item.quantity}";

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 12;
            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor = rarityColor;
            foldoutStyle.onNormal.textColor = rarityColor;
            foldoutStyle.focused.textColor = rarityColor;
            foldoutStyle.onFocused.textColor = rarityColor;
            foldoutStyle.active.textColor = rarityColor;
            foldoutStyle.onActive.textColor = rarityColor;

            EditorGUILayout.BeginHorizontal();
            this.itemFoldouts[itemFoldoutKey] = EditorGUILayout.Foldout(this.itemFoldouts[itemFoldoutKey], headerLabel, true, foldoutStyle);

            // Rarity badge
            if (item.definition != null && !string.IsNullOrEmpty(item.definition.rarity))
            {
                GUIStyle rarityStyle = new GUIStyle(EditorStyles.label);
                rarityStyle.fontSize = 10;
                rarityStyle.normal.textColor = rarityColor;
                rarityStyle.fontStyle = FontStyle.Bold;
                rarityStyle.alignment = TextAnchor.MiddleRight;
                EditorGUILayout.LabelField(item.definition.rarity, rarityStyle, GUILayout.MinWidth(70));
            }
            EditorGUILayout.EndHorizontal();

            if (!this.itemFoldouts[itemFoldoutKey])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
                return;
            }

            // Separator
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("──────────────────────────────────────────────────────────────────────────────────", separatorStyle);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            // Definition info
            if (item.definition != null)
            {
                if (!string.IsNullOrEmpty(item.definition.item_code))
                    EditorGUILayout.LabelField($"code: {item.definition.item_code}", labelStyle);
                if (!string.IsNullOrEmpty(item.definition.category))
                    EditorGUILayout.LabelField($"category: {item.definition.category}", labelStyle);
                EditorGUILayout.LabelField($"grid: {item.grid_x}, {item.grid_y}", labelStyle);
                EditorGUILayout.LabelField($"stack: {(item.definition.is_stackable ? "yes" : "no")}  (max {item.definition.max_stack_size})", labelStyle);
            }
            else
            {
                EditorGUILayout.LabelField($"grid: {item.grid_x}, {item.grid_y}", labelStyle);
            }

            EditorGUILayout.LabelField($"level: {item.level}    quantity: {item.quantity}    version: {item.version}", labelStyle);

            // Item ID with copy
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"id: {item.id}", idStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.id ?? "";
            EditorGUILayout.EndHorizontal();

            // Definition ID with copy
            GUIStyle subIdStyle = new GUIStyle(EditorStyles.label);
            subIdStyle.fontSize = 10;
            subIdStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"definition: {item.item_definition_id}", subIdStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.item_definition_id ?? "";
            EditorGUILayout.EndHorizontal();

            // Container ID with copy
            EditorGUILayout.BeginHorizontal();
            GUIStyle containerStyle = new GUIStyle(EditorStyles.label);
            containerStyle.fontSize = 10;
            containerStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            EditorGUILayout.LabelField($"container: {item.item_container_id}", containerStyle);
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.item_container_id ?? "";
            EditorGUILayout.EndHorizontal();

            // Acquired timestamp
            if (!string.IsNullOrEmpty(item.acquired_at))
            {
                EditorGUILayout.Space(2);
                GUIStyle timeStyle = new GUIStyle(EditorStyles.label);
                timeStyle.fontSize = 9;
                timeStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);
                EditorGUILayout.LabelField($"⏱ acquired {item.acquired_at}", timeStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawColorBanner(string colorString, Color resolvedColor)
        {
            string display = string.IsNullOrEmpty(colorString) ? "—" : colorString;

            Rect line = EditorGUILayout.GetControlRect(false, 14);
            Rect swatch = new Rect(line.x, line.y + 2, 18, 10);

            EditorGUI.DrawRect(swatch, resolvedColor);
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y, swatch.width, 1), new Color(0f, 0f, 0f, 0.4f));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + swatch.height - 1, swatch.width, 1), new Color(0f, 0f, 0f, 0.4f));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y, 1, swatch.height), new Color(0f, 0f, 0f, 0.4f));
            EditorGUI.DrawRect(new Rect(swatch.x + swatch.width - 1, swatch.y, 1, swatch.height), new Color(0f, 0f, 0f, 0.4f));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            Rect textRect = new Rect(swatch.xMax + 6, line.y, line.width - swatch.width - 6, line.height);
            GUI.Label(textRect, $"color  {display}", labelStyle);

            EditorGUILayout.Space(2);
        }

        private Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;

            string cleaned = hex.Trim();
            if (!cleaned.StartsWith("#")) cleaned = "#" + cleaned;

            if (ColorUtility.TryParseHtmlString(cleaned, out Color result))
                return result;

            return fallback;
        }

        private Color GetRarityColor(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return Color.white;

            switch (rarity.ToLower(CultureInfo.InvariantCulture))
            {
                case "common":    return new Color(0.7f, 0.7f, 0.7f);
                case "uncommon":  return new Color(0.3f, 1f, 0.3f);
                case "rare":      return new Color(0.3f, 0.6f, 1f);
                case "epic":      return new Color(0.8f, 0.3f, 1f);
                case "legendary": return new Color(1f, 0.6f, 0f);
                case "mythic":    return new Color(1f, 0.3f, 0.3f);
                default:          return Color.white;
            }
        }
    }
}
