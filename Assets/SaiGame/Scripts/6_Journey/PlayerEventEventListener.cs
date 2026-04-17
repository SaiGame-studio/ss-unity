using UnityEngine;

namespace SaiGame.Services
{
    public class PlayerEventEventListener : SaiBehaviour
    {
        private PlayerEvent playerEvent => SaiServer.Instance?.PlayerEvent;

        private void OnEnable()
        {
            if (this.playerEvent != null)
            {
                this.playerEvent.OnTrackEventSuccess += this.HandleTrackEventSuccess;
                this.playerEvent.OnTrackEventFailure += this.HandleTrackEventFailure;
            }
        }

        private void OnDisable()
        {
            if (this.playerEvent != null)
            {
                this.playerEvent.OnTrackEventSuccess -= this.HandleTrackEventSuccess;
                this.playerEvent.OnTrackEventFailure -= this.HandleTrackEventFailure;
            }
        }

        private void HandleTrackEventSuccess(TrackEventResponse response)
        {
            // Handle successful event tracking
            // Example: show confirmation UI, trigger analytics, etc.
        }

        private void HandleTrackEventFailure(string error)
        {
            // Handle event tracking failure
            // Example: show error message or queue for retry
        }
    }
}
