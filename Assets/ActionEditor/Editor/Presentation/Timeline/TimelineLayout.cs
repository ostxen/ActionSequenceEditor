using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Application.Editing;

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

    // 计算全部展开行及片段的几何和命中，不代表可见区虚拟化；键缓存只保留当前图。
    public sealed class TimelineLayout
    {
        private readonly Dictionary<IDirectable, int> keys = new Dictionary<IDirectable, int>();
        private readonly Dictionary<int, IDirectable> values = new Dictionary<int, IDirectable>();
        private readonly List<IDirectable> rows = new List<IDirectable>();
        private readonly List<(TimelineClip clip, TimelineRect bounds)> clips = new List<(TimelineClip, TimelineRect)>();
        private int nextKey;
        private ActionDocument document;
        public IReadOnlyList<IDirectable> Rows => rows.AsReadOnly();
        public TimeTransform Transform { get; private set; }
        public float ContentHeight(float pitch) => rows.Count * pitch + 32;
        public IDirectable Resolve(int key) => values.TryGetValue(key, out var item) ? item : null;
        public int KeyFor(IDirectable item) => keys.TryGetValue(item, out var key) ? key : 0;
        public void Clear() { keys.Clear(); values.Clear(); rows.Clear(); clips.Clear(); document = null; }
        public TimelineClip HitClip(float x, float y) => clips.LastOrDefault(pair => pair.bounds.Contains(x, y)).clip;
        public TimelineRect Bounds(TimelineClip clip) => clips.First(pair => ReferenceEquals(pair.clip, clip)).bounds;
        public IEnumerable<TimelineClip> In(TimelineRect rect) => clips.Where(pair => pair.bounds.Overlaps(rect)).Select(pair => pair.clip);
        public IDirectable RowAt(float y, TimelineWorkspace workspace, TimelineSize size)
        {
            var index = (int)Math.Floor((y + workspace.ScrollY) / size.RowHeight);
            return index >= 0 && index < rows.Count ? rows[index] : null;
        }
        public TimelineRenderState Build(ActionDocument source, SelectionState selection, TimelineWorkspace workspace,
            TimelineSize size, float playhead, TimelineRect? marquee, InsertionIndicator insertion = default,
            IEnumerable<ClipGuideState> guides = null, bool resizeCursor = false, bool playing = false, bool rangeEnabled = false)
        {
            if (!ReferenceEquals(document, source)) { keys.Clear(); values.Clear(); document = source; }
            var current = new HashSet<IDirectable>(source?.directables ?? Enumerable.Empty<IDirectable>());
            foreach (var item in keys.Keys.Where(item => !current.Contains(item)).ToArray())
            { values.Remove(keys[item]); keys.Remove(item); }
            rows.Clear(); clips.Clear(); workspace.Bind(source);
            if (source != null)
                foreach (var root in source.RootItems)
                {
                    rows.Add((IDirectable)root);
                    if (root is TimelineGroup group && !workspace.IsCollapsed(group)) rows.AddRange(group.Tracks);
                }
            workspace.ScrollY = Math.Max(0, Math.Min(workspace.ScrollY, Math.Max(0, ContentHeight(size.RowHeight) - size.Height)));
            var left = workspace.LeftWidth + size.ContentInset;
            Transform = new TimeTransform(workspace.ViewTimeMin, workspace.ViewTime, Math.Max(1, size.WindowWidth - left), left);
            var rowStates = new List<TimelineRowState>(); var clipStates = new List<TimelineClipState>();
            for (var index = 0; index < rows.Count; index++)
            {
                var item = rows[index]; var group = item as TimelineGroup;
                var y = index * size.RowHeight - workspace.ScrollY;
                rowStates.Add(new TimelineRowState(Key(item), item.Parent == null ? 0 : 1, item.Name ?? DirectableTypeInfo.For(item.GetType()).Name,
                    item.GetType().AssemblyQualifiedName, group != null, group != null && group.Tracks.Count > 0,
                    workspace.IsCollapsed(item), item.IsLocked, item.IsActive, selection.Contains(item), new TimelineRect(0, y, size.Width, size.RowHeight), DirectableTypeInfo.For(item.GetType()).Color));
                if (item is not TimelineTrack track) continue;
                foreach (var clip in track.Clips.OrderBy(clip => clip.StartTime))
                {
                    var point = clip is PointClip || clip.Length == 0;
                    var width = point ? 12 : Math.Max(5, clip.Length * Transform.Scale);
                    var rect = new TimelineRect(Transform.X(clip.StartTime) - (point ? 6 : 0), y + 3, width, Math.Max(1, size.RowHeight - 6));
                    clips.Add((clip, rect));
                    var next = clip.GetNextSibling(); var previous = clip.GetPreviousSibling();
                    clipStates.Add(new TimelineClipState(Key(clip), Key(track), DirectableTypeInfo.For(clip.GetType()).Name,
                        clip.GetType().AssemblyQualifiedName, point, selection.Contains(clip), clip.IsLocked, clip.CanScale(), rect,
                        DirectableTypeInfo.For(clip.GetType()).Color, Transform.X(clip.StartTime + Math.Min(clip.Length, clip.BlendIn)),
                        Transform.X(clip.EndTime - Math.Min(clip.Length, clip.BlendOut)), clip.IsValid,
                        next == null || next.StartTime >= clip.EndTime, previous != null && previous.EndTime > clip.StartTime));
                }
            }
            return new TimelineRenderState(rowStates, clipStates, size, workspace, playhead, marquee, insertion, guides, resizeCursor, source, playing, rangeEnabled);
        }
        private int Key(IDirectable item)
        {
            if (!keys.TryGetValue(item, out var key)) { key = ++nextKey; keys.Add(item, key); values.Add(key, item); }
            return key;
        }
    }
}
