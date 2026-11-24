using System.Collections.Generic;

namespace SaiGame.LevelPlaySample
{
    // Setup requirements per network, taken from the LevelPlay Unity network guides.
    // DashboardUrl is the page closest to where the IDs are copied. Replace it with a deeper link
    // whenever one is known.
    // Required IDs come from docs.unity.com/en-us/grow/levelplay/sdk/unity/networks/guides/.
    // Keys match the adapter names shown in AdsManager.
    public static class AdNetworkSetupCatalog
    {
        private static readonly AdNetworkSetupInfo Unknown = new AdNetworkSetupInfo(null, null, null);

        private static readonly Dictionary<string, AdNetworkSetupInfo> Networks = new Dictionary<string, AdNetworkSetupInfo>
        {
            ["UnityAds"] = new AdNetworkSetupInfo(
                "Game ID and Placement ID (Monetization > Placements)",
                null,
                "https://cloud.unity.com/monetization"),
            ["AdMob"] = new AdNetworkSetupInfo(
                "App ID, Ad Unit ID (plus Google login for reporting)",
                "Enter both AdMob App IDs below, or the app crashes at launch.",
                "https://apps.admob.com/v2/apps/list"),
            ["AppLovin"] = new AdNetworkSetupInfo(
                "SDK Key, Report Key",
                null,
                "https://dash.applovin.com/o/account#keys"),
            ["APS"] = new AdNetworkSetupInfo(
                "APS App ID, slot UUID, format and size, price points",
                null,
                "https://ams.amazon.com/webpublisher/mdtb/mobile_apps#/apps/integration"),
            ["BidMachine"] = new AdNetworkSetupInfo(
                "Source ID",
                null,
                "https://bidmachine.io/#sign-up"),
            ["Bigo"] = new AdNetworkSetupInfo(
                "App ID, Ad Slot ID (bidding slot)",
                null,
                "https://www.bigossp.com/media/slot?pageNo=1&pageSize=10"),
            ["Chartboost"] = new AdNetworkSetupInfo(
                "App ID, App Signature, User ID, User Signature",
                null,
                "https://platform.chartboost.com/apps/overview"),
            ["Facebook"] = new AdNetworkSetupInfo(
                "App ID, Placement ID (plus Facebook login for reporting)",
                null,
                "https://business.facebook.com/pub/home"),
            ["Fyber"] = new AdNetworkSetupInfo(
                "Publisher ID, Consumer Key, Consumer Secret, App ID, Placement ID",
                null,
                "https://console.fyber.com/login"),
            ["HyprMX"] = new AdNetworkSetupInfo(
                "API Key, Distributor ID, Placement Name",
                null,
                "mailto:integration@hyprmx.com"),
            ["InMobi"] = new AdNetworkSetupInfo(
                "Account ID, API Key, Username, Password, Placement ID",
                null,
                "https://publisher.inmobi.com/my-inventory/app-and-placements"),
            ["Line"] = new AdNetworkSetupInfo(
                "App ID, Slot ID",
                null,
                null),
            ["Mintegral"] = new AdNetworkSetupInfo(
                "App Key, Secret, App ID, Placement ID, Ad Unit ID",
                null,
                "https://dev.mintegral.com/user/login"),
            ["MobileFuse"] = new AdNetworkSetupInfo(
                "Publisher ID, Placement IDs",
                null,
                "mailto:pubdev@mobilefuse.com"),
            ["Moloco"] = new AdNetworkSetupInfo(
                "Platform ID, Publisher ID, App Key, Ad Unit ID",
                null,
                "https://publisher.moloco.cloud/"),
            ["Ogury"] = new AdNetworkSetupInfo(
                "Asset Key, Ad Unit ID, Reporting API Token",
                null,
                "https://publishers.ogury.co/dashboard"),
            ["Pangle"] = new AdNetworkSetupInfo(
                "User ID, API Key, App ID, Placement ID",
                null,
                "https://www.pangleglobal.com/"),
            ["PubMatic"] = new AdNetworkSetupInfo(
                "Publisher ID, Profile ID, Ad Unit ID",
                null,
                "https://apps.pubmatic.com/login/publisher"),
            ["Smaato"] = new AdNetworkSetupInfo(
                "Publisher ID (Inventory), Client ID and Client Secret, Adspace ID",
                null,
                "https://spx.smaato.com/publisherportal/ui/index.html"),
            ["SuperAwesome"] = new AdNetworkSetupInfo(
                "App ID, Placement ID, App Key",
                null,
                "https://www.superawesome.com/"),
            ["Verve"] = new AdNetworkSetupInfo(
                "App Token, Zone Reference ID",
                null,
                "mailto:ironsourcebidding@verve.com"),
            ["Voodoo"] = Unknown,
            ["Vungle"] = new AdNetworkSetupInfo(
                "App ID, Placement Reference ID, Reporting API ID and Key",
                null,
                "https://app.vungle.com/login/"),
            ["YSO"] = new AdNetworkSetupInfo(
                "the IDs issued by YSO Network",
                null,
                "https://ysonetwork.com/"),
            ["Yandex"] = new AdNetworkSetupInfo(
                "App ID, Ad Unit ID",
                null,
                "https://partner.yandex.com/"),
        };

        // Networks the LevelPlay guides describe an automatic (or bidder) setup for.
        private static readonly HashSet<string> AutoSetupNetworks = new HashSet<string>
        {
            "UnityAds", "AppLovin", "Facebook", "Vungle"
        };

        public static bool SupportsAutoSetup(string networkName) => AutoSetupNetworks.Contains(networkName);

        internal static IEnumerable<string> NetworkNames => Networks.Keys;

        public static AdNetworkSetupInfo Get(string networkName)
        {
            return networkName != null && Networks.TryGetValue(networkName, out var info) ? info : Unknown;
        }
    }
}
