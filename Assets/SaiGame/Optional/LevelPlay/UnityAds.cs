using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    [AddComponentMenu("SaiGame/UnityAds")]
    [DisallowMultipleComponent]
    public sealed class UnityAds : MonoBehaviour
    {
        [SerializeField] private string androidAppKey;
        [SerializeField] private string androidBannerAdUnitId;
        [SerializeField] private string iosAppKey;
        [SerializeField] private string iosBannerAdUnitId;
        [SerializeField, Tooltip("Ad format this component shows.")]
        private SaiAdFormat format = SaiAdFormat.Banner;
        [SerializeField] private string androidUnitId;
        [SerializeField] private string iosUnitId;
        [SerializeField, Tooltip("Position, size and safe area of the banner.")]
        private BannerAdLayout bannerLayout = new BannerAdLayout();

        private readonly BannerAdSession banner = new BannerAdSession();
        private AdsManager manager;

        public AdsManager Manager => Application.isPlaying ? manager : FindManager();
        public SaiAdFormat Format => format;

        public static bool IsFormatSupported(SaiAdFormat value) => value == SaiAdFormat.Banner;

        public bool IsBannerAvailable => CanUseAds && HasBannerConfiguration;
        public string BannerStatus => banner.Status;
        public bool IsBannerVisible => banner.IsVisible;

        public bool HasBannerConfiguration =>
            !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(SelectedBannerUnitId);

        // App Key for the active build target, from the AdsManager when this ad has one.
        public string AppKey => Manager == null ? LegacyAppKey : Manager.AppKey;
        private bool CanUseAds => isActiveAndEnabled && banner.IsAvailable && (Manager == null || Manager.isActiveAndEnabled);
        private string SelectedBannerUnitId => Manager == null ? BannerAdUnitId : UnitId;

#if UNITY_ANDROID
        private string LegacyAppKey => androidAppKey;
        private string UnitId => androidUnitId;
        private string BannerAdUnitId => androidBannerAdUnitId;
#elif UNITY_IOS
        private string LegacyAppKey => iosAppKey;
        private string UnitId => iosUnitId;
        private string BannerAdUnitId => iosBannerAdUnitId;
#else
        private string LegacyAppKey => null;
        private string UnitId => null;
        private string BannerAdUnitId => null;
#endif

        private void Awake() => manager = FindManager();

        private AdsManager FindManager()
        {
            return transform.parent != null && transform.parent.TryGetComponent<AdsManager>(out var parent) ? parent : null;
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

        internal void ShowBannerDirectly() => banner.Show(AppKey, SelectedBannerUnitId, bannerLayout);

        // Returns a copy; use the Set methods to apply changes.
        public BannerAdLayout BannerLayout => bannerLayout.Clone();

        public void SetBannerPosition(SaiBannerPosition position)
        {
            bannerLayout.Position = position;
            RefreshVisibleBanner();
        }

        // Offset of the banner's top-left corner from the top-left corner of the screen.
        public void SetBannerPosition(Vector2 offset)
        {
            bannerLayout.Position = SaiBannerPosition.Custom;
            bannerLayout.CustomPosition = offset;
            RefreshVisibleBanner();
        }

        public void SetBannerLayout(BannerAdLayout layout)
        {
            if (layout == null)
                return;

            bannerLayout = layout.Clone();
            RefreshVisibleBanner();
        }

        private void RefreshVisibleBanner()
        {
            if (IsBannerVisible)
                ShowBanner();
        }

        public void HideBanner()
        {
            banner.Hide();
            if (Manager != null)
                Manager.ReleaseBanner(this);
        }

        // Toggles the ad according to its format.
        public void ShowHide()
        {
            switch (format)
            {
                case SaiAdFormat.Banner:
                    ShowHideBanner();
                    break;
                default:
                    Debug.LogError($"[UnityAds] Unsupported ad format '{format}' on '{name}'.", this);
                    break;
            }
        }

        private void ShowHideBanner()
        {
            if (IsBannerVisible)
            {
                HideBanner();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedBannerUnitId))
            {
                Debug.LogError($"[UnityAds] Missing unit ID for '{name}' ({format}) on the active build target.", this);
                return;
            }

            ShowBanner();
        }

        private void OnDisable() => HideBanner();

        private void OnDestroy() => banner.Dispose();
    }
}
