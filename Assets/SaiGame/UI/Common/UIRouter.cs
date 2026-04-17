using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SaiGame.Services;

namespace SaiGame.UI
{
    // =====================================================================
    //  UIRouter — Single UIDocument manager and panel navigation controller.
    //
    //  Scene setup (do once):
    //    1. Create a GameObject "AppShell".
    //    2. Add UIDocument → assign AppShell.uxml as Source Asset.
    //    3. Add UIRouter component.
    //    4. For each feature panel, create a CHILD GameObject under AppShell,
    //       attach its <Name>PanelUI script, drag its .uxml into "Panel Asset".
    //    5. Set "Default Panel Id" to the panel shown on startup (e.g. "Login").
    //
    //  Navigation from anywhere:
    //      UIRouter.Instance.ShowPanel("GamerProgress");
    //      UIRouter.Instance.ShowPanel<GamerProgressPanelUI>();
    //      UIRouter.Instance.Back();
    // =====================================================================
    [RequireComponent(typeof(UIDocument))]
    public class UIRouter : SaiSingleton<UIRouter>
    {
        [Header("Navigation")]
        [SerializeField] private string defaultPanelId = "Login";

        // Fired whenever the active panel changes (previousId, newId).
        public event Action<string, string> OnPanelChanged;

        // Runtime state
        private UIDocument document;
        private VisualElement container;

        // All registered panels, keyed by PanelId.
        private readonly Dictionary<string, UIPanelBase> panels = new Dictionary<string, UIPanelBase>();

        // Navigation history for Back().
        private readonly Stack<string> history = new Stack<string>();

        private string currentPanelId;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------
        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.document = this.GetComponent<UIDocument>();
        }

        protected override void Start()
        {
            base.Start();
            this.container = this.document.rootVisualElement.Q("PanelContainer");

            if (this.container == null)
            {
                Debug.LogError("[UIRouter] #PanelContainer not found in AppShell.uxml!", this);
                return;
            }

            this.RegisterAllChildPanels();
            this.ShowPanel(this.defaultPanelId, addToHistory: false);
        }

        // ------------------------------------------------------------------
        //  Auto-discover all UIPanelBase children and initialise them.
        // ------------------------------------------------------------------
        private void RegisterAllChildPanels()
        {
            UIPanelBase[] childPanels = this.GetComponentsInChildren<UIPanelBase>(includeInactive: true);

            foreach (UIPanelBase panel in childPanels)
            {
                if (this.panels.ContainsKey(panel.PanelId))
                {
                    Debug.LogWarning($"[UIRouter] Duplicate panel id '{panel.PanelId}' — skipping.", panel);
                    continue;
                }

                panel.Initialize(this.container);
                this.panels[panel.PanelId] = panel;

                if (SaiServer.Instance != null && SaiServer.Instance.ShowDebug)
                    Debug.Log($"[UIRouter] Registered panel: {panel.PanelId}");
            }
        }

        // ------------------------------------------------------------------
        //  Navigation — by id
        // ------------------------------------------------------------------
        public void ShowPanel(string panelId, bool addToHistory = true)
        {
            if (!this.panels.TryGetValue(panelId, out UIPanelBase next))
            {
                Debug.LogWarning($"[UIRouter] Panel '{panelId}' not found.", this);
                return;
            }

            if (this.currentPanelId == panelId) return;

            // Hide current panel.
            string previousId = this.currentPanelId;
            if (!string.IsNullOrEmpty(previousId) &&
                this.panels.TryGetValue(previousId, out UIPanelBase current))
            {
                current.Hide();
            }

            // Track history.
            if (addToHistory && !string.IsNullOrEmpty(previousId))
                this.history.Push(previousId);

            this.currentPanelId = panelId;
            next.Show();

            this.OnPanelChanged?.Invoke(previousId, panelId);
        }

        // ------------------------------------------------------------------
        //  Navigation — by type (generic convenience)
        // ------------------------------------------------------------------
        public void ShowPanel<T>() where T : UIPanelBase
        {
            foreach (KeyValuePair<string, UIPanelBase> pair in this.panels)
            {
                if (pair.Value is T)
                {
                    this.ShowPanel(pair.Key);
                    return;
                }
            }

            Debug.LogWarning($"[UIRouter] Panel of type '{typeof(T).Name}' not registered.", this);
        }

        // ------------------------------------------------------------------
        //  Navigate back
        // ------------------------------------------------------------------
        public void Back()
        {
            if (this.history.Count == 0) return;
            string previous = this.history.Pop();
            this.ShowPanel(previous, addToHistory: false);
        }

        public bool CanGoBack => this.history.Count > 0;

        // ------------------------------------------------------------------
        //  Read-only accessors
        // ------------------------------------------------------------------
        public string CurrentPanelId => this.currentPanelId;

        public T GetPanel<T>() where T : UIPanelBase
        {
            foreach (UIPanelBase panel in this.panels.Values)
            {
                if (panel is T typed) return typed;
            }
            return null;
        }
    }
}
