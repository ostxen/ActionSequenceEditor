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
            action.Name = EditorGUILayout.TextField("Name", action.Name);
            if (showBaseInspector)
            {
                base.OnInspectorGUI();
            }
        }
    }
}