using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    public class SaiService : SaiBehaviour
    {
        [SerializeField] protected SaiAuth saiAuth;

        [Header("Server Configuration")]
        [SerializeField] protected string domain = "local-api.saigame.studio";
        [SerializeField] protected int port = 82;
        [SerializeField] protected bool useHttps = false;



        [Header("API Settings")]
        [SerializeField] protected int requestTimeout = 30;

        public event Action<string> OnTokenRefreshed;

        public string BaseUrl
        {
            get
            {
                string protocol = useHttps ? "https" : "http";
                return $"{protocol}://{domain}:{port}";
            }
        }

        public bool IsAuthenticated => saiAuth != null && saiAuth.IsAuthenticated;

        public string AccessToken => saiAuth?.AccessToken ?? "";

        public string RefreshToken => saiAuth?.RefreshToken ?? "";

        public int ExpiresIn => saiAuth?.ExpiresIn ?? 0;

        public UserData CurrentUser => saiAuth?.CurrentUser;

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
                    string errorMsg = $"GET {endpoint} failed: {request.error}";
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
                    string errorMsg = $"POST {endpoint} failed: {request.error}";
                    onError?.Invoke(errorMsg);
                }
            }
        }

        public void SetDomain(string newDomain)
        {
            domain = newDomain;
        }

        public void SetPort(int newPort)
        {
            port = newPort;
        }

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadSaiAuth();
        }

        protected virtual void LoadSaiAuth()
        {
            if (this.saiAuth != null) return;
            this.saiAuth = GetComponent<SaiAuth>();
            Debug.Log(transform.name + ": LoadSaiAuth", gameObject);
        }

        public void TestConnection(Action<bool> callback = null)
        {
            StartCoroutine(TestConnectionCoroutine(callback));
        }

        private IEnumerator TestConnectionCoroutine(Action<bool> callback)
        {
            string url = $"{BaseUrl}/api/v1/health";

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
