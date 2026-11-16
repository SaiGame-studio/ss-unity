using System;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SteamAchievements))]
    public class SteamAchievementsEditor : Editor
    {
        private const string PREF_STEAM_SETTINGS = "SteamAchievementsEditor.showSteamSettings";

        private string progressName = "";
        private int progressCurrent;
        private int progressMax = 10;
        private string statName = "";
        private bool statIsFloat;
        private int statInt;
        private float statFloat;
        private string statReadResult = "";
        private bool resetAchievementsToo = true;

        private bool showSteamSettings
        {
            get => EditorPrefs.GetBool(PREF_STEAM_SETTINGS, true);
            set => EditorPrefs.SetBool(PREF_STEAM_SETTINGS, value);
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SteamAchievements component = (SteamAchievements)target;

            EditorGUILayout.LabelField("achievement state", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isLoading"), new GUIContent("is loading"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("statsLoaded"), new GUIContent("stats loaded"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lastError"), new GUIContent("last error"));

            EditorGUILayout.Space();

            bool steamSettings = EditorGUILayout.Foldout(this.showSteamSettings, "steam settings", true);
            if (steamSettings != this.showSteamSettings) this.showSteamSettings = steamSettings;
            if (this.showSteamSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("steamClient"), new GUIContent("steam client"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("statsTimeoutSeconds"), new GUIContent("stats timeout seconds"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            if (component.SteamClient == null)
                EditorGUILayout.HelpBox("Assign the SteamworksNetTicketProvider in steam settings.", MessageType.Warning);

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Enter Play Mode with the Steam client running to load and unlock achievements.", MessageType.Info);
                return;
            }

            this.DrawActions(component);
            this.DrawAchievements(component);
            this.DrawProgress(component);
            this.DrawStats(component);
            this.DrawLastError(component);
        }

        private void DrawActions(SteamAchievements component)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(component.IsLoading))
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Load Achievements", GUILayout.Height(25)))
                    component.LoadAchievements(list => this.Log($"Loaded {list.Count} achievement(s)"), this.LogError);
                GUI.backgroundColor = Color.white;
            }

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Reset All Stats", GUILayout.Height(25)))
            {
                string scope = this.resetAchievementsToo ? "all stats and achievements" : "all stats";
                if (EditorUtility.DisplayDialog("Reset Steam stats", $"Reset {scope} of the signed-in Steam account for this AppID?", "Reset", "Cancel"))
                    component.ResetAll(this.resetAchievementsToo, list => this.Log("Steam stats reset"), this.LogError);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            this.resetAchievementsToo = EditorGUILayout.Toggle("reset achievements too", this.resetAchievementsToo);
        }

        private void DrawAchievements(SteamAchievements component)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"achievements ({component.Achievements.Count})", EditorStyles.boldLabel);

            if (component.Achievements.Count == 0)
            {
                EditorGUILayout.HelpBox(component.StatsLoaded
                    ? "This AppID has no achievements defined in Steamworks."
                    : "Press Load Achievements.", MessageType.None);
                return;
            }

            foreach (SteamAchievementInfo info in component.Achievements)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUIStyle status = new GUIStyle(EditorStyles.boldLabel);
                status.normal.textColor = info.achieved ? new Color(0.3f, 0.8f, 0.4f) : Color.gray;
                EditorGUILayout.LabelField(info.achieved ? "unlocked" : "locked", status, GUILayout.Width(70));
                EditorGUILayout.LabelField(string.IsNullOrEmpty(info.display_name) ? info.api_name : info.display_name, EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(component.IsLoading))
                {
                    if (!info.achieved && GUILayout.Button("Unlock", GUILayout.Width(70)))
                        component.Unlock(info.api_name, name => this.Log("Unlocked " + name), this.LogError);
                    if (info.achieved && GUILayout.Button("Clear", GUILayout.Width(70)))
                        component.Clear(info.api_name, name => this.Log("Cleared " + name), this.LogError);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("api name", info.api_name);
                if (!string.IsNullOrEmpty(info.description))
                    EditorGUILayout.LabelField("description", info.description, EditorStyles.wordWrappedLabel);
                if (info.hidden)
                    EditorGUILayout.LabelField("hidden", "yes");
                if (info.achieved && info.unlock_time > 0)
                    EditorGUILayout.LabelField("unlock time", DateTimeOffset.FromUnixTimeSeconds(info.unlock_time).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawProgress(SteamAchievements component)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("achievement progress notification", EditorStyles.boldLabel);
            this.progressName = EditorGUILayout.TextField("api name", this.progressName);
            this.progressCurrent = Mathf.Max(0, EditorGUILayout.IntField("current", this.progressCurrent));
            this.progressMax = Mathf.Max(1, EditorGUILayout.IntField("max", this.progressMax));
            if (GUILayout.Button("Indicate Progress", GUILayout.Height(22)))
                component.IndicateProgress(this.progressName.Trim(), (uint)this.progressCurrent, (uint)this.progressMax,
                    name => this.Log("Progress shown for " + name), this.LogError);
        }

        private void DrawStats(SteamAchievements component)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("stats", EditorStyles.boldLabel);
            this.statName = EditorGUILayout.TextField("stat name", this.statName);
            this.statIsFloat = EditorGUILayout.Toggle("float stat", this.statIsFloat);
            if (this.statIsFloat) this.statFloat = EditorGUILayout.FloatField("value", this.statFloat);
            else this.statInt = EditorGUILayout.IntField("value", this.statInt);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Get Stat", GUILayout.Height(22)))
            {
                string name = this.statName.Trim();
                bool found;
                if (this.statIsFloat) { found = component.TryGetStat(name, out float value); this.statReadResult = found ? value.ToString() : ""; }
                else { found = component.TryGetStat(name, out int value); this.statReadResult = found ? value.ToString() : ""; }
                if (!found) this.statReadResult = component.StatsLoaded ? "not found" : "load achievements first";
            }
            using (new EditorGUI.DisabledScope(component.IsLoading))
            {
                if (GUILayout.Button("Set Stat", GUILayout.Height(22)))
                {
                    string name = this.statName.Trim();
                    if (this.statIsFloat) component.SetStat(name, this.statFloat, n => this.Log("Stored stat " + n), this.LogError);
                    else component.SetStat(name, this.statInt, n => this.Log("Stored stat " + n), this.LogError);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(this.statReadResult))
                EditorGUILayout.LabelField("read value", this.statReadResult);
        }

        private void DrawLastError(SteamAchievements component)
        {
            if (string.IsNullOrEmpty(component.LastError)) return;
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(component.LastError + "\n" + Guidance(component.LastError), MessageType.Error);
        }

        private static string Guidance(string error)
        {
            switch (error)
            {
                case SteamAchievements.SteamworksMissing: return "Install the Steamworks.NET package (com.rlabrecque.steamworks.net).";
                case SteamAchievements.SteamClientRequired: return "Assign the SteamworksNetTicketProvider in steam settings.";
                case SteamAchievements.NotInitialized: return "SteamAPI is not initialized. Check steam_appid.txt and that the Steam client is running.";
                case SteamAchievements.NotLoggedOn: return "Sign in to the Steam client.";
                case SteamAchievements.StatsUnavailable: return "Steam could not return stats for this AppID and account.";
                case SteamAchievements.StatsTimeout: return "No answer from Steam. SteamAPI.RunCallbacks must run every frame.";
                case SteamAchievements.StoreFailed: return "Steam rejected the change. Check the achievement or stat definition in Steamworks.";
                case SteamAchievements.AchievementNotFound: return "No achievement with this API name in Steamworks, or it is not published.";
                case SteamAchievements.ProgressRejected: return "Already unlocked, or current is not below max.";
                case SteamAchievements.StatNotFound: return "No stat with this name and type, or the stat is set by the game server only.";
                case SteamAchievements.InvalidName: return "Enter an API name.";
                default: return "";
            }
        }

        private static bool ShowDebug => SaiServer.Instance == null || SaiServer.Instance.ShowDebug;

        private void Log(string message)
        {
            if (ShowDebug) Debug.Log("[Editor] " + message);
        }

        private void LogError(string error)
        {
            if (ShowDebug) Debug.LogWarning("[Editor] Steam achievement operation failed: " + error);
        }
    }
}
