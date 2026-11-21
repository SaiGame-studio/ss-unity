using System;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    [AddComponentMenu("SaiGame/UnityAds")]
    [DisallowMultipleComponent]
    public sealed class UnityAds : MonoBehaviour
    {
        [SerializeField] private string androidAppKey;
        [SerializeField] private string androidAdUnitId;
        [SerializeField] private string androidBannerAdUnitId;
        [SerializeField] private string iosAppKey;
        [SerializeField] private string iosAdUnitId;
        [SerializeField] private string iosBannerAdUnitId;
        [SerializeField] private SaiAdFormat format;
        [SerializeField] private string androidUnitId;
        [SerializeField] private string iosUnitId;

        private readonly RewardedAdSession session = new RewardedAdSession();
        private readonly BannerAdSession banner = new BannerAdSession();
        private AdsManager manager;

        public AdsManager Manager => Application.isPlaying ? manager : FindManager();
        public SaiAdFormat Format => format;

        public bool IsAvailable => CanUseAds && HasConfiguration;
        public bool IsReady => IsAvailable && session.IsReady;
        public string Status => session.Status;
        public bool IsBannerAvailable => CanUseAds && HasBannerConfiguration;
        public string BannerStatus => banner.Status;
        public bool IsBannerVisible => banner.IsVisible;

        public bool HasConfiguration => (Manager == null || format == SaiAdFormat.Rewarded) &&
            !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(RewardedUnitId);
        public bool HasBannerConfiguration => (Manager == null || format == SaiAdFormat.Banner) &&
            !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(SelectedBannerUnitId);

        private string AppKey => Manager == null ? LegacyAppKey : Manager.AppKey;
        private bool CanUseAds => isActiveAndEnabled && session.IsAvailable && (Manager == null || Manager.isActiveAndEnabled);
        private string RewardedUnitId => Manager == null ? AdUnitId : UnitId;
        private string SelectedBannerUnitId => Manager == null ? BannerAdUnitId : UnitId;

#if UNITY_ANDROID
        private string LegacyAppKey => androidAppKey;
        private string UnitId => androidUnitId;
        private string AdUnitId => androidAdUnitId;
        private string BannerAdUnitId => androidBannerAdUnitId;
#elif UNITY_IOS
        private string LegacyAppKey => iosAppKey;
        private string UnitId => iosUnitId;
        private string AdUnitId => iosAdUnitId;
        private string BannerAdUnitId => iosBannerAdUnitId;
#else
        private string LegacyAppKey => null;
        private string UnitId => null;
        private string AdUnitId => null;
        private string BannerAdUnitId => null;
#endif

        private void Awake() => manager = FindManager();

        private AdsManager FindManager()
        {
            return transform.parent != null && transform.parent.TryGetComponent<AdsManager>(out var parent) ? parent : null;
        }

        public event Action<string, int> RewardEarned
        {
            add => session.RewardEarned += value;
            remove => session.RewardEarned -= value;
        }

        // Call after the game's privacy and consent setup is complete.
        public void Load()
        {
            if (IsAvailable)
                session.Load(AppKey, RewardedUnitId);
        }

        public bool TryShow(string placementName = null)
        {
            return IsAvailable && session.TryShow(placementName);
        }

        // Call after the game's privacy and consent setup is complete.
        public void ShowBanner()
        {
            if (!IsBannerAvailable)
                return;

            if (Manager != null)
                Manager.ShowBanner(this);
            else
                ShowBannerDirectly();
        }

        internal void ShowBannerDirectly() => banner.Show(AppKey, SelectedBannerUnitId);

        public void HideBanner()
        {
            banner.Hide();
            if (Manager != null)
                Manager.ReleaseBanner(this);
        }

        // Banner: toggles visibility. Rewarded: shows the ad when it is ready.
        public void ShowHide()
        {
            if (format == SaiAdFormat.Banner && IsBannerVisible)
            {
                HideBanner();
                return;
            }

            string unitId = format == SaiAdFormat.Rewarded ? RewardedUnitId : SelectedBannerUnitId;
            if (string.IsNullOrWhiteSpace(unitId))
            {
                Debug.LogError($"[UnityAds] Missing unit ID for '{name}' ({format}) on the active build target.", this);
                return;
            }

            if (format == SaiAdFormat.Rewarded)
                TryShow();
            else
                ShowBanner();
        }

        private void OnDisable() => HideBanner();

        private void OnDestroy()
        {
            banner.Dispose();
            session.Dispose();
        }
    }
}
