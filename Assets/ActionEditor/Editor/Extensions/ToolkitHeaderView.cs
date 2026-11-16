using UnityEngine.UIElements;

namespace NBC.ActionEditor
{
    // 原生Toolkit Header不承载IMGUI绘制回调。
    public abstract class ToolkitHeaderView
    { public abstract VisualElement CreateGUI(EditorExtensionContext context); }
}
