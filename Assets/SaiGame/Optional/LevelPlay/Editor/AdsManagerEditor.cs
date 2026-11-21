using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SaiGame.LevelPlaySample
{
    [CustomEditor(typeof(AdsManager))]
    public sealed class AdsManagerEditor : Editor
    {
        private const string AdsProperty = "ads";
        private const string ScriptProperty = "m_Script";

        private List<InstalledAdNetwork> networks = new List<InstalledAdNetwork>();
        private string adMobAndroidAppId = string.Empty;
        private string adMobIosAppId = string.Empty;
        private bool showNetworks = true;
        private GUIStyle disabledLabelStyle;
        // Only one turned-on network shows its setup steps at a time.
        private string expandedNetwork;

        // Built-in editor icon shown next to recommended networks.
        private const string RecommendedIconName = "Favorite";
        // Built-in editor icon shown next to a network with a crash risk.
        private const string DangerIconName = "console.erroricon.sml";

        private const string AdMobAppIdTooltip =
            "From AdMob > Apps > App settings. Saved to the LevelPlay Mediated Network Settings and added to the build.";

        private static readonly Color TurnOffColor = new Color(1f, 0.55f, 0.55f);
        private static readonly Color TurnOnColor = new Color(0.55f, 1f, 0.6f);

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        private void OnEnable()
        {
            var manager = (AdsManager)target;
            if (!Application.isPlaying && !EditorUtility.IsPersistent(manager))
                SyncAds(manager);
            RefreshNetworks();
        }

        private void RefreshNetworks()
        {
            networks = InstalledAdNetworkScanner.Scan();
            InstalledAdNetworkScanner.ReadAdMobAppIds(out adMobAndroidAppId, out adMobIosAppId);
        }

        private void DrawNetworks()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = new GUIContent($"Installed ad networks ({networks.Count})");
                float width = EditorStyles.foldout.CalcSize(title).x + 4f;
                var rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
                showNetworks = EditorGUI.Foldout(rect, showNetworks, title, true);
                GUILayout.FlexibleSpace();
                var tooltip = "Open LevelPlay Setup > Networks to choose which networks serve ads.";
                if (GUILayout.Button(new GUIContent("Setup networks", tooltip), GUILayout.Width(140)))
                    LevelPlayDashboardLinks.OpenNetworks();
            }

            if (!showNetworks)
                return;

            if (networks.Count == 0)
                EditorGUILayout.LabelField("No adapters installed. LevelPlay serves ironSource ads only.");

            if (disabledLabelStyle == null)
                disabledLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };
            var recommendedIcon = EditorGUIUtility.IconContent(RecommendedIconName);
            recommendedIcon.tooltip = "Recommended";

            foreach (var network in networks)
            {
                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    // Name takes only its own width so the icons sit right after it.
                    if (network.Enabled)
                    {
                        bool expanded = expandedNetwork == network.Name;
                        var title = new GUIContent(network.Name);
                        float width = EditorStyles.foldout.CalcSize(title).x + 4f;
                        var rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(width));
                        // Opening one network closes the one that was open.
                        if (EditorGUI.Foldout(rect, expanded, title, true) != expanded)
                            expandedNetwork = expanded ? null : network.Name;
                    }
                    else
                    {
                        var title = new GUIContent($"{network.Name} (off)");
                        GUILayout.Label(title, disabledLabelStyle,
                            GUILayout.Width(disabledLabelStyle.CalcSize(title).x));
                    }

                    if (InstalledAdNetworkScanner.IsRecommended(network))
                        GUILayout.Label(recommendedIcon, GUILayout.Width(20), GUILayout.Height(18));
                    if (HasCrashRisk(network))
                    {
                        var danger = new GUIContent(EditorGUIUtility.IconContent(DangerIconName).image,
                            AdNetworkSetupCatalog.Get(network.Name).CrashWarning);
                        GUILayout.Label(danger, GUILayout.Width(20), GUILayout.Height(18));
                    }

                    GUILayout.FlexibleSpace();
                    var previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = network.Enabled ? TurnOffColor : TurnOnColor;
                    bool clicked = GUILayout.Button(network.Enabled ? "Turn off" : "Turn on", GUILayout.Width(70));
                    GUI.backgroundColor = previousColor;
                    if (clicked)
                    {
                        var selected = network;
                        RunAfterGui(() =>
                        {
                            if (selected.Enabled)
                                InstalledAdNetworkScanner.TurnOff(selected);
                            else
                                InstalledAdNetworkScanner.TurnOn(selected);
                        });
                    }
                }

                if (!network.Enabled)
                    continue;

                if (expandedNetwork != network.Name)
                    continue;

                if (HasCrashRisk(network))
                    using (new EditorGUI.IndentLevelScope())
                        EditorGUILayout.HelpBox(AdNetworkSetupCatalog.Get(network.Name).CrashWarning, MessageType.Error);

                DrawSetupInfo(network);
                if (network.Name == InstalledAdNetworkScanner.AdMobNetworkName)
                    DrawAdMobSettings();
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var tooltip = "Keep only " + string.Join(", ", InstalledAdNetworkScanner.RecommendedNetworks) +
                    ". These need no extra App ID setup in Unity, so builds have the fewest errors.";
                if (GUILayout.Button(new GUIContent("Apply recommended", tooltip)))
                {
                    var snapshot = networks;
                    RunAfterGui(() => InstalledAdNetworkScanner.ApplyRecommended(snapshot));
                }
            }

            if (GUILayout.Button("Refresh ad networks"))
                RefreshNetworks();
        }

        private void DrawSetupInfo(InstalledAdNetwork network)
        {
            var info = AdNetworkSetupCatalog.Get(network.Name);
            var appKey = ((AdsManager)target).AppKey;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField($"{network.Name} needs setup before it shows ads:", EditorStyles.boldLabel);

                if (info.DashboardUrl != null)
                    DrawSetupStep(1, $"Copy {info.RequiredIds}.", GetIdsButtonLabel(network.Name, info.DashboardUrl), info.DashboardUrl,
                        () => Application.OpenURL(info.DashboardUrl));
                else if (info.RequiredIds != null)
                    DrawSetupStep(1, $"Copy {info.RequiredIds} from the {network.Name} dashboard.", null, null, null);
                else
                    DrawSetupStep(1, $"Ask your LevelPlay account manager for the {network.Name} IDs.", null, null, null);

                DrawSetupStep(2, $"In LevelPlay, open this app's networks, add {network.Name} and paste the IDs.",
                    "Open networks", "LevelPlay Setup > Networks",
                    LevelPlayDashboardLinks.OpenNetworks);

                DrawSetupStep(3, $"Turn {network.Name} on for the Banner ad unit.",
                    "Open ad units", "LevelPlay Ad units page of the App Key above",
                    string.IsNullOrWhiteSpace(appKey) ? null : (System.Action)(() => LevelPlayDashboardLinks.OpenAdUnits(appKey)));

                if (network.Name == InstalledAdNetworkScanner.AdMobNetworkName)
                    DrawSetupStep(4, "Enter both AdMob App IDs below.", null, null, null);

                if (AdNetworkSetupCatalog.SupportsAutoSetup(network.Name))
                    EditorGUILayout.LabelField("LevelPlay can do steps 1 and 2 automatically for this network.",
                        EditorStyles.wordWrappedMiniLabel);
            }
        }

        // Networks without a self-serve dashboard hand out IDs by email.
        private static string GetIdsButtonLabel(string networkName, string url)
        {
            return url.StartsWith("mailto:", System.StringComparison.OrdinalIgnoreCase)
                ? $"Email {networkName}"
                : $"Open {networkName} IDs";
        }

        // A null button label draws the step without a button; a null action disables the button.
        private static void DrawSetupStep(int number, string text, string buttonLabel, string tooltip, System.Action action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = number > 0 ? $"{number}. {text}" : text;
                EditorGUILayout.LabelField(label, EditorStyles.wordWrappedLabel);
                if (buttonLabel == null)
                    return;

                using (new EditorGUI.DisabledScope(action == null))
                {
                    if (GUILayout.Button(new GUIContent(buttonLabel, tooltip), GUILayout.Width(140)))
                        action();
                }
            }
        }

        private bool HasCrashRisk(InstalledAdNetwork network)
        {
            return network.Enabled &&
                !string.IsNullOrEmpty(AdNetworkSetupCatalog.Get(network.Name).CrashWarning) &&
                !IsCrashRiskResolved(network);
        }

        // AdMob is the only network whose missing Unity setting crashes the app.
        private bool IsCrashRiskResolved(InstalledAdNetwork network)
        {
            return network.Name != InstalledAdNetworkScanner.AdMobNetworkName ||
                (!string.IsNullOrWhiteSpace(adMobAndroidAppId) && !string.IsNullOrWhiteSpace(adMobIosAppId));
        }

        private void DrawAdMobSettings()
        {
            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var android = EditorGUILayout.DelayedTextField(
                    new GUIContent("AdMob Android App ID", AdMobAppIdTooltip), adMobAndroidAppId);
                var ios = EditorGUILayout.DelayedTextField(
                    new GUIContent("AdMob iOS App ID", AdMobAppIdTooltip), adMobIosAppId);

                if ((android != adMobAndroidAppId || ios != adMobIosAppId) &&
                    InstalledAdNetworkScanner.WriteAdMobAppIds(android, ios))
                    InstalledAdNetworkScanner.ReadAdMobAppIds(out adMobAndroidAppId, out adMobIosAppId);

                if (!InstalledAdNetworkScanner.IsValidAdMobAppId(adMobAndroidAppId) ||
                    !InstalledAdNetworkScanner.IsValidAdMobAppId(adMobIosAppId))
                    EditorGUILayout.HelpBox(
                        "An AdMob App ID looks like ca-app-pub-1234567890123456~1234567890. " +
                        "Use the App ID, not an Ad Unit ID (which has a slash).",
                        MessageType.Warning);
            }
        }

        // Asset changes inside OnInspectorGUI break the layout, so they run after this GUI pass.
        private void RunAfterGui(System.Action action)
        {
            EditorApplication.delayCall += () =>
            {
                action();
                if (this == null)
                    return;

                RefreshNetworks();
                Repaint();
            };
            GUIUtility.ExitGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, ScriptProperty, AdsProperty);
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button(new GUIContent("Get App Key", LevelPlayDashboardLinks.AppKeyTooltip)))
                LevelPlayDashboardLinks.OpenAppKeys();

            var manager = (AdsManager)target;
            var package = PackageInfo.FindForAssetPath("Packages/com.unity.services.levelplay/package.json");
            EditorGUILayout.LabelField("LevelPlay", package == null ? "Not installed" : $"Installed ({package.version})");
            EditorGUILayout.LabelField("Build target", EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!BannerAdSession.HasCompatiblePackage)
                EditorGUILayout.HelpBox("Install a supported Ads Mediation 9.x package to enable ads.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(manager.AppKey))
                EditorGUILayout.HelpBox("Select Android or iOS and enter the App Key for that platform.", MessageType.Info);

            DrawAds(manager);

            using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(manager)))
            {
                if (GUILayout.Button("Create UnityAds"))
                    CreateAd(manager);
            }

            DrawNetworks();
        }

        private static void DrawAds(AdsManager manager)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ads", EditorStyles.boldLabel);
            if (manager.Ads.Count == 0)
            {
                EditorGUILayout.HelpBox("No ads yet. Click Create UnityAds to add one.", MessageType.Info);
                return;
            }

            foreach (var ad in manager.Ads)
            {
                if (ad == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(ad, typeof(UnityAds), true);

                    EditorGUILayout.LabelField(ad.Format.ToString(), GUILayout.Width(70));

                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button(GetShowHideLabel(ad), GUILayout.Width(60)))
                            ad.ShowHide();
                    }
                }
            }
        }

        private static string GetShowHideLabel(UnityAds ad)
        {
            return ad.Format == SaiAdFormat.Banner && ad.IsBannerVisible ? "Hide" : "Show";
        }

        private static void SyncAds(AdsManager manager)
        {
            var serialized = new SerializedObject(manager);
            var list = serialized.FindProperty(AdsProperty);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    list.DeleteArrayElementAtIndex(i);
            }

            foreach (Transform child in manager.transform)
            {
                if (child.TryGetComponent<UnityAds>(out var ad) && !Contains(list, ad))
                    Append(list, ad);
            }

            serialized.ApplyModifiedProperties();
        }

        private static bool Contains(SerializedProperty list, Object item)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                    return true;
            }

            return false;
        }

        private static void Append(SerializedProperty list, Object item)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        }

        private static void CreateAd(AdsManager manager)
        {
            var name = GameObjectUtility.GetUniqueNameForSibling(manager.transform, nameof(UnityAds));
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create UnityAds");
            Undo.SetTransformParent(child.transform, manager.transform, false, "Parent UnityAds");
            var ad = Undo.AddComponent<UnityAds>(child);

            var serialized = new SerializedObject(manager);
            Append(serialized.FindProperty(AdsProperty), ad);
            serialized.ApplyModifiedProperties();
            Selection.activeGameObject = child;
        }
    }
}
