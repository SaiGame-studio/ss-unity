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
                if (GUILayout.Button(new GUIContent("Get App Key", LevelPlayDashboardLinks.AppKeyTooltip)))
                    LevelPlayDashboardLinks.OpenAppKeys();
            }

            if (GUILayout.Button(new GUIContent("Get Unit ID", LevelPlayDashboardLinks.UnitIdTooltip)))
                LevelPlayDashboardLinks.OpenAdUnits(ads.AppKey);

            bool layoutChanged = false;
            if (ads.Format == SaiAdFormat.Banner)
            {
                EditorGUI.BeginChangeCheck();
                DrawBannerLayout(serializedObject.FindProperty("bannerLayout"));
                layoutChanged = EditorGUI.EndChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableSaiServerBannerFallback"),
                    new GUIContent("Use Banner fallback", "Show the fallback banner when LevelPlay cannot provide an ad."));
            }
            serializedObject.ApplyModifiedProperties();

            if (layoutChanged && Application.isPlaying)
                ads.SetBannerLayout(ads.BannerLayout);

            var package = PackageInfo.FindForAssetPath(LevelPlayPackagePath);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LevelPlay", package == null ? "Not installed" : $"Installed ({package.version})");
            EditorGUILayout.LabelField("Build target", EditorUserBuildSettings.activeBuildTarget.ToString());
            string blocker = GetBlocker(ads, package);
            if (blocker != null)
                EditorGUILayout.HelpBox(blocker, MessageType.Warning);

            if (ads.Format == SaiAdFormat.Banner)
                DrawBanner(ads);
        }

        private static void DrawBanner(UnityAds ads)
        {
            EditorGUILayout.LabelField("Banner", EditorStyles.boldLabel);
            string bannerStatus = Application.isPlaying
                ? ads.BannerStatus
                : "Enter Play Mode to show the banner or test the fallback.";
            EditorGUILayout.HelpBox(bannerStatus, ads.IsBannerAvailable ? MessageType.Info : MessageType.Warning);
            if (!ads.HasBannerConfiguration)
                EditorGUILayout.HelpBox("Set the App Key and Banner Ad Unit ID to serve LevelPlay ads.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !ads.CanUseSaiServerFallback))
            {
                if (GUILayout.Button("Test fallback banner"))
                    ads.TestSaiServerFallbackBanner();
            }

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

        private static void DrawBannerLayout(SerializedProperty layout)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Banner layout", EditorStyles.boldLabel);

            var position = layout.FindPropertyRelative("position");
            EditorGUILayout.PropertyField(position);
            if ((SaiBannerPosition)position.intValue == SaiBannerPosition.Custom)
                EditorGUILayout.PropertyField(layout.FindPropertyRelative("customPosition"), new GUIContent("Offset from top left"));

            var size = layout.FindPropertyRelative("size");
            EditorGUILayout.PropertyField(size);
            switch ((SaiBannerSize)size.intValue)
            {
                case SaiBannerSize.Adaptive:
                    EditorGUILayout.PropertyField(layout.FindPropertyRelative("adaptiveWidth"));
                    break;
                case SaiBannerSize.Custom:
                    EditorGUILayout.PropertyField(layout.FindPropertyRelative("customWidth"));
                    EditorGUILayout.PropertyField(layout.FindPropertyRelative("customHeight"));
                    break;
            }

            EditorGUILayout.PropertyField(layout.FindPropertyRelative("respectSafeArea"));
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
                return "LevelPlay ads unavailable. Install Ads Mediation to enable LevelPlay.";
            if (!BannerAdSession.HasCompatiblePackage)
                return "LevelPlay ads unavailable. Install Ads Mediation 9.6 or later in the 9.x series.";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                return "LevelPlay ads unavailable on this build target. Select Android or iOS.";
            return null;
        }
    }
}
