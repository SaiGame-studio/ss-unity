using System;

namespace SaiGame.Services
{
    [Serializable]
    public class CreateGamerProgressRequest
    {
        public string user_id;
        public string game_id;
        public int experience = 0;
        public int gold = 0;
        public string game_data = "{}";
    }

    [Serializable]
    public class CreateGamerProgressResponse
    {
        public GamerProgress data;
        public string message;
    }

    [Serializable]
    public class UpdateGamerProgressRequest
    {
        public int experience_delta;
        public int gold_delta;
        public string game_data;
    }
}