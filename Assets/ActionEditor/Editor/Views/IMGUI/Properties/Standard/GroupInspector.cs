using NBC.ActionEditor.Presentation.Properties;
using NBC.ActionEditor.Model;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Properties.Standard
{
    // 绘制组名称和公共字段，提交交由属性Presenter。
    [CustomInspectors(typeof(ActionGroup), true)]
    public class GroupInspector : PropertyInspectorBase
    {
        private NBC.ActionEditor.Presentation.Properties.PropertyTargetState action => target;

        public override void OnInspectorGUI()
        {
            ShowCommonInspector();
        }

        protected void ShowCommonInspector(bool showBaseInspector = true)
        {
            var name = EditorGUILayout.DelayedTextField("Name", action.Name);
            if (name != action.Name) Presenter.Rename(action.Id, name);
            if (showBaseInspector)
            {
                base.OnInspectorGUI();
            }
        }
    }
}