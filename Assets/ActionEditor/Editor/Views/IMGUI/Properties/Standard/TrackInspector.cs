using NBC.ActionEditor.Domain;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Views.IMGUI.Properties.Standard
{
    // 为轨道属性扩展提供明确类型的目标访问。
    public abstract class TrackInspector<T> : TrackInspector where T : TimelineTrack
    {
        protected T action => (T)target;
    }

    // 绘制轨道名称和公共字段，提交交由属性Presenter。
    [CustomInspectors(typeof(TimelineTrack), true)]
    public class TrackInspector : InspectorsBase
    {
        private TimelineTrack action => (TimelineTrack)target;

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