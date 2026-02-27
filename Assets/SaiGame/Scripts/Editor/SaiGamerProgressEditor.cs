using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GamerProgress))]
    public class SaiGamerProgressEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

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

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Progress", GUILayout.Height(20)))
            {
                gamerProgress.ClearProgress();
            }
            GUI.backgroundColor = Color.white;
        }
    }
}