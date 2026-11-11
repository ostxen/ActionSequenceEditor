using UnityEngine;

namespace NBC.ActionEditor
{
    public static class TimelineNavigation
    {
        public static float ZoomMaximum(float minimum, float maximum, float wheel, float windowWidth, bool enabled)
        {
            var span = maximum - minimum;
            if (!enabled || wheel == 0 || wheel > 0 && span > 240) return maximum;
            return maximum + Mathf.Sign(wheel) * 225 / Mathf.Max(1, windowWidth) * span;
        }
        public static Vector2 Pan(float minimum, float maximum, float pixels, float width, bool preserveSpanAtZero)
        {
            var offset = pixels / Mathf.Max(1, width) * (maximum - minimum);
            var min = minimum - offset;
            var max = maximum - offset;
            if (min >= 0) return new Vector2(min, max);
            if (preserveSpanAtZero) return new Vector2(0, maximum - minimum);
            return minimum > 0 ? new Vector2(0, max) : new Vector2(minimum, maximum);
        }
    }
}
