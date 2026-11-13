using NBC.ActionEditor.Domain;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public abstract class TrackInspector<T> : TrackInspector where T : TimelineTrack
    {
        protected T action => (T)target;
    }

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