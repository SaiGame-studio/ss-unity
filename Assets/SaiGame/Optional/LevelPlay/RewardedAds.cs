using System;
using SaiGame.Services;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    [DisallowMultipleComponent]
    public sealed class RewardedAds : SaiSingleton<RewardedAds>
    {
        [SerializeField, Tooltip("LevelPlay App Key for the Android app.")]
        private string androidAppKey;

        [SerializeField, Tooltip("Rewarded Ad Unit ID for the Android app.")]
        private string androidAdUnitId;

        [SerializeField, Tooltip("LevelPlay App Key for the iOS app.")]
        private string iosAppKey;

        [SerializeField, Tooltip("Rewarded Ad Unit ID for the iOS app.")]
        private string iosAdUnitId;

        private readonly RewardedAdSession session = new RewardedAdSession();

        public string Status => session.Status;
        public bool IsAvailable => session.IsAvailable;
        public bool IsReady => isActiveAndEnabled && session.IsReady;

        public event Action<string, int> RewardEarned
        {
            add => session.RewardEarned += value;
            remove => session.RewardEarned -= value;
        }

        public event Action AdClosed
        {
            add => session.AdClosed += value;
            remove => session.AdClosed -= value;
        }

        // Call after the game's privacy and consent setup is complete.
        public void Load()
        {
            if (!isActiveAndEnabled || !IsAvailable)
                return;

#if UNITY_ANDROID
            session.Load(androidAppKey, androidAdUnitId);
#elif UNITY_IOS
            session.Load(iosAppKey, iosAdUnitId);
#endif
        }

        public bool TryShow(string placementName = null)
        {
            return IsReady && session.TryShow(placementName);
        }

        protected override void OnDestroy()
        {
            session.Dispose();
            base.OnDestroy();
        }
    }
}
