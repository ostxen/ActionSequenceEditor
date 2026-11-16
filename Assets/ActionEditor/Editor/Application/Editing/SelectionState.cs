using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Model;

namespace NBC.ActionEditor.Application.Editing
{
    // 选择归共享作者会话，捕获和悬停不在这里保存。
    public sealed class SelectionState : IDisposable
    {
        private readonly Sequences.SequenceSession session;
        public SelectionState(Sequences.SequenceSession session = null)
        {
            this.session = session;
            if (session != null) session.ContentChanged += OnContentChanged;
        }
        private void OnContentChanged() => Retain(session.Sequence);
        public void Dispose() { if (session != null) session.ContentChanged -= OnContentChanged; }
        private readonly List<IDirectable> items = new List<IDirectable>();
        public IDirectable[] Items => items.ToArray();
        public int Count => items.Count;
        public long Version { get; private set; }
        public IDirectable First => items.Count == 0 ? null : items[0];
        public bool CanMultipleSelect { get; set; }
        public event Action InspectorRequested;
        public event Action Changed;
        public bool Contains(IDirectable item) => items.Contains(item);
        public IDirectable[] ClickCandidate(IDirectable item, bool add, bool retainSelected)
        {
            if (item == null) return Array.Empty<IDirectable>();
            if (retainSelected && items.Contains(item)) return Items;
            return add ? items.Concat(new[] { item }).Distinct().ToArray() : new[] { item };
        }
        public bool Set(IDirectable[] selected, bool allowMultiple)
        {
            var next = selected?.Where(item => item != null).Distinct().ToArray() ?? Array.Empty<IDirectable>();
            var changed = !next.SequenceEqual(items);
            if (allowMultiple && !changed) return false;
            items.Clear(); items.AddRange(next);
            CanMultipleSelect = allowMultiple && items.Count == 1 && items[0] is not ActionClip;
            if (changed) { Version++; Changed?.Invoke(); }
            if (allowMultiple && changed && selected != null) InspectorRequested?.Invoke();
            return changed;
        }
        public void Retain(ActionSequence document)
        {
            var removed = items.RemoveAll(item => document == null || !document.directables.Contains(item));
            if (items.Count != 1 || items[0] is ActionClip) CanMultipleSelect = false;
            if (removed != 0) { Version++; Changed?.Invoke(); }
        }
    }
}
