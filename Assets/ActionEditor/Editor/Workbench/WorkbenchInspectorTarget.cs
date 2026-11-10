using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Workbench
{
    public sealed class WorkbenchInspectorTarget : ScriptableObject
    {
        [System.NonSerialized] public ActionWorkbench Owner;
    }

    [CustomEditor(typeof(WorkbenchInspectorTarget))]
    public sealed class WorkbenchSelectionInspector : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var owner = ((WorkbenchInspectorTarget)target).Owner;
            return owner != null ? owner.CreatePropertyView() : new Label("工作台已关闭，请重新选择。");
        }
    }
}
