using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    // Reads, turns off and turns on the adapters installed by the LevelPlay Network Manager.
    // A turned-off adapter file is moved out of Assets so it is not built, and can be moved back.
    // Installed adapters are not the same as networks enabled on the LevelPlay dashboard.
    public static class InstalledAdNetworkScanner
    {
        public const string AdMobNetworkName = "AdMob";

        // Networks kept by "Apply recommended": they need no extra App ID setup in Unity.
        public static readonly string[] RecommendedNetworks = { "UnityAds" };

        // Listed right after the recommended networks, in this order; the rest follow by name.
        private static readonly string[] PinnedNetworks = { "AdMob", "Facebook" };

        private const string AdapterFilePrefix = "IS";
        private const string AdapterFileSuffix = "AdapterDependencies";
        private const string EnabledFolder = "Assets/LevelPlay/Editor";
        private const string DisabledFolder = "LevelPlayDisabledAdapters";
        private const string ResolverTypeName = "GooglePlayServices.PlayServicesResolver";
        private const string ResolverMethodName = "ResolveSync";
        private const string NetworkSettingsType = "LevelPlayMediationNetworkSettings";
        // Same path the LevelPlay package uses (Constants.k_LevelPlayResourcesPath + k_LevelPlayNetworkSettingName).
        private const string NetworkSettingsPath = "Assets/LevelPlay/Resources/LevelPlayMediatedNetworkSettings.asset";
        private static readonly Regex AdMobAppIdPattern = new Regex(@"^ca-app-pub-\d{16}~\d{10}$");
        private const string EnableAdMobField = "EnableAdmob";
        private const string AdMobAndroidAppIdField = "AdmobAndroidAppId";
        private const string AdMobIosAppIdField = "AdmobIOSAppId";

        public static List<InstalledAdNetwork> Scan()
        {
            var result = new List<InstalledAdNetwork>();
            foreach (var guid in AssetDatabase.FindAssets(AdapterFileSuffix))
                TryAdd(result, AssetDatabase.GUIDToAssetPath(guid), true);

            if (Directory.Exists(DisabledFolder))
            {
                foreach (var path in Directory.GetFiles(DisabledFolder, "*.xml"))
                    TryAdd(result, path.Replace('\\', '/'), false);
            }

            return result
                .OrderByDescending(IsRecommended)
                .ThenBy(PinnedRank)
                .ThenBy(network => network.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int PinnedRank(InstalledAdNetwork network)
        {
            int index = Array.IndexOf(PinnedNetworks, network.Name);
            return index < 0 ? PinnedNetworks.Length : index;
        }

        public static bool IsRecommended(InstalledAdNetwork network) => RecommendedNetworks.Contains(network.Name);

        public static void TurnOff(InstalledAdNetwork network)
        {
            if (MoveOut(network))
                AfterChange();
        }

        public static void TurnOn(InstalledAdNetwork network)
        {
            if (MoveIn(network))
                AfterChange();
        }

        public static void ApplyRecommended(IEnumerable<InstalledAdNetwork> networks)
        {
            bool changed = false;
            foreach (var network in networks)
            {
                if (IsRecommended(network) && !network.Enabled)
                    changed |= MoveIn(network);
                else if (!IsRecommended(network) && network.Enabled)
                    changed |= MoveOut(network);
            }

            if (changed)
                AfterChange();
        }

        // Empty when the AdMob integration is disabled or the ID is not set, because LevelPlay
        // only writes the App ID into the build when the integration is enabled.
        public static void ReadAdMobAppIds(out string androidAppId, out string iosAppId)
        {
            androidAppId = string.Empty;
            iosAppId = string.Empty;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkSettingsPath);
            if (asset == null)
                return;

            var settings = new SerializedObject(asset);
            var enabled = settings.FindProperty(EnableAdMobField);
            if (enabled == null || !enabled.boolValue)
                return;

            androidAppId = settings.FindProperty(AdMobAndroidAppIdField)?.stringValue ?? string.Empty;
            iosAppId = settings.FindProperty(AdMobIosAppIdField)?.stringValue ?? string.Empty;
        }

        // Saves the IDs into the LevelPlay settings asset and enables the AdMob integration,
        // so LevelPlay adds them to AndroidManifest.xml and Info.plist at build time.
        public static bool WriteAdMobAppIds(string androidAppId, string iosAppId)
        {
            var asset = LoadOrCreateNetworkSettings();
            if (asset == null)
            {
                Debug.LogError("[AdsManager] Could not create the LevelPlay network settings. Open Ads Mediation > Developer Settings > Mediated Network Settings once, then try again.");
                return false;
            }

            var settings = new SerializedObject(asset);
            var enabled = settings.FindProperty(EnableAdMobField);
            var android = settings.FindProperty(AdMobAndroidAppIdField);
            var ios = settings.FindProperty(AdMobIosAppIdField);
            if (enabled == null || android == null || ios == null)
            {
                Debug.LogError("[AdsManager] The LevelPlay network settings have an unexpected format. Set the AdMob App IDs in Ads Mediation > Developer Settings > Mediated Network Settings.");
                return false;
            }

            enabled.boolValue = true;
            android.stringValue = (androidAppId ?? string.Empty).Trim();
            ios.stringValue = (iosAppId ?? string.Empty).Trim();
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool IsValidAdMobAppId(string value)
        {
            return string.IsNullOrEmpty(value) || AdMobAppIdPattern.IsMatch(value.Trim());
        }

        private static ScriptableObject LoadOrCreateNetworkSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkSettingsPath);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance(NetworkSettingsType);
            if (asset == null)
                return null;

            Directory.CreateDirectory(Path.GetDirectoryName(NetworkSettingsPath));
            AssetDatabase.CreateAsset(asset, NetworkSettingsPath);
            return asset;
        }

        private static void TryAdd(List<InstalledAdNetwork> result, string path, bool enabled)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                !fileName.StartsWith(AdapterFilePrefix, StringComparison.Ordinal) ||
                !fileName.EndsWith(AdapterFileSuffix, StringComparison.Ordinal))
                return;

            var name = fileName.Substring(AdapterFilePrefix.Length,
                fileName.Length - AdapterFilePrefix.Length - AdapterFileSuffix.Length);
            if (name.Length == 0)
                return;

            result.Add(new InstalledAdNetwork(name, path, enabled));
        }

        private static bool MoveOut(InstalledAdNetwork network)
        {
            if (!network.Enabled)
                return false;

            Directory.CreateDirectory(DisabledFolder);
            var target = Path.Combine(DisabledFolder, Path.GetFileName(network.Path));
            File.Copy(network.Path, target, true);
            if (AssetDatabase.DeleteAsset(network.Path))
                return true;

            Debug.LogError($"[AdsManager] Could not turn off {network.Name}. Failed to remove '{network.Path}'.");
            return false;
        }

        private static bool MoveIn(InstalledAdNetwork network)
        {
            if (network.Enabled)
                return false;

            Directory.CreateDirectory(EnabledFolder);
            var target = $"{EnabledFolder}/{Path.GetFileName(network.Path)}";
            if (File.Exists(target))
            {
                Debug.LogError($"[AdsManager] Could not turn on {network.Name}. '{target}' already exists.");
                return false;
            }

            File.Move(network.Path, target);
            AssetDatabase.ImportAsset(target);
            return true;
        }

        // Re-runs the Android Resolver so Gradle dependencies match the turned-on adapters.
        // iOS pods are read from the adapter files at build time.
        private static void AfterChange()
        {
            AssetDatabase.Refresh();
            var resolver = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(ResolverTypeName, false))
                .FirstOrDefault(type => type != null);
            var resolve = resolver?.GetMethod(ResolverMethodName, BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            if (resolve == null)
            {
                Debug.LogWarning("[AdsManager] Android Resolver not found. Run Assets > Mobile Dependency Resolver > Android Resolver > Force Resolve.");
                return;
            }

            resolve.Invoke(null, new object[] { true });
        }
    }
}
