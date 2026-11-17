using System;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 收集名称并交回调用者，不直接修改作者图。
    public sealed class RenameWindow : EditorWindow
    {
        private string newName;
        private string error;
        private Action<string> rename;
        public static void Show(string initialName, Action<string> rename)
        {
            if (rename == null) throw new ArgumentNullException(nameof(rename));
            var window = CreateInstance<RenameWindow>(); window.newName = initialName; window.rename = rename;
            window.titleContent = new GUIContent("重命名"); window.ShowUtility(); window.position = new Rect(200, 200, 350, 80);
        }
        private void OnGUI()
        {
            newName = EditorGUILayout.TextField("名称", newName);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (GUILayout.Button("确定"))
            {
                try { rename(newName); Close(); }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { error = exception.Message; }
            }
        }
    }
}
