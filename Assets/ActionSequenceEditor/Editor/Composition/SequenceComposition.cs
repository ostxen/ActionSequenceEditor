using ActionSequenceEditor.Infrastructure.Serialization;
using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Infrastructure.Sequences;

namespace ActionSequenceEditor.Composition
{
    // 显式装配会话及其独占撤销桥，不提供全局服务查询。
    public static class SequenceComposition
    {
        public static SequenceSession Create(SequenceFileRepository repository = null) => new SequenceSession(
            new SessionSnapshotCodec(), repository ?? new SequenceFileRepository(), new UnityUndoHistory(), new JsonRecoveryCodec());
        public static SequenceLifecycle Lifecycle(SequenceSession session, SequenceFileRepository repository) =>
            new SequenceLifecycle(session, repository, new SequenceExportService(), new SequenceFactory());
    }
}
