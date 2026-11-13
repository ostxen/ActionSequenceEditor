using System.Collections.Generic;
using System.Linq;
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
            rows.AddRange(asset.rootTracks);
            return rows;
        }
        public static bool IsVisible(Clip clip) => clip?.Parent is Track track && !track.IsCollapsed;
        public static TrackDropTarget TrackDrop(Asset asset, Track moving, Vector2 point, float scroll, float width, float height, float pitch)
        {
            if (asset == null || moving == null || point.x < 0 || point.x >= width || point.y < 0 || point.y >= height) return default;
            var rows = VisibleRows(asset); var row = RowAt(point.y, scroll, pitch);
            if (row < 0) return default;
            var boundary = Mathf.Clamp(row, 0, rows.Count) * pitch - scroll;
            if (row >= rows.Count || point.x < 20)
            {
                if (row < rows.Count && rows[row] is Track root && root.Group == null)
                {
                    var rootAfter = (point.y + scroll) % pitch >= pitch / 2;
                    return new TrackDropTarget(null, asset.rootTracks.IndexOf(root) + (rootAfter ? 1 : 0), root,
                        boundary + (rootAfter ? pitch : 0), !rootAfter);
                }
                return new TrackDropTarget(null, asset.rootTracks.Count, rows.LastOrDefault(), rows.Count * pitch - scroll, false);
            }
            if (rows[row] is Group group) return new TrackDropTarget(group, 0, group, boundary + pitch, false);
            var target = (Track)rows[row]; var after = (point.y + scroll) % pitch >= pitch / 2;
            var siblings = target.Group?.Tracks ?? asset.rootTracks;
            return new TrackDropTarget(target.Group, siblings.IndexOf(target) + (after ? 1 : 0), target, boundary + (after ? pitch : 0), !after);
        }
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

    public readonly struct TrackDropTarget
    {
        public readonly bool IsValid, Before;
        public readonly Group Group;
        public readonly int Index;
        public readonly IDirectable Row;
        public readonly float Y;
        public TrackDropTarget(Group group, int index, IDirectable row, float y, bool before)
        { IsValid = true; Group = group; Index = index; Row = row; Y = y; Before = before; }
    }
}
