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
        public const string ClaimEndpoint = "/api/v1/player/auth/steam/rewards/claim";

        [SerializeField] protected SteamLogin steamLogin;

        protected override string LogTag => "[SteamLink]";

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
            StartTicketOperation(true, onError, ticket =>
            {
                SteamTicketRequest request = NewTicketRequest(ticket);
                request.code = twoFactorCode?.Trim();
                request.recovery_code = recoveryCode?.Trim();
                return PostTicket<SteamLinkResponse>(LinkEndpoint, request, result =>
                {
                    ApplyClaimGameResult(result.ownership, result.reward);
                    if (this.steamLogin == null) this.steamLogin = GetComponent<SteamLogin>();
                    if (this.steamLogin != null && result.ownership != null) this.steamLogin.MarkLinked(result.ownership.steam_id);
                    Succeed("Link Steam", ClaimGameStatus(result.reward));
                    onSuccess?.Invoke(result);
                }, onError);
            });
        }

        // Claim Game: grants the game's Steam owner items to the signed-in account.
        public void ClaimGame(Action<SteamLinkResponse> onSuccess = null, Action<string> onError = null)
        {
            LogButton("Claim Game");
            StartTicketOperation(true, onError, ticket =>
                PostTicket<SteamLinkResponse>(ClaimEndpoint, NewTicketRequest(ticket), result =>
                {
                    ApplyClaimGameResult(result.ownership, result.reward);
                    Succeed("Claim Game", ClaimGameStatus(result.reward));
                    onSuccess?.Invoke(result);
                }, onError));
        }
    }
}
