using NBC.ActionEditor.Infrastructure.Serialization;
using System;
using NBC.ActionEditor.Application.Sequences;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Playback;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Infrastructure.Sequences;
using NBC.ActionEditor.Infrastructure.Preview;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 唯一共享对象的创建与销毁归组合根；不向使用方提供服务查询。
    public sealed class EditorCompositionRoot : IDisposable
    {
        private readonly SequenceSession session;
        private readonly SequenceLifecycle lifecycle;
        private readonly SequenceAutoSave autoSave;
        private readonly SelectionState selection;
        private readonly ClipboardState clipboard;
        private readonly StructureEditing structure;
        private readonly NodeEditing nodes;
        private readonly ClipboardEditing clipboardEditing;
        private readonly PropertyEditing properties;
        private readonly ClipEditing clips;
        private readonly IEditorClock clock;
        private readonly IRecoveryStore recovery;
        private readonly SequencePlayback playback;
        private readonly SequenceSampler sampler;
        private readonly SequenceNavigation navigation;
        private bool disposed;
        public EditorCompositionRoot(SequenceSession session, SequenceFileRepository repository = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            repository ??= new SequenceFileRepository();
            clock = new Infrastructure.Preview.EditorClock(); recovery = new Infrastructure.Sequences.SessionStateRecoveryStore();
            lifecycle = SequenceComposition.Lifecycle(session, repository);
            autoSave = new SequenceAutoSave(session, lifecycle, clock);
            sampler = new SequenceSampler(() => session.Sequence, session: session);
            playback = new SequencePlayback(sampler);
            selection = new SelectionState(session); clipboard = new ClipboardState();
            structure = new StructureEditing(session, () => selection.Items); nodes = new NodeEditing(session);
            clipboardEditing = new ClipboardEditing(session, structure, clipboard, new FragmentCodec(), item => selection.Set(new[] { item }, true));
            properties = new PropertyEditing(session, new PropertyValueFactory()); clips = new ClipEditing(session);
            navigation = new SequenceNavigation(session, lifecycle, repository, playback);
        }
        public WindowBinding CreateBinding(object owner, Action repaint, Action changed, EditorViewRegistry views,
            SettingsSession settings, Func<bool, string> chooseSavePath = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EditorCompositionRoot));
            var dialogs = (owner as Views.IEditorDialogHost)?.Dialogs ?? new Views.IMGUI.EditorDialogAdapter();
            var sequencePresenter = CreateSequencePresenter(chooseSavePath, dialogs);
            return new WindowBinding(owner, repaint, changed, views, sequencePresenter,
                PlaybackComposition.Create(playback, sampler, clock, settings), CreateProperties(settings), settings,
                new EditorExtensionContext(new ExtensionSequenceProjection(() => session.Sequence, () => sampler.CurrentTime).Capture),
                () => navigation.Source != null ? navigation.Source.name : string.Empty, () => autoSave.LastSaved,
                (workspace, view) => TimelineComposition.Create(session, selection, structure, nodes, clipboardEditing, clips, clipboard,
                    sampler, workspace, view, settings, sequencePresenter, dialogs),
                items => selection.Set(items, false), () => playback.Stop());
        }
        public SequencePresenter CreateSequencePresenter(Func<bool, string> chooseSavePath = null, Views.IEditorDialogs dialogs = null) => SequencePresenterComposition.Create(
            session, lifecycle, navigation, () => navigation.Source != null ? navigation.Source.name : "Action", autoSave.RecordSaved, chooseSavePath, dialogs);
        public PropertyPresenter CreateProperties(SettingsSession settings) => PropertyComposition.Create(properties, nodes, clips, selection, settings);
        public EditorUpdateDriver CreateDriver(EditorViewRegistry views, SettingsSession settings, Action<Exception> failed)
            => new EditorUpdateDriver(autoSave, playback, clock, views, settings, failed);
        public void ConnectInspector(Action show) => selection.InspectorRequested += show;
        public void DisconnectInspector(Action show) => selection.InspectorRequested -= show;
        public void RestoreRecovery()
        {
            var text = recovery.Read();
            if (!string.IsNullOrEmpty(text)) session.RestoreRecovery(text);
        }
        public void BeforeReload() { try { recovery.Write(session.CaptureRecovery()); } finally { Dispose(); } }
        public void StopPlayback() => playback.Stop();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { playback.Dispose(); }
            finally { try { sampler.Dispose(); } finally { selection.Dispose(); session.Dispose(); } }
        }
    }
}
