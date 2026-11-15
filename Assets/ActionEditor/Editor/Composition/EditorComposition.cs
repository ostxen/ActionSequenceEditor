using NBC.ActionEditor.Infrastructure.Serialization;
using System;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Editing;
using NBC.ActionEditor.Application.Playback;
using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Application.Settings;
using NBC.ActionEditor.Infrastructure.Documents;
using NBC.ActionEditor.Infrastructure.Preview;
using NBC.ActionEditor.Presentation.Presenters;

namespace NBC.ActionEditor.Composition
{
    // 唯一共享对象的创建与销毁归组合根；不向使用方提供服务查询。
    public sealed class EditorComposition : IDisposable
    {
        private readonly DocumentSession session;
        private readonly DocumentLifecycle lifecycle;
        private readonly DocumentAutoSave autoSave;
        private readonly SelectionState selection;
        private readonly ClipboardState clipboard;
        private readonly StructureEditing structure;
        private readonly NodeEditing nodes;
        private readonly ClipboardEditing clipboardEditing;
        private readonly PropertyEditing properties;
        private readonly ClipEditing clips;
        private readonly IEditorClock clock;
        private readonly IRecoveryStore recovery;
        private readonly DocumentPlayback playback;
        private readonly DocumentSampler player;
        private readonly DocumentNavigation navigation;
        private bool disposed;
        public EditorComposition(DocumentSession session, JsonDocumentRepository repository = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            repository ??= new JsonDocumentRepository();
            clock = new Infrastructure.Preview.EditorClock(); recovery = new Infrastructure.Documents.RecoveryStore();
            lifecycle = DocumentComposition.Lifecycle(session, repository);
            autoSave = new DocumentAutoSave(session, lifecycle, clock);
            player = new DocumentSampler(() => session.Document, session: session);
            playback = new DocumentPlayback(player);
            selection = new SelectionState(session); clipboard = new ClipboardState();
            structure = new StructureEditing(session, () => selection.Items); nodes = new NodeEditing(session);
            clipboardEditing = new ClipboardEditing(session, structure, clipboard, new FragmentCodec(), item => selection.Set(new[] { item }, true));
            properties = new PropertyEditing(session, new PropertyValueFactory()); clips = new ClipEditing(session);
            navigation = new DocumentNavigation(session, lifecycle, repository, playback);
        }
        public WindowBinding CreateBinding(object owner, Action repaint, Action changed, ActionEditorViews views,
            SettingsSession settings, Func<bool, string> chooseSavePath = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EditorComposition));
            var documents = CreateDocuments(chooseSavePath);
            return new WindowBinding(owner, repaint, changed, views, documents,
                PlaybackComposition.Create(playback, player, clock, settings), CreateProperties(settings), settings,
                new EditorExtensionContext(new ExtensionDocumentPresenter(() => session.Document, () => player.CurrentTime).Capture),
                () => navigation.Source != null ? navigation.Source.name : string.Empty, () => autoSave.LastSaved,
                (workspace, view) => TimelineComposition.Create(session, selection, structure, nodes, clipboardEditing, clips, clipboard,
                    player, workspace, view, settings, documents),
                items => selection.Set(items, false), () => playback.Stop());
        }
        public DocumentPresenter CreateDocuments(Func<bool, string> chooseSavePath = null) => DocumentPresenterComposition.Create(
            session, lifecycle, navigation, () => navigation.Source != null ? navigation.Source.name : "Action", autoSave.RecordSaved, chooseSavePath);
        public PropertyPresenter CreateProperties(SettingsSession settings) => PropertyComposition.Create(properties, nodes, clips, selection, settings);
        public EditorUpdateDriver CreateDriver(ActionEditorViews views, SettingsSession settings, Action<Exception> failed)
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
            finally { try { player.Dispose(); } finally { selection.Dispose(); session.Dispose(); } }
        }
    }
}
