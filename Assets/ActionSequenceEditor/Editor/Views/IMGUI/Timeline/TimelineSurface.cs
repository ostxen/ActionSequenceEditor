using System;
using ActionSequenceEditor.Presentation.Contracts;
using ActionSequenceEditor.Presentation.Presenters;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // 逐窗表面装配原生控件、绘制和输入适配，不持有作者手势状态。
    public sealed class TimelineSurface : ITimelineView, IDisposable
    {
        private readonly EditorWindow window;
        private readonly TimelineControls controls;
        private readonly TimelineInputAdapter input;
        private bool disposed;
        public TimelinePresenter Presenter { get; }
        public TimelineRenderState State { get; private set; }
        public string Error { get; private set; }
        public TimelineSurface(EditorWindow window, Composition.WindowBinding binding, TimelineWorkspace workspace)
        {
            this.window = window;
            Presenter = binding.CreateTimeline(workspace, this);
            controls = new TimelineControls(Presenter);
            input = new TimelineInputAdapter(Presenter, controls);
        }
        public void Render(TimelineRenderState state) { State = state; window.Repaint(); }
        public void ShowError(string message) => Error = message;
        public void SetCapture(bool value) => input?.SetCapture(value);
        public GenericMenu ContextMenu(float x, float y) => controls.ContextMenu(x, y);

        // 主体先裁剪，标尺与全宽插入线在窗口坐标绘制，避免被标题区域截断。
        public void Draw(Rect bounds, Action drawHeader)
        {
            if (disposed) return;
            input.Begin();
            var body = new Rect(0, 40, bounds.width, Mathf.Max(0, bounds.height - 56));
            Presenter.Resize(new TimelineSize(body.width, body.height, 28, bounds.width, 9));
            var previousColor = GUI.color;
            try { drawHeader(); }
            finally { GUI.color = previousColor; }
            GUILayout.BeginArea(body);
            try { TimelineRenderer.Body(State); controls.DrawHeaders(); }
            finally { GUILayout.EndArea(); }
            TimelineRenderer.Ruler(State);
            controls.DrawFooter(bounds);
            controls.DrawScroll(body);
            input.Handle(Event.current, body);
            TimelineRenderer.Insertion(State.Insertion, body.y);
            if (State.ResizeCursor) EditorGUIUtility.AddCursorRect(body, MouseCursor.ResizeHorizontal);
            if (!string.IsNullOrEmpty(Error)) GUI.Label(new Rect(0, bounds.height - 36, bounds.width, 20), Error);
        }
        public void Dispose() { if (disposed) return; disposed = true; Presenter.Dispose(); State = null; }
    }
}
