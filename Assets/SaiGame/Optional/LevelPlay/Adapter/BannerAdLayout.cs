using System;
using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    [Serializable]
    public sealed class BannerAdLayout
    {
        [SerializeField, Tooltip("Where the banner appears on screen.")]
        private SaiBannerPosition position = SaiBannerPosition.BottomCenter;

        [SerializeField, Tooltip("Offset of the banner's top-left corner from the top-left corner of the screen. Used when Position is Custom.")]
        private Vector2 customPosition;

        [SerializeField, Tooltip("Banner size. Banner is 320x50, Large is 320x90, Medium Rectangle is 300x250, Leaderboard is 728x90.")]
        private SaiBannerSize size = SaiBannerSize.Banner;

        [SerializeField, Tooltip("Adaptive banner width in dp (Android) or points (iOS). -1 uses the screen width.")]
        private int adaptiveWidth = -1;

        [SerializeField, Tooltip("Custom banner width in pixels.")]
        private int customWidth = 320;

        [SerializeField, Tooltip("Custom banner height in pixels.")]
        private int customHeight = 50;

        [SerializeField, Tooltip("Keep the banner inside the device safe area (notches, rounded corners).")]
        private bool respectSafeArea = true;

        public SaiBannerPosition Position { get => position; set => position = value; }
        public Vector2 CustomPosition { get => customPosition; set => customPosition = value; }
        public SaiBannerSize Size { get => size; set => size = value; }
        public int AdaptiveWidth { get => adaptiveWidth; set => adaptiveWidth = value; }
        public int CustomWidth { get => customWidth; set => customWidth = value; }
        public int CustomHeight { get => customHeight; set => customHeight = value; }
        public bool RespectSafeArea { get => respectSafeArea; set => respectSafeArea = value; }

        public BannerAdLayout Clone() => (BannerAdLayout)MemberwiseClone();

        public bool SameAs(BannerAdLayout other)
        {
            return other != null &&
                position == other.position &&
                customPosition == other.customPosition &&
                size == other.size &&
                adaptiveWidth == other.adaptiveWidth &&
                customWidth == other.customWidth &&
                customHeight == other.customHeight &&
                respectSafeArea == other.respectSafeArea;
        }
    }
}
