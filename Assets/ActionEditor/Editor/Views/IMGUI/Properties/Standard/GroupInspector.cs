using NBC.ActionEditor.Domain;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Properties.Standard
{
    // 为组属性扩展提供明确类型的目标访问。
    public abstract class GroupInspector<T> : GroupInspector where T : TimelineGroup
    {
        protected T action => (T)target;
    }

    // 绘制组名称和公共字段，提交交由属性Presenter。
    [CustomInspectors(typeof(TimelineGroup), true)]
    public class GroupInspector : InspectorsBase
    {
        private TimelineGroup action => (TimelineGroup)target;

        public override void OnInspectorGUI()
        {
            ShowCommonInspector();
        }

        protected void ShowCommonInspector(bool showBaseInspector = true)
        {
            var name = EditorGUILayout.DelayedTextField("Name", action.Name);
            if (name != action.Name) Presenter.Rename(action, name);
            if (showBaseInspector)
            {
                base.OnInspectorGUI();
            }
        }
    }
}