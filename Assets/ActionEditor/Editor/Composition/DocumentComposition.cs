using NBC.ActionEditor.Infrastructure.Serialization;
using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Infrastructure.Documents;

namespace NBC.ActionEditor.Composition
{
    // 显式装配会话及其独占撤销桥，不提供全局服务查询。
    public static class DocumentComposition
    {
        public static DocumentSession Create(JsonDocumentRepository repository = null) => new DocumentSession(
            new SessionSnapshotCodec(), repository ?? new JsonDocumentRepository(), new UnityUndoHistory(), new RecoveryCodec());
        public static DocumentLifecycle Lifecycle(DocumentSession session, JsonDocumentRepository repository) =>
            new DocumentLifecycle(session, repository, new DocumentExportService(), new DocumentFactory());
    }
}
