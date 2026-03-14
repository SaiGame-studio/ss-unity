using System;

namespace SaiGame.Services
{
    // Reference model — not used for serialization (event_data is raw JSON embedded manually)
    [Serializable]
    public class TrackEventRequest
    {
        public string event_type;
        public string session_id;
        // event_data is a free-form JSON object; see PlayerEvent.TrackEventCoroutine
    }
}
