using System;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Storage
{
    public sealed class JsonUndoState : ScriptableObject
    {
        [TextArea] public string Json;
    }

    public sealed class JsonEditTransaction : IDisposable
    {
        private readonly JsonDocumentSession session;
        internal object Owner { get; }
        internal JsonEditTransaction(JsonDocumentSession session, object owner) { this.session = session; Owner = owner; }
        public bool IsActive => session.Owns(this);
        public bool Update(Action action)
        {
            if (!IsActive) return false;
            try { action(); return true; }
            catch { Cancel(); throw; }
        }
        public void Commit(string label = "Edit ActionEditor document") => session.EndEdit(this, label);
        public void Cancel() => session.CancelEdit(this);
        public void Dispose() => Cancel();
    }

    /// <summary>One document shared by both renderers. Undo owns serialized snapshots, not Inspector proxies.</summary>
    public sealed class JsonDocumentSession : IDisposable
    {
        public Asset Document { get; private set; }
        public string Path { get; private set; }
        public bool IsDirty => Document != null && Content(Document) != saved;
        public event Action Changed;
        public bool IsEditing => pending != null;
        [Serializable] private sealed class Recovery
        {
            public string Json, Path, Saved, SavedJson, Revision;
        }
        public string CaptureRecovery() => Document == null ? "" : JsonUtility.ToJson(new Recovery
        {
            Json = Encode(Document), Path = Path, Saved = saved, SavedJson = savedJson, Revision = revision == null ? null : Convert.ToBase64String(revision)
        });
        public void RestoreRecovery(string json)
        {
            if (string.IsNullOrEmpty(json)) { Open(null); return; }
            var data = JsonUtility.FromJson<Recovery>(json);
            var document = Decode(data.Json);
            var bytes = string.IsNullOrEmpty(data.Revision) ? null : Convert.FromBase64String(data.Revision);
            ReleaseState(); Document = document; Path = data.Path; saved = data.Saved; savedJson = data.SavedJson ?? data.Saved; revision = bytes; pending = null;
            state = ScriptableObject.CreateInstance<JsonUndoState>(); state.hideFlags = HideFlags.HideAndDontSave; state.Json = Encode(document);
            Changed?.Invoke();
        }
        private JsonUndoState state;
        private string saved;
        private string savedJson;
        private byte[] revision;
        private readonly JsonDocumentStore store = new JsonDocumentStore();
        private string pending;
        private JsonEditTransaction transaction;
        private readonly object legacyOwner = new object();
        public JsonDocumentSession() { Undo.undoRedoPerformed += UndoRedo; }

        public static string Encode(Asset asset) => JsonDocumentCodec.Encode(asset);
        public static Asset Decode(string json) => JsonDocumentCodec.Decode(json);
        public static void RequireValid(Asset asset) => JsonDocumentCodec.RequireValid(asset);

        public void Open(TextAsset text)
        {
            var candidate = text == null ? null : Decode(text.text);
            var path = store.GetPath(text);
            var bytes = store.ReadRevision(path);
            var encoded = candidate == null ? null : Encode(candidate);
            var content = candidate == null ? null : JsonDocumentCodec.Normalize(encoded);
            ReleaseState();
            Document = candidate; Path = path; revision = bytes; pending = null;
            if (candidate != null)
            {
                state = ScriptableObject.CreateInstance<JsonUndoState>(); state.hideFlags = HideFlags.HideAndDontSave;
                state.Json = encoded; saved = content; savedJson = encoded;
            }
            else { saved = null; savedJson = null; }
            Changed?.Invoke();
        }

        public void DiscardChanges()
        {
            if (Document == null) return;
            var candidate = Decode(savedJson);
            var encoded = Encode(candidate);
            ReleaseState();
            Document = candidate; pending = null;
            state = ScriptableObject.CreateInstance<JsonUndoState>(); state.hideFlags = HideFlags.HideAndDontSave; state.Json = encoded;
            Changed?.Invoke();
        }

        public void BeginEdit() => BeginEdit(legacyOwner);
        public JsonEditTransaction BeginEdit(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (Document == null) return null;
            if (transaction != null) return ReferenceEquals(transaction.Owner, owner) ? transaction : null;
            pending = Encode(Document);
            return transaction = new JsonEditTransaction(this, owner);
        }
        internal bool Owns(JsonEditTransaction token) => token != null && ReferenceEquals(transaction, token) && pending != null;
        public void EndEdit(string label = "Edit ActionEditor document")
        {
            if (transaction != null && ReferenceEquals(transaction.Owner, legacyOwner)) EndEdit(transaction, label);
        }
        internal void EndEdit(JsonEditTransaction token, string label)
        {
            if (!Owns(token) || Document == null) return;
            var before = pending;
            string after;
            try { RequireValid(Document); Document.Init(); after = Encode(Document); }
            catch { CancelEdit(token); throw; }
            pending = null; transaction = null;
            if (JsonDocumentCodec.Normalize(before) == JsonDocumentCodec.Normalize(after)) return;
            state.Json = before;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, label);
            state.Json = after;
            EditorUtility.SetDirty(state);
            Undo.CollapseUndoOperations(group);
            Changed?.Invoke();
        }
        public void Edit(string label, Action action)
        {
            var ownsEdit = !IsEditing;
            var token = ownsEdit ? BeginEdit(legacyOwner) : transaction;
            try { action(); if (ownsEdit) token?.Commit(label); }
            catch { token?.Cancel(); throw; }
        }
        public void CancelEdit()
        {
            if (transaction != null && ReferenceEquals(transaction.Owner, legacyOwner)) CancelEdit(transaction);
        }
        internal void CancelEdit(JsonEditTransaction token)
        {
            if (!Owns(token)) return;
            var candidate = Decode(pending);
            pending = null; transaction = null; Document = candidate; Changed?.Invoke();
        }
        private void UndoRedo()
        {
            if (state == null || string.IsNullOrEmpty(state.Json)) return;
            Document = Decode(state.Json); pending = null; transaction = null; Changed?.Invoke();
        }

        public void Save(string target = null)
        {
            if (Document == null) throw new InvalidOperationException("没有打开的文档。");
            target = ValidatePath(target ?? Path);
            var same = string.Equals(target, Path, StringComparison.OrdinalIgnoreCase);
            var committed = store.Commit(target, Document, same ? revision : null);
            // Advance the baseline before import: committed bytes must stay known if import fails.
            Path = target; revision = committed.Revision; saved = Content(Document); savedJson = committed.Json;
            store.Import(target);
            Changed?.Invoke();
        }
        public string CreateSaveSnapshot() => JsonDocumentCodec.CreateSaveSnapshot(Document);
        public static string ValidatePath(string path) => JsonDocumentStore.ValidatePath(path);
        private void ReleaseState() { transaction = null; pending = null; if (state != null) { Undo.ClearUndo(state); UnityEngine.Object.DestroyImmediate(state); } state = null; }
        private static string Content(Asset asset) => JsonDocumentCodec.Content(asset);
        public string[] InspectReadbackDifferences() => JsonDocumentCodec.InspectReadbackDifferences(Document);
        public void Dispose() { Undo.undoRedoPerformed -= UndoRedo; ReleaseState(); Document = null; }
    }
}
