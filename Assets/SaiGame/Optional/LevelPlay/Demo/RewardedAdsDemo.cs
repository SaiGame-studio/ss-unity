using UnityEngine;
using UnityEngine.UI;

namespace SaiGame.LevelPlaySample
{
    public sealed class RewardedAdsDemo : MonoBehaviour
    {
        [SerializeField] private RewardedAds rewardedAds;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Button showButton;

        private int totalReward;

        private void OnEnable()
        {
            rewardedAds.AdClosed += HandleClosed;
            rewardedAds.RewardEarned += HandleReward;
        }

        private void OnDisable()
        {
            rewardedAds.AdClosed -= HandleClosed;
            rewardedAds.RewardEarned -= HandleReward;
        }

        private void Update()
        {
            statusLabel.text = rewardedAds.Status;
            showButton.interactable = rewardedAds.IsReady;
        }

        public void LoadAd()
        {
            rewardedAds.Load();
        }

        public void ShowAd()
        {
            resultLabel.text = "Waiting for LevelPlay to confirm a reward.";
            if (!rewardedAds.TryShow())
                resultLabel.text = "Ad unavailable.";
        }

        private void HandleClosed()
        {
            resultLabel.text += "\nAd closed.";
        }

        private void HandleReward(string rewardName, int amount)
        {
            totalReward += amount;
            resultLabel.text = $"Reward confirmed: {amount} {rewardName}.\nTotal demo rewards: {totalReward}";
        }
    }
}
