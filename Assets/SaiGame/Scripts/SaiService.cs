using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-100)]
    public class SaiService : SaiSingleton<SaiService>
    {
        public const string PACKAGE_VERSION = "0.2.29";
        public const string PACKAGE_NAME = "SaiGame Services";

        [SerializeField] protected SaiAuth saiAuth;
        [SerializeField] protected GamerProgress gamerProgress;
        [SerializeField] protected Mailbox mailbox;
        [SerializeField] protected PlayerItem playerItem;
        [SerializeField] protected PlayerEvent playerEvent;
        [SerializeField] protected PlayerContainer playerContainer;
        [SerializeField] protected ItemPreset itemPreset;
        [SerializeField] protected ItemAddDeduct itemAddDeduct;
        [SerializeField] protected ItemCrafting itemCrafting;
        [SerializeField] protected ItemGenerator itemGenerator;
        [SerializeField] protected EquipmentSlot equipmentSlotManager;
        [SerializeField] protected Shop shop;
        [SerializeField] protected ChainQuest chainQuest;
        [SerializeField] protected QuestProgressor questProgressor;
        [SerializeField] protected QuestStatus questClaims;
        [SerializeField] protected DailyQuest dailyQuest;
        [SerializeField] protected ItemMove itemMove;
        [SerializeField] protected ItemSwap itemSwap;
        [SerializeField] protected ItemTag itemTag;
        [SerializeField] protected Leaderboard leaderboard;
        [SerializeField] protected BattleSessions battleSessions;
        [SerializeField] protected BattleScript battleScript;

        [Header("Server Configuration")]
        [HideInInspector][SerializeField] protected ServerEndpointOption serverEndpoint = ServerEndpointOption.ProductionHttps;
        [HideInInspector][SerializeField] protected DomainOption domainOption = DomainOption.Local;
        [HideInInspector][SerializeField] protected int port = 80;
        [HideInInspector][SerializeField] protected bool useHttps = false;

        [Header("Game Configuration")]
        [SerializeField] protected string gameId = "";
        private const string PREF_GAME_ID = "SaiGame_GameId";
        private const string PREF_SERVER_ENDPOINT = "SaiGame_ServerEndpoint";

        // Tracks the last value written to PlayerPrefs so we only save on actual change
        private string lastSavedGameId = null;

        [Header("API Settings")]
        [SerializeField] protected int requestTimeout = 30;

        [SerializeField] protected bool showButtonsLog = true;
        [SerializeField] protected bool showCallbackLog = true;
        [SerializeField] protected bool showDebugLog = true;
        [SerializeField] protected bool showUrlRequest = true;
        [SerializeField] protected bool showJsonRequest = true;
        [SerializeField] protected bool showJsonResponse = true;

        public event Action<string> OnTokenRefreshed;

        public bool ShowDebug => showDebugLog;

        public bool ShowButtonsLog => showButtonsLog;

        public bool ShowCallbackLog => showCallbackLog;

        public bool ShowUrlRequest => showUrlRequest;

        public bool ShowJsonRequest => showJsonRequest;

        public bool ShowJsonResponse => showJsonResponse;

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

        public PlayerEvent PlayerEvent => playerEvent;

        public PlayerContainer PlayerContainer => playerContainer;

        public ItemPreset ItemPreset => this.itemPreset;

        public ItemAddDeduct ItemAddDeduct => this.itemAddDeduct;

        public ItemCrafting ItemCrafting => this.itemCrafting;

        public ItemGenerator ItemGenerator => itemGenerator;

        public Shop Shop => this.shop;

        public EquipmentSlot EquipmentSlotManager => this.equipmentSlotManager;

        public ChainQuest ChainQuest => this.chainQuest;

        public QuestProgressor QuestProgressor => this.questProgressor;

        public QuestStatus QuestClaims => this.questClaims;

        public DailyQuest DailyQuest => this.dailyQuest;

        public ItemMove ItemMove => this.itemMove;

        public ItemSwap ItemSwap => this.itemSwap;

        public ItemTag ItemTag => this.itemTag;

        public Leaderboard Leaderboard => this.leaderboard;

        public BattleSessions BattleSessions => this.battleSessions;

        public BattleScript BattleScript => this.battleScript;

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
                if (this.showUrlRequest)
                    Debug.Log($"[SaiService] GET {request.url}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (this.showJsonResponse)
                        Debug.Log($"[SaiService] GET Response\n{request.downloadHandler.text}");
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

                if (this.showUrlRequest)
                    Debug.Log($"[SaiService] POST {request.url}");
                if (this.showJsonRequest)
                    Debug.Log($"[SaiService] POST Request Body\n{jsonData}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (this.showJsonResponse)
                        Debug.Log($"[SaiService] POST Response\n{request.downloadHandler.text}");
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

        public IEnumerator PutRequest(string endpoint, string jsonData, Action<string> onSuccess, Action<string> onError)
        {
            using (UnityWebRequest request = CreateAuthenticatedRequest(endpoint, "PUT"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");

                if (this.showUrlRequest)
                    Debug.Log($"[SaiService] PUT {request.url}");
                if (this.showJsonRequest)
                    Debug.Log($"[SaiService] PUT Request Body\n{jsonData}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (this.showJsonResponse)
                        Debug.Log($"[SaiService] PUT Response\n{request.downloadHandler.text}");
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    string rawResponse = request.downloadHandler?.text ?? "No response data";
                    string errorMsg = $"PUT {endpoint} failed: {request.error}\nResponse Code: {request.responseCode}\nRaw Data: {rawResponse}";
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

                if (this.showUrlRequest)
                    Debug.Log($"[SaiService] PATCH {request.url}");
                if (this.showJsonRequest)
                    Debug.Log($"[SaiService] PATCH Request Body\n{jsonData}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (this.showJsonResponse)
                        Debug.Log($"[SaiService] PATCH Response\n{request.downloadHandler.text}");
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
                if (this.showUrlRequest)
                    Debug.Log($"[SaiService] DELETE {request.url}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (this.showJsonResponse)
                        Debug.Log($"[SaiService] DELETE Response\n{request.downloadHandler.text}");
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

        protected override void ResetValue()
        {
            base.ResetValue();
            this.ManualClearGameId();
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadServerEndpointFromPlayerPrefs();
            this.SyncLegacyServerFieldsFromEndpoint();
            this.LoadSaiAuth();
            this.LoadGamerProgress();
            this.LoadMailbox();
            this.LoadPlayerEvent();
            this.LoadLeaderboard();
            this.LoadPlayerItem();
            this.LoadPlayerContainer();
            this.LoadItemPreset();
            this.LoadItemAddDeduct();
            this.LoadItemCrafting();
            this.LoadItemGenerator();
            this.LoadEquipmentSlotManager();
            this.LoadShop();
            this.LoadChainQuest();
            this.LoadQuestProgressor();
            this.LoadQuestStatus();
            this.LoadDailyQuest();
            this.LoadItemTag();
            this.LoadItemMove();
            this.LoadItemSwap();
            this.LoadBattleSessions();
            this.LoadBattleScript();
            this.LoadGameIdFromPlayerPrefs();
        }


        protected virtual void LoadSaiAuth()
        {
            if (this.saiAuth != null) return;
            this.saiAuth = GetComponent<SaiAuth>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadSaiAuth", gameObject);
        }

        protected virtual void LoadPlayerEvent()
        {
            if (this.playerEvent != null) return;
            this.playerEvent = GetComponent<PlayerEvent>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadPlayerEvent", gameObject);
        }

        protected virtual void LoadLeaderboard()
        {
            if (this.leaderboard != null) return;
            this.leaderboard = GetComponent<Leaderboard>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadLeaderboard", gameObject);
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
            this.playerItem = GetComponentInChildren<PlayerItem>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadPlayerItem", gameObject);
        }

        protected virtual void LoadPlayerContainer()
        {
            if (this.playerContainer != null) return;
            this.playerContainer = GetComponentInChildren<PlayerContainer>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadPlayerContainer", gameObject);
        }

        protected virtual void LoadItemPreset()
        {
            if (this.itemPreset != null) return;
            this.itemPreset = GetComponentInChildren<ItemPreset>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemPreset", gameObject);
        }

        protected virtual void LoadItemAddDeduct()
        {
            if (this.itemAddDeduct != null) return;
            this.itemAddDeduct = GetComponentInChildren<ItemAddDeduct>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemAddDeduct", gameObject);
        }

        protected virtual void LoadItemCrafting()
        {
            if (this.itemCrafting != null) return;
            this.itemCrafting = GetComponentInChildren<ItemCrafting>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemCrafting", gameObject);
        }

        protected virtual void LoadItemGenerator()
        {
            if (this.itemGenerator != null) return;
            this.itemGenerator = GetComponentInChildren<ItemGenerator>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemGenerator", gameObject);
        }

        protected virtual void LoadEquipmentSlotManager()
        {
            if (this.equipmentSlotManager != null) return;
            this.equipmentSlotManager = GetComponentInChildren<EquipmentSlot>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadEquipmentSlotManager", gameObject);
        }

        protected virtual void LoadShop()
        {
            if (this.shop != null) return;
            this.shop = GetComponentInChildren<Shop>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadShop", gameObject);
        }

        protected virtual void LoadChainQuest()
        {
            if (this.chainQuest != null) return;
            this.chainQuest = GetComponentInChildren<ChainQuest>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadChainQuest", gameObject);
        }

        protected virtual void LoadQuestProgressor()
        {
            if (this.questProgressor != null) return;
            this.questProgressor = GetComponentInChildren<QuestProgressor>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadQuestProgressor", gameObject);
        }

        protected virtual void LoadDailyQuest()
        {
            if (this.dailyQuest != null) return;
            this.dailyQuest = GetComponentInChildren<DailyQuest>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadDailyQuest", gameObject);
        }

        protected virtual void LoadQuestStatus()
        {
            if (this.questClaims != null) return;
            this.questClaims = GetComponentInChildren<QuestStatus>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadQuestStatus", gameObject);
        }

        protected virtual void LoadItemMove()
        {
            if (this.itemMove != null) return;
            this.itemMove = GetComponentInChildren<ItemMove>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemMove", gameObject);
        }

        protected virtual void LoadItemSwap()
        {
            if (this.itemSwap != null) return;
            this.itemSwap = GetComponentInChildren<ItemSwap>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemSwap", gameObject);
        }

        protected virtual void LoadItemTag()
        {
            if (this.itemTag != null) return;
            this.itemTag = GetComponentInChildren<ItemTag>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadItemTag", gameObject);
        }

        protected virtual void LoadBattleSessions()
        {
            if (this.battleSessions != null) return;
            this.battleSessions = GetComponentInChildren<BattleSessions>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadBattleSessions", gameObject);
        }

        protected virtual void LoadBattleScript()
        {
            if (this.battleScript != null) return;
            this.battleScript = GetComponentInChildren<BattleScript>();
            if (this.showDebugLog)
                Debug.Log(transform.name + ": LoadBattleScript", gameObject);
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

            this.lastSavedGameId = this.gameId;
        }

        protected virtual void SaveGameIdToPlayerPrefs()
        {
            this.gameId = this.NormalizeInput(this.gameId);
            this.lastSavedGameId = this.gameId;
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

            string normalized = this.NormalizeInput(this.gameId);
            this.gameId = normalized;

            // Auto-save whenever the Game ID value actually changes
            if (this.lastSavedGameId != normalized)
            {
                this.lastSavedGameId = normalized;
                PlayerPrefs.SetString(PREF_GAME_ID, normalized);
                PlayerPrefs.Save();
                if (this.showDebugLog)
                    Debug.Log($"[SaiService] Game ID auto-saved to PlayerPrefs: {normalized}");
            }

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
