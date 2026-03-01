using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class Mailbox : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<MailBoxResponse> OnGetMessagesSuccess;
        public event Action<string> OnGetMessagesFailure;
        public event Action<MailboxMessage> OnReadMessageSuccess;
        public event Action<string> OnReadMessageFailure;
        public event Action<MailboxMessage> OnClaimMessageSuccess;
        public event Action<string> OnClaimMessageFailure;
        public event Action<MailboxMessage[]> OnClaimAllMessagesSuccess;
        public event Action<string> OnClaimAllMessagesFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Mail Data")]
        [SerializeField] protected MailBoxResponse currentMailBox;
        [SerializeField] protected int messageLimit = 20;
        [SerializeField] protected int messageOffset = 0;

        public MailBoxResponse CurrentMailBox => currentMailBox;
        public bool HasMessages => currentMailBox != null && currentMailBox.messages != null && currentMailBox.messages.Length > 0;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
        }

        protected virtual void RegisterLoginListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLoginSuccess += HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLogoutSuccess += HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiService.Instance?.SaiAuth != null)
            {
                SaiService.Instance.SaiAuth.OnLoginSuccess -= HandleLoginSuccess;
                SaiService.Instance.SaiAuth.OnLogoutSuccess -= HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!autoLoadOnLogin) return;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("Auto-loading mailbox after successful login...");

            GetMessages(
                onSuccess: mailBox =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"Mailbox auto-loaded: {mailBox.messages.Length} messages, total: {mailBox.total}");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"Auto-load mailbox failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[MailBox] Logout successful, clearing mailbox data...");

            ClearLocalMailBox();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[MailBox] Mailbox data cleared successfully");
        }

        public void GetMessages(int? limit = null, int? offset = null, System.Action<MailBoxResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[MailBox] ► Get Messages</b></color>", gameObject);
            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            // Use provided parameters or fallback to Inspector values
            int actualLimit = limit ?? this.messageLimit;
            int actualOffset = offset ?? this.messageOffset;

            StartCoroutine(GetMessagesCoroutine(actualLimit, actualOffset, onSuccess, onError));
        }

        private IEnumerator GetMessagesCoroutine(int limit, int offset, System.Action<MailBoxResponse> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages?limit={limit}&offset={offset}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        MailBoxResponse mailBoxResponse = JsonUtility.FromJson<MailBoxResponse>(response);
                        this.currentMailBox = mailBoxResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Mailbox loaded: {mailBoxResponse.messages.Length} messages, total: {mailBoxResponse.total}");

                        OnGetMessagesSuccess?.Invoke(mailBoxResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › GetMessagesCoroutine");
                        onSuccess?.Invoke(mailBoxResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get messages response error: {e.Message}";
                        OnGetMessagesFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › GetMessagesCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetMessagesFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › GetMessagesCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ReadMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FF88><b>[MailBox] ► Read Message</b></color>", gameObject);
            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(ReadMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator ReadMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        MailboxMessage message = this.ParseMailboxMessage(response);

                        // Update the message in our local cache if it exists
                        if (message != null && !string.IsNullOrEmpty(message.id) && this.currentMailBox != null && this.currentMailBox.messages != null)
                        {
                            for (int i = 0; i < this.currentMailBox.messages.Length; i++)
                            {
                                if (this.currentMailBox.messages[i].id == messageId)
                                {
                                    this.currentMailBox.messages[i] = message;
                                    break;
                                }
                            }
                        }

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Message {messageId} marked as read");

                        OnReadMessageSuccess?.Invoke(message);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ReadMessageCoroutine");
                        onSuccess?.Invoke(message);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse read message response error: {e.Message}";
                        OnReadMessageFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › ReadMessageCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnReadMessageFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › ReadMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClaimMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[MailBox] ► Claim Message</b></color>", gameObject);
            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(ClaimMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator ClaimMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}/claim";

            yield return SaiService.Instance.PostRequest(endpoint, "{}",
                response =>
                {
                    try
                    {
                        ClaimMessageResponse parsed = JsonUtility.FromJson<ClaimMessageResponse>(response);

                        // API returns rewards, not the updated message — update claimed_at locally
                        if (this.currentMailBox != null && this.currentMailBox.messages != null)
                        {
                            for (int i = 0; i < this.currentMailBox.messages.Length; i++)
                            {
                                if (this.currentMailBox.messages[i].id == messageId)
                                {
                                    this.currentMailBox.messages[i].claimed_at = DateTime.UtcNow.ToString("o");
                                    break;
                                }
                            }
                        }

                        MailboxMessage cached = this.GetMessageById(messageId);
                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"Message {messageId} claimed successfully");

                        OnClaimMessageSuccess?.Invoke(cached);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ClaimMessageCoroutine");
                        onSuccess?.Invoke(cached);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse claim message response error: {e.Message}";
                        OnClaimMessageFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › ClaimMessageCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnClaimMessageFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › ClaimMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClaimAllMessages(System.Action<MailboxMessage[]> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[MailBox] ► Claim All Messages</b></color>", gameObject);
            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            MailboxMessage[] unclaimed = this.GetUnclaimedMessages();
            if (unclaimed == null || unclaimed.Length == 0)
            {
                onError?.Invoke("No unclaimed messages found.");
                return;
            }

            StartCoroutine(ClaimAllMessagesCoroutine(unclaimed, onSuccess, onError));
        }

        private System.Collections.IEnumerator ClaimAllMessagesCoroutine(MailboxMessage[] unclaimed, System.Action<MailboxMessage[]> onSuccess, System.Action<string> onError)
        {
            var claimed = new System.Collections.Generic.List<MailboxMessage>();
            string lastError = null;

            foreach (MailboxMessage msg in unclaimed)
            {
                if (msg.attachments == null || msg.attachments.Length == 0)
                    continue;

                if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                    Debug.Log($"[MailBox] Claim message: \"{msg.subject}\" | ID: {msg.id}");

                string gameId = SaiService.Instance.GameId;
                string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{msg.id}/claim";
                bool done = false;

                yield return SaiService.Instance.PostRequest(endpoint, "{}",
                    response =>
                    {
                        try
                        {
                            JsonUtility.FromJson<ClaimMessageResponse>(response);

                            // API returns rewards — update claimed_at locally
                            if (this.currentMailBox != null && this.currentMailBox.messages != null)
                            {
                                for (int i = 0; i < this.currentMailBox.messages.Length; i++)
                                {
                                    if (this.currentMailBox.messages[i].id == msg.id)
                                    {
                                        this.currentMailBox.messages[i].claimed_at = DateTime.UtcNow.ToString("o");
                                        claimed.Add(this.currentMailBox.messages[i]);
                                        break;
                                    }
                                }
                            }

                            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                                Debug.Log($"Message {msg.id} claimed successfully");
                        }
                        catch (System.Exception e)
                        {
                            lastError = $"Parse claim message response error: {e.Message}";
                        }
                        done = true;
                    },
                    error =>
                    {
                        lastError = error;
                        done = true;
                    }
                );

                yield return new WaitUntil(() => done);
            }

            if (claimed.Count > 0)
            {
                MailboxMessage[] result = claimed.ToArray();
                OnClaimAllMessagesSuccess?.Invoke(result);
                if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                    Debug.Log("<color=#66CCFF>[MailBox] ClaimAllMessages</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ClaimAllMessagesCoroutine");
                onSuccess?.Invoke(result);

                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.Log($"[MailBox] Claimed {result.Length}/{unclaimed.Length} messages");
            }
            else
            {
                string errorMsg = lastError ?? "Failed to claim any messages.";
                OnClaimAllMessagesFailure?.Invoke(errorMsg);
                if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                    Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimAllMessages</color> → <b><color=#FF4444>onError</color></b> callback | Mailbox.cs › ClaimAllMessagesCoroutine | {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }

        public void ClearMailBox()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[MailBox] ► Clear Mailbox</b></color>", gameObject);
            ClearLocalMailBox();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("Mailbox data cleared locally");
        }

        private void ClearLocalMailBox()
        {
            this.currentMailBox = new MailBoxResponse
            {
                messages = new MailboxMessage[0],
                total = 0
            };
        }

        public void SetMessageLimit(int limit)
        {
            this.messageLimit = limit;
        }

        public void SetMessageOffset(int offset)
        {
            this.messageOffset = offset;
        }

        public int GetMessageLimit()
        {
            return this.messageLimit;
        }

        public int GetMessageOffset()
        {
            return this.messageOffset;
        }

        public MailboxMessage GetMessageById(string messageId)
        {
            if (this.currentMailBox == null || this.currentMailBox.messages == null)
                return null;

            foreach (var message in this.currentMailBox.messages)
            {
                if (message.id == messageId)
                    return message;
            }

            return null;
        }

        // Tries to parse a MailboxMessage from a response that may be a wrapper object or a direct message.
        private MailboxMessage ParseMailboxMessage(string json)
        {
            // Try wrapped format first: { "message": {...}, "message_text": "..." }
            ReadMessageResponse wrapped = JsonUtility.FromJson<ReadMessageResponse>(json);
            if (wrapped != null && wrapped.message != null && !string.IsNullOrEmpty(wrapped.message.id))
                return wrapped.message;

            // Fall back to direct MailboxMessage format
            MailboxMessage direct = JsonUtility.FromJson<MailboxMessage>(json);
            if (direct != null && !string.IsNullOrEmpty(direct.id))
                return direct;

            return null;
        }

        public MailboxMessage[] GetUnreadMessages()
        {
            if (this.currentMailBox == null || this.currentMailBox.messages == null)
                return new MailboxMessage[0];

            var unreadMessages = new System.Collections.Generic.List<MailboxMessage>();
            foreach (var message in this.currentMailBox.messages)
            {
                if (message.status == "unread")
                    unreadMessages.Add(message);
            }

            return unreadMessages.ToArray();
        }

        public MailboxMessage[] GetUnclaimedMessages()
        {
            if (this.currentMailBox == null || this.currentMailBox.messages == null)
                return new MailboxMessage[0];

            var unclaimedMessages = new System.Collections.Generic.List<MailboxMessage>();
            foreach (var message in this.currentMailBox.messages)
            {
                if (message.status == "unread" || message.status == "read")
                    unclaimedMessages.Add(message);
            }

            return unclaimedMessages.ToArray();
        }

        /// <summary>
        /// Returns locally cached messages filtered by the given status.
        /// MailboxStatusFilter.All returns every message.
        /// </summary>
        public MailboxMessage[] GetMessagesByStatus(MailboxStatusFilter filter)
        {
            if (this.currentMailBox == null || this.currentMailBox.messages == null)
                return new MailboxMessage[0];

            if (filter == MailboxStatusFilter.All)
                return this.currentMailBox.messages;

            string statusKey;
            switch (filter)
            {
                case MailboxStatusFilter.Unread:  statusKey = "unread";  break;
                case MailboxStatusFilter.Read:    statusKey = "read";    break;
                case MailboxStatusFilter.Claimed: statusKey = "claimed"; break;
                default: return this.currentMailBox.messages;
            }

            var result = new System.Collections.Generic.List<MailboxMessage>();
            foreach (MailboxMessage message in this.currentMailBox.messages)
            {
                if (message.status == statusKey)
                    result.Add(message);
            }

            return result.ToArray();
        }
    }
}