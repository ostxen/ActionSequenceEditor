using System;
using System.Collections.Generic;
using System.Linq;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 渲染状态只含值和本窗口不透明键，不把可写领域集合交给视图。
    public sealed class TimelineRenderState
    {
        public IReadOnlyList<TimelineRowState> Rows { get; }
        public IReadOnlyList<TimelineClipState> Clips { get; }
        public TimelineSize Size { get; }
        public float LeftWidth { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float ScrollY { get; }
        public float Playhead { get; }
        public TimelineRect? Marquee { get; }
        public InsertionIndicator Insertion { get; }
        public TimelineRenderState(IEnumerable<TimelineRowState> rows, IEnumerable<TimelineClipState> clips,
            TimelineSize size, TimelineWorkspace workspace, float playhead, TimelineRect? marquee, InsertionIndicator insertion = default)
        {
            Rows = Array.AsReadOnly(rows.ToArray()); Clips = Array.AsReadOnly(clips.ToArray()); Size = size;
            LeftWidth = workspace.LeftWidth; Minimum = workspace.ViewTimeMin; Maximum = workspace.ViewTimeMax;
            ScrollY = workspace.ScrollY; Playhead = playhead; Marquee = marquee; Insertion = insertion;
        }
    }
    public readonly struct TimelineRowState
    {
        public readonly int Key, Depth;
        public readonly string Title, TypeName;
        public readonly bool IsGroup, CanFold, Collapsed, Locked, Active, Selected;
        public readonly TimelineRect Bounds;
        public TimelineRowState(int key, int depth, string title, string typeName, bool group, bool canFold,
            bool collapsed, bool locked, bool active, bool selected, TimelineRect bounds)
        { Key = key; Depth = depth; Title = title; TypeName = typeName; IsGroup = group; CanFold = canFold;
            Collapsed = collapsed; Locked = locked; Active = active; Selected = selected; Bounds = bounds; }
    }
    public readonly struct TimelineClipState
    {
        public readonly int Key, RowKey;
        public readonly string Title, TypeName;
        public readonly bool Point, Selected, Locked, CanResize;
        public readonly TimelineRect Bounds;
        public TimelineClipState(int key, int rowKey, string title, string typeName, bool point, bool selected, bool locked, bool canResize, TimelineRect bounds)
        { Key = key; RowKey = rowKey; Title = title; TypeName = typeName; Point = point; Selected = selected; Locked = locked; CanResize = canResize; Bounds = bounds; }
    }
}
