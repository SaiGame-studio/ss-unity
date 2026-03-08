using UnityEngine;
using UnityEngine.UIElements;
using SaiGame.Services;

namespace SaiGame.UI
{
    // =====================================================================
    //  UIPanelBase — Base MonoBehaviour for every UI Toolkit panel.
    //
    //  Architecture:
    //    • ONE UIDocument lives on the AppShell GameObject (managed by UIRouter).
    //    • Each panel is a MonoBehaviour on a CHILD GameObject of the AppShell.
    //    • UIRouter calls Initialize(container) once on Start; it clones the
    //      panel's VisualTreeAsset into the shared container, then calls
    //      OnBindElements() so the subclass can wire up its own elements.
    //    • Navigation is done via:  UIRouter.Instance.ShowPanel("Login")
    //
    //  How to create a new panel:
    //    1. Folder:   Assets/SaiGame/UI/<Feature>/
    //    2. Files:    <Name>Panel.uxml   <Name>Panel.uss   <Name>PanelUI.cs
    //    3. Class:    extend UIPanelBase, set PanelId, override OnBindElements.
    //    4. Scene:    add a child GameObject under AppShell, attach <Name>PanelUI,
    //                 drag <Name>Panel.uxml into the "Panel Asset" field.
    // =====================================================================
    public abstract class UIPanelBase : SaiBehaviour
    {
        // Unique string key used by UIRouter to find this panel.
        public abstract string PanelId { get; }

        // The UXML asset for this panel — assigned in the Inspector.
        [Header("Panel")]
        [SerializeField] protected VisualTreeAsset panelAsset;

        // Override to change which Label displays feedback messages.
        protected virtual string FeedbackLabelName => "MessageLabel";

        // Root VisualElement of this panel's cloned tree (set by UIRouter).
        protected VisualElement Root { get; private set; }

        private Label feedbackLabel;
        private bool initialized;

        // ------------------------------------------------------------------
        //  Called ONCE by UIRouter — clones VTA into the shared container.
        // ------------------------------------------------------------------
        public void Initialize(VisualElement container)
        {
            if (this.initialized) return;
            this.initialized = true;

            if (this.panelAsset == null)
            {
                Debug.LogError($"[{this.GetType().Name}] panelAsset is not assigned!", this);
                return;
            }

            // Clone this panel's UXML tree into the shared container.
            TemplateContainer clone = this.panelAsset.CloneTree();
            clone.style.position = Position.Absolute;
            clone.style.left   = 0;
            clone.style.top    = 0;
            clone.style.right  = 0;
            clone.style.bottom = 0;
            container.Add(clone);

            this.Root = clone;
            this.feedbackLabel = this.Root.Q<Label>(this.FeedbackLabelName);

            // Start hidden; UIRouter.ShowPanel controls visibility.
            this.Hide();

            this.OnBindElements(this.Root);
        }

        // ------------------------------------------------------------------
        //  Abstract — subclass wires up its own elements here.
        // ------------------------------------------------------------------
        protected abstract void OnBindElements(VisualElement root);

        // ------------------------------------------------------------------
        //  Show / Hide  (USS display property, not GameObject active state)
        // ------------------------------------------------------------------
        public void Show()
        {
            if (this.Root == null) return;
            this.Root.style.display = DisplayStyle.Flex;
            this.OnShow();
        }

        public void Hide()
        {
            if (this.Root == null) return;
            this.Root.style.display = DisplayStyle.None;
            this.OnHide();
        }

        public bool IsVisible => this.Root != null &&
                                 this.Root.style.display == DisplayStyle.Flex;

        // Optional lifecycle hooks.
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        // ------------------------------------------------------------------
        //  Query shorthand
        // ------------------------------------------------------------------
        protected T Q<T>(string name) where T : VisualElement => this.Root.Q<T>(name);
        protected VisualElement Q(string name) => this.Root.Q(name);

        // ------------------------------------------------------------------
        //  Feedback message helpers
        // ------------------------------------------------------------------
        protected void ShowFeedback(string message, bool isError)
        {
            if (this.feedbackLabel == null) return;

            this.feedbackLabel.text = message;
            this.feedbackLabel.RemoveFromClassList("feedback--error");
            this.feedbackLabel.RemoveFromClassList("feedback--success");
            this.feedbackLabel.AddToClassList(isError ? "feedback--error" : "feedback--success");
        }

        protected void HideFeedback()
        {
            if (this.feedbackLabel == null) return;

            this.feedbackLabel.text = string.Empty;
            this.feedbackLabel.RemoveFromClassList("feedback--error");
            this.feedbackLabel.RemoveFromClassList("feedback--success");
        }
    }
}
