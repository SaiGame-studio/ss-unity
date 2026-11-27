using System;
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
using Unity.Services.LevelPlay;
#endif

namespace SaiGame.LevelPlaySample
{
    public sealed class BannerAdSession : IDisposable
    {
        public string Status { get; private set; } = "Select Show banner to begin.";
        public event Action AdUnavailable;
        public event Action AdDisplayed;

#if UNITY_EDITOR
        public static bool TryGetEditorBannerSize(BannerAdLayout layout, out UnityEngine.Vector2 size)
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            float scale = UnityEngine.Screen.dpi / 160f;
            if (scale > 0f)
            {
                var adSize = ToAdSize(layout);
                size = new UnityEngine.Vector2(adSize.Width * scale, adSize.Height * scale);
                return true;
            }
#endif
            size = default;
            return false;
        }
#endif

        private void ReportAdUnavailable(string status)
        {
            Status = status;
            AdUnavailable?.Invoke();
        }

        private void ReportAdDisplayed()
        {
            Status = "Banner displayed.";
            AdDisplayed?.Invoke();
        }

#if SAIGAME_LEVELPLAY_AVAILABLE
        public static bool HasCompatiblePackage => true;
#else
        public static bool HasCompatiblePackage => false;
#endif

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        public bool IsAvailable => true;
#else
        public bool IsAvailable => false;
#endif

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        private LevelPlayBannerAd banner;
        private string adUnitId;
        private bool initializing;
        private bool loading;
        private bool loaded;
        private bool wantsVisible;
        private bool showRequested;
        private BannerAdLayout layout = new BannerAdLayout();
        private BannerAdLayout appliedLayout;

        public bool IsVisible => wantsVisible;
#else
        public bool IsVisible => false;
#endif

        // A layout different from the current banner's recreates the banner, because LevelPlay
        // fixes position and size when the banner is created.
        public void Show(string appKey, string unitId, BannerAdLayout bannerLayout)
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            wantsVisible = true;
            layout = (bannerLayout ?? new BannerAdLayout()).Clone();
            if (initializing || loading)
                return;

            if (banner != null && !layout.SameAs(appliedLayout))
                DestroyBanner();

            if (banner != null)
            {
                if (loaded)
                    ShowLoadedBanner();
                else
                    Load();
                return;
            }

            adUnitId = unitId.Trim();
            initializing = true;
            Status = "Initializing LevelPlay...";
            LevelPlayInitialization.Request(appKey.Trim(), HandleInitialization);
#endif
        }

        public void Hide()
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            wantsVisible = false;
            if (showRequested)
                banner.HideAd();

            showRequested = false;
            if (loaded)
                banner.PauseAutoRefresh();

            Status = "Banner hidden.";
#endif
        }

#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
        private void HandleInitialization(string error)
        {
            initializing = false;
            if (error != null)
            {
                string status = $"LevelPlay initialization failed. ({error})";
                if (wantsVisible)
                    ReportAdUnavailable(status);
                else
                    Status = status;
                return;
            }

            if (!wantsVisible)
                return;

            CreateBanner();
        }

        private void CreateBanner()
        {
            appliedLayout = layout.Clone();
            var config = new LevelPlayBannerAd.Config.Builder()
                .SetSize(ToAdSize(appliedLayout))
                .SetPosition(ToPosition(appliedLayout))
                .SetDisplayOnLoad(false)
                .SetRespectSafeArea(appliedLayout.RespectSafeArea)
                .Build();
            banner = new LevelPlayBannerAd(adUnitId, config);
            banner.OnAdLoaded += HandleLoaded;
            banner.OnAdLoadFailed += HandleLoadFailed;
            banner.OnAdDisplayed += HandleDisplayed;
            banner.OnAdDisplayFailed += HandleDisplayFailed;
            Load();
        }

        private static LevelPlayAdSize ToAdSize(BannerAdLayout value)
        {
            switch (value.Size)
            {
                case SaiBannerSize.Large: return LevelPlayAdSize.LARGE;
                case SaiBannerSize.MediumRectangle: return LevelPlayAdSize.MEDIUM_RECTANGLE;
                case SaiBannerSize.Leaderboard: return LevelPlayAdSize.LEADERBOARD;
                case SaiBannerSize.Adaptive: return LevelPlayAdSize.CreateAdaptiveAdSize(value.AdaptiveWidth);
                case SaiBannerSize.Custom: return LevelPlayAdSize.CreateCustomBannerSize(value.CustomWidth, value.CustomHeight);
                default: return LevelPlayAdSize.BANNER;
            }
        }

        private static LevelPlayBannerPosition ToPosition(BannerAdLayout value)
        {
            switch (value.Position)
            {
                case SaiBannerPosition.TopLeft: return LevelPlayBannerPosition.TopLeft;
                case SaiBannerPosition.TopCenter: return LevelPlayBannerPosition.TopCenter;
                case SaiBannerPosition.TopRight: return LevelPlayBannerPosition.TopRight;
                case SaiBannerPosition.CenterLeft: return LevelPlayBannerPosition.CenterLeft;
                case SaiBannerPosition.Center: return LevelPlayBannerPosition.Center;
                case SaiBannerPosition.CenterRight: return LevelPlayBannerPosition.CenterRight;
                case SaiBannerPosition.BottomLeft: return LevelPlayBannerPosition.BottomLeft;
                case SaiBannerPosition.BottomRight: return LevelPlayBannerPosition.BottomRight;
                case SaiBannerPosition.Custom: return new LevelPlayBannerPosition(value.CustomPosition);
                default: return LevelPlayBannerPosition.BottomCenter;
            }
        }

        private void DestroyBanner()
        {
            banner.OnAdLoaded -= HandleLoaded;
            banner.OnAdLoadFailed -= HandleLoadFailed;
            banner.OnAdDisplayed -= HandleDisplayed;
            banner.OnAdDisplayFailed -= HandleDisplayFailed;
            banner.Dispose();
            banner = null;
            appliedLayout = null;
            loaded = false;
            showRequested = false;
        }

        private void Load()
        {
            loading = true;
            Status = "Loading banner...";
            banner.LoadAd();
        }

        private void ShowLoadedBanner()
        {
            if (showRequested)
                return;

            showRequested = true;
            Status = "Showing banner...";
            banner.ResumeAutoRefresh();
            banner.ShowAd();
        }

        private void HandleLoaded(LevelPlayAdInfo info)
        {
            loading = false;
            loaded = true;
            if (wantsVisible && !layout.SameAs(appliedLayout))
            {
                DestroyBanner();
                CreateBanner();
                return;
            }

            if (wantsVisible)
                ShowLoadedBanner();
            else
                banner.PauseAutoRefresh();
        }

        private void HandleLoadFailed(LevelPlayAdError error)
        {
            loading = false;
            if (!loaded && wantsVisible)
                ReportAdUnavailable($"Banner load failed. Select Show banner to retry. ({error.ErrorMessage})");
        }

        private void HandleDisplayed(LevelPlayAdInfo info)
        {
            if (wantsVisible)
                ReportAdDisplayed();
        }

        private void HandleDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            showRequested = false;
            if (wantsVisible)
                ReportAdUnavailable($"Banner display failed. Select Show banner to retry. ({error.ErrorMessage})");
        }
#endif

        public void Dispose()
        {
#if SAIGAME_LEVELPLAY_AVAILABLE && (UNITY_ANDROID || UNITY_IOS)
            LevelPlayInitialization.Cancel(HandleInitialization);
            if (banner != null)
                DestroyBanner();
#endif
            AdUnavailable = null;
            AdDisplayed = null;
        }
    }
}
