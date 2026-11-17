using ActionSequenceEditor.Application.Ports;
using UnityEditor;

namespace ActionSequenceEditor.Infrastructure.Sequences
{
    // SessionState 只保存本工具的恢复键，不写作者文件。
    public sealed class SessionStateRecoveryStore : IRecoveryStore
    {
        private const string Key = "ActionSequenceEditor.JsonRecovery";
        public string Read() => SessionState.GetString(Key, "");
        public void Write(string snapshot) => SessionState.SetString(Key, snapshot);
    }
}
