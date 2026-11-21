using SaiGame.Services;
using UnityEditor;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    // Connects the Ads module to SaiServer: lists the AdsManager under Service References and
    // joins Full Reset, which clears every AdsManager and deletes the UnityAds objects it created.
    [InitializeOnLoad]
    public static class AdsManagerFullReset
    {
        private const string AndroidAppKeyField = "androidAppKey";
        private const string IosAppKeyField = "iosAppKey";
        private const string AdsField = "ads";

        static AdsManagerFullReset()
        {
            SaiServerEditor.FullResetting -= Reset;
            SaiServerEditor.FullResetting += Reset;
            SaiServerEditor.ServiceReferencesDrawing -= DrawReference;
            SaiServerEditor.ServiceReferencesDrawing += DrawReference;
        }

        private static void DrawReference(SaiServer server)
        {
            var manager = server.GetComponentInChildren<AdsManager>(true);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Ads Child Object", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("Ads Manager", "Found under SaiServer; reset by Full Reset."),
                    manager, typeof(AdsManager), true);
        }

        private static void Reset(SaiServer server)
        {
            foreach (var manager in server.GetComponentsInChildren<AdsManager>(true))
            {
                DeleteAds(manager);
                ClearSettings(manager);
            }
        }

        private static void DeleteAds(AdsManager manager)
        {
            foreach (var ad in manager.GetComponentsInChildren<UnityAds>(true))
            {
                if (ad == null || ad.gameObject == manager.gameObject)
                    continue;

                // Objects that come from a prefab cannot be deleted from a prefab instance.
                if (PrefabUtility.GetCorrespondingObjectFromSource(ad.gameObject) != null)
                {
                    Debug.LogWarning($"[AdsManager] '{ad.name}' is part of a prefab and was not deleted. Open the prefab and delete it there.", ad);
                    continue;
                }

                Undo.DestroyObjectImmediate(ad.gameObject);
            }
        }

        private static void ClearSettings(AdsManager manager)
        {
            var settings = new SerializedObject(manager);
            settings.FindProperty(AndroidAppKeyField).stringValue = string.Empty;
            settings.FindProperty(IosAppKeyField).stringValue = string.Empty;
            settings.FindProperty(AdsField).ClearArray();
            settings.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }
    }
}
