using NBC.ActionEditor.Domain;
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
        public Rect ClipRect(TimelineClip clip, float y, float height, float pointWidth, float minimumWidth)
        {
            var point = DirectableTypeInfo.For(clip.GetType()).IsSignal || clip.Length == 0;
            return new Rect(X(clip.StartTime) - (point ? pointWidth / 2 : 0), y,
                point ? pointWidth : Mathf.Max(minimumWidth, clip.Length / Span * Width), height);
        }
    }

    public static class TimelineGeometry
    {
        public static List<IDirectable> VisibleRows(ActionDocument asset, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace)
        {
            var rows = new List<IDirectable>();
            if (asset == null) return rows;
            foreach (var item in asset.RootItems)
            {
                rows.Add((IDirectable)item);
                if (item is TimelineGroup group && !workspace.IsCollapsed(group)) rows.AddRange(group.Tracks);
            }
            return rows;
        }
        public static bool IsVisible(TimelineClip clip, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace) => clip?.Parent is TimelineTrack track && !workspace.IsCollapsed(track);
        public static TrackDropTarget TrackDrop(ActionDocument asset, TimelineTrack moving, Vector2 point, float scroll, float width, float height, float pitch, NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace workspace)
        {
            if (asset == null || moving == null || point.x < 0 || point.x >= width || point.y < 0 || point.y >= height) return default;
            var rows = VisibleRows(asset, workspace); var row = RowAt(point.y, scroll, pitch);
            if (row < 0) return default;
            var boundary = Mathf.Clamp(row, 0, rows.Count) * pitch - scroll;
            if (row >= rows.Count || point.x < 20)
            {
                if (row < rows.Count && rows[row] is TimelineTrack root && root.Group == null)
                {
                    var rootAfter = (point.y + scroll) % pitch >= pitch / 2;
                    return new TrackDropTarget(null, asset.RootItems.ToList().IndexOf(root) + (rootAfter ? 1 : 0), root,
                        boundary + (rootAfter ? pitch : 0), !rootAfter);
                }
                return new TrackDropTarget(null, asset.RootItems.Count, rows.LastOrDefault(), rows.Count * pitch - scroll, false);
            }
            if (rows[row] is TimelineGroup group) return new TrackDropTarget(group, 0, group, boundary + pitch, false);
            var target = (TimelineTrack)rows[row]; var after = (point.y + scroll) % pitch >= pitch / 2;
            var index = target.Group == null ? asset.RootItems.ToList().IndexOf(target) : target.Group.Tracks.IndexOf(target);
            return new TrackDropTarget(target.Group, index + (after ? 1 : 0), target, boundary + (after ? pitch : 0), !after);
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
        public readonly TimelineGroup Group;
        public readonly int Index;
        public readonly IDirectable Row;
        public readonly float Y;
        public TrackDropTarget(TimelineGroup group, int index, IDirectable row, float y, bool before)
        { IsValid = true; Group = group; Index = index; Row = row; Y = y; Before = before; }
    }
}
