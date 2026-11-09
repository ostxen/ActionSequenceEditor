using System;
using System.Collections.Generic;
using NBC.ActionEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    public sealed class SessionSelection : ScriptableObject
    {
        public List<string> Ids = new();
    }

    [Serializable]
    public sealed class WorkspaceState
    {
        public double PixelsPerSecond = 100;
        public double ScrollTime;
        public float ScrollY;
        public float LibraryWidth = 210;
        public float InspectorWidth = 280;
        public double Playhead;
        public double SnapSeconds = 0.1;
        public List<string> CollapsedGroups = new();
    }

    public sealed class DocumentSession : IDisposable
    {
        private readonly IDocumentStore store;
        private string savedState;
        private byte[] revision;
        private bool disposed;
        public ActionDocument Document { get; private set; }
        public string Path { get; private set; }
        public WorkspaceState Workspace { get; } = new();
        private readonly SessionSelection selection = ScriptableObject.CreateInstance<SessionSelection>();
        internal SessionSelection SelectionObject => selection;
        public List<string> Selection => selection.Ids;
        internal bool TransactionActive;
        public bool IsDirty => Document != null && EditorJsonUtility.ToJson(Document) != savedState;
        public double LastSaveTime { get; private set; }
        public event Action Changed;

        public DocumentSession(IDocumentStore store = null)
        {
            this.store = store ?? new DocumentStore();
            selection.hideFlags = HideFlags.HideAndDontSave;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public void New()
        {
            EnsureCanSwitch();
            var candidate = ScriptableObject.CreateInstance<ActionDocument>();
            candidate.hideFlags = HideFlags.HideAndDontSave;
            Replace(candidate, null, null, false);
        }

        public void Open(string path)
        {
            EnsureCanSwitch();
            ActionDocument candidate = null;
            try
            {
                candidate = store.Load(path);
                var candidateRevision = store.ReadRevision(path);
                Replace(candidate, path, candidateRevision, true);
                candidate = null;
            }
            finally { if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate); }
        }

        public void Save(string newPath = null)
        {
            EnsureAlive();
            if (TransactionActive) throw new InvalidOperationException("Finish or cancel the active edit before saving.");
            if (Document == null) throw new InvalidOperationException("No document.");
            var target = newPath ?? Path;
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Choose a document target.");
            store.Save(target, Document, target == Path ? revision : null);
            var nextRevision = store.ReadRevision(target);
            Path = target;
            revision = nextRevision;
            savedState = EditorJsonUtility.ToJson(Document);
            LastSaveTime = EditorApplication.timeSinceStartup;
            NotifyChanged();
        }

        public void DiscardAndClose()
        {
            EnsureAlive();
            if (TransactionActive) throw new InvalidOperationException("Finish or cancel the active edit before closing.");
            Release();
            NotifyChanged();
        }

        public bool TryAutoSave(double now, double interval)
        {
            EnsureAlive();
            if (Document == null || string.IsNullOrEmpty(Path) || !IsDirty || !AutoSaveSchedule.IsDue(LastSaveTime, now, interval)) return false;
            Save();
            LastSaveTime = now;
            return true;
        }

        public void NotifyChanged() { EnsureAlive(); Changed?.Invoke(); }

        private void Replace(ActionDocument candidate, string path, byte[] nextRevision, bool clean)
        {
            Release();
            Document = candidate;
            Path = path;
            revision = nextRevision;
            savedState = clean ? EditorJsonUtility.ToJson(candidate) : null;
            LastSaveTime = EditorApplication.timeSinceStartup;
            Workspace.Playhead = 0;
            NotifyChanged();
        }

        private void Release()
        {
            if (Document != null)
            {
                Undo.ClearUndo(Document);
                UnityEngine.Object.DestroyImmediate(Document);
            }
            Document = null;
            Path = null;
            savedState = null;
            revision = null;
            Selection.Clear();
            Undo.ClearUndo(selection);
        }

        private void EnsureCanSwitch()
        {
            EnsureAlive();
            if (TransactionActive) throw new InvalidOperationException("Finish or cancel the active edit first.");
            if (IsDirty) throw new InvalidOperationException("Save or explicitly discard the current document first.");
        }
        private void EnsureAlive() { if (disposed) throw new ObjectDisposedException(nameof(DocumentSession)); }
        private void OnUndoRedo() { if (!disposed) Changed?.Invoke(); }
        public void Dispose()
        {
            if (disposed) return;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Release();
            Undo.ClearUndo(selection);
            UnityEngine.Object.DestroyImmediate(selection);
            Changed = null;
            disposed = true;
        }
    }
}
