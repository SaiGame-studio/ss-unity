using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GamerProgress))]
    public class SaiGamerProgressEditor : Editor
    {
        private string twoFactorCode = string.Empty;

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
                else
                {
                    using (new EditorGUI.DisabledScope(prop.name == "isDataLoaded"))
                        EditorGUILayout.PropertyField(prop, true);
                }
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
                    progress => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Progress created! ID: {progress.id}, Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"); },
                    error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Create progress failed: {error}"); }
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Get Progress", GUILayout.Height(30)))
            {
                gamerProgress.GetProgress(
                    progress => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Progress retrieved! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}, Game Data: {progress.game_data}"); },
                    error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Get progress failed: {error}"); }
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
                if (gamerProgress.IsDataLoaded)
                {
                    gamerProgress.UpdateProgress(
                        expDelta,
                        goldDelta,
                        null,
                        progress => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] Progress updated! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"); },
                        error => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.LogError($"[Editor] Update progress failed: {error}"); }
                    );
                }
                else
                {
                    Debug.LogError("Call GamerProgress.CreateProgress or GamerProgress.GetProgress first and successfully load GamerProgress data before updating progress.", gamerProgress);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Check 2FA", GUILayout.Height(25), GUILayout.Width(100)))
            {
                gamerProgress.CheckTwoFactorStatus(
                    status => { if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug) Debug.Log($"[Editor] 2FA status: enabled={status.enabled}, verification_required={status.verification_required}"); Repaint(); },
                    error => { Debug.LogError($"[Editor] Check 2FA failed: {error}"); Repaint(); });
            }
            EditorGUILayout.LabelField(TwoFactorStatusLabel(gamerProgress), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!gamerProgress.RequiresTwoFactorToClear))
                this.twoFactorCode = EditorGUILayout.TextField(new GUIContent("2FA Code", "Authenticator code, required to clear progress when the account has 2FA enabled."), this.twoFactorCode);
            using (new EditorGUI.DisabledScope(!gamerProgress.CanClearProgress(this.twoFactorCode)))
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Progress", GUILayout.Height(25)))
                {
                    string code = gamerProgress.RequiresTwoFactorToClear ? this.twoFactorCode : null;
                    this.twoFactorCode = string.Empty;
                    gamerProgress.ClearProgress(code);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private static string TwoFactorStatusLabel(GamerProgress gamerProgress)
        {
            if (!gamerProgress.IsDataLoaded) return "load progress first";
            if (!gamerProgress.IsTwoFactorStatusChecked) return "2fa status not checked";
            return gamerProgress.RequiresTwoFactorToClear ? "2fa code required" : "2fa code not required";
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