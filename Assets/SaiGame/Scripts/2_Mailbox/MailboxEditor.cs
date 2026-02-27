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
        private string claimMessageId = "";

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
                        showMessageList = EditorGUILayout.Foldout(showMessageList, "Message List", true);
                        if (showMessageList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var message in mailBox.CurrentMailBox.messages)
                            {
                                DrawMessageSummary(message);
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
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] You have {unread.Length} unread messages");
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.magenta;
                if (GUILayout.Button("Unclaimed Count", GUILayout.Height(25)))
                {
                    var unclaimed = mailBox.GetUnclaimedMessages();
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] You have {unclaimed.Length} unclaimed messages");
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUIStyle placeholderStyle = new GUIStyle(EditorStyles.textField);
                placeholderStyle.normal.textColor = Color.gray;
                if (string.IsNullOrEmpty(claimMessageId))
                {
                    string input = EditorGUILayout.TextField("message id", placeholderStyle, GUILayout.Height(30));
                    if (input != "message id")
                        claimMessageId = input;
                }
                else
                {
                    claimMessageId = EditorGUILayout.TextField(claimMessageId, GUILayout.Height(30));
                }
                GUI.backgroundColor = new Color(1f, 0.6f, 0f);
                if (GUILayout.Button("Claim Message", GUILayout.Height(30)))
                {
                    ClaimSingleMessage(claimMessageId);
                }
                GUI.backgroundColor = new Color(1f, 0.84f, 0f);

                if (GUILayout.Button("Claim All", GUILayout.Height(30)))
                {
                    ClaimAllMessages();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMessageSummary(MailboxMessage message)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"ID: {message.id}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Subject: {message.subject}");
            EditorGUILayout.LabelField($"Status: {message.status}");
            EditorGUILayout.LabelField($"Type: {message.message_type}");
            EditorGUILayout.LabelField($"Created: {message.created_at}");
            if (!string.IsNullOrEmpty(message.read_at))
                EditorGUILayout.LabelField($"Read: {message.read_at}");
            if (!string.IsNullOrEmpty(message.claimed_at))
                EditorGUILayout.LabelField($"Claimed: {message.claimed_at}");

            if (message.attachments != null && message.attachments.Length > 0)
            {
                EditorGUILayout.LabelField($"Attachments: {message.attachments.Length}");
                foreach (var attachment in message.attachments)
                {
                    EditorGUILayout.LabelField($"  - {attachment.definition_id} x{attachment.quantity}");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void LoadMessages()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.GetMessages(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Loaded {response.messages.Length} messages");
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Failed to load messages: {error}");
                }
            );
        }

        private void ClaimSingleMessage(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                Debug.LogWarning("[MailBoxEditor] Please enter a valid message ID.");
                return;
            }

            if (SaiService.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.ClaimMessage(
                messageId,
                onSuccess: result =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Message {result.id} claimed successfully");
                    claimMessageId = "";
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Claim failed: {error}");
                }
            );
        }

        private void ClaimAllMessages()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[MailBoxEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[MailBoxEditor] Not authenticated! Please login first.");
                return;
            }

            mailBox.ClaimAllMessages(
                onSuccess: results =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[MailBoxEditor] Claimed {results.Length} messages successfully");
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[MailBoxEditor] Claim all failed: {error}");
                }
            );
        }
    }
}