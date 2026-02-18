using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to listen to SaiAuth events
    /// </summary>
    public class AuthEventListener : SaiBehaviour
    {
        private SaiAuth saiAuth => SaiService.Instance?.GetComponent<SaiAuth>();

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        private void OnEnable()
        {
            if (saiAuth != null)
            {
                // Subscribe to authentication events
                saiAuth.OnLoginSuccess += HandleLoginSuccess;
                saiAuth.OnLoginFailure += HandleLoginFailure;
                saiAuth.OnLogoutSuccess += HandleLogoutSuccess;
                saiAuth.OnLogoutFailure += HandleLogoutFailure;
            }
        }

        private void OnDisable()
        {
            if (saiAuth != null)
            {
                // Unsubscribe from authentication events
                saiAuth.OnLoginSuccess -= HandleLoginSuccess;
                saiAuth.OnLoginFailure -= HandleLoginFailure;
                saiAuth.OnLogoutSuccess -= HandleLogoutSuccess;
                saiAuth.OnLogoutFailure -= HandleLogoutFailure;
            }
        }

        private void HandleLoginSuccess(LoginResponse response)
        {
            // Handle successful login
            // Example: Update UI, show welcome message, etc.
        }

        private void HandleLoginFailure(string error)
        {
            // Handle login failure
            // Example: Show error message to user
        }

        private void HandleLogoutSuccess()
        {
            // Handle successful logout
            // Example: Clear UI data, redirect to login screen
        }

        private void HandleLogoutFailure(string error)
        {
            // Handle logout failure
            // Example: Show warning but still treat as logged out
        }
    }
}