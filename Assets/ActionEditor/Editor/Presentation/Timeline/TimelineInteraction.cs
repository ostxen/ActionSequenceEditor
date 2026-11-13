using System;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Presentation.Contracts;

namespace NBC.ActionEditor.Presentation.Timeline
{
    public enum TimelineGestureMode { Idle, Structure, Move, ResizeLeft, ResizeRight, Marquee, Pan, Splitter, Seek }

    // 捕获和候选归单窗口实例，取消只触碰本实例创建的令牌。
    public sealed class TimelineInteraction
    {
        private readonly object owner = new object();
        private readonly DocumentSession session;
        private readonly SelectionState selection;
        private readonly TimelineWorkspace workspace;
        private readonly VisibleTimelineLayout layout;
        private readonly ClipGesture clip;
        private readonly StructureGesture structure;
        private readonly ITimelinePlayhead playhead;
        private readonly Action<bool> capture;
        private readonly Func<EditorSettings> settings;
        private float downX, downY, lastX, seekBefore;
        private bool control;
        private IDirectable[] selectionBefore;
        private PointerButton button;
        public TimelineGestureMode Mode { get; private set; }
        public TimelineRect? Marquee { get; private set; }
        public InsertionIndicator Insertion => structure.Indicator;
        public ClipGuideState[] Guides => clip.Guides;
        public bool ResizeCursor { get; private set; }
        public bool Captured => Mode != TimelineGestureMode.Idle;
        public TimelineInteraction(DocumentSession session, SelectionState selection, TimelineWorkspace workspace,
            VisibleTimelineLayout layout, ClipEditing editing, StructureEditing structureEditing, ITimelinePlayhead playhead, Func<EditorSettings> settings, Action<bool> capture)
        { this.session = session; this.selection = selection; this.workspace = workspace; this.layout = layout;
            this.playhead = playhead; this.settings = settings; this.capture = capture; clip = new ClipGesture(session, editing);
            structure = new StructureGesture(session, structureEditing, layout, workspace); }

        public void Tick(double seconds, TimelineSize size)
        {
            if (Mode != TimelineGestureMode.Structure) return;
            structure.Tick(seconds, size);
            if (!structure.Active) Cancel();
        }

        public void Pointer(PointerInput input, TimelineSize size)
        {
            if (input.Action == PointerAction.Cancel) { Cancel(); return; }
            if (input.Action == PointerAction.Down) { Down(input, size); return; }
            if (!Captured)
            {
                var hovered = input.Action == PointerAction.Move && input.X >= workspace.LeftWidth ? layout.HitClip(input.X, input.Y) : null;
                ResizeCursor = hovered != null && !hovered.IsLocked && hovered.CanScale()
                    && (input.X - layout.Bounds(hovered).X < 5 || layout.Bounds(hovered).Right - input.X < 5);
                return;
            }
            if (input.Action == PointerAction.Up)
            {
                if (input.Button != button) return;
                if (input.X < 0 || input.X >= size.Width || input.Y < 0 || input.Y >= size.Height) { Cancel(); return; }
                if (Mode == TimelineGestureMode.Structure) structure.Move(input.X, input.Y, size);
                Finish(); return;
            }
            if (input.Action == PointerAction.Move) Move(input, size);
        }
        private void Down(PointerInput input, TimelineSize size)
        {
            if (Captured || session.Document == null || session.IsEditing || input.Button == PointerButton.Secondary) return;
            if (input.X < 0 || input.X >= size.Width || input.Y < 0 || input.Y >= size.Height) return;
            downX = lastX = input.X; downY = input.Y; control = input.Control; button = input.Button;
            selectionBefore = selection.Items;
            if (input.Button == PointerButton.Middle) Mode = TimelineGestureMode.Pan;
            else if (input.Region == TimelineRegion.Ruler)
            { Mode = TimelineGestureMode.Seek; seekBefore = playhead.Time; playhead.Seek(Math.Max(0, layout.Transform.Time(input.X))); }
            else if (Math.Abs(input.X - workspace.LeftWidth) < 5) Mode = TimelineGestureMode.Splitter;
            else if (input.X < workspace.LeftWidth)
            {
                var row = layout.RowAt(input.Y, workspace, size);
                if (row == null) return;
                selection.Set(selection.ClickCandidate(row, input.Control, false), true);
                if (!structure.Begin((TimelineRootItem)row, input.X, input.Y)) return;
                Mode = TimelineGestureMode.Structure;
            }
            else
            {
                var target = layout.HitClip(input.X, input.Y);
                if (target == null)
                { Mode = TimelineGestureMode.Marquee; if (!input.Control) selection.Set(Array.Empty<IDirectable>(), false); }
                else
                {
                    if (!selection.Contains(target)) selection.Set(selection.ClickCandidate(target, input.Control, true), true);
                    if (target.IsLocked) return;
                    var bounds = layout.Bounds(target);
                    var edge = target.CanScale() ? input.X - bounds.X < 5 ? -1 : bounds.Right - input.X < 5 ? 1 : 0 : 0;
                    if (!clip.Begin(owner, target, selection.Items.OfType<TimelineClip>(), edge, input.X, playhead.Time)) return;
                    Mode = edge < 0 ? TimelineGestureMode.ResizeLeft : edge > 0 ? TimelineGestureMode.ResizeRight : TimelineGestureMode.Move;
                    ResizeCursor = edge != 0;
                }
            }
            capture(true);
        }
        private void Move(PointerInput input, TimelineSize size)
        {
            if (Mode == TimelineGestureMode.Structure)
            { structure.Move(input.X, input.Y, size); if (!structure.Active) Cancel(); }
            else if (Mode == TimelineGestureMode.Pan) ViewportNavigation.Pan(workspace, size, input.X - lastX);
            else if (Mode == TimelineGestureMode.Splitter) workspace.LeftWidth = Math.Max(120, Math.Min(size.Width - 120, workspace.LeftWidth + input.X - lastX));
            else if (Mode == TimelineGestureMode.Seek) playhead.Seek(Math.Max(0, layout.Transform.Time(input.X)));
            else if (Mode == TimelineGestureMode.Marquee) Marquee = TimelineRect.Between(Math.Max(workspace.LeftWidth, downX), downY, Math.Max(workspace.LeftWidth, input.X), input.Y);
            else
            {
                if (!clip.Active) { Cancel(); return; }
                clip.Move(input.X, input.Shift, layout.Transform, settings());
            }
            lastX = input.X;
        }
        private void Finish()
        {
            var mode = Mode; var marquee = Marquee;
            Clear();
            selectionBefore = null;
            if (mode == TimelineGestureMode.Structure) structure.Finish();
            else if (mode == TimelineGestureMode.Marquee && marquee.HasValue)
            {
                var selected = layout.In(marquee.Value).Cast<IDirectable>();
                if (control) selected = selection.Items.Concat(selected).Distinct();
                selection.Set(selected.ToArray(), true);
            }
            else if (mode == TimelineGestureMode.Move || mode == TimelineGestureMode.ResizeLeft || mode == TimelineGestureMode.ResizeRight) clip.Finish();
        }
        public void Cancel()
        {
            var seek = Mode == TimelineGestureMode.Seek;
            var selected = Mode == TimelineGestureMode.Marquee ? selectionBefore : null;
            selectionBefore = null;
            try { Clear(); }
            finally { structure.Cancel(); clip.Cancel(); }
            if (selected != null) selection.Set(selected.Where(item => session.Document != null && session.Document.directables.Contains(item)).ToArray(), false);
            if (seek) playhead.Seek(seekBefore);
        }
        private void Clear() { var captured = Captured; Mode = TimelineGestureMode.Idle; Marquee = null; ResizeCursor = false; if (captured) capture(false); }
    }
}
