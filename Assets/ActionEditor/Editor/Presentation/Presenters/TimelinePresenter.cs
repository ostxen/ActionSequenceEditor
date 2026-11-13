using System;
using System.Linq;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Presentation.Contracts;
using NBC.ActionEditor.Presentation.Timeline;

namespace NBC.ActionEditor.Presentation.Presenters
{
    // 每窗协调输入、工作区与显示快照，共享作者会话不共享捕获。
    public sealed class TimelinePresenter : IDisposable
    {
        private readonly DocumentSession session;
        private readonly SelectionState selection;
        private readonly StructureEditing structure;
        private readonly ClipboardEditing clipboard;
        private readonly ITimelineView view;
        private readonly ITimelinePlayhead playhead;
        private readonly Func<EditorSettings> settings;
        private readonly VisibleTimelineLayout layout = new VisibleTimelineLayout();
        private readonly TimelineInteraction interaction;
        private TimelineSize size = new TimelineSize(1000, 500);
        private bool disposed, refreshing;
        public TimelineWorkspace Workspace { get; }
        public TimelineRenderState State { get; private set; }
        public TimelineGestureMode Mode => interaction.Mode;
        public event Action<TimelineKey> HostAction;
        public TimelinePresenter(DocumentSession session, SelectionState selection, StructureEditing structure, ClipboardEditing clipboard,
            ClipEditing clips, TimelineWorkspace workspace, ITimelinePlayhead playhead, Func<EditorSettings> settings, ITimelineView view)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.structure = structure ?? throw new ArgumentNullException(nameof(structure));
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
        public void Tick(double seconds)
        {
            RequireAlive();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Run(() => interaction.Tick(seconds, size));
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
            if (layout.Resolve(rowKey) is NBC.ActionEditor.Domain.TimelineGroup group && group.Tracks.Count > 0)
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
                if (interaction.Captured) return;
                switch (input.Key)
                {
                    case TimelineKey.Delete: structure.DeleteSelection(); break;
                    case TimelineKey.Copy: clipboard.Copy(selection.First); break;
                    case TimelineKey.Cut: clipboard.Copy(selection.First, true); break;
                    case TimelineKey.Duplicate: clipboard.Duplicate(selection.First); break;
                    case TimelineKey.Paste:
                        var parent = selection.First is NBC.ActionEditor.Domain.TimelineClip clip ? clip.Parent : selection.First;
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
            try { State = layout.Build(session.Document, selection, Workspace, size, playhead.Time, interaction.Marquee, interaction.Insertion); view.Render(State); }
            finally { refreshing = false; }
        }
        private void OnContentChanged() { interaction.Cancel(); Workspace.Bind(session.Document); }
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
