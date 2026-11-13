using NBC.ActionEditor.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Storage;
using UnityEngine;

namespace NBC.ActionEditor
{
    /// <summary>The one shared document and authoring state. Window adapters own GUI and focus.</summary>
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
        private void OnSessionChanged() => Changed?.Invoke();

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

    public sealed class ActionEditorViews
    {
        private sealed class View
        {
            public Action Repaint, Changed, Tick;
        }
        private readonly Dictionary<object, View> views = new Dictionary<object, View>();
        public int Count => views.Count;
        public void Attach(object owner, Action repaint, Action changed)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            views[owner] = new View { Repaint = repaint, Changed = changed };
        }
        public void Detach(object owner)
        {
            if (owner == null) return;
            views.Remove(owner);
        }
        // 窗口更新由唯一宿主驱动，解除注册后快照内的旧回调也不能执行。
        public void SetTick(object owner, Action tick)
        {
            if (!views.TryGetValue(owner, out var view)) throw new InvalidOperationException("窗口尚未绑定。");
            view.Tick = tick;
        }
        public void Tick(Action<Exception> failed)
        {
            foreach (var entry in views.ToArray())
            {
                if (!views.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry.Value)) continue;
                try { current.Tick?.Invoke(); }
                catch (Exception error) { failed(error); }
            }
        }
        public void Repaint() { foreach (var view in views.Values.ToArray()) view.Repaint?.Invoke(); }
        public void NotifyChanged() { foreach (var view in views.Values.ToArray()) view.Changed?.Invoke(); }
    }
}
