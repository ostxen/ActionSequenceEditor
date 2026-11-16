using NBC.ActionEditor.Application.Ports;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Serialization;

namespace NBC.ActionEditor.Infrastructure.Serialization
{
    // 让会话使用受控隔离快照，不依赖具体 JSON 类型。
    public sealed class SessionSnapshotCodec : ISequenceSnapshot
    {
        public string Capture(ActionSequence sequence) => SequenceJsonCodec.Encode(sequence);
        public ActionSequence Restore(string snapshot) => SequenceJsonCodec.Decode(snapshot);
        public string GetContentKey(ActionSequence sequence) => SequenceJsonCodec.GetContentKey(sequence);
        public void ValidateAndInitialize(ActionSequence sequence) => SequenceGraphValidation.Initialize(sequence, SequenceFormats.Context);
    }
}
