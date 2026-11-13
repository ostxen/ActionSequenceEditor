using System;
using System.Linq;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 候选引用当前真实所有者及移除前边界，几何不修改作者图。
    public readonly struct StructureDrop
    {
        public readonly bool Hit;
        public readonly TimelineGroup Owner;
        public readonly int Boundary;
        public readonly float Y;
        public StructureDrop(TimelineGroup owner, int boundary, float y)
        { Hit = true; Owner = owner; Boundary = boundary; Y = y; }
    }

    // 根区和入组区互斥；组排序始终使用整个可见组块。
    public static class StructureDropGeometry
    {
        public static StructureDrop Hit(ActionDocument document, TimelineRootItem source, VisibleTimelineLayout layout,
            TimelineWorkspace workspace, TimelineSize size, float x, float y)
        {
            if (document == null || x < 0 || x >= size.Width || y < 0 || y >= size.Height) return default;
            var contentY = y + workspace.ScrollY;
            var rows = layout.Rows;
            var index = (int)Math.Floor(contentY / size.RowHeight);
            if (index >= rows.Count) return new StructureDrop(null, document.RootItems.Count, rows.Count * size.RowHeight - workspace.ScrollY);
            var row = rows[index];
            if (source is TimelineTrack && x >= 20)
            {
                if (row is TimelineGroup group) return new StructureDrop(group, 0, (index + 1) * size.RowHeight - workspace.ScrollY);
                var track = (TimelineTrack)row;
                var after = contentY >= (index + .5f) * size.RowHeight;
                var boundary = track.Group == null ? document.RootItems.ToList().IndexOf(track) : track.Group.Tracks.IndexOf(track);
                return new StructureDrop(track.Group, boundary + (after ? 1 : 0), (index + (after ? 1 : 0)) * size.RowHeight - workspace.ScrollY);
            }
            var root = row is TimelineTrack child && child.Group != null ? (TimelineRootItem)child.Group : (TimelineRootItem)row;
            var first = index;
            while (first > 0 && !ReferenceEquals(rows[first], root)) first--;
            var end = first + 1;
            if (root is TimelineGroup block)
                while (end < rows.Count && rows[end] is TimelineTrack member && ReferenceEquals(member.Group, block)) end++;
            var below = source is TimelineGroup
                ? contentY >= (first + end) * .5f * size.RowHeight
                : contentY >= (index + .5f) * size.RowHeight;
            return new StructureDrop(null, document.RootItems.ToList().IndexOf(root) + (below ? 1 : 0),
                (below ? end : first) * size.RowHeight - workspace.ScrollY);
        }
    }
}
