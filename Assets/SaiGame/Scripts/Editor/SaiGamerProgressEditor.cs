using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GamerProgress))]
    public class SaiGamerProgressEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw all properties except gameData
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "gameData")
                {
                    EditorGUILayout.Space(5);
                    // Draw label + Beautify button on same row
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Game Data", GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                    GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                    if (GUILayout.Button("Beautify JSON", GUILayout.Height(18)))
                    {
                        GUI.FocusControl(null);
                        string raw = prop.stringValue;
                        if (!string.IsNullOrEmpty(raw) && raw != "{}")
                        {
                            try
                            {
                                prop.stringValue = BeautifyJson(raw);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[GamerProgress] Failed to beautify JSON: {e.Message}");
                            }
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // Draw the text area without label
                    prop.stringValue = EditorGUILayout.TextArea(prop.stringValue, GUILayout.MinHeight(60));
                    continue;
                }
                if (prop.name == "m_Script") { GUI.enabled = false; EditorGUILayout.PropertyField(prop); GUI.enabled = true; }
                else EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();

            GamerProgress gamerProgress = (GamerProgress)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Progress Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Create Progress", GUILayout.Height(30)))
            {
                gamerProgress.CreateProgress(
                    progress => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Progress created! ID: {progress.id}, Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"); },
                    error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Create progress failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Get Progress", GUILayout.Height(30)))
            {
                gamerProgress.GetProgress(
                    progress => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Progress retrieved! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}, Game Data: {progress.game_data}"); },
                    error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Get progress failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            SerializedProperty expDeltaProp = serializedObject.FindProperty("experienceDelta");
            SerializedProperty goldDeltaProp = serializedObject.FindProperty("goldDelta");
            int expDelta = expDeltaProp != null ? expDeltaProp.intValue : 100;
            int goldDelta = goldDeltaProp != null ? goldDeltaProp.intValue : 50;

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button($"Update Progress (+{expDelta} XP, +{goldDelta} Gold)", GUILayout.Height(25)))
            {
                if (gamerProgress.HasProgress)
                {
                    gamerProgress.UpdateProgress(
                        expDelta,
                        goldDelta,
                        null,
                        progress => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.Log($"[Editor] Progress updated! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"); },
                        error => { if (SaiService.Instance == null || SaiService.Instance.ShowDebug) Debug.LogError($"[Editor] Update progress failed: {error}"); }
                    );
                }
                else
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogWarning("[Editor] No progress found! Create progress first.");
                }
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Progress", GUILayout.Height(25)))
            {
                gamerProgress.ClearProgress();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private string BeautifyJson(string json)
        {
            var sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;
            bool escaped = false;

            foreach (char c in json)
            {
                if (escaped) { sb.Append(c); escaped = false; continue; }
                if (c == '\\' && inString) { sb.Append(c); escaped = true; continue; }
                if (c == '"') { inString = !inString; sb.Append(c); continue; }
                if (inString) { sb.Append(c); continue; }
                if (char.IsWhiteSpace(c)) continue;

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c).Append('\n');
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent--;
                        sb.Append(new string(' ', indent * 2)).Append(c);
                        break;
                    case ',':
                        sb.Append(c).Append('\n').Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}