using UnityEngine.UIElements;

namespace ActionSequenceEditor
{
    // 原生Toolkit Header不承载IMGUI绘制回调。
    public abstract class ToolkitHeaderView
    { public abstract VisualElement CreateGUI(EditorExtensionContext context); }
}
