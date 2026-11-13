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
        public TextAsset Source => string.IsNullOrEmpty(Session.Path) ? source : repository.Load(Session.Path);
        public ActionEditorSelection Selection { get; } = new ActionEditorSelection();
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

        public ActionEditorContext(NBC.ActionEditor.Application.Documents.DocumentSession session,
            NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository repository = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Clock = new NBC.ActionEditor.Infrastructure.EditorClock();
            Recovery = new NBC.ActionEditor.Infrastructure.RecoveryStore();
            this.repository = repository ?? new NBC.ActionEditor.Infrastructure.Documents.JsonDocumentRepository();
            Lifecycle = NBC.ActionEditor.Composition.DocumentComposition.Lifecycle(session, this.repository);
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
            Session.ContentChanged -= OnDocumentChanged;
            Session.Changed -= OnSessionChanged;
            Playback.Stop(false);
            Session.Dispose();
        }
    }

    public sealed class ActionEditorSelection
    {
        private readonly List<IDirectable> items = new List<IDirectable>();
        public IDirectable[] Items => items.ToArray();
        public int Count => items.Count;
        public IDirectable First => items.Count == 0 ? null : items[0];
        public bool CanMultipleSelect { get; set; }
        public bool Contains(IDirectable item) => items.Contains(item);
        public IDirectable[] ClickCandidate(IDirectable item, bool add, bool retainSelected)
        {
            if (item == null) return Array.Empty<IDirectable>();
            if (retainSelected && items.Contains(item)) return Items;
            return add ? items.Concat(new[] { item }).Distinct().ToArray() : new[] { item };
        }

        public event Action InspectorRequested;

        public bool Set(IDirectable[] selected, bool allowMultiple)
        {
            var next = selected?.Where(item => item != null).ToArray() ?? Array.Empty<IDirectable>();
            var changed = next.Length != items.Count || next.Any(item => !items.Contains(item));
            if (allowMultiple && !changed) return false;
            items.Clear(); items.AddRange(next);
            CanMultipleSelect = allowMultiple && items.Count == 1 && items[0] is not TimelineClip;
            if (allowMultiple && changed && selected != null) InspectorRequested?.Invoke();
            return changed;
        }

        internal void Retain(ActionDocument document)
        {
            items.RemoveAll(item => document == null || !document.directables.Contains(item));
            if (items.Count != 1 || items[0] is TimelineClip) CanMultipleSelect = false;
        }
    }

    public sealed class ActionEditorViews
    {
        private sealed class View
        {
            public Action Repaint, Changed;
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
        public void Repaint() { foreach (var view in views.Values.ToArray()) view.Repaint?.Invoke(); }
        public void NotifyChanged() { foreach (var view in views.Values.ToArray()) view.Changed?.Invoke(); }
    }
}
