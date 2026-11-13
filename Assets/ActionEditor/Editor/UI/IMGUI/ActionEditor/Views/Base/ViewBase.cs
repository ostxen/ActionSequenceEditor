using System;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    // 欢迎页和工具栏的布局基类，不再保存或分发时间轴手势。
    public abstract class ViewBase
    {
        protected EditorWindow Window;
        protected NBC.ActionEditor.Presentation.Timeline.TimelineWorkspace Workspace => ((ActionEditorWindow)Window).Workspace;
        public Rect Position;
        public bool Visible { get; set; }
        public void Init(EditorWindow window) { Window = window; Visible = true; OnInit(); }
        public void Update() { if (Position.width > 0 || Position.height > 0) OnUpdate(); }
        protected virtual void OnInit() { }
        protected virtual void OnUpdate() { }
        public virtual void OnGUI(Rect rect)
        {
            Position = rect;
            try { OnDraw(); }
            catch (ExitGUIException) { throw; }
            catch (Exception error)
            {
                (Window as ActionEditorWindow)?.CancelOwnEdit();
                Debug.LogException(error);
            }
        }
        public abstract void OnDraw();
    }
}