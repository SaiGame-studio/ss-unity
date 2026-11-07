using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaiGame.Services
{
    /// <summary>
    /// Applies values from .env.build (project root) to every SaiServer in each scene
    /// while a player build is running. Only the built copy of the scene is changed;
    /// the scene and prefab assets in the project stay untouched.
    /// Supported keys: GAME_ID (applied only when SaiServer.BuildEnvOverrideGameId is on).
    /// </summary>
    public class BuildEnvSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // report is null when the scene is processed for Play mode, not for a build.
            if (report == null) return;

            List<SaiServer> servers = FindSaiServers(scene);
            servers.RemoveAll(server => !server.BuildEnvOverrideGameId);
            if (servers.Count == 0) return;

            Dictionary<string, string> env = EnvLoader.LoadBuild();
            if (!env.TryGetValue(EnvLoader.KEY_GAME_ID, out string gameId) || string.IsNullOrWhiteSpace(gameId))
            {
                Debug.LogWarning($"[BuildEnv] GAME_ID not found in {EnvLoader.BuildEnvFilePath} — scene '{scene.name}' keeps the Game Id set in the inspector.");
                return;
            }

            gameId = gameId.Trim();
            if (!Guid.TryParse(gameId, out _))
            {
                throw new BuildFailedException($"[BuildEnv] GAME_ID in {EnvLoader.BUILD_ENV_FILE_NAME} is not a valid UUID: '{gameId}'.");
            }

            foreach (SaiServer server in servers)
            {
                SerializedObject serverSO = new SerializedObject(server);
                SerializedProperty gameIdProp = serverSO.FindProperty("gameId");
                if (gameIdProp == null)
                {
                    throw new BuildFailedException("[BuildEnv] SaiServer has no serialized 'gameId' field.");
                }
                gameIdProp.stringValue = gameId;
                serverSO.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log($"[BuildEnv] Applied GAME_ID from {EnvLoader.BUILD_ENV_FILE_NAME} to {servers.Count} SaiServer(s) in scene '{scene.name}'.");
        }

        private static List<SaiServer> FindSaiServers(Scene scene)
        {
            var servers = new List<SaiServer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                servers.AddRange(root.GetComponentsInChildren<SaiServer>(true));
            }
            return servers;
        }
    }
}
