using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Application = UnityEngine.Device.Application;
using Object = UnityEngine.Object;
using Screen = UnityEngine.Device.Screen;

namespace SaiGame.LevelPlaySample
{
    internal sealed class SaiServerFallbackBanner : IDisposable
    {
        private const string ResourceFolder = "SaiServerFallbackBanners/";
        private const float AndroidBaseDpi = 160f;
        private const float PreviewPhoneShortSide = 390f;
        // Keep the central 62% of the Leaderboard artwork, which contains the text and icon.
        private const float LeaderboardContentUvWidth = 0.62f;
        private static readonly Rect FullArtworkUv = new Rect(0f, 0f, 1f, 1f);

#if UNITY_IOS && !UNITY_EDITOR
        private float iosPixelsPerPoint;
        private float iosOffsetScale;

        [DllImport("__Internal")]
        private static extern float SaiGameBannerPixelsPerPoint();

        [DllImport("__Internal")]
        private static extern float SaiGameBannerCustomOffsetScale();
#endif

        private Canvas canvas;
        private RawImage image;
        private Texture2D artwork;
        private SaiBannerSize artworkSize;

#if UNITY_EDITOR
        private bool editorPreviewVisible;
        private Vector2 editorPreviewSize;

        public void DrawEditorPreview(BannerAdLayout layout)
        {
            if (!editorPreviewVisible || Event.current.type != EventType.Repaint)
                return;

            // LevelPlay's Editor mock uses the full Game View and integer screen midpoints.
            int width = UnityEngine.Screen.width;
            int height = UnityEngine.Screen.height;
            var center = new Vector2(width / 2, height / 2);
            var available = new Vector2(width, height);
            Vector2 size = editorPreviewSize;
            if (layout.Size == SaiBannerSize.Leaderboard)
                size = Vector2.Min(size, available);

            Vector2 offset = GetAnchorOffset(layout, available, center, size);
            if (size.x < editorPreviewSize.x && layout.Position != SaiBannerPosition.Custom)
                offset.x = 0f;

            int previousDepth = GUI.depth;
            GUI.depth = 0;
            GUI.DrawTextureWithTexCoords(new Rect(offset, size), artwork, GetArtworkUv(layout.Size, size));
            GUI.depth = previousDepth;
        }
#endif

        public bool IsVisible =>
#if UNITY_EDITOR
            editorPreviewVisible ||
#endif
            (canvas != null && canvas.gameObject.activeSelf);

        public void Show(BannerAdLayout layout, Scene scene)
        {
            if (artwork == null || artworkSize != layout.Size)
            {
                artworkSize = layout.Size;
                artwork = Resources.Load<Texture2D>(ResourceFolder + GetArtworkName(artworkSize));
            }

            if (artwork == null)
            {
                Hide();
                return;
            }

#if UNITY_EDITOR
            editorPreviewVisible = BannerAdSession.TryGetEditorBannerSize(layout, out editorPreviewSize);
            if (editorPreviewVisible)
            {
                HideCanvas();
                return;
            }
#endif

            if (canvas == null)
                CreateCanvas(scene);
            if (image.texture != artwork)
                image.texture = artwork;

            bool respectSafeArea = layout.RespectSafeArea;
#if UNITY_IOS && !UNITY_EDITOR
            // LevelPlay always anchors native iOS banners to the root view's safe area.
            respectSafeArea |= BannerAdSession.HasCompatiblePackage;
#endif
            Rect area = respectSafeArea ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            Vector2 size = GetSize(layout, artwork, area.size);
            Vector2 offset = GetCanvasOffset(layout, area, size, respectSafeArea);
            image.uvRect = GetArtworkUv(layout.Size, size);
            var rect = image.rectTransform;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(offset.x, -offset.y);
            if (!canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
#if UNITY_EDITOR
            editorPreviewVisible = false;
#endif
            HideCanvas();
        }

        private void HideCanvas()
        {
            if (canvas != null && canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            Hide();
            if (canvas != null)
                Object.Destroy(canvas.gameObject);
        }

        private void CreateCanvas(Scene scene)
        {
            var root = new GameObject("Sai Server Fallback Banner", typeof(Canvas));
            root.SetActive(false);
            if (root.scene != scene && scene.IsValid() && scene.isLoaded)
                SceneManager.MoveGameObjectToScene(root, scene);

            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var graphic = new GameObject("Banner Artwork", typeof(RawImage));
            graphic.transform.SetParent(canvas.transform, false);
            image = graphic.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.up;
            image.rectTransform.anchorMax = Vector2.up;
            image.rectTransform.pivot = Vector2.up;
        }

        private static string GetArtworkName(SaiBannerSize size)
        {
            switch (size)
            {
                case SaiBannerSize.Large: return "large-320x90";
                case SaiBannerSize.MediumRectangle: return "medium-rectangle-300x250";
                case SaiBannerSize.Leaderboard: return "leaderboard-728x90";
                case SaiBannerSize.Adaptive: return "adaptive-example-390x60";
                case SaiBannerSize.Custom: return "custom-example-480x80";
                default: return "banner-320x50";
            }
        }

        private Vector2 GetSize(BannerAdLayout layout, Texture texture, Vector2 available)
        {
            var artwork = new Vector2(texture.width, texture.height);
            Vector2 bounds;
            switch (layout.Size)
            {
                case SaiBannerSize.Adaptive:
                    float width = layout.AdaptiveWidth > 0 ? layout.AdaptiveWidth * GetPixelsPerAdUnit() : available.x;
                    bounds = artwork * (width / artwork.x);
                    break;
                case SaiBannerSize.Custom:
                    bounds = new Vector2(Mathf.Max(1, layout.CustomWidth), Mathf.Max(1, layout.CustomHeight));
                    break;
                default:
                    bounds = GetPresetSize(layout.Size) * GetPixelsPerAdUnit();
                    break;
            }

            bounds = Vector2.Min(bounds, available);
            if (layout.Size == SaiBannerSize.Leaderboard)
                return bounds;

            return artwork * Mathf.Min(bounds.x / artwork.x, bounds.y / artwork.y);
        }

        private Rect GetArtworkUv(SaiBannerSize size, Vector2 displaySize)
        {
            if (size != SaiBannerSize.Leaderboard || displaySize.y <= 0f)
                return FullArtworkUv;

            float uvWidth = displaySize.x * artwork.height / (displaySize.y * artwork.width);
            uvWidth = Mathf.Clamp(uvWidth, LeaderboardContentUvWidth, 1f);
            return new Rect((1f - uvWidth) / 2f, 0f, uvWidth, 1f);
        }

        private static Vector2 GetPresetSize(SaiBannerSize size)
        {
            switch (size)
            {
                case SaiBannerSize.Large: return new Vector2(320f, 90f);
                case SaiBannerSize.MediumRectangle: return new Vector2(300f, 250f);
                case SaiBannerSize.Leaderboard: return new Vector2(728f, 90f);
                default: return new Vector2(320f, 50f);
            }
        }

        private float GetPixelsPerAdUnit()
        {
            if (Application.platform == RuntimePlatform.Android && Screen.dpi > 0f)
                return Screen.dpi / AndroidBaseDpi;

#if UNITY_IOS && !UNITY_EDITOR
            if (iosPixelsPerPoint <= 0f)
                iosPixelsPerPoint = SaiGameBannerPixelsPerPoint();
            return Mathf.Max(1f, iosPixelsPerPoint);
#elif UNITY_EDITOR
            // Device Simulator reports physical iOS DPI. Use the nearest Retina scale for its preview.
            if (Application.platform == RuntimePlatform.IPhonePlayer && Screen.dpi > 0f)
                return Mathf.Max(1f, Mathf.Round(Screen.dpi / AndroidBaseDpi));

            // The regular Game view has no mobile density, so preview at a phone-sized logical resolution.
            return Mathf.Min(Screen.width, Screen.height) / PreviewPhoneShortSide;
#else
            return 1f;
#endif
        }

        private Vector2 GetCanvasOffset(BannerAdLayout layout, Rect area, Vector2 size, bool respectSafeArea)
        {
            var topLeft = new Vector2(area.xMin, Screen.height - area.yMax);
            if (layout.Position != SaiBannerPosition.Custom)
                return topLeft + GetAnchorOffset(layout, area.size, area.size / 2f, size);

            Vector2 offset = layout.CustomPosition;
#if UNITY_IOS && !UNITY_EDITOR
            if (BannerAdSession.HasCompatiblePackage)
            {
                // LevelPlay adds the root view inset to the custom constant on its safe area anchor.
                if (iosOffsetScale <= 0f)
                    iosOffsetScale = SaiGameBannerCustomOffsetScale();
                return topLeft * 2f + offset * iosOffsetScale;
            }
#endif
            if (respectSafeArea)
            {
                Vector2 space = area.size - size;
                offset.x = Mathf.Clamp(offset.x, topLeft.x, topLeft.x + space.x);
                offset.y = Mathf.Clamp(offset.y, topLeft.y, topLeft.y + space.y);
            }
            return offset;
        }

        private static Vector2 GetAnchorOffset(BannerAdLayout layout, Vector2 area, Vector2 center, Vector2 size)
        {
            Vector2 farEdge = area - size;
            Vector2 centered = center - size / 2f;
            switch (layout.Position)
            {
                case SaiBannerPosition.TopLeft: return Vector2.zero;
                case SaiBannerPosition.TopCenter: return new Vector2(centered.x, 0f);
                case SaiBannerPosition.TopRight: return new Vector2(farEdge.x, 0f);
                case SaiBannerPosition.CenterLeft: return new Vector2(0f, centered.y);
                case SaiBannerPosition.Center: return centered;
                case SaiBannerPosition.CenterRight: return new Vector2(farEdge.x, centered.y);
                case SaiBannerPosition.BottomLeft: return new Vector2(0f, farEdge.y);
                case SaiBannerPosition.BottomRight: return farEdge;
                case SaiBannerPosition.Custom: return layout.CustomPosition;
                default: return new Vector2(centered.x, farEdge.y);
            }
        }
    }
}
