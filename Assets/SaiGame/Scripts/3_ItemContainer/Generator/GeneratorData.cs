using System;
using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents a single generator instance owned by the player.
    /// Generators produce resources over time based on their configuration.
    /// </summary>
    [Serializable]
    public class GeneratorData
    {
        public string definition_id;
        public string inventory_item_id;
        public string output_item_code;
        public int pending_units;
        public int capacity;
        public string checkpoint_at;
        public int production_interval_seconds;

        [Header("Local Calculation Setting")]
        public bool enableLocalCalculation = true;

        /// <summary>
        /// Calculates the current pending units based on elapsed time since checkpoint.
        /// Returns the pending_units from server if local calculation is disabled.
        /// </summary>
        public int GetCurrentPendingUnits()
        {
            if (!this.enableLocalCalculation)
                return this.pending_units;

            try
            {
                DateTime checkpointTime = DateTime.Parse(this.checkpoint_at).ToUniversalTime();
                DateTime currentTime = DateTime.UtcNow;
                double elapsedSeconds = (currentTime - checkpointTime).TotalSeconds;

                if (elapsedSeconds < 0)
                    return this.pending_units;

                int newUnits = (int)(elapsedSeconds / this.production_interval_seconds);
                int calculatedPending = this.pending_units + newUnits;

                return Mathf.Min(calculatedPending, this.capacity);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GeneratorData] Failed to parse checkpoint_at time: {e.Message}");
                return this.pending_units;
            }
        }

        /// <summary>
        /// Calculates how many seconds until the generator reaches full capacity.
        /// Returns 0 if already at or above capacity.
        /// </summary>
        public double GetSecondsUntilFull()
        {
            int currentPending = this.GetCurrentPendingUnits();
            if (currentPending >= this.capacity)
                return 0;

            int unitsNeeded = this.capacity - currentPending;
            return unitsNeeded * this.production_interval_seconds;
        }

        /// <summary>
        /// Gets a formatted string for time until full (e.g., "2h 30m 15s").
        /// </summary>
        public string GetTimeUntilFullFormatted()
        {
            double seconds = this.GetSecondsUntilFull();
            if (seconds <= 0)
                return "Full";

            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            else if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
            else
                return $"{(int)timeSpan.TotalSeconds}s";
        }

        /// <summary>
        /// Returns true if the generator is at or above capacity.
        /// </summary>
        public bool IsAtCapacity()
        {
            return this.GetCurrentPendingUnits() >= this.capacity;
        }

        /// <summary>
        /// Syncs the checkpoint_at to the current time.
        /// Call this when receiving fresh data from server to ensure local calculations start from "now".
        /// </summary>
        public void SyncCheckpointToNow()
        {
            this.checkpoint_at = System.DateTime.UtcNow.ToString("o");
        }
    }
}
