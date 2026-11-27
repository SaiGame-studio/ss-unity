using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    [DisallowMultipleComponent]
    public sealed class AdsManager : MonoBehaviour
    {
        [SerializeField] private string androidAppKey;
        [SerializeField] private string iosAppKey;

        [SerializeField, Tooltip("UnityAds children created and managed by this manager.")]
        private List<UnityAds> ads = new List<UnityAds>();

        [SerializeField, Tooltip("Scene object outside the SaiServer prefab that holds the UnityAds objects, " +
            "so SDK updates and prefab reverts never remove them. Created by Create UnityAds.")]
        private Transform adsHolder;

        private UnityAds visibleBanner;

        public IReadOnlyList<UnityAds> Ads => ads;
        public Transform AdsHolder => adsHolder;

        public string AppKey
        {
            get
            {
#if UNITY_ANDROID
                return androidAppKey;
#elif UNITY_IOS
                return iosAppKey;
#else
                return null;
#endif
            }
        }

        public bool TryGetAd(string name, out UnityAds result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            foreach (var candidate in ads)
            {
                if (candidate == null || !string.Equals(candidate.name, name, StringComparison.Ordinal))
                    continue;

                if (result != null)
                {
                    result = null;
                    return false;
                }

                result = candidate;
            }

            return result != null;
        }

        public void ShowHide(string name)
        {
            if (TryGetAd(name, out var ad))
                ad.ShowHide();
        }

        public bool SetBannerPosition(string name, SaiBannerPosition position)
        {
            if (!TryGetAd(name, out var ad))
                return false;

            ad.SetBannerPosition(position);
            return true;
        }

        // Offset of the banner's top-left corner from the top-left corner of the screen.
        public bool SetBannerPosition(string name, Vector2 offset)
        {
            if (!TryGetAd(name, out var ad))
                return false;

            ad.SetBannerPosition(offset);
            return true;
        }

        public bool SetBannerLayout(string name, BannerAdLayout layout)
        {
            if (!TryGetAd(name, out var ad))
                return false;

            ad.SetBannerLayout(layout);
            return true;
        }

        public bool ShowBanner(string name)
        {
            return TryGetAd(name, out var ad) && ShowBanner(ad);
        }

        public void HideBanner(string name)
        {
            if (TryGetAd(name, out var ad))
                ad.HideBanner();
        }

        internal bool ShowBanner(UnityAds ad, bool previewFallback = false)
        {
            if (!isActiveAndEnabled || !(previewFallback ? ad.CanUseSaiServerFallback : ad.IsBannerAvailable))
                return false;

            if (visibleBanner != null && visibleBanner != ad)
                visibleBanner.HideBanner();

            visibleBanner = ad;
            ad.ShowBannerDirectly(previewFallback);
            return true;
        }

        internal void ReleaseBanner(UnityAds ad)
        {
            if (visibleBanner == ad)
                visibleBanner = null;
        }

        private void OnDisable()
        {
            if (visibleBanner != null)
                visibleBanner.HideBanner();
        }
    }
}
