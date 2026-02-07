using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    public class SaiAuth : SaiBehaviour
    {
        [SerializeField] protected SaiService saiService;


        [Header("Authentication Data")]
        [SerializeField] protected string accessToken;
        [SerializeField] protected string refreshToken;
        [SerializeField] protected int expiresIn;
        [SerializeField] protected UserData userData;

        [Header("Login Inputs")]
        [SerializeField] protected string username = "SimonSai@saigame.studio";
        [SerializeField] protected string password = "123qweasd";

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSaiService();
        }

        protected virtual void LoadSaiService()
        {
            if (this.saiService != null) return;
            this.saiService = GetComponent<SaiService>();
            Debug.Log(transform.name + ": LoadSaiService", gameObject);
        }

        public void Login(string username, string password, System.Action<LoginResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (saiService == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            StartCoroutine(LoginCoroutine(username, password, onSuccess, onError));
        }

        private IEnumerator LoginCoroutine(string username, string password, System.Action<LoginResponse> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/login";

            LoginRequest loginRequest = new LoginRequest
            {
                username = username,
                password = password
            };

            string jsonData = JsonUtility.ToJson(loginRequest);

            yield return saiService.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(response);
                        saiService.SetLoginData(loginResponse.access_token, loginResponse.refresh_token, loginResponse.expires_in, loginResponse.user);

                        this.accessToken = loginResponse.access_token;
                        this.refreshToken = loginResponse.refresh_token;
                        this.expiresIn = loginResponse.expires_in;
                        this.userData = loginResponse.user;

                        onSuccess?.Invoke(loginResponse);
                    }
                    catch (System.Exception e)
                    {
                        onError?.Invoke($"Parse login response error: {e.Message}");
                    }
                },
                error => onError?.Invoke(error)
            );
        }

        public void Logout()
        {
            if (saiService != null)
            {
                saiService.SetLoginData("", "", 0, null);
            }

            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
        }

        public void GetMyProfile(System.Action<string> onSuccess, System.Action<string> onError)
        {
            if (saiService == null || !saiService.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(saiService.GetRequest("/api/v1/auth/me", onSuccess, onError));
        }

        public void SetAccessToken(string token)
        {
            this.accessToken = token;
        }

        public void SetLoginData(string access, string refresh, int expires, UserData user = null)
        {
            this.accessToken = access;
            this.refreshToken = refresh;
            this.expiresIn = expires;
            this.userData = user;
        }
    }
}
