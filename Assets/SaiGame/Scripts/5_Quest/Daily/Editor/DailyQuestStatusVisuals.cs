using UnityEngine;

namespace SaiGame.Services
{
    public static class DailyQuestStatusVisuals
    {
        public static string GetIcon(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed": return "✓";
                case "claimed": return "🎁";
                case "in_progress":
                case "active": return "↻";
                case "cancelled": return "⊘";
                case "expired": return "⌛";
                case "failed": return "⚠";
                case "not_started":
                case "available": return "◌";
                case "locked": return "🔒";
                default: return "?";
            }
        }

        public static Color GetColor(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed": return new Color32(96, 165, 250, 255);
                case "claimed": return new Color32(34, 197, 94, 255);
                case "in_progress":
                case "active": return new Color32(245, 158, 11, 255);
                case "cancelled":
                case "expired":
                case "failed": return new Color32(248, 113, 113, 255);
                default: return new Color32(161, 161, 170, 255);
            }
        }

        public static string GetHex(string status)
        {
            switch ((status ?? "").ToLower())
            {
                case "completed": return "#60A5FA";
                case "claimed": return "#22C55E";
                case "in_progress":
                case "active": return "#F59E0B";
                case "cancelled":
                case "expired":
                case "failed": return "#F87171";
                default: return "#A1A1AA";
            }
        }
    }
}
