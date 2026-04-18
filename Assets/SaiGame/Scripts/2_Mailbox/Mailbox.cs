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
        public event Action<MailboxMessage> OnUnreadMessageSuccess;
        public event Action<string> OnUnreadMessageFailure;
        public event Action<MailboxMessage> OnClaimMessageSuccess;
        public event Action<string> OnClaimMessageFailure;
        public event Action<MailboxMessage> OnUnclaimMessageSuccess;
        public event Action<string> OnUnclaimMessageFailure;
        public event Action<MailboxMessage[]> OnClaimAllMessagesSuccess;
        public event Action<string> OnClaimAllMessagesFailure;
        public event Action<string> OnDeleteMessageSuccess;
        public event Action<string> OnDeleteMessageFailure;

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
            if (SaiServer.Instance?.SaiAuth == null) return;

            SaiServer.Instance.SaiAuth.OnLoginSuccess += this.HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiServer.Instance?.SaiAuth == null) return;

            SaiServer.Instance.SaiAuth.OnLogoutSuccess += this.HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiServer.Instance?.SaiAuth != null)
            {
                SaiServer.Instance.SaiAuth.OnLoginSuccess -= this.HandleLoginSuccess;
                SaiServer.Instance.SaiAuth.OnLogoutSuccess -= this.HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!this.autoLoadOnLogin) return;

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("Auto-loading mailbox after successful login...");

            this.GetMessages(
                onSuccess: mailBox =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"Mailbox auto-loaded: {mailBox.messages.Length} messages, total: {mailBox.total}");
                },
                onError: error =>
                {
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.LogWarning($"Auto-load mailbox failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[MailBox] Logout successful, clearing mailbox data...");

            this.ClearLocalMailBox();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                Debug.Log("[MailBox] Mailbox data cleared successfully");
        }

        public void GetMessages(int? limit = null, int? offset = null, System.Action<MailBoxResponse> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[MailBox] ► Get Messages</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
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
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages?limit={limit}&offset={offset}";

            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        MailBoxResponse mailBoxResponse = JsonUtility.FromJson<MailBoxResponse>(response);
                        this.currentMailBox = mailBoxResponse;

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Mailbox loaded: {mailBoxResponse.messages.Length} messages, total: {mailBoxResponse.total}");

                        OnGetMessagesSuccess?.Invoke(mailBoxResponse);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › GetMessagesCoroutine");
                        onSuccess?.Invoke(mailBoxResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get messages response error: {e.Message}";
                        OnGetMessagesFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › GetMessagesCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnGetMessagesFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] GetMessages</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › GetMessagesCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ReadMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FF88><b>[MailBox] ► Read Message</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(ReadMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator ReadMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}";
            string body = "{\"read\": true}";

            yield return SaiServer.Instance.PatchRequest(endpoint, body,
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

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Message {messageId} marked as read");

                        OnReadMessageSuccess?.Invoke(message);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ReadMessageCoroutine");
                        onSuccess?.Invoke(message);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse read message response error: {e.Message}";
                        OnReadMessageFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › ReadMessageCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnReadMessageFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] ReadMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › ReadMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void UnreadMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFA0A0><b>[MailBox] ► Unread Message</b></color>", gameObject);

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(UnreadMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator UnreadMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}";
            string body = "{\"read\": false}";

            yield return SaiServer.Instance.PatchRequest(endpoint, body,
                response =>
                {
                    try
                    {
                        MailboxMessage message = this.ParseMailboxMessage(response);

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

                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Message {messageId} marked as unread");

                        OnUnreadMessageSuccess?.Invoke(message);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] UnreadMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › UnreadMessageCoroutine");
                        onSuccess?.Invoke(message);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse unread message response error: {e.Message}";
                        OnUnreadMessageFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] UnreadMessage</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › UnreadMessageCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnUnreadMessageFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] UnreadMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › UnreadMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClaimMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[MailBox] ► Claim Message</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(ClaimMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator ClaimMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}/claim";

            yield return SaiServer.Instance.PostRequest(endpoint, "{}",
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
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                            Debug.Log($"Message {messageId} claimed successfully");

                        OnClaimMessageSuccess?.Invoke(cached);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ClaimMessageCoroutine");
                        onSuccess?.Invoke(cached);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse claim message response error: {e.Message}";
                        OnClaimMessageFailure?.Invoke(errorMsg);
                        if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#FF4444>onError</color></b> callback (parse) | Mailbox.cs › ClaimMessageCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    OnClaimMessageFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › ClaimMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void UnclaimMessage(string messageId, System.Action<MailboxMessage> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#CC88FF><b>[MailBox] ► Unclaim Message</b></color>", gameObject);

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(UnclaimMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator UnclaimMessageCoroutine(string messageId, System.Action<MailboxMessage> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}/claim";

            yield return SaiServer.Instance.DeleteRequest(endpoint,
                response =>
                {
                    // Clear claimed_at locally
                    if (this.currentMailBox != null && this.currentMailBox.messages != null)
                    {
                        for (int i = 0; i < this.currentMailBox.messages.Length; i++)
                        {
                            if (this.currentMailBox.messages[i].id == messageId)
                            {
                                this.currentMailBox.messages[i].claimed_at = string.Empty;
                                break;
                            }
                        }
                    }

                    MailboxMessage cached = this.GetMessageById(messageId);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"Message {messageId} unclaimed successfully");

                    OnUnclaimMessageSuccess?.Invoke(cached);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[MailBox] UnclaimMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › UnclaimMessageCoroutine");
                    onSuccess?.Invoke(cached);
                },
                error =>
                {
                    OnUnclaimMessageFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] UnclaimMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › UnclaimMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClaimAllMessages(System.Action<MailboxMessage[]> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FFD700><b>[MailBox] ► Claim All Messages</b></color>", gameObject);
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
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

                if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                    Debug.Log($"[MailBox] Claim message: \"{msg.subject}\" | ID: {msg.id}");

                string gameId = SaiServer.Instance.GameId;
                string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{msg.id}/claim";
                bool done = false;

                yield return SaiServer.Instance.PostRequest(endpoint, "{}",
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

                            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
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
                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                    Debug.Log("<color=#66CCFF>[MailBox] ClaimAllMessages</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › ClaimAllMessagesCoroutine");
                onSuccess?.Invoke(result);

                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.Log($"[MailBox] Claimed {result.Length}/{unclaimed.Length} messages");
            }
            else
            {
                string errorMsg = lastError ?? "Failed to claim any messages.";
                OnClaimAllMessagesFailure?.Invoke(errorMsg);
                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                    Debug.LogWarning($"<color=#66CCFF>[MailBox] ClaimAllMessages</color> → <b><color=#FF4444>onError</color></b> callback | Mailbox.cs › ClaimAllMessagesCoroutine | {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }

        public void DeleteMessage(string messageId, System.Action<string> onSuccess = null, System.Action<string> onError = null)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF4444><b>[MailBox] ► Delete Message</b></color>", gameObject);

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(DeleteMessageCoroutine(messageId, onSuccess, onError));
        }

        private IEnumerator DeleteMessageCoroutine(string messageId, System.Action<string> onSuccess, System.Action<string> onError)
        {
            string gameId = SaiServer.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/mailbox/messages/{messageId}";

            yield return SaiServer.Instance.DeleteRequest(endpoint,
                response =>
                {
                    // Remove message from local cache
                    if (this.currentMailBox != null && this.currentMailBox.messages != null)
                    {
                        var list = new System.Collections.Generic.List<MailboxMessage>(this.currentMailBox.messages);
                        list.RemoveAll(m => m.id == messageId);
                        this.currentMailBox.messages = list.ToArray();
                        this.currentMailBox.total = Mathf.Max(0, this.currentMailBox.total - 1);
                    }

                    if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                        Debug.Log($"Message {messageId} deleted successfully");

                    OnDeleteMessageSuccess?.Invoke(messageId);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.Log("<color=#66CCFF>[MailBox] DeleteMessage</color> → <b><color=#00FF88>onSuccess</color></b> callback | Mailbox.cs › DeleteMessageCoroutine");
                    onSuccess?.Invoke(messageId);
                },
                error =>
                {
                    OnDeleteMessageFailure?.Invoke(error);
                    if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[MailBox] DeleteMessage</color> → <b><color=#FF4444>onError</color></b> callback (network) | Mailbox.cs › DeleteMessageCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        public void ClearMailBox()
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[MailBox] ► Clear Mailbox</b></color>", gameObject);
            ClearLocalMailBox();

            if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
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

        private MailboxStatusFilter lastLoggedFilter = (MailboxStatusFilter)(-1);

        /// <summary>
        /// Returns locally cached messages filtered by the given status.
        /// MailboxStatusFilter.All returns every message.
        /// </summary>
        public MailboxMessage[] GetMessagesByStatus(MailboxStatusFilter filter)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog && filter != this.lastLoggedFilter)
            {
                this.lastLoggedFilter = filter;
                Debug.Log($"<color=#AADDFF><b>[MailBox] \u25ba Filter by Status: <color=#FFD700>{filter}</color></b></color>", gameObject);
            }

            if (this.currentMailBox == null || this.currentMailBox.messages == null)
                return new MailboxMessage[0];

            if (filter == MailboxStatusFilter.All)
                return this.currentMailBox.messages;

            if (filter == MailboxStatusFilter.Unclaimed)
            {
                var unclaimed = new System.Collections.Generic.List<MailboxMessage>();
                foreach (MailboxMessage message in this.currentMailBox.messages)
                    if (string.IsNullOrEmpty(message.claimed_at))
                        unclaimed.Add(message);
                return unclaimed.ToArray();
            }

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