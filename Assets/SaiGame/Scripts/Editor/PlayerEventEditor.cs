using System.Text;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(PlayerEvent))]
    public class PlayerEventEditor : Editor
    {
        private const string SessionIdInfo =
            "Why Session ID:\n" +
            "• Groups events of the same play session, separating them from other sessions of the same user.\n" +
            "• Enables per-session analytics (duration, action sequence, drop-off) that user_id alone cannot.\n" +
            "• Lets us replay the exact flow of one play session when debugging.\n" +
            "• Generated fresh on each login, cleared on logout — avoids mixing sessions across logins or devices.";

        private SerializedProperty sessionId;
        private SerializedProperty eventType;
        private SerializedProperty eventDataJson;

        private void OnEnable()
        {
            this.sessionId = serializedObject.FindProperty("sessionId");
            this.eventType = serializedObject.FindProperty("eventType");
            this.eventDataJson = serializedObject.FindProperty("eventDataJson");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PlayerEvent playerEvent = (PlayerEvent)target;

            EditorGUILayout.HelpBox(SessionIdInfo, MessageType.Info);
            EditorGUILayout.Space();

            // Draw label + field + New button all on one row
            Rect sessionRow = EditorGUILayout.GetControlRect();
            float newBtnWidth = 45f;
            float spacing = 4f;
            float labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new Rect(sessionRow.x, sessionRow.y, labelWidth, sessionRow.height);
            Rect fieldRect = new Rect(sessionRow.x + labelWidth, sessionRow.y, sessionRow.width - labelWidth - newBtnWidth - spacing, sessionRow.height);
            Rect btnRect   = new Rect(sessionRow.xMax - newBtnWidth, sessionRow.y, newBtnWidth, sessionRow.height);

            EditorGUI.LabelField(labelRect, new GUIContent("Session ID", "Auto-generated on each login. Can be overridden manually."));
            EditorGUI.BeginChangeCheck();
            string newSessionValue = EditorGUI.TextField(fieldRect, this.sessionId.stringValue);
            if (EditorGUI.EndChangeCheck())
                this.sessionId.stringValue = newSessionValue;
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUI.Button(btnRect, "New"))
            {
                playerEvent.RegenerateSessionId();
                serializedObject.Update();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Event Settings
            EditorGUILayout.PropertyField(this.eventType, new GUIContent("Event Type", "The type of event to track (e.g. join_game, start_level, quit_game)"));

            EditorGUILayout.Space(4);

            // Event Data JSON textarea
            EditorGUILayout.LabelField("Event Data (JSON)", EditorStyles.boldLabel);
            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            this.eventDataJson.stringValue = EditorGUILayout.TextArea(
                this.eventDataJson.stringValue,
                textAreaStyle,
                GUILayout.MinHeight(80)
            );

            // Beautify / Minify buttons
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            if (GUILayout.Button("Beautify", GUILayout.Height(22)))
            {
                string pretty = BeautifyJson(this.eventDataJson.stringValue);
                if (pretty != null)
                    this.eventDataJson.stringValue = pretty;
                else
                    Debug.LogWarning("[PlayerEventEditor] Cannot beautify: invalid JSON.");
            }
            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
            if (GUILayout.Button("Minify", GUILayout.Height(22)))
            {
                this.eventDataJson.stringValue = MinifyJson(this.eventDataJson.stringValue);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Track Event button
            GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
            if (GUILayout.Button("Track Event", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    playerEvent.TrackEvent(
                        onSuccess: r =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.Log($"[Editor] Event tracked! Type: {playerEvent.EventType}, Session: {playerEvent.SessionId}, ID: {r.event_id}");
                        },
                        onError: error =>
                        {
                            if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                                Debug.LogError($"[Editor] Track event failed: {error}");
                        }
                    );
                }
                else
                {
                    Debug.LogWarning("[Editor] Track Event requires Play Mode.");
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private static string BeautifyJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            char prev = '\0';

            foreach (char c in json)
            {
                if (c == '"' && prev != '\\') inString = !inString;

                if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                    }
                    else if (c == '}' || c == ']')
                    {
                        sb.AppendLine();
                        indent--;
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(c);
                    }
                    else if (c == ',')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        sb.Append(new string(' ', indent * 2));
                    }
                    else if (c == ':')
                    {
                        sb.Append(": ");
                    }
                    else if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }

                prev = c;
            }

            // Basic validation: indent should return to 0 for valid JSON
            return indent == 0 ? sb.ToString() : null;
        }

        private static string MinifyJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            var sb = new StringBuilder();
            bool inString = false;
            char prev = '\0';

            foreach (char c in json)
            {
                if (c == '"' && prev != '\\') inString = !inString;

                if (inString)
                    sb.Append(c);
                else if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    sb.Append(c);

                prev = c;
            }

            return sb.ToString();
        }
    }
}
