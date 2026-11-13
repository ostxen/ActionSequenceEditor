using System;
using NBC.ActionEditor.Application.Ports;
using UnityEditor;

namespace NBC.ActionEditor.Infrastructure
{
    // 读取当前 Editor 的单调时钟，不自行注册更新回调。
    public sealed class EditorClock : IEditorClock
    {
        public double Now => EditorApplication.timeSinceStartup;
        public float PlaybackTime => UnityEngine.Time.realtimeSinceStartup;
        public float TimeScale => UnityEngine.Time.timeScale;
        public DateTime LocalTime => DateTime.Now;
    }

    // SessionState 只保存本工具的恢复键，不写作者文件。
    public sealed class RecoveryStore : IRecoveryStore
    {
        private const string Key = "ActionEditor.JsonRecovery";
        public string Read() => SessionState.GetString(Key, "");
        public void Write(string snapshot) => SessionState.SetString(Key, snapshot);
    }
}
