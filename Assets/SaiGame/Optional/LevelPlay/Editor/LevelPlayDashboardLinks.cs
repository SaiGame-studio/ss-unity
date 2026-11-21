using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    // LevelPlay dashboard pages where App Keys and Ad Unit IDs are copied from.
    public static class LevelPlayDashboardLinks
    {
        private const string AppsUrl = "https://platform.ironsrc.com/partners/next/mediation/apps?visibility=shown";
        private const string AdUnitsUrlFormat = "https://platform.ironsrc.com/partners/next/adUnits/{0}?visibility=show";

        public const string AppKeyTooltip =
            "Open the LevelPlay Apps page and copy the App Key of the game's app.";

        public const string UnitIdTooltip =
            "Open the LevelPlay Ad units page of this App Key and copy the Ad unit ID. " +
            "Enter the App Key first.";

        public static void OpenAppKeys() => Application.OpenURL(AppsUrl);

        public static void OpenAdUnits(string appKey)
        {
            if (string.IsNullOrWhiteSpace(appKey))
            {
                Debug.LogError("[UnityAds] Missing App Key for the active build target. Enter the App Key before opening Ad units.");
                return;
            }

            Application.OpenURL(string.Format(AdUnitsUrlFormat, System.Uri.EscapeDataString(appKey.Trim())));
        }
    }
}
