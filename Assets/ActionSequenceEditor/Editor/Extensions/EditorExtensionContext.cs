using System;

namespace ActionSequenceEditor
{
    // 扩展只捕获显示值，不取得作者图、路径或默认宿主。
    public sealed class EditorExtensionContext
    {
        private readonly Func<ActionSequenceEditor.Presentation.Contracts.ExtensionSequenceState> capture;
        public ActionSequenceEditor.Presentation.Contracts.ExtensionSequenceState Capture() => capture();
        public EditorExtensionContext(Func<ActionSequenceEditor.Presentation.Contracts.ExtensionSequenceState> capture)
        { this.capture = capture ?? throw new ArgumentNullException(nameof(capture)); }
    }
}
