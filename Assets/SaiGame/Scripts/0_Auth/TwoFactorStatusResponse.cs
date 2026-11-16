using System;

namespace SaiGame.Services
{
    [Serializable]
    public class TwoFactorStatusResponse
    {
        public bool enabled;
        public bool verification_required;
        public bool available;
        public bool can_disable;
        public int recovery_codes_remaining;
    }
}
