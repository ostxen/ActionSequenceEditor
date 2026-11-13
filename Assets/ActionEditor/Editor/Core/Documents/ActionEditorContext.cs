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
        public JsonDocumentSession Session { get; }
        public ActionDocument Document => Session.Document;
        private TextAsset source;
        public TextAsset Source => string.IsNullOrEmpty(Session.Path) ? source : Session.Source;
        public ActionEditorSelection Selection { get; } = new ActionEditorSelection();
        public ActionEditorClipboard Clipboard { get; } = new ActionEditorClipboard();
        public ActionEditorPlayback Playback { get; }
        public AssetPlayer Player => Playback.Player;
        public event Action Changed;

        public ActionEditorContext(JsonDocumentSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Playback = new ActionEditorPlayback(new AssetPlayer(() => Document));
            Session.Changed += OnDocumentChanged;
        }

        public void Open(TextAsset source)
        {
            Session.Open(source);
            this.source = source;
            Playback.Stop(false);
        }

        internal void RestoreSource(TextAsset source) => this.source = source;

        private void OnDocumentChanged()
        {
            Player.Reset();
            Selection.Retain(Document);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Session.Changed -= OnDocumentChanged;
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

    public sealed class ActionEditorClipboard
    {
        public string Json { get; internal set; }
        public Type Type { get; internal set; }
        public IDirectable Item { get; set; }
        public IDirectable CutSource { get; internal set; }
        public bool IsCut { get; set; }
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
