# SaiGame LevelPlay banner ads

This optional module ships with the SaiGame SDK and supports banner ads on
Android and iOS. Unity automatically detects Ads Mediation 9.6 or newer
within the 9.x series. Without a compatible package, banners stay hidden
and the scene keeps its component references.

## Set up

1. In the game's Package Manager, install **Ads Mediation**
   (`com.unity.services.levelplay`) 9.6.0. Set up the networks and native
   dependencies for that game using the
   [Unity package integration guide](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/package-integration).
   Leave SDK auto-initialization off in the LevelPlay Developer Settings;
   `UnityAds` initializes LevelPlay.
2. Create a **Banner** ad unit for the Android or iOS app in LevelPlay.
3. Select **SaiServer > AdsManager** and enter the App Key for each
   platform. Click **Create UnityAds** and give the child a unique name
   such as `login-loading`. Set its Banner Ad Unit ID for each platform
   you support. The manager lists every ad it created under **Ads**.

## Preview a banner

In Play Mode, click **Show** or **Hide** next to the ad in the AdsManager
**Ads** list. If the ad has no unit ID for the active build target, an
error is logged and nothing is shown. The Editor preview exercises the
mock banner. Verify delivery and screen layout on a configured test device.

## Use in a game

Work through `AdsManager` only, after the game's consent setup:

- `ShowHide(name)` toggles a banner.
- `ShowBanner(name)` and `HideBanner(name)` show or hide it explicitly.
- `TryGetAd(name, out ad)` returns the `UnityAds` component when needed.

Showing a different managed banner hides the previous one.

## Banner layout

Each `UnityAds` has a **Banner layout** section:

- **Position**: one of nine screen anchors, or **Custom** with an offset of
  the banner's top-left corner from the top-left corner of the screen.
- **Size**: Banner (320x50), Large (320x90), Medium Rectangle (300x250),
  Leaderboard (728x90), Adaptive (optional width), or Custom width and height.
- **Respect safe area**: keeps the banner clear of notches and rounded corners.

Change it at runtime through the manager; a visible banner is recreated
with the new layout:

- `SetBannerPosition(name, SaiBannerPosition.TopCenter)`
- `SetBannerPosition(name, new Vector2(x, y))`
- `SetBannerLayout(name, layout)` for position, size and safe area together.

Edits in the Inspector apply live to a visible banner in Play Mode.

## Verify on a device

Use a configured test device and the
[LevelPlay Integration Test Suite](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/integration-test-suite)
to verify the network adapters for each target platform.
