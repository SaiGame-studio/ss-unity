using System;
using UnityEngine;

namespace SaiGame.Services
{
    // Steam is not a separate login: the Steam client running the game already
    // identifies the player. Link links that Steam account once to the signed-in
    // Sai account. Signing in with the linked account is SteamLogin.
    [DefaultExecutionOrder(-99)]
    public class SteamLink : SteamTicketService
    {
        public const string LinkEndpoint = "/api/v1/player/auth/steam/link";

        [SerializeField] protected SteamLogin steamLogin;

        protected override string LogTag => "[SteamLink]";
        public bool CanLink => steamLogin != null && !steamLogin.IsBusy
            && steamLogin.LinkState == SteamLinkState.NotLinked;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (this.steamLogin == null) this.steamLogin = GetComponent<SteamLogin>();
        }

        // Links the running Steam account to the signed-in Sai account.
        // Accounts with 2FA pass an Authenticator code or a recovery code.
        // Accounts without 2FA must have signed in within the backend's
        // re-authentication window, otherwise STEAM_LINK_REAUTH_REQUIRED.
        public void Link(Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null,
            string twoFactorCode = null, string recoveryCode = null)
        {
            LogButton("Link Steam");
            if (!CanLink)
            {
                Fail(this.steamLogin != null && this.steamLogin.LinkState == SteamLinkState.Linked
                    ? "STEAM_ALREADY_LINKED" : "STEAM_LINK_CHECK_REQUIRED", onError);
                return;
            }
            StartTicketOperation(true, onError, ticket =>
            {
                SteamTicketRequest request = NewTicketRequest(ticket);
                request.code = twoFactorCode?.Trim();
                request.recovery_code = recoveryCode?.Trim();
                return PostTicket<SteamLinkResponse>(LinkEndpoint, request, result =>
                {
                    ClearResult();
                    this.ownership = result.ownership;
                    if (this.steamLogin == null) this.steamLogin = GetComponent<SteamLogin>();
                    if (this.steamLogin != null && result.ownership != null) this.steamLogin.MarkLinked(result.ownership.steam_id);
                    Succeed("Link Steam", "linked: sign in with SteamLogin to start the game session");
                    onSuccess?.Invoke(result);
                }, onError);
            });
        }

    }
}
