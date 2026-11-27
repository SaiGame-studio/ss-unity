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
        [SerializeField, Tooltip("Show the fallback banner when LevelPlay cannot provide an ad.")]
        private bool enableSaiServerBannerFallback = true;

        [SerializeField, Tooltip("The AdsManager that owns this ad. Set when the ad is created from the AdsManager.")]
        private AdsManager adsManager;

        private readonly BannerAdSession banner = IsEnabledForBuild ? new BannerAdSession() : null;
        private readonly SaiServerFallbackBanner fallbackBanner =
            IsEnabledForBuild ? new SaiServerFallbackBanner() : null;
        private AdsManager manager;
        private bool fallbackRequested;

        public AdsManager Manager => Application.isPlaying ? manager : FindManager();
        public SaiAdFormat Format => format;

        public static bool IsFormatSupported(SaiAdFormat value) => value == SaiAdFormat.Banner;

        internal static bool IsEnabledForBuild =>
#if SAIGAME_DISABLE_ADS
            false;
#else
            true;
#endif

        public bool IsBannerAvailable => CanUseLevelPlayBanner || CanUseSaiServerFallback;
        public bool CanUseSaiServerFallback => CanShowBanner && enableSaiServerBannerFallback;
        public string BannerStatus => !IsEnabledForBuild ? "Banner ads are disabled for this build." :
            fallbackBanner.IsVisible ? "Fallback banner displayed." : banner.Status;
        public bool IsBannerVisible => IsEnabledForBuild && (banner.IsVisible || fallbackBanner.IsVisible);

        public bool HasBannerConfiguration =>
            !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(SelectedBannerUnitId);

        // App Key for the active build target, from the AdsManager when this ad has one.
        public string AppKey => Manager == null ? LegacyAppKey : Manager.AppKey;
        private bool CanShowBanner => IsEnabledForBuild && isActiveAndEnabled &&
                                      (Manager == null || Manager.isActiveAndEnabled);
        private bool CanUseLevelPlayBanner => CanShowBanner && banner.IsAvailable && HasBannerConfiguration;
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

        private void Awake()
        {
            if (!IsEnabledForBuild)
                return;

            manager = FindManager();
            banner.AdUnavailable += ShowSaiServerFallback;
            banner.AdDisplayed += HideSaiServerFallback;
        }

        // Ads live in an AdsHolder outside the SaiServer prefab and point to their manager.
        // An ad placed directly under an AdsManager still works without the reference.
        private AdsManager FindManager()
        {
            if (adsManager != null)
                return adsManager;

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

        internal void ShowBannerDirectly(bool previewFallback = false)
        {
            if (!CanShowBanner)
                return;

            if (previewFallback || !CanUseLevelPlayBanner)
            {
                banner.Hide();
                ShowSaiServerFallback();
                return;
            }

            // Keep the Sai Server fallback visible during a retry until LevelPlay displays an ad.
            banner.Show(AppKey, SelectedBannerUnitId, bannerLayout);
        }

#if UNITY_EDITOR
#if !SAIGAME_DISABLE_ADS
        private void OnGUI() => fallbackBanner.DrawEditorPreview(bannerLayout);
#endif

        public void TestSaiServerFallbackBanner()
        {
            if (!Application.isPlaying || !CanUseSaiServerFallback)
                return;

            if (Manager != null)
                Manager.ShowBanner(this, previewFallback: true);
            else
                ShowBannerDirectly(previewFallback: true);
        }
#endif

        private void ShowSaiServerFallback()
        {
            fallbackRequested = true;
            UpdateSaiServerFallback();
        }

        private void HideSaiServerFallback()
        {
            fallbackRequested = false;
            fallbackBanner.Hide();
        }

#if !SAIGAME_DISABLE_ADS
        private void LateUpdate()
        {
            if (!fallbackRequested)
                return;

            UpdateSaiServerFallback();
            fallbackBanner.UpdateClick(bannerLayout);
        }
#endif

        private void UpdateSaiServerFallback()
        {
            if (CanUseSaiServerFallback)
                fallbackBanner.Show(bannerLayout, gameObject.scene);
            else
                fallbackBanner.Hide();
        }

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
            if (!IsEnabledForBuild)
                return;

            if (banner.IsVisible)
                ShowBanner();
            if (fallbackRequested)
                UpdateSaiServerFallback();
        }

        public void HideBanner()
        {
            if (!IsEnabledForBuild)
                return;

            banner.Hide();
            HideSaiServerFallback();
            if (Manager != null)
                Manager.ReleaseBanner(this);
        }

        // Toggles the ad according to its format.
        public void ShowHide()
        {
            if (!IsEnabledForBuild)
                return;

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

            if (!CanUseSaiServerFallback && string.IsNullOrWhiteSpace(SelectedBannerUnitId))
            {
                Debug.LogError($"[UnityAds] Missing unit ID for '{name}' ({format}) on the active build target.", this);
                return;
            }

            ShowBanner();
        }

        private void OnDisable() => HideBanner();

        private void OnDestroy()
        {
            if (!IsEnabledForBuild)
                return;

            banner.Dispose();
            fallbackBanner.Dispose();
        }
    }
}
