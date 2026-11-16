using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 渲染状态只含值和本窗口不透明键，不把可写领域集合交给视图。
    public sealed class TimelineRenderState
    {
        public IReadOnlyList<TimelineRowState> Rows { get; }
        public IReadOnlyList<TimelineClipState> Clips { get; }
        public TimelineSize Size { get; }
        public float LeftWidth { get; }
        public float ContentLeft => LeftWidth + Size.ContentInset;
        public float Minimum { get; }
        public float Maximum { get; }
        public float ScrollY { get; }
        public float Playhead { get; }
        public TimelineRect? Marquee { get; }
        public InsertionIndicator Insertion { get; }
        public IReadOnlyList<ClipGuideState> Guides { get; }
        public bool ResizeCursor { get; }
        public float Duration { get; }
        public float RangeStart { get; }
        public float RangeEnd { get; }
        public bool Playing { get; }
        public bool RangeEnabled { get; }
        public bool HasSequence { get; }
        public TimelineRenderState(IEnumerable<TimelineRowState> rows, IEnumerable<TimelineClipState> clips,
            TimelineSize size, TimelineWorkspace workspace, float playhead, TimelineRect? marquee, InsertionIndicator insertion = default,
            IEnumerable<ClipGuideState> guides = null, bool resizeCursor = false, NBC.ActionEditor.Model.ActionSequence sequence = null,
            bool playing = false, bool rangeEnabled = false)
        {
            Rows = Array.AsReadOnly(rows.ToArray()); Clips = Array.AsReadOnly(clips.ToArray()); Size = size;
            LeftWidth = workspace.LeftWidth; Minimum = workspace.ViewTimeMin; Maximum = workspace.ViewTimeMax;
            ScrollY = workspace.ScrollY; Playhead = playhead; Marquee = marquee; Insertion = insertion;
            Guides = Array.AsReadOnly((guides ?? Enumerable.Empty<ClipGuideState>()).ToArray()); ResizeCursor = resizeCursor;
            HasSequence = sequence != null; Duration = sequence?.Length ?? 0; RangeStart = sequence?.RangeMin ?? 0; RangeEnd = sequence?.RangeMax ?? 0;
            Playing = playing; RangeEnabled = rangeEnabled;
        }
    }
    // 保存单行绘制值，键只在本窗口当前布局中解析。
    public readonly struct TimelineRowState
    {
        public readonly int Key, Depth;
        public readonly string Title, TypeName;
        public readonly bool IsGroup, CanFold, Collapsed, Locked, Active, Selected;
        public readonly TimelineRect Bounds;
        public readonly Color Color;
        public TimelineRowState(int key, int depth, string title, string typeName, bool group, bool canFold,
            bool collapsed, bool locked, bool active, bool selected, TimelineRect bounds, Color color = default)
        { Key = key; Depth = depth; Title = title; TypeName = typeName; IsGroup = group; CanFold = canFold;
            Collapsed = collapsed; Locked = locked; Active = active; Selected = selected; Bounds = bounds; Color = color; }
    }
    // 保存片段几何与显示能力，不向视图暴露可写片段。
    public readonly struct TimelineClipState
    {
        public readonly int Key, RowKey;
        public readonly string Title, TypeName;
        public readonly bool Point, Selected, Locked, CanResize;
        public readonly TimelineRect Bounds;
        public readonly Color Color;
        public readonly float BlendInX, BlendOutX;
        public readonly bool Valid, DrawBlendOut, OverlapBefore;
        public TimelineClipState(int key, int rowKey, string title, string typeName, bool point, bool selected, bool locked, bool canResize, TimelineRect bounds,
            Color color = default, float blendInX = 0, float blendOutX = 0, bool valid = true, bool drawBlendOut = true, bool overlapBefore = false)
        { Key = key; RowKey = rowKey; Title = title; TypeName = typeName; Point = point; Selected = selected; Locked = locked; CanResize = canResize; Bounds = bounds;
            Color = color; BlendInX = blendInX; BlendOutX = blendOutX; Valid = valid; DrawBlendOut = drawBlendOut; OverlapBefore = overlapBefore; }
    }
    // 时间参考线随手势快照生成，视图不再自行寻找磁吸对象。
    public readonly struct ClipGuideState
    {
        public readonly float Time;
        public readonly bool Solid, Visible;
        public ClipGuideState(float time, bool solid, bool visible = true) { Time = time; Solid = solid; Visible = visible; }
    }
}
