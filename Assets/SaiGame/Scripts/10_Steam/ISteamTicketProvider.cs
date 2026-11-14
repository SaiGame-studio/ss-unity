using System;

namespace SaiGame.Auth.Steam
{
    // The game owns Steam initialization and callback pumping. Implementations
    // keep the ticket alive until CancelTicket is called by the login flow.
    public interface ISteamTicketProvider
    {
        void RequestTicket(string identity, Action<string> onSuccess, Action<string> onError);
        void CancelTicket();
    }
}
