#if SAI_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR)
using System;
using SaiGame.Services;
using Steamworks;

namespace SaiGame.Auth.Steam
{
    // Requires Steamworks.NET and the SAI_STEAMWORKS_NET scripting define.
    // The host game initializes SteamAPI and calls SteamAPI.RunCallbacks.
    public class SteamworksNetTicketProvider : SaiBehaviour, ISteamTicketProvider
    {
        private Callback<GetTicketForWebApiResponse_t> callback;
        private HAuthTicket ticket = HAuthTicket.Invalid;
        private Action<string> success;
        private Action<string> failure;

        public void RequestTicket(string identity, Action<string> onSuccess, Action<string> onError)
        {
            CancelTicket();
            success = onSuccess;
            failure = onError;
            try
            {
                if (!SteamUser.BLoggedOn()) { ReportFailure("STEAM_NOT_LOGGED_ON"); return; }
                callback = Callback<GetTicketForWebApiResponse_t>.Create(ReceiveTicket);
                ticket = SteamUser.GetAuthTicketForWebApi(identity);
                if (ticket == HAuthTicket.Invalid) ReportFailure("STEAM_TICKET_FAILED");
            }
            catch (Exception) { ReportFailure("STEAM_UNAVAILABLE"); }
        }

        private void ReceiveTicket(GetTicketForWebApiResponse_t response)
        {
            if (response.m_hAuthTicket != ticket) return;
            if (response.m_eResult != EResult.k_EResultOK || response.m_cubTicket <= 0 || response.m_cubTicket > response.m_rgubTicket.Length)
            { ReportFailure("STEAM_TICKET_FAILED"); return; }
            var handler = success;
            success = null;
            failure = null;
            handler?.Invoke(BitConverter.ToString(response.m_rgubTicket, 0, response.m_cubTicket).Replace("-", string.Empty));
        }

        private void ReportFailure(string error)
        {
            var handler = failure;
            CancelTicket();
            handler?.Invoke(error);
        }

        public void CancelTicket()
        {
            success = null;
            failure = null;
            if (callback != null) { callback.Dispose(); callback = null; }
            if (ticket != HAuthTicket.Invalid)
            {
                try { SteamUser.CancelAuthTicket(ticket); }
                catch (Exception) { /* Steam may already be shutting down. */ }
                ticket = HAuthTicket.Invalid;
            }
        }

        private void OnDisable() { CancelTicket(); }
    }
}
#endif
