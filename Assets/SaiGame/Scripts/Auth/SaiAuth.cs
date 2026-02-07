using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    public class SaiAuth : SaiBehaviour
    {
        [SerializeField] protected SaiService saiService;

        // Events for other classes to listen to
        public event Action<LoginResponse> OnLoginSuccess;
        public event Action<string> OnLoginFailure;
        public event Action<LoginResponse> OnRefreshTokenSuccess;
        public event Action<string> OnRefreshTokenFailure;
        public event Action<UserData> OnGetProfileSuccess;
        public event Action<string> OnGetProfileFailure;
        public event Action OnLogoutSuccess;
        public event Action<string> OnLogoutFailure;


        [Header("Authentication Data")]
        [SerializeField] protected string accessToken;
        [SerializeField] protected string refreshToken;
        [SerializeField] protected int expiresIn;
        [SerializeField] protected UserData userData;
        [SerializeField] protected float loginTime;

        [Header("Auto Refresh Settings")]
        [SerializeField] protected bool autoRefreshToken = true;
        [SerializeField] protected int refreshBeforeExpire = 2;

        [Header("Login Inputs")]
        [SerializeField] protected string username = "SimonSai@saigame.studio";
        [SerializeField] protected string password = "123qweasd";

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;

        private Coroutine tokenExpirationChecker;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSaiService();
        }

        protected virtual void LoadSaiService()
        {
            if (this.saiService != null) return;
            this.saiService = GetComponent<SaiService>();
            if (this.saiService != null && this.saiService.ShowDebug)
                Debug.Log(transform.name + ": LoadSaiService", gameObject);
        }

        protected virtual void OnDestroy()
        {
            StopTokenExpirationCheck();
        }

        private void StartTokenExpirationCheck()
        {
            StopTokenExpirationCheck();
            if (autoRefreshToken && IsAuthenticated)
            {
                tokenExpirationChecker = StartCoroutine(CheckTokenExpiration());
            }
        }

        private void StopTokenExpirationCheck()
        {
            if (tokenExpirationChecker != null)
            {
                StopCoroutine(tokenExpirationChecker);
                tokenExpirationChecker = null;
            }
        }

        private IEnumerator CheckTokenExpiration()
        {
            while (IsAuthenticated)
            {
                float elapsedTime = Time.time - loginTime;
                float timeUntilExpire = expiresIn - elapsedTime;

                if (timeUntilExpire <= refreshBeforeExpire && timeUntilExpire > 0)
                {
                    if (saiService != null && saiService.ShowDebug)
                        Debug.Log($"Auto-refreshing token... (expires in {timeUntilExpire:F1}s)");

                    RefreshAuthToken(
                        response => 
                        {
                            if (saiService != null && saiService.ShowDebug)
                                Debug.Log("Token auto-refreshed successfully!");
                        },
                        error => 
                        {
                            if (saiService != null && saiService.ShowDebug)
                                Debug.LogError($"Auto-refresh failed: {error}");
                        }
                    );

                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
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
                        this.loginTime = Time.time;

                        StartTokenExpirationCheck();

                        OnLoginSuccess?.Invoke(loginResponse);
                        onSuccess?.Invoke(loginResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse login response error: {e.Message}";
                        OnLoginFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error => 
                {
                    OnLoginFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void RefreshAuthToken(System.Action<LoginResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (saiService == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (string.IsNullOrEmpty(this.refreshToken))
            {
                onError?.Invoke("No refresh token available! Please login first.");
                return;
            }

            StartCoroutine(RefreshTokenCoroutine(onSuccess, onError));
        }

        private IEnumerator RefreshTokenCoroutine(System.Action<LoginResponse> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/refresh";

            RefreshTokenRequest refreshRequest = new RefreshTokenRequest
            {
                refresh_token = this.refreshToken
            };

            string jsonData = JsonUtility.ToJson(refreshRequest);

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
                        this.loginTime = Time.time;

                        StartTokenExpirationCheck();

                        GetMyProfile(
                            userData => 
                            {
                                if (saiService != null && saiService.ShowDebug)
                                    Debug.Log($"User data refreshed after token refresh: {userData.username}");
                            },
                            error => 
                            {
                                if (saiService != null && saiService.ShowDebug)
                                    Debug.LogWarning($"Failed to refresh user data after token refresh: {error}");
                            }
                        );

                        OnRefreshTokenSuccess?.Invoke(loginResponse);
                        onSuccess?.Invoke(loginResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse refresh token response error: {e.Message}";
                        OnRefreshTokenFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnRefreshTokenFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void Logout()
        {
            StartCoroutine(LogoutCoroutine());
        }

        private IEnumerator LogoutCoroutine()
        {
            if (saiService != null && saiService.IsAuthenticated)
            {
                yield return StartCoroutine(saiService.PostRequest("/api/v1/auth/logout", "{}", 
                    response => 
                    {
                        // Logout successful from server
                        ClearAuthData();
                        OnLogoutSuccess?.Invoke();
                    },
                    error => 
                    {
                        // Even if server logout fails, clear local data
                        ClearAuthData();
                        OnLogoutFailure?.Invoke(error);
                    }
                ));
            }
            else
            {
                // No authentication, just clear local data
                ClearAuthData();
                OnLogoutSuccess?.Invoke();
            }
        }

        private void ClearAuthData()
        {
            StopTokenExpirationCheck();

            if (saiService != null)
            {
                saiService.SetLoginData("", "", 0, null);
            }

            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.loginTime = 0;
        }

        public void GetMyProfile(System.Action<UserData> onSuccess = null, System.Action<string> onError = null)
        {
            if (saiService == null || !saiService.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(GetMyProfileCoroutine(onSuccess, onError));
        }

        private IEnumerator GetMyProfileCoroutine(System.Action<UserData> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/me";

            yield return saiService.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        GetMeResponse meResponse = JsonUtility.FromJson<GetMeResponse>(response);
                        this.userData = meResponse.user;

                        if (saiService != null && saiService.ShowDebug)
                            Debug.Log($"Profile loaded: {userData.username} ({userData.email})");

                        OnGetProfileSuccess?.Invoke(userData);
                        onSuccess?.Invoke(userData);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get profile response error: {e.Message}";
                        OnGetProfileFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetProfileFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
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
