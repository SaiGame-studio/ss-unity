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
                DrawFormatField(serializedObject.FindProperty("format"));
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

            if (ads.Format == SaiAdFormat.Banner)
                DrawBanner(ads);
        }

        private static void DrawBanner(UnityAds ads)
        {
            EditorGUILayout.LabelField("Banner", EditorStyles.boldLabel);
            string bannerStatus = ads.HasBannerConfiguration
                ? Application.isPlaying ? ads.BannerStatus : "Enter Play Mode and select Show banner to preview."
                : "Set the App Key and Banner Ad Unit ID for the active build target.";
            EditorGUILayout.HelpBox(bannerStatus, ads.IsBannerAvailable ? MessageType.Info : MessageType.Warning);
            if (!Application.isPlaying)
                return;

            using (new EditorGUI.DisabledScope(!ads.IsBannerAvailable))
            {
                if (GUILayout.Button("Show banner"))
                    ads.ShowBanner();
            }

            if (GUILayout.Button("Hide banner"))
                ads.HideBanner();
        }

        private static void DrawFormatField(SerializedProperty property)
        {
            var current = (SaiAdFormat)property.intValue;
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Format", "Ad format this component shows."));
            if (!EditorGUI.DropdownButton(rect, new GUIContent(GetFormatLabel(current)), FocusType.Keyboard))
                return;

            var menu = new GenericMenu();
            var serializedObject = property.serializedObject;
            string path = property.propertyPath;
            foreach (SaiAdFormat value in System.Enum.GetValues(typeof(SaiAdFormat)))
            {
                var label = new GUIContent(GetFormatLabel(value));
                if (!UnityAds.IsFormatSupported(value))
                {
                    menu.AddDisabledItem(label, value == current);
                    continue;
                }

                menu.AddItem(label, value == current, () =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty(path).intValue = (int)value;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.DropDown(rect);
        }

        private static string GetFormatLabel(SaiAdFormat value)
        {
            return UnityAds.IsFormatSupported(value) ? value.ToString() : $"{value} (coming soon)";
        }

        private void DrawPlatformFields(string label, string prefix)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "AppKey"), new GUIContent("App Key"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "BannerAdUnitId"), new GUIContent("Banner Ad Unit ID"));
        }

        private static string GetBlocker(UnityAds ads, PackageInfo package)
        {
            if (!ads.isActiveAndEnabled || (ads.Manager != null && !ads.Manager.isActiveAndEnabled))
                return "Ads are disabled on this object.";
            if (package == null)
                return "Ads unavailable. Install Ads Mediation to enable LevelPlay.";
            if (!BannerAdSession.HasCompatiblePackage)
                return "Ads unavailable. Install Ads Mediation 9.6 or later in the 9.x series.";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                return "Ads unavailable on this build target. Select Android or iOS.";
            return null;
        }
    }
}
