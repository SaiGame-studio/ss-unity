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
                
                EditorGUILayout.PropertyField(currentMailBox);
                
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

                EditorGUILayout.Space(5);

                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Get Unread Messages Count", GUILayout.Height(25)))
                {
                    var unread = mailBox.GetUnreadMessages();
                    Debug.Log($"[MailBoxEditor] You have {unread.Length} unread messages");
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(5);

                GUI.backgroundColor = Color.magenta;
                if (GUILayout.Button("Get Unclaimed Messages Count", GUILayout.Height(25)))
                {
                    var unclaimed = mailBox.GetUnclaimedMessages();
                    Debug.Log($"[MailBoxEditor] You have {unclaimed.Length} unclaimed messages");
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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"ID: {message.ID}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Subject: {message.Subject}");
            EditorGUILayout.LabelField($"Status: {message.Status}");
            EditorGUILayout.LabelField($"Type: {message.MessageType}");
            EditorGUILayout.LabelField($"Created: {message.CreatedAt}");
            if (!string.IsNullOrEmpty(message.ReadAt))
                EditorGUILayout.LabelField($"Read: {message.ReadAt}");
            if (!string.IsNullOrEmpty(message.ClaimedAt))
                EditorGUILayout.LabelField($"Claimed: {message.ClaimedAt}");
            
            if (message.Attachments != null && message.Attachments.Length > 0)
            {
                EditorGUILayout.LabelField($"Attachments: {message.Attachments.Length}");
                foreach (var attachment in message.Attachments)
                {
                    EditorGUILayout.LabelField($"  - {attachment.ItemName} x{attachment.Quantity} (Coins: {attachment.CoinAmount})");
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
                    Debug.Log($"[MailBoxEditor] Loaded {response.messages.Length} messages");
                },
                onError: error =>
                {
                    Debug.LogError($"[MailBoxEditor] Failed to load messages: {error}");
                }
            );
        }
    }
}