using System;
using System.Collections.Generic;
using System.Linq;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Editing;
using ActionSequenceEditor.Application.Settings;
using ActionSequenceEditor.Model;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Timeline;

namespace ActionSequenceEditor.Presentation.Presenters
{
    // 每窗协调输入、工作区与显示快照，共享作者会话不共享捕获。
    public sealed class TimelinePresenter : IDisposable
    {
        private readonly SequenceSession session;
        private readonly SelectionState selection;
        private readonly StructureEditing structure;
        private readonly NodeEditing nodes;
        private readonly ClipboardEditing clipboard;
        private readonly ITimelineView view;
        private readonly ITimelinePlayhead playhead;
        private readonly Func<EditorSettings> settings;
        private readonly TimelineLayout layout = new TimelineLayout();
        private readonly TimelineInteraction interaction;
        private TimelineSize size = new TimelineSize(1000, 500);
        private bool disposed, refreshing;
        private bool playing, rangeEnabled;
        public TimelineWorkspace Workspace { get; }
        public TimelineRenderState State { get; private set; }
        public TimelineGestureMode Mode => interaction.Mode;
        public event Action<TimelineKey> HostAction;
        public TimelineMenuPresenter Menus { private get; set; }
        public IReadOnlyList<TimelineMenuEntry> Menu(float x, float y)
        {
            if (Menus == null || interaction.Captured) return Array.Empty<TimelineMenuEntry>();
            var item = x < State.ContentLeft ? layout.RowAt(y, Workspace, size) : (ActionSequenceEditor.IDirectable)layout.HitClip(x, y) ?? layout.RowAt(y, Workspace, size);
            var time = Math.Max(0, layout.Transform.Time(x)); var value = settings();
            if (value.MagnetSnapping) time = (float)Math.Round(time / value.TimeStep, MidpointRounding.ToEven) * value.TimeStep;
            return Menus.Build(item, x < State.ContentLeft, time);
        }
        public IReadOnlyList<TimelineMenuEntry> GroupMenu() => Menus?.Groups() ?? Array.Empty<TimelineMenuEntry>();
        public void Invoke(TimelineMenuEntry entry) => Run(() => { if (entry.Enabled) entry.Invoke(); });
        public int KeyFor(ActionSequenceEditor.IDirectable item) => layout.KeyFor(item);
        public void ToggleLock(int key) => Run(() => nodes.ToggleLock(layout.Resolve(key)));
        public void ToggleActive(int key) => Run(() => nodes.ToggleActive(layout.Resolve(key)));
        public void SetPlaybackState(bool isPlaying, bool rangePlaybackEnabled) { playing = isPlaying; rangeEnabled = rangePlaybackEnabled; Refresh(); }
        public void Scroll(float value) => Run(() => Workspace.ScrollY = Math.Max(0, value));
        public void ViewRange(float minimum, float maximum) => Run(() => Workspace.SetRange(minimum, maximum));
        public void Fit() => Run(() => Workspace.SetRange(0, Math.Max(.25f, session.Sequence?.Length ?? 7)));
        // 只滚动发起创建的窗口；展开父组后按当前行几何揭示完整对象。
        public void Reveal(IDirectable item) => Run(() =>
        {
            var row = item is ActionClip selectedClip ? selectedClip.Parent : item;
            if (row?.Parent is ActionGroup group) Workspace.SetCollapsed(group, false);
            Refresh();
            var index = layout.Rows.ToList().IndexOf(row);
            if (index < 0) return;
            var top = index * size.RowHeight;
            if (top < Workspace.ScrollY) Workspace.ScrollY = top;
            else if (top + size.RowHeight > Workspace.ScrollY + size.Height)
                Workspace.ScrollY = top + size.RowHeight - size.Height;
        });
        public TimelinePresenter(SequenceSession session, SelectionState selection, StructureEditing structure, NodeEditing nodes, ClipboardEditing clipboard,
            ClipEditing clips, TimelineWorkspace workspace, ITimelinePlayhead playhead, Func<EditorSettings> settings, ITimelineView view)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.structure = structure ?? throw new ArgumentNullException(nameof(structure));
            this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.playhead = playhead ?? throw new ArgumentNullException(nameof(playhead));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            interaction = new TimelineInteraction(session, selection, workspace, layout, clips, structure, playhead, settings, view.SetCapture);
            session.ContentChanged += OnContentChanged; session.Changed += Refresh; selection.Changed += Refresh;
            try { Refresh(); }
            catch { Dispose(); throw; }
        }
        public void Resize(TimelineSize value) { RequireAlive(); size = value; Refresh(); }
        public void Tick(double deltaSeconds)
        {
            RequireAlive();
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            Run(() => interaction.Tick(deltaSeconds, size));
        }
        public void Pointer(PointerInput input) => Run(() => interaction.Pointer(input, size));
        public void Wheel(WheelInput input) => Run(() =>
        {
            interaction.Cancel();
            if (input.X < Workspace.LeftWidth) Workspace.ScrollY = Math.Max(0, Workspace.ScrollY + input.Delta * 20);
            else ViewportNavigation.Zoom(Workspace, size, input.X, input.Delta, settings().ScrollWheelZooms);
        });
        public void ToggleFold(int rowKey) => Run(() =>
        {
            if (layout.Resolve(rowKey) is ActionSequenceEditor.Model.ActionGroup group && group.Tracks.Count > 0)
                Workspace.SetCollapsed(group, !Workspace.IsCollapsed(group));
        });
        public void Select(int key, bool add = false) => Run(() =>
        {
            var item = layout.Resolve(key);
            if (item == null) throw new InvalidOperationException("显示对象已失效。");
            selection.Set(selection.ClickCandidate(item, add, false), true);
        });
        public void Key(KeyInput input)
        {
            if (input.TextConsumed) return;
            Run(() =>
            {
                if (input.Key == TimelineKey.Escape) { interaction.Cancel(); return; }
                if (input.Key == TimelineKey.Undo || input.Key == TimelineKey.Redo)
                { interaction.Cancel(); HostAction?.Invoke(input.Key); return; }
                if (interaction.Captured) return;
                switch (input.Key)
                {
                    case TimelineKey.Delete: structure.DeleteSelection(); break;
                    case TimelineKey.Copy: clipboard.Copy(selection.First); break;
                    case TimelineKey.Cut: clipboard.Copy(selection.First, true); break;
                    case TimelineKey.Duplicate: clipboard.Duplicate(selection.First); break;
                    case TimelineKey.Paste:
                        var parent = selection.First is ActionSequenceEditor.Model.ActionClip clip ? clip.Parent : selection.First;
                        clipboard.Paste(parent, playhead.Time); break;
                    default: HostAction?.Invoke(input.Key); break;
                }
            });
        }
        public void Cancel() => Run(interaction.Cancel);
        public void Refresh()
        {
            if (disposed || refreshing) return;
            refreshing = true;
            try { State = layout.Build(session.Sequence, selection, Workspace, size, playhead.Time, interaction.Marquee, interaction.Insertion,
                interaction.Guides, interaction.ResizeCursor, playing, rangeEnabled); view.Render(State); }
            finally { refreshing = false; }
        }
        private void OnContentChanged() { interaction.Cancel(); Workspace.Bind(session.Sequence); }
        private void Run(Action action)
        {
            RequireAlive();
            try { action(); view.ShowError(null); }
            catch (Exception error)
            {
                var message = error.Message;
                try { interaction.Cancel(); }
                catch (Exception cleanup) { message += "; " + cleanup.Message; }
                view.ShowError(message);
            }
            finally { Refresh(); }
        }
        private void RequireAlive() { if (disposed) throw new ObjectDisposedException(nameof(TimelinePresenter)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; session.ContentChanged -= OnContentChanged; session.Changed -= Refresh; selection.Changed -= Refresh;
            try { interaction.Cancel(); }
            finally { layout.Clear(); HostAction = null; }
        }
    }
}
