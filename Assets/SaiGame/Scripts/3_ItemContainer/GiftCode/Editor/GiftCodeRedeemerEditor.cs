using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(GiftCodeRedeemer))]
    [CanEditMultipleObjects]
    public class GiftCodeRedeemerEditor : Editor
    {
        private GiftCodeRedeemer redeemer;
        private SerializedProperty defaultGiftCode;

        private bool showLastResponse = true;
        private bool isRedeeming = false;

        private void OnEnable()
        {
            this.redeemer = (GiftCodeRedeemer)target;
            this.defaultGiftCode = serializedObject.FindProperty("defaultGiftCode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gift Code Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Settings
            EditorGUILayout.PropertyField(this.defaultGiftCode, new GUIContent("Default Gift Code", "The code string to redeem"));

            EditorGUILayout.Space();

            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Row 1: Redeem
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = this.isRedeeming ? Color.gray : new Color(1f, 0.85f, 0.1f);
            EditorGUI.BeginDisabledGroup(this.isRedeeming);
            if (GUILayout.Button(this.isRedeeming ? "Redeeming..." : "Redeem Gift Code", GUILayout.Height(30)))
            {
                this.DoRedeemGiftCode();
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Row 2: Clear
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Result", GUILayout.Height(24)))
            {
                this.redeemer.ClearLastResponse();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Last Response
            this.showLastResponse = EditorGUILayout.Foldout(this.showLastResponse, "Last Redeem Result", true);
            if (this.showLastResponse)
            {
                EditorGUI.indentLevel++;

                GiftCodeResponse response = this.redeemer.LastResponse;
                if (response != null)
                {
                    EditorGUILayout.LabelField("Total Items Granted", response.total_items_granted.ToString());
                    int resultsCount = response.results != null ? response.results.Length : 0;
                    EditorGUILayout.LabelField("Total Gacha Rolls", resultsCount.ToString());
                }
                else
                {
                    EditorGUILayout.HelpBox("No redeem result yet. Click Redeem to see results.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Subscribe to OnRedeemSuccess / OnRedeemFailure events from code.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DoRedeemGiftCode()
        {
            if (SaiServer.Instance == null)
            {
                Debug.LogError("[GiftCodeRedeemerEditor] SaiServer not found!");
                return;
            }

            if (!SaiServer.Instance.IsAuthenticated)
            {
                Debug.LogError("[GiftCodeRedeemerEditor] Not authenticated! Please login first.");
                return;
            }

            if (string.IsNullOrEmpty(this.defaultGiftCode.stringValue))
            {
                Debug.LogError("[GiftCodeRedeemerEditor] Gift Code is empty!");
                return;
            }

            this.isRedeeming = true;
            Repaint();

            this.redeemer.Redeem(
                onSuccess: response =>
                {
                    this.isRedeeming = false;
                    int resultsCount = response.results != null ? response.results.Length : 0;
                    Debug.Log($"[GiftCodeRedeemerEditor] Gift code redeemed! Results granted: {resultsCount}");
                    Repaint();
                },
                onError: error =>
                {
                    this.isRedeeming = false;
                    Debug.LogError($"[GiftCodeRedeemerEditor] Failed to redeem gift code: {error}");
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
