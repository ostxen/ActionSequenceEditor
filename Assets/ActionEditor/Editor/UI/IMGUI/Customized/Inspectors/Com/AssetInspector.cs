using NBC.ActionEditor.Domain;
using UnityEditor;

namespace NBC.ActionEditor
{
    [CustomInspectors(typeof(ActionDocument), true)]
    public class AssetInspector : InspectorsBase
    {
        private ActionDocument action => (ActionDocument)target;

        public override void OnInspectorGUI()
        {
            ShowCommonInspector();
            base.OnInspectorGUI();
        }

        protected void ShowCommonInspector(bool showBaseInspector = true)
        {
            EditorGUILayout.TextField("Name", action.Length.ToString());
        }
    }
}