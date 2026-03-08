using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SaiGame.Services;

namespace SaiGame.UI
{
    // =====================================================================
    //  TopNavigatorUI — Manages the persistent top navigation bar.
    //
    //  • Reads the #TopNav, #NavTabs, #LogoutButton elements from AppShell.uxml.
    //  • Dynamically creates one tab button per registered NavEntry.
    //  • Listens to UIRouter.OnPanelChanged to sync the active-tab highlight.
    //  • Hides the nav bar on panels listed in hiddenOnPanels (default: "Login").
    // =====================================================================
    public class TopNavigatorUI : SaiBehaviour
    {
        // A navigation tab entry assigned in the Inspector.
        [Serializable]
        private class NavEntry
        {
            public string panelId;
            public string label;
        }

        [Header("Tabs")]
        [SerializeField] private NavEntry[] navEntries = new NavEntry[]
        {
            new NavEntry { panelId = "GamerProgress", label = "Progress" }
        };

        [Header("Hide nav on these panel ids")]
        [SerializeField] private string[] hiddenOnPanels = { "Login" };

        [Header("References")]
        [SerializeField] private SaiAuth saiAuth;

        private VisualElement topNav;
        private VisualElement navTabs;
        private Button logoutButton;

        // Maps panelId → tab button, for fast active-state updates.
        private readonly Dictionary<string, Button> tabMap = new Dictionary<string, Button>();

        private const string CLASS_TAB = "nav-tab";
        private const string CLASS_TAB_ACTIVE = "nav-tab--active";

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------
        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (this.saiAuth == null)
                this.saiAuth = this.GetComponentInParent<SaiAuth>();
        }

        protected override void Start()
        {
            base.Start();
            this.BindShellElements();
            this.BuildTabs();
            this.SubscribeToRouter();
        }

        // Bind elements that live in AppShell.uxml (not in a panel clone).
        private void BindShellElements()
        {
            UIDocument document = this.GetComponentInParent<UIDocument>();
            if (document == null)
            {
                Debug.LogError("[TopNavigatorUI] UIDocument not found in parent.", this);
                return;
            }

            VisualElement root = document.rootVisualElement;
            this.topNav      = root.Q("TopNav");
            this.navTabs     = root.Q("NavTabs");
            this.logoutButton = root.Q<Button>("LogoutButton");

            if (this.logoutButton != null)
                this.logoutButton.clicked += this.OnLogoutClicked;
        }

        // Dynamically create one Button per NavEntry inside #NavTabs.
        private void BuildTabs()
        {
            if (this.navTabs == null) return;

            foreach (NavEntry entry in this.navEntries)
            {
                Button tab = new Button();
                tab.text = entry.label;
                tab.AddToClassList(CLASS_TAB);

                // Capture loop variable for closure.
                string panelId = entry.panelId;
                tab.clicked += () => UIRouter.Instance?.ShowPanel(panelId);

                this.navTabs.Add(tab);
                this.tabMap[panelId] = tab;
            }
        }

        private void SubscribeToRouter()
        {
            if (UIRouter.Instance == null) return;
            UIRouter.Instance.OnPanelChanged += this.HandlePanelChanged;

            // Sync initial state.
            this.HandlePanelChanged(null, UIRouter.Instance.CurrentPanelId);
        }

        // ------------------------------------------------------------------
        //  Handle panel change — update visibility + active tab
        // ------------------------------------------------------------------
        private void HandlePanelChanged(string previousId, string newId)
        {
            this.UpdateNavVisibility(newId);
            this.UpdateActiveTab(newId);
        }

        private void UpdateNavVisibility(string panelId)
        {
            if (this.topNav == null) return;

            bool hidden = Array.IndexOf(this.hiddenOnPanels, panelId) >= 0;
            this.topNav.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdateActiveTab(string panelId)
        {
            foreach (KeyValuePair<string, Button> pair in this.tabMap)
            {
                if (pair.Key == panelId)
                    pair.Value.AddToClassList(CLASS_TAB_ACTIVE);
                else
                    pair.Value.RemoveFromClassList(CLASS_TAB_ACTIVE);
            }
        }

        // ------------------------------------------------------------------
        //  Logout
        // ------------------------------------------------------------------
        private void OnLogoutClicked()
        {
            this.saiAuth?.Logout();
            UIRouter.Instance?.ShowPanel("Login", addToHistory: false);
        }

        // ------------------------------------------------------------------
        //  Cleanup
        // ------------------------------------------------------------------
        protected virtual void OnDestroy()
        {
            if (UIRouter.Instance != null)
                UIRouter.Instance.OnPanelChanged -= this.HandlePanelChanged;

            if (this.logoutButton != null)
                this.logoutButton.clicked -= this.OnLogoutClicked;
        }
    }
}
