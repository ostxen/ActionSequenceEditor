using System;
using NBC.ActionEditor.Domain;

namespace NBC.ActionEditor.Application.Ports
{
    // 快照隔离和内容比较由格式适配器实现，会话不解释磁盘格式。
    public interface IDocumentSnapshot
    {
        string Capture(ActionDocument document);
        ActionDocument Restore(string snapshot);
        string Content(ActionDocument document);
        void ValidateAndInitialize(ActionDocument document);
    }

    // 将可靠字节提交与资源导入分离，使已提交状态不会因导入失败丢失。
    public interface IDocumentRepository
    {
        string ValidatePath(string path);
        NBC.ActionEditor.Application.Documents.DocumentOpenData Read(string path);
        DocumentCommit Commit(string path, ActionDocument document, byte[] expectedRevision);
        void Import(string path);
    }

    // 独立导出不切换来源或改变会话、选择及捕获。
    public interface IDocumentExporter
    {
        void Export(ActionDocument document, string sourcePath, string target, string formatId);
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
        string Encode(DocumentRecovery recovery);
        DocumentRecovery Decode(string text);
    }

    // 提交结果携带经过校验的基线及精确落盘字节版本。
    public sealed class DocumentCommit
    {
        public string Snapshot { get; }
        public string Content { get; }
        private readonly byte[] revision;
        public byte[] Revision => (byte[])revision.Clone();
        public DocumentCommit(string snapshot, string content, byte[] revision)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            this.revision = (byte[])(revision ?? throw new ArgumentNullException(nameof(revision))).Clone();
        }
    }

    // 恢复保存当前图、保存基线和来源版本，不携带窗口工作区。
    [Serializable]
    public sealed class DocumentRecovery
    {
        public int Version;
        public string FormatId, Json, Path, Saved, SavedJson, Revision;
    }
}
