using System;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public sealed class RenameItemWindow : EditorWindow
    {
        private IDirectable item;
        private string newName;
        private Action<IDirectable, string> rename;
        public static void Show(IDirectable target, Action<IDirectable, string> rename)
        {
            var window = CreateInstance<RenameItemWindow>(); window.item = target; window.newName = target.Name; window.rename = rename;
            window.titleContent = new GUIContent("重命名"); window.ShowUtility(); window.position = new Rect(200, 200, 350, 80);
        }
        private void OnGUI()
        {
            newName = EditorGUILayout.TextField("名称", newName);
            if (GUILayout.Button("确定")) { rename(item, newName); Close(); }
        }
    }
}
