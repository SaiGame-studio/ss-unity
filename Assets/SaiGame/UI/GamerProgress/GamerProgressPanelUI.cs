using UnityEngine;
using UnityEngine.UIElements;
using SaiGame.Services;

namespace SaiGame.UI
{
    // GamerProgress panel — displays level, XP, gold and progress actions.
    // All data operations are delegated to the GamerProgress service.
    public class GamerProgressPanelUI : UIPanelBase
    {
        public override string PanelId => "GamerProgress";

        [Header("References")]
        [SerializeField] private GamerProgress gamerProgress;

        // Stat display elements
        private Label levelValue;
        private Label expValue;
        private Label goldValue;
        private VisualElement xpFill;

        // Action buttons
        private Button refreshButton;
        private Button createButton;
        private Button deleteButton;

        // XP required to reach the next level (simple formula: level * 1000).
        private const int XP_PER_LEVEL = 1000;

        // ------------------------------------------------------------------
        //  Component discovery
        // ------------------------------------------------------------------
        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (this.gamerProgress == null)
                this.gamerProgress = this.GetComponentInParent<GamerProgress>();
        }

        // ------------------------------------------------------------------
        //  Bind UI elements (called once by UIPanelBase after UXML is cloned)
        // ------------------------------------------------------------------
        protected override void OnBindElements(VisualElement root)
        {
            this.levelValue   = this.Q<Label>("LevelValue");
            this.expValue     = this.Q<Label>("ExpValue");
            this.goldValue    = this.Q<Label>("GoldValue");
            this.xpFill       = this.Q("XpFill");
            this.refreshButton = this.Q<Button>("RefreshButton");
            this.createButton  = this.Q<Button>("CreateProgressButton");
            this.deleteButton  = this.Q<Button>("DeleteProgressButton");

            this.refreshButton.clicked += this.OnRefreshClicked;
            this.createButton.clicked  += this.OnCreateClicked;
            this.deleteButton.clicked  += this.OnDeleteClicked;

            this.SubscribeToServiceEvents();
        }

        // ------------------------------------------------------------------
        //  Panel lifecycle
        // ------------------------------------------------------------------
        protected override void OnShow()
        {
            this.HideFeedback();
            this.RefreshDisplay();
        }

        // ------------------------------------------------------------------
        //  Service event subscriptions
        // ------------------------------------------------------------------
        private void SubscribeToServiceEvents()
        {
            if (this.gamerProgress == null) return;
            this.gamerProgress.OnGetProgressSuccess    += this.HandleProgressLoaded;
            this.gamerProgress.OnGetProgressFailure    += this.HandleProgressError;
            this.gamerProgress.OnCreateProgressSuccess += this.HandleProgressLoaded;
            this.gamerProgress.OnCreateProgressFailure += this.HandleProgressError;
            this.gamerProgress.OnDeleteProgressSuccess += this.HandleDeleteSuccess;
            this.gamerProgress.OnDeleteProgressFailure += this.HandleProgressError;
        }

        private void UnsubscribeFromServiceEvents()
        {
            if (this.gamerProgress == null) return;
            this.gamerProgress.OnGetProgressSuccess    -= this.HandleProgressLoaded;
            this.gamerProgress.OnGetProgressFailure    -= this.HandleProgressError;
            this.gamerProgress.OnCreateProgressSuccess -= this.HandleProgressLoaded;
            this.gamerProgress.OnCreateProgressFailure -= this.HandleProgressError;
            this.gamerProgress.OnDeleteProgressSuccess -= this.HandleDeleteSuccess;
            this.gamerProgress.OnDeleteProgressFailure -= this.HandleProgressError;
        }

        // ------------------------------------------------------------------
        //  Button handlers
        // ------------------------------------------------------------------
        private void OnRefreshClicked()
        {
            this.HideFeedback();
            this.SetButtonsEnabled(false);
            this.gamerProgress?.GetProgress(
                _ => this.SetButtonsEnabled(true),
                _ => this.SetButtonsEnabled(true));
        }

        private void OnCreateClicked()
        {
            this.HideFeedback();
            this.SetButtonsEnabled(false);
            this.gamerProgress?.CreateProgress(
                _ => this.SetButtonsEnabled(true),
                _ => this.SetButtonsEnabled(true));
        }

        private void OnDeleteClicked()
        {
            this.HideFeedback();
            this.SetButtonsEnabled(false);
            this.gamerProgress?.ClearProgress();
        }

        // ------------------------------------------------------------------
        //  Service event handlers
        // ------------------------------------------------------------------
        private void HandleProgressLoaded(GamerProgressData data)
        {
            this.RefreshDisplay();
            this.ShowFeedback("Progress loaded.", isError: false);
        }

        private void HandleDeleteSuccess()
        {
            this.SetButtonsEnabled(true);
            this.ClearDisplay();
            this.ShowFeedback("Progress deleted.", isError: false);
        }

        private void HandleProgressError(string error)
        {
            this.SetButtonsEnabled(true);
            this.ShowFeedback(error, isError: true);
        }

        // ------------------------------------------------------------------
        //  Display helpers
        // ------------------------------------------------------------------
        private void RefreshDisplay()
        {
            GamerProgressData data = this.gamerProgress?.CurrentProgress;
            if (data == null || string.IsNullOrEmpty(data.id))
            {
                this.ClearDisplay();
                return;
            }

            this.levelValue.text = data.level.ToString();
            this.goldValue.text  = data.gold.ToString("N0");
            this.expValue.text   = $"{data.experience:N0} XP";

            // XP progress bar: width % based on XP within current level.
            int xpInLevel   = data.experience % XP_PER_LEVEL;
            float fillRatio = Mathf.Clamp01((float)xpInLevel / XP_PER_LEVEL);
            this.xpFill.style.width = new StyleLength(new Length(fillRatio * 100f, LengthUnit.Percent));
        }

        private void ClearDisplay()
        {
            const string EMPTY = "—";
            this.levelValue.text = EMPTY;
            this.expValue.text   = EMPTY;
            this.goldValue.text  = EMPTY;
            this.xpFill.style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
        }

        private void SetButtonsEnabled(bool enabled)
        {
            this.refreshButton.SetEnabled(enabled);
            this.createButton.SetEnabled(enabled);
            this.deleteButton.SetEnabled(enabled);
        }

        // ------------------------------------------------------------------
        //  Cleanup
        // ------------------------------------------------------------------
        protected virtual void OnDestroy()
        {
            this.UnsubscribeFromServiceEvents();

            if (this.refreshButton != null) this.refreshButton.clicked -= this.OnRefreshClicked;
            if (this.createButton  != null) this.createButton.clicked  -= this.OnCreateClicked;
            if (this.deleteButton  != null) this.deleteButton.clicked  -= this.OnDeleteClicked;
        }
    }
}
