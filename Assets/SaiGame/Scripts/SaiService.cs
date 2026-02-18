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

    public enum ServerEndpointOption
    {
        LocalHttp,
        ProductionHttps
    }

    [DefaultExecutionOrder(-100)]
    public class SaiService : SaiSingleton<SaiService>
    {
        public const string PACKAGE_VERSION = "0.0.6b1";
        public const string PACKAGE_NAME = "SaiGame Services";

        [SerializeField] protected SaiAuth saiAuth;
        [SerializeField] protected GamerProgress gamerProgress;

        [Header("Server Configuration")]
        [HideInInspector][SerializeField] protected ServerEndpointOption serverEndpoint = ServerEndpointOption.LocalHttp;
        [HideInInspector][SerializeField] protected DomainOption domainOption = DomainOption.Local;
        [HideInInspector][SerializeField] protected int port = 80;
        [HideInInspector][SerializeField] protected bool useHttps = false;

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
                switch (this.serverEndpoint)
                {
                    case ServerEndpointOption.ProductionHttps:
                        return "https://api.saigame.studio";
                    case ServerEndpointOption.LocalHttp:
                    default:
                        return "http://local-api.saigame.studio:82";
                }
            }
        }

        public bool IsAuthenticated => saiAuth != null && saiAuth.IsAuthenticated;

        public string AccessToken => saiAuth?.AccessToken ?? "";

        public string RefreshToken => saiAuth?.RefreshToken ?? "";

        public int ExpiresIn => saiAuth?.ExpiresIn ?? 0;

        public UserData CurrentUser => saiAuth?.CurrentUser;

        public GamerProgress GamerProgress => gamerProgress;

        public SaiAuth SaiAuth => saiAuth;

        public string GameId => this.NormalizeInput(this.gameId);

        private string NormalizeInput(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private void SyncLegacyServerFieldsFromEndpoint()
        {
            switch (this.serverEndpoint)
            {
                case ServerEndpointOption.ProductionHttps:
                    this.domainOption = DomainOption.Production;
                    this.useHttps = true;
                    this.port = 443;
                    break;
                case ServerEndpointOption.LocalHttp:
                default:
                    this.domainOption = DomainOption.Local;
                    this.useHttps = false;
                    this.port = 82;
                    break;
            }
        }

        private void SyncEndpointFromLegacyServerFields()
        {
            bool isProduction = this.domainOption == DomainOption.Production;
            this.serverEndpoint = isProduction
                ? ServerEndpointOption.ProductionHttps
                : ServerEndpointOption.LocalHttp;
        }

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
            this.domainOption = newDomain;
            this.SyncEndpointFromLegacyServerFields();
            this.SyncLegacyServerFieldsFromEndpoint();
        }

        public void SetPort(int newPort)
        {
            this.port = newPort;
            this.SyncEndpointFromLegacyServerFields();
            this.SyncLegacyServerFieldsFromEndpoint();
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.SyncEndpointFromLegacyServerFields();
            this.SyncLegacyServerFieldsFromEndpoint();
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
                this.gameId = this.NormalizeInput(PlayerPrefs.GetString(PREF_GAME_ID));
                if (this.showDebug)
                    Debug.Log($"Loaded Game ID from PlayerPrefs: {this.gameId}");
            }
            else
            {
                this.gameId = this.NormalizeInput(this.gameId);
            }
        }

        protected virtual void SaveGameIdToPlayerPrefs()
        {
            this.gameId = this.NormalizeInput(this.gameId);
            PlayerPrefs.SetString(PREF_GAME_ID, this.gameId);
            PlayerPrefs.Save();
            if (this.showDebug)
                Debug.Log($"Saved Game ID to PlayerPrefs: {this.gameId}");
        }

        public void SetGameId(string newGameId)
        {
            this.gameId = this.NormalizeInput(newGameId);
            this.SaveGameIdToPlayerPrefs();
        }

        protected virtual void OnValidate()
        {
            this.SyncEndpointFromLegacyServerFields();
            this.SyncLegacyServerFieldsFromEndpoint();
            this.gameId = this.NormalizeInput(this.gameId);
        }

        public void ManualSaveGameId()
        {
            Debug.Log("<color=#00FF88><b>[SaiService] ► Save Game ID to PlayerPrefs</b></color>", gameObject);
            this.SaveGameIdToPlayerPrefs();
        }

        public void ManualClearGameId()
        {
            Debug.Log("<color=#FF6666><b>[SaiService] ► Clear PlayerPrefs</b></color>", gameObject);
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
            Debug.Log("<color=#66CCFF><b>[SaiService] ► Test Connection</b></color>", gameObject);
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
