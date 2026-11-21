# SaiGame LevelPlay rewarded ads sample

This sample ships with the SaiGame SDK and supports Android and iOS.
Unity automatically detects Ads Mediation 9.6 or newer within the 9.x
series. Without a compatible package, `IsAvailable` and `TryShow()`
return false, `Load()` does nothing, and the scene keeps its component
references.

## Preview a banner

1. Create a **Banner** ad unit for the Android or iOS app in LevelPlay.
2. Select **SaiServer > AdsManager** and enter the App Key for the active
   build target. Click **Create UnityAds** and give the child a unique name
   such as `login-loading`. Select **Banner** and set its Ad Unit ID for
   each platform you support.
3. In Play Mode, click **Show** or **Hide** next to the ad in the AdsManager
   **Ads** list, or select the child and click **Show banner** or **Hide banner**.
   In a game, call `manager.ShowHide("login-loading")` to toggle it, or call `manager.ShowBanner("login-loading")` after the game's
   consent setup, then `manager.HideBanner("login-loading")` when the screen
   closes. Showing a different managed banner hides the previous one.
   The banner uses the bottom center of the screen and respects the safe area.

The Editor preview exercises the mock banner. Verify delivery and screen
layout on a configured test device. Both ad formats share the platform App Key.
Existing standalone ad components continue to use their own fields.

## Run the rewarded ads demo

1. Switch the Unity build target to Android or iOS and open
   `Assets/SaiGame/Optional/LevelPlay/Demo/RewardedAdsDemo.unity`.
2. Create the app and a rewarded ad unit for your target platform in the
   LevelPlay dashboard. Select **Rewarded Ads** in the scene hierarchy
   and fill in that platform's App Key and Rewarded Ad Unit ID.
3. Enter Play Mode, select **Load ad**, wait for **Ad ready**, then select
   **Show ad**. Unity's editor mock can exercise button wiring and
   reward callbacks. To try the demo on a device, include the demo scene
   in the active build profile and build for that device.

## Add rewarded ads to a game

1. In the game's Package Manager, install **Ads Mediation**
   (`com.unity.services.levelplay`) 9.6.0. Set up the networks and native
   dependencies for that game using the
   [Unity package integration guide](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/package-integration).
2. In **SaiServer > AdsManager**, set the App Key, create a `UnityAds` child,
   select **Rewarded**, and enter the Ad Unit ID for the target platform.
   The Inspector shows the installed LevelPlay version, the active build
   target, configuration blockers, and whether an ad is ready in Play Mode.
   Use one ads component to own LevelPlay initialization. Leave SDK
   auto-initialization off in the LevelPlay Developer Settings when using
   `UnityAds` to initialize LevelPlay.
3. Work through `AdsManager`: call `ShowHide(name)`, or look the ad up with
   `TryGetAd(name, out ad)`. The manager lists every ad it created in `Ads`.
   Call `Load()` after completing the game's privacy and consent setup.
   Use `IsAvailable` to hide the ad entry point when ads are unavailable.
   Use `IsReady` to enable the gameplay button and `TryShow()` to show
   the ad. If `TryShow()` returns false, keep gameplay available.
   The sample preloads after an ad closes. Call `Load()` again after a
   failed initialization or load when the player retries.
4. Subscribe to `RewardEarned` before showing an ad and keep the
   listener alive across the full ad lifecycle. Use this event as the
   client confirmation to grant the game's reward. `AdClosed` can arrive
   before the reward event, so use it only for UI. Unsubscribe when the
   listener is destroyed. The demo updates a local counter. Games with
   server-owned rewards should use
   [LevelPlay server-to-server callbacks](https://docs.unity.com/en-us/grow/levelplay/platform/settings/server-to-server-callback)
   with their own reward validation.

## Verify on a device

Use a configured test device and the
[LevelPlay Integration Test Suite](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/integration-test-suite)
to verify the network adapters for each target platform. Exercise these
flows in the game:

- Complete a test ad and confirm the reward fires once, even if the ad
  closes before the reward event.
- Close before qualifying for a reward and verify no reward is granted.
- Try to show before an ad is ready and verify gameplay stays available.
- Go offline before loading, then reconnect, retry, and verify ads can
  load again.
