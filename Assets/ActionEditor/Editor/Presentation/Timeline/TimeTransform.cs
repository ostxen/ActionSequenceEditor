using System;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 时间与像素使用同一变换，最小宽度只影响命中和绘制，不写回作者时间。
    public readonly struct TimeTransform
    {
        public readonly float Minimum, Span, Width, Left;
        public float Scale => Width / Span;
        public TimeTransform(float minimum, float span, float width, float left = 0)
        {
            if (float.IsNaN(minimum) || float.IsInfinity(minimum) || float.IsNaN(span) || float.IsInfinity(span) || span <= 0
                || float.IsNaN(width) || float.IsInfinity(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(span));
            Minimum = minimum; Span = span; Width = width; Left = left;
        }
        // 中间运算保留双精度，避免先舍入比例后放大远端坐标误差。
        public float X(float time) => (float)(Left + ((double)time - Minimum) / Span * Width);
        public float Time(float x) => (float)(Minimum + ((double)x - Left) / Width * Span);
        public float Duration(float pixels) => (float)((double)pixels / Width * Span);
    }
}
