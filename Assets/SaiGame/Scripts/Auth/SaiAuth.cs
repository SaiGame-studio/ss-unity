using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class SaiAuth : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<LoginResponse> OnLoginSuccess;
        public event Action<string> OnLoginFailure;
        public event Action<RegisterResponse> OnRegisterSuccess;
        public event Action<string> OnRegisterFailure;
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
        [SerializeField] protected string username = "";
        [SerializeField] protected string password = "";
        [SerializeField] protected bool saveEmail = false;
        [SerializeField] protected bool savePassword = false;

        [Header("Register Inputs")]
        [SerializeField] protected string registerEmail = "";
        [SerializeField] protected string registerUsername = "";
        [SerializeField] protected string registerPassword = "";

        private const string PREF_EMAIL = "SaiGame_SavedEmail";
        private const string PREF_PASSWORD = "SaiGame_SavedPassword";
        private const string PREF_SAVE_EMAIL_FLAG = "SaiGame_SaveEmailFlag";
        private const string PREF_SAVE_PASSWORD_FLAG = "SaiGame_SavePasswordFlag";

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;

        private Coroutine tokenExpirationChecker;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadCredentialsFromPlayerPrefs();
        }

        protected virtual void LoadCredentialsFromPlayerPrefs()
        {
            // If there's already a SaiService singleton instance with authenticated SaiAuth, skip loading
            if (SaiService.Instance != null && this.GetComponent<SaiService>() != SaiService.Instance)
            {
                SaiAuth existingAuth = SaiService.Instance.GetComponent<SaiAuth>();
                if (existingAuth != null && existingAuth.IsAuthenticated)
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log("[SaiAuth] Singleton instance already authenticated, skipping credential load");
                    return;
                }
            }

            this.saveEmail = PlayerPrefs.GetInt(PREF_SAVE_EMAIL_FLAG, 0) == 1;
            this.savePassword = PlayerPrefs.GetInt(PREF_SAVE_PASSWORD_FLAG, 0) == 1;

            if (this.saveEmail && PlayerPrefs.HasKey(PREF_EMAIL))
            {
                this.username = PlayerPrefs.GetString(PREF_EMAIL);
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log($"[SaiAuth] Loaded email from PlayerPrefs: {this.username}");
            }

            if (this.savePassword && PlayerPrefs.HasKey(PREF_PASSWORD))
            {
                string encryptedPassword = PlayerPrefs.GetString(PREF_PASSWORD);
                this.password = SaiEncryption.Decrypt(encryptedPassword);
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log("[SaiAuth] Loaded password from PlayerPrefs");
            }
        }

        protected virtual void SaveCredentialsToPlayerPrefs()
        {
            PlayerPrefs.SetInt(PREF_SAVE_EMAIL_FLAG, this.saveEmail ? 1 : 0);
            PlayerPrefs.SetInt(PREF_SAVE_PASSWORD_FLAG, this.savePassword ? 1 : 0);

            if (this.saveEmail)
            {
                PlayerPrefs.SetString(PREF_EMAIL, this.username);
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log($"Saved email to PlayerPrefs: {this.username}");
            }
            else
            {
                PlayerPrefs.DeleteKey(PREF_EMAIL);
            }

            if (this.savePassword)
            {
                string encryptedPassword = SaiEncryption.Encrypt(this.password);
                PlayerPrefs.SetString(PREF_PASSWORD, encryptedPassword);
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log("Saved encrypted password to PlayerPrefs");
            }
            else
            {
                PlayerPrefs.DeleteKey(PREF_PASSWORD);
            }

            PlayerPrefs.Save();
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
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"Auto-refreshing token... (expires in {timeUntilExpire:F1}s)");

                    RefreshAuthToken(
                        response =>
                        {
                            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                                Debug.Log("Token auto-refreshed successfully!");
                        },
                        error =>
                        {
                            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                                Debug.LogError($"Auto-refresh failed: {error}");
                        }
                    );

                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
        }

        public void Register(string email, string username, string password, System.Action<RegisterResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            StartCoroutine(RegisterCoroutine(email, username, password, onSuccess, onError));
        }

        private IEnumerator RegisterCoroutine(string email, string username, string password, System.Action<RegisterResponse> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/register";

            RegisterRequest registerRequest = new RegisterRequest
            {
                email = email,
                username = username,
                password = password
            };

            string jsonData = JsonUtility.ToJson(registerRequest);

            yield return SaiService.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        RegisterResponse registerResponse = JsonUtility.FromJson<RegisterResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Registration successful! User: {registerResponse.user.username} ({registerResponse.user.email})");

                        OnRegisterSuccess?.Invoke(registerResponse);
                        onSuccess?.Invoke(registerResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse register response error: {e.Message}";
                        OnRegisterFailure?.Invoke(errorMsg);
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnRegisterFailure?.Invoke(error);
                    onError?.Invoke(error);
                }
            );
        }

        public void Login(string username, string password, System.Action<LoginResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance == null)
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

            yield return SaiService.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(response);
                        SaiService.Instance.SetLoginData(loginResponse.access_token, loginResponse.refresh_token, loginResponse.expires_in, loginResponse.user);

                        this.accessToken = loginResponse.access_token;
                        this.refreshToken = loginResponse.refresh_token;
                        this.expiresIn = loginResponse.expires_in;
                        this.userData = loginResponse.user;
                        this.loginTime = Time.time;

                        this.username = username;
                        this.password = password;
                        this.SaveCredentialsToPlayerPrefs();

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
            if (SaiService.Instance == null)
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

            yield return SaiService.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(response);
                        SaiService.Instance.SetLoginData(loginResponse.access_token, loginResponse.refresh_token, loginResponse.expires_in, loginResponse.user);

                        this.accessToken = loginResponse.access_token;
                        this.refreshToken = loginResponse.refresh_token;
                        this.expiresIn = loginResponse.expires_in;
                        this.userData = loginResponse.user;
                        this.loginTime = Time.time;

                        StartTokenExpirationCheck();

                        GetMyProfile(
                            userData =>
                            {
                                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                                    Debug.Log($"User data refreshed after token refresh: {userData.username}");
                            },
                            error =>
                            {
                                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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
            if (SaiService.Instance != null && SaiService.Instance.IsAuthenticated)
            {
                yield return StartCoroutine(SaiService.Instance.PostRequest("/api/v1/auth/logout", "{}",
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

            if (SaiService.Instance != null)
            {
                SaiService.Instance.SetLoginData("", "", 0, null);
            }

            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.loginTime = 0;

            if (!this.saveEmail)
            {
                this.username = "";
            }

            if (!this.savePassword)
            {
                this.password = "";
            }
        }

        public void GetMyProfile(System.Action<UserData> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance == null || !SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(GetMyProfileCoroutine(onSuccess, onError));
        }

        private IEnumerator GetMyProfileCoroutine(System.Action<UserData> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/me";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        GetMeResponse meResponse = JsonUtility.FromJson<GetMeResponse>(response);
                        this.userData = meResponse.user;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
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

        public void SetSaveEmail(bool save)
        {
            this.saveEmail = save;
            this.SaveCredentialsToPlayerPrefs();
        }

        public void SetSavePassword(bool save)
        {
            this.savePassword = save;
            this.SaveCredentialsToPlayerPrefs();
        }

        public bool GetSaveEmail()
        {
            return this.saveEmail;
        }

        public bool GetSavePassword()
        {
            return this.savePassword;
        }

        public void ManualSaveCredentials()
        {
            this.SaveCredentialsToPlayerPrefs();
        }

        public void ManualClearCredentials()
        {
            PlayerPrefs.DeleteKey(PREF_EMAIL);
            PlayerPrefs.DeleteKey(PREF_PASSWORD);
            PlayerPrefs.DeleteKey(PREF_SAVE_EMAIL_FLAG);
            PlayerPrefs.DeleteKey(PREF_SAVE_PASSWORD_FLAG);
            PlayerPrefs.Save();

            this.username = string.Empty;
            this.password = string.Empty;
            this.saveEmail = false;
            this.savePassword = false;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("Cleared all credentials from PlayerPrefs");
        }
    }
}
