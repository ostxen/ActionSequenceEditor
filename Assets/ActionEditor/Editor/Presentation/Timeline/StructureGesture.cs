using System;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Presentation.Timeline
{
    // 按下到释放只保存候选，成功释放才开启一次作者事务。
    public sealed class StructureGesture
    {
        private readonly DocumentSession session;
        private readonly StructureEditing editing;
        private readonly TimelineLayout layout;
        private readonly TimelineWorkspace workspace;
        private TimelineRootItem source;
        private StructureDrop drop;
        private long version;
        private float downX, downY, x, y;
        public bool Active => source != null;
        public bool Dragging { get; private set; }
        public InsertionIndicator Indicator { get; private set; }
        public StructureGesture(DocumentSession session, StructureEditing editing, TimelineLayout layout, TimelineWorkspace workspace)
        { this.session = session; this.editing = editing; this.layout = layout; this.workspace = workspace; }
        public bool Begin(TimelineRootItem item, float pointerX, float pointerY)
        {
            if (item is not IDirectable directable || directable.IsLocked || session.IsEditing) return false;
            source = item; version = session.Version; downX = x = pointerX; downY = y = pointerY;
            return true;
        }
        public void Move(float pointerX, float pointerY, TimelineSize size)
        {
            x = pointerX; y = pointerY;
            if (!Active) return;
            if (session.Version != version) { Cancel(); return; }
            if (!Dragging && (x - downX) * (x - downX) + (y - downY) * (y - downY) > 9) Dragging = true;
            Update(size);
        }
        // 时钟传入秒增量，边缘速度为0至300像素/秒，不越过内容范围。
        public void Tick(double deltaSeconds, TimelineSize size)
        {
            if (!Active || !Dragging) return;
            if (session.Version != version) { Cancel(); return; }
            if (x >= 0 && x < size.Width && y >= 0 && y < size.Height)
            {
                var velocity = y < 24 ? -300 * (24 - y) / 24 : y > size.Height - 24 ? 300 * (y - size.Height + 24) / 24 : 0;
                var maximum = Math.Max(0, layout.ContentHeight(size.RowHeight) - size.Height);
                workspace.ScrollY = (float)Math.Max(0, Math.Min(maximum, workspace.ScrollY + velocity * deltaSeconds));
            }
            Update(size);
        }
        private void Update(TimelineSize size)
        {
            drop = Dragging ? StructureDropGeometry.Hit(session.Document, source, layout, workspace, size, x, y) : default;
            var visible = drop.Hit && drop.Y >= 0 && drop.Y <= size.Height;
            Indicator = new InsertionIndicator(visible, drop.Hit && editing.MoveError(source, drop.Owner, drop.Boundary, version) == null, drop.Y, size.Width);
        }
        public void Finish()
        {
            var item = source; var target = drop; var currentVersion = version; var moved = Dragging;
            Cancel();
            if (!moved || !target.Hit) return;
            if (editing.Move(item, target.Owner, target.Boundary, currentVersion) && target.Owner != null)
                workspace.SetCollapsed(target.Owner, false);
        }
        public void Cancel() { source = null; drop = default; Dragging = false; Indicator = default; }
    }
}
