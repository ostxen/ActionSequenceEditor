using ActionSequenceEditor.Samples.FeatureSample;

namespace ActionSequenceEditor.Samples
{
    // 样例的Toolkit工具栏适配独立声明，不借用IMGUI实现。
    [ToolkitHeader(typeof(FeatureSampleSequence))]
    public sealed class FeatureSampleToolkitHeader : ToolkitHeaderView
    {
        public override UnityEngine.UIElements.VisualElement CreateGUI(EditorExtensionContext context) =>
            new UnityEditor.UIElements.ToolbarButton(() => FeatureSamplePreviewWindow.ShowFor(context)) { text = EditorText.PreviewTitle };
    }
}
