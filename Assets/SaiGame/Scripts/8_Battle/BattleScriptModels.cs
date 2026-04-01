using System;
using UnityEngine;

namespace SaiGame.Services
{
    [Serializable]
    public class BattleScriptPayload
    {
        // Add payload fields here as needed
    }

    [Serializable]
    public class BattleScriptRequest
    {
        public BattleScriptPayload payload;
    }

    [Serializable]
    public class BattleScriptResponse
    {
        public string raw; // stores raw JSON string from API
    }
}
