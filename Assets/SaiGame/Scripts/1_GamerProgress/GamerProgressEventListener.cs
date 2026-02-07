using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to listen to SaiGamerProgress events
    /// </summary>
    public class GamerProgressEventListener : SaiBehaviour
    {
        [SerializeField] private SaiGamerProgress gamerProgress;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadGamerProgress();
        }

        protected virtual void LoadGamerProgress()
        {
            if (this.gamerProgress != null) return;
            this.gamerProgress = GetComponent<SaiGamerProgress>();
        }

        private void OnEnable()
        {
            if (gamerProgress != null)
            {
                // Subscribe to gamer progress events
                gamerProgress.OnCreateProgressSuccess += HandleCreateProgressSuccess;
                gamerProgress.OnCreateProgressFailure += HandleCreateProgressFailure;
                gamerProgress.OnGetProgressSuccess += HandleGetProgressSuccess;
                gamerProgress.OnGetProgressFailure += HandleGetProgressFailure;
                gamerProgress.OnDeleteProgressSuccess += HandleDeleteProgressSuccess;
                gamerProgress.OnDeleteProgressFailure += HandleDeleteProgressFailure;
            }
        }

        private void OnDisable()
        {
            if (gamerProgress != null)
            {
                // Unsubscribe from gamer progress events
                gamerProgress.OnCreateProgressSuccess -= HandleCreateProgressSuccess;
                gamerProgress.OnCreateProgressFailure -= HandleCreateProgressFailure;
                gamerProgress.OnGetProgressSuccess -= HandleGetProgressSuccess;
                gamerProgress.OnGetProgressFailure -= HandleGetProgressFailure;
                gamerProgress.OnDeleteProgressSuccess -= HandleDeleteProgressSuccess;
                gamerProgress.OnDeleteProgressFailure -= HandleDeleteProgressFailure;
            }
        }

        private void HandleCreateProgressSuccess(GamerProgress progress)
        {
            // Handle successful progress creation
            // Example: Show success message, update UI, etc.
        }

        private void HandleCreateProgressFailure(string error)
        {
            // Handle progress creation failure
            // Example: Show error message to user
        }

        private void HandleGetProgressSuccess(GamerProgress progress)
        {
            // Handle successful progress retrieval
            // Example: Update UI with progress data
        }

        private void HandleGetProgressFailure(string error)
        {
            // Handle progress retrieval failure
            // Example: Show error message or retry logic
        }

        private void HandleDeleteProgressSuccess()
        {
            // Handle successful progress deletion
            // Example: Show success message, redirect to create screen
        }

        private void HandleDeleteProgressFailure(string error)
        {
            // Handle progress deletion failure
            // Example: Show error message to user
        }
    }
}