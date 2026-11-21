using System;
#if SAIGAME_LEVELPLAY_AVAILABLE
using Unity.Services.LevelPlay;
#endif

namespace SaiGame.LevelPlaySample
{
    public sealed class RewardedAdSession : IDisposable
    {
#if !SAIGAME_LEVELPLAY_AVAILABLE
        private const string InitialStatus = "Install Ads Mediation 9.6 or later (9.x) to enable rewarded ads.";
#elif !UNITY_ANDROID && !UNITY_IOS
        private const string InitialStatus = "Rewarded ads require an Android or iOS build target.";
#else
        private const string InitialStatus = "Select Load ad to begin.";
#endif

        public string Status { get; private set; } = InitialStatus;

#if SAIGAME_LEVELPLAY_AVAILABLE
        public static bool HasCompatiblePackage => true;
#else
        public static bool HasCompatiblePackage => false;
#endif

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        public bool IsAvailable => true;
        public bool IsReady => !showing && !loading && ad?.IsAdReady() == true;
#else
        public bool IsAvailable => false;
        public bool IsReady => false;
#endif

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        public event Action<string, int> RewardEarned;
        public event Action AdClosed;
#else
        public event Action<string, int> RewardEarned { add { } remove { } }
        public event Action AdClosed { add { } remove { } }
#endif

        public void Load(string appKey, string unitId)
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            if (initializing || loading || showing || IsReady)
                return;

            if (ad != null)
            {
                LoadNextAd();
                return;
            }

            if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(unitId))
            {
                Status = "Enter the App Key and Rewarded Ad Unit ID for this platform.";
                return;
            }

            adUnitId = unitId.Trim();
            initializing = true;
            Status = "Initializing LevelPlay...";
            LevelPlayInitialization.Request(appKey.Trim(), HandleInitialization);
#endif
        }

        public bool TryShow(string placementName)
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            if (!IsReady)
                return false;

            if (!string.IsNullOrEmpty(placementName) && LevelPlayRewardedAd.IsPlacementCapped(placementName))
                return false;

            showing = true;
            Status = "Opening ad...";
            ad.ShowAd(placementName);
            return true;
#else
            return false;
#endif
        }

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        private LevelPlayRewardedAd ad;
        private string adUnitId;
        private bool initializing;
        private bool loading;
        private bool showing;

        private void HandleInitialization(string error)
        {
            initializing = false;
            if (error != null)
            {
                Status = $"Initialization failed. Select Load ad to retry. ({error})";
                return;
            }

            ad = new LevelPlayRewardedAd(adUnitId);
            ad.OnAdLoaded += HandleLoaded;
            ad.OnAdLoadFailed += HandleLoadFailed;
            ad.OnAdDisplayFailed += HandleDisplayFailed;
            ad.OnAdClosed += HandleClosed;
            ad.OnAdRewarded += HandleRewarded;
            LoadNextAd();
        }

        private void LoadNextAd()
        {
            loading = true;
            Status = "Loading ad...";
            ad.LoadAd();
        }

        private void HandleLoaded(LevelPlayAdInfo info)
        {
            loading = false;
            Status = "Ad ready.";
        }

        private void HandleLoadFailed(LevelPlayAdError error)
        {
            loading = false;
            Status = $"Ad unavailable. Select Load ad to retry. ({error.ErrorMessage})";
        }

        private void HandleDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            showing = false;
            Status = $"Ad could not be shown. Select Load ad to retry. ({error.ErrorMessage})";
        }

        private void HandleClosed(LevelPlayAdInfo info)
        {
            showing = false;
            LoadNextAd();
            AdClosed?.Invoke();
        }

        private void HandleRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
        {
            // LevelPlay can deliver this after OnAdClosed, even while the next ad is loading.
            RewardEarned?.Invoke(reward.Name, reward.Amount);
        }
#endif

        public void Dispose()
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            LevelPlayInitialization.Cancel(HandleInitialization);
            if (ad != null)
            {
                ad.OnAdLoaded -= HandleLoaded;
                ad.OnAdLoadFailed -= HandleLoadFailed;
                ad.OnAdDisplayFailed -= HandleDisplayFailed;
                ad.OnAdClosed -= HandleClosed;
                ad.OnAdRewarded -= HandleRewarded;
                ad.Dispose();
                ad = null;
            }
            RewardEarned = null;
            AdClosed = null;
#endif
        }
    }
}
