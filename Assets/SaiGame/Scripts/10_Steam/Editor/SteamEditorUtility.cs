using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    // Inspector pieces shared by the SteamLink and SteamLogin Inspectors.
    public static class SteamEditorUtility
    {
        public const float ButtonHeight = 25f;
        private const float SelectButtonWidth = 190f;
        private const string MetColor = "#4CAF50";
        private const string UnmetColor = "#F44336";

        private static GUIStyle statusStyle;
        private static GUIStyle selectStyle;

        // The Steam log color (SteamTicketService.LogColor), used to tint Steam buttons.
        public static Color ButtonColor
        {
            get
            {
                ColorUtility.TryParseHtmlString(SteamTicketService.LogColor, out Color color);
                return color;
            }
        }

        // A full-height button tinted with the Steam log color.
        public static bool SteamButton(string label, params GUILayoutOption[] options)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = ButtonColor;
            bool pressed = GUILayout.Button(label, options);
            GUI.backgroundColor = previous;
            return pressed;
        }

        public static SaiServer FindServer(Component component)
        {
            return SaiServer.Instance != null ? SaiServer.Instance : component.GetComponentInParent<SaiServer>();
        }

        // The game's Steam AppID from public info and, in Play Mode, a mismatch with
        // the AppID SteamAPI runs as. Returns true when the AppID is usable.
        public static bool DrawAppId(SteamTicketService service, SaiServer server)
        {
            if (server == null)
            {
                EditorGUILayout.HelpBox("Place this component under a SaiServer to read the game's Steam AppID.", MessageType.Warning);
                return false;
            }

            PublicGameInfoLookup.Update(server);
            long serverAppId = PublicGameInfoLookup.SteamAppId;
            string serverValue = serverAppId > 0 ? serverAppId.ToString()
                : PublicGameInfoLookup.Loaded ? "Steam is not enabled for this game" : "";

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent("sai server app id", "Read only. steam_app_id from the game's public info on the Sai Server."),
                    serverValue);
            }
            using (new EditorGUI.DisabledScope(PublicGameInfoLookup.IsLoading || string.IsNullOrEmpty(server.GameId)))
            {
                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(PublicGameInfoLookup.RefreshButtonWidth)))
                    PublicGameInfoLookup.Refresh(server);
            }
            EditorGUILayout.EndHorizontal();
            if (serverAppId == 0 && !PublicGameInfoLookup.IsLoading)
                EditorGUILayout.HelpBox("The Steam App ID is entered in Game Settings > Steam on the Sai Server dashboard, not here.", MessageType.Warning);
            PublicGameInfoLookup.DrawStatus();

            bool mismatch = false;
            SteamworksNetTicketProvider provider = service.TicketProvider as SteamworksNetTicketProvider;
            if (Application.isPlaying && provider != null)
            {
                uint runningAppId = provider.RunningAppId;
                mismatch = runningAppId > 0 && serverAppId > 0 && runningAppId != serverAppId;
                if (mismatch)
                {
                    EditorGUILayout.HelpBox(
                        $"SteamAPI runs as AppID {runningAppId}, but the Sai Server has AppID {serverAppId}. Steam requests will fail. " +
                        "Fix the game's Steam settings, then restart the Editor.", MessageType.Error);
                }
            }
            return serverAppId > 0 && !mismatch;
        }

        // Ticket provider problems: missing provider or a SteamAPI start error.
        public static void DrawProviderProblems(SteamTicketService service)
        {
            if (service.TicketProvider == null)
                EditorGUILayout.HelpBox("Assign a ticket provider (for example SteamworksNetTicketProvider) in steam settings.", MessageType.Warning);
            SteamworksNetTicketProvider provider = service.TicketProvider as SteamworksNetTicketProvider;
            if (provider != null && !string.IsNullOrEmpty(provider.InitError))
                EditorGUILayout.HelpBox(provider.InitError + "\n" + SteamErrorGuidance.For(provider.InitError), MessageType.Warning);
        }

        public static void DrawLastError(SteamTicketService service)
        {
            if (string.IsNullOrEmpty(service.LastError)) return;
            EditorGUILayout.HelpBox(service.LastError + "\n" + SteamErrorGuidance.For(service.LastError), MessageType.Error);
        }

        // A ✔ / ✘ status line; message may use rich text.
        public static void DrawStatus(bool met, string message)
        {
            if (statusStyle == null) statusStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            string mark = met ? $"<color={MetColor}>✔</color>" : $"<color={UnmetColor}>✘</color>";
            EditorGUILayout.LabelField(mark + " " + message, statusStyle);
        }

        // "Select (ClassName)": selects that component, same height as the button beside it.
        public static void DrawSelectButton(Object component, string className)
        {
            if (selectStyle == null) selectStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            using (new EditorGUI.DisabledScope(component == null))
            {
                if (GUILayout.Button(new GUIContent("Select (" + className + ")", "Select the " + className + " component."),
                        selectStyle, GUILayout.Width(SelectButtonWidth), GUILayout.Height(ButtonHeight)))
                    Select(component);
            }
        }

        // Selects the component's GameObject with only that component expanded in the
        // Inspector; the other components on the same GameObject are collapsed.
        public static void Select(Object target)
        {
            if (target is Component component)
            {
                foreach (Component other in component.GetComponents<Component>())
                    if (other != null) UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(other, other == component);
            }
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        public static void DrawTicketSettings(SerializedObject serializedObject)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ticketProvider"), new GUIContent("ticket provider"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ticketTimeoutSeconds"), new GUIContent("ticket timeout seconds"));
        }

        public static void DrawResult(SerializedObject serializedObject)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ownership"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("reward"));
            }
        }

        public static bool ShowDebug => SaiServer.Instance == null || SaiServer.Instance.ShowDebug;

        public static void LogInfo(string message)
        {
            if (ShowDebug) Debug.Log($"[Editor] {message}");
        }

        public static void LogError(string error)
        {
            if (ShowDebug) Debug.LogWarning($"[Editor] Steam request failed: {error}");
        }

        public static void LogResult(string message, SteamLinkResponse result)
        {
            if (!ShowDebug) return;
            string reward = result != null && result.reward != null ? result.reward.status : "none";
            Debug.Log($"[Editor] {message} (reward: {reward})");
        }
    }
}
