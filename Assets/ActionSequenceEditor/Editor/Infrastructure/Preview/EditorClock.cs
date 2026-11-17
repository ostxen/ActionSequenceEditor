using System;
using ActionSequenceEditor.Application.Ports;
using UnityEditor;

namespace ActionSequenceEditor.Infrastructure.Preview
{
    // 读取当前 Editor 的单调时钟，不自行注册更新回调。
    public sealed class EditorClock : IEditorClock
    {
        public double Now => EditorApplication.timeSinceStartup;
        public float PlaybackTime => UnityEngine.Time.realtimeSinceStartup;
        public float TimeScale => UnityEngine.Time.timeScale;
        public DateTime LocalTime => DateTime.Now;
    }
}
