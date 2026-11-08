using System;

namespace NBC.ActionEditor.Model
{
    public static class AutoSaveSchedule
    {
        public static bool IsDue(double lastSuccess, double now, double interval)
        {
            TimelineTime.RequireNonNegative(lastSuccess, nameof(lastSuccess));
            TimelineTime.RequireNonNegative(now, nameof(now));
            TimelineTime.RequireNonNegative(interval, nameof(interval));
            if (now < lastSuccess) throw new ArgumentOutOfRangeException(nameof(now));
            return interval > 0 && now - lastSuccess >= interval;
        }
    }
}
