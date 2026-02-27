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
        public const string PACKAGE_VERSION = "0.1.0b1";
        public const string PACKAGE_NAME = "SaiGame Services";

        [SerializeField] protected SaiAuth saiAuth;
        [SerializeField] protected GamerProgress gamerProgress;
        [SerializeField] protected Mailbox mailbox;
        [SerializeField] protected PlayerItem playerItem;
        [SerializeField] protected PlayerContainer playerContainer;

        [Header("Server Configuration")]
        [HideInInspector][SerializeField] protected ServerEndpointOption serverEndpoint = ServerEndpointOption.LocalHttp;
        [HideInInspector][SerializeField] protected DomainOption domainOption = DomainOption.Local;
        [HideInInspector][SerializeField] protected int port = 80;
        [HideInInspector][SerializeField] protected bool useHttps = false;

        [Header("Game Configuration")]
        [SerializeField] protected string gameId = "";

        private const string PREF_GAME_ID = "SaiGame_GameId";
        private const string PREF_SERVER_ENDPOINT = "SaiGame_ServerEndpoint";

        [Header("API Settings")]
        [SerializeField] protected int requestTimeout = 30;

        [SerializeField] protected bool showButtonsLog = true;
        [SerializeField] protected bool showCallbackLog = true;
        [SerializeField] protected bool showDebugLog = true;

        public event Action<string> OnTokenRefreshed;

        public bool ShowDebug => showDebugLog;

        public bool ShowButtonsLog => showButtonsLog;

        public bool ShowCallbackLog => showCallbackLog;

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

        public PlayerItem PlayerItem => playerItem;

        public PlayerContainer PlayerContainer => playerContainer;

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
            this.LoadServerEndpointFromPlayerPrefs();
            this.SyncLegacyServerFieldsFromEndpoint();
            this.LoadSaiAuth();
            this.LoadGamerProgress();
            this.LoadMailbox();
            this.LoadPlayerItem();
            this.LoadPlayerContainer();
            this.LoadGameIdFromPlayerPrefs();
        }


        protected virtual void LoadSaiAuth()
        {
            if (this.saiAuth != null) return;
            this.saiAuth = GetComponent<SaiAuth>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadSaiAuth", gameObject);
        }

        protected virtual void LoadMailbox()
        {
            if (this.mailbox != null) return;
            this.mailbox = GetComponent<Mailbox>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadMailbox", gameObject);
        }

        protected virtual void LoadGamerProgress()
        {
            if (this.gamerProgress != null) return;
            this.gamerProgress = GetComponent<GamerProgress>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadSaiGamerProgress", gameObject);
        }

        protected virtual void LoadPlayerItem()
        {
            if (this.playerItem != null) return;
            this.playerItem = GetComponent<PlayerItem>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadPlayerItem", gameObject);
        }

        protected virtual void LoadPlayerContainer()
        {
            if (this.playerContainer != null) return;
            this.playerContainer = GetComponent<PlayerContainer>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadPlayerContainer", gameObject);
        }




        protected virtual void LoadServerEndpointFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(PREF_SERVER_ENDPOINT))
            {
                this.serverEndpoint = (ServerEndpointOption)PlayerPrefs.GetInt(PREF_SERVER_ENDPOINT);
                this.SyncLegacyServerFieldsFromEndpoint();
                if (this.showDebugLog)
                    Debug.Log($"Loaded Server Endpoint from PlayerPrefs: {this.serverEndpoint}");
            }
        }

        protected virtual void SaveServerEndpointToPlayerPrefs()
        {
            PlayerPrefs.SetInt(PREF_SERVER_ENDPOINT, (int)this.serverEndpoint);
            PlayerPrefs.Save();
            if (this.showDebugLog)
                Debug.Log($"Saved Server Endpoint to PlayerPrefs: {this.serverEndpoint}");
        }

        public void ManualSaveServerEndpoint()
        {
            if (this.showButtonsLog)
                Debug.Log("<color=#00FF88><b>[SaiService] ► Save Server Endpoint to PlayerPrefs</b></color>", gameObject);
            this.SaveServerEndpointToPlayerPrefs();
        }

        protected virtual void LoadGameIdFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(PREF_GAME_ID))
            {
                this.gameId = this.NormalizeInput(PlayerPrefs.GetString(PREF_GAME_ID));
                if (this.showDebugLog)
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
            if (this.showDebugLog)
                Debug.Log($"Saved Game ID to PlayerPrefs: {this.gameId}");
        }

        public void SetGameId(string newGameId)
        {
            this.gameId = this.NormalizeInput(newGameId);
            this.SaveGameIdToPlayerPrefs();
        }

        protected virtual void OnValidate()
        {
            // serverEndpoint is the source of truth; sync legacy fields from it only.
            this.SyncLegacyServerFieldsFromEndpoint();
            this.gameId = this.NormalizeInput(this.gameId);
        }

        public void ManualSaveGameId()
        {
            if (this.showButtonsLog)
                Debug.Log("<color=#00FF88><b>[SaiService] ► Save Game ID to PlayerPrefs</b></color>", gameObject);
            this.SaveGameIdToPlayerPrefs();
        }

        public void ManualClearGameId()
        {
            if (this.showButtonsLog)
                Debug.Log("<color=#FF6666><b>[SaiService] ► Clear PlayerPrefs</b></color>", gameObject);
            if (PlayerPrefs.HasKey(PREF_GAME_ID))
            {
                PlayerPrefs.DeleteKey(PREF_GAME_ID);
                this.gameId = string.Empty;
                if (this.showButtonsLog)
                    Debug.Log("Cleared Game ID from PlayerPrefs");
            }
            if (PlayerPrefs.HasKey(PREF_SERVER_ENDPOINT))
            {
                PlayerPrefs.DeleteKey(PREF_SERVER_ENDPOINT);
                if (this.showButtonsLog)
                    Debug.Log("Cleared Server Endpoint from PlayerPrefs");
            }
            PlayerPrefs.Save();
        }

        public void TestConnection(Action<bool> callback = null)
        {
            if (this.showButtonsLog)
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
