namespace SaiGame.LevelPlaySample
{
    // What a network needs before it serves ads through LevelPlay, and where to get it.
    public sealed class AdNetworkSetupInfo
    {
        public AdNetworkSetupInfo(string requiredIds, string crashWarning, string dashboardUrl)
        {
            RequiredIds = requiredIds;
            CrashWarning = crashWarning;
            DashboardUrl = dashboardUrl;
        }

        // IDs copied from the network's dashboard into the LevelPlay dashboard. Null when unknown.
        public string RequiredIds { get; }
        // Set only when a missing setup step crashes the app.
        public string CrashWarning { get; }
        // Direct link to the page that shows the IDs. Null when no such link is known.
        public string DashboardUrl { get; }
    }
}
