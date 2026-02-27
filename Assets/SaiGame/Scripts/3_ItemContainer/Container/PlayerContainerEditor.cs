using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(PlayerContainer))]
    [CanEditMultipleObjects]
    public class PlayerContainerEditor : Editor
    {
        private PlayerContainer playerContainer;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty containerLimit;
        private SerializedProperty containerOffset;

        private bool showCurrentContainers = true;
        private bool showContainerList = true;
        private bool showUtilityButtons = true;

        private void OnEnable()
        {
            this.playerContainer = (PlayerContainer)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.containerLimit = serializedObject.FindProperty("containerLimit");
            this.containerOffset = serializedObject.FindProperty("containerOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Container Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto Load Settings
            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load containers when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Query Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.containerLimit, new GUIContent("Container Limit", "Number of containers to load per request"));
            EditorGUILayout.PropertyField(this.containerOffset, new GUIContent("Container Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current Container Data
            this.showCurrentContainers = EditorGUILayout.Foldout(this.showCurrentContainers, "Current Container Data", true);
            if (this.showCurrentContainers)
            {
                EditorGUI.indentLevel++;

                if (this.playerContainer.CurrentContainers != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Loaded Containers: {this.playerContainer.CurrentContainers.containers?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Has More: {this.playerContainer.CurrentContainers.has_more}");
                    EditorGUILayout.LabelField($"Limit: {this.playerContainer.CurrentContainers.limit}  |  Offset: {this.playerContainer.CurrentContainers.offset}");

                    if (this.playerContainer.CurrentContainers.containers != null
                        && this.playerContainer.CurrentContainers.containers.Length > 0)
                    {
                        this.showContainerList = EditorGUILayout.Foldout(this.showContainerList, "Container List", true);
                        if (this.showContainerList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (ContainerData container in this.playerContainer.CurrentContainers.containers)
                            {
                                this.DrawContainerSummary(container);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No container data loaded yet.", MessageType.None);
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
                if (GUILayout.Button("Get Containers", GUILayout.Height(30)))
                {
                    this.LoadContainers();
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Containers", GUILayout.Height(30)))
                {
                    this.playerContainer.ClearContainers();
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

        private void DrawContainerSummary(ContainerData container)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"ID: {container.id}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {container.container_type}");

            if (container.definition != null)
            {
                EditorGUILayout.LabelField($"Name: {container.definition.name}");
                EditorGUILayout.LabelField($"Grid: {container.definition.grid_cols}×{container.definition.grid_rows}  |  Portable: {container.definition.is_portable}");
            }

            EditorGUILayout.LabelField($"Created: {container.created_at}");

            EditorGUILayout.EndVertical();
        }

        private void LoadContainers()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[PlayerContainerEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[PlayerContainerEditor] Not authenticated! Please login first.");
                return;
            }

            this.playerContainer.GetContainers(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[PlayerContainerEditor] Loaded {response.containers.Length} containers");
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[PlayerContainerEditor] Failed to load containers: {error}");
                }
            );
        }
    }
}
