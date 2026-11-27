#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

namespace SaiGame.LevelPlaySample
{
    internal sealed class SaiServerFallbackBannerBuildProcessor : BuildPlayerProcessor
    {
        private const string DisableAdsDefine = "SAIGAME_DISABLE_ADS";

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            if (AdsDisabled(context.BuildPlayerOptions))
                return;

            string folder = AssetDatabase.GUIDToAssetPath(SaiServerFallbackBanner.ArtworkFolderGuid);
            if (!Directory.Exists(folder))
                return;

            foreach (string path in Directory.EnumerateFiles(folder, "*.png"))
            {
                string destination = $"{SaiServerFallbackBanner.ArtworkFolder}/{Path.GetFileName(path)}";
                context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(path), destination);
            }
        }

        private static bool AdsDisabled(BuildPlayerOptions options)
        {
            if (options.extraScriptingDefines?.Contains(DisableAdsDefine) == true)
                return true;

#if UNITY_6000_0_OR_NEWER
            var profile = BuildProfile.GetActiveBuildProfile();
            if (options.target == EditorUserBuildSettings.activeBuildTarget &&
                profile != null && profile.scriptingDefines.Contains(DisableAdsDefine))
                return true;
#endif

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(options.target);
            bool isServer = group == BuildTargetGroup.Standalone &&
                            options.subtarget == (int)StandaloneBuildSubtarget.Server;
            NamedBuildTarget target = isServer ? NamedBuildTarget.Server : NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            return defines.Contains(DisableAdsDefine);
        }
    }
}
#endif
