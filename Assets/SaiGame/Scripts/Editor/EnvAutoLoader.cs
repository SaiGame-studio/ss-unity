using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// When "Load .env on Play" is checked, applies .env values at game start
    /// (after all Awake calls, before any Start calls) using RuntimeInitializeOnLoadMethod.
    /// The checkbox flag lives in EditorPrefs — machine-local, never committed to git.
    /// </summary>
    public static class EnvAutoLoader
    {
        private const string PREF_KEY = "SaiServer.loadEnvOnPlay";

        public static bool LoadEnvOnPlay
        {
            get => EditorPrefs.GetBool(PREF_KEY, false);
            set => EditorPrefs.SetBool(PREF_KEY, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            if (!LoadEnvOnPlay) return;

            if (!EnvLoader.EnvFileExists())
            {
                Debug.LogWarning("[EnvLoader] .env file not found — skipping auto-load.");
                return;
            }

            SaiServer saiServer = Object.FindFirstObjectByType<SaiServer>();
            if (saiServer == null)
            {
                Debug.LogWarning("[EnvLoader] SaiServer not found in scene — skipping auto-load.");
                return;
            }

            int applied = EnvLoader.ApplyAtRuntime(saiServer);
            Debug.Log($"[EnvLoader] Applied {applied} value(s) from .env at game start.");
        }
    }
}
