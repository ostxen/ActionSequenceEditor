using System;
using System.IO;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Application.Ports;

namespace NBC.ActionEditor.Application.Sequences
{
    // 共享作者图、保存基线与事务，所有宿主能力由显式端口注入。
    public sealed class SequenceSession : IDisposable
    {
        private readonly ISequenceSnapshot snapshots;
        private readonly ISequenceRepository repository;
        private readonly IUndoHistory history;
        private readonly IRecoveryCodec recovery;
        private readonly object commandOwner = new object();
        private SequenceEditTransaction transaction;
        private string editStartSnapshot, savedContentKey, savedSnapshot;
        private byte[] revision;
        private bool disposed;
        public ActionSequence Sequence { get; private set; }
        public string Path { get; private set; }
        public long Version { get; private set; }
        public long CommitVersion { get; private set; }
        public bool IsDirty => Sequence != null && snapshots.GetContentKey(Sequence) != savedContentKey;
        public bool IsEditing => transaction != null;
        public event Action Changed;
        public event Action ContentChanged;
        public string NotificationWarning { get; private set; }

        public SequenceSession(ISequenceSnapshot snapshots, ISequenceRepository repository, IUndoHistory history, IRecoveryCodec recovery)
        {
            this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.history = history ?? throw new ArgumentNullException(nameof(history));
            this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            history.Restored += OnRestored;
        }

        // 全部候选与基线先完成，再替换当前图；失败不影响现有会话。
        public void Open(SequenceOpenData source)
        {
            ThrowIfDisposed();
            var candidate = source?.Sequence;
            var snapshot = candidate == null ? null : snapshots.Capture(candidate);
            var contentKey = candidate == null ? null : snapshots.GetContentKey(candidate);
            var path = source?.Path;
            if (!string.IsNullOrEmpty(path)) path = repository.ValidatePath(path);
            history.Reset(snapshot);
            transaction = null; editStartSnapshot = null; Sequence = candidate; Path = path;
            revision = source?.Revision; savedContentKey = contentKey; savedSnapshot = snapshot;
            Notify();
        }

        public string CaptureRecovery()
        {
            ThrowIfDisposed();
            return Sequence == null ? "" : recovery.Encode(new SequenceRecovery
            {
                Version = 2, FormatId = "actioneditor.recovery.v2", Json = snapshots.Capture(Sequence), Path = Path,
                Saved = savedContentKey, SavedJson = savedSnapshot, Revision = revision == null ? null : Convert.ToBase64String(revision)
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
            if (snapshots.GetContentKey(baseline) != data.Saved) throw new InvalidDataException("Inconsistent recovery baseline.");
            var path = string.IsNullOrEmpty(data.Path) ? null : repository.ValidatePath(data.Path);
            var bytes = string.IsNullOrEmpty(data.Revision) ? null : Convert.FromBase64String(data.Revision);
            var snapshot = snapshots.Capture(candidate);
            history.Reset(snapshot);
            transaction = null; editStartSnapshot = null; Sequence = candidate; Path = path;
            savedContentKey = data.Saved; savedSnapshot = data.SavedJson; revision = bytes;
            Notify();
        }

        public void DiscardChanges()
        {
            ThrowIfDisposed();
            if (Sequence == null) return;
            var candidate = snapshots.Restore(savedSnapshot);
            history.Reset(snapshots.Capture(candidate));
            transaction = null; editStartSnapshot = null; Sequence = candidate; Notify();
        }

        public void BeginEdit() => BeginEdit(commandOwner);
        public SequenceEditTransaction BeginEdit(object owner)
        {
            ThrowIfDisposed();
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (Sequence == null) return null;
            if (transaction != null) return ReferenceEquals(transaction.Owner, owner) ? transaction : null;
            editStartSnapshot = snapshots.Capture(Sequence);
            return transaction = new SequenceEditTransaction(this, owner);
        }
        internal bool Owns(SequenceEditTransaction token) => !disposed && token != null && ReferenceEquals(transaction, token);
        public void EndEdit(string label = "Edit document")
        {
            if (transaction != null && ReferenceEquals(transaction.Owner, commandOwner)) Commit(transaction, label);
        }
        internal void Commit(SequenceEditTransaction token, string label)
        {
            if (!Owns(token)) return;
            var before = editStartSnapshot;
            string after;
            bool changed;
            try
            {
                snapshots.ValidateAndInitialize(Sequence);
                after = snapshots.Capture(Sequence);
                changed = snapshots.GetContentKey(snapshots.Restore(before)) != snapshots.GetContentKey(Sequence);
                if (changed) history.Record(before, after, label);
            }
            catch { Cancel(token); throw; }
            transaction = null; editStartSnapshot = null;
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
        internal void Cancel(SequenceEditTransaction token)
        {
            if (!Owns(token)) return;
            var candidate = snapshots.Restore(editStartSnapshot);
            transaction = null; editStartSnapshot = null; Sequence = candidate; Notify();
        }
        private void OnRestored(string snapshot)
        {
            if (disposed || string.IsNullOrEmpty(snapshot)) return;
            var candidate = snapshots.Restore(snapshot);
            transaction = null; editStartSnapshot = null; Sequence = candidate; Notify();
        }

        public void Save(string target = null)
        {
            ThrowIfDisposed();
            if (Sequence == null) throw new InvalidOperationException("没有打开的文档。");
            if (IsEditing) throw new InvalidOperationException("请先完成或取消当前编辑，再保存。");
            target = repository.ValidatePath(target ?? Path);
            var same = string.Equals(target, Path, StringComparison.OrdinalIgnoreCase);
            var committed = repository.Commit(target, Sequence, same ? revision : null);
            // 先推进已提交基线再导入，导入失败不可让下一次误覆盖未知字节。
            CommitVersion++;
            Path = target; revision = committed.Revision; savedContentKey = committed.ContentKey; savedSnapshot = committed.Snapshot;
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
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(SequenceSession)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; history.Restored -= OnRestored; history.Dispose();
            transaction = null; editStartSnapshot = null; Sequence = null;
            Changed = null; ContentChanged = null;
        }
    }

    // 已解析来源由宿主提供，会话不读取 Unity 资源或文件。
    public sealed class SequenceOpenData
    {
        public ActionSequence Sequence { get; }
        public string Path { get; }
        private readonly byte[] revision;
        public byte[] Revision => revision == null ? null : (byte[])revision.Clone();
        public SequenceOpenData(ActionSequence sequence, string path = null, byte[] revision = null)
        { Sequence = sequence; Path = path; this.revision = revision == null ? null : (byte[])revision.Clone(); }
    }
}
