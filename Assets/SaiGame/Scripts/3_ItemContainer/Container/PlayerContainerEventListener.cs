using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to listen to PlayerContainer events.
    /// </summary>
    public class PlayerContainerEventListener : SaiBehaviour
    {
        private PlayerContainer playerContainer => SaiService.Instance?.PlayerContainer;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        private void OnEnable()
        {
            if (this.playerContainer != null)
            {
                this.playerContainer.OnGetContainersSuccess += this.HandleGetContainersSuccess;
                this.playerContainer.OnGetContainersFailure += this.HandleGetContainersFailure;
            }
        }

        private void OnDisable()
        {
            if (this.playerContainer != null)
            {
                this.playerContainer.OnGetContainersSuccess -= this.HandleGetContainersSuccess;
                this.playerContainer.OnGetContainersFailure -= this.HandleGetContainersFailure;
            }
        }

        private void HandleGetContainersSuccess(ContainerResponse response)
        {
            // Handle successful container retrieval
            // Example: Update UI, populate container grid, etc.
        }

        private void HandleGetContainersFailure(string error)
        {
            // Handle container retrieval failure
            // Example: Show error message or retry logic
        }
    }
}
