using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaiGame.Services
{
    public class LuaScriptManager : SaiBehaviour
    {
        public const string SCRIPT_FOLDER_ASSET_PATH = "Assets/SaiGame/LuaScript/Scripts";

        [SerializeField] private string battleStartScriptName = "battle_start";
        [SerializeField] private string battleTurnScriptName = "battle_debug_turn";
        [SerializeField] private string battleEndScriptName = "battle_end";
        [SerializeField] private List<ScriptFileRecord> scriptFiles = new List<ScriptFileRecord>();

        public string BattleStartScriptName => this.battleStartScriptName;

        public string BattleTurnScriptName => this.battleTurnScriptName;

        public string BattleEndScriptName => this.battleEndScriptName;

        public void LoadScripts(Action<string> onSuccess = null, Action<string> onError = null)
        {
            this.LoadLocalScriptFiles();

            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found. Local scripts were loaded only.");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated. Local scripts were loaded only.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SaiServer.Instance.GameId))
            {
                onError?.Invoke("Game Id is required. Local scripts were loaded only.");
                return;
            }

            this.StartCoroutine(this.LoadBackendScriptsCoroutine(onSuccess, onError));
        }

        public void CreateScriptByName(string scriptName, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.FindScriptFileByName(scriptName);
            if (scriptFile == null)
            {
                scriptFile = new ScriptFileRecord($"{scriptName}.lua");
                if (!File.Exists(this.GetScriptFullPath(scriptFile)))
                {
                    onError?.Invoke($"Script file not found: {scriptFile.FileName}");
                    return;
                }

                this.scriptFiles.Add(scriptFile);
            }

            this.CreateScript(scriptFile, onSuccess, onError);
        }

        public void UpdateScriptByName(string scriptName, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.FindScriptFileByName(scriptName);
            if (scriptFile == null)
            {
                onError?.Invoke("Load scripts and set Script Id before updating this script.");
                return;
            }

            this.UpdateScript(scriptFile, onSuccess, onError);
        }

        public void CreateScriptAtIndex(int index, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.GetScriptFileAtIndex(index, onError);
            if (scriptFile == null)
            {
                return;
            }

            this.CreateScript(scriptFile, onSuccess, onError);
        }

        public void UpdateScriptAtIndex(int index, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.GetScriptFileAtIndex(index, onError);
            if (scriptFile == null)
            {
                return;
            }

            this.UpdateScript(scriptFile, onSuccess, onError);
        }

        public void DeleteScriptAtIndex(int index, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.GetScriptFileAtIndex(index, onError);
            if (scriptFile == null)
            {
                return;
            }

            this.DeleteScript(scriptFile, onSuccess, onError);
        }

        public void DownloadScriptAtIndex(int index, Action<string> onSuccess = null, Action<string> onError = null)
        {
            ScriptFileRecord scriptFile = this.GetScriptFileAtIndex(index, onError);
            if (scriptFile == null)
            {
                return;
            }

            if (!scriptFile.HasBackendScript)
            {
                onError?.Invoke("Backend script is required for download.");
                return;
            }

            if (string.IsNullOrWhiteSpace(scriptFile.ScriptName))
            {
                onError?.Invoke("Script name is required for download.");
                return;
            }

            if (string.IsNullOrEmpty(scriptFile.BackendScriptBody))
            {
                onError?.Invoke("Backend script body is empty.");
                return;
            }

            string fileName = $"{scriptFile.ScriptName}.lua";
            scriptFile.SetLocalFile(fileName, scriptFile.ScriptName);
            Directory.CreateDirectory(Path.GetFullPath(SCRIPT_FOLDER_ASSET_PATH));
            File.WriteAllText(this.GetScriptFullPath(scriptFile), scriptFile.BackendScriptBody);
            onSuccess?.Invoke(fileName);
        }

        private void CreateScript(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            if (!this.CanSendScriptRequest(scriptFile, true, onError))
            {
                return;
            }

            this.StartCoroutine(this.CreateScriptCoroutine(scriptFile, onSuccess, onError));
        }

        private void UpdateScript(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            if (!this.CanSendScriptRequest(scriptFile, true, onError))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(scriptFile.ScriptId))
            {
                onError?.Invoke("Script Id is required for update.");
                return;
            }

            this.StartCoroutine(this.UpdateScriptCoroutine(scriptFile, onSuccess, onError));
        }

        private void DeleteScript(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            if (!this.CanSendScriptRequest(scriptFile, false, onError))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(scriptFile.ScriptId))
            {
                onError?.Invoke("Script Id is required for delete.");
                return;
            }

            this.StartCoroutine(this.DeleteScriptCoroutine(scriptFile, onSuccess, onError));
        }

        private ScriptFileRecord FindScriptFileByName(string scriptName)
        {
            string fileName = $"{scriptName}.lua";
            for (int index = 0; index < this.scriptFiles.Count; index++)
            {
                ScriptFileRecord scriptFile = this.scriptFiles[index];
                if (scriptFile != null && (scriptFile.ScriptName == scriptName || scriptFile.FileName == fileName))
                {
                    return scriptFile;
                }
            }

            return null;
        }

        private ScriptFileRecord GetScriptFileAtIndex(int index, Action<string> onError)
        {
            if (index < 0 || index >= this.scriptFiles.Count)
            {
                onError?.Invoke("Script index is invalid.");
                return null;
            }

            return this.scriptFiles[index];
        }

        private void LoadLocalScriptFiles()
        {
            string folderPath = Path.GetFullPath(SCRIPT_FOLDER_ASSET_PATH);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            for (int index = 0; index < this.scriptFiles.Count; index++)
            {
                if (this.scriptFiles[index] != null)
                {
                    this.scriptFiles[index].ClearLocalFile();
                }
            }

            string[] filePaths = Directory.GetFiles(folderPath, "*.lua", SearchOption.TopDirectoryOnly);
            Array.Sort(filePaths);

            for (int index = 0; index < filePaths.Length; index++)
            {
                string fileName = Path.GetFileName(filePaths[index]);
                string scriptName = Path.GetFileNameWithoutExtension(fileName);
                ScriptFileRecord scriptFile = this.FindScriptFileByName(scriptName);
                if (scriptFile == null)
                {
                    scriptFile = new ScriptFileRecord(fileName);
                    this.scriptFiles.Add(scriptFile);
                }

                scriptFile.SetLocalFile(fileName, scriptName);
            }

            this.scriptFiles.RemoveAll(scriptFile => scriptFile == null || (!scriptFile.HasLocalFile && !scriptFile.HasBackendScript));
        }

        private IEnumerator LoadBackendScriptsCoroutine(Action<string> onSuccess, Action<string> onError)
        {
            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/scripts";
            yield return SaiServer.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        this.MergeBackendScripts(response);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception exception)
                    {
                        onError?.Invoke($"Parse backend scripts failed: {exception.Message}");
                    }
                },
                onError);
        }

        private void MergeBackendScripts(string response)
        {
            for (int index = 0; index < this.scriptFiles.Count; index++)
            {
                if (this.scriptFiles[index] != null)
                {
                    this.scriptFiles[index].ClearBackendScript();
                }
            }

            BackendScriptListResponse listResponse = this.ParseBackendScriptList(response);
            BackendScriptRecord[] backendScripts = this.GetBackendScripts(listResponse);
            if (backendScripts == null)
            {
                return;
            }

            for (int index = 0; index < backendScripts.Length; index++)
            {
                BackendScriptRecord backendScript = backendScripts[index];
                if (backendScript == null || string.IsNullOrWhiteSpace(backendScript.name))
                {
                    continue;
                }

                ScriptFileRecord scriptFile = this.FindScriptFileByName(backendScript.name);
                if (scriptFile == null)
                {
                    scriptFile = new ScriptFileRecord();
                    this.scriptFiles.Add(scriptFile);
                }

                scriptFile.SetBackendScript(backendScript);
            }

            this.scriptFiles.RemoveAll(scriptFile => scriptFile == null || (!scriptFile.HasLocalFile && !scriptFile.HasBackendScript));
        }

        private BackendScriptListResponse ParseBackendScriptList(string response)
        {
            string trimmedResponse = string.IsNullOrWhiteSpace(response) ? "{}" : response.Trim();
            if (trimmedResponse.StartsWith("["))
            {
                trimmedResponse = $"{{\"scripts\":{trimmedResponse}}}";
            }

            return JsonUtility.FromJson<BackendScriptListResponse>(trimmedResponse);
        }

        private BackendScriptRecord[] GetBackendScripts(BackendScriptListResponse response)
        {
            if (response == null)
            {
                return null;
            }

            if (response.scripts != null)
            {
                return response.scripts;
            }

            if (response.data != null)
            {
                return response.data;
            }

            return response.items;
        }

        private IEnumerator CreateScriptCoroutine(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            string scriptBody = this.ReadScriptBody(scriptFile);
            CreateRequest request = new CreateRequest
            {
                name = this.GetScriptName(scriptFile),
                description = scriptFile.Description,
                script_body = scriptBody
            };

            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/scripts";
            string jsonBody = JsonUtility.ToJson(request);

            yield return SaiServer.Instance.PostRequest(endpoint, jsonBody,
                response => this.HandleScriptResponse(response, scriptFile, onSuccess, onError),
                onError);
        }

        private IEnumerator UpdateScriptCoroutine(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            string scriptBody = this.ReadScriptBody(scriptFile);
            UpdateRequest request = new UpdateRequest
            {
                description = scriptFile.Description,
                script_body = scriptBody,
                is_active = scriptFile.IsActive
            };

            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/scripts/{scriptFile.ScriptId}";
            string jsonBody = JsonUtility.ToJson(request);

            yield return SaiServer.Instance.PatchRequest(endpoint, jsonBody,
                response => this.HandleScriptResponse(response, scriptFile, onSuccess, onError),
                onError);
        }

        private IEnumerator DeleteScriptCoroutine(ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/scripts/{scriptFile.ScriptId}";
            yield return SaiServer.Instance.DeleteRequest(endpoint,
                response =>
                {
                    scriptFile.SetScriptId(string.Empty);
                    onSuccess?.Invoke(response);
                },
                onError);
        }

        private bool CanSendScriptRequest(ScriptFileRecord scriptFile, bool requireLocalFile, Action<string> onError)
        {
            if (SaiServer.Instance == null)
            {
                onError?.Invoke("SaiServer not found.");
                return false;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated. Please login first.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SaiServer.Instance.GameId))
            {
                onError?.Invoke("Game Id is required.");
                return false;
            }

            if (scriptFile == null || string.IsNullOrWhiteSpace(scriptFile.ScriptName))
            {
                onError?.Invoke("Script name is required.");
                return false;
            }

            if (requireLocalFile)
            {
                string fullPath = this.GetScriptFullPath(scriptFile);
                if (!File.Exists(fullPath))
                {
                    onError?.Invoke($"Script file not found: {scriptFile.FileName}");
                    return false;
                }
            }

            return true;
        }

        private void HandleScriptResponse(string response, ScriptFileRecord scriptFile, Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                ApiResponse apiResponse = JsonUtility.FromJson<ApiResponse>(response);
                if (apiResponse != null && !string.IsNullOrWhiteSpace(apiResponse.id))
                {
                    scriptFile.SetScriptId(apiResponse.id);
                }

                onSuccess?.Invoke(response);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Parse script response failed: {exception.Message}");
            }
        }

        private string ReadScriptBody(ScriptFileRecord scriptFile)
        {
            return File.ReadAllText(this.GetScriptFullPath(scriptFile));
        }

        private string GetScriptFullPath(ScriptFileRecord scriptFile)
        {
            return Path.GetFullPath($"{SCRIPT_FOLDER_ASSET_PATH}/{scriptFile.FileName}");
        }

        private string GetScriptName(ScriptFileRecord scriptFile)
        {
            return !string.IsNullOrWhiteSpace(scriptFile.ScriptName)
                ? scriptFile.ScriptName
                : Path.GetFileNameWithoutExtension(scriptFile.FileName);
        }

        [Serializable]
        private class ScriptFileRecord
        {
            [SerializeField] private string fileName = "";
            [SerializeField] private string scriptName = "";
            [SerializeField] private string scriptId = "";
            [SerializeField] private string description = "";
            [SerializeField] private string backendScriptBody = "";
            [SerializeField] private bool isActive = true;
            [SerializeField] private bool hasLocalFile = false;
            [SerializeField] private bool hasBackendScript = false;

            public string FileName => this.fileName;

            public string ScriptName => this.scriptName;

            public string ScriptId => this.scriptId;

            public string Description => this.description;

            public string BackendScriptBody => this.backendScriptBody;

            public bool IsActive => this.isActive;

            public bool HasLocalFile => this.hasLocalFile;

            public bool HasBackendScript => this.hasBackendScript;

            public ScriptFileRecord()
            {
            }

            public ScriptFileRecord(string fileName)
            {
                this.SetLocalFile(fileName, Path.GetFileNameWithoutExtension(fileName));
            }

            public void SetLocalFile(string fileName, string scriptName)
            {
                this.fileName = fileName;
                this.scriptName = scriptName;
                this.hasLocalFile = true;
            }

            public void ClearLocalFile()
            {
                this.fileName = string.Empty;
                this.hasLocalFile = false;
            }

            public void SetBackendScript(BackendScriptRecord backendScript)
            {
                this.scriptName = backendScript.name;
                this.scriptId = backendScript.id;
                this.description = backendScript.description;
                this.backendScriptBody = backendScript.script_body;
                this.isActive = backendScript.is_active;
                this.hasBackendScript = true;
            }

            public void ClearBackendScript()
            {
                this.scriptId = string.Empty;
                this.backendScriptBody = string.Empty;
                this.hasBackendScript = false;
            }

            public void SetScriptId(string scriptId)
            {
                this.scriptId = scriptId;
                this.hasBackendScript = !string.IsNullOrWhiteSpace(scriptId);
            }
        }

        [Serializable]
        private class CreateRequest
        {
            public string name;
            public string description;
            public string script_body;
        }

        [Serializable]
        private class UpdateRequest
        {
            public string description;
            public string script_body;
            public bool is_active;
        }

        [Serializable]
        private class ApiResponse
        {
            public string id;
        }

        [Serializable]
        private class BackendScriptListResponse
        {
            public BackendScriptRecord[] scripts;
            public BackendScriptRecord[] data;
            public BackendScriptRecord[] items;
        }

        [Serializable]
        private class BackendScriptRecord
        {
            public string id;
            public string name;
            public string description;
            public string script_body;
            public int version;
            public bool is_active;
            public string created_by;
            public string created_at;
            public string updated_at;
        }
    }
}