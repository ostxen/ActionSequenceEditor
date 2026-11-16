using System;
using NBC.ActionEditor.Presentation.Presenters;
using NBC.ActionEditor.Presentation.Timeline;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Views.UIToolkit
{
    // 只转换原生局部坐标、焦点和捕获，手势及快捷键规则由Presenter执行。
    internal sealed class TimelineInputAdapter : IDisposable
    {
        private readonly TimelineCanvas canvas;
        private readonly VisualElement root;
        private readonly TimelinePresenter presenter;
        private int pointer = -1;
        private bool releasing, disposed;
        private TimelineRegion region;
        public int PointerId => pointer;
        public TimelineInputAdapter(TimelineCanvas canvas, VisualElement root, TimelinePresenter presenter)
        {
            this.canvas = canvas; this.root = root; this.presenter = presenter;
            canvas.RegisterCallback<PointerDownEvent>(Down); canvas.RegisterCallback<PointerMoveEvent>(Move); canvas.RegisterCallback<PointerUpEvent>(Up);
            canvas.RegisterCallback<PointerCaptureOutEvent>(CaptureOut); canvas.RegisterCallback<PointerLeaveEvent>(Leave);
            canvas.RegisterCallback<WheelEvent>(Wheel); canvas.RegisterCallback<DetachFromPanelEvent>(Detach);
            root.RegisterCallback<PointerDownEvent>(HeaderDown); root.RegisterCallback<PointerMoveEvent>(HeaderMove); root.RegisterCallback<PointerUpEvent>(HeaderUp);
            root.RegisterCallback<WheelEvent>(HeaderWheel); root.RegisterCallback<KeyDownEvent>(Key, TrickleDown.TrickleDown);
        }
        public void Capture(bool value)
        {
            if (value) { if (pointer >= 0) canvas.CapturePointer(pointer); return; }
            var previous = pointer; pointer = -1;
            if (previous < 0 || !canvas.HasPointerCapture(previous)) return;
            releasing = true;
            try { canvas.ReleasePointer(previous); } finally { releasing = false; }
        }
        private static PointerButton Button(int value) => value == 2 ? PointerButton.Middle : value == 1 ? PointerButton.Secondary : PointerButton.Primary;
        private void Down(PointerDownEvent e)
        {
            if (pointer >= 0) return;
            if (e.button == 1 || IsControl(e.target as VisualElement)) return;
            canvas.Focus(); pointer = e.pointerId; region = TimelineRegion.Body;
            var point = canvas.WorldToLocal(e.position);
            presenter.Pointer(new PointerInput(PointerAction.Down, point.x, point.y, Button(e.button), e.ctrlKey, e.shiftKey));
            if (presenter.Mode == TimelineGestureMode.Idle) pointer = -1;
            e.StopPropagation();
        }
        private static bool IsControl(VisualElement target)
        {
            for (var item = target; item != null; item = item.parent)
                if (item is Button || item.name == "timeline-welcome") return true;
            return false;
        }
        private void Move(PointerMoveEvent e)
        {
            if (region == TimelineRegion.Ruler && pointer >= 0) { SendRuler(PointerAction.Move, e.position, e.button, e.ctrlKey, e.shiftKey); e.StopPropagation(); return; }
            if (pointer >= 0 && pointer != e.pointerId) return;
            var point = canvas.WorldToLocal(e.position);
            presenter.Pointer(new PointerInput(PointerAction.Move, point.x, point.y, Button(e.button), e.ctrlKey, e.shiftKey));
            if (pointer >= 0) e.StopPropagation();
        }
        private void Up(PointerUpEvent e)
        {
            if (pointer != e.pointerId) return;
            if (region == TimelineRegion.Ruler) SendRuler(PointerAction.Up, e.position, e.button, e.ctrlKey, e.shiftKey);
            else
            {
                var point = canvas.WorldToLocal(e.position);
                presenter.Pointer(new PointerInput(PointerAction.Up, point.x, point.y, Button(e.button), e.ctrlKey, e.shiftKey));
            }
            e.PreventDefault(); e.StopImmediatePropagation();
        }
        private bool InRuler(Vector2 point) => point.y >= 20 && point.y < 40 && point.x >= canvas.Left;
        private void HeaderDown(PointerDownEvent e)
        {
            if (pointer >= 0) return;
            var point = root.WorldToLocal(e.position); if (e.button != 0 || !InRuler(point)) return;
            canvas.Focus(); pointer = e.pointerId; region = TimelineRegion.Ruler;
            presenter.Pointer(new PointerInput(PointerAction.Down, point.x, 0, PointerButton.Primary, e.ctrlKey, e.shiftKey, TimelineRegion.Ruler));
            e.PreventDefault(); e.StopPropagation();
        }
        private void SendRuler(PointerAction action, Vector3 position, int button, bool control, bool shift)
        {
            var point = root.WorldToLocal(position);
            var inside = point.x >= 0 && point.x < root.contentRect.width && point.y >= 0 && point.y < root.contentRect.height;
            presenter.Pointer(new PointerInput(action, inside ? point.x : -1, 0, Button(button), control, shift, TimelineRegion.Ruler));
        }
        private void HeaderMove(PointerMoveEvent e)
        { if (region == TimelineRegion.Ruler && pointer == e.pointerId) { SendRuler(PointerAction.Move, e.position, e.button, e.ctrlKey, e.shiftKey); e.StopPropagation(); } }
        private void HeaderUp(PointerUpEvent e)
        { if (region == TimelineRegion.Ruler && pointer == e.pointerId) Up(e); }
        private void Wheel(WheelEvent e)
        { var point = canvas.WorldToLocal(e.mousePosition); presenter.Wheel(new WheelInput(point.x, point.y, e.delta.y)); e.StopPropagation(); }
        private void HeaderWheel(WheelEvent e)
        { var point = root.WorldToLocal(e.mousePosition); if (!InRuler(point)) return; presenter.Wheel(new WheelInput(point.x, 0, e.delta.y)); e.StopPropagation(); }
        private void Key(KeyDownEvent e)
        {
            for (var item = e.target as VisualElement; item != null; item = item.parent)
                if (item.ClassListContains("unity-base-text-field")) return;
            TimelineKey? key = e.keyCode == KeyCode.Escape ? TimelineKey.Escape : e.keyCode == KeyCode.Delete ? TimelineKey.Delete : (TimelineKey?)null;
            if (e.ctrlKey || e.commandKey)
                switch (e.keyCode)
                {
                    case KeyCode.C: key = TimelineKey.Copy; break;
                    case KeyCode.X: key = TimelineKey.Cut; break;
                    case KeyCode.V: key = TimelineKey.Paste; break;
                    case KeyCode.D: key = TimelineKey.Duplicate; break;
                    case KeyCode.S: key = TimelineKey.Save; break;
                    case KeyCode.Z: key = e.shiftKey ? TimelineKey.Redo : TimelineKey.Undo; break;
                }
            if (!key.HasValue) return;
            presenter.Key(new KeyInput(key.Value)); e.StopPropagation(); e.PreventDefault();
        }
        private void CaptureOut(PointerCaptureOutEvent e) { if (!releasing && pointer >= 0) presenter.Cancel(); }
        private void Leave(PointerLeaveEvent e) { if (pointer < 0) presenter.Pointer(new PointerInput(PointerAction.Move, -1, -1)); }
        private void Detach(DetachFromPanelEvent e) { if (!disposed) presenter.Cancel(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            canvas.UnregisterCallback<PointerDownEvent>(Down); canvas.UnregisterCallback<PointerMoveEvent>(Move); canvas.UnregisterCallback<PointerUpEvent>(Up);
            canvas.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut); canvas.UnregisterCallback<PointerLeaveEvent>(Leave);
            canvas.UnregisterCallback<WheelEvent>(Wheel); canvas.UnregisterCallback<DetachFromPanelEvent>(Detach);
            root.UnregisterCallback<PointerDownEvent>(HeaderDown); root.UnregisterCallback<PointerMoveEvent>(HeaderMove); root.UnregisterCallback<PointerUpEvent>(HeaderUp);
            root.UnregisterCallback<WheelEvent>(HeaderWheel); root.UnregisterCallback<KeyDownEvent>(Key, TrickleDown.TrickleDown);
        }
    }
}
