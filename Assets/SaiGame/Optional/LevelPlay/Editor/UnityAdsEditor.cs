using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SaiGame.LevelPlaySample
{
    [CustomEditor(typeof(UnityAds), true)]
    public sealed class UnityAdsEditor : Editor
    {
        private const string LevelPlayPackagePath = "Packages/com.unity.services.levelplay/package.json";

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var ads = (UnityAds)target;
            serializedObject.Update();
            if (ads.Manager != null)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("format"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("androidUnitId"), new GUIContent("Android Ad Unit ID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("iosUnitId"), new GUIContent("iOS Ad Unit ID"));
            }
            else
            {
                DrawPlatformFields("Android", "android");
                DrawPlatformFields("iOS", "ios");
            }
            serializedObject.ApplyModifiedProperties();

            var package = PackageInfo.FindForAssetPath(LevelPlayPackagePath);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LevelPlay", package == null ? "Not installed" : $"Installed ({package.version})");
            EditorGUILayout.LabelField("Build target", EditorUserBuildSettings.activeBuildTarget.ToString());
            string blocker = GetBlocker(ads, package);
            if (blocker != null)
            {
                EditorGUILayout.HelpBox(blocker, MessageType.Warning);
                return;
            }

            if (ads.Manager == null || ads.Format == SaiAdFormat.Banner)
                DrawBanner(ads);

            if (ads.Manager == null || ads.Format == SaiAdFormat.Rewarded)
            {
                EditorGUILayout.LabelField("Rewarded ads", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(GetRewardedStatus(ads), ads.IsAvailable ? MessageType.Info : MessageType.Warning);
            }
        }

        private static void DrawBanner(UnityAds ads)
        {
            EditorGUILayout.LabelField("Banner", EditorStyles.boldLabel);
            string bannerStatus = ads.HasBannerConfiguration
                ? Application.isPlaying ? ads.BannerStatus : "Enter Play Mode and select Show banner to preview."
                : "Set the App Key and Banner Ad Unit ID for the active build target.";
            EditorGUILayout.HelpBox(bannerStatus, ads.IsBannerAvailable ? MessageType.Info : MessageType.Warning);
            if (Application.isPlaying)
            {
                using (new EditorGUI.DisabledScope(!ads.IsBannerAvailable))
                {
                    if (GUILayout.Button("Show banner"))
                        ads.ShowBanner();
                }

                if (GUILayout.Button("Hide banner"))
                    ads.HideBanner();
            }

        }

        private void DrawPlatformFields(string label, string prefix)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "AppKey"), new GUIContent("App Key"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "BannerAdUnitId"), new GUIContent("Banner Ad Unit ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "AdUnitId"), new GUIContent("Rewarded Ad Unit ID"));
        }

        private static string GetBlocker(UnityAds ads, PackageInfo package)
        {
            if (!ads.isActiveAndEnabled || (ads.Manager != null && !ads.Manager.isActiveAndEnabled))
                return "Ads are disabled on this object.";
            if (package == null)
                return "Ads unavailable. Install Ads Mediation to enable LevelPlay.";
            if (!RewardedAdSession.HasCompatiblePackage)
                return "Ads unavailable. Install Ads Mediation 9.6 or later in the 9.x series.";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                return "Ads unavailable on this build target. Select Android or iOS.";
            return null;
        }

        private static string GetRewardedStatus(UnityAds ads)
        {
            if (!ads.HasConfiguration)
                return "Ads unavailable. Set the App Key and Rewarded Ad Unit ID for the active build target.";
            if (!Application.isPlaying)
                return "Ads can be loaded in Play Mode or on a device. Call Load after the game's consent setup.";
            if (ads.IsReady)
                return "Ad loaded and ready to show.";
            return ads.Status;
        }
    }
}
