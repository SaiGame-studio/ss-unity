using UnityEngine;

namespace SaiGame.Services
{
    public class ItemTagEventListener : SaiBehaviour
    {
        private ItemTag itemTag => SaiServer.Instance?.ItemTag;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        private void OnEnable()
        {
            if (this.itemTag != null)
            {
                this.itemTag.OnGetTagsSuccess += this.HandleGetTagsSuccess;
                this.itemTag.OnGetTagsFailure += this.HandleGetTagsFailure;
            }
        }

        private void OnDisable()
        {
            if (this.itemTag != null)
            {
                this.itemTag.OnGetTagsSuccess -= this.HandleGetTagsSuccess;
                this.itemTag.OnGetTagsFailure -= this.HandleGetTagsFailure;
            }
        }

        protected virtual void HandleGetTagsSuccess(ItemTagsResponse tags) { }

        protected virtual void HandleGetTagsFailure(string error) { }
    }
}
