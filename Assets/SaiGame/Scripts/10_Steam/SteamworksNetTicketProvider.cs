#if (STEAMWORKS_NET || SAI_STEAMWORKS_NET) && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR)
#define SAI_STEAMWORKS_AVAILABLE
#endif
using System;
using UnityEngine;
#if SAI_STEAMWORKS_AVAILABLE
using Steamworks;
#endif

namespace SaiGame.Services
{
    // Steam ticket source backed by Steamworks.NET. The class always compiles so
    // prefabs never lose the component; without Steamworks.NET every request
    // fails with STEAMWORKS_NET_MISSING.
    [DefaultExecutionOrder(-100)]
    public class SteamworksNetTicketProvider : SaiBehaviour, ISteamTicketProvider
    {
        [Header("Steam Settings")]
        [Tooltip("On the first Steam request, initialize SteamAPI and pump its callbacks if the game has not initialized it. A SteamAPI initialized by the game is always reused and never shut down by this component.")]
        [SerializeField] protected bool initializeSteamApi = true;

        [Header("Steam State")]
        [SerializeField] protected bool steamworksAvailable;
        [SerializeField] protected bool steamInitialized;
        [SerializeField] protected bool steamLoggedOn;
        [SerializeField] protected string localSteamId;
        [SerializeField] protected string personaName;
        [SerializeField] protected string initError;

        public bool SteamworksAvailable => steamworksAvailable;
        public bool SteamInitialized => steamInitialized;
        public string LocalSteamId => localSteamId;
        public string InitError => initError;

#if SAI_STEAMWORKS_AVAILABLE
        private bool ownsSteamApi;
        private Callback<GetTicketForWebApiResponse_t> callback;
        private HAuthTicket ticket = HAuthTicket.Invalid;
        private Action<string> success;
        private Action<string> failure;

        protected override void Awake()
        {
            base.Awake();
            steamworksAvailable = true;
        }

        protected override void Start()
        {
            base.Start();
            RefreshState();
        }

        protected virtual void Update()
        {
            if (ownsSteamApi) SteamAPI.RunCallbacks();
        }

        // Initializes SteamAPI only when nothing else has. Never called
        // automatically before the first Steam request.
        public void InitializeSteam()
        {
            if (ownsSteamApi || IsSteamApiReady()) { RefreshState(); return; }
            try
            {
                if (SteamAPI.InitEx(out string error) == ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    ownsSteamApi = true;
                    initError = "";
                }
                else initError = string.IsNullOrEmpty(error) ? "SteamAPI init failed" : error;
            }
            catch (Exception e) { initError = e.Message; }
            RefreshState();
            if (!string.IsNullOrEmpty(initError))
                Debug.LogWarning("[SteamworksNetTicketProvider] SteamAPI init failed: " + initError, gameObject);
        }

        public void RefreshState()
        {
            steamInitialized = IsSteamApiReady();
            try
            {
                steamLoggedOn = steamInitialized && SteamUser.BLoggedOn();
                localSteamId = steamLoggedOn ? SteamUser.GetSteamID().m_SteamID.ToString() : "";
                personaName = steamLoggedOn ? SteamFriends.GetPersonaName() : "";
            }
            catch (Exception)
            {
                steamLoggedOn = false;
                localSteamId = "";
                personaName = "";
            }
        }

        // Makes SteamAPI usable for any Steam feature, honouring initializeSteamApi.
        public bool EnsureSteamReady()
        {
            if (initializeSteamApi) InitializeSteam();
            else RefreshState();
            return steamInitialized;
        }

        // Steamworks.NET throws InvalidOperationException from any interface
        // call while SteamAPI is not initialized, by this component or the game.
        private static bool IsSteamApiReady()
        {
            try
            {
                SteamUser.BLoggedOn();
                return true;
            }
            catch (Exception) { return false; }
        }

        public void RequestTicket(string identity, Action<string> onSuccess, Action<string> onError)
        {
            CancelTicket();
            success = onSuccess;
            failure = onError;
            try
            {
                if (!EnsureSteamReady()) { ReportFailure("STEAM_NOT_INITIALIZED"); return; }
                if (!steamLoggedOn) { ReportFailure("STEAM_NOT_LOGGED_ON"); return; }
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

        private void OnDestroy()
        {
            CancelTicket();
            if (!ownsSteamApi) return;
            SteamAPI.Shutdown();
            ownsSteamApi = false;
            steamInitialized = false;
        }
#else
        protected override void Awake()
        {
            base.Awake();
            steamworksAvailable = false;
            initError = "STEAMWORKS_NET_MISSING";
        }

        public void InitializeSteam() { }

        public void RefreshState() { }

        public bool EnsureSteamReady() => false;

        public void RequestTicket(string identity, Action<string> onSuccess, Action<string> onError)
        {
            onError?.Invoke("STEAMWORKS_NET_MISSING");
        }

        public void CancelTicket() { }
#endif
    }
}
