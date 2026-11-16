using NBC.ActionEditor.Model;
using UnityEditor;

namespace NBC.ActionEditor.Views.IMGUI.Properties.Standard
{
    // 文档公共信息以只读值显示，业务字段仍由统一属性控件绘制。
    [CustomInspectors(typeof(ActionSequence), true)]
    public class SequenceInspector : PropertyInspectorBase
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Length", Presenter.SequenceLength.ToString());
            base.OnInspectorGUI();
        }
    }
}