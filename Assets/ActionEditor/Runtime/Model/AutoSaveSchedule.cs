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
            // 直接比较截止时刻，避免减法抵消让边界时刻被误判为未到期。
            return interval > 0 && now >= lastSuccess + interval;
        }
    }
}
