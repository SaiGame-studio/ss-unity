namespace SaiGame.Services
{
    /// <summary>
    /// Server endpoint selectable on SaiServer. Only production is allowed;
    /// use SERVER_ENDPOINT in .env to point the Editor at another server.
    /// The explicit value keeps previously serialized data compatible.
    /// </summary>
    public enum ServerEndpointOption
    {
        ProductionHttps = 1
    }
}
