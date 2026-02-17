using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    public enum DomainOption
    {
        Local,
        Production
    }

    [DefaultExecutionOrder(-100)]
    public class SaiService : SaiSingleton<SaiService>
    {
        public const string PACKAGE_VERSION = "0.0.5b4";
        public const string PACKAGE_NAME = "SaiGame Services";

        [SerializeField] protected SaiAuth saiAuth;
        [SerializeField] protected GamerProgress gamerProgress;

        [Header("Server Configuration")]
        [SerializeField] protected DomainOption domainOption = DomainOption.Local;
        [SerializeField] protected int port = 80;
        [SerializeField] protected bool useHttps = false;

        private string Domain
        {
            get
            {
                switch (domainOption)
                {
                    case DomainOption.Production:
                        return "api.saigame.studio";
                    case DomainOption.Local:
                        return "local-api.saigame.studio";
                    default:
                        return "api.saigame.studio";
                }
            }
        }

        [Header("Game Configuration")]
        [SerializeField] protected string gameId = "";

        private const string PREF_GAME_ID = "SaiGame_GameId";

        [Header("API Settings")]
        [SerializeField] protected int requestTimeout = 30;

        [Header("Debug Settings")]
        [SerializeField] protected bool showDebug = true;

        public event Action<string> OnTokenRefreshed;

        public bool ShowDebug => showDebug;

        public string BaseUrl
        {
            get
            {
                string protocol = useHttps ? "https" : "http";
                return $"{protocol}://{Domain}:{port}";
            }
        }

        public bool IsAuthenticated => saiAuth != null && saiAuth.IsAuthenticated;

        public string AccessToken => saiAuth?.AccessToken ?? "";

        public string RefreshToken => saiAuth?.RefreshToken ?? "";

        public int ExpiresIn => saiAuth?.ExpiresIn ?? 0;

        public UserData CurrentUser => saiAuth?.CurrentUser;

        public GamerProgress GamerProgress => gamerProgress;

        public SaiAuth SaiAuth => saiAuth;

        public string GameId => gameId;

        public void SetAccessToken(string token)
        {
            if (saiAuth != null)
            {
                saiAuth.SetAccessToken(token);
                OnTokenRefreshed?.Invoke(token);
            }
        }

        public void SetLoginData(string access, string refresh, int expires, UserData user = null)
        {
            if (saiAuth != null)
            {
                saiAuth.SetLoginData(access, refresh, expires, user);
                OnTokenRefreshed?.Invoke(access);
            }
        }

        protected UnityWebRequest CreateAuthenticatedRequest(string endpoint, string method = "GET")
        {
            string url = $"{BaseUrl}{endpoint}";
            UnityWebRequest request = new UnityWebRequest(url, method);

            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = requestTimeout;
            request.certificateHandler = new AllowAllCertificateHandler();

            if (IsAuthenticated)
            {
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            return request;
        }

        public IEnumerator GetRequest(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateAuthenticatedRequest(endpoint, "GET"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string rawResponse = request.downloadHandler?.text ?? "No response data";
                    string errorMsg = $"GET {endpoint} failed: {request.error}\nResponse Code: {request.responseCode}\nRaw Data: {rawResponse}";
                    onError?.Invoke(errorMsg);
                }
            }
        }

        public IEnumerator PostRequest(string endpoint, string jsonData, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateAuthenticatedRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string rawResponse = request.downloadHandler?.text ?? "No response data";
                    string errorMsg = $"POST {endpoint} failed: {request.error}\nResponse Code: {request.responseCode}\nRaw Data: {rawResponse}";
                    onError?.Invoke(errorMsg);
                }
            }
        }

        public IEnumerator PatchRequest(string endpoint, string jsonData, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateAuthenticatedRequest(endpoint, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string rawResponse = request.downloadHandler?.text ?? "No response data";
                    string errorMsg = $"PATCH {endpoint} failed: {request.error}\nResponse Code: {request.responseCode}\nRaw Data: {rawResponse}";
                    onError?.Invoke(errorMsg);
                }
            }
        }

        public IEnumerator DeleteRequest(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateAuthenticatedRequest(endpoint, "DELETE"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string rawResponse = request.downloadHandler?.text ?? "No response data";
                    string errorMsg = $"DELETE {endpoint} failed: {request.error}\nResponse Code: {request.responseCode}\nRaw Data: {rawResponse}";
                    onError?.Invoke(errorMsg);
                }
            }
        }

        public void SetDomain(DomainOption newDomain)
        {
            domainOption = newDomain;
        }

        public void SetPort(int newPort)
        {
            port = newPort;
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSaiAuth();
            this.LoadSaiGamerProgress();
            this.LoadGameIdFromPlayerPrefs();
        }

        protected virtual void LoadSaiAuth()
        {
            if (this.saiAuth != null) return;
            this.saiAuth = GetComponent<SaiAuth>();
            Debug.Log(transform.name + ": LoadSaiAuth", gameObject);
        }

        protected virtual void LoadSaiGamerProgress()
        {
            if (this.gamerProgress != null) return;
            this.gamerProgress = GetComponent<GamerProgress>();
            Debug.Log(transform.name + ": LoadSaiGamerProgress", gameObject);
        }

        protected virtual void LoadGameIdFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(PREF_GAME_ID))
            {
                this.gameId = PlayerPrefs.GetString(PREF_GAME_ID);
                if (this.showDebug)
                    Debug.Log($"Loaded Game ID from PlayerPrefs: {this.gameId}");
            }
        }

        protected virtual void SaveGameIdToPlayerPrefs()
        {
            PlayerPrefs.SetString(PREF_GAME_ID, this.gameId);
            PlayerPrefs.Save();
            if (this.showDebug)
                Debug.Log($"Saved Game ID to PlayerPrefs: {this.gameId}");
        }

        public void SetGameId(string newGameId)
        {
            this.gameId = newGameId;
            this.SaveGameIdToPlayerPrefs();
        }

        public void ManualSaveGameId()
        {
            this.SaveGameIdToPlayerPrefs();
        }

        public void ManualClearGameId()
        {
            if (PlayerPrefs.HasKey(PREF_GAME_ID))
            {
                PlayerPrefs.DeleteKey(PREF_GAME_ID);
                PlayerPrefs.Save();
                this.gameId = string.Empty;
                if (this.showDebug)
                    Debug.Log("Cleared Game ID from PlayerPrefs");
            }
        }

        public void TestConnection(Action<bool> callback = null)
        {
            StartCoroutine(TestConnectionCoroutine(callback));
        }

        private IEnumerator TestConnectionCoroutine(Action<bool> callback)
        {
            string url = $"{BaseUrl}/health";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                request.certificateHandler = new AllowAllCertificateHandler();
                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;
                callback?.Invoke(success);
            }
        }
    }
}
