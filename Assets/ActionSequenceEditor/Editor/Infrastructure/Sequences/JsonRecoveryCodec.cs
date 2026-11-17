using ActionSequenceEditor.Application.Sequences;
using ActionSequenceEditor.Application.Ports;
using UnityEngine;

namespace ActionSequenceEditor.Infrastructure.Sequences
{
    // 恢复封套单独验证版本，作者内容仍由快照端口解析。
    public sealed class JsonRecoveryCodec : IRecoveryCodec
    {
        public string Encode(SequenceRecovery recovery) => JsonUtility.ToJson(recovery);
        public SequenceRecovery Decode(string text) => JsonUtility.FromJson<SequenceRecovery>(text);
    }
}
