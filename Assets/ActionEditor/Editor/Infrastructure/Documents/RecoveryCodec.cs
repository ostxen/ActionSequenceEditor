using NBC.ActionEditor.Application.Documents;
using NBC.ActionEditor.Application.Ports;
using UnityEngine;

namespace NBC.ActionEditor.Infrastructure.Documents
{
    // 恢复封套单独验证版本，作者内容仍由快照端口解析。
    public sealed class RecoveryCodec : IRecoveryCodec
    {
        public string Encode(DocumentRecovery recovery) => JsonUtility.ToJson(recovery);
        public DocumentRecovery Decode(string text) => JsonUtility.FromJson<DocumentRecovery>(text);
    }
}
