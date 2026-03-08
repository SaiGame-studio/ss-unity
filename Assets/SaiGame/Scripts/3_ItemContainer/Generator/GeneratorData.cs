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
        public GeneratorDefinition definition;
        public int ticket_count;
        public int tick_capacity;
        public bool is_full;
        public int next_tick_in_seconds;
        public string checkpoint_at;
        public int production_interval_seconds;

        [Header("Local Calculation Setting")]
        public bool enableLocalCalculation = true;

        // Property for backward compatibility and easy access
        public int capacity => this.tick_capacity;

        /// <summary>
        /// Calculates the current pending units based on elapsed time since last sync.
        /// Server's ticket_count = CURRENT real count at the moment of API call.
        /// After receiving server data, checkpoint_at is synced to NOW.
        /// First new tick happens after next_tick_in_seconds, subsequent ticks every production_interval_seconds.
        /// </summary>
        public int GetCurrentPendingUnits()
        {
            if (!this.enableLocalCalculation)
                return this.ticket_count;

            try
            {
                DateTime checkpointTime = DateTime.Parse(this.checkpoint_at).ToUniversalTime();
                DateTime currentTime = DateTime.UtcNow;
                double elapsedSeconds = (currentTime - checkpointTime).TotalSeconds;

                if (elapsedSeconds < 0)
                    return this.ticket_count;

                // First tick happens after next_tick_in_seconds, not after a full interval
                if (elapsedSeconds < this.next_tick_in_seconds)
                    return this.ticket_count;

                // First tick completed, count additional ticks after that
                double elapsedAfterFirstTick = elapsedSeconds - this.next_tick_in_seconds;
                int newTicks = 1 + (int)(elapsedAfterFirstTick / this.production_interval_seconds);
                int totalPending = this.ticket_count + newTicks;

                return Mathf.Min(totalPending, this.capacity);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GeneratorData] Failed to parse checkpoint_at time: {e.Message}");
                return this.ticket_count;
            }
        }

        /// <summary>
        /// Calculates the total time (in seconds) to reach full capacity from 0 ticks.
        /// Formula: tick_capacity × production_interval_seconds
        /// </summary>
        public double GetTotalSecondsToFull()
        {
            return this.capacity * this.production_interval_seconds;
        }

        /// <summary>
        /// Gets a formatted string for total time to full capacity (e.g., "24m", "2h 30m").
        /// </summary>
        public string GetTotalTimeToFullFormatted()
        {
            double seconds = this.GetTotalSecondsToFull();
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            else if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes}m";
            else
                return $"{(int)timeSpan.TotalSeconds}s";
        }

        /// <summary>
        /// Calculates how many seconds until the generator reaches full capacity.
        /// Accounts for the partial tick in progress (interval countdown).
        /// Returns 0 if already at or above capacity.
        /// </summary>
        public double GetSecondsUntilFull()
        {
            int currentPending = this.GetCurrentPendingUnits();
            if (currentPending >= this.capacity)
                return 0;

            int unitsNeeded = this.capacity - currentPending;
            
            // Time for remaining full ticks after the current in-progress tick completes
            int dynamicNextTick = this.GetDynamicNextTickSeconds();
            
            // First tick completes in dynamicNextTick seconds, remaining (unitsNeeded - 1) ticks take full intervals
            if (unitsNeeded <= 1)
                return dynamicNextTick;
            
            return dynamicNextTick + ((unitsNeeded - 1) * this.production_interval_seconds);
        }

        /// <summary>
        /// Gets a formatted string for time until full with seconds precision (e.g., "23m 47s").
        /// </summary>
        public string GetTimeUntilFullFormatted()
        {
            double seconds = this.GetSecondsUntilFull();
            if (seconds <= 0)
                return "Full";

            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
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
        /// Calculates the dynamic countdown to the next tick in seconds.
        /// Uses server's next_tick_in_seconds as starting point, then counts down based on elapsed time since sync.
        /// </summary>
        public int GetDynamicNextTickSeconds()
        {
            if (!this.enableLocalCalculation)
                return this.next_tick_in_seconds;

            if (this.IsAtCapacity())
                return 0;

            try
            {
                DateTime checkpointTime = DateTime.Parse(this.checkpoint_at).ToUniversalTime();
                DateTime currentTime = DateTime.UtcNow;
                double elapsedSeconds = (currentTime - checkpointTime).TotalSeconds;

                if (elapsedSeconds < 0)
                    return this.next_tick_in_seconds;

                // next_tick_in_seconds was the countdown at the moment of sync (checkpoint_at)
                // Subtract elapsed time, then loop within production_interval_seconds
                double remaining = this.next_tick_in_seconds - elapsedSeconds;
                
                if (remaining > 0)
                    return (int)remaining;
                
                // Past the first tick, calculate cyclically
                double pastFirstTick = elapsedSeconds - this.next_tick_in_seconds;
                double remainder = pastFirstTick % this.production_interval_seconds;
                int secondsUntilNextTick = (int)(this.production_interval_seconds - remainder);

                return Mathf.Clamp(secondsUntilNextTick, 0, this.production_interval_seconds);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GeneratorData] Failed to calculate next tick: {e.Message}");
                return this.next_tick_in_seconds;
            }
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
