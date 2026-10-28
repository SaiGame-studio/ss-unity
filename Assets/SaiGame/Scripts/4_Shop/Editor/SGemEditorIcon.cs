using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Draws the sGem icon (same artwork as the web sgem.svg) next to "sGem" text in inspectors.
    /// </summary>
    internal static class SGemEditorIcon
    {
        public const string ICON_PATH = "Assets/SaiGame/Icons/SGemIcon.png";
        private const float ICON_SIZE = 16f;
        private const float INDENT_WIDTH = 15f;

        private static Texture2D icon;

        public static Texture2D Icon
        {
            get
            {
                if (icon == null) icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
                return icon;
            }
        }

        /// <summary>Adds the current inspector indent inside a horizontal GUILayout row.</summary>
        public static void Indent()
        {
            GUILayout.Space(EditorGUI.indentLevel * INDENT_WIDTH);
        }

        /// <summary>Draws the sGem icon followed by text, inside a horizontal GUILayout row.</summary>
        public static void InlineLabel(string text = ShopCurrencyTypes.SGemLabel)
        {
            Texture2D texture = Icon;
            if (texture != null)
            {
                Rect rect = GUILayoutUtility.GetRect(ICON_SIZE, EditorGUIUtility.singleLineHeight,
                    GUILayout.Width(ICON_SIZE), GUILayout.ExpandWidth(false));
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            }
            GUILayout.Label(text, GUILayout.ExpandWidth(false));
        }

        /// <summary>Button whose label starts with the sGem icon.</summary>
        public static bool Button(string text, params GUILayoutOption[] options)
        {
            Vector2 previousIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(ICON_SIZE, ICON_SIZE));
            bool clicked = GUILayout.Button(new GUIContent(text, Icon), options);
            EditorGUIUtility.SetIconSize(previousIconSize);
            return clicked;
        }
    }
}
