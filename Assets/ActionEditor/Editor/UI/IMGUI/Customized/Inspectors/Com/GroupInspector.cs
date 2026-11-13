using NBC.ActionEditor.Domain;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Design.Editor.Inspectors
{
    public abstract class GroupInspector<T> : ActionClipInspector where T : TimelineGroup
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
            action.Name = EditorGUILayout.TextField("Name", action.Name);
            if (showBaseInspector)
            {
                base.OnInspectorGUI();
            }
        }
    }
}