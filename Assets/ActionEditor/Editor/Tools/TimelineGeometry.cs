using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor
{
    /// <summary>Read-only timeline geometry. Each renderer supplies its own dimensions and offsets.</summary>
    public readonly struct JsonTimelineTransform
    {
        public readonly float Minimum, Span, Width, Left;
        public JsonTimelineTransform(float minimum, float span, float width, float left = 0)
        { Minimum = minimum; Span = Mathf.Max(.001f, span); Width = Mathf.Max(1, width); Left = left; }
        public float Scale => Width / Span;
        public float X(float time) => Left + (time - Minimum) / Span * Width;
        public float Time(float x) => (x - Left) / Width * Span + Minimum;
        public float Duration(float pixels) => pixels / Width * Span;
        public Rect ClipRect(Clip clip, float y, float height, float pointWidth, float minimumWidth)
        {
            var point = DirectableTypeInfo.For(clip.GetType()).IsSignal || clip.Length == 0;
            return new Rect(X(clip.StartTime) - (point ? pointWidth / 2 : 0), y,
                point ? pointWidth : Mathf.Max(minimumWidth, clip.Length / Span * Width), height);
        }
    }

    public static class TimelineGeometry
    {
        public static List<IDirectable> VisibleRows(Asset asset)
        {
            var rows = new List<IDirectable>();
            if (asset == null) return rows;
            foreach (var group in asset.groups)
            {
                rows.Add(group);
                if (!group.IsCollapsed) rows.AddRange(group.Tracks);
            }
            return rows;
        }
        public static bool IsVisible(Clip clip) => clip?.Parent is Track track && track.Group != null && !track.Group.IsCollapsed;
        public static int RowAt(float y, float scroll, float pitch) => Mathf.FloorToInt((y + scroll) / pitch);
        // Left handle wins when a very short interval has overlapping handles.
        public static int ResizeEdge(Rect rect, Vector2 point, float handleWidth)
        {
            if (!rect.Contains(point)) return 0;
            if (point.x - rect.xMin < handleWidth) return -1;
            return rect.xMax - point.x < handleWidth ? 1 : 0;
        }
        public static bool Overlaps(Rect first, Rect second) => first.Overlaps(second, true);
    }
}
