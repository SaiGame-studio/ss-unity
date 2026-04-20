using UnityEngine;

namespace SaiGame.Services
{
    /// <summary>
    /// Shared icon / color / hex mapping for quest status strings.
    /// Keeps visual language consistent across Chain, Daily, Progress, and History editors.
    /// </summary>
    public static class QuestStatusIcons
    {
        public static string GetIcon(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed":   return "✓";
                case "claimed":     return "✓✓";
                case "in_progress":
                case "active":      return "▶";
                case "not_started":
                case "available":   return "○";
                case "locked":      return "🔒";
                default:            return "•";
            }
        }

        public static Color GetColor(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed":   return new Color(0f, 1f, 0.53f);
                case "claimed":     return new Color(0f, 1f, 0.53f);
                case "in_progress":
                case "active":      return new Color(0.4f, 0.8f, 1f);
                case "not_started":
                case "available":   return new Color(0.55f, 0.6f, 0.7f);
                case "locked":      return new Color(0.5f, 0.5f, 0.5f);
                default:            return new Color(0.67f, 0.67f, 0.67f);
            }
        }

        public static string GetHex(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed":   return "#00FF88";
                case "claimed":     return "#00FF88";
                case "in_progress":
                case "active":      return "#66CCFF";
                case "not_started":
                case "available":   return "#8C99B3";
                case "locked":      return "#808080";
                default:            return "#AAAAAA";
            }
        }
    }
}
