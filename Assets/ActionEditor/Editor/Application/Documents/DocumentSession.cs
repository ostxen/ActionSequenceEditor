using System;
using System.IO;
using NBC.ActionEditor.Domain;
using NBC.ActionEditor.Application.Ports;

namespace NBC.ActionEditor.Application.Documents
{
    // 共享作者图、保存基线与事务，所有宿主能力由显式端口注入。
    public sealed class DocumentSession : IDisposable
    {
        private readonly IDocumentSnapshot snapshots;
        private readonly IDocumentRepository repository;
        private readonly IUndoHistory history;
        private readonly IRecoveryCodec recovery;
        private readonly object commandOwner = new object();
        private DocumentEditTransaction transaction;
        private string pending, saved, savedSnapshot;
        private byte[] revision;
        private bool disposed;
        public ActionDocument Document { get; private set; }
        public string Path { get; private set; }
        public long Version { get; private set; }
        public long CommitVersion { get; private set; }
        public bool IsDirty => Document != null && snapshots.Content(Document) != saved;
        public bool IsEditing => transaction != null;
        public event Action Changed;
        public event Action ContentChanged;
        public string NotificationWarning { get; private set; }

        public DocumentSession(IDocumentSnapshot snapshots, IDocumentRepository repository, IUndoHistory history, IRecoveryCodec recovery)
        {
            this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.history = history ?? throw new ArgumentNullException(nameof(history));
            this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            history.Restored += OnRestored;
        }

        // 全部候选与基线先完成，再替换当前图；失败不影响现有会话。
        public void Open(DocumentOpenData source)
        {
            ThrowIfDisposed();
            var candidate = source?.Document;
            var encoded = candidate == null ? null : snapshots.Capture(candidate);
            var content = candidate == null ? null : snapshots.Content(candidate);
            var path = source?.Path;
            if (!string.IsNullOrEmpty(path)) path = repository.ValidatePath(path);
            history.Reset(encoded);
            transaction = null; pending = null; Document = candidate; Path = path;
            revision = source?.Revision; saved = content; savedSnapshot = encoded;
            Notify();
        }

        public string CaptureRecovery()
        {
            ThrowIfDisposed();
            return Document == null ? "" : recovery.Encode(new DocumentRecovery
            {
                Version = 2, FormatId = "actioneditor.recovery.v2", Json = snapshots.Capture(Document), Path = Path,
                Saved = saved, SavedJson = savedSnapshot, Revision = revision == null ? null : Convert.ToBase64String(revision)
            });
        }

        public void RestoreRecovery(string text)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text)) { Open(null); return; }
            var data = recovery.Decode(text);
            if (data == null || data.Version != 2 || data.FormatId != "actioneditor.recovery.v2" || data.SavedJson == null || data.Saved == null)
                throw new InvalidDataException("Unsupported recovery contract.");
            var candidate = snapshots.Restore(data.Json);
            var baseline = snapshots.Restore(data.SavedJson);
            if (snapshots.Content(baseline) != data.Saved) throw new InvalidDataException("Inconsistent recovery baseline.");
            var path = string.IsNullOrEmpty(data.Path) ? null : repository.ValidatePath(data.Path);
            var bytes = string.IsNullOrEmpty(data.Revision) ? null : Convert.FromBase64String(data.Revision);
            var encoded = snapshots.Capture(candidate);
            history.Reset(encoded);
            transaction = null; pending = null; Document = candidate; Path = path;
            saved = data.Saved; savedSnapshot = data.SavedJson; revision = bytes;
            Notify();
        }

        public void DiscardChanges()
        {
            ThrowIfDisposed();
            if (Document == null) return;
            var candidate = snapshots.Restore(savedSnapshot);
            history.Reset(snapshots.Capture(candidate));
            transaction = null; pending = null; Document = candidate; Notify();
        }

        public void BeginEdit() => BeginEdit(commandOwner);
        public DocumentEditTransaction BeginEdit(object owner)
        {
            ThrowIfDisposed();
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (Document == null) return null;
            if (transaction != null) return ReferenceEquals(transaction.Owner, owner) ? transaction : null;
            pending = snapshots.Capture(Document);
            return transaction = new DocumentEditTransaction(this, owner);
        }
        internal bool Owns(DocumentEditTransaction token) => !disposed && token != null && ReferenceEquals(transaction, token);
        public void EndEdit(string label = "Edit document")
        {
            if (transaction != null && ReferenceEquals(transaction.Owner, commandOwner)) Commit(transaction, label);
        }
        internal void Commit(DocumentEditTransaction token, string label)
        {
            if (!Owns(token)) return;
            var before = pending;
            string after;
            bool changed;
            try
            {
                snapshots.ValidateAndInitialize(Document);
                after = snapshots.Capture(Document);
                changed = snapshots.Content(snapshots.Restore(before)) != snapshots.Content(Document);
                if (changed) history.Record(before, after, label);
            }
            catch { Cancel(token); throw; }
            transaction = null; pending = null;
            if (changed) Notify();
        }
        public void Edit(string label, Action action)
        {
            ThrowIfDisposed();
            if (action == null) throw new ArgumentNullException(nameof(action));
            var owns = transaction == null;
            var token = owns ? BeginEdit(commandOwner) : transaction;
            if (token == null) throw new InvalidOperationException("没有打开的文档。");
            try { action(); if (owns) token.Commit(label); }
            catch { token.Cancel(); throw; }
        }
        public void CancelEdit()
        {
            if (transaction != null && ReferenceEquals(transaction.Owner, commandOwner)) Cancel(transaction);
        }
        internal void Cancel(DocumentEditTransaction token)
        {
            if (!Owns(token)) return;
            var candidate = snapshots.Restore(pending);
            transaction = null; pending = null; Document = candidate; Notify();
        }
        private void OnRestored(string snapshot)
        {
            if (disposed || string.IsNullOrEmpty(snapshot)) return;
            var candidate = snapshots.Restore(snapshot);
            transaction = null; pending = null; Document = candidate; Notify();
        }

        public void Save(string target = null)
        {
            ThrowIfDisposed();
            if (Document == null) throw new InvalidOperationException("没有打开的文档。");
            if (IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再保存。");
            target = repository.ValidatePath(target ?? Path);
            var same = string.Equals(target, Path, StringComparison.OrdinalIgnoreCase);
            var committed = repository.Commit(target, Document, same ? revision : null);
            // 先推进已提交基线再导入，导入失败不可让下一次误覆盖未知字节。
            CommitVersion++;
            Path = target; revision = committed.Revision; saved = committed.Content; savedSnapshot = committed.Snapshot;
            try { repository.Import(target); }
            finally { Notify(false); }
        }
        // 保存只发布会话状态，不能因刷新通知释放正在预览的资源或选择。
        private void Notify(bool content = true)
        {
            Version++;
            var warnings = new System.Collections.Generic.List<string>();
            if (content) Publish(ContentChanged);
            Publish(Changed);
            NotificationWarning = warnings.Count == 0 ? null : string.Join("; ", warnings);
            void Publish(Action listeners)
            {
                foreach (var listener in listeners?.GetInvocationList() ?? Array.Empty<Delegate>())
                    try { ((Action)listener)(); } catch (Exception error) { warnings.Add(error.Message); }
            }
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(DocumentSession)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; history.Restored -= OnRestored; history.Dispose();
            transaction = null; pending = null; Document = null;
            Changed = null; ContentChanged = null;
        }
    }

    // 已解析来源由宿主提供，会话不读取 Unity 资源或文件。
    public sealed class DocumentOpenData
    {
        public ActionDocument Document { get; }
        public string Path { get; }
        private readonly byte[] revision;
        public byte[] Revision => revision == null ? null : (byte[])revision.Clone();
        public DocumentOpenData(ActionDocument document, string path = null, byte[] revision = null)
        { Document = document; Path = path; this.revision = revision == null ? null : (byte[])revision.Clone(); }
    }
}
