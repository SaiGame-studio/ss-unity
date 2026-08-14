using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace SaiGame.Services
{
    /// <summary>Runtime cache of item definitions received from item APIs.</summary>
    public class ItemDefinitions : SaiBehaviour
    {
        [Header("Fetch Item Definition")]
        [SerializeField] private string fetchItemId = "";
        [SerializeField] private string fetchItemCode = "";

        [SerializeField] private List<ItemDefinitionData> itemDefinitions = new List<ItemDefinitionData>();

        public IReadOnlyList<ItemDefinitionData> Definitions => this.itemDefinitions;
        public string FetchItemId => this.fetchItemId;
        public string FetchItemCode => this.fetchItemCode;

        [Serializable]
        private class ItemDefinitionsListResponse
        {
            public ItemDefinitionData[] items;
        }

        public void Cache(ItemDefinitionData itemDefinition)
        {
            if (itemDefinition == null || string.IsNullOrEmpty(itemDefinition.id))
                return;

            for (int i = 0; i < this.itemDefinitions.Count; i++)
            {
                if (this.itemDefinitions[i] != null && this.itemDefinitions[i].id == itemDefinition.id)
                {
                    this.itemDefinitions[i] = itemDefinition;
                    return;
                }
            }

            this.itemDefinitions.Add(itemDefinition);
        }

        public void CacheRange(IEnumerable<ItemDefinitionData> definitions)
        {
            if (definitions == null)
                return;

            foreach (ItemDefinitionData definition in definitions)
                this.Cache(definition);
        }

        public ItemDefinitionData GetItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            foreach (ItemDefinitionData definition in this.itemDefinitions)
            {
                if (definition != null && definition.id == itemId)
                    return definition;
            }

            return null;
        }

        public ItemDefinitionData GetItemByCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode))
                return null;

            foreach (ItemDefinitionData definition in this.itemDefinitions)
            {
                if (definition != null && definition.item_code == itemCode)
                    return definition;
            }

            return null;
        }

        public ItemDefinitionData GetItemByItemCode(string itemCode) => this.GetItemByCode(itemCode);

        public void Clear() => this.itemDefinitions.Clear();

        /// <summary>Fetches one item definition by its ID and adds it to this cache.</summary>
        public void FetchById(
            string itemId,
            Action<ItemDefinitionData> onSuccess = null,
            Action<string> onError = null)
        {
            ItemDefinitionData cachedDefinition = this.GetItemById(itemId);
            if (cachedDefinition != null)
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                    Debug.Log("<color=#66CCFF>[ItemDefinitions]</color> → <b><color=#00FF88>cache hit</color></b> | FetchById", gameObject);

                onSuccess?.Invoke(cachedDefinition);
                return;
            }

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemDefinitions] ► Get by Item ID</b></color>", gameObject);

            if (!this.ValidateRequest(itemId, "Item ID", onError))
                return;

            StartCoroutine(this.FetchByIdCoroutine(itemId, onSuccess, onError));
        }

        /// <summary>Fetches one item definition by item code and adds it to this cache.</summary>
        public void FetchByCode(
            string itemCode,
            Action<ItemDefinitionData> onSuccess = null,
            Action<string> onError = null)
        {
            ItemDefinitionData cachedDefinition = this.GetItemByCode(itemCode);
            if (cachedDefinition != null)
            {
                if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                    Debug.Log("[ItemDefinitions] <b>Cache hit</b> | FetchByCode", gameObject);

                onSuccess?.Invoke(cachedDefinition);
                return;
            }

            if (SaiServer.Instance != null && SaiServer.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemDefinitions] ► Get by Item Code</b></color>", gameObject);

            if (!this.ValidateRequest(itemCode, "Item Code", onError))
                return;

            StartCoroutine(this.FetchByCodeCoroutine(itemCode, onSuccess, onError));
        }

        private bool ValidateRequest(string value, string fieldName, Action<string> onError)
        {
            if (SaiServer.Instance == null)
            {
                this.HandleFetchError("SaiServer not found!", onError);
                return false;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                this.HandleFetchError("Not authenticated! Please login first.", onError);
                return false;
            }

            if (string.IsNullOrEmpty(value))
            {
                this.HandleFetchError($"{fieldName} cannot be empty.", onError);
                return false;
            }

            return true;
        }

        private IEnumerator FetchByIdCoroutine(
            string itemId,
            Action<ItemDefinitionData> onSuccess,
            Action<string> onError)
        {
            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/items/{UnityWebRequest.EscapeURL(itemId)}";
            yield return SaiServer.Instance.GetRequest(
                endpoint,
                response => this.HandleSingleItemResponse(response, onSuccess, onError),
                error => this.HandleFetchError(error, onError));
        }

        private IEnumerator FetchByCodeCoroutine(
            string itemCode,
            Action<ItemDefinitionData> onSuccess,
            Action<string> onError)
        {
            string endpoint = $"/api/v1/games/{SaiServer.Instance.GameId}/items?item_code={UnityWebRequest.EscapeURL(itemCode)}";
            yield return SaiServer.Instance.GetRequest(
                endpoint,
                response => this.HandleItemCodeResponse(response, itemCode, onSuccess, onError),
                error => this.HandleFetchError(error, onError));
        }

        private void HandleSingleItemResponse(
            string response,
            Action<ItemDefinitionData> onSuccess,
            Action<string> onError)
        {
            try
            {
                string sanitized = InventoryJsonHelper.StringifyObjectFields(response);
                ItemDefinitionResponse wrapped = JsonUtility.FromJson<ItemDefinitionResponse>(sanitized);
                ItemDefinitionData definition = wrapped?.item_definition
                    ?? wrapped?.item
                    ?? wrapped?.data
                    ?? JsonUtility.FromJson<ItemDefinitionData>(sanitized);

                if (definition == null || string.IsNullOrEmpty(definition.id))
                    definition = this.ParseNestedItemDefinition(response);

                this.CacheAndComplete(definition, onSuccess, onError);
            }
            catch (Exception exception)
            {
                this.HandleFetchError($"Parse item definition response error: {exception.Message}", onError);
            }
        }

        private void HandleItemCodeResponse(
            string response,
            string itemCode,
            Action<ItemDefinitionData> onSuccess,
            Action<string> onError)
        {
            try
            {
                string sanitized = InventoryJsonHelper.StringifyObjectFields(response);
                ItemDefinitionsListResponse listResponse = JsonUtility.FromJson<ItemDefinitionsListResponse>(sanitized);
                ItemDefinitionData definition = null;

                if (listResponse?.items != null)
                {
                    foreach (ItemDefinitionData item in listResponse.items)
                    {
                        if (item != null && item.item_code == itemCode)
                        {
                            definition = item;
                            break;
                        }
                    }
                }

                if (definition == null)
                {
                    ItemDefinitionResponse wrapped = JsonUtility.FromJson<ItemDefinitionResponse>(sanitized);
                    definition = wrapped?.item_definition
                        ?? wrapped?.item
                        ?? wrapped?.data
                        ?? JsonUtility.FromJson<ItemDefinitionData>(sanitized);
                }

                if (definition == null || string.IsNullOrEmpty(definition.id))
                    definition = this.ParseNestedItemDefinition(response);

                this.CacheAndComplete(definition, onSuccess, onError);
            }
            catch (Exception exception)
            {
                this.HandleFetchError($"Parse item definition by code response error: {exception.Message}", onError);
            }
        }

        private ItemDefinitionData ParseNestedItemDefinition(string response)
        {
            string[] propertyNames = { "item_definition", "item", "data" };
            foreach (string propertyName in propertyNames)
            {
                string itemJson = this.ExtractObjectProperty(response, propertyName);
                if (string.IsNullOrEmpty(itemJson))
                    continue;

                ItemDefinitionData definition = JsonUtility.FromJson<ItemDefinitionData>(
                    InventoryJsonHelper.StringifyObjectFields(itemJson));
                if (definition != null && !string.IsNullOrEmpty(definition.id))
                    return definition;
            }

            return null;
        }

        private string ExtractObjectProperty(string json, string propertyName)
        {
            string key = $"\"{propertyName}\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return null;

            int valueIndex = keyIndex + key.Length;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex])) valueIndex++;
            if (valueIndex >= json.Length || json[valueIndex] != ':')
                return null;

            valueIndex++;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex])) valueIndex++;
            if (valueIndex >= json.Length || json[valueIndex] != '{')
                return null;

            int depth = 0;
            bool insideString = false;
            for (int i = valueIndex; i < json.Length; i++)
            {
                char current = json[i];
                if (insideString)
                {
                    if (current == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (current == '"')
                        insideString = false;

                    continue;
                }

                if (current == '"')
                {
                    insideString = true;
                    continue;
                }

                if (current == '{') depth++;
                if (current == '}' && --depth == 0)
                    return json.Substring(valueIndex, i - valueIndex + 1);
            }

            return null;
        }

        private void CacheAndComplete(
            ItemDefinitionData definition,
            Action<ItemDefinitionData> onSuccess,
            Action<string> onError)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id))
            {
                this.HandleFetchError("Item definition was missing from the response.", onError);
                return;
            }

            this.Cache(definition);
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.Log("<color=#66CCFF>[ItemDefinitions]</color> → <b><color=#00FF88>onSuccess</color></b> callback", gameObject);
            onSuccess?.Invoke(definition);
        }

        private void HandleFetchError(string error, Action<string> onError)
        {
            if (SaiServer.Instance != null && SaiServer.Instance.ShowCallbackLog)
                Debug.LogWarning($"<color=#66CCFF>[ItemDefinitions]</color> → <b><color=#FF4444>onError</color></b> callback | {error}", gameObject);

            onError?.Invoke(error);
        }
    }
}
