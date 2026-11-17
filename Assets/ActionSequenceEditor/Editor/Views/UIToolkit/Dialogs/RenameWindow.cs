using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ActionSequenceEditor.Views.UIToolkit
{
    public sealed class RenameWindow : DialogWindowBase
    {
        private string initialName;
        private Action<string> submit;
        public static void ShowFor(string initialName, Action<string> submit)
        {
            if (submit == null) throw new ArgumentNullException(nameof(submit));
            var window = CreateInstance<RenameWindow>(); window.initialName = initialName; window.submit = submit;
            window.minSize = new Vector2(350, 110); window.ShowUtility();
        }
        public void CreateGUI()
        {
            if (submit == null) { LostBinding(); return; }
            Begin(EditorText.Rename);
            var name = new TextField(EditorText.Rename) { value = initialName, name = "rename-name" };
            rootVisualElement.Add(name);
            Action confirm = () => { submit(name.value); Close(); };
            Finish(EditorText.TipsConfirm, confirm);
            name.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopPropagation(); Run(confirm);
            });
            name.schedule.Execute(name.Focus);
        }
        private void OnDisable() { submit = null; initialName = null; }
    }
}
