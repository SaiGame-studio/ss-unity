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

        [Header("Auto Settings")]
        [SerializeField] protected bool autoLogin = false;

        [Header("Auto Refresh Settings")]
        [SerializeField] protected bool autoRefreshToken = true;
        [SerializeField] protected int refreshBeforeExpire = 2;

        [SerializeField] protected string username = "";
        [SerializeField] protected string password = "";

        [SerializeField] protected string registerEmail = "";
        [SerializeField] protected string registerUsername = "";
        [SerializeField] protected string registerPassword = "";

        private const string PREF_EMAIL = "SaiGame_SavedEmail";
        private const string PREF_PASSWORD = "SaiGame_SavedPassword";
        private const string PREF_AUTO_LOGIN_FLAG = "SaiGame_AutoLoginFlag";

        public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken);
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public int ExpiresIn => expiresIn;
        public UserData CurrentUser => userData;

        private string NormalizeInput(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private Coroutine tokenExpirationChecker;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        protected override void ResetValue()
        {
            base.ResetValue();
        }

        protected override void Start()
        {
            if (this.autoLogin) this.AutoLogin();
        }

        public void AutoLogin()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[SaiAuth] ► Auto Login</b></color>", gameObject);

            this.LoadUsername();
            this.LoadPassword();

            if (string.IsNullOrEmpty(this.username) || string.IsNullOrEmpty(this.password))
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.LogWarning("<color=#00FFFF>[SaiAuth] Auto-login skipped: missing username or password</color>");
                return;
            }

            this.Login(this.username, this.password,
                response =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"<color=#00FFFF>[SaiAuth] Auto-login successful! Welcome {response.user.username}</color>");
                },
                error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"<color=#00FFFF>[SaiAuth] Auto-login failed: {error}</color>");
                }
            );
        }

        public void SaveUsername()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[SaiAuth] ► Save Username</b></color>", gameObject);

            this.username = this.NormalizeInput(this.username);
            PlayerPrefs.SetString(PREF_EMAIL, this.username);
            PlayerPrefs.Save();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"<color=#FFD700>[SaiAuth] Saved username to PlayerPrefs: {this.username}</color>");
        }

        public void LoadUsername()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#7FFFD4><b>[SaiAuth] ► Load Username</b></color>", gameObject);

            if (!PlayerPrefs.HasKey(PREF_EMAIL))
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.Log("<color=#7FFFD4>[SaiAuth] No saved username found in PlayerPrefs</color>");
                return;
            }

            this.username = this.NormalizeInput(PlayerPrefs.GetString(PREF_EMAIL));

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log($"<color=#7FFFD4>[SaiAuth] Loaded username from PlayerPrefs: {this.username}</color>");
        }

        public void SavePassword()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFA500><b>[SaiAuth] ► Save Password</b></color>", gameObject);

            this.password = this.NormalizeInput(this.password);
            string encryptedPassword = SaiEncryption.Encrypt(this.password);
            PlayerPrefs.SetString(PREF_PASSWORD, encryptedPassword);
            PlayerPrefs.Save();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("<color=#FFA500>[SaiAuth] Saved encrypted password to PlayerPrefs</color>");
        }

        public void LoadPassword()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF69B4><b>[SaiAuth] ► Load Password</b></color>", gameObject);

            if (!PlayerPrefs.HasKey(PREF_PASSWORD))
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.Log("<color=#FF69B4>[SaiAuth] No saved password found in PlayerPrefs</color>");
                return;
            }

            string encryptedPassword = PlayerPrefs.GetString(PREF_PASSWORD);
            this.password = this.NormalizeInput(SaiEncryption.Decrypt(encryptedPassword));

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("<color=#FF69B4>[SaiAuth] Loaded password from PlayerPrefs</color>");
        }

        protected virtual void OnDestroy()
        {
            StopTokenExpirationCheck();
        }

        private void StartTokenExpirationCheck()
        {
            this.StopTokenExpirationCheck();
            if (this.autoRefreshToken && this.IsAuthenticated)
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
            while (this.IsAuthenticated)
            {
                float elapsedTime = Time.time - this.loginTime;
                float timeUntilExpire = this.expiresIn - elapsedTime;

                if (timeUntilExpire <= this.refreshBeforeExpire && timeUntilExpire > 0)
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"Auto-refreshing token... (expires in {timeUntilExpire:F1}s)");

                    RefreshAuthToken(
                        response =>
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                Debug.Log("Token auto-refreshed successfully!");
                        },
                        error =>
                        {
                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
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
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#66AAFF><b>[SaiAuth] ► Register</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            string normalizedEmail = this.NormalizeInput(email);
            string normalizedPassword = this.NormalizeInput(password);

            StartCoroutine(RegisterCoroutine(normalizedEmail, username, normalizedPassword, onSuccess, onError));
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

            yield return SaiServer.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        RegisterResponse registerResponse = JsonUtility.FromJson<RegisterResponse>(response);

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Registration successful! User: {registerResponse.user.username} ({registerResponse.user.email})");

                        OnRegisterSuccess?.Invoke(registerResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[SaiAuth] Register</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiAuth.cs › RegisterCoroutine");
                        onSuccess?.Invoke(registerResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse register response error: {e.Message}";
                        OnRegisterFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[SaiAuth] Register</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiAuth.cs › RegisterCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnRegisterFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[SaiAuth] Register</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiAuth.cs › RegisterCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void Login(string username, string password, System.Action<LoginResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FF88><b>[SaiAuth] ► Login</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            string normalizedUsername = this.NormalizeInput(username);
            string normalizedPassword = this.NormalizeInput(password);

            StartCoroutine(LoginCoroutine(normalizedUsername, normalizedPassword, onSuccess, onError));
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

            yield return SaiServer.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(response);
                        SaiServer.Instance.SetLoginData(loginResponse.access_token, loginResponse.refresh_token, loginResponse.expires_in, loginResponse.user);

                        this.accessToken = loginResponse.access_token;
                        this.refreshToken = loginResponse.refresh_token;
                        this.expiresIn = loginResponse.expires_in;
                        this.userData = loginResponse.user;
                        this.loginTime = Time.time;

                        this.username = username;
                        this.password = password;

                        StartTokenExpirationCheck();

                        OnLoginSuccess?.Invoke(loginResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[SaiAuth] Login</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiAuth.cs › LoginCoroutine");
                        onSuccess?.Invoke(loginResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse login response error: {e.Message}";
                        OnLoginFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[SaiAuth] Login</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiAuth.cs › LoginCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnLoginFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[SaiAuth] Login</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiAuth.cs › LoginCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void RefreshAuthToken(System.Action<LoginResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#66CCFF><b>[SaiAuth] ► Refresh Token</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
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

            yield return SaiServer.Instance.PostRequest(endpoint, jsonData,
                response =>
                {
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(response);
                        SaiServer.Instance.SetLoginData(loginResponse.access_token, loginResponse.refresh_token, loginResponse.expires_in, loginResponse.user);

                        this.accessToken = loginResponse.access_token;
                        this.refreshToken = loginResponse.refresh_token;
                        this.expiresIn = loginResponse.expires_in;
                        this.userData = loginResponse.user;
                        this.loginTime = Time.time;

                        StartTokenExpirationCheck();

                        GetMyProfile(
                            userData =>
                            {
                                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                    Debug.Log($"User data refreshed after token refresh: {userData.username}");
                            },
                            error =>
                            {
                                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                                    Debug.LogWarning($"Failed to refresh user data after token refresh: {error}");
                            }
                        );

                        OnRefreshTokenSuccess?.Invoke(loginResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[SaiAuth] RefreshToken</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiAuth.cs › RefreshTokenCoroutine");
                        onSuccess?.Invoke(loginResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse refresh token response error: {e.Message}";
                        OnRefreshTokenFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[SaiAuth] RefreshToken</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiAuth.cs › RefreshTokenCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnRefreshTokenFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[SaiAuth] RefreshToken</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiAuth.cs › RefreshTokenCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void Logout()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF9944><b>[SaiAuth] ► Logout</b></color>", gameObject);
            StartCoroutine(LogoutCoroutine());
        }

        private IEnumerator LogoutCoroutine()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.IsAuthenticated)
            {
                yield return StartCoroutine(SaiServer.Instance.PostRequest("/api/v1/auth/logout", "{}",
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

            if (SaiServer.Instance != null)
            {
                SaiServer.Instance.SetLoginData("", "", 0, null);
            }

            this.accessToken = "";
            this.refreshToken = "";
            this.expiresIn = 0;
            this.userData = null;
            this.loginTime = 0;
        }

        public void GetMyProfile(System.Action<UserData> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#AAFFAA><b>[SaiAuth] ► Get Me</b></color>", gameObject);
            if (SaiServer.Instance == null || !SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(GetMyProfileCoroutine(onSuccess, onError));
        }

        private IEnumerator GetMyProfileCoroutine(System.Action<UserData> onSuccess, System.Action<string> onError)
        {
            string endpoint = "/api/v1/auth/me";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        GetMeResponse meResponse = JsonUtility.FromJson<GetMeResponse>(response);
                        this.userData = meResponse.user;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Profile loaded: {userData.username} ({userData.email})");

                        OnGetProfileSuccess?.Invoke(userData);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[SaiAuth] GetMyProfile</color> → <b><color=#00FF88>onSuccess</color></b> callback | SaiAuth.cs › GetMyProfileCoroutine");
                        onSuccess?.Invoke(userData);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get profile response error: {e.Message}";
                        OnGetProfileFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[SaiAuth] GetMyProfile</color> → <b><color=#FF4444>onError</color></b> callback (parse) | SaiAuth.cs › GetMyProfileCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetProfileFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[SaiAuth] GetMyProfile</color> → <b><color=#FF4444>onError</color></b> callback (network) | SaiAuth.cs › GetMyProfileCoroutine | {error}");
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

        public void SetAutoLogin(bool auto)
        {
            this.autoLogin = auto;
            PlayerPrefs.SetInt(PREF_AUTO_LOGIN_FLAG, this.autoLogin ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool GetAutoLogin()
        {
            return this.autoLogin;
        }

        public string GetUsername()
        {
            return this.username;
        }

        public string GetPassword()
        {
            return this.password;
        }

        public void ManualClearCredentials()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[SaiAuth] ► Clear PlayerPrefs</b></color>", gameObject);
            PlayerPrefs.DeleteKey(PREF_EMAIL);
            PlayerPrefs.DeleteKey(PREF_PASSWORD);
            PlayerPrefs.DeleteKey(PREF_AUTO_LOGIN_FLAG);
            PlayerPrefs.Save();

            this.username = string.Empty;
            this.password = string.Empty;
            this.autoLogin = false;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("Cleared all credentials from PlayerPrefs");
        }

        protected virtual void OnValidate()
        {
            this.username = this.NormalizeInput(this.username);
            this.password = this.NormalizeInput(this.password);
            this.registerEmail = this.NormalizeInput(this.registerEmail);
            this.registerPassword = this.NormalizeInput(this.registerPassword);
        }
    }
}
