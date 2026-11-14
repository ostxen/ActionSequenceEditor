using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 持有共享作者依赖；窗口工作区与捕获由各自绑定拥有。
    public sealed class ActionEditorContext : IDisposable
    {
        public NBC.ActionEditor.Application.Documents.DocumentSession Session { get; }
        public ActionDocument Document => Session.Document;
        private TextAsset source;
        private readonly NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository repository;
        public NBC.ActionEditor.Application.Documents.DocumentLifecycle Lifecycle { get; }
        public NBC.ActionEditor.Application.Documents.DocumentAutoSave AutoSave { get; }
        public TextAsset Source => string.IsNullOrEmpty(Session.Path) ? source : repository.Load(Session.Path);
        public NBC.ActionEditor.Application.Editing.SelectionState Selection { get; } = new NBC.ActionEditor.Application.Editing.SelectionState();
        public NBC.ActionEditor.Application.Editing.ClipboardState Clipboard { get; } = new NBC.ActionEditor.Application.Editing.ClipboardState();
        public NBC.ActionEditor.Application.Editing.StructureEditing Structure { get; }
        public NBC.ActionEditor.Application.Editing.ClipboardEditing ClipboardEditing { get; }
        public NBC.ActionEditor.Application.Editing.PropertyEditing Properties { get; }
        public NBC.ActionEditor.Application.Editing.ClipEditing Clips { get; }
        public NBC.ActionEditor.Application.Ports.IEditorClock Clock { get; }
        public NBC.ActionEditor.Application.Ports.IRecoveryStore Recovery { get; }
        public ActionEditorPlayback Playback { get; }
        public AssetPlayer Player => Playback.Player;
        public event Action Changed;
        private bool disposed;

        public ActionEditorContext(NBC.ActionEditor.Application.Documents.DocumentSession session,
            NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository repository = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Clock = new NBC.ActionEditor.Infrastructure.EditorClock();
            Recovery = new NBC.ActionEditor.Infrastructure.RecoveryStore();
            this.repository = repository ?? new NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository();
            Lifecycle = NBC.ActionEditor.Composition.DocumentComposition.Lifecycle(session, this.repository);
            AutoSave = new NBC.ActionEditor.Application.Documents.DocumentAutoSave(session, Lifecycle, Clock);
            Structure = new NBC.ActionEditor.Application.Editing.StructureEditing(session, () => Selection.Items);
            ClipboardEditing = new NBC.ActionEditor.Application.Editing.ClipboardEditing(session, Structure, Clipboard,
                new NBC.ActionEditor.Infrastructure.Documents.FragmentCodec(), item => Selection.Set(new[] { item }, true));
            Properties = new NBC.ActionEditor.Application.Editing.PropertyEditing(session, new NBC.ActionEditor.Infrastructure.Documents.PropertyValueFactory());
            Clips = new NBC.ActionEditor.Application.Editing.ClipEditing(session);
            Playback = new ActionEditorPlayback(new AssetPlayer(() => Document));
            Session.ContentChanged += OnDocumentChanged;
            Session.Changed += OnSessionChanged;
        }

        public void Open(TextAsset source)
        {
            var result = Lifecycle.Open(repository.ReadSource(source));
            if (!result.Succeeded) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Exception).Throw();
            this.source = source;
            Playback.Stop(false);
        }

        internal void RestoreSource(TextAsset source) => this.source = source;

        private void OnDocumentChanged()
        {
            Player.Reset();
            Selection.Retain(Document);
        }
        // 每窗独立通知，汇总异常交给会话警告，避免首个坏视图阻断其余窗口。
        private void OnSessionChanged()
        {
            List<Exception> errors = null;
            foreach (var listener in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try { ((Action)listener)(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            }
            if (errors != null) throw new AggregateException("窗口状态通知失败。", errors);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Session.ContentChanged -= OnDocumentChanged;
            Session.Changed -= OnSessionChanged;
            try { Playback.Stop(false); }
            finally { Session.Dispose(); }
        }
    }
}
