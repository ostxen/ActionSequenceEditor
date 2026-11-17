using System;
using ActionSequenceEditor.Model;

namespace ActionSequenceEditor.Application.Ports
{
    // 快照隔离和内容比较由格式适配器实现，会话不解释磁盘格式。
    public interface ISequenceSnapshot
    {
        string Capture(ActionSequence document);
        ActionSequence Restore(string snapshot);
        string GetContentKey(ActionSequence document);
        void ValidateAndInitialize(ActionSequence document);
    }

    // 将可靠字节提交与资源导入分离，使已提交状态不会因导入失败丢失。
    public interface ISequenceRepository
    {
        string ValidatePath(string path);
        ActionSequenceEditor.Application.Sequences.SequenceOpenData Read(string path);
        SequenceCommit Commit(string path, ActionSequence document, byte[] expectedRevision);
        void Import(string path);
    }

    // 独立导出不切换来源或改变会话、选择及捕获。
    public interface ISequenceExporter
    {
        void Export(ActionSequence document, string sourcePath, string target, string formatId);
    }

    // 撤销历史只传递隔离快照，不将 Unity 对象泄漏进应用层。
    public interface IUndoHistory : IDisposable
    {
        event Action<string> Restored;
        void Reset(string snapshot);
        void Record(string before, string after, string label);
    }

    // 恢复封套是宿主边界，不与作者磁盘格式或编辑逻辑耦合。
    public interface IRecoveryCodec
    {
        string Encode(SequenceRecovery recovery);
        SequenceRecovery Decode(string text);
    }

    // 提交结果携带经过校验的基线及精确落盘字节版本。
    public sealed class SequenceCommit
    {
        public string Snapshot { get; }
        public string ContentKey { get; }
        private readonly byte[] revision;
        public byte[] Revision => (byte[])revision.Clone();
        public SequenceCommit(string snapshot, string content, byte[] revision)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ContentKey = content ?? throw new ArgumentNullException(nameof(content));
            this.revision = (byte[])(revision ?? throw new ArgumentNullException(nameof(revision))).Clone();
        }
    }

    // 恢复保存当前图、保存基线和来源版本，不携带窗口工作区。
    [Serializable]
    public sealed class SequenceRecovery
    {
        public int Version;
        public string FormatId, Json, Path, Saved, SavedJson, Revision;
    }
}
