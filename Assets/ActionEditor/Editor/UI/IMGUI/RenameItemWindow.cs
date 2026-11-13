using NBC.ActionEditor.Storage;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public sealed class RenameItemWindow : EditorWindow
    {
        private IDirectable item;
        private string newName;
        public static void Show(IDirectable target)
        {
            var window = CreateInstance<RenameItemWindow>(); window.item = target; window.newName = target.Name;
            window.titleContent = new GUIContent("重命名"); window.ShowUtility(); window.position = new Rect(200, 200, 350, 80);
        }
        private void OnGUI()
        {
            newName = EditorGUILayout.TextField("名称", newName);
            if (GUILayout.Button("确定")) { App.Context.Structure.Rename(item, newName); Close(); }
        }
    }
}
