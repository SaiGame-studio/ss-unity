using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(Mailbox))]
    [CanEditMultipleObjects]
    public class MailboxEditor : Editor
    {
        private Mailbox mailBox;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty currentMailBox;
        private SerializedProperty messageLimit;
        private SerializedProperty messageOffset;

        private bool showCurrentMailBox = true;
        private bool showMessageList = true;
        private bool showUtilityButtons = true;
        private MailboxStatusFilter statusFilter = MailboxStatusFilter.All;

        private readonly Dictionary<string, bool> messageFoldouts = new Dictionary<string, bool>();

        private void OnEnable()
        {
            mailBox = (Mailbox)target;
            autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            currentMailBox = serializedObject.FindProperty("currentMailBox");
            messageLimit = serializedObject.FindProperty("messageLimit");
            messageOffset = serializedObject.FindProperty("messageOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MailBox Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load mailbox when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Message Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(messageLimit, new GUIContent("Message Limit", "Number of messages to load per request"));
            EditorGUILayout.PropertyField(messageOffset, new GUIContent("Message Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current MailBox Data
            showCurrentMailBox = EditorGUILayout.Foldout(showCurrentMailBox, "Current MailBox Data", true);
            if (showCurrentMailBox)
            {
                EditorGUI.indentLevel++;

                if (mailBox.CurrentMailBox != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Messages: {mailBox.CurrentMailBox.total}");
                    EditorGUILayout.LabelField($"Loaded Messages: {mailBox.CurrentMailBox.messages?.Length ?? 0}");

                    if (mailBox.CurrentMailBox.messages != null && mailBox.CurrentMailBox.messages.Length > 0)
                    {
                        // Status filter
                        EditorGUILayout.Space(2);
                        this.statusFilter = (MailboxStatusFilter)EditorGUILayout.EnumPopup(
                            new GUIContent("Filter by Status", "Show only messages matching this status"),
                            this.statusFilter);

                        MailboxMessage[] filtered = mailBox.GetMessagesByStatus(this.statusFilter);
                        showMessageList = EditorGUILayout.Foldout(showMessageList,
                            this.statusFilter == MailboxStatusFilter.All
                                ? $"Message List ({filtered.Length})"
                                : $"Message List ({filtered.Length} {this.statusFilter.ToString().ToLower()})",
                            true);
                        if (showMessageList)
                        {
                            EditorGUI.indentLevel++;
                            if (filtered.Length == 0)
                            {
                                EditorGUILayout.LabelField($"No {this.statusFilter.ToString().ToLower()} messages.", EditorStyles.miniLabel);
                            }
                            else
                            {
                                foreach (var message in filtered)
                                {
                                    DrawMessageSummary(message);
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            showUtilityButtons = EditorGUILayout.Foldout(showUtilityButtons, "Utility Actions", true);
            if (showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Load Messages", GUILayout.Height(30)))
                {
                    LoadMessages();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Clear MailBox", GUILayout.Height(30)))
                {
                    mailBox.ClearMailBox();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Unread Count", GUILayout.Height(25)))
                {
                    var unread = mailBox.GetUnreadMessages();
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] You have {unread.Length} unread messages");
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.magenta;
                if (GUILayout.Button("Unclaimed Count", GUILayout.Height(25)))
                {
                    var unclaimed = mailBox.GetUnclaimedMessages();
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] You have {unclaimed.Length} unclaimed messages");
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(1f, 0.84f, 0f);
                if (GUILayout.Button("Claim All", GUILayout.Height(30)))
                {
                    ClaimAllMessages();
                }
                GUI.backgroundColor = Color.white;

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMessageSummary(MailboxMessage message)
        {
            if (!this.messageFoldouts.ContainsKey(message.id))
                this.messageFoldouts[message.id] = false;

            bool isClaimed = !string.IsNullOrEmpty(message.claimed_at);
            bool isRead = !string.IsNullOrEmpty(message.read_at);
            bool isExpired = message.status == "expired";
            bool hasAttachments = message.attachments != null && message.attachments.Length > 0;

            // Build foldout label with status
            string statusBadge = isClaimed ? " [Claimed]" : isRead ? " [Read]" : isExpired ? " [Expired]" : " [Unread]";
            string label = message.subject;

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontStyle = FontStyle.Bold;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            this.messageFoldouts[message.id] = EditorGUILayout.Foldout(this.messageFoldouts[message.id], label, true, foldoutStyle);

            // Status badge with color
            Color badgeColor;
            if (isClaimed)       badgeColor = new Color(1f, 0.84f, 0f);    // gold
            else if (isRead)     badgeColor = new Color(0.6f, 0.6f, 0.6f); // gray
            else if (isExpired)  badgeColor = new Color(0.6f, 0.6f, 0.6f); // gray
            else                 badgeColor = new Color(0.3f, 1f, 0.5f);   // green

            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = badgeColor;
            GUILayout.Label(statusBadge, badgeStyle, GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            if (this.messageFoldouts[message.id])
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ID: {message.id}");
                if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = message.id;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Status: {message.status}");
                EditorGUILayout.LabelField($"Type: {message.message_type}");
                EditorGUILayout.LabelField($"Created: {message.created_at}");
                if (!string.IsNullOrEmpty(message.read_at))
                    EditorGUILayout.LabelField($"Read: {message.read_at}");
                if (!string.IsNullOrEmpty(message.claimed_at))
                    EditorGUILayout.LabelField($"Claimed: {message.claimed_at}");

                if (hasAttachments)
                {
                    EditorGUILayout.LabelField($"Attachments: {message.attachments.Length}");
                    foreach (var attachment in message.attachments)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  - {attachment.definition_id}");
                        GUIStyle qtyStyle = new GUIStyle(EditorStyles.boldLabel);
                        qtyStyle.normal.textColor = new Color(0.4f, 1f, 0.9f);
                        GUILayout.Label($"x{attachment.quantity}", qtyStyle, GUILayout.ExpandWidth(false));
                        EditorGUILayout.EndHorizontal();
                    }
                }

                // Action buttons
                EditorGUILayout.BeginHorizontal();

                // Claim: only show when message has attachments
                if (hasAttachments)
                {
                    GUI.enabled = !isClaimed;
                    GUI.backgroundColor = isClaimed ? Color.gray : new Color(1f, 0.6f, 0f);
                    if (GUILayout.Button(isClaimed ? "Claimed ✓" : "Claim", GUILayout.Height(24)))
                        ClaimSpecificMessage(message.id);
                }

                // Read button
                GUI.enabled = !isRead && !isClaimed;
                GUI.backgroundColor = (isRead || isClaimed) ? Color.gray : new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button(isRead ? "Read ✓" : "Read", GUILayout.Height(24)))
                    ReadSpecificMessage(message.id);

                // Unread button
                GUI.enabled = isRead && !isClaimed;
                GUI.backgroundColor = (isRead && !isClaimed) ? new Color(1f, 0.55f, 0.55f) : Color.gray;
                if (GUILayout.Button("Unread", GUILayout.Height(24)))
                    UnreadSpecificMessage(message.id);

                // Delete button
                bool canDelete = (isRead && !isClaimed && !hasAttachments) || isClaimed || isExpired;
                GUI.enabled = canDelete;
                GUI.backgroundColor = canDelete ? new Color(0.9f, 0.2f, 0.2f) : Color.gray;
                if (GUILayout.Button("Delete", GUILayout.Height(24)))
                    DeleteSpecificMessage(message.id);

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void LoadMessages()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.GetMessages(
                onSuccess: response =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Loaded {response.messages.Length} messages");
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Failed to load messages: {error}");
                }
            );
        }

        private void ClaimAllMessages()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.ClaimAllMessages(
                onSuccess: results =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Claimed {results.Length} messages successfully");
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Claim all failed: {error}");
                }
            );
        }

        private void ClaimSpecificMessage(string messageId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.ClaimMessage(
                messageId,
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {result.id} claimed successfully");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Claim failed: {error}");
                }
            );
        }

        private void UnclaimSpecificMessage(string messageId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.UnclaimMessage(
                messageId,
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {result?.id} unclaimed successfully");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Unclaim failed: {error}");
                }
            );
        }

        private void ReadSpecificMessage(string messageId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.ReadMessage(
                messageId,
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {result.id} marked as read");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Read failed: {error}");
                }
            );
        }

        private void DeleteSpecificMessage(string messageId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.DeleteMessage(
                messageId,
                onSuccess: deletedId =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {deletedId} deleted successfully");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Delete failed: {error}");
                }
            );
        }

        private void UnreadSpecificMessage(string messageId)
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.UnreadMessage(
                messageId,
                onSuccess: result =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {result.id} marked as unread");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiServer.Instance == null || SaiServer.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Unread failed: {error}");
                }
            );
        }
    }
}