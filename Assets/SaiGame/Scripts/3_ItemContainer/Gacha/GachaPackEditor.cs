using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GachaPack))]
    [CanEditMultipleObjects]
    public class GachaPackEditor : Editor
    {
        private GachaPack gachaPack;
        private SerializedProperty gachaPackId;
        private SerializedProperty gachaPackCode;
        private SerializedProperty containerId;

        private bool showLastResponse = true;
        private bool showItemsGranted = false;
        private bool isOpening = false;
        private bool isOpeningByCode = false;

        private void OnEnable()
        {
            this.gachaPack = (GachaPack)target;
            this.gachaPackId = serializedObject.FindProperty("gachaPackId");
            this.gachaPackCode = serializedObject.FindProperty("gachaPackCode");
            this.containerId = serializedObject.FindProperty("containerId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gacha Pack Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Gacha Pack Settings
            EditorGUILayout.PropertyField(this.gachaPackId, new GUIContent("Gacha Pack ID", "The item_definition_id of the gacha pack"));
            EditorGUILayout.PropertyField(this.gachaPackCode, new GUIContent("Gacha Pack Code", "The code name of the gacha pack (e.g. premium_chest)"));
            EditorGUILayout.PropertyField(this.containerId, new GUIContent("Container ID", "The container_id where the pack resides"));

            EditorGUILayout.Space();

            // Utility Buttons
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Row 1: Open by ID / Open by Code
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = this.isOpening ? Color.gray : new Color(1f, 0.85f, 0.1f);
            EditorGUI.BeginDisabledGroup(this.isOpening);
            if (GUILayout.Button(this.isOpening ? "Opening..." : "Open by ID", GUILayout.Height(30)))
            {
                this.DoOpenGachaPack();
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = this.isOpeningByCode ? Color.gray : new Color(0.4f, 0.8f, 1.0f);
            EditorGUI.BeginDisabledGroup(this.isOpeningByCode);
            if (GUILayout.Button(this.isOpeningByCode ? "Opening..." : "Open by Code", GUILayout.Height(30)))
            {
                this.DoOpenGachaPackByCode();
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Row 2: Clear
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Result", GUILayout.Height(24)))
            {
                this.gachaPack.ClearLastResponse();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Last Response
            this.showLastResponse = EditorGUILayout.Foldout(this.showLastResponse, "Last Gacha Result", true);
            if (this.showLastResponse)
            {
                EditorGUI.indentLevel++;

                GachaResponse response = this.gachaPack.LastResponse;
                if (response != null)
                {
                    EditorGUILayout.LabelField("Is Duplicate", response.is_duplicate.ToString());
                    DrawIdField("Transaction ID", response.transaction_id);
                    DrawIdField("Mailbox Message ID", response.mailbox_message_id);

                    if (response.items_granted != null && response.items_granted.Length > 0)
                    {
                        this.showItemsGranted = EditorGUILayout.Foldout(this.showItemsGranted, $"Items Granted ({response.items_granted.Length})", true);
                        if (this.showItemsGranted)
                        {
                            EditorGUI.indentLevel++;
                            foreach (GachaItemGranted item in response.items_granted)
                            {
                                this.DrawGrantedItem(item);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Items Granted", "0");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No gacha result yet. Open a gacha pack to see results.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Subscribe to OnOpenGachaSuccess / OnOpenGachaFailure events from code.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGrantedItem(GachaItemGranted item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Name: {item.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Category: {item.category}  |  Quantity: {item.quantity} ({item.quantity_min}-{item.quantity_max})");
            DrawIdField("Definition ID", item.item_definition_id);
            DrawIdField("Inventory Item ID", item.inventory_item_id);

            EditorGUILayout.EndVertical();
        }

        private void DoOpenGachaPack()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[GachaPackEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[GachaPackEditor] Not authenticated! Please login first.");
                return;
            }

            if (string.IsNullOrEmpty(this.gachaPackId.stringValue))
            {
                Debug.LogError("[GachaPackEditor] Gacha Pack ID is empty!");
                return;
            }

            if (string.IsNullOrEmpty(this.containerId.stringValue))
            {
                Debug.LogError("[GachaPackEditor] Container ID is empty!");
                return;
            }

            this.isOpening = true;
            Repaint();

            this.gachaPack.OpenGachaPack(
                onSuccess: response =>
                {
                    this.isOpening = false;
                    int count = response.items_granted?.Length ?? 0;
                    Debug.Log($"[GachaPackEditor] Gacha opened! {count} item(s) granted. Transaction: {response.transaction_id}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isOpening = false;
                    Debug.LogError($"[GachaPackEditor] Failed to open gacha pack: {error}");
                    Repaint();
                }
            );
        }

        private void DoOpenGachaPackByCode()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[GachaPackEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[GachaPackEditor] Not authenticated! Please login first.");
                return;
            }

            if (string.IsNullOrEmpty(this.gachaPackCode.stringValue))
            {
                Debug.LogError("[GachaPackEditor] Gacha Pack Code is empty!");
                return;
            }

            if (string.IsNullOrEmpty(this.containerId.stringValue))
            {
                Debug.LogError("[GachaPackEditor] Container ID is empty!");
                return;
            }

            this.isOpeningByCode = true;
            Repaint();

            this.gachaPack.OpenGachaPackByCode(
                onSuccess: response =>
                {
                    this.isOpeningByCode = false;
                    int count = response.items_granted?.Length ?? 0;
                    Debug.Log($"[GachaPackEditor] Gacha by code opened! {count} item(s) granted. Transaction: {response.transaction_id}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isOpeningByCode = false;
                    Debug.LogError($"[GachaPackEditor] Failed to open gacha by code: {error}");
                    Repaint();
                }
            );
        }

        private static void DrawIdField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, value);
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
                GUIUtility.systemCopyBuffer = value ?? "";
            EditorGUILayout.EndHorizontal();
        }
    }
}
