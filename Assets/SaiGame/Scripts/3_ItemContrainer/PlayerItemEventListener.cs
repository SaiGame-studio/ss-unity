using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to listen to ItemContainer events.
    /// </summary>
    public class PlayerItemEventListener : SaiBehaviour
    {
        private PlayerItem itemContainer => SaiService.Instance?.ItemContainer;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        private void OnEnable()
        {
            if (this.itemContainer != null)
            {
                this.itemContainer.OnGetItemsSuccess += this.HandleGetItemsSuccess;
                this.itemContainer.OnGetItemsFailure += this.HandleGetItemsFailure;
            }
        }

        private void OnDisable()
        {
            if (this.itemContainer != null)
            {
                this.itemContainer.OnGetItemsSuccess -= this.HandleGetItemsSuccess;
                this.itemContainer.OnGetItemsFailure -= this.HandleGetItemsFailure;
            }
        }

        private void HandleGetItemsSuccess(InventoryResponse inventory)
        {
            // Handle successful inventory retrieval
            // Example: Update UI, populate item grid, etc.
        }

        private void HandleGetItemsFailure(string error)
        {
            // Handle inventory retrieval failure
            // Example: Show error message or retry logic
        }
    }
}
