using System;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to work with custom game_data
    /// </summary>
    [Serializable]
    public class CustomGameData
    {
        public string[] achievements;
        public string current_quest;
        
        public static CustomGameData FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<CustomGameData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse game_data: {e.Message}");
                return null;
            }
        }
        
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    /// <summary>
    /// Example usage of custom game_data
    /// </summary>
    public class GameDataExample : MonoBehaviour
    {
        [SerializeField] private SaiGamerProgress gamerProgress;

        private void Example_ReadGameData()
        {
            if (gamerProgress.CurrentProgress != null)
            {
                string gameDataJson = gamerProgress.CurrentProgress.game_data;
                CustomGameData customData = CustomGameData.FromJson(gameDataJson);
                
                if (customData != null)
                {
                    Debug.Log($"Current Quest: {customData.current_quest}");
                    Debug.Log($"Achievements: {string.Join(", ", customData.achievements)}");
                }
            }
        }

        private void Example_WriteGameData()
        {
            CustomGameData customData = new CustomGameData
            {
                achievements = new[] { "first_login", "level_10", "defeat_boss" },
                current_quest = "explore_dungeon"
            };

            string gameDataJson = customData.ToJson();
            gamerProgress.SetGameData(gameDataJson);
        }

        private void Example_UpdateProgressWithGameData()
        {
            if (gamerProgress.CurrentProgress != null)
            {
                CustomGameData customData = new CustomGameData
                {
                    achievements = new[] { "first_login", "level_20" },
                    current_quest = "final_boss"
                };

                gamerProgress.UpdateProgress(
                    experienceDelta: 500,
                    goldDelta: 100,
                    newGameData: customData.ToJson(),
                    onSuccess: progress => Debug.Log("Progress updated with custom game data!"),
                    onError: error => Debug.LogError($"Update failed: {error}")
                );
            }
        }
    }
}