using System;
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

        // Collapse states per generator (keyed by inventory_item_id)
        private readonly Dictionary<string, bool> expandedGenerators = new Dictionary<string, bool>();

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
                        "Local Calculation: When enabled, calculates current ticks from server checkpoint + elapsed time. " +
                        "When disabled, displays server's ticket_count value (static until next sync).",
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

            // === COLLAPSIBLE HEADER ===
            string genId = generator.inventory_item_id;
            if (!this.expandedGenerators.ContainsKey(genId))
                this.expandedGenerators[genId] = false;

            // Build header label with key info
            string headerName = generator.definition != null ? generator.definition.name : genId;
            int currentTicks = generator.GetCurrentPendingUnits();
            string headerLabel = $"★ {headerName}  [{currentTicks}/{generator.tick_capacity}]";

            // Custom foldout style matching rarity color
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;
            if (generator.definition != null)
                foldoutStyle.normal.textColor = this.GetRarityColor(generator.definition.rarity);
            foldoutStyle.onNormal.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.focused.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.onFocused.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.active.textColor = foldoutStyle.normal.textColor;
            foldoutStyle.onActive.textColor = foldoutStyle.normal.textColor;

            EditorGUILayout.BeginHorizontal();
            this.expandedGenerators[genId] = EditorGUILayout.Foldout(this.expandedGenerators[genId], headerLabel, true, foldoutStyle);

            // Rarity badge (right-aligned)
            if (generator.definition != null)
            {
                GUIStyle rarityStyle = new GUIStyle(EditorStyles.label);
                rarityStyle.fontSize = 11;
                rarityStyle.normal.textColor = this.GetRarityColor(generator.definition.rarity);
                rarityStyle.fontStyle = FontStyle.Bold;
                rarityStyle.alignment = TextAnchor.MiddleRight;
                EditorGUILayout.LabelField(generator.definition.rarity.ToUpper(), rarityStyle, GUILayout.MinWidth(70));
            }
            EditorGUILayout.EndHorizontal();

            if (!this.expandedGenerators[genId])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            // Subtle separator
            GUIStyle separatorStyle = new GUIStyle(EditorStyles.label);
            separatorStyle.fontSize = 8;
            separatorStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", separatorStyle);

            // === COMPACT INFO ===
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            
            if (generator.definition != null)
            {
                EditorGUILayout.LabelField($"CODE: {generator.definition.item_code}", labelStyle);
            }
            
            EditorGUILayout.LabelField($"DEF: {generator.definition_id}", labelStyle);
            
            GUIStyle idStyle = new GUIStyle(EditorStyles.label);
            idStyle.fontSize = 10;
            idStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            EditorGUILayout.LabelField($"ID: {generator.inventory_item_id}", idStyle);

            EditorGUILayout.Space(8);

            // === LOCAL CALCULATION TOGGLE ===
            EditorGUI.BeginChangeCheck();
            bool newLocalCalcValue = EditorGUILayout.Toggle("Local Calculation", generator.enableLocalCalculation);
            if (EditorGUI.EndChangeCheck())
            {
                if (!newLocalCalcValue && generator.enableLocalCalculation)
                {
                    // Turning OFF: snapshot the current calculated value so counter freezes here
                    // GetCurrentPendingUnits() still works because enableLocalCalculation is still true
                    int currentValue = generator.GetCurrentPendingUnits();
                    generator.ticket_count = currentValue;
                    generator.checkpoint_at = System.DateTime.UtcNow.ToString("o");
                }
                
                generator.enableLocalCalculation = newLocalCalcValue;
                // When turning ON again, it calculates from snapshotted ticket_count + checkpoint_at
                // so counter continues from where it stopped
                
                EditorUtility.SetDirty(target);
                Repaint();
            }

            EditorGUILayout.Space(4);

            // === PRODUCTION STATUS ===
            int currentPending = generator.GetCurrentPendingUnits();
            string timeElapsed = this.GetTimeElapsed(generator.checkpoint_at);
            
            // Status bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUIStyle statusLabel = new GUIStyle(EditorStyles.label);
            statusLabel.fontSize = 11;
            statusLabel.fontStyle = FontStyle.Bold;
            statusLabel.normal.textColor = new Color(1f, 0.7f, 0.2f);
            EditorGUILayout.LabelField("⚡ PRODUCTION STATUS", statusLabel, GUILayout.Width(180));
            
            if (!string.IsNullOrEmpty(timeElapsed))
            {
                GUIStyle timeStyle = new GUIStyle(EditorStyles.label);
                timeStyle.fontSize = 10;
                timeStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);
                timeStyle.alignment = TextAnchor.MiddleRight;
                EditorGUILayout.LabelField($"running {timeElapsed}", timeStyle, GUILayout.ExpandWidth(true));
            }
            EditorGUILayout.EndHorizontal();

            // Tick info
            GUIStyle tickLabelStyle = new GUIStyle(EditorStyles.label);
            tickLabelStyle.fontSize = 11;
            tickLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            
            GUIStyle tickValueStyle = new GUIStyle(EditorStyles.boldLabel);
            tickValueStyle.fontSize = 13;
            tickValueStyle.normal.textColor = generator.is_full ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 1f, 0.6f);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ticks:", tickLabelStyle, GUILayout.Width(110));
            EditorGUILayout.LabelField($"{currentPending}/{generator.tick_capacity}", tickValueStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(2);
            
            // Interval info with dynamic countdown
            EditorGUILayout.BeginHorizontal();
            GUIStyle intervalLabelStyle = new GUIStyle(EditorStyles.label);
            intervalLabelStyle.fontSize = 10;
            intervalLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField("⏱ Interval:", intervalLabelStyle, GUILayout.Width(110));
            
            int dynamicNextTick = generator.GetDynamicNextTickSeconds();
            GUIStyle intervalValueStyle = new GUIStyle(EditorStyles.label);
            intervalValueStyle.fontSize = 11;
            intervalValueStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            EditorGUILayout.LabelField($"{dynamicNextTick}/{generator.production_interval_seconds}s", intervalValueStyle);
            EditorGUILayout.EndHorizontal();
            
            // Capacity time info (total time to full)
            EditorGUILayout.BeginHorizontal();
            GUIStyle capacityTimeLabelStyle = new GUIStyle(EditorStyles.label);
            capacityTimeLabelStyle.fontSize = 10;
            capacityTimeLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField("⏳ Capacity Time:", capacityTimeLabelStyle, GUILayout.Width(110));
            
            string totalTimeFormatted = generator.GetTotalTimeToFullFormatted();
            GUIStyle capacityTimeValueStyle = new GUIStyle(EditorStyles.label);
            capacityTimeValueStyle.fontSize = 10;
            capacityTimeValueStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            EditorGUILayout.LabelField($"{totalTimeFormatted} ({generator.tick_capacity} ticks)", capacityTimeValueStyle);
            EditorGUILayout.EndHorizontal();

            // Time remaining info
            EditorGUILayout.BeginHorizontal();
            GUIStyle remainingLabelStyle = new GUIStyle(EditorStyles.label);
            remainingLabelStyle.fontSize = 10;
            remainingLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField("⏱ Time Remaining:", remainingLabelStyle, GUILayout.Width(110));
            
            string timeRemaining = generator.GetTimeUntilFullFormatted();
            GUIStyle remainingValueStyle = new GUIStyle(EditorStyles.label);
            remainingValueStyle.fontSize = 10;
            remainingValueStyle.normal.textColor = currentPending >= generator.tick_capacity ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 1f, 0.6f);
            EditorGUILayout.LabelField(timeRemaining, remainingValueStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(4);
            
            // Progress bar
            float fillPercentage = generator.tick_capacity > 0 ? (float)currentPending / generator.tick_capacity : 0f;
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            
            // Draw background
            EditorGUI.DrawRect(progressRect, new Color(0.2f, 0.2f, 0.2f));
            
            // Draw fill
            Rect fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * fillPercentage, progressRect.height);
            Color barColor = generator.is_full ? new Color(1f, 0.7f, 0.2f) : new Color(0.3f, 0.8f, 0.5f);
            EditorGUI.DrawRect(fillRect, barColor);
            
            // Draw percentage text with shadow for better visibility
            GUIStyle percentStyle = new GUIStyle(GUI.skin.label);
            percentStyle.alignment = TextAnchor.MiddleCenter;
            percentStyle.fontSize = 11;
            percentStyle.fontStyle = FontStyle.Bold;
            percentStyle.normal.textColor = Color.white;
            
            // Draw shadow first
            Rect shadowRect = new Rect(progressRect.x + 1, progressRect.y + 1, progressRect.width, progressRect.height);
            GUIStyle shadowStyle = new GUIStyle(percentStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(shadowRect, $"{fillPercentage * 100:F0}%", shadowStyle);
            
            // Draw main text
            GUI.Label(progressRect, $"{fillPercentage * 100:F0}%", percentStyle);

            // Status message
            if (generator.is_full)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.label);
                warningStyle.fontSize = 11;
                warningStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
                EditorGUILayout.LabelField("⚠ Full — Production stopped", warningStyle);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // === OUTPUT POOL (Card-based layout) ===
            if (generator.definition != null && generator.definition.output_pool != null && generator.definition.output_pool.Length > 0)
            {
                GUIStyle poolHeader = new GUIStyle(EditorStyles.boldLabel);
                poolHeader.fontSize = 11;
                poolHeader.normal.textColor = new Color(0.7f, 0.9f, 1f);
                EditorGUILayout.LabelField($"📤 LOOT POOL ({generator.definition.output_pool.Length})", poolHeader);
                
                EditorGUILayout.Space(3);
                
                foreach (var output in generator.definition.output_pool)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    // Item Definition ID (full, no truncation)
                    GUIStyle itemIdStyle = new GUIStyle(EditorStyles.label);
                    itemIdStyle.fontSize = 10;
                    itemIdStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
                    itemIdStyle.fontStyle = FontStyle.Bold;
                    itemIdStyle.wordWrap = false;
                    EditorGUILayout.LabelField($"Item: {output.item_definition_id}", itemIdStyle);
                    
                    EditorGUILayout.Space(2);
                    
                    // Drop Rate
                    EditorGUILayout.BeginHorizontal();
                    GUIStyle lootLabelStyle = new GUIStyle(EditorStyles.label);
                    lootLabelStyle.fontSize = 9;
                    lootLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                    EditorGUILayout.LabelField("Drop Rate:", lootLabelStyle, GUILayout.Width(80));
                    
                    GUIStyle dropStyle = new GUIStyle(EditorStyles.label);
                    dropStyle.fontSize = 10;
                    dropStyle.normal.textColor = (output.drop_rate * 100) >= 50 ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 1f, 0.5f);
                    dropStyle.fontStyle = FontStyle.Bold;
                    EditorGUILayout.LabelField($"{output.drop_rate * 100:F1}%", dropStyle);
                    EditorGUILayout.EndHorizontal();
                    
                    // Quantity Range
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Quantity:", lootLabelStyle, GUILayout.Width(80));
                    
                    GUIStyle qtyStyle = new GUIStyle(EditorStyles.label);
                    qtyStyle.fontSize = 10;
                    qtyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                    EditorGUILayout.LabelField($"{output.quantity_min}-{output.quantity_max}", qtyStyle);
                    EditorGUILayout.EndHorizontal();
                    
                    // Expected Output Calculation
                    // drop_rate: 1 = 100%, 0.5 = 50%, etc.
                    // Logic: drop_rate determines how many ticks will drop items
                    // Each successful tick drops quantity_min to quantity_max items
                    bool isGuaranteed = output.drop_rate >= 1.0f;
                    float expectedDrops = currentPending * output.drop_rate; // Number of successful drops
                    
                    int rawMin, rawMax;
                    string expectedText;
                    
                    if (output.quantity_min == output.quantity_max)
                    {
                        // Fixed quantity per drop (e.g., 10-10 means always 10 if dropped)
                        int quantityPerDrop = output.quantity_min;
                        float expectedTotal = expectedDrops * quantityPerDrop;
                        
                        if (expectedDrops < 1.0f && !isGuaranteed)
                        {
                            // Low probability: might get nothing or at least 1 drop worth
                            // Example: 0.18 drops × 10 = might get 0 or 10
                            rawMin = 0;
                            rawMax = quantityPerDrop;
                            expectedText = $"0-{rawMax}";
                        }
                        else
                        {
                            // High enough probability or guaranteed
                            rawMin = Mathf.FloorToInt(expectedTotal);
                            rawMax = Mathf.CeilToInt(expectedTotal);
                            expectedText = rawMin == rawMax ? rawMin.ToString() : $"{rawMin}-{rawMax}";
                        }
                    }
                    else
                    {
                        // Variable quantity per drop (e.g., 10-30)
                        rawMin = Mathf.FloorToInt(expectedDrops * output.quantity_min);
                        rawMax = Mathf.FloorToInt(expectedDrops * output.quantity_max);
                        expectedText = rawMin == rawMax ? rawMin.ToString() : $"{rawMin}-{rawMax}";
                    }
                    
                    // Apply collect_cap (0 = unlimited)
                    int expectedMin = output.collect_cap > 0 ? Mathf.Min(rawMin, output.collect_cap) : rawMin;
                    int expectedMax = output.collect_cap > 0 ? Mathf.Min(rawMax, output.collect_cap) : rawMax;
                    bool isCapped = output.collect_cap > 0 && rawMax > output.collect_cap;
                    
                    // Update display text if capped
                    if (isCapped)
                    {
                        expectedText = expectedMin == expectedMax ? expectedMin.ToString() : $"{expectedMin}-{expectedMax}";
                    }
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Expected:", lootLabelStyle, GUILayout.Width(80));
                    
                    GUIStyle expectedStyle = new GUIStyle(EditorStyles.label);
                    expectedStyle.fontSize = 10;
                    expectedStyle.normal.textColor = isCapped ? new Color(1f, 0.6f, 0.3f) : new Color(0.4f, 1f, 0.6f);
                    expectedStyle.fontStyle = FontStyle.Bold;
                    
                    // Format: guaranteed items show as "→ X", probabilistic show as "→ ~X"
                    string prefix = isGuaranteed ? "→ " : "→ ~";
                    EditorGUILayout.LabelField($"{prefix}{expectedText}", expectedStyle);
                    
                    GUILayout.Space(10);
                    GUIStyle capStyle = new GUIStyle(EditorStyles.label);
                    capStyle.fontSize = 9;
                    capStyle.normal.textColor = isCapped ? new Color(1f, 0.5f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
                    capStyle.fontStyle = isCapped ? FontStyle.Bold : FontStyle.Normal;
                    string capIcon = isCapped ? "▲" : "◆";
                    string capText = output.collect_cap == 0 ? "∞" : output.collect_cap.ToString();
                    EditorGUILayout.LabelField($"{capIcon} CAP: {capText}", capStyle);
                    
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space(6);

            // === ACTION BUTTONS ===
            bool isCheckLoading = this.loadingCheckGenerators.Contains(generator.inventory_item_id);
            bool isCollectLoading = this.loadingCollectGenerators.Contains(generator.inventory_item_id);
            bool hasUnits = currentPending > 0;

            // Primary Actions Row
            EditorGUILayout.BeginHorizontal();
            
            // Check Button
            GUI.backgroundColor = isCheckLoading ? Color.gray : new Color(0.3f, 0.8f, 1f);
            EditorGUI.BeginDisabledGroup(isCheckLoading);
            if (GUILayout.Button(isCheckLoading ? "🔄 Checking..." : "🔄 Check", GUILayout.Height(32)))
            {
                this.CheckGenerator(generator.inventory_item_id);
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;
            
            // Collect Button (Green/Gray)
            GUI.backgroundColor = isCollectLoading ? Color.gray : (hasUnits ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.4f, 0.4f, 0.4f));
            EditorGUI.BeginDisabledGroup(isCollectLoading || !hasUnits);
            if (GUILayout.Button(isCollectLoading ? "📦 Collecting..." : $"📦 Collect ({currentPending})", GUILayout.Height(32)))
            {
                this.CollectGenerator(generator.inventory_item_id);
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Debug Actions Row (Compact)
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.6f, 0.7f);
            if (GUILayout.Button("📊 Pending", GUILayout.Height(20)))
            {
                this.itemGenerator.GetGeneratorExpectedOutput(generator.inventory_item_id);
            }
            
            if (GUILayout.Button("⏰ Time", GUILayout.Height(20)))
            {
                this.itemGenerator.GetGeneratorTimeUntilFull(generator.inventory_item_id);
            }
            
            if (GUILayout.Button("📝 Log", GUILayout.Height(20)))
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
                        Debug.Log($"[ItemGeneratorEditor] Generator checked: {inventoryItemId} | Tickets: {generatorData.ticket_count}/{generatorData.capacity}");

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
                    {
                        string itemInfo = !string.IsNullOrEmpty(collectResponse.output_item_code) 
                            ? collectResponse.output_item_code 
                            : "Unknown Item";
                        Debug.Log($"[ItemGeneratorEditor] Collected {collectResponse.units_collected} units of {itemInfo} | Output Item ID: {collectResponse.output_inventory_item_id}");
                    }

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

        private Color GetRarityColor(string rarity)
        {
            switch (rarity.ToLower())
            {
                case "common":
                    return new Color(0.7f, 0.7f, 0.7f); // Gray
                case "uncommon":
                    return new Color(0.3f, 1f, 0.3f); // Green
                case "rare":
                    return new Color(0.3f, 0.6f, 1f); // Blue
                case "epic":
                    return new Color(0.8f, 0.3f, 1f); // Purple
                case "legendary":
                    return new Color(1f, 0.6f, 0f); // Orange
                case "mythic":
                    return new Color(1f, 0.3f, 0.3f); // Red
                default:
                    return Color.white;
            }
        }

        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private string GetTimeElapsed(string checkpointAt)
        {
            if (string.IsNullOrEmpty(checkpointAt))
                return "";

            try
            {
                DateTime checkpointTime = DateTime.Parse(checkpointAt).ToUniversalTime();
                DateTime currentTime = DateTime.UtcNow;
                TimeSpan elapsed = currentTime - checkpointTime;

                if (elapsed.TotalSeconds < 0)
                    return "";

                if (elapsed.TotalHours >= 1)
                    return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
                else if (elapsed.TotalMinutes >= 1)
                    return $"{elapsed.Minutes}m {elapsed.Seconds}s";
                else
                    return $"{elapsed.Seconds}s";
            }
            catch
            {
                return "";
            }
        }

        private string FormatSeconds(int totalSeconds)
        {
            if (totalSeconds < 60)
                return $"{totalSeconds}s";

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m ({seconds}s)";
            else
                return $"{minutes}m {seconds}s";
        }
    }
}
