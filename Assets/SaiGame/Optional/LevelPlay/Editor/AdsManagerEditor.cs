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

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        private void OnEnable()
        {
            var manager = (AdsManager)target;
            if (!Application.isPlaying && !EditorUtility.IsPersistent(manager))
                SyncAds(manager);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, ScriptProperty, AdsProperty);
            serializedObject.ApplyModifiedProperties();

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
