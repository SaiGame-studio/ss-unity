using System;
using System.Collections;
using UnityEngine;

namespace SaiGame.Services
{
    [DefaultExecutionOrder(-99)]
    public class ItemGenerator : SaiBehaviour
    {
        // Events for other classes to listen to
        public event Action<GeneratorsResponse> OnGetGeneratorsSuccess;
        public event Action<string> OnGetGeneratorsFailure;
        public event Action<GeneratorData> OnCheckGeneratorSuccess;
        public event Action<string> OnCheckGeneratorFailure;
        public event Action<GeneratorCollectResponse> OnCollectGeneratorSuccess;
        public event Action<string> OnCollectGeneratorFailure;

        [Header("Auto Load Settings")]
        [SerializeField] protected bool autoLoadOnLogin = false;

        [Header("Current Generator Data")]
        [SerializeField] protected GeneratorsResponse currentGenerators;

        public GeneratorsResponse CurrentGenerators => this.currentGenerators;
        public bool HasGenerators => this.currentGenerators != null
                                     && this.currentGenerators.generators != null
                                     && this.currentGenerators.generators.Length > 0;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.RegisterLoginListener();
            this.RegisterLogoutListener();
        }

        protected virtual void RegisterLoginListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLoginSuccess += this.HandleLoginSuccess;
        }

        protected virtual void RegisterLogoutListener()
        {
            if (SaiService.Instance?.SaiAuth == null) return;

            SaiService.Instance.SaiAuth.OnLogoutSuccess += this.HandleLogoutSuccess;
        }

        protected virtual void OnDestroy()
        {
            if (SaiService.Instance?.SaiAuth != null)
            {
                SaiService.Instance.SaiAuth.OnLoginSuccess -= this.HandleLoginSuccess;
                SaiService.Instance.SaiAuth.OnLogoutSuccess -= this.HandleLogoutSuccess;
            }
        }

        protected virtual void HandleLoginSuccess(LoginResponse response)
        {
            if (!this.autoLoadOnLogin) return;

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemGenerator] Auto-loading generators after successful login...");

            this.GetGenerators(
                onSuccess: generators =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemGenerator] Generators auto-loaded: {generators.generators.Length} generators");
                },
                onError: error =>
                {
                    if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        Debug.LogWarning($"[ItemGenerator] Auto-load generators failed: {error}");
                }
            );
        }

        protected virtual void HandleLogoutSuccess()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemGenerator] Logout successful, clearing generator data...");

            this.ClearLocalGenerators();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemGenerator] Generator data cleared successfully");
        }

        /// <summary>
        /// Fetches all player's generators from the server.
        /// Endpoint: GET /api/v1/games/{game_id}/generators
        /// </summary>
        public void GetGenerators(
            System.Action<GeneratorsResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#00FFFF><b>[ItemGenerator] ► Get Generators</b></color>", gameObject);

            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(this.GetGeneratorsCoroutine(onSuccess, onError));
        }

        private IEnumerator GetGeneratorsCoroutine(
            System.Action<GeneratorsResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/generators";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        // Wrap the array response in an object for JsonUtility
                        string wrappedResponse = "{\"generators\":" + response + "}";
                        GeneratorsResponse generatorsResponse = JsonUtility.FromJson<GeneratorsResponse>(wrappedResponse);
                        
                        // Save local calculation settings before updating
                        System.Collections.Generic.Dictionary<string, bool> localCalcSettings = new System.Collections.Generic.Dictionary<string, bool>();
                        if (this.currentGenerators != null && this.currentGenerators.generators != null)
                        {
                            foreach (GeneratorData oldGen in this.currentGenerators.generators)
                            {
                                localCalcSettings[oldGen.inventory_item_id] = oldGen.enableLocalCalculation;
                            }
                        }
                        
                        // Restore only the local calculation setting
                        // Sync checkpoint_at to NOW because server's ticket_count is already the CURRENT count
                        if (generatorsResponse.generators != null)
                        {
                            foreach (GeneratorData gen in generatorsResponse.generators)
                            {
                                // ticket_count from server = current real count → sync checkpoint to now
                                gen.SyncCheckpointToNow();
                                
                                // Restore local calculation setting if it existed
                                if (localCalcSettings.ContainsKey(gen.inventory_item_id))
                                {
                                    gen.enableLocalCalculation = localCalcSettings[gen.inventory_item_id];
                                }
                            }
                        }
                        
                        this.currentGenerators = generatorsResponse;

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        {
                            Debug.Log($"[ItemGenerator] Generators loaded: {generatorsResponse.generators.Length} generators");
                            foreach (GeneratorData gen in generatorsResponse.generators)
                            {
                                string genName = gen.definition != null ? gen.definition.name : "Unknown";
                                Debug.Log($"  → Generator: <b><color=#FFD700>{gen.inventory_item_id}</color></b> | Name: {genName} | Tickets: {gen.ticket_count}/{gen.tick_capacity} | Interval: {gen.production_interval_seconds}s");
                            }
                        }

                        this.OnGetGeneratorsSuccess?.Invoke(generatorsResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemGenerator] GetGenerators</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemGenerator.cs › GetGeneratorsCoroutine");
                        onSuccess?.Invoke(generatorsResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse get generators response error: {e.Message}";
                        this.OnGetGeneratorsFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] GetGenerators</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemGenerator.cs › GetGeneratorsCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnGetGeneratorsFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] GetGenerators</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemGenerator.cs › GetGeneratorsCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Clears generator data locally.
        /// </summary>
        public void ClearGenerators()
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log("<color=#FF6666><b>[ItemGenerator] ► Clear Generators</b></color>", gameObject);
            this.ClearLocalGenerators();

            if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                Debug.Log("[ItemGenerator] Generator data cleared locally");
        }

        private void ClearLocalGenerators()
        {
            this.currentGenerators = new GeneratorsResponse
            {
                generators = new GeneratorData[0]
            };
        }

        // ── Convenience query helpers ──────────────────────────────────────────

        /// <summary>Returns the locally cached generator with the given inventory_item_id, or null.</summary>
        public GeneratorData GetGeneratorByInventoryItemId(string inventoryItemId)
        {
            if (this.currentGenerators == null || this.currentGenerators.generators == null)
                return null;

            foreach (GeneratorData generator in this.currentGenerators.generators)
            {
                if (generator.inventory_item_id == inventoryItemId)
                    return generator;
            }

            return null;
        }

        /// <summary>Returns the locally cached generator with the given definition_id, or null.</summary>
        public GeneratorData GetGeneratorByDefinitionId(string definitionId)
        {
            if (this.currentGenerators == null || this.currentGenerators.generators == null)
                return null;

            foreach (GeneratorData generator in this.currentGenerators.generators)
            {
                if (generator.definition_id == definitionId)
                    return generator;
            }

            return null;
        }

        /// <summary>Returns all locally cached generators that have the given item code in their definition or output pool.</summary>
        public GeneratorData[] GetGeneratorsByOutputItemCode(string itemCode)
        {
            if (this.currentGenerators == null || this.currentGenerators.generators == null)
                return new GeneratorData[0];

            var result = new System.Collections.Generic.List<GeneratorData>();

            foreach (GeneratorData generator in this.currentGenerators.generators)
            {
                // Check if definition item_code matches
                if (generator.definition != null && generator.definition.item_code == itemCode)
                {
                    result.Add(generator);
                    continue;
                }

                // Check if any output pool item matches
                if (generator.definition != null && generator.definition.output_pool != null)
                {
                    foreach (var output in generator.definition.output_pool)
                    {
                        if (output.item_definition_id == itemCode)
                        {
                            result.Add(generator);
                            break;
                        }
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>Returns all locally cached generators that have pending units ready to collect.</summary>
        public GeneratorData[] GetGeneratorsWithPendingUnits()
        {
            if (this.currentGenerators == null || this.currentGenerators.generators == null)
                return new GeneratorData[0];

            var result = new System.Collections.Generic.List<GeneratorData>();

            foreach (GeneratorData generator in this.currentGenerators.generators)
            {
                if (generator.ticket_count > 0)
                    result.Add(generator);
            }

            return result.ToArray();
        }

        /// <summary>Returns all locally cached generators that have reached their capacity.</summary>
        public GeneratorData[] GetGeneratorsAtCapacity()
        {
            if (this.currentGenerators == null || this.currentGenerators.generators == null)
                return new GeneratorData[0];

            var result = new System.Collections.Generic.List<GeneratorData>();

            foreach (GeneratorData generator in this.currentGenerators.generators)
            {
                if (generator.ticket_count >= generator.capacity)
                    result.Add(generator);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Checks the current state of a specific generator by its inventory_item_id.
        /// Endpoint: GET /api/v1/games/{game_id}/generators/{inventory_item_id}
        /// </summary>
        public void CheckGenerator(
            string inventoryItemId,
            System.Action<GeneratorData> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00DDFF><b>[ItemGenerator] ► Check Generator: {inventoryItemId}</b></color>", gameObject);

            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(this.CheckGeneratorCoroutine(inventoryItemId, onSuccess, onError));
        }

        private IEnumerator CheckGeneratorCoroutine(
            string inventoryItemId,
            System.Action<GeneratorData> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/generators/{inventoryItemId}";

            yield return SaiService.Instance.GetRequest(endpoint,
                response =>
                {
                    try
                    {
                        GeneratorData generatorData = JsonUtility.FromJson<GeneratorData>(response);
                        
                        // Preserve local calculation setting from current generator
                        bool preservedLocalCalc = true; // default
                        if (this.currentGenerators != null && this.currentGenerators.generators != null)
                        {
                            for (int i = 0; i < this.currentGenerators.generators.Length; i++)
                            {
                                if (this.currentGenerators.generators[i].inventory_item_id == inventoryItemId)
                                {
                                    preservedLocalCalc = this.currentGenerators.generators[i].enableLocalCalculation;
                                    break;
                                }
                            }
                        }
                        
                        // Server's ticket_count = current real count → sync checkpoint to now
                        // Only preserve the local calculation setting
                        generatorData.SyncCheckpointToNow();
                        generatorData.enableLocalCalculation = preservedLocalCalc;

                        // Update the generator in currentGenerators if it exists
                        if (this.currentGenerators != null && this.currentGenerators.generators != null)
                        {
                            for (int i = 0; i < this.currentGenerators.generators.Length; i++)
                            {
                                if (this.currentGenerators.generators[i].inventory_item_id == inventoryItemId)
                                {
                                    this.currentGenerators.generators[i] = generatorData;
                                    break;
                                }
                            }
                        }

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                        {
                            string genName = generatorData.definition != null ? generatorData.definition.name : "Unknown";
                            Debug.Log($"[ItemGenerator] Generator checked: <b><color=#FFD700>{generatorData.inventory_item_id}</color></b> | Name: {genName} | Tickets: {generatorData.ticket_count}/{generatorData.tick_capacity} | Checkpoint: {generatorData.checkpoint_at}");
                        }

                        this.OnCheckGeneratorSuccess?.Invoke(generatorData);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemGenerator] CheckGenerator</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemGenerator.cs › CheckGeneratorCoroutine");
                        onSuccess?.Invoke(generatorData);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse check generator response error: {e.Message}";
                        this.OnCheckGeneratorFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] CheckGenerator</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemGenerator.cs › CheckGeneratorCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnCheckGeneratorFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] CheckGenerator</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemGenerator.cs › CheckGeneratorCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        /// <summary>
        /// Collects pending units from a specific generator.
        /// Endpoint: POST /api/v1/games/{game_id}/generators/{inventory_item_id}/collect
        /// </summary>
        public void CollectGenerator(
            string inventoryItemId,
            System.Action<GeneratorCollectResponse> onSuccess = null,
            System.Action<string> onError = null)
        {
            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
                Debug.Log($"<color=#00FF66><b>[ItemGenerator] ► Collect Generator: {inventoryItemId}</b></color>", gameObject);

            if (SaiService.Instance == null)
            {
                onError?.Invoke("SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                onError?.Invoke("Not authenticated! Please login first.");
                return;
            }

            StartCoroutine(this.CollectGeneratorCoroutine(inventoryItemId, onSuccess, onError));
        }

        private IEnumerator CollectGeneratorCoroutine(
            string inventoryItemId,
            System.Action<GeneratorCollectResponse> onSuccess,
            System.Action<string> onError)
        {
            string gameId = SaiService.Instance.GameId;
            string endpoint = $"/api/v1/games/{gameId}/generators/{inventoryItemId}/collect";

            yield return SaiService.Instance.PostRequest(endpoint, "{}",
                response =>
                {
                    try
                    {
                        GeneratorCollectResponse collectResponse = JsonUtility.FromJson<GeneratorCollectResponse>(response);

                        if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                            Debug.Log($"[ItemGenerator] Collected {collectResponse.units_collected} units of {collectResponse.output_item_code} | Output Item ID: <b><color=#FFD700>{collectResponse.output_inventory_item_id}</color></b>");

                        // Refresh generator state after collecting
                        this.CheckGenerator(inventoryItemId);

                        this.OnCollectGeneratorSuccess?.Invoke(collectResponse);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.Log("<color=#66CCFF>[ItemGenerator] CollectGenerator</color> → <b><color=#00FF88>onSuccess</color></b> callback | ItemGenerator.cs › CollectGeneratorCoroutine");
                        onSuccess?.Invoke(collectResponse);
                    }
                    catch (System.Exception e)
                    {
                        string errorMsg = $"Parse collect generator response error: {e.Message}";
                        this.OnCollectGeneratorFailure?.Invoke(errorMsg);
                        if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                            Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] CollectGenerator</color> → <b><color=#FF4444>onError</color></b> callback (parse) | ItemGenerator.cs › CollectGeneratorCoroutine | {errorMsg}");
                        onError?.Invoke(errorMsg);
                    }
                },
                error =>
                {
                    this.OnCollectGeneratorFailure?.Invoke(error);
                    if (SaiService.Instance != null && SaiService.Instance.ShowCallbackLog)
                        Debug.LogWarning($"<color=#66CCFF>[ItemGenerator] CollectGenerator</color> → <b><color=#FF4444>onError</color></b> callback (network) | ItemGenerator.cs › CollectGeneratorCoroutine | {error}");
                    onError?.Invoke(error);
                }
            );
        }

        // ── Local Calculation Helper Methods ──────────────────────────────────

        /// <summary>
        /// Toggles local calculation for a specific generator by inventory_item_id.
        /// When enabled: calculates current ticks from server's checkpoint (ticket_count + elapsed ticks).
        /// When disabled: displays server's ticket_count as-is (static value until next server sync).
        /// </summary>
        public void SetGeneratorLocalCalculation(string inventoryItemId, bool enabled)
        {
            GeneratorData generator = this.GetGeneratorByInventoryItemId(inventoryItemId);
            if (generator == null)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.LogWarning($"[ItemGenerator] Generator not found: {inventoryItemId}");
                return;
            }

            generator.enableLocalCalculation = enabled;

            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
            {
                string status = enabled ? "<color=#00FF88>ENABLED</color>" : "<color=#FF4444>DISABLED</color>";
                Debug.Log($"<color=#FFA500><b>[ItemGenerator] ► Local Calculation {status}</b></color> | Generator: <b><color=#FFD700>{inventoryItemId}</color></b> | <i>ItemGenerator.cs › SetGeneratorLocalCalculation</i>", gameObject);
            }
        }

        /// <summary>
        /// Gets the expected output for all items in a generator's output pool.
        /// Returns null if generator not found or has no output pool.
        /// </summary>
        public GeneratorExpectedOutput[] GetGeneratorExpectedOutput(string inventoryItemId)
        {
            GeneratorData generator = this.GetGeneratorByInventoryItemId(inventoryItemId);
            if (generator == null)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.LogWarning($"[ItemGenerator] Generator not found: {inventoryItemId}");
                return null;
            }

            if (generator.definition == null || generator.definition.output_pool == null || generator.definition.output_pool.Length == 0)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.LogWarning($"[ItemGenerator] Generator has no output pool: {inventoryItemId}");
                return null;
            }

            int currentPendingTicks = generator.GetCurrentPendingUnits();
            var expectedOutputs = new System.Collections.Generic.List<GeneratorExpectedOutput>();

            foreach (var output in generator.definition.output_pool)
            {
                float expectedDrops = currentPendingTicks * output.drop_rate;
                int expectedMin = Mathf.FloorToInt(expectedDrops * output.quantity_min);
                int expectedMax = Mathf.FloorToInt(expectedDrops * output.quantity_max);

                // Clamp to collect_cap (0 = unlimited)
                if (output.collect_cap > 0)
                {
                    expectedMin = Mathf.Min(expectedMin, output.collect_cap);
                    expectedMax = Mathf.Min(expectedMax, output.collect_cap);
                }

                expectedOutputs.Add(new GeneratorExpectedOutput
                {
                    item_definition_id = output.item_definition_id,
                    drop_rate = output.drop_rate,
                    quantity_min = output.quantity_min,
                    quantity_max = output.quantity_max,
                    collect_cap = output.collect_cap,
                    expected_min = expectedMin,
                    expected_max = expectedMax,
                    current_pending_ticks = currentPendingTicks
                });
            }

            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
            {
                string calcType = generator.enableLocalCalculation ? "<color=#00FF88>Calculated</color>" : "<color=#AAAAAA>Server</color>";
                
                // Build detailed output info
                string outputDetails = "";
                foreach (var exp in expectedOutputs)
                {
                    string expectedRange = exp.expected_min == exp.expected_max ? exp.expected_min.ToString() : $"{exp.expected_min}-{exp.expected_max}";
                    outputDetails += $"\n    • <b>Item:</b> {exp.item_definition_id}" +
                                    $"\n      <b>Drop Rate:</b> {exp.drop_rate * 100:F1}% | <b>Quantity:</b> {exp.quantity_min}-{exp.quantity_max} | <b>Cap:</b> {exp.collect_cap}" +
                                    $"\n      <b>Expected:</b> <color=#00FF88>{expectedRange}</color> items | <b>Ticks:</b> {exp.current_pending_ticks}";
                }
                
                Debug.Log(
                    $"<color=#00DDFF><b>[ItemGenerator] ► Get Expected Output</b></color>\n" +
                    $"  <b>Type:</b> {calcType}\n" +
                    $"  <b>Current Ticks:</b> <b>{currentPendingTicks}/{generator.capacity}</b>\n" +
                    $"  <b>Output Pool ({expectedOutputs.Count} items):</b>{outputDetails}\n" +
                    $"  <i>ItemGenerator.cs › GetGeneratorExpectedOutput</i>",
                    gameObject
                );
            }

            return expectedOutputs.ToArray();
        }

        /// <summary>
        /// Gets the formatted time until a generator reaches full capacity.
        /// Returns "Unknown" if generator not found or local calculation is disabled.
        /// </summary>
        public string GetGeneratorTimeUntilFull(string inventoryItemId)
        {
            GeneratorData generator = this.GetGeneratorByInventoryItemId(inventoryItemId);
            if (generator == null)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.LogWarning($"[ItemGenerator] Generator not found: {inventoryItemId}");
                return "Unknown";
            }

            if (!generator.enableLocalCalculation)
            {
                if (SaiService.Instance != null && SaiService.Instance.ShowDebug)
                    Debug.LogWarning($"[ItemGenerator] Local calculation is disabled for generator: {inventoryItemId}");
                return "Disabled";
            }

            string timeUntilFull = generator.GetTimeUntilFullFormatted();

            if (SaiService.Instance != null && SaiService.Instance.ShowButtonsLog)
            {
                string coloredTime = generator.IsAtCapacity() 
                    ? $"<color=#FFFF00>{timeUntilFull}</color>" 
                    : $"<color=#66DDFF>{timeUntilFull}</color>";
                Debug.Log($"<color=#9966FF><b>[ItemGenerator] ► Get Time Until Full</b></color> | Time: {coloredTime} | <i>ItemGenerator.cs › GetGeneratorTimeUntilFull</i>", gameObject);
            }

            return timeUntilFull;
        }

        /// <summary>
        /// Gets detailed info about a generator's current state (for debugging).
        /// </summary>
        public void LogGeneratorState(string inventoryItemId)
        {
            GeneratorData generator = this.GetGeneratorByInventoryItemId(inventoryItemId);
            if (generator == null)
            {
                Debug.LogWarning($"[ItemGenerator] Generator not found: {inventoryItemId}");
                return;
            }

            int currentPending = generator.GetCurrentPendingUnits();
            string timeUntilFull = generator.enableLocalCalculation ? generator.GetTimeUntilFullFormatted() : "N/A";
            bool isAtCapacity = generator.IsAtCapacity();

            // Get expected outputs using the new method
            GeneratorExpectedOutput[] expectedOutputs = this.GetGeneratorExpectedOutput(inventoryItemId);
            int totalExpectedMin = 0;
            int totalExpectedMax = 0;
            
            if (expectedOutputs != null)
            {
                foreach (var exp in expectedOutputs)
                {
                    totalExpectedMin += exp.expected_min;
                    totalExpectedMax += exp.expected_max;
                }
            }

            // Build definition info
            string definitionInfo = "N/A";
            if (generator.definition != null)
            {
                definitionInfo = $"{generator.definition.name} ({generator.definition.item_code}) - Rarity: {generator.definition.rarity}";
            }

            // Build output pool info using expected outputs
            string outputPoolInfo = "N/A";
            if (expectedOutputs != null && expectedOutputs.Length > 0)
            {
                outputPoolInfo = $"{expectedOutputs.Length} items:\n";
                foreach (var exp in expectedOutputs)
                {
                    string expectedRange = exp.expected_min == exp.expected_max ? exp.expected_min.ToString() : $"{exp.expected_min}-{exp.expected_max}";
                    outputPoolInfo += $"    • Item {exp.item_definition_id}: {exp.quantity_min}-{exp.quantity_max} units (Drop Rate: {exp.drop_rate * 100:F1}%, Cap: {exp.collect_cap}) → Expected: {expectedRange}\n";
                }
            }

            string totalExpectedRange = totalExpectedMin == totalExpectedMax ? totalExpectedMin.ToString() : $"{totalExpectedMin}-{totalExpectedMax}";

            Debug.Log(
                $"<color=#FF99FF><b>[ItemGenerator] ═══ Generator State ═══</b></color>\n" +
                $"  <b>Inventory Item ID:</b> <color=#FFD700>{generator.inventory_item_id}</color>\n" +
                $"  <b>Definition:</b> {definitionInfo}\n" +
                $"  <b>Output Pool:</b> {outputPoolInfo}" +
                $"  <b>Local Calculation:</b> {(generator.enableLocalCalculation ? "<color=#00FF88>ENABLED</color>" : "<color=#AAAAAA>DISABLED</color>")}\n" +
                $"  <b>Server Ticket Count:</b> {generator.ticket_count}\n" +
                $"  <b>Current Pending Ticks:</b> <color=#00DDFF>{currentPending}</color> / {generator.tick_capacity}\n" +
                $"  <b>Total Expected Items:</b> <color=#00FF88>{totalExpectedRange}</color>\n" +
                $"  <b>Production Interval:</b> {generator.production_interval_seconds}s\n" +
                $"  <b>Time Until Full:</b> {timeUntilFull}\n" +
                $"  <b>Status Server:</b> {(generator.is_full ? "<color=#FFFF00>FULL</color>" : "<color=#00FF88>PRODUCING</color>")} | Next Tick: {generator.next_tick_in_seconds}s\n" +
                $"  <b>Status Local:</b> {(isAtCapacity ? "<color=#FFFF00>AT CAPACITY</color>" : "<color=#00FF88>PRODUCING</color>")}\n" +
                $"  <b>Checkpoint:</b> {generator.checkpoint_at}\n" +
                $"<color=#FF99FF>═══════════════════════════</color>",
                this.gameObject
            );
        }
    }
}
