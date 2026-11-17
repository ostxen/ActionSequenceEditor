using ActionSequenceEditor.Presentation.Presenters;
using ActionSequenceEditor.Presentation.Timeline;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views.IMGUI
{
    // hotControl与原生事件仅归此窗口，业务手势和令牌仍由共享Presenter持有。
    internal sealed class TimelineInputAdapter
    {
        private readonly TimelinePresenter Presenter;
        private readonly TimelineControls controls;
        private TimelineRenderState State => Presenter.State;
        private bool captured;
        private int captureId;
        public TimelineInputAdapter(TimelinePresenter presenter, TimelineControls controls)
        { Presenter = presenter; this.controls = controls; }
        // 稳定控件ID在每帧相同顺序申请，外部捕获丢失时取消本窗口手势。
        public void Begin()
        {
            captureId = GUIUtility.GetControlID("ActionTimelineCapture".GetHashCode(), FocusType.Passive);
            if (captured && GUIUtility.hotControl != captureId) Presenter.Cancel();
        }
        public void SetCapture(bool value)
        {
            captured = value;
            // 失焦和文档替换可能不在OnGUI内，仍须释放本窗口留下的原生捕获。
            if (value) { if (Event.current != null) GUIUtility.hotControl = captureId; }
            else if (GUIUtility.hotControl == captureId) GUIUtility.hotControl = 0;
        }
        // 主体坐标减去工具栏高度；捕获后的松开仍传递到共享状态机校验按钮与越界。
        public void Handle(Event e, Rect body)
        {
            if (e.type == EventType.Used) return;
            if (e.type == EventType.KeyDown)
            {
                if (EditorGUIUtility.editingTextField) return;
                TimelineKey? key = e.keyCode == KeyCode.Escape ? TimelineKey.Escape : e.keyCode == KeyCode.Delete ? TimelineKey.Delete : (TimelineKey?)null;
                if (e.control || e.command)
                    switch (e.keyCode)
                    {
                        case KeyCode.C: key = TimelineKey.Copy; break; case KeyCode.X: key = TimelineKey.Cut; break;
                        case KeyCode.V: key = TimelineKey.Paste; break; case KeyCode.D: key = TimelineKey.Duplicate; break;
                        case KeyCode.S: key = TimelineKey.Save; break; case KeyCode.Z: key = e.shift ? TimelineKey.Redo : TimelineKey.Undo; break;
                    }
                if (key.HasValue) { Presenter.Key(new KeyInput(key.Value)); e.Use(); } return;
            }
            var point = e.mousePosition; var ruler = point.y >= 20 && point.y < 40 && point.x >= State.ContentLeft;
            if (e.type == EventType.ScrollWheel && (body.Contains(point) || ruler))
            { Presenter.Wheel(new WheelInput(point.x, point.y - body.y, e.delta.y)); e.Use(); return; }
            if (e.type == EventType.ContextClick && body.Contains(point))
            { controls.ShowMenu(Presenter.Menu(point.x, point.y - body.y)); e.Use(); return; }
            var region = Presenter.Mode == TimelineGestureMode.Seek || ruler ? TimelineRegion.Ruler : TimelineRegion.Body;
            var y = region == TimelineRegion.Ruler ? (point.y >= 0 && point.y < body.yMax ? 0 : -1) : point.y - body.y;
            var button = e.button == 2 ? PointerButton.Middle : e.button == 1 ? PointerButton.Secondary : PointerButton.Primary;
            if (e.type == EventType.MouseDown && (body.Contains(point) || ruler))
            { if (e.button == 1) return; GUI.FocusControl(null); Presenter.Pointer(new PointerInput(PointerAction.Down, point.x, y, button, e.control, e.shift, region)); if (captured) e.Use(); }
            else if (e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
            { Presenter.Pointer(new PointerInput(PointerAction.Move, point.x, y, button, e.control, e.shift, region)); if (captured) e.Use(); }
            else if (e.rawType == EventType.MouseUp && captured)
            { Presenter.Pointer(new PointerInput(PointerAction.Up, point.x, y, button, e.control, e.shift, region)); e.Use(); }
        }
    }
}
