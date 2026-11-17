using ActionSequenceEditor.Presentation.Properties;
using ActionSequenceEditor.Model;
using ActionSequenceEditor;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI.Properties.Standard
{
    // 绘制轨道名称和公共字段，提交交由属性Presenter。
    [CustomInspectors(typeof(ActionTrack), true)]
    public class TrackInspector : PropertyInspectorBase
    {
        private ActionSequenceEditor.Presentation.Properties.PropertyTargetState action => target;

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