using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the response returned by the generators endpoint.
    /// Contains an array of all generators owned by the player.
    /// </summary>
    [Serializable]
    public class GeneratorsResponse
    {
        public GeneratorData[] generators;
    }
}
