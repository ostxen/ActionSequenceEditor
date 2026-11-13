using NBC.ActionEditor.Domain;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Design.Editor.Inspectors
{
    public abstract class GroupInspector<T> : GroupInspector where T : TimelineGroup
    {
        protected T action => (T)target;
    }

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