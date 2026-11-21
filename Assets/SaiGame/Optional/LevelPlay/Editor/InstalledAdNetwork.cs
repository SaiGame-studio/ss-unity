namespace SaiGame.LevelPlaySample
{
    // An ad network adapter from the LevelPlay Network Manager, turned on (inside Assets) or off.
    public sealed class InstalledAdNetwork
    {
        public InstalledAdNetwork(string name, string path, bool enabled)
        {
            Name = name;
            Path = path;
            Enabled = enabled;
        }

        public string Name { get; }
        public string Path { get; }
        public bool Enabled { get; }
    }
}
