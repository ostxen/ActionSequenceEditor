using UnityEditor;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    // 将Inspector绘制委托给仍存在的发起窗口。
    [CustomEditor(typeof(UIToolkitInspectorTarget))]
    public sealed class UIToolkitSelectionInspector : Editor
    {
        public override VisualElement CreateInspectorGUI() => ((UIToolkitInspectorTarget)target).Owner?.CreatePropertyView() ?? new Label("窗口已关闭");
    }
}
