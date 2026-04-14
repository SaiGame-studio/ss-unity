using UnityEngine;
using UnityEngine.UIElements;
using SaiGame.Services;

namespace SaiGame.UI
{
    // Login panel — binds UI to SaiAuth, no auth logic inside.
    public class LoginPanelUI : UIPanelBase
    {
        public override string PanelId => "Login";

        [Header("References")]
        [SerializeField] private SaiAuth saiAuth;

        private TextField usernameField;
        private TextField passwordField;
        private Button loginButton;

        protected override void LoadComponents()
        {
            base.LoadComponents();

            if (this.saiAuth == null)
                this.saiAuth = this.GetComponentInParent<SaiAuth>();
        }

        protected override void OnBindElements(VisualElement root)
        {
            this.usernameField = this.Q<TextField>("UsernameField");
            this.passwordField = this.Q<TextField>("PasswordField");
            this.loginButton   = this.Q<Button>("LoginButton");

            this.loginButton.clicked += this.OnLoginButtonClicked;

            this.SubscribeToAuthEvents();
        }

        protected override void OnShow()
        {
            if (this.usernameField == null || this.saiAuth == null) return;

            this.usernameField.SetValueWithoutNotify(this.saiAuth.GetUsername());
            this.passwordField.SetValueWithoutNotify(this.saiAuth.GetPassword());

            this.HideFeedback();
        }

        private void SubscribeToAuthEvents()
        {
            if (this.saiAuth == null) return;
            this.saiAuth.OnLoginSuccess += this.HandleLoginSuccess;
            this.saiAuth.OnLoginFailure += this.HandleLoginFailure;
        }

        private void UnsubscribeFromAuthEvents()
        {
            if (this.saiAuth == null) return;
            this.saiAuth.OnLoginSuccess -= this.HandleLoginSuccess;
            this.saiAuth.OnLoginFailure -= this.HandleLoginFailure;
        }

        private void OnLoginButtonClicked()
        {
            this.HideFeedback();
            this.loginButton.SetEnabled(false);

            this.saiAuth?.Login(
                this.usernameField.value,
                this.passwordField.value,
                onSuccess: _ => this.loginButton.SetEnabled(true),
                onError:   _ => this.loginButton.SetEnabled(true));
        }

        private void HandleLoginSuccess(LoginResponse response)
        {
            this.ShowFeedback($"Welcome, {response.user?.username}!", isError: false);
            UIRouter.Instance?.ShowPanel("GamerProgress", addToHistory: false);
        }

        private void HandleLoginFailure(string error)
        {
            this.ShowFeedback(error, isError: true);
        }

        protected virtual void OnDestroy()
        {
            this.UnsubscribeFromAuthEvents();

            if (this.loginButton != null)
                this.loginButton.clicked -= this.OnLoginButtonClicked;
        }
    }
}
