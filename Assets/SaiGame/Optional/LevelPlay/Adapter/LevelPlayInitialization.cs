#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    internal static class LevelPlayInitialization
    {
        private static bool initialized;
        private static bool initializing;
        private static string activeAppKey;

        private static event Action<string> Completed;

        internal static void Request(string appKey, Action<string> callback)
        {
            if (activeAppKey != null && activeAppKey != appKey)
            {
                callback("Use the same LevelPlay App Key for all ad formats.");
                return;
            }

            if (initialized)
            {
                callback(null);
                return;
            }

            Completed += callback;
            if (initializing)
                return;

            initializing = true;
            activeAppKey = appKey;
            LevelPlay.OnInitSuccess += HandleSuccess;
            LevelPlay.OnInitFailed += HandleFailure;
            LevelPlay.Init(appKey);
        }

        internal static void Cancel(Action<string> callback) => Completed -= callback;

        private static void HandleSuccess(LevelPlayConfiguration configuration)
        {
            initialized = true;
            Finish(null);
        }

        private static void HandleFailure(LevelPlayInitError error)
        {
            activeAppKey = null;
            Finish(error.ErrorMessage ?? "Unknown LevelPlay initialization error.");
        }

        private static void Finish(string error)
        {
            initializing = false;
            LevelPlay.OnInitSuccess -= HandleSuccess;
            LevelPlay.OnInitFailed -= HandleFailure;
            var callbacks = Completed;
            Completed = null;
            callbacks?.Invoke(error);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            initialized = false;
            initializing = false;
            activeAppKey = null;
            Completed = null;
        }
    }
}
#endif
