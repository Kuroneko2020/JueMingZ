using System;

namespace JueMingZ.Config
{
    public static class CombatAimModes
    {
        public const string RangeOriginCursor = "Cursor";
        public const string RangeOriginPlayer = "Player";
        public const string TargetPriorityClearLine = "ClearLine";
        public const string TargetPriorityNearest = "Nearest";

        public static string NormalizeRangeOrigin(string value)
        {
            return string.Equals(value, RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)
                ? RangeOriginPlayer
                : RangeOriginCursor;
        }

        public static string NormalizeTargetPriority(string value)
        {
            return string.Equals(value, TargetPriorityNearest, StringComparison.OrdinalIgnoreCase)
                ? TargetPriorityNearest
                : TargetPriorityClearLine;
        }

        public static string ToggleRangeOrigin(string value)
        {
            return string.Equals(NormalizeRangeOrigin(value), RangeOriginCursor, StringComparison.OrdinalIgnoreCase)
                ? RangeOriginPlayer
                : RangeOriginCursor;
        }

        public static string ToggleTargetPriority(string value)
        {
            return string.Equals(NormalizeTargetPriority(value), TargetPriorityClearLine, StringComparison.OrdinalIgnoreCase)
                ? TargetPriorityNearest
                : TargetPriorityClearLine;
        }

        public static string RangeOriginLabel(string value)
        {
            return string.Equals(NormalizeRangeOrigin(value), RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)
                ? "玩家中心"
                : "鼠标中心";
        }

        public static string TargetPriorityLabel(string value)
        {
            return string.Equals(NormalizeTargetPriority(value), TargetPriorityNearest, StringComparison.OrdinalIgnoreCase)
                ? "最近优先"
                : "清线优先";
        }

        public static int ActiveRadius(AppSettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            return settings.CursorAimRadius;
        }
    }
}
