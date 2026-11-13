using System;

namespace NBC.ActionEditor.Presentation.Timeline
{
    public enum PointerAction { Down, Move, Up, Cancel }
    public enum PointerButton { Primary, Secondary, Middle }
    public enum TimelineRegion { Body, Ruler }
    public enum TimelineKey { Escape, Delete, Copy, Cut, Paste, Duplicate, Save, Undo, Redo }

    // 原生事件在视图边界转换为局部坐标，不依赖全局GUI事件。
    public readonly struct PointerInput
    {
        public readonly PointerAction Action;
        public readonly PointerButton Button;
        public readonly float X, Y;
        public readonly bool Control, Shift;
        public readonly TimelineRegion Region;
        public PointerInput(PointerAction action, float x, float y, PointerButton button = PointerButton.Primary,
            bool control = false, bool shift = false, TimelineRegion region = TimelineRegion.Body)
        { Action = action; X = x; Y = y; Button = button; Control = control; Shift = shift; Region = region; }
    }
    public readonly struct WheelInput
    {
        public readonly float X, Y, Delta;
        public WheelInput(float x, float y, float delta) { X = x; Y = y; Delta = delta; }
    }
    public readonly struct KeyInput
    {
        public readonly TimelineKey Key;
        public readonly bool TextConsumed;
        public KeyInput(TimelineKey key, bool textConsumed = false) { Key = key; TextConsumed = textConsumed; }
    }
    public readonly struct TimelineSize
    {
        public readonly float Width, Height, RowHeight, WindowWidth;
        public TimelineSize(float width, float height, float rowHeight = 28, float windowWidth = 0)
        {
            if (!Finite(width) || !Finite(height) || !Finite(rowHeight) || !Finite(windowWidth) || width <= 0 || height < 0 || rowHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Height = height; RowHeight = rowHeight; WindowWidth = windowWidth > 0 ? windowWidth : width;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    // 轻量几何值可复制给渲染器，不暴露领域可变对象。
    public readonly struct TimelineRect
    {
        public readonly float X, Y, Width, Height;
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public TimelineRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;
        public bool Overlaps(TimelineRect other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        public static TimelineRect Between(float x, float y, float otherX, float otherY) =>
            new TimelineRect(Math.Min(x, otherX), Math.Min(y, otherY), Math.Abs(otherX - x), Math.Abs(otherY - y));
    }
}
