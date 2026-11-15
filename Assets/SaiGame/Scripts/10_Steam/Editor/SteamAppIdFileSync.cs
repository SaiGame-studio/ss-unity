using System;
using System.IO;
using UnityEngine;

namespace SaiGame.Services
{
    // Keeps steam_appid.txt in the project root equal to the Steam AppID the Sai
    // Server has for the game, so nobody edits it by hand. SteamAPI reads this file
    // when the game is not launched by Steam (Editor testing). Steamworks.NET
    // recreates it with AppID 480 when it is missing, so it cannot be removed.
    public static class SteamAppIdFileSync
    {
        private const string FileName = "steam_appid.txt";

#if STEAMWORKS_NET || SAI_STEAMWORKS_NET
        // After each script reload (project open, compile, entering Play Mode),
        // read the AppID once so the file is in sync without opening an Inspector.
        [UnityEditor.InitializeOnLoadMethod]
        private static void SyncOnLoad()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                SaiServer server = UnityEngine.Object.FindFirstObjectByType<SaiServer>();
                if (server != null) PublicGameInfoLookup.Update(server);
            };
        }
#endif

        public static void Sync(long appId)
        {
#if STEAMWORKS_NET || SAI_STEAMWORKS_NET
            if (appId <= 0 || appId > uint.MaxValue) return;
            string path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            string wanted = appId.ToString();
            try
            {
                string current = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
                if (current == wanted) return;
                File.WriteAllText(path, wanted);
                Debug.Log($"[SteamLink] {FileName} set to {wanted} from the Sai Server (was {(current.Length > 0 ? current : "missing")}). " +
                          "Restart the Editor if SteamAPI already started in this session.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SteamLink] Could not update {FileName}: {exception.Message}");
            }
#endif
        }
    }
}
