using ActionSequenceEditor.Samples.FeatureSample;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Samples
{
    // 从IMGUI工具栏打开绑定当前宿主的样例预览。
    [CustomHeader(typeof(FeatureSampleSequence))]
    public sealed class FeatureSampleHeader : SequenceHeaderExtension
    {
        public override void OnGUI(EditorExtensionContext context)
        {
            if (GUILayout.Button(EditorText.PreviewTitle, EditorStyles.toolbarButton))
                FeatureSamplePreviewWindow.ShowFor(context);
        }
    }
}
