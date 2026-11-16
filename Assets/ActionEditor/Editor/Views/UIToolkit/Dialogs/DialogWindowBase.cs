using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 异常留在发起窗口；域重载失去回调后关闭，不回退到全局会话。
    public abstract class DialogWindowBase : EditorWindow
    {
        private HelpBox error;
        protected void Begin(string title)
        {
            titleContent = new GUIContent(title);
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = rootVisualElement.style.paddingBottom = 8;
            error = new HelpBox("", HelpBoxMessageType.Error) { name = "dialog-error" };
            error.style.display = DisplayStyle.None;
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                evt.StopPropagation(); Close();
            }, TrickleDown.TrickleDown);
        }
        protected void Finish(string title, Action submit)
        {
            rootVisualElement.Add(error);
            rootVisualElement.Add(new Button(() => Run(submit)) { text = title, name = "dialog-submit" });
            rootVisualElement.Add(new Button(Close) { text = EditorText.TipsCancel, name = "dialog-cancel" });
        }
        protected void Run(Action action)
        {
            try { action(); error.style.display = DisplayStyle.None; }
            catch (Exception exception) { error.text = exception.Message; error.style.display = DisplayStyle.Flex; }
        }
        protected void LostBinding() => Close();
    }
}
