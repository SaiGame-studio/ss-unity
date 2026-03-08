using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(ItemGenerator))]
    [CanEditMultipleObjects]
    public class ItemGeneratorEditor : Editor
    {
        private ItemGenerator itemGenerator;
        private SerializedProperty autoLoadOnLogin;

        private bool showCurrentGenerators = true;
        private bool showGeneratorList = true;
        private bool showUtilityButtons = true;

        // Loading states per generator
        private readonly HashSet<string> loadingCheckGenerators = new HashSet<string>();
        private readonly HashSet<string> loadingCollectGenerators = new HashSet<string>();

        private double lastRepaintTime = 0;
        private const double repaintInterval = 1.0; // Repaint every second for real-time updates

        private void OnEnable()
        {
            this.itemGenerator = (ItemGenerator)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            EditorApplication.update += this.OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= this.OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // Auto-repaint every second if any generator has local calculation enabled
            if (EditorApplication.timeSinceStartup - this.lastRepaintTime > repaintInterval)
            {
                if (this.ShouldAutoRepaint())
                {
                    this.lastRepaintTime = EditorApplication.timeSinceStartup;
                    Repaint();
                }
            }
        }

        private bool ShouldAutoRepaint()
        {
            if (this.itemGenerator?.CurrentGenerators?.generators == null)
                return false;

            // Check if any generator has local calculation enabled
            foreach (GeneratorData gen in this.itemGenerator.CurrentGenerators.generators)
            {
                if (gen.enableLocalCalculation && gen.GetCurrentPendingUnits() < gen.capacity)
                    return true;
            }

            return false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Item Generator Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load generators when user logs in"));

            EditorGUILayout.Space();

            // Current Generator Data
            this.showCurrentGenerators = EditorGUILayout.Foldout(this.showCurrentGenerators, "Current Generator Data", true);
            if (this.showCurrentGenerators)
            {
                EditorGUI.indentLevel++;

                if (this.itemGenerator.CurrentGenerators != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Loaded Generators: {this.itemGenerator.CurrentGenerators.generators?.Length ?? 0}");

                    // Info box about local calculation
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(
                        "Local Calculation: Automatically calculates pending units based on elapsed time without calling the server. " +
                        "UI updates every second when enabled. Disable to show server values only.",
                        MessageType.Info);

                    if (this.itemGenerator.CurrentGenerators.generators != null
                        && this.itemGenerator.CurrentGenerators.generators.Length > 0)
                    {
                        this.showGeneratorList = EditorGUILayout.Foldout(this.showGeneratorList, "Generator List", true);
                        if (this.showGeneratorList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (GeneratorData generator in this.itemGenerator.CurrentGenerators.generators)
                            {
                                this.DrawGeneratorSummary(generator);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No generator data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Generators", GUILayout.Height(30)))
                {
                    this.LoadGenerators();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Generators", GUILayout.Height(30)))
                {
                    this.itemGenerator.ClearGenerators();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneratorSummary(GeneratorData generator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Highlight inventory_item_id
            GUIStyle idStyle = new GUIStyle(EditorStyles.boldLabel);
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f); // Gold color

            EditorGUILayout.LabelField("Inventory Item ID:", generator.inventory_item_id, idStyle);
            EditorGUILayout.LabelField($"Definition ID: {generator.definition_id}");
            EditorGUILayout.LabelField($"Output Item Code: {generator.output_item_code}");

            EditorGUILayout.Space(4);

            // Local Calculation Toggle
            EditorGUI.BeginChangeCheck();
            bool newLocalCalcValue = EditorGUILayout.Toggle(
                new GUIContent("Enable Local Calculation", "Calculate pending units locally based on elapsed time"),
                generator.enableLocalCalculation);
            if (EditorGUI.EndChangeCheck())
            {
                this.itemGenerator.SetGeneratorLocalCalculation(generator.inventory_item_id, newLocalCalcValue);
                Repaint();
            }

            EditorGUILayout.Space(2);

            // Display calculated vs server values
            int currentPending = generator.GetCurrentPendingUnits();
            
            if (generator.enableLocalCalculation)
            {
                // Show calculated value prominently
                GUIStyle calculatedStyle = new GUIStyle(EditorStyles.boldLabel);
                calculatedStyle.normal.textColor = new Color(0.4f, 1f, 0.6f); // Light green
                EditorGUILayout.LabelField($"Current Pending (Calculated): {currentPending} / {generator.capacity}", calculatedStyle);
                
                // Show server value as reference
                GUIStyle serverStyle = new GUIStyle(EditorStyles.miniLabel);
                serverStyle.normal.textColor = Color.gray;
                EditorGUILayout.LabelField($"  └─ Server Value: {generator.pending_units}", serverStyle);
            }
            else
            {
                // Show server value only
                EditorGUILayout.LabelField($"Pending Units (Server): {generator.pending_units} / {generator.capacity}");
            }

            // Progress bar with calculated value
            float fillPercentage = generator.capacity > 0 ? (float)currentPending / generator.capacity : 0f;
            Rect progressRect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(progressRect, fillPercentage, $"{currentPending}/{generator.capacity}");

            // Time until full (only if local calculation is enabled)
            if (generator.enableLocalCalculation)
            {
                string timeUntilFull = generator.GetTimeUntilFullFormatted();
                GUIStyle timeStyle = new GUIStyle(EditorStyles.miniLabel);
                if (generator.IsAtCapacity())
                {
                    timeStyle.normal.textColor = Color.yellow;
                }
                else
                {
                    timeStyle.normal.textColor = new Color(0.6f, 0.8f, 1f); // Light blue
                }
                EditorGUILayout.LabelField($"Time Until Full: {timeUntilFull}", timeStyle);
            }

            EditorGUILayout.LabelField($"Production Interval: {generator.production_interval_seconds}s");
            EditorGUILayout.LabelField($"Checkpoint At: {generator.checkpoint_at}");

            // Action Buttons
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            // Check Button
            bool isCheckLoading = this.loadingCheckGenerators.Contains(generator.inventory_item_id);
            GUI.backgroundColor = isCheckLoading ? Color.gray : new Color(0.3f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(isCheckLoading);
            if (GUILayout.Button(isCheckLoading ? "Checking..." : "Check", GUILayout.Height(24)))
            {
                this.CheckGenerator(generator.inventory_item_id);
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            // Collect Button - use calculated value for display
            bool isCollectLoading = this.loadingCollectGenerators.Contains(generator.inventory_item_id);
            bool hasUnits = currentPending > 0;
            GUI.backgroundColor = isCollectLoading ? Color.gray : (hasUnits ? new Color(0.2f, 1f, 0.4f) : new Color(0.5f, 0.5f, 0.5f));
            EditorGUI.BeginDisabledGroup(isCollectLoading || !hasUnits);
            if (GUILayout.Button(isCollectLoading ? "Collecting..." : $"Collect ({currentPending})", GUILayout.Height(24)))
            {
                this.CollectGenerator(generator.inventory_item_id);
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Developer Tools Buttons
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.9f, 1f);
            if (GUILayout.Button("Get Pending", GUILayout.Height(22)))
            {
                this.itemGenerator.GetGeneratorCurrentPendingUnits(generator.inventory_item_id);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.6f, 0.4f, 1f);
            if (GUILayout.Button("Get Time", GUILayout.Height(22)))
            {
                this.itemGenerator.GetGeneratorTimeUntilFull(generator.inventory_item_id);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.6f, 1f);
            if (GUILayout.Button("Log State", GUILayout.Height(22)))
            {
                this.itemGenerator.LogGeneratorState(generator.inventory_item_id);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void LoadGenerators()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemGeneratorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemGeneratorEditor] Not authenticated! Please login first.");
                return;
            }

            this.itemGenerator.GetGenerators(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemGeneratorEditor] Loaded {response.generators.Length} generators");

                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ItemGeneratorEditor] Failed to load generators: {error}");

                    Repaint();
                }
            );
        }

        private void CheckGenerator(string inventoryItemId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemGeneratorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemGeneratorEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingCheckGenerators.Add(inventoryItemId);
            Repaint();

            this.itemGenerator.CheckGenerator(
                inventoryItemId: inventoryItemId,
                onSuccess: generatorData =>
                {
                    this.loadingCheckGenerators.Remove(inventoryItemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemGeneratorEditor] Generator checked: {inventoryItemId} | Pending: {generatorData.pending_units}/{generatorData.capacity}");

                    Repaint();
                },
                onError: error =>
                {
                    this.loadingCheckGenerators.Remove(inventoryItemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ItemGeneratorEditor] Failed to check generator {inventoryItemId}: {error}");

                    Repaint();
                }
            );
        }

        private void CollectGenerator(string inventoryItemId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ItemGeneratorEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ItemGeneratorEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingCollectGenerators.Add(inventoryItemId);
            Repaint();

            this.itemGenerator.CollectGenerator(
                inventoryItemId: inventoryItemId,
                onSuccess: collectResponse =>
                {
                    this.loadingCollectGenerators.Remove(inventoryItemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ItemGeneratorEditor] Collected {collectResponse.units_collected} units of {collectResponse.output_item_code} | Output Item ID: {collectResponse.output_inventory_item_id}");

                    Repaint();
                },
                onError: error =>
                {
                    this.loadingCollectGenerators.Remove(inventoryItemId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ItemGeneratorEditor] Failed to collect from generator {inventoryItemId}: {error}");

                    Repaint();
                }
            );
        }
    }
}
