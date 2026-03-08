using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Example class showing how to listen to ItemGenerator events.
    /// Attach this to a GameObject to receive generator-related events.
    /// </summary>
    public class ItemGeneratorEventListener : SaiBehaviour
    {
        private ItemGenerator itemGenerator => SaiService.Instance?.ItemGenerator;

        protected override void LoadComponents()
        {
            base.LoadComponents();
        }

        private void OnEnable()
        {
            if (this.itemGenerator != null)
            {
                this.itemGenerator.OnGetGeneratorsSuccess += this.HandleGetGeneratorsSuccess;
                this.itemGenerator.OnGetGeneratorsFailure += this.HandleGetGeneratorsFailure;
                this.itemGenerator.OnCheckGeneratorSuccess += this.HandleCheckGeneratorSuccess;
                this.itemGenerator.OnCheckGeneratorFailure += this.HandleCheckGeneratorFailure;
                this.itemGenerator.OnCollectGeneratorSuccess += this.HandleCollectGeneratorSuccess;
                this.itemGenerator.OnCollectGeneratorFailure += this.HandleCollectGeneratorFailure;
            }
        }

        private void OnDisable()
        {
            if (this.itemGenerator != null)
            {
                this.itemGenerator.OnGetGeneratorsSuccess -= this.HandleGetGeneratorsSuccess;
                this.itemGenerator.OnGetGeneratorsFailure -= this.HandleGetGeneratorsFailure;
                this.itemGenerator.OnCheckGeneratorSuccess -= this.HandleCheckGeneratorSuccess;
                this.itemGenerator.OnCheckGeneratorFailure -= this.HandleCheckGeneratorFailure;
                this.itemGenerator.OnCollectGeneratorSuccess -= this.HandleCollectGeneratorSuccess;
                this.itemGenerator.OnCollectGeneratorFailure -= this.HandleCollectGeneratorFailure;
            }
        }

        private void HandleGetGeneratorsSuccess(GeneratorsResponse response)
        {
            // Handle successful generators retrieval
            // Example: Update UI, populate generator list, etc.
            Debug.Log($"[ItemGeneratorEventListener] Received {response.generators.Length} generators");
        }

        private void HandleGetGeneratorsFailure(string error)
        {
            // Handle generators retrieval failure
            // Example: Show error message or retry logic
            Debug.LogError($"[ItemGeneratorEventListener] Failed to get generators: {error}");
        }

        private void HandleCheckGeneratorSuccess(GeneratorData generatorData)
        {
            // Handle successful generator check
            // Example: Update UI with latest generator state
            Debug.Log($"[ItemGeneratorEventListener] Generator checked: {generatorData.inventory_item_id} | Tickets: {generatorData.ticket_count}/{generatorData.capacity}");
        }

        private void HandleCheckGeneratorFailure(string error)
        {
            // Handle generator check failure
            Debug.LogError($"[ItemGeneratorEventListener] Failed to check generator: {error}");
        }

        private void HandleCollectGeneratorSuccess(GeneratorCollectResponse response)
        {
            // Handle successful collection
            // Example: Update UI, show rewards animation, play sound effect
            Debug.Log($"[ItemGeneratorEventListener] Collected {response.units_collected} units of {response.output_item_code}");
        }

        private void HandleCollectGeneratorFailure(string error)
        {
            // Handle collection failure
            Debug.LogError($"[ItemGeneratorEventListener] Failed to collect from generator: {error}");
        }
    }
}
