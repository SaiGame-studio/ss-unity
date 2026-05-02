using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(BattleScript))]
    [CanEditMultipleObjects]
    public class BattleScriptEditor : Editor
    {
        private BattleScript battleScript;
        private SerializedProperty scriptName;
        private SerializedProperty requestBody;
        private SerializedProperty jsonResponse;

        private bool showRequestBody    = true;
        private bool showResponse       = true;
        private bool showUtilityButtons = true;
        private Vector2 responseScroll  = Vector2.zero;
        private Vector2 requestScroll   = Vector2.zero;

        private void OnEnable()
        {
            this.battleScript  = (BattleScript)target;
            this.scriptName    = serializedObject.FindProperty("scriptName");
            this.requestBody   = serializedObject.FindProperty("requestBody");
            this.jsonResponse  = serializedObject.FindProperty("jsonResponse");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Battle Script Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.scriptName, new GUIContent("Script Name", "Name of the Lua script to run on the server"));

            EditorGUILayout.Space();

            // Request Body
            this.showRequestBody = EditorGUILayout.Foldout(this.showRequestBody, "Request Body", true);
            if (this.showRequestBody)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("JSON Body (editable)", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.75f, 0f);
                if (GUILayout.Button("Format JSON", GUILayout.Width(90), GUILayout.Height(18)))
                {
                    GUI.FocusControl(null);
                    this.requestBody.stringValue = this.battleScript.BeautifyJson(this.requestBody.stringValue);
                    serializedObject.ApplyModifiedProperties();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                GUIStyle bodyStyle = new GUIStyle(EditorStyles.textArea);
                bodyStyle.wordWrap = true;
                bodyStyle.fontSize = 11;
                bodyStyle.richText = false;
                bodyStyle.font     = EditorStyles.textField.font;

                string current = this.requestBody.stringValue;
                float height = bodyStyle.CalcHeight(new GUIContent(current), EditorGUIUtility.currentViewWidth - 40);
                height = Mathf.Clamp(height, 80f, 300f);

                string edited = EditorGUILayout.TextArea(current, bodyStyle, GUILayout.Height(height));
                if (edited != current)
                {
                    this.requestBody.stringValue = edited;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // JSON Response View
            this.showResponse = EditorGUILayout.Foldout(this.showResponse, "JSON Response", true);
            if (this.showResponse)
            {
                EditorGUI.indentLevel++;

                if (!string.IsNullOrEmpty(this.battleScript.JsonResponse))
                {
                    EditorGUILayout.LabelField("Raw Response", EditorStyles.boldLabel);

                    GUIStyle jsonStyle = new GUIStyle(EditorStyles.textArea);
                    jsonStyle.wordWrap  = false;
                    jsonStyle.fontSize  = 11;
                    jsonStyle.richText  = false;

                    // calc content size without wrapping to get true width & height
                    GUIContent jsonContent = new GUIContent(this.battleScript.JsonResponse);
                    Vector2 contentSize = jsonStyle.CalcSize(jsonContent);
                    float scrollHeight  = Mathf.Min(contentSize.y + 6f, 300f);

                    this.responseScroll = EditorGUILayout.BeginScrollView(
                        this.responseScroll,
                        alwaysShowHorizontal: true,
                        alwaysShowVertical:   false,
                        GUILayout.Height(scrollHeight));

                    EditorGUILayout.SelectableLabel(
                        this.battleScript.JsonResponse,
                        jsonStyle,
                        GUILayout.Height(contentSize.y),
                        GUILayout.Width(contentSize.x));

                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox("No response yet. Click \"Run Script\" to fetch data.", MessageType.None);
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
                if (GUILayout.Button("Run Script", GUILayout.Height(30)))
                {
                    this.RunScript();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Response", GUILayout.Height(30)))
                {
                    this.battleScript.ClearResponse();
                    serializedObject.Update();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("OnRunScriptSuccess / OnRunScriptFailure events are fired after each API call.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void RunScript()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[BattleScriptEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[BattleScriptEditor] Not authenticated! Please login first.");
                return;
            }

            this.battleScript.RunScript(
                overrideRequestBody: this.battleScript.RequestBody,
                onSuccess: response =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[BattleScriptEditor] Script response received ({response.Length} chars)");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[BattleScriptEditor] Failed to run script: {error}");
                }
            );
        }
    }
}
