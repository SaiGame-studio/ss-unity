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

        private UnityAds visibleBanner;

        public IReadOnlyList<UnityAds> Ads => ads;

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

        public bool ShowBanner(string name)
        {
            return TryGetAd(name, out var ad) && ShowBanner(ad);
        }

        public void HideBanner(string name)
        {
            if (TryGetAd(name, out var ad))
                ad.HideBanner();
        }

        internal bool ShowBanner(UnityAds ad)
        {
            if (!isActiveAndEnabled || !ad.IsBannerAvailable)
                return false;

            if (visibleBanner != null && visibleBanner != ad)
                visibleBanner.HideBanner();

            visibleBanner = ad;
            ad.ShowBannerDirectly();
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
