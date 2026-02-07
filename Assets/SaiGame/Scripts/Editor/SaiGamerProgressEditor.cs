using UnityEngine;
using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiGamerProgress))]
    public class SaiGamerProgressEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SaiGamerProgress gamerProgress = (SaiGamerProgress)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Progress Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Create Progress", GUILayout.Height(30)))
            {
                gamerProgress.CreateProgress(
                    progress => Debug.Log($"Progress created! ID: {progress.id}, Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"),
                    error => Debug.LogError($"Create progress failed: {error}")
                );
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Get Progress", GUILayout.Height(30)))
            {
                gamerProgress.GetProgress(
                    progress => Debug.Log($"Progress retrieved! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}, Game Data: {progress.game_data}"),
                    error => Debug.LogError($"Get progress failed: {error}")
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
                        progress => Debug.Log($"Progress updated! Level: {progress.level}, XP: {progress.experience}, Gold: {progress.gold}"),
                        error => Debug.LogError($"Update progress failed: {error}")
                    );
                }
                else
                {
                    Debug.LogWarning("No progress found! Create progress first.");
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