using NBC.ActionEditor.Domain;
using UnityEditor;

namespace NBC.ActionEditor
{
    // 文档公共信息以只读值显示，业务字段仍由统一属性控件绘制。
    [CustomInspectors(typeof(ActionDocument), true)]
    public class AssetInspector : InspectorsBase
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Length", Presenter.DocumentLength.ToString());
            base.OnInspectorGUI();
        }
    }
}