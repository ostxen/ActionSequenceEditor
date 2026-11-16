using NBC.ActionEditor.Model;
using System;

namespace NBC.ActionEditor.Application.Editing
{
    // 剪贴板归共享会话，窗口只发送复制或粘贴意图。
    public sealed class ClipboardState
    {
        public string Snapshot { get; internal set; }
        public Type Type { get; internal set; }
        public IDirectable CutSource { get; internal set; }
        public IDirectable Item { get; set; }
        public bool IsCut { get; set; }
    }
}
