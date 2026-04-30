using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Reads .env file from project root and applies values to SaiServer / SaiAuth serialized fields.
    /// Supported keys: GAME_ID, USERNAME, PASSWORD.
    /// </summary>
    public static class EnvLoader
    {
        private static string EnvFilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), ".env");

        /// <summary>
        /// Parses the .env file and returns a key-value dictionary.
        /// Lines starting with '#' and empty lines are ignored.
        /// </summary>
        public static Dictionary<string, string> Load()
        {
            var result = new Dictionary<string, string>();

            if (!File.Exists(EnvFilePath))
            {
                return result;
            }

            foreach (string line in File.ReadAllLines(EnvFilePath))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                int separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = trimmed.Substring(0, separatorIndex).Trim();
                string value = trimmed.Substring(separatorIndex + 1).Trim();
                result[key] = value;
            }

            return result;
        }

        public static bool EnvFileExists() => File.Exists(EnvFilePath);

        /// <summary>
        /// Applies GAME_ID to SaiServer and USERNAME / PASSWORD to SaiAuth.
        /// Returns the number of fields that were applied.
        /// </summary>
        public static int ApplyToSaiServer(SaiServer saiServer)
        {
            if (saiServer == null)
            {
                return 0;
            }

            Dictionary<string, string> env = Load();
            if (env.Count == 0)
            {
                return 0;
            }

            int applied = 0;

            // Apply GAME_ID
            if (env.TryGetValue("GAME_ID", out string gameId))
            {
                SerializedObject serverSO = new SerializedObject(saiServer);
                SerializedProperty gameIdProp = serverSO.FindProperty("gameId");
                if (gameIdProp != null)
                {
                    gameIdProp.stringValue = gameId;
                    serverSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(saiServer);
                    applied++;
                }
            }

            if (applied > 0)
            {
                EditorSceneManager.MarkSceneDirty(saiServer.gameObject.scene);
            }

            return applied;
        }

        /// <summary>
        /// Applies .env values to live runtime instances using reflection.
        /// Safe to call from RuntimeInitializeOnLoadMethod (AfterSceneLoad).
        /// USERNAME / PASSWORD are written to PlayerPrefs only — never left in serialized fields.
        /// </summary>
        public static int ApplyAtRuntime(SaiServer saiServer)
        {
            if (saiServer == null) return 0;

            Dictionary<string, string> env = Load();
            if (env.Count == 0) return 0;

            int applied = 0;

            if (env.TryGetValue("GAME_ID", out string gameId))
            {
                FieldInfo field = FindField(saiServer.GetType(), "gameId");
                if (field != null)
                {
                    field.SetValue(saiServer, gameId);
                    applied++;
                }
            }

            bool hasUsername = env.ContainsKey("USERNAME");
            bool hasPassword = env.ContainsKey("PASSWORD");

            if (hasUsername || hasPassword)
            {
                SaiAuth saiAuth = saiServer.GetComponent<SaiAuth>();
                if (saiAuth != null)
                {
                    FieldInfo usernameField = FindField(typeof(SaiAuth), "username");
                    FieldInfo passwordField = FindField(typeof(SaiAuth), "password");

                    if (hasUsername && usernameField != null)
                    {
                        usernameField.SetValue(saiAuth, env["USERNAME"]);
                        applied++;
                    }

                    if (hasPassword && passwordField != null)
                    {
                        passwordField.SetValue(saiAuth, env["PASSWORD"]);
                        applied++;
                    }
                }
            }

            return applied;
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }
    }
}
